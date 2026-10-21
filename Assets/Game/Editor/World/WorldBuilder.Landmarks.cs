using Framework.Editor;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// 지역 랜드마크: 산악 바위(TheTalesFactory), 폐허 마을(ASTROFISH 건물), 트롤 능선, 미노타우로스 아레나, 호수, 시작 지점 쉼터.
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
        buildLake(InTerrain);
        buildStartGlade(InTerrain);
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
            place(InTerrain, root, $"{Tales}/{name}.prefab", p, (float)LandmarkRandom.NextDouble() * 360.0f, scale, 1.5f, true);
        }

        // 동쪽 고원 절벽 바위
        var highland = group("HighlandCliffs");
        for (int i = 0; i < 18; ++i)
        {
            var p = WorldRegions.Highlands + randomInCircle(70.0f);
            string name = SmallRocks[LandmarkRandom.Next(SmallRocks.Length)];
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
        for (int i = 0; i < 8; ++i)
        {
            float angle = i * 45.0f + 20.0f;
            var offset = (Vector2)(Quaternion.Euler(0.0f, 0.0f, angle) * Vector2.up) * 34.0f;
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
        for (int i = 0; i < 10; ++i)
        {
            float angle = i * 36.0f;
            var offset = (Vector2)(Quaternion.Euler(0.0f, 0.0f, angle) * Vector2.up) * (30.0f + (float)LandmarkRandom.NextDouble() * 6.0f);
            string name = SmallRocks[LandmarkRandom.Next(SmallRocks.Length)];
            place(InTerrain, root, $"{Tales}/{name}.prefab", c + offset, (float)LandmarkRandom.NextDouble() * 360.0f, 1.0f + (float)LandmarkRandom.NextDouble() * 0.6f, 0.8f, true);
        }

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
            place(InTerrain, root, $"{Tales}/MountainRocks01_A.prefab", c + offset, -angle, 0.9f, 1.0f, true);
        }

        for (int i = 0; i < 4; ++i)
        {
            var offset = (Vector2)(Quaternion.Euler(0.0f, 0.0f, i * 90.0f + 45.0f) * Vector2.up) * 15.0f;
            torch(InTerrain, root, c + offset);
        }
    }

    // ------------------------------------------------------------------ lake

    /// <summary>호수: URP Lit 반투명 수면 (WeatherMaker 물 프리팹은 무한 바다용이라 사용하지 않음).</summary>
    private static void buildLake(Terrain InTerrain)
    {
        string path = EditorPaths.Generated + "/World/M_LakeWater.mat";
        var material = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (material == null)
        {
            material = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            AssetDatabase.CreateAsset(material, path);
        }

        material.SetColor("_BaseColor", new Color(0.08f, 0.18f, 0.22f, 0.78f));
        material.SetFloat("_Smoothness", 0.95f);
        material.SetFloat("_Metallic", 0.0f);
        material.SetFloat("_Surface", 1.0f);
        material.SetFloat("_Blend", 0.0f);
        material.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
        material.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
        material.SetFloat("_ZWrite", 0.0f);
        material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        material.renderQueue = (int)RenderQueue.Transparent;
        EditorUtility.SetDirty(material);

        var water = GameObject.CreatePrimitive(PrimitiveType.Plane);
        water.name = "Lake";
        Object.DestroyImmediate(water.GetComponent<Collider>());
        water.transform.SetParent(LandmarkRoot, false);
        water.transform.position = new Vector3(WorldRegions.Lake.x, WorldRegions.WaterLevel, WorldRegions.Lake.y);
        float diameter = (WorldRegions.LakeRadius + 10.0f) * 2.0f;
        water.transform.localScale = new Vector3(diameter / 10.0f, 1.0f, diameter / 10.0f);
        var renderer = water.GetComponent<MeshRenderer>();
        renderer.sharedMaterial = material;
        renderer.shadowCastingMode = ShadowCastingMode.Off;
        water.isStatic = true;
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
