using System.Collections;
using Framework;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.TestTools;

/// <summary>
/// 월드 식생/물/카메라 가림 테스트 (World.md 7절, CameraOcclusion.md).
///  - 건축 이벤트가 겹친 나무와 Terrain 풀을 지우고, TerrainData 원본 에셋은 건드리지 않는다
///  - 카메라와 플레이어 사이 나무는 메시만 숨기고(그림자 유지), 벗어나면 되돌린다
///  - 런타임 스폰(채집 노드)이 강/호수 안에 생기지 않는다
/// </summary>
public class WorldFoliageTests : GamePlayTestBase
{
    [UnityTest]
    public IEnumerator Build_ClearsOverlappingFoliageAndGrass()
    {
        var context = SceneContext.Current;
        Assert.IsNotNull(context.Foliage, "SceneContext.Foliage 가 없습니다 (World 씬 재빌드 필요)");
        var tree = findTree(StartPosition, 200.0f);
        Assert.IsNotNull(tree, "시작 지점 근처에 장식 나무가 없습니다");

        var terrain = context.Terrain;
        Assert.AreEqual(terrain.terrainData, terrain.GetComponent<TerrainCollider>().terrainData, "Terrain 과 TerrainCollider 가 같은 복제본을 써야 함");
#if UNITY_EDITOR
        Assert.IsFalse(UnityEditor.AssetDatabase.Contains(terrain.terrainData), "런타임 복제 TerrainData 를 편집해야 함 (원본 에셋 보존)");
#endif

        // 나무 밑동을 덮는 6m × 6m 건축물이 설치된 것처럼 이벤트를 보낸다 (BuildManager.place 와 같은 경로).
        Vector3 center = tree.transform.position;
        var bounds = new Bounds(center + Vector3.up * 2.0f, new Vector3(6.0f, 4.0f, 6.0f));
        EventBus<StructurePlacedEvent>.Publish(new StructurePlacedEvent { Structure = null, Bounds = bounds });
        yield return null;

        Assert.IsTrue(tree.IsCleared, "건축물과 겹친 나무는 비활성화되어야 함");
        Assert.AreEqual(0, countDetails(terrain, bounds), "건축물 아래 풀(Detail)은 지워져야 함");
    }

    [UnityTest]
    public IEnumerator CameraOcclusion_HidesTreeBetweenCameraAndPlayer()
    {
        var camera = SceneContext.Current.Camera;
        var occlusion = camera.GetComponent<CameraOcclusionSystem>();
        Assert.IsNotNull(occlusion, "카메라에 CameraOcclusionSystem 이 붙어야 함 (TopDownCamera.Setup)");

        var tree = findTree(StartPosition, 200.0f);
        Assert.IsNotNull(tree, "장식 나무가 없습니다");
        var occluder = tree.GetComponent<CameraOccluder>();
        Assert.IsNotNull(occluder, "나무 프리팹에 CameraOccluder 가 있어야 함");

        // 카메라는 플레이어 뒤쪽 위에 있으므로, 나무 앞(카메라 전방)으로 6m 에 서면 나무가 시야를 가린다.
        Vector3 forward = Vector3.ProjectOnPlane(camera.transform.forward, Vector3.up).normalized;
        Player.Motor.Teleport(CombatUtil.SnapToGround(tree.transform.position + forward * 6.0f));
        yield return WaitFor(() => occluder.IsOccluded == true, 3.0f, "나무 가림 감지");
        Assert.AreEqual(ShadowCastingMode.ShadowsOnly, occluder.GetRenderer(0).shadowCastingMode, "가린 나무는 그림자만 남아야 함");

        // 나무 뒤(카메라 쪽 반대편이 아닌, 카메라 뒤)로 멀리 가면 다시 보인다.
        Player.Motor.Teleport(CombatUtil.SnapToGround(tree.transform.position - forward * 40.0f));
        yield return WaitFor(() => occluder.IsOccluded == false, 3.0f, "가림 해제");
        Assert.AreEqual(occluder.GetOriginalShadowMode(0), occluder.GetRenderer(0).shadowCastingMode, "가림이 끝나면 원래 그림자 모드로 복구");
        Assert.IsFalse(occluder.GetRenderer(0).forceRenderingOff, "가림이 끝나면 다시 그려져야 함");
        Player.Motor.Teleport(StartPosition);
    }

    [UnityTest]
    public IEnumerator Spawn_ResourceNodesAvoidWater()
    {
        yield return null;
        int inWater = 0;
        foreach (var node in Object.FindObjectsByType<ResourceNode>(FindObjectsSortMode.None))
        {
            if (WaterBody.IsInWater(node.transform.position, 0.0f) == true)
            {
                ++inWater;
            }
        }

        Assert.AreEqual(0, inWater, "강/호수 안에 스폰된 채집 노드");
    }

    // ------------------------------------------------------------------ helpers

    /// <summary>InCenter 에서 InRange 안, 물 밖에 있는 활성 나무(CameraOccluder 가 있는 장식 식생) 중 가장 가까운 것.</summary>
    private static FoliageInstance findTree(Vector3 InCenter, float InRange)
    {
        FoliageInstance best = null;
        float bestDistance = InRange;
        foreach (var instance in SceneContext.Current.Foliage.Instances)
        {
            if (instance == null || instance.IsCleared == true || instance.GetComponent<CameraOccluder>() == null)
            {
                continue;
            }

            float distance = MathUtil.FlatDistance(instance.transform.position, InCenter);
            if (distance < bestDistance && distance > 15.0f && WaterBody.IsInWater(instance.transform.position, 8.0f) == false)
            {
                best = instance;
                bestDistance = distance;
            }
        }

        return best;
    }

    private static int countDetails(Terrain InTerrain, Bounds InBounds)
    {
        var data = InTerrain.terrainData;
        int resolution = data.detailResolution;
        Vector3 origin = InTerrain.transform.position;
        int x0 = Mathf.FloorToInt((InBounds.min.x - origin.x) / data.size.x * resolution) + 1;
        int x1 = Mathf.FloorToInt((InBounds.max.x - origin.x) / data.size.x * resolution) - 1;
        int y0 = Mathf.FloorToInt((InBounds.min.z - origin.z) / data.size.z * resolution) + 1;
        int y1 = Mathf.FloorToInt((InBounds.max.z - origin.z) / data.size.z * resolution) - 1;
        int total = 0;
        for (int layer = 0; layer < data.detailPrototypes.Length; ++layer)
        {
            var map = data.GetDetailLayer(x0, y0, x1 - x0, y1 - y0, layer);
            foreach (int value in map)
            {
                total += value;
            }
        }

        return total;
    }
}
