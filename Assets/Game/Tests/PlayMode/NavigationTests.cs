using System.Collections;
using Framework;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.TestTools;

/// <summary>
/// 월드 리디자인 (2026-10-31): 걸을 수 있는 곳 = NavMesh, 길(Road) 우선 경로, 퀘스트 길 안내.
///  - 모든 스폰 지역(트롤 능선 포함)에서 시작 지점까지 NavMesh 경로가 이어진다 (예전 트롤 능선은 절벽 고리로 갇혔다)
///  - 시작 → 트롤 능선 경로의 대부분이 길 위
///  - 플레이어는 호수(걸을 수 없음)로 걸어 들어가지 못한다
///  - 퀘스트 길 안내: 경로 꼭짓점 · 남은 거리 · 캐릭터 화살표
/// </summary>
public class NavigationTests : GamePlayTestBase
{
    private const float SampleStep = 2.0f;

    [Test]
    public void EverySpawnArea_ReachesStart()
    {
        var path = new NavMeshPath();
        NavMeshHit start;
        Assert.IsTrue(NavMesh.SamplePosition(StartPosition, out start, 5.0f, NavMesh.AllAreas), "시작 지점이 NavMesh 위");
        foreach (var row in Tables.Root.Spawn.All)
        {
            if (row.Kind != ESpawnKind.Monster && row.Kind != ESpawnKind.Merchant)
            {
                continue;
            }

            NavMeshHit hit;
            Assert.IsTrue(NavMesh.SamplePosition(onGround(row.Position), out hit, 15.0f, NavMesh.AllAreas), $"Spawn {row.Index} 근처 NavMesh");
            Assert.IsTrue(NavMesh.CalculatePath(hit.position, start.position, NavMesh.AllAreas, path), $"Spawn {row.Index} 경로 계산");
            Assert.AreEqual(NavMeshPathStatus.PathComplete, path.status, $"Spawn {row.Index} ({row.Position}) → 시작 지점까지 걸어갈 수 있어야 함");
        }
    }

    [Test]
    public void PathToTrollRidge_FollowsRoads()
    {
        NavMeshHit start;
        NavMeshHit end;
        Assert.IsTrue(NavMesh.SamplePosition(StartPosition, out start, 5.0f, NavMesh.AllAreas));
        Assert.IsTrue(NavMesh.SamplePosition(onGround(new Vector3(250.0f, 0.0f, 385.0f)), out end, 15.0f, NavMesh.AllAreas), "트롤 능선 NavMesh");
        var path = new NavMeshPath();
        Assert.IsTrue(NavMesh.CalculatePath(start.position, end.position, NavMesh.AllAreas, path));
        Assert.AreEqual(NavMeshPathStatus.PathComplete, path.status);

        int total = 0;
        int onRoad = 0;
        var corners = path.corners;
        for (int i = 1; i < corners.Length; ++i)
        {
            float length = Vector3.Distance(corners[i - 1], corners[i]);
            int steps = Mathf.Max(1, Mathf.CeilToInt(length / SampleStep));
            for (int s = 0; s < steps; ++s)
            {
                var p = Vector3.Lerp(corners[i - 1], corners[i], (float)s / steps);
                NavMeshHit hit;
                total += 1;
                if (NavMesh.SamplePosition(p, out hit, 0.6f, NavAreaId.RoadMask) == true)
                {
                    onRoad += 1;
                }
            }
        }

        Assert.Greater((float)onRoad / total, 0.7f, $"경로의 70% 이상이 길 위 ({onRoad}/{total})");
    }

    private static Vector3 onGround(Vector3 InPosition)
    {
        var terrain = SceneContext.Current.Terrain;
        InPosition.y = terrain.SampleHeight(InPosition) + terrain.transform.position.y;
        return InPosition;
    }

    [UnityTest]
    public IEnumerator Player_CannotWalkIntoLake()
    {
        Player.AddTag((int)ETag.Cheat_God);
        var lake = new Vector3(170.0f, 0.0f, 262.0f);
        const float lakeRadius = 28.0f;
        NavMeshHit shore;
        Assert.IsTrue(NavMesh.SamplePosition(lake + Vector3.forward * -(lakeRadius + 8.0f), out shore, 10.0f, NavMesh.AllAreas), "호수 남쪽 물가");
        Player.Motor.Teleport(shore.position + Vector3.up * 0.2f);
        yield return null;

        var toLake = MathUtil.Flat(lake - Player.transform.position).normalized;
        yield return WaitSeconds(3.0f, () => Player.Motor.SetMoveInput(toLake));
        Player.Motor.SetMoveInput(Vector3.zero);

        float distance = MathUtil.Flat(Player.transform.position - lake).magnitude;
        Assert.Greater(distance, lakeRadius - 2.0f, $"호수 안으로 들어가면 안 됨 (중심까지 {distance:0.0}m)");
        NavMeshHit onMesh;
        Assert.IsTrue(NavMesh.SamplePosition(Player.transform.position, out onMesh, 1.0f, NavMesh.AllAreas), "NavMesh 위에 있어야 함");
        Player.RemoveTag((int)ETag.Cheat_God);
    }

    [UnityTest]
    public IEnumerator QuestGuide_PathAndArrow()
    {
        var quests = Managers.Get<QuestManager>();
        var navigator = Managers.Get<QuestNavigator>();
        Assert.IsTrue(quests.Begin(16), "트롤 퀘스트");
        yield return WaitFor(() => navigator.HasGuide == true && navigator.Corners.Count >= 2, 3.0f, "길 안내 경로");
        Assert.IsTrue(navigator.OnPath, "NavMesh 경로로 이어짐");
        Assert.Greater(navigator.Distance, MathUtil.Flat(navigator.Target - Player.transform.position).magnitude - 1.0f, "경로 길이 ≥ 직선 거리");

        var arrow = Object.FindAnyObjectByType<QuestGuideArrow>();
        Assert.IsNotNull(arrow, "캐릭터 화살표 생성");
        yield return null;
        Assert.IsTrue(arrow.Visual.gameObject.activeSelf, "멀리 있으면 화살표 표시");
        Vector3 corner;
        Assert.IsTrue(navigator.TryGetNextCorner(out corner));
        var toCorner = MathUtil.Flat(corner - Player.transform.position).normalized;
        Assert.Greater(Vector3.Dot(arrow.transform.forward, toCorner), 0.95f, "화살표가 다음 꼭짓점을 가리킴");

        Assert.IsTrue(quests.Begin(1), "이동 퀘스트 (장소 없음)");
        yield return null;
        yield return null;
        Assert.IsFalse(navigator.HasGuide);
        Assert.IsFalse(arrow.Visual.gameObject.activeSelf, "장소 없는 퀘스트는 화살표 숨김");
    }
}
