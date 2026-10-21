using System.Collections.Generic;
using Framework.Editor;
using Unity.AI.Navigation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

/// <summary>
/// 오픈월드 맵(World.unity) 생성기.
///  - Terrain 512×512m, 높이 120m: Perlin 언덕 + TerrainSampleAssets 실측 침식 높이맵으로 가장자리 산맥 + 지역별 지형(고원/능선/호수 분지)
///  - TerrainSampleAssets Terrain Layer 8종 / 풀·고사리·히스 Detail / TheTalesFactory 포토스캔 산악 바위
///  - ASTROFISH 중세 건물로 폐허 마을, 보스 아레나 바위 고리, 트롤 능선
///  - WeatherMaker 하늘/태양/달/구름 + 게임 낮밤과 브리지 연결
/// 지역 좌표는 SpawnTable.csv 와 같은 값을 쓴다 (WorldRegions).
/// 채집 노드/몬스터는 런타임에 SpawnTable 로 생성되므로 여기서는 배치하지 않는다.
/// </summary>
public static partial class WorldBuilder
{
    private const int Size = 512;
    private const int HeightRes = 513;
    private const float MaxHeight = 120.0f;
    private const int Seed = 1337;
    private const string External = "Assets/ExternalAssets";

    private struct FlatZone
    {
        public Vector2 Center;
        public float Radius;
        public float Height;
    }

    /// <summary>지역 중심 (SpawnTable 과 일치). 문서: Documentation/VRising/World.md</summary>
    private static class WorldRegions
    {
        public static readonly Vector2 StartGlade = new Vector2(256.0f, 96.0f);
        public static readonly Vector2 RuinedHamlet = new Vector2(300.0f, 215.0f);
        public static readonly Vector2 SerpentMarsh = new Vector2(175.0f, 300.0f);
        public static readonly Vector2 Lake = new Vector2(170.0f, 262.0f);
        public static readonly Vector2 TrollRidge = new Vector2(250.0f, 385.0f);
        public static readonly Vector2 MinotaurArena = new Vector2(390.0f, 430.0f);
        public static readonly Vector2 DunleyWoods = new Vector2(130.0f, 230.0f);
        public static readonly Vector2 Highlands = new Vector2(390.0f, 220.0f);
        public const float LakeRadius = 28.0f;
        public const float WaterLevel = 4.0f;
        public const float BaseHeight = 8.0f;
    }

    [MenuItem("Game/Setup/5. Build World Scene", priority = 5)]
    public static void Build()
    {
        EditorAssetUtil.EnsureFolder(EditorPaths.Scenes);
        EditorAssetUtil.EnsureFolder(EditorPaths.Generated + "/World");
        MaterialUpgradeUtil.ClearCache();
        configurePipeline();

        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        var zones = collectFlatZones();

        var terrain = buildTerrain(zones);
        // Terrain Tree 는 물리 판정이 없어 그림자만 보이고 햇빛 판정에서 빠졌다 → 나무 메시 모양 그늘 콜라이더를 만든다 (Issues 26).
        ShadeColliderUtil.BuildTreeShade(terrain, LayerId.SunBlocker, "[TreeShade]");
        Volume day;
        Volume night;
        buildVolumes(out day, out night);
        var camera = buildCamera();
        var sun = buildSky(camera.GetComponent<Camera>());
        buildLandmarks(terrain);

        var contextObject = new GameObject("[SceneContext]");
        var context = contextObject.AddComponent<SceneContext>();
        context.Sun = sun;
        context.DayVolume = day;
        context.NightVolume = night;
        context.Camera = camera;
        context.Terrain = terrain;
        var start = new GameObject("PlayerStart");
        start.transform.position = onGround(terrain, WorldRegions.StartGlade);
        context.PlayerStart = start.transform;

        var bootstrap = new GameObject("[GameBootstrap]").AddComponent<GameBootstrap>();
        bootstrap.Context = context;
        string guid = AssetDatabase.AssetPathToGUID(EditorPaths.GameTablesAsset);
        bootstrap.TablesReference = new AssetReferenceT<GameTables>(guid);

        bakeNavMesh();

        EditorSceneManager.SaveScene(scene, EditorPaths.WorldScene);
        registerBuildScene();
        Debug.Log("[World] scene built: " + EditorPaths.WorldScene);
    }

