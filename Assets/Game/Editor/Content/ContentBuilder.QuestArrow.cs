using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>캐릭터 퀘스트 화살표 프리팹 (VFX/QuestArrow): 납작한 금색 화살촉 메시 + QuestGuideArrow. 양면 · 그림자 없음 · 반투명 Unlit.</summary>
public static partial class ContentBuilder
{
    private static readonly Color QuestArrowColor = new Color(1.0f, 0.82f, 0.3f, 0.85f);

    [MenuItem("Game/Setup/3d. Build Quest Arrow Only", priority = 3)]
    public static void BuildQuestArrowOnly()
    {
        buildQuestArrow();
        AssetDatabase.SaveAssets();
    }

    private static void buildQuestArrow()
    {
        string folder = EditorPaths.Generated + "/Meshes";
        Framework.Editor.EditorAssetUtil.EnsureFolder(folder);
        string meshPath = folder + "/QuestArrow.asset";
        var mesh = AssetDatabase.LoadAssetAtPath<Mesh>(meshPath);
        if (mesh == null)
        {
            mesh = new Mesh();
            AssetDatabase.CreateAsset(mesh, meshPath);
        }

        // XZ 평면, +Z 방향 화살촉: 끝(0, 0.8) · 오른쪽(0.7, -0.4) · 오목점(0, -0.05) · 왼쪽(-0.7, -0.4).
        mesh.Clear();
        mesh.vertices = new[] { new Vector3(0.0f, 0.0f, 0.8f), new Vector3(0.7f, 0.0f, -0.4f), new Vector3(0.0f, 0.0f, -0.05f), new Vector3(-0.7f, 0.0f, -0.4f) };
        mesh.triangles = new[] { 0, 1, 2, 0, 2, 3 };
        mesh.normals = new[] { Vector3.up, Vector3.up, Vector3.up, Vector3.up };
        mesh.RecalculateBounds();
        EditorUtility.SetDirty(mesh);

        var material = loadOrCreate(EditorPaths.Generated + "/Materials/QuestArrow.mat", Shader.Find("Universal Render Pipeline/Unlit"));
        material.SetColor("_BaseColor", QuestArrowColor);
        material.SetFloat("_Surface", 1.0f);
        material.SetFloat("_Blend", 0.0f);
        material.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
        material.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
        material.SetFloat("_ZWrite", 0.0f);
        material.SetFloat("_Cull", (float)CullMode.Off);
        material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        material.renderQueue = (int)RenderQueue.Transparent;
        EditorUtility.SetDirty(material);

        var root = new GameObject("QuestArrow");
        var arrow = root.AddComponent<QuestGuideArrow>();
        var visual = new GameObject("Visual");
        visual.transform.SetParent(root.transform, false);
        visual.transform.localScale = Vector3.one * 0.9f;
        visual.AddComponent<MeshFilter>().sharedMesh = mesh;
        var renderer = visual.AddComponent<MeshRenderer>();
        renderer.sharedMaterial = material;
        renderer.shadowCastingMode = ShadowCastingMode.Off;
        renderer.receiveShadows = false;
        arrow.Visual = visual.transform;
        visual.SetActive(false);
        savePrefab(root, "VFX/QuestArrow");
    }
}

/// <summary>대상 하이라이트 머티리얼 (Content/Materials/Highlight, AssetTable 142): Game/Highlight 셰이더. 색은 런타임 MaterialPropertyBlock.</summary>
public static partial class ContentBuilder
{
    [MenuItem("Game/Setup/3e. Build Highlight Material", priority = 3)]
    public static void BuildHighlightMaterialOnly()
    {
        buildHighlightMaterial();
        AssetDatabase.SaveAssets();
    }

    private static void buildHighlightMaterial()
    {
        var shader = Shader.Find("Game/Highlight");
        if (shader == null)
        {
            Debug.LogWarning("[Content] Game/Highlight shader missing");
            return;
        }

        var material = loadOrCreate(contentPath("Materials/Highlight", ".mat"), shader);
        material.enableInstancing = true;
        EditorUtility.SetDirty(material);
    }
}
