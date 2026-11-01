using Framework.Editor;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// 지역 랜드마크: 산악 바위(TheTalesFactory), 폐허 마을(ASTROFISH 건물), 트롤 능선, 미노타우로스 아레나, 시작 지점 쉼터. (호수/강 = WorldBuilder.Water)
/// 모든 건물/바위는 Default 레이어 콜라이더를 가져 햇빛을 가리는 그늘이 된다.
/// </summary>
public static partial class WorldBuilder
{
    private const string Astro = External + "/ASTROFISH_GAMES/MEDIEVAL_SERIES/MEDIEVAL_VILLAGE_HD";
    private const string AstroBuildings = Astro + "/_Buildings/Prefabs/Building_Exteriors";
    private const string Tales = External + "/TheTalesFactory/Photoscanned MoutainsRocks PBR/Prefabs";

    private static readonly string[] BigRocks = { "MountainRocks01", "MountainRocks02", "MountainsRocks03", "MountainRocks01_A", "MountainRocks01_B" };
    private static readonly string[] SmallRocks = { "MountainRocks01_C", "MountainRocks01_D", "MountainRocks02_A", "MountainRocks02_B" };

    private static Transform LandmarkRoot;
    private static System.Random LandmarkRandom;

    private static void buildLandmarks(Terrain InTerrain)
    {
        LandmarkRoot = new GameObject("[Landmarks]").transform;
        LandmarkRandom = new System.Random(Seed + 7);

        buildMountainRocks(InTerrain);
        buildRuinedHamlet(InTerrain);
        buildTrollRidge(InTerrain);
        buildMinotaurArena(InTerrain);
        buildStartGlade(InTerrain);
        buildRuins(InTerrain);
    }

    // ------------------------------------------------------------------ mountains

    private static void buildMountainRocks(Terrain InTerrain)
    {
        var root = group("MountainRocks");
        for (int i = 0; i < 70; ++i)
        {
            // 가장자리 띠(40~75m) 를 따라 큰 바위로 산세를 강조한다.
            float t = (float)LandmarkRandom.NextDouble();
            int side = LandmarkRandom.Next(4);
            float inset = 40.0f + (float)LandmarkRandom.NextDouble() * 35.0f;
            float along = 20.0f + t * (Size - 40.0f);
            Vector2 p;
            switch (side)
            {
                case 0:
                    p = new Vector2(along, inset);
                    break;
                case 1:
                    p = new Vector2(along, Size - inset);
                    break;
                case 2:
                    p = new Vector2(inset, along);
                    break;
                default:
                    p = new Vector2(Size - inset, along);
                    break;
            }

            string name = BigRocks[LandmarkRandom.Next(BigRocks.Length)];
            float scale = 1.0f + (float)LandmarkRandom.NextDouble() * 1.4f;
            if (clearOfRoads(p, 10.0f) == false)
            {
                continue;
            }

            place(InTerrain, root, $"{Tales}/{name}.prefab", p, (float)LandmarkRandom.NextDouble() * 360.0f, scale, 1.5f, true);
        }

        // 동쪽 고원 절벽 바위
        var highland = group("HighlandCliffs");
        for (int i = 0; i < 18; ++i)
        {
            var p = WorldRegions.Highlands + randomInCircle(70.0f);
            string name = SmallRocks[LandmarkRandom.Next(SmallRocks.Length)];
            if (clearOfRoads(p, 8.0f) == false)
            {
                continue;
            }

            place(InTerrain, highland, $"{Tales}/{name}.prefab", p, (float)LandmarkRandom.NextDouble() * 360.0f, 0.8f + (float)LandmarkRandom.NextDouble() * 0.8f, 0.6f, true);
        }
    }

    // ------------------------------------------------------------------ ruined hamlet

