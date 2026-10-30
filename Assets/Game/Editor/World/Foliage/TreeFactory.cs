using System.Collections.Generic;
using Framework.Editor;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// 절차 나무/덤불 프리팹 생성 (Art/Generated/World/Foliage). WorldBuilder(장식 나무)와 ContentBuilder(채집 나무)가 공용.
/// 나무 프리팹 구조:
///   Tree_Spruce_0  (LODGroup, FoliageInstance, CameraOccluder)
///     LOD0  (MeshRenderer: 껍질 + 잎)  └ SunShade (MeshCollider, SunBlocker 레이어 = 그늘 판정 + 카메라 가림 판정)
///     LOD1  (원거리 메시)
/// 배치 정적 플래그 중 BatchingStatic 은 끈다 — 정적 배칭은 정점을 월드로 굽기 때문에 셰이더의 개체별 바람 위상/색 변화가 사라진다.
/// </summary>
public static class TreeFactory
{
    public const int SpruceVariants = 4;
    public const int BroadleafVariants = 3;
    public const int BirchVariants = 3;
    public const int DeadVariants = 2;

    private const string PolyHavenBark = EditorPaths.PolyHaven + "/Bark/";
    private const string TerrainSamplePrefabs = "Assets/ExternalAssets/TerrainSampleAssets/Prefabs";
    private static readonly string[] BushSources = { "Bush_A", "Bush_B", "BushDry_A", "BushDry_B" };

    private static string Folder
    {
        get { return EditorPaths.Generated + "/World/Foliage"; }
    }

    public static int VariantCount(ETreeSpecies InSpecies)
    {
        switch (InSpecies)
        {
            case ETreeSpecies.Spruce:
                return SpruceVariants;
            case ETreeSpecies.Broadleaf:
                return BroadleafVariants;
            case ETreeSpecies.Birch:
                return BirchVariants;
            default:
                return DeadVariants;
        }
    }

    public static int BushCount
    {
        get { return BushSources.Length; }
    }

    /// <summary>모든 나무/덤불 프리팹을 다시 만든다.</summary>
    [MenuItem("Game/World/Rebuild Tree Prefabs", priority = 41)]
    public static void BuildAll()
    {
        for (int s = 0; s <= (int)ETreeSpecies.Dead; ++s)
        {
            var species = (ETreeSpecies)s;
            for (int v = 0; v < VariantCount(species); ++v)
            {
                buildTree(species, v);
            }
        }

        for (int i = 0; i < BushSources.Length; ++i)
        {
            buildBush(i);
        }

        AssetDatabase.SaveAssets();
    }

