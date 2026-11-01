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
///  - TerrainSampleAssets Terrain Layer 8종 / 풀·고사리·히스 Detail 12종(바람) / TheTalesFactory 포토스캔 산악 바위
///  - 절차 나무 4종(가문비/활엽수/자작/고사목) + 덤불 GameObject 배치 (.Foliage), 강 + 호수 물 셰이더 (.Water)
///  - ASTROFISH 중세 건물로 폐허 마을, 보스 아레나 바위 고리, 트롤 능선
///  - URP 절차 스카이박스 · 안개 · 반사 프로브 (.Sky) + 낮밤(DayNightManager) + 포스트 프로세싱 볼륨 (.PostFx)
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
        // 나무는 GameObject(절차 나무 프리팹)라 프리팹 안의 SunShade 콜라이더가 그늘 판정을 한다 (Issues 26).
        var foliage = buildFoliage(terrain, zones);
        buildWater(terrain);
        Volume day;
        Volume night;
        buildVolumes(out day, out night);
        var camera = buildCamera();
        buildScreenEffects(camera.gameObject);
        var sun = buildSky(camera.GetComponent<Camera>());
        buildLandmarks(terrain);

        var contextObject = new GameObject("[SceneContext]");
        var context = contextObject.AddComponent<SceneContext>();
        context.Sun = sun;
        context.DayVolume = day;
        context.NightVolume = night;
        context.Camera = camera;
        context.Terrain = terrain;
        context.Foliage = foliage;
        var start = new GameObject("PlayerStart");
        start.transform.position = onGround(terrain, WorldRegions.StartGlade);
        context.PlayerStart = start.transform;

        var bootstrap = new GameObject("[GameBootstrap]").AddComponent<GameBootstrap>();
        bootstrap.Context = context;
        string guid = AssetDatabase.AssetPathToGUID(EditorPaths.GameTablesAsset);
        bootstrap.TablesReference = new AssetReferenceT<GameTables>(guid);

        bakeNavMesh(terrain);
        WorldMapBaker.Bake(terrain);

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
        // 물 셰이더(Game/Water): 깊이(물 깊이·거품) + 불투명 텍스처(굴절)
        serialized.FindProperty("m_RequireDepthTexture").boolValue = true;
        serialized.FindProperty("m_RequireOpaqueTexture").boolValue = true;
        serialized.ApplyModifiedProperties();
        configureRendering(asset);
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

        // 트롤 능선은 낮은 고원 (+5m, 길 두 개가 완만하게 올라간다).
        // 높이를 지정한 구역은 맨 앞에 둔다: sampleHeight 는 가중치가 같으면 앞 구역을 고르는데, 예전에는 같은 자리의 몬스터 스폰 구역(기본 높이)이 앞이라
        // 반경 28m 에서 고원 높이로 8m 가 뚝 끊기는 절벽 고리가 생겨 능선을 빠져나갈 수 없었다 (Issues 40).
        zones.Insert(0, new FlatZone { Center = WorldRegions.TrollRidge, Radius = 30.0f, Height = WorldRegions.BaseHeight + 5.0f });
        return zones;
    }

    // ------------------------------------------------------------------ camera

    private static TopDownCamera buildCamera()
    {
        var go = new GameObject("Main Camera");
        go.tag = "MainCamera";
        var camera = go.AddComponent<Camera>();
        camera.fieldOfView = 40.0f;
        camera.nearClipPlane = 0.5f;
        camera.farClipPlane = 2000.0f;
        camera.allowHDR = true;
        // URP 절차 스카이박스 (WorldBuilder.Sky). 2026-10-30 WeatherMaker 제거.
        camera.clearFlags = CameraClearFlags.Skybox;
        go.AddComponent<AudioListener>();
        var data = go.AddComponent<UniversalAdditionalCameraData>();
        data.renderPostProcessing = true;
        data.antialiasing = AntialiasingMode.SubpixelMorphologicalAntiAliasing;
        data.antialiasingQuality = AntialiasingQuality.High;
        // 컬러 그레이딩 · 블룸 뒤 밴딩(계단 무늬)을 줄인다.
        data.dithering = true;
        data.renderShadows = true;
        go.transform.position = new Vector3(WorldRegions.StartGlade.x, 30.0f, WorldRegions.StartGlade.y - 20.0f);
        go.transform.rotation = Quaternion.Euler(50.0f, 45.0f, 0.0f);
        var topDown = go.AddComponent<TopDownCamera>();
        topDown.Camera = camera;
        return topDown;
    }

    // ------------------------------------------------------------------ navmesh / build settings

    /// <summary>NavMesh: 지형 · 건물 · 바위 콜라이더 + 영역 상자(길 = Road, 물 · 가장자리 = Not Walkable, WorldBuilder.Roads). 걸을 수 있는 곳 = NavMesh.</summary>
    private static void bakeNavMesh(Terrain InTerrain)
    {
        var go = new GameObject("[NavMesh]");
        buildNavAreas(InTerrain, go.transform);
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