    private static void buildRuinedHamlet(Terrain InTerrain)
    {
        var root = group("RuinedHamlet");
        var c = WorldRegions.RuinedHamlet;
        // 광장을 바라보도록 둘러싼 건물들 (yaw = 광장 방향)
        placeFacing(InTerrain, root, AstroBuildings + "/Medieval_Building_Composite_A.prefab", c + new Vector2(-20.0f, 16.0f), c);
        placeFacing(InTerrain, root, AstroBuildings + "/Medieval_Building_Composite_C.prefab", c + new Vector2(20.0f, 18.0f), c);
        placeFacing(InTerrain, root, AstroBuildings + "/Medieval_Building_House1.prefab", c + new Vector2(-24.0f, -14.0f), c);
        placeFacing(InTerrain, root, AstroBuildings + "/Medieval_Building_RepunTower.prefab", c + new Vector2(24.0f, -16.0f), c);
        placeFacing(InTerrain, root, AstroBuildings + "/Medieval_Buildings_OpenAirSheter.prefab", c + new Vector2(0.0f, 26.0f), c);
        placeFacing(InTerrain, root, AstroBuildings + "/Medieval_Building_StoneLookoutTower.prefab", c + new Vector2(0.0f, -30.0f), c);

        string assets = Astro + "/_Assets";
        // 마을을 두른 무너진 성벽 조각 (폐허 마을다운 실루엣)
        for (int i = 0; i < 6; ++i)
        {
            float angle = i * 60.0f + 5.0f;
            var offset = (Vector2)(Quaternion.Euler(0.0f, 0.0f, angle) * Vector2.up) * 40.0f;
            if (clearOfRoads(c + offset, 5.0f) == true)
            {
                place(InTerrain, root, Masonry + "Medieval_Masonary_CellarWall.prefab", c + offset, -angle, 1.3f, i % 2 == 0 ? 0.2f : 0.8f, true);
            }
        }

        for (int i = 0; i < 8; ++i)
        {
            float angle = i * 45.0f + 20.0f;
            var offset = (Vector2)(Quaternion.Euler(0.0f, 0.0f, angle) * Vector2.up) * 34.0f;
            if (clearOfRoads(c + offset, 3.0f) == false)
            {
                continue;
            }

            string fence = i % 3 == 0 ? "/Fences/Prfbs/Medieval_Fences_Broken.prefab" : "/Fences/Prfbs/Medieval_Fences_Fixed.prefab";
            place(InTerrain, root, assets + fence, c + offset, -angle + 90.0f, 1.0f, 0.0f, true);
        }

        for (int i = 0; i < 6; ++i)
        {
            place(InTerrain, root, assets + "/Containers/Prfbs/Medieval_Barrel.prefab", c + randomInCircle(12.0f), (float)LandmarkRandom.NextDouble() * 360.0f, 1.0f, 0.0f, true);
        }

        torch(InTerrain, root, c + new Vector2(-6.0f, 6.0f));
        torch(InTerrain, root, c + new Vector2(6.0f, -6.0f));
    }

    // ------------------------------------------------------------------ troll ridge

    private static void buildTrollRidge(Terrain InTerrain)
    {
        var root = group("TrollRidge");
        var c = WorldRegions.TrollRidge;
        // 바위 고리: 길이 지나가는 쪽은 비워 둔다 (예전에는 고리가 닫혀 고원에서 나갈 수 없었다).
        for (int i = 0; i < 10; ++i)
        {
            float angle = i * 36.0f;
            var offset = (Vector2)(Quaternion.Euler(0.0f, 0.0f, angle) * Vector2.up) * (30.0f + (float)LandmarkRandom.NextDouble() * 6.0f);
            string name = SmallRocks[LandmarkRandom.Next(SmallRocks.Length)];
            float scale = 0.7f + (float)LandmarkRandom.NextDouble() * 0.4f;
            if (clearOfRoads(c + offset, 9.0f) == false)
            {
                continue;
            }

            place(InTerrain, root, $"{Tales}/{name}.prefab", c + offset, (float)LandmarkRandom.NextDouble() * 360.0f, scale, 0.8f, true);
        }

        // 능선 위 무너진 망루 · 성벽 (트롤 소굴)
        place(InTerrain, root, Masonry + "Medieval_Masonary_CellarWall.prefab", c + new Vector2(12.0f, -12.0f), 40.0f, 1.3f, 0.2f, true);
        place(InTerrain, root, Masonry + "Medieval_Masonary_WallDoorArch.prefab", c + new Vector2(-14.0f, -4.0f), 100.0f, 1.3f, 0.1f, true);

        place(InTerrain, root, AstroBuildings + "/Medieval_Shelter_Small.prefab", c + new Vector2(-8.0f, 10.0f), 160.0f, 1.0f, 0.0f, true);
        torch(InTerrain, root, c + new Vector2(4.0f, 2.0f));
        torch(InTerrain, root, c + new Vector2(-6.0f, -6.0f));
    }

    // ------------------------------------------------------------------ minotaur arena

    private static void buildMinotaurArena(Terrain InTerrain)
    {
        var root = group("MinotaurArena");
        var c = WorldRegions.MinotaurArena;
        for (int i = 0; i < 9; ++i)
        {
            float angle = i * 40.0f;
            var offset = (Vector2)(Quaternion.Euler(0.0f, 0.0f, angle) * Vector2.up) * 24.0f;
            // 입구(길 두 개)는 비운다.
            if (clearOfRoads(c + offset, 8.0f) == false)
            {
                continue;
            }

            place(InTerrain, root, $"{Tales}/MountainRocks01_A.prefab", c + offset, -angle, 0.9f, 1.0f, true);
        }

        for (int i = 0; i < 4; ++i)
        {
            var offset = (Vector2)(Quaternion.Euler(0.0f, 0.0f, i * 90.0f + 45.0f) * Vector2.up) * 15.0f;
            torch(InTerrain, root, c + offset);
        }
    }

    // ------------------------------------------------------------------ start glade

