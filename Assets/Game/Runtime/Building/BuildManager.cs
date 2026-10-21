using System.Collections.Generic;
using Framework;
using UnityEngine;

/// <summary>
/// 건축 모드. 선택한 건축물의 고스트를 커서 위치(그리드 스냅)에 보여 주고 좌클릭으로 설치한다.
///  - 성 심장(CastleHeart)은 1개만, 나머지 RequiresHeart 건축물은 성 심장 반경 안에서만 설치 가능.
///  - 비용은 StructureTable.Costs/CostCounts, 인벤토리에서 차감.
/// </summary>
public class BuildManager : IManager
{
    private static readonly Color ValidColor = new Color(0.3f, 1.0f, 0.4f, 1.0f);
    private static readonly Color InvalidColor = new Color(1.0f, 0.25f, 0.25f, 1.0f);
    private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
    private const float BuildAnimSeconds = 1.1f;

    private readonly List<Structure> Hearts = new List<Structure>();
    private Transform Root;
    private StructureRow Current;
    private GameObject Ghost;
    private Renderer[] GhostRenderers;
    private MaterialPropertyBlock Block;
    private float Rotation;
    private bool IsValid;

    public bool IsActive
    {
        get { return Current != null; }
    }

    public StructureRow CurrentRow
    {
        get { return Current; }
    }

    void IManager.Initialize()
    {
        Root = new GameObject("[Structures]").transform;
        Block = new MaterialPropertyBlock();
    }

    void IManager.UnInitialize()
    {
        Cancel();
        Hearts.Clear();
        if (Root != null)
        {
            Object.Destroy(Root.gameObject);
        }
    }

    void IManager.Update(float InDeltaTime)
    {
        if (Current == null)
        {
            return;
        }

        var player = PlayerCharacter.Local;
        var input = Managers.Get<GameInput>();
        if (player == null || player.IsDead == true || input.Pressed(EInputAction.Cancel) == true)
        {
            Cancel();
            return;
        }

        if (input.Pressed(EInputAction.RotateStructure) == true)
        {
            Rotation = Mathf.Repeat(Rotation + 90.0f, 360.0f);
        }

        var controller = player.GetComponent<PlayerController>();
        Vector3 point = snap(controller.AimPoint);
        Ghost.transform.SetPositionAndRotation(point, Quaternion.Euler(0.0f, Rotation, 0.0f));

        IsValid = validate(player, point);
        tintGhost(IsValid == true ? ValidColor : InvalidColor);

        bool pointerOverUI = UnityEngine.EventSystems.EventSystem.current != null && UnityEngine.EventSystems.EventSystem.current.IsPointerOverGameObject() == true;
        if (input.Pressed(EInputAction.Primary) == true && pointerOverUI == false)
        {
            place(player, point);
        }
    }

    public void Begin(int InStructureId)
    {
        Cancel();
        StructureRow row;
        if (Tables.Root.Structure.TryGet(InStructureId, out row) == false)
        {
            return;
        }

        var prefab = Managers.Get<AssetLoader>().LoadSync<GameObject>(Tables.Address(row.Prefab));
        if (prefab == null)
        {
            return;
        }

        Current = row;
        Ghost = Object.Instantiate(prefab);
        foreach (var collider in Ghost.GetComponentsInChildren<Collider>())
        {
            collider.enabled = false;
        }

        GhostRenderers = Ghost.GetComponentsInChildren<Renderer>();
        setSuppress(true);
    }

    public void Cancel()
    {
        if (Ghost != null)
        {
            Object.Destroy(Ghost);
        }

        Ghost = null;
        GhostRenderers = null;
        Current = null;
        setSuppress(false);
    }

    public bool HasHeart
    {
        get { return Hearts.Count > 0; }
    }

