using System.IO;
using Framework.Editor;
using UnityEditor;
using UnityEngine;

/// <summary>
/// 채집 노드 / 건축물 프리팹.
///  - 건축물: ASTROFISH Medieval Village HD 모듈 (3m 그리드). 모듈 피벗이 끝점에 있어 바운드 기준으로 가운데 정렬한다.
///  - 채집 노드: 바위 = TheTalesFactory 포토스캔 바위(URP 변환), 덤불 = TerrainSampleAssets, 나무 = Kenney (사실적인 나무 에셋 없음 → AssetRequests.md)
/// </summary>
public static partial class ContentBuilder
{
    private const string Astro = External + "/ASTROFISH_GAMES/MEDIEVAL_SERIES/MEDIEVAL_VILLAGE_HD/_Assets";
    private const string TalesRocks = External + "/TheTalesFactory/Photoscanned MoutainsRocks PBR/Prefabs";
    private const string TerrainSample = External + "/TerrainSampleAssets/Prefabs";
    private const float Grid = 3.0f;

    // ------------------------------------------------------------------ resource nodes

    private static void buildResourceNodes()
    {
        buildTree("Nodes/PineTree", "tree_pineTallA", 4.5f);
        buildTree("Nodes/OakTree", "tree_oak_dark", 5.0f);
        buildRealRock("Nodes/Rock", TalesRocks + "/MountainRocks02_B.prefab", 3.2f, null);
        buildRealRock("Nodes/Boulder", TalesRocks + "/MountainRocks01_D.prefab", 5.0f, null);
        buildRealRock("Nodes/CopperVein", TalesRocks + "/MountainRocks02_A.prefab", 3.4f, CopperMaterial);

        var bush = new GameObject("Bush");
        var model = instantiateExternal(TerrainSample + "/Bush_B.prefab", bush.transform);
        model.transform.localScale = Vector3.one * 2.4f;
        setShadows(bush, UnityEngine.Rendering.ShadowCastingMode.Off);
        setLayer(bush, LayerId.Interactable);
        saveShadedPrefab(bush, "Nodes/Bush");
    }