    /// <summary>시작 지점: 무너진 석벽과 작은 쉼터 — 첫 낮에 들어갈 그늘을 보여 준다.</summary>
    private static void buildStartGlade(Terrain InTerrain)
    {
        var root = group("StartGlade");
        var c = WorldRegions.StartGlade;
        string masonary = Astro + "/_Assets/Masonary/Prfbs/";
        place(InTerrain, root, masonary + "Medieval_Masonary_CellarWall.prefab", c + new Vector2(-14.0f, 10.0f), 0.0f, 1.3f, 0.0f, true);
        place(InTerrain, root, masonary + "Medieval_Masonary_CellarWall.prefab", c + new Vector2(-10.0f, 10.0f), 0.0f, 1.3f, 0.2f, true);
        place(InTerrain, root, masonary + "Medieval_Masonary_WallCorner.prefab", c + new Vector2(-17.0f, 7.0f), 90.0f, 1.0f, 0.0f, true);
        place(InTerrain, root, AstroBuildings + "/Medieval_Shelter_Small.prefab", c + new Vector2(14.0f, 14.0f), 200.0f, 1.0f, 0.0f, true);
        torch(InTerrain, root, c + new Vector2(4.0f, 8.0f));
    }

    // ------------------------------------------------------------------ helpers

    private static Transform group(string InName)
    {
        var go = new GameObject(InName);
        go.transform.SetParent(LandmarkRoot, false);
        return go.transform;
    }

    private static GameObject placeFacing(Terrain InTerrain, Transform InParent, string InPrefab, Vector2 InPosition, Vector2 InLookAt)
    {
        var dir = InLookAt - InPosition;
        float yaw = Mathf.Atan2(dir.x, dir.y) * Mathf.Rad2Deg;
        return place(InTerrain, InParent, InPrefab, InPosition, yaw, 1.0f, 0.1f, false);
    }

    /// <summary>외부 프리팹을 지면에 배치. 비호환(Standard) 재질은 URP 사본으로 교체. InAddCollider 면 콜라이더가 없을 때 박스 콜라이더 추가.</summary>
    private static GameObject place(Terrain InTerrain, Transform InParent, string InPrefab, Vector2 InPosition, float InYaw, float InScale, float InSink, bool InAddCollider)
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(InPrefab);
        if (prefab == null)
        {
            Debug.LogWarning("[World] missing " + InPrefab);
            return null;
        }

        var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, InParent);
        var position = onGround(InTerrain, InPosition);
        position.y -= InSink * InScale;
        instance.transform.SetPositionAndRotation(position, Quaternion.Euler(0.0f, InYaw, 0.0f));
        instance.transform.localScale = prefab.transform.localScale * InScale;
        instance.isStatic = true;
        // 건물/바위가 카메라와 캐릭터 사이를 가리면 숨긴다 (CameraOcclusionSystem).
        Framework.ComponentUtil.GetOrAdd<Framework.CameraOccluder>(instance);
        MaterialUpgradeUtil.UpgradeRenderers(instance, EditorPaths.Generated + "/Materials/World");

        if (InAddCollider == true && instance.GetComponentInChildren<Collider>() == null)
        {
            var renderers = instance.GetComponentsInChildren<Renderer>();
            if (renderers.Length > 0)
            {
                var bounds = renderers[0].bounds;
                foreach (var renderer in renderers)
                {
                    bounds.Encapsulate(renderer.bounds);
                }

                var box = instance.AddComponent<BoxCollider>();
                box.center = instance.transform.InverseTransformPoint(bounds.center);
                var size = instance.transform.InverseTransformVector(bounds.size);
                box.size = new Vector3(Mathf.Abs(size.x), Mathf.Abs(size.y), Mathf.Abs(size.z));
            }
        }

        // 지붕·처마처럼 콜라이더가 없는 부분도 그늘이 되도록 그림자 메시 그대로 판정용 콜라이더를 붙인다.
        ShadeColliderUtil.AddShadeColliders(instance, LayerId.SunBlocker);
        return instance;
    }

    private static void torch(Terrain InTerrain, Transform InParent, Vector2 InPosition)
    {
        // 석조 기둥(2.3m) 위에 큰 횃불을 올린다.
        place(InTerrain, InParent, Astro + "/_Assets/PltfrmSprts/Prfbs/Medieval_PortchColumn_Medium.prefab", InPosition, 0.0f, 1.0f, 0.0f, true);
        var instance = place(InTerrain, InParent, Astro + "/_Assets/Iron/Prefbs/Medieval_IronFixtures_LargeTorch.prefab", InPosition, 0.0f, 1.4f, -1.9f, false);
        if (instance == null)
        {
            return;
        }

        var lightObject = new GameObject("TorchLight");
        lightObject.transform.SetParent(instance.transform, false);
        lightObject.transform.localPosition = Vector3.up * 0.6f;
        var light = lightObject.AddComponent<Light>();
        light.type = LightType.Point;
        light.color = new Color(1.0f, 0.55f, 0.2f);
        light.range = 14.0f;
        light.intensity = 3.0f;
        light.shadows = LightShadows.None;
    }

    private static Vector2 randomInCircle(float InRadius)
    {
        double angle = LandmarkRandom.NextDouble() * Mathf.PI * 2.0;
        double radius = System.Math.Sqrt(LandmarkRandom.NextDouble()) * InRadius;
        return new Vector2((float)(System.Math.Cos(angle) * radius), (float)(System.Math.Sin(angle) * radius));
    }
}