    private void place(PlayerCharacter InPlayer, Vector3 InPoint)
    {
        if (IsValid == false)
        {
            int message = Current.Type == EStructureType.CastleHeart || HasHeart == true ? (int)EText.Place : (int)EText.NeedCastleHeart;
            EventBus<NotifyEvent>.Publish(new NotifyEvent { TextId = message, Color = Color.red });
            return;
        }

        if (InPlayer.Inventory.Remove(Current.Costs, Current.CostCounts) == false)
        {
            EventBus<NotifyEvent>.Publish(new NotifyEvent { TextId = (int)EText.NotEnoughMaterials, Color = Color.red });
            return;
        }

        var prefab = Managers.Get<AssetLoader>().LoadSync<GameObject>(Tables.Address(Current.Prefab));
        var instance = Object.Instantiate(prefab, InPoint, Quaternion.Euler(0.0f, Rotation, 0.0f), Root);
        var structure = ComponentUtil.GetOrAdd<Structure>(instance);
        structure.Initialize(Current);

        // 건축 모션 (Crafting 팩 망치질) 동안 이동을 잠근다.
        InPlayer.Motor.Face(InPoint - InPlayer.transform.position, true);
        InPlayer.Motor.LockMovement(BuildAnimSeconds);
        if (InPlayer.Anim != null)
        {
            InPlayer.Anim.Play(Tables.ConstInt(EConst.BuildAnim));
        }

        if (Current.Type == EStructureType.CastleHeart)
        {
            Hearts.Add(structure);
            Cancel();
            return;
        }

        if (Current.Type == EStructureType.Coffin)
        {
            InPlayer.RespawnPoint = InPoint + instance.transform.forward * 1.5f;
        }

        // 같은 건축물을 연속 설치할 수 있게 재료가 남아 있으면 모드를 유지한다.
        if (InPlayer.Inventory.Has(Current.Costs, Current.CostCounts) == false)
        {
            Cancel();
        }
    }

    private bool validate(PlayerCharacter InPlayer, Vector3 InPoint)
    {
        if (MathUtil.FlatDistance(InPlayer.transform.position, InPoint) > 12.0f)
        {
            return false;
        }

        if (Current.Type == EStructureType.CastleHeart)
        {
            return HasHeart == false;
        }

        if (Current.RequiresHeart == false)
        {
            return true;
        }

        float radius = Tables.Const(EConst.CastleHeartRadius);
        for (int i = 0; i < Hearts.Count; ++i)
        {
            if (Hearts[i] != null && MathUtil.FlatDistance(Hearts[i].transform.position, InPoint) <= radius)
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// 그리드 스냅. 바닥은 칸 중심, 벽은 칸 경계선(회전 방향에 따라 X/Z 중 하나를 반 칸 이동), 기둥(Decoration Footprint 1 이하)은 칸 모서리.
    /// ASTROFISH 모듈이 3m 단위라 BuildGridSize = 3 이다.
    /// </summary>
    private Vector3 snap(Vector3 InPoint)
    {
        float grid = Mathf.Max(0.5f, Tables.Const(EConst.BuildGridSize));
        float half = grid * 0.5f;
        float cellX = Mathf.Round(InPoint.x / grid) * grid;
        float cellZ = Mathf.Round(InPoint.z / grid) * grid;
        float edgeX = Mathf.Round((InPoint.x - half) / grid) * grid + half;
        float edgeZ = Mathf.Round((InPoint.z - half) / grid) * grid + half;

        bool alongX = Mathf.Abs(Mathf.DeltaAngle(Rotation, 90.0f)) > 45.0f && Mathf.Abs(Mathf.DeltaAngle(Rotation, 270.0f)) > 45.0f;
        switch (Current.Type)
        {
            case EStructureType.Wall:
                InPoint.x = alongX == true ? cellX : edgeX;
                InPoint.z = alongX == true ? edgeZ : cellZ;
                break;
            case EStructureType.Decoration:
                bool isPillar = Current.Footprint.x <= 1.0f && Current.Footprint.y <= 1.0f;
                InPoint.x = isPillar == true ? edgeX : cellX;
                InPoint.z = isPillar == true ? edgeZ : cellZ;
                break;
            default:
                InPoint.x = cellX;
                InPoint.z = cellZ;
                break;
        }

        return CombatUtil.SnapToGround(InPoint);
    }

    private void tintGhost(Color InColor)
    {
        if (GhostRenderers == null)
        {
            return;
        }

        Block.SetColor(BaseColorId, InColor);
        for (int i = 0; i < GhostRenderers.Length; ++i)
        {
            GhostRenderers[i].SetPropertyBlock(Block);
        }
    }

    private void setSuppress(bool InSuppress)
    {
        var player = PlayerCharacter.Local;
        if (player == null)
        {
            return;
        }

        var controller = player.GetComponent<PlayerController>();
        if (controller != null)
        {
            controller.SuppressAbilities = InSuppress;
        }
    }
}