    /// <summary>
    /// Kenney 나무는 잎 재질이 URP 조명에서 청록색으로 떠서 사실적인 지형과 어울리지 않는다.
    /// 잎/줄기 재질을 어두운 초록·갈색 URP 재질로 바꾼 사본 프리팹을 만든다 (지형 Tree 와 채집 나무가 공용).
    /// </summary>
    public static GameObject GetRecoloredTree(string InModel)
    {
        string folder = EditorPaths.Generated + "/World/Trees";
        EditorAssetUtil.EnsureFolder(folder);
        string path = $"{folder}/{InModel}.prefab";
        var leaves = loadOrCreate(folder + "/M_TreeLeaves.mat", Shader.Find("Universal Render Pipeline/Lit"));
        leaves.SetColor("_BaseColor", new Color(0.16f, 0.27f, 0.11f));
        leaves.SetFloat("_Smoothness", 0.15f);
        var bark = loadOrCreate(folder + "/M_TreeBark.mat", Shader.Find("Universal Render Pipeline/Lit"));
        bark.SetColor("_BaseColor", new Color(0.24f, 0.17f, 0.12f));
        bark.SetFloat("_Smoothness", 0.1f);
        EditorUtility.SetDirty(leaves);
        EditorUtility.SetDirty(bark);

        var model = AssetDatabase.LoadAssetAtPath<GameObject>(nature(InModel));
        var instance = (GameObject)PrefabUtility.InstantiatePrefab(model);
        PrefabUtility.UnpackPrefabInstance(instance, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
        foreach (var renderer in instance.GetComponentsInChildren<Renderer>(true))
        {
            var materials = renderer.sharedMaterials;
            for (int i = 0; i < materials.Length; ++i)
            {
                bool isLeaf = materials[i] != null && materials[i].name.ToLowerInvariant().Contains("leaf") == true;
                materials[i] = isLeaf == true ? leaves : bark;
            }

            renderer.sharedMaterials = materials;
        }

        var prefab = PrefabUtility.SaveAsPrefabAsset(instance, path);
        Object.DestroyImmediate(instance);
        return prefab;
    }

    private static void buildTree(string InAddress, string InModel, float InScale)
    {
        var root = new GameObject(Path.GetFileName(InAddress));
        var tree = (GameObject)PrefabUtility.InstantiatePrefab(GetRecoloredTree(InModel));
        PrefabUtility.UnpackPrefabInstance(tree, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
        tree.transform.SetParent(root.transform, false);
        tree.transform.localScale = Vector3.one * InScale;
        setLayer(root, LayerId.Interactable);

        var trunk = root.AddComponent<CapsuleCollider>();
        trunk.center = new Vector3(0.0f, 1.5f, 0.0f);
        trunk.height = 3.0f;
        trunk.radius = 0.35f;

        float height = InScale * 1.4f;
        var canopy = new GameObject("Canopy");
        canopy.transform.SetParent(root.transform, false);
        canopy.transform.localPosition = new Vector3(0.0f, height * 0.6f, 0.0f);
        canopy.layer = LayerId.SunBlocker;
        var sphere = canopy.AddComponent<SphereCollider>();
        sphere.isTrigger = true;
        sphere.radius = InScale * 0.42f;

        addObstacle(root, new Vector3(0.0f, 1.0f, 0.0f), new Vector3(0.8f, 2.0f, 0.8f), true);
        saveShadedPrefab(root, InAddress);
    }

    /// <summary>포토스캔 바위를 가로 최대 크기 InSize(m) 로 맞춰 채집 노드로 만든다.</summary>
    private static void buildRealRock(string InAddress, string InPrefab, float InSize, Material InOverride)
    {
        var root = new GameObject(Path.GetFileName(InAddress));
        var model = instantiateExternal(InPrefab, root.transform);
        stripToVisuals(model);
        MaterialUpgradeUtil.UpgradeRenderers(model, EditorPaths.Generated + "/Materials/Rocks");
        fitHorizontal(model, InSize);
        groundAlign(root, model, 0.25f);

        if (InOverride != null)
        {
            var tinted = MaterialUpgradeUtil.Upgrade(model.GetComponentInChildren<Renderer>().sharedMaterial, EditorPaths.Generated + "/Materials/Rocks", false);
            var copper = loadOrCreate(EditorPaths.Generated + "/Materials/M_CopperRock.mat", tinted.shader);
            copper.CopyPropertiesFromMaterial(tinted);
            copper.SetColor("_BaseColor", new Color(0.95f, 0.6f, 0.35f));
            copper.SetFloat("_Metallic", 0.6f);
            EditorUtility.SetDirty(copper);
            foreach (var renderer in model.GetComponentsInChildren<Renderer>())
            {
                var materials = renderer.sharedMaterials;
                for (int i = 0; i < materials.Length; ++i)
                {
                    materials[i] = copper;
                }

                renderer.sharedMaterials = materials;
            }
        }

        var bounds = computeBounds(model);
        var box = root.AddComponent<BoxCollider>();
        box.center = root.transform.InverseTransformPoint(bounds.center);
        box.size = bounds.size;
        setLayer(root, LayerId.Interactable);
        addObstacle(root, box.center, box.size, false);
        saveShadedPrefab(root, InAddress);
    }

    // ------------------------------------------------------------------ structures

    private static void buildStructures()
    {
        // 성 심장: 석조 기둥 받침 + 큰 횃불 + 붉은 코어
        var heart = new GameObject("CastleHeart");
        var column = placeCentered(Astro + "/PltfrmSprts/Prfbs/Medieval_PortchColumn_Medium.prefab", heart.transform, 0.0f, 2.2f);
        var torch = placeCentered(Astro + "/Iron/Prefbs/Medieval_IronFixtures_LargeTorch.prefab", heart.transform, 2.2f, 0.9f);
        var core = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        Object.DestroyImmediate(core.GetComponent<Collider>());
        core.transform.SetParent(heart.transform, false);
        core.transform.localPosition = new Vector3(0.0f, 3.0f, 0.0f);
        core.transform.localScale = Vector3.one * 0.55f;
        core.GetComponent<MeshRenderer>().sharedMaterial = createEmissive("M_Heart", BloodRed, 6.0f);
        addPointLight(heart.transform, new Vector3(0.0f, 3.0f, 0.0f), BloodRed, 12.0f, 4.0f);
        addStructureCollider(heart, new Vector3(0.0f, 1.2f, 0.0f), new Vector3(1.0f, 2.4f, 1.0f));
        saveShadedPrefab(heart, "Structures/CastleHeart");

        buildModule("Structures/Floor", Astro + "/Ground/Prfbs/Medieval_StoneFloor_Tile.prefab", new Vector3(Grid, 0.4f, Grid), false, 0.0f);
        buildModule("Structures/Wall", Astro + "/Walls/Prfbs/Medieval_PlasterWalls_B_MasonaryWall.prefab", new Vector3(Grid, 3.0f, 0.6f), true, 0.0f);
        buildModule("Structures/WallWindow", Astro + "/Walls/Prfbs/Medieval_PlasterWalls_A_WindowBase.prefab", new Vector3(Grid, 3.0f, 0.6f), true, 0.0f);
        buildDoorWall();
        buildModule("Structures/Pillar", Astro + "/PltfrmSprts/Prfbs/Medieval_PortchColumn_Medium.prefab", new Vector3(0.6f, 2.6f, 0.6f), true, 2.6f);

        // 작업대: 판자 받침 + Crafting 팩 망치/톱
        var bench = new GameObject("Workbench");
        placeCentered(Astro + "/PltfrmSprts/Prfbs/Medieval_Platforms_ShortBoardPlanks_Supports.prefab", bench.transform, 0.0f, 0.0f);
        var hammer = instantiateExternal(CraftRoot + "/Props/Hammer.FBX", bench.transform);
        hammer.transform.localPosition = new Vector3(-0.6f, 0.8f, 0.0f);
        hammer.transform.localRotation = Quaternion.Euler(0.0f, 30.0f, 90.0f);
        var saw = instantiateExternal(CraftRoot + "/Props/Saw.FBX", bench.transform);
        saw.transform.localPosition = new Vector3(0.6f, 0.8f, 0.1f);
        saw.transform.localRotation = Quaternion.Euler(90.0f, 0.0f, 0.0f);
        MaterialUpgradeUtil.UpgradeRenderers(bench, EditorPaths.Generated + "/Materials/Props");
        addStructureCollider(bench, new Vector3(0.0f, 0.4f, 0.0f), new Vector3(2.8f, 0.8f, 1.4f));
        saveShadedPrefab(bench, "Structures/Workbench");

        var smelter = new GameObject("Smelter");
        placeCentered(Astro + "/Chimneys/Prfbs/Medieval_Chimney_Small.prefab", smelter.transform, 0.0f, 2.6f);
        addPointLight(smelter.transform, new Vector3(0.0f, 1.0f, 0.0f), new Color(1.0f, 0.5f, 0.15f), 7.0f, 3.0f);
        addStructureCollider(smelter, new Vector3(0.0f, 1.3f, 0.0f), new Vector3(1.6f, 2.6f, 1.6f));
        saveShadedPrefab(smelter, "Structures/Smelter");

        var coffin = new GameObject("Coffin");
        var box = instantiateExternal(CraftRoot + "/Props/Box.FBX", coffin.transform);
        MaterialUpgradeUtil.UpgradeRenderers(box, EditorPaths.Generated + "/Materials/Props");
        fitBox(box, new Vector3(2.0f, 0.7f, 0.9f));
        groundAlign(coffin, box, 0.0f);
        addStructureCollider(coffin, new Vector3(0.0f, 0.35f, 0.0f), new Vector3(2.0f, 0.7f, 0.9f));
        saveShadedPrefab(coffin, "Structures/Coffin");

        var lamp = new GameObject("Lamp");
        placeCentered(Astro + "/PltfrmSprts/Prfbs/Medieval_PortchColumn_Medium.prefab", lamp.transform, 0.0f, 2.0f);
        placeCentered(Astro + "/Iron/Prefbs/Medieval_IronFixtures_Lamp.prefab", lamp.transform, 2.0f, 0.7f);
        addPointLight(lamp.transform, new Vector3(0.0f, 2.3f, 0.0f), new Color(1.0f, 0.75f, 0.45f), 9.0f, 2.5f);
        addStructureCollider(lamp, new Vector3(0.0f, 1.0f, 0.0f), new Vector3(0.6f, 2.0f, 0.6f));
        saveShadedPrefab(lamp, "Structures/Banner");
    }

    /// <summary>모듈 1개를 가운데 정렬 + 목표 크기(InSize)로 맞춘 건축물. InHeightOnly 면 높이만 맞춘다.</summary>
    private static void buildModule(string InAddress, string InPrefab, Vector3 InSize, bool InObstacle, float InHeightOnly)
    {
        var root = new GameObject(Path.GetFileName(InAddress));
        var model = instantiateExternal(InPrefab, root.transform);
        stripToVisuals(model);
        MaterialUpgradeUtil.UpgradeRenderers(model, EditorPaths.Generated + "/Materials/Castle");
        if (InHeightOnly > 0.0f)
        {
            fitHeight(model, InHeightOnly);
        }
        else
        {
            fitBox(model, InSize);
        }

        groundAlign(root, model, 0.0f);
        var bounds = computeBounds(model);
        addStructureCollider(root, root.transform.InverseTransformPoint(bounds.center), bounds.size);
        if (InObstacle == true)
        {
            addObstacle(root, root.transform.InverseTransformPoint(bounds.center), bounds.size, false);
        }

        saveShadedPrefab(root, InAddress);
    }

    /// <summary>문이 있는 벽: 문 구멍을 비워 두도록 좌/우/상단 세 개의 콜라이더를 쓴다.</summary>
    private static void buildDoorWall()
    {
        var root = new GameObject("WallDoor");
        var model = instantiateExternal(Astro + "/Walls/Prfbs/Medieval_WallTall_DoorCropped.prefab", root.transform);
        stripToVisuals(model);
        MaterialUpgradeUtil.UpgradeRenderers(model, EditorPaths.Generated + "/Materials/Castle");
        fitBox(model, new Vector3(Grid, 3.2f, 0.6f));
        groundAlign(root, model, 0.0f);

        foreach (var x in new[] { -1.05f, 1.05f })
        {
            var side = root.AddComponent<BoxCollider>();
            side.center = new Vector3(x, 1.6f, 0.0f);
            side.size = new Vector3(0.9f, 3.2f, 0.6f);
        }

        var top = root.AddComponent<BoxCollider>();
        top.center = new Vector3(0.0f, 2.8f, 0.0f);
        top.size = new Vector3(1.2f, 0.8f, 0.6f);
        setLayer(root, LayerId.Structure);
        saveShadedPrefab(root, "Structures/WallDoor");
    }

    // ------------------------------------------------------------------ fitting helpers

    /// <summary>외부 프리팹을 InParent 아래 가운데 정렬로 놓는다. InHeight > 0 이면 그 높이로 스케일, InBottom 은 바닥 높이.</summary>
    private static GameObject placeCentered(string InPrefab, Transform InParent, float InBottom, float InHeight)
    {
        var holder = new GameObject(Path.GetFileNameWithoutExtension(InPrefab));
        holder.transform.SetParent(InParent, false);
        var model = instantiateExternal(InPrefab, holder.transform);
        stripToVisuals(model);
        MaterialUpgradeUtil.UpgradeRenderers(model, EditorPaths.Generated + "/Materials/Castle");
        if (InHeight > 0.0f)
        {
            fitHeight(model, InHeight);
        }

        groundAlign(holder, model, 0.0f);
        holder.transform.localPosition = new Vector3(0.0f, InBottom, 0.0f);
        return holder;
    }

    private static void fitHeight(GameObject InModel, float InHeight)
    {
        var bounds = computeBounds(InModel);
        if (bounds.size.y > 0.001f)
        {
            InModel.transform.localScale *= InHeight / bounds.size.y;
        }
    }

    private static void fitHorizontal(GameObject InModel, float InSize)
    {
        var bounds = computeBounds(InModel);
        float horizontal = Mathf.Max(bounds.size.x, bounds.size.z);
        if (horizontal > 0.001f)
        {
            InModel.transform.localScale *= InSize / horizontal;
        }
    }

    /// <summary>바운드가 InSize 에 맞도록 축별 스케일 (모듈이 3m 그리드에 딱 맞게).</summary>
    private static void fitBox(GameObject InModel, Vector3 InSize)
    {
        var bounds = computeBounds(InModel);
        var scale = InModel.transform.localScale;
        if (bounds.size.x > 0.001f)
        {
            scale.x *= InSize.x / bounds.size.x;
        }

        if (bounds.size.y > 0.001f)
        {
            scale.y *= InSize.y / bounds.size.y;
        }

        if (bounds.size.z > 0.001f)
        {
            scale.z *= InSize.z / bounds.size.z;
        }

        InModel.transform.localScale = scale;
    }

    /// <summary>모델 바운드의 XZ 중심을 루트 원점으로, 바닥을 y=0 (InSink 만큼 땅에 묻음) 으로 옮긴다.</summary>
    private static void groundAlign(GameObject InRoot, GameObject InModel, float InSink)
    {
        var bounds = computeBounds(InModel);
        Vector3 local = InRoot.transform.InverseTransformPoint(bounds.center);
        float bottom = InRoot.transform.InverseTransformPoint(bounds.min).y;
        InModel.transform.localPosition -= new Vector3(local.x, bottom + InSink, local.z);
    }

    private static Bounds computeBounds(GameObject InObject)
    {
        var renderers = InObject.GetComponentsInChildren<Renderer>();
        var bounds = new Bounds(InObject.transform.position, Vector3.zero);
        bool first = true;
        foreach (var renderer in renderers)
        {
            if (first == true)
            {
                bounds = renderer.bounds;
                first = false;
            }
            else
            {
                bounds.Encapsulate(renderer.bounds);
            }
        }

        return bounds;
    }

    private static void addStructureCollider(GameObject InRoot, Vector3 InCenter, Vector3 InSize)
    {
        var box = InRoot.AddComponent<BoxCollider>();
        box.center = InCenter;
        box.size = InSize;
        setLayer(InRoot, LayerId.Structure);
    }
}
