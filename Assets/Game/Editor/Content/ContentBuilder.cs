using Framework.Editor;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Rendering;

/// <summary>
/// 무료 에셋(Kenney CC0)과 코드 생성 재질/파티클로 Content 폴더의 Addressable 프리팹을 만든다.
/// 결과 경로 = Content/{AssetTable.Address}.prefab — 주소 규칙과 1:1 이다.
/// 다시 실행하면 덮어쓴다 (수작업 수정은 Kenney 원본이 아니라 이 빌더에서 한다).
/// </summary>
public static partial class ContentBuilder
{

    [MenuItem("Game/Setup/3. Build Content (Prefabs, VFX, Icons)", priority = 3)]
    public static void BuildAll()
    {
        EditorAssetUtil.EnsureFolder(EditorPaths.Content);
        EditorAssetUtil.EnsureFolder(EditorPaths.Generated);

        MaterialUpgradeUtil.ClearCache();

        buildMaterials();
        buildIcons();
        buildVfx();
        buildProjectiles();
        buildSkillVfx();
        buildWeapons();
        buildCharacters();
        buildResourceNodes();
        buildStructures();
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("[Content] build complete");
    }

    // ------------------------------------------------------------------ helpers

    private static string contentPath(string InAddress, string InExtension)
    {
        string path = $"{EditorPaths.Content}/{InAddress}{InExtension}";
        EditorAssetUtil.EnsureFolder(Path.GetDirectoryName(path).Replace('\\', '/'));
        return path;
    }

    private static GameObject savePrefab(GameObject InRoot, string InAddress)
    {
        string path = contentPath(InAddress, ".prefab");
        var prefab = PrefabUtility.SaveAsPrefabAsset(InRoot, path);
        Object.DestroyImmediate(InRoot);
        return prefab;
    }

    /// <summary>그림자를 드리우는 메시에 그늘 판정용 콜라이더(SunBlocker)를 붙이고 저장 — 채집 노드/건축물용 (SunExposure 와 화면 그림자 일치).</summary>
    private static GameObject saveShadedPrefab(GameObject InRoot, string InAddress)
    {
        ShadeColliderUtil.AddShadeColliders(InRoot, LayerId.SunBlocker);
        return savePrefab(InRoot, InAddress);
    }

    private static GameObject instantiateModel(string InModelPath, Transform InParent, float InScale)
    {
        var model = AssetDatabase.LoadAssetAtPath<GameObject>(InModelPath);
        if (model == null)
        {
            Debug.LogError($"[Content] model not found: {InModelPath}");
            var placeholder = GameObject.CreatePrimitive(PrimitiveType.Cube);
            placeholder.transform.SetParent(InParent, false);
            return placeholder;
        }

        var instance = (GameObject)PrefabUtility.InstantiatePrefab(model);
        PrefabUtility.UnpackPrefabInstance(instance, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
        instance.transform.SetParent(InParent, false);
        instance.transform.localScale = Vector3.one * InScale;
        foreach (var collider in instance.GetComponentsInChildren<Collider>())
        {
            Object.DestroyImmediate(collider);
        }

        return instance;
    }

    private static string nature(string InName)
    {
        return $"{EditorPaths.Kenney}/Nature/{InName}.fbx";
    }

    private static string dungeon(string InName)
    {
        return $"{EditorPaths.Kenney}/Dungeon/{InName}.fbx";
    }

    private static void setLayer(GameObject InObject, int InLayer)
    {
        foreach (var t in InObject.GetComponentsInChildren<Transform>(true))
        {
            t.gameObject.layer = InLayer;
        }
    }

    private static void setShadows(GameObject InObject, ShadowCastingMode InMode)
    {
        foreach (var renderer in InObject.GetComponentsInChildren<Renderer>(true))
        {
            renderer.shadowCastingMode = InMode;
        }
    }

    private static Light addPointLight(Transform InParent, Vector3 InLocalPosition, Color InColor, float InRange, float InIntensity)
    {
        var go = new GameObject("Light");
        go.transform.SetParent(InParent, false);
        go.transform.localPosition = InLocalPosition;
        var light = go.AddComponent<Light>();
        light.type = LightType.Point;
        light.color = InColor;
        light.range = InRange;
        light.intensity = InIntensity;
        light.shadows = LightShadows.None;
        return light;
    }

    private static void addObstacle(GameObject InRoot, Vector3 InCenter, Vector3 InSize, bool InCapsule)
    {
        var obstacle = InRoot.AddComponent<NavMeshObstacle>();
        obstacle.shape = InCapsule == true ? NavMeshObstacleShape.Capsule : NavMeshObstacleShape.Box;
        obstacle.center = InCenter;
        obstacle.size = InSize;
        if (InCapsule == true)
        {
            obstacle.radius = InSize.x * 0.5f;
            obstacle.height = InSize.y;
        }

        obstacle.carving = true;
        obstacle.carveOnlyStationary = true;
    }

    // ------------------------------------------------------------------ icons

    private static readonly string[,] IconMap =
    {
        { "Sword", "dun_weapon-sword" }, { "Spear", "dun_weapon-spear" }, { "Potion", "dun_potion" }, { "Coin", "dun_coin" },
        { "Chest", "dun_chest" }, { "Shield", "dun_shield-round" }, { "Wall", "dun_wall" }, { "Floor", "dun_floor" },
        { "Lumber", "nat_log_stack" }, { "Stone", "nat_rock_smallA" }, { "PlantFiber", "nat_plant_bush" }, { "Mushroom", "nat_mushroom_red" },
        { "CopperOre", "dun_rocks" }, { "CastleHeart", "nat_statue_obelisk" }, { "Workbench", "dun_table" }, { "Barrel", "dun_barrel" },
        { "Banner", "dun_banner" }, { "Campfire", "nat_campfire_stones" }, { "Tent", "nat_tent_detailedOpen" },
    };

    private static void buildIcons()
    {
        for (int i = 0; i < IconMap.GetLength(0); ++i)
        {
            string source = $"{EditorPaths.Kenney}/Icons/{IconMap[i, 1]}.png";
            string target = contentPath($"Icons/{IconMap[i, 0]}", ".png");
            if (File.Exists(source) == false)
            {
                Debug.LogWarning($"[Content] icon source missing {source}");
                continue;
            }

            if (File.Exists(target) == false)
            {
                AssetDatabase.CopyAsset(source, target);
            }

            var importer = (TextureImporter)AssetImporter.GetAtPath(target);
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.alphaIsTransparency = true;
            importer.mipmapEnabled = false;
            importer.maxTextureSize = 256;
            importer.SaveAndReimport();
        }
    }
}
