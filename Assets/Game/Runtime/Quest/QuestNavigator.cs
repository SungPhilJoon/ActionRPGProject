using System.Collections.Generic;
using Framework;
using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// 퀘스트 길 안내: 플레이어 → 퀘스트 장소(QuestManager.TryGetGuidePosition)까지 NavMesh 경로.
///  - 길(NavMesh Road 영역) 비용 1, 길 밖(Walkable) 비용 ConstTable.OffRoadCost → 경로가 흙길을 따라간다.
///  - 경로는 RefreshInterval 마다 또는 플레이어가 RefreshDistance 이상 움직이면 다시 구한다. 첫 꼭짓점은 매 프레임 플레이어 위치.
///  - 경로를 못 구하면(NavMesh 밖 등) 직선 두 점으로 대체한다.
/// 미니맵 · 월드맵(MapView)이 꺾은선으로, 캐릭터 화살표(QuestGuideArrow)가 다음 꼭짓점 방향으로, HUD 가 남은 거리로 쓴다.
/// </summary>
public class QuestNavigator : IManager
{
    private const float RefreshInterval = 0.5f;
    private const float RefreshDistance = 2.0f;
    private const float StartProbe = 3.0f;
    /// <summary>퀘스트 장소(몬스터 지역 중심, 상인 등)는 NavMesh 위가 아닐 수 있어 넓게 찾는다.</summary>
    private const float TargetProbe = 10.0f;
    /// <summary>이 거리 안의 꼭짓점은 지난 것으로 보고 화살표가 다음 꼭짓점을 가리킨다.</summary>
    private const float CornerReach = 1.5f;

    private readonly List<Vector3> CornerList = new List<Vector3>();
    private NavMeshPath Path;
    private float RefreshRemaining;
    private Vector3 LastFrom;
    private Vector3 LastTarget;
    private GameObject Arrow;

    /// <summary>안내할 곳이 있는가 (퀘스트 장소가 있는 퀘스트).</summary>
    public bool HasGuide { get; private set; }
    /// <summary>NavMesh 경로로 이어졌는가 (false 면 Corners 는 직선 두 점).</summary>
    public bool OnPath { get; private set; }
    /// <summary>남은 경로 길이 (m, 수평).</summary>
    public float Distance { get; private set; }
    public Vector3 Target { get; private set; }

    /// <summary>[0] = 플레이어, 마지막 = 퀘스트 장소.</summary>
    public IReadOnlyList<Vector3> Corners
    {
        get { return CornerList; }
    }

    void IManager.Initialize()
    {
        Path = new NavMeshPath();
        NavMesh.SetAreaCost(NavAreaId.Road, 1.0f);
        NavMesh.SetAreaCost(NavAreaId.Walkable, Mathf.Max(1.0f, Tables.Const(EConst.OffRoadCost)));
        EventBus<QuestChangedEvent>.Subscribe(onQuestChanged);
        EventBus<PlayerSpawnedEvent>.Subscribe(onPlayerSpawned);
    }

    void IManager.UnInitialize()
    {
        EventBus<QuestChangedEvent>.Unsubscribe(onQuestChanged);
        EventBus<PlayerSpawnedEvent>.Unsubscribe(onPlayerSpawned);
        if (Arrow != null)
        {
            Object.Destroy(Arrow);
            Arrow = null;
        }

        CornerList.Clear();
        HasGuide = false;
    }

    void IManager.Update(float InDeltaTime)
    {
        var player = PlayerCharacter.Local;
        var quests = Managers.Get<QuestManager>();
        Vector3 target;
        if (player == null || quests == null || quests.TryGetGuidePosition(player.transform.position, out target) == false)
        {
            clear();
            return;
        }

        Vector3 from = player.transform.position;
        RefreshRemaining -= InDeltaTime;
        bool moved = MathUtil.Flat(from - LastFrom).sqrMagnitude > RefreshDistance * RefreshDistance;
        bool retarget = HasGuide == false || MathUtil.Flat(target - LastTarget).sqrMagnitude > 1.0f;
        if (RefreshRemaining <= 0.0f || moved == true || retarget == true)
        {
            rebuild(from, target);
        }
        else
        {
            trim(from);
        }
    }