    // ------------------------------------------------------------------ pipeline

    private static void configurePipeline()
    {
        var asset = GraphicsSettings.defaultRenderPipeline as UniversalRenderPipelineAsset;
        if (asset == null)
        {
            return;
        }

        var serialized = new SerializedObject(asset);
        serialized.FindProperty("m_ShadowDistance").floatValue = 90.0f;
        serialized.FindProperty("m_ShadowCascadeCount").intValue = 2;
        serialized.FindProperty("m_SoftShadowsSupported").boolValue = true;
        serialized.FindProperty("m_SupportsHDR").boolValue = true;
        serialized.ApplyModifiedProperties();
        EditorUtility.SetDirty(asset);
    }

    private static List<FlatZone> collectFlatZones()
    {
        var zones = new List<FlatZone>();
        var tables = AssetDatabase.LoadAssetAtPath<GameTables>(EditorPaths.GameTablesAsset);
        if (tables != null)
        {
            foreach (var row in tables.Spawn.All)
            {
                bool flatten = row.Kind == ESpawnKind.PlayerStart || row.Kind == ESpawnKind.Merchant || row.Kind == ESpawnKind.Monster;
                if (flatten == true)
                {
                    zones.Add(new FlatZone { Center = new Vector2(row.Position.x, row.Position.z), Radius = Mathf.Max(10.0f, row.Radius + 8.0f), Height = -1.0f });
                }
            }
        }

        // 트롤 능선은 높은 고원으로 평탄화한다 (Height 지정).
        zones.Add(new FlatZone { Center = WorldRegions.TrollRidge, Radius = 32.0f, Height = WorldRegions.BaseHeight + 9.0f });
        return zones;
    }

    // ------------------------------------------------------------------ volumes / camera

    private static void buildVolumes(out Volume OutDay, out Volume OutNight)
    {
        string folder = EditorPaths.Generated + "/World";

        var dayProfile = createProfile(folder + "/VP_Day.asset");
        dayProfile.Add<Tonemapping>(true).mode.Override(TonemappingMode.ACES);
        var bloom = dayProfile.Add<Bloom>(true);
        bloom.threshold.Override(1.1f);
        bloom.intensity.Override(0.45f);
        var colorDay = dayProfile.Add<ColorAdjustments>(true);
        colorDay.postExposure.Override(0.35f);
        colorDay.contrast.Override(10.0f);
        colorDay.saturation.Override(6.0f);
        dayProfile.Add<Vignette>(true).intensity.Override(0.2f);
        saveProfile(dayProfile);

        var nightProfile = createProfile(folder + "/VP_Night.asset");
        nightProfile.Add<Tonemapping>(true).mode.Override(TonemappingMode.ACES);
        var bloomNight = nightProfile.Add<Bloom>(true);
        bloomNight.threshold.Override(0.8f);
        bloomNight.intensity.Override(1.2f);
        bloomNight.tint.Override(new Color(1.0f, 0.55f, 0.6f));
        var colorNight = nightProfile.Add<ColorAdjustments>(true);
        // 밤에도 지형이 보이도록 (ACES 가 어두운 영역을 많이 눌러서 노출을 높게, 대비는 낮게).
        colorNight.postExposure.Override(1.5f);
        colorNight.contrast.Override(4.0f);
        colorNight.saturation.Override(-10.0f);
        colorNight.colorFilter.Override(new Color(0.8f, 0.85f, 1.0f));
        var vignetteNight = nightProfile.Add<Vignette>(true);
        vignetteNight.intensity.Override(0.25f);
        vignetteNight.color.Override(new Color(0.08f, 0.0f, 0.03f));
        saveProfile(nightProfile);

        OutDay = createVolume("Volume_Day", dayProfile, 1.0f);
        OutNight = createVolume("Volume_Night", nightProfile, 0.0f);
    }