    /// <summary>프리팹이 없으면 만들고 돌려준다.</summary>
    public static GameObject GetTree(ETreeSpecies InSpecies, int InVariant)
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(treePath(InSpecies, InVariant));
        return prefab != null ? prefab : buildTree(InSpecies, InVariant);
    }

    public static GameObject GetBush(int InIndex)
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>($"{Folder}/Bush_{InIndex}.prefab");
        return prefab != null ? prefab : buildBush(InIndex);
    }

    // ------------------------------------------------------------------ trees

    private static string treePath(ETreeSpecies InSpecies, int InVariant)
    {
        return $"{Folder}/Tree_{InSpecies}_{InVariant}.prefab";
    }

    private static GameObject buildTree(ETreeSpecies InSpecies, int InVariant)
    {
        EditorAssetUtil.EnsureFolder(Folder + "/Meshes");
        int seed = 9000 + (int)InSpecies * 100 + InVariant * 17;
        var skeleton = TreeGenerator.Build(InSpecies, seed);
        string name = $"Tree_{InSpecies}_{InVariant}";
        var lod0 = saveMesh(TreeGenerator.ToMesh(skeleton, 0, name + "_LOD0"));
        var lod1 = saveMesh(TreeGenerator.ToMesh(skeleton, 1, name + "_LOD1"));
        var materials = treeMaterials(InSpecies);

        var root = new GameObject(name);
        var r0 = addRenderer(root.transform, "LOD0", lod0, materials, ShadowCastingMode.On);
        var r1 = addRenderer(root.transform, "LOD1", lod1, materials, ShadowCastingMode.On);
        var group = root.AddComponent<LODGroup>();
        group.SetLODs(new[]
        {
            new LOD(0.16f, new Renderer[] { r0 }),
            new LOD(0.012f, new Renderer[] { r1 }),
        });
        group.RecalculateBounds();

        root.AddComponent<FoliageInstance>().Radius = skeleton.TrunkRadius + 0.35f;
        root.AddComponent<Framework.CameraOccluder>();
        ShadeColliderUtil.AddShadeColliders(root, LayerId.SunBlocker);
        setStaticFlags(root);

        var prefab = PrefabUtility.SaveAsPrefabAsset(root, treePath(InSpecies, InVariant));
        Object.DestroyImmediate(root);
        return prefab;
    }

    private static Material[] treeMaterials(ETreeSpecies InSpecies)
    {
        switch (InSpecies)
        {
            case ETreeSpecies.Spruce:
                return new[]
                {
                    barkMaterial("M_Bark_Pine", polyHaven("pine_bark", "diff", false), polyHaven("pine_bark", "nor_gl", true), new Color(0.62f, 0.52f, 0.47f)),
                    leafMaterial("M_Leaves_Spruce", FoliageTextures.SpruceSpray, new Color(0.85f, 0.95f, 0.85f), new Color(0.45f, 0.6f, 0.3f), 0.35f, 0.4f),
                };
            case ETreeSpecies.Birch:
                return new[]
                {
                    barkMaterial("M_Bark_Birch", FoliageTextures.BirchBark, FoliageTextures.BirchBarkNormal, Color.white),
                    leafMaterial("M_Leaves_Birch", FoliageTextures.BirchCluster, Color.white, new Color(0.85f, 0.95f, 0.4f), 0.8f, 0.3f),
                };
            case ETreeSpecies.Dead:
                return new[]
                {
                    barkMaterial("M_Bark_Dead", polyHaven("bark_willow_02", "diff", false), polyHaven("bark_willow_02", "nor_gl", true), new Color(0.72f, 0.7f, 0.68f)),
                };
            default:
                return new[]
                {
                    barkMaterial("M_Bark_Oak", polyHaven("bark_brown_02", "diff", false), polyHaven("bark_brown_02", "nor_gl", true), new Color(0.72f, 0.66f, 0.6f)),
                    leafMaterial("M_Leaves_Broadleaf", FoliageTextures.BroadleafCluster, Color.white, new Color(0.75f, 0.95f, 0.35f), 0.7f, 0.35f),
                };
        }
    }

    // ------------------------------------------------------------------ bushes

    /// <summary>TerrainSampleAssets 덤불 메시 + Game/Foliage 머티리얼 (원본 HDRP 셰이더 그래프는 URP 에서 그려지지 않음, Issues 19).</summary>
    private static GameObject buildBush(int InIndex)
    {
        var source = AssetDatabase.LoadAssetAtPath<GameObject>($"{TerrainSamplePrefabs}/{BushSources[InIndex]}.prefab");
        if (source == null)
        {
            return null;
        }

        var filter = source.GetComponentInChildren<MeshFilter>();
        var renderer = source.GetComponentInChildren<MeshRenderer>();
        if (filter == null || renderer == null)
        {
            return null;
        }

        var root = new GameObject($"Bush_{InIndex}");
        var material = ConvertToFoliage(renderer.sharedMaterial, 0.6f, 0.18f);
        var model = addRenderer(root.transform, "Model", filter.sharedMesh, new[] { material }, ShadowCastingMode.On);
        model.transform.localScale = Vector3.one * 2.0f;
        root.AddComponent<FoliageInstance>().Radius = 0.9f;
        setStaticFlags(root);
        var prefab = PrefabUtility.SaveAsPrefabAsset(root, $"{Folder}/Bush_{InIndex}.prefab");
        Object.DestroyImmediate(root);
        return prefab;
    }

    /// <summary>
    /// 외부 식생 머티리얼의 BaseColor/Normal 텍스처를 Game/Foliage 머티리얼 사본으로 옮긴다 (풀 Detail, 덤불).
    /// 외부 메시는 바람 정점 색이 없으므로 오브젝트 높이(InSwayHeight m)를 흔들림 가중치로 쓴다.
    /// </summary>
    public static Material ConvertToFoliage(Material InSource, float InSwayHeight, float InWindStrength)
    {
        EditorAssetUtil.EnsureFolder(Folder + "/Materials");
        string path = $"{Folder}/Materials/M_{InSource.name}.mat";
        var material = loadOrCreate(path);
        var shader = InSource.shader;
        for (int i = 0; i < shader.GetPropertyCount(); ++i)
        {
            if (shader.GetPropertyType(i) != ShaderPropertyType.Texture)
            {
                continue;
            }

            var texture = InSource.GetTexture(shader.GetPropertyName(i));
            if (texture == null)
            {
                continue;
            }

            if (texture.name.Contains("BaseColor") == true)
            {
                material.SetTexture("_BaseMap", texture);
            }
            else if (texture.name.Contains("Normal") == true)
            {
                material.SetTexture("_BumpMap", texture);
                material.EnableKeyword("_NORMALMAP");
            }
        }

        material.SetColor("_BaseColor", Color.white);
        material.SetFloat("_Cutoff", 0.45f);
        material.SetFloat("_Smoothness", 0.1f);
        material.SetFloat("_Translucency", 0.5f);
        material.SetColor("_HueVariation", new Color(0.85f, 0.8f, 0.4f, 0.3f));
        material.SetFloat("_UseHeightWeight", 1.0f);
        material.SetFloat("_SwayHeight", InSwayHeight);
        material.SetFloat("_WindStrength", InWindStrength);
        material.SetFloat("_WindSpeed", 1.6f);
        material.SetFloat("_WindFlutter", 0.02f);
        material.SetFloat("_UseVertexAO", 0.0f);
        material.SetFloat("_FlipBackNormals", 1.0f);
        material.SetFloat("_Cull", (float)CullMode.Off);
        material.enableInstancing = true;
        EditorUtility.SetDirty(material);
        return material;
    }

    // ------------------------------------------------------------------ materials

    private static Material barkMaterial(string InName, Texture2D InAlbedo, Texture2D InNormal, Color InTint)
    {
        var material = loadOrCreate($"{Folder}/Materials/{InName}.mat");
        material.SetTexture("_BaseMap", InAlbedo);
        material.SetColor("_BaseColor", InTint);
        material.SetFloat("_Cutoff", 0.0f);
        material.SetFloat("_Smoothness", 0.08f);
        if (InNormal != null)
        {
            material.SetTexture("_BumpMap", InNormal);
            material.SetFloat("_BumpScale", 1.2f);
            material.EnableKeyword("_NORMALMAP");
        }

        material.SetFloat("_Translucency", 0.0f);
        material.SetColor("_HueVariation", new Color(1.0f, 1.0f, 1.0f, 0.0f));
        setTreeWind(material);
        material.SetFloat("_WindFlutter", 0.0f);
        material.SetFloat("_FlipBackNormals", 0.0f);
        material.SetFloat("_Cull", (float)CullMode.Back);
        material.renderQueue = (int)RenderQueue.Geometry;
        EditorUtility.SetDirty(material);
        return material;
    }

    private static Material leafMaterial(string InName, Texture2D InAlbedo, Color InTint, Color InTranslucency, float InTranslucencyAmount, float InHueAmount)
    {
        var material = loadOrCreate($"{Folder}/Materials/{InName}.mat");
        material.SetTexture("_BaseMap", InAlbedo);
        material.SetColor("_BaseColor", InTint);
        material.SetFloat("_Cutoff", 0.42f);
        material.SetFloat("_Smoothness", 0.18f);
        material.DisableKeyword("_NORMALMAP");
        material.SetFloat("_Translucency", InTranslucencyAmount);
        material.SetColor("_TranslucencyColor", InTranslucency);
        material.SetColor("_HueVariation", new Color(0.95f, 0.8f, 0.4f, InHueAmount));
        setTreeWind(material);
        material.SetFloat("_WindFlutter", 0.05f);
        // 잎 카드는 수관 기준 구형 노멀이라 뒷면에서도 같은 노멀을 써야 덩어리로 보인다.
        material.SetFloat("_FlipBackNormals", 0.0f);
        material.SetFloat("_Cull", (float)CullMode.Off);
        material.renderQueue = (int)RenderQueue.AlphaTest;
        EditorUtility.SetDirty(material);
        return material;
    }

    /// <summary>같은 나무의 껍질/잎이 같이 움직이도록 바람 값을 공유한다.</summary>
    private static void setTreeWind(Material InMaterial)
    {
        InMaterial.SetFloat("_UseHeightWeight", 0.0f);
        InMaterial.SetFloat("_UseVertexAO", 1.0f);
        InMaterial.SetFloat("_WindStrength", 0.35f);
        InMaterial.SetFloat("_WindSpeed", 0.9f);
        InMaterial.SetVector("_WindDirection", new Vector4(1.0f, 0.0f, 0.35f, 0.0f));
        InMaterial.enableInstancing = true;
    }

    private static Material loadOrCreate(string InPath)
    {
        EditorAssetUtil.EnsureFolder(Folder + "/Materials");
        var shader = Shader.Find("Game/Foliage");
        var material = AssetDatabase.LoadAssetAtPath<Material>(InPath);
        if (material == null)
        {
            material = new Material(shader);
            AssetDatabase.CreateAsset(material, InPath);
        }

        material.shader = shader;
        return material;
    }

    private static Texture2D polyHaven(string InName, string InMap, bool InIsNormal)
    {
        string path = $"{PolyHavenBark}{InName}_{InMap}_1k.jpg";
        var importer = AssetImporter.GetAtPath(path) as TextureImporter;
        if (importer != null && InIsNormal == true && importer.textureType != TextureImporterType.NormalMap)
        {
            importer.textureType = TextureImporterType.NormalMap;
            importer.SaveAndReimport();
        }

        return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
    }

    // ------------------------------------------------------------------ helpers

    private static Mesh saveMesh(Mesh InMesh)
    {
        string path = $"{Folder}/Meshes/{InMesh.name}.asset";
        var existing = AssetDatabase.LoadAssetAtPath<Mesh>(path);
        if (existing != null)
        {
            // 같은 에셋에 덮어써서 GUID 를 유지한다 (씬/프리팹 참조가 끊기지 않게).
            // CopySerialized 는 서브메시 범위가 옛 값으로 남는 경우가 있어 데이터를 직접 옮긴다.
            existing.Clear();
            existing.indexFormat = InMesh.indexFormat;
            existing.vertices = InMesh.vertices;
            existing.normals = InMesh.normals;
            existing.tangents = InMesh.tangents;
            existing.uv = InMesh.uv;
            existing.colors = InMesh.colors;
            existing.subMeshCount = InMesh.subMeshCount;
            for (int i = 0; i < InMesh.subMeshCount; ++i)
            {
                existing.SetTriangles(InMesh.GetTriangles(i), i);
            }

            existing.RecalculateBounds();
            Object.DestroyImmediate(InMesh);
            EditorUtility.SetDirty(existing);
            return existing;
        }

        AssetDatabase.CreateAsset(InMesh, path);
        return InMesh;
    }

    private static MeshRenderer addRenderer(Transform InParent, string InName, Mesh InMesh, Material[] InMaterials, ShadowCastingMode InShadows)
    {
        var go = new GameObject(InName);
        go.transform.SetParent(InParent, false);
        go.AddComponent<MeshFilter>().sharedMesh = InMesh;
        var renderer = go.AddComponent<MeshRenderer>();
        var materials = new Material[Mathf.Min(InMesh.subMeshCount, InMaterials.Length)];
        for (int i = 0; i < materials.Length; ++i)
        {
            materials[i] = InMaterials[i];
        }

        renderer.sharedMaterials = materials;
        renderer.shadowCastingMode = InShadows;
        renderer.lightProbeUsage = LightProbeUsage.BlendProbes;
        return renderer;
    }

    private static void setStaticFlags(GameObject InRoot)
    {
        var flags = StaticEditorFlags.OccludeeStatic | StaticEditorFlags.ReflectionProbeStatic;
        foreach (var t in InRoot.GetComponentsInChildren<Transform>(true))
        {
            GameObjectUtility.SetStaticEditorFlags(t.gameObject, flags);
        }
    }

    /// <summary>나무 종류 전체 목록 (배치용). 가중치는 WorldBuilder 가 정한다.</summary>
    public static List<GameObject> GetVariants(ETreeSpecies InSpecies)
    {
        var list = new List<GameObject>();
        for (int v = 0; v < VariantCount(InSpecies); ++v)
        {
            var prefab = GetTree(InSpecies, v);
            if (prefab != null)
            {
                list.Add(prefab);
            }
        }

        return list;
    }
}
