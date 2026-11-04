using Framework;
using Framework.UI;
using UnityEngine;

/// <summary>건축물 격자 스냅에 필요한 후보 좌표 (BuildManager.snap 이 계산해서 넘긴다).</summary>
public struct StructureSnap
{
    /// <summary>칸 중심.</summary>
    public float CellX;
    public float CellZ;
    /// <summary>칸 경계선.</summary>
    public float EdgeX;
    public float EdgeZ;
    /// <summary>벽이 X 축을 따라 놓이는 회전인가.</summary>
    public bool AlongX;
}

/// <summary>
/// 건축물 종류별 규칙 (EStructureType 마다 한 클래스, 2026-11-01 — BuildManager · Structure · BuildWindow 의 switch / == 분기를 옮김).
///  - 격자 스냅 위치, 성 심장 여부, 설치 직후 처리, 상호작용, 지도 표시
/// </summary>
public abstract class StructureKind
{
    public static StructureKind Of(EStructureType InType)
    {
        var kind = KindRegistry<StructureKind>.Get((int)InType);
        return kind != null ? kind : KindRegistry<StructureKind>.Get((int)EStructureType.Floor);
    }

    /// <summary>성 심장인가 (먼저 하나만 지을 수 있고, 다른 건축물은 그 반경 안에만).</summary>
    public virtual bool IsCastleHeart
    {
        get { return false; }
    }

    /// <summary>G 로 상호작용하는가.</summary>
    public virtual bool IsInteractable
    {
        get { return false; }
    }

    /// <summary>지도에 성 표식으로 보이는가.</summary>
    public virtual bool ShowsOnMap
    {
        get { return false; }
    }

    /// <summary>기본: 칸 중심.</summary>
    public virtual Vector3 Snap(Vector3 InPoint, StructureSnap InSnap, StructureRow InRow)
    {
        InPoint.x = InSnap.CellX;
        InPoint.z = InSnap.CellZ;
        return InPoint;
    }

    /// <summary>설치 직후. true 면 건축 모드를 끝낸다 (성 심장).</summary>
    public virtual bool OnPlaced(BuildManager InBuild, PlayerCharacter InPlayer, Structure InStructure)
    {
        return false;
    }

    public virtual void Interact(Structure InStructure, PlayerCharacter InPlayer)
    {
    }
}

[Kind(EStructureType.CastleHeart)]
public sealed class CastleHeartKind : StructureKind
{
    public override bool IsCastleHeart
    {
        get { return true; }
    }

    public override bool ShowsOnMap
    {
        get { return true; }
    }

    public override bool OnPlaced(BuildManager InBuild, PlayerCharacter InPlayer, Structure InStructure)
    {
        InBuild.RegisterHeart(InStructure);
        return true;
    }
}

[Kind(EStructureType.Floor)]
public sealed class FloorKind : StructureKind
{
}

/// <summary>벽: 칸 경계선 (회전 방향에 따라 X/Z 중 하나를 반 칸 이동).</summary>
[Kind(EStructureType.Wall)]
public sealed class WallKind : StructureKind
{
    public override Vector3 Snap(Vector3 InPoint, StructureSnap InSnap, StructureRow InRow)
    {
        InPoint.x = InSnap.AlongX == true ? InSnap.CellX : InSnap.EdgeX;
        InPoint.z = InSnap.AlongX == true ? InSnap.EdgeZ : InSnap.CellZ;
        return InPoint;
    }
}

/// <summary>장식: 기둥처럼 작은 것(Footprint 1 이하)은 칸 모서리, 나머지는 칸 중심.</summary>
[Kind(EStructureType.Decoration)]
public sealed class DecorationKind : StructureKind
{
    public override Vector3 Snap(Vector3 InPoint, StructureSnap InSnap, StructureRow InRow)
    {
        bool isPillar = InRow.Footprint.x <= 1.0f && InRow.Footprint.y <= 1.0f;
        InPoint.x = isPillar == true ? InSnap.EdgeX : InSnap.CellX;
        InPoint.z = isPillar == true ? InSnap.EdgeZ : InSnap.CellZ;
        return InPoint;
    }
}

/// <summary>작업대 · 제련소: G 로 그 장소의 제작 창.</summary>
[Kind(EStructureType.Station)]
public sealed class StationKind : StructureKind
{
    public override bool IsInteractable
    {
        get { return true; }
    }

    public override void Interact(Structure InStructure, PlayerCharacter InPlayer)
    {
        var craft = Managers.Get<UIManager>().Get<CraftWindow>();
        if (craft != null)
        {
            craft.OpenFor(InStructure.Row.Station);
        }
    }
}

/// <summary>관: 설치하거나 G 로 상호작용하면 리스폰 지점이 관 앞으로.</summary>
[Kind(EStructureType.Coffin)]
public sealed class CoffinKind : StructureKind
{
    public override bool IsInteractable
    {
        get { return true; }
    }

    public override bool ShowsOnMap
    {
        get { return true; }
    }

    public override bool OnPlaced(BuildManager InBuild, PlayerCharacter InPlayer, Structure InStructure)
    {
        InPlayer.RespawnPoint = respawnPoint(InStructure);
        return false;
    }

    public override void Interact(Structure InStructure, PlayerCharacter InPlayer)
    {
        InPlayer.RespawnPoint = respawnPoint(InStructure);
        EventBus<NotifyEvent>.Publish(new NotifyEvent { TextId = InStructure.Row.NameId, Color = new Color(0.7f, 0.2f, 0.25f) });
    }

    private static Vector3 respawnPoint(Structure InStructure)
    {
        return InStructure.transform.position + InStructure.transform.forward * 1.5f;
    }
}