    private static VolumeProfile createProfile(string InPath)
    {
        AssetDatabase.DeleteAsset(InPath);
        var profile = ScriptableObject.CreateInstance<VolumeProfile>();
        AssetDatabase.CreateAsset(profile, InPath);
        return profile;
    }

    private static void saveProfile(VolumeProfile InProfile)
    {
        foreach (var component in InProfile.components)
        {
            component.name = component.GetType().Name;
            AssetDatabase.AddObjectToAsset(component, InProfile);
        }

        EditorUtility.SetDirty(InProfile);
    }

    private static Volume createVolume(string InName, VolumeProfile InProfile, float InWeight)
    {
        var go = new GameObject(InName);
        var volume = go.AddComponent<Volume>();
        volume.isGlobal = true;
        volume.sharedProfile = InProfile;
        volume.weight = InWeight;
        return volume;
    }

    private static TopDownCamera buildCamera()
    {
        var go = new GameObject("Main Camera");
        go.tag = "MainCamera";
        var camera = go.AddComponent<Camera>();
        camera.fieldOfView = 40.0f;
        camera.nearClipPlane = 0.5f;
        camera.farClipPlane = 2000.0f;
        camera.allowHDR = true;
        // WeatherMaker 권장: 하늘/구름 패스가 그려지도록 Solid Color (0,0,0,0).
        camera.clearFlags = CameraClearFlags.SolidColor;
        camera.backgroundColor = new Color(0.0f, 0.0f, 0.0f, 0.0f);
        go.AddComponent<AudioListener>();
        var data = go.AddComponent<UniversalAdditionalCameraData>();
        data.renderPostProcessing = true;
        data.antialiasing = AntialiasingMode.SubpixelMorphologicalAntiAliasing;
        data.renderShadows = true;
        go.transform.position = new Vector3(WorldRegions.StartGlade.x, 30.0f, WorldRegions.StartGlade.y - 20.0f);
        go.transform.rotation = Quaternion.Euler(50.0f, 45.0f, 0.0f);
        var topDown = go.AddComponent<TopDownCamera>();
        topDown.Camera = camera;
        return topDown;
    }

    // ------------------------------------------------------------------ navmesh / build settings

    private static void bakeNavMesh()
    {
        var go = new GameObject("[NavMesh]");
        var surface = go.AddComponent<NavMeshSurface>();
        surface.collectObjects = CollectObjects.All;
        surface.useGeometry = UnityEngine.AI.NavMeshCollectGeometry.PhysicsColliders;
        surface.layerMask = (1 << LayerId.Ground) | (1 << LayerId.Default) | (1 << LayerId.Structure);
        surface.BuildNavMesh();

        string path = EditorPaths.Generated + "/World/NavMesh.asset";
        AssetDatabase.DeleteAsset(path);
        AssetDatabase.CreateAsset(surface.navMeshData, path);
    }

    private static void registerBuildScene()
    {
        var scenes = new List<EditorBuildSettingsScene>();
        scenes.Add(new EditorBuildSettingsScene(EditorPaths.WorldScene, true));
        foreach (var scene in EditorBuildSettings.scenes)
        {
            if (scene.path != EditorPaths.WorldScene)
            {
                scenes.Add(new EditorBuildSettingsScene(scene.path, false));
            }
        }

        EditorBuildSettings.scenes = scenes.ToArray();
    }

    // ------------------------------------------------------------------ helpers

    private static Vector3 onGround(Terrain InTerrain, Vector2 InXZ)
    {
        var position = new Vector3(InXZ.x, 0.0f, InXZ.y);
        position.y = InTerrain.SampleHeight(position) + InTerrain.transform.position.y;
        return position;
    }
}