    /// <summary>화살표가 가리킬 다음 꼭짓점 (플레이어에서 CornerReach 보다 먼 첫 꼭짓점).</summary>
    public bool TryGetNextCorner(out Vector3 OutCorner)
    {
        OutCorner = Vector3.zero;
        if (HasGuide == false || CornerList.Count < 2)
        {
            return false;
        }

        OutCorner = CornerList[1];
        return true;
    }

    // ------------------------------------------------------------------ path

    private void rebuild(Vector3 InFrom, Vector3 InTarget)
    {
        RefreshRemaining = RefreshInterval;
        LastFrom = InFrom;
        LastTarget = InTarget;
        Target = InTarget;
        HasGuide = true;
        CornerList.Clear();

        NavMeshHit start;
        NavMeshHit end;
        bool found = NavMesh.SamplePosition(InFrom, out start, StartProbe, NavMesh.AllAreas) == true
                     && NavMesh.SamplePosition(onGround(InTarget), out end, TargetProbe, NavMesh.AllAreas) == true
                     && NavMesh.CalculatePath(start.position, end.position, NavMesh.AllAreas, Path) == true
                     && Path.status != NavMeshPathStatus.PathInvalid;
        OnPath = found;
        if (found == true)
        {
            var corners = Path.corners;
            CornerList.Add(InFrom);
            for (int i = 1; i < corners.Length; ++i)
            {
                CornerList.Add(corners[i]);
            }
        }
        else
        {
            CornerList.Add(InFrom);
            CornerList.Add(InTarget);
        }

        trim(InFrom);
    }

    /// <summary>첫 꼭짓점을 지금 플레이어 위치로 바꾸고, 이미 지나친 꼭짓점을 빼고, 남은 거리를 다시 잰다.</summary>
    private void trim(Vector3 InFrom)
    {
        if (CornerList.Count == 0)
        {
            return;
        }

        CornerList[0] = InFrom;
        while (CornerList.Count > 2 && MathUtil.Flat(CornerList[1] - InFrom).sqrMagnitude < CornerReach * CornerReach)
        {
            CornerList.RemoveAt(1);
        }

        float length = 0.0f;
        for (int i = 1; i < CornerList.Count; ++i)
        {
            length += MathUtil.Flat(CornerList[i] - CornerList[i - 1]).magnitude;
        }

        Distance = length;
    }

    /// <summary>퀘스트 장소 높이를 지형에 맞춘다 (SpawnTable 좌표는 y = 0 이라 고원 위 NavMesh 를 못 찾았다).</summary>
    private static Vector3 onGround(Vector3 InPosition)
    {
        var context = SceneContext.Current;
        if (context == null || context.Terrain == null)
        {
            return InPosition;
        }

        InPosition.y = context.Terrain.SampleHeight(InPosition) + context.Terrain.transform.position.y;
        return InPosition;
    }

    private void clear()
    {
        if (HasGuide == false)
        {
            return;
        }

        HasGuide = false;
        OnPath = false;
        Distance = 0.0f;
        CornerList.Clear();
    }

    private void onQuestChanged(QuestChangedEvent InEvent)
    {
        // 퀘스트가 바뀌면 다음 Update 에서 바로 다시 구한다.
        RefreshRemaining = 0.0f;
    }

    /// <summary>캐릭터 화살표는 플레이어가 생길 때 한 번 만든다 (QuestGuideArrow 가 스스로 따라다님).</summary>
    private void onPlayerSpawned(PlayerSpawnedEvent InEvent)
    {
        if (Arrow != null)
        {
            return;
        }

        var loader = Managers.Get<AssetLoader>();
        string address = Tables.Address(Tables.ConstInt(EConst.QuestArrowAsset));
        var prefab = loader != null && address != null ? loader.LoadSync<GameObject>(address) : null;
        if (prefab == null)
        {
            Log.Warning("QuestNavigator: quest arrow prefab missing (Game/Setup/3. Build Content)");
            return;
        }

        Arrow = Object.Instantiate(prefab);
    }
}
