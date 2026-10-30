using Framework;
using UnityEngine;

/// <summary>
/// 건축물이 설치되면(StructurePlacedEvent) 그 바운드와 겹치는 장식 나무/덤불(FoliageRoot)과 Terrain 풀(Detail)을 지운다.
///  - Terrain 은 시작할 때 TerrainData 복제본으로 바꿔 편집한다 (원본 에셋 보존, TerrainUtil.UseRuntimeCopy).
///  - 멀티플레이에서는 같은 이벤트를 각 피어에서 발행하면 된다 (설치 결과만 복제).
/// </summary>
public class FoliageManager : IManager
{
    /// <summary>건축 바운드를 사방으로 넓히는 여유 (m). 벽 바로 옆 풀/줄기도 같이 지운다.</summary>
    private const float ClearMargin = 0.4f;

    private Terrain Terrain;
    private TerrainData OriginalData;
    private FoliageRoot Foliage;

    void IManager.Initialize()
    {
        var context = SceneContext.Current;
        if (context != null)
        {
            Terrain = context.Terrain;
            Foliage = context.Foliage;
        }

        OriginalData = TerrainUtil.UseRuntimeCopy(Terrain);
        EventBus<StructurePlacedEvent>.Subscribe(onStructurePlaced);
    }

    void IManager.UnInitialize()
    {
        EventBus<StructurePlacedEvent>.Unsubscribe(onStructurePlaced);
        TerrainUtil.RestoreOriginal(Terrain, OriginalData);
        Terrain = null;
        OriginalData = null;
        Foliage = null;
    }

    void IManager.Update(float InDeltaTime)
    {
    }

    /// <summary>InBounds(XZ) 안의 나무/덤불/풀을 지운다. 지운 나무·덤불 수를 돌려준다.</summary>
    public int ClearArea(Bounds InBounds)
    {
        InBounds.Expand(new Vector3(ClearMargin * 2.0f, 0.0f, ClearMargin * 2.0f));
        TerrainUtil.ClearDetails(Terrain, InBounds);
        return Foliage != null ? Foliage.ClearArea(InBounds) : 0;
    }

    private void onStructurePlaced(StructurePlacedEvent InEvent)
    {
        ClearArea(InEvent.Bounds);
    }
}
