using Framework.Editor;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// 캐릭터 · 몬스터 머티리얼을 Game/Character 셰이더 사본으로 바꾼다 (URP.md 11절).
///  - URP Lit 의 맵 · 값(Base, Normal, MetallicGloss, Occlusion, Emission, 알파 클립, Cull)을 그대로 옮긴다
///  - 플레이어(unity-chan) = 툰 모드 (원래 툰 셰이더 캐릭터라 Lit 변환 후 밋밋했던 것을 되살림)
///  - 몬스터 = PBR + 림라이트 + 얇은 외곽선
///  - 눈 · 속눈썹 · 이빨 · 볼터치 · 반사 · 반투명 재질은 외곽선을 끈다 (반투명은 변환하지 않음)
/// ContentBuilder 가 캐릭터 프리팹을 저장하기 전에 부르고, 메뉴로 이미 만든 프리팹에도 적용할 수 있다.
/// </summary>
public static class CharacterLook
{
    private const string Folder = EditorPaths.Generated + "/Materials/Characters";
    private const string LitShader = "Universal Render Pipeline/Lit";
    private static readonly string[] NoOutlineWords = { "eye", "lash", "teeth", "tooth", "cheek", "reflect", "brow", "mouth", "tongue" };

    [MenuItem("Game/Art/Apply Character Look (캐릭터 셰이더 적용)", priority = 70)]
    public static void ApplyToExistingPrefabs()
    {
        int count = 0;
        foreach (var guid in AssetDatabase.FindAssets("t:Prefab", new[] { EditorPaths.Content + "/Characters" }))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            var root = PrefabUtility.LoadPrefabContents(path);
            bool isActor = root.GetComponent<Actor>() != null;
            if (isActor == true)
            {
                count += Apply(root, root.GetComponent<PlayerCharacter>() != null);
                PrefabUtility.SaveAsPrefabAsset(root, path);
            }

            PrefabUtility.UnloadPrefabContents(root);
        }

        AssetDatabase.SaveAssets();
        Debug.Log($"[CharacterLook] converted {count} material slots");
    }

    /// <summary>InRoot 아래 렌더러의 Lit 머티리얼을 Game/Character 사본으로 바꾼다. 바꾼 슬롯 수를 돌려준다.</summary>
    public static int Apply(GameObject InRoot, bool InToon)
    {
        EditorAssetUtil.EnsureFolder(Folder);
        int count = 0;
        foreach (var renderer in InRoot.GetComponentsInChildren<Renderer>(true))
        {
            if (renderer is ParticleSystemRenderer == true)
            {
                continue;
            }

            var materials = renderer.sharedMaterials;
            bool changed = false;
            for (int i = 0; i < materials.Length; ++i)
            {
                var converted = convert(materials[i], InToon);
                if (converted != materials[i])
                {
                    materials[i] = converted;
                    changed = true;
                    count += 1;
                }
            }

            if (changed == true)
            {
                renderer.sharedMaterials = materials;
            }
        }

        return count;
    }

    private static Material convert(Material InSource, bool InToon)
    {
        if (InSource == null || InSource.shader == null)
        {
            return InSource;
        }

        if (InSource.shader.name == "Game/Character")
        {
            return InSource;
        }

        if (InSource.shader.name != LitShader || (InSource.HasProperty("_Surface") == true && InSource.GetFloat("_Surface") > 0.5f))
        {
            return InSource;
        }

        string path = $"{Folder}/{InSource.name}_Char.mat";
        var shader = Shader.Find("Game/Character");
        var material = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (material == null)
        {
            material = new Material(shader);
            AssetDatabase.CreateAsset(material, path);
        }

        material.shader = shader;
        copyTexture(InSource, material, "_BaseMap");
        material.SetColor("_BaseColor", InSource.GetColor("_BaseColor"));
        material.SetTextureScale("_BaseMap", InSource.GetTextureScale("_BaseMap"));
        material.SetTextureOffset("_BaseMap", InSource.GetTextureOffset("_BaseMap"));
        copyTexture(InSource, material, "_BumpMap");
        material.SetFloat("_BumpScale", InSource.GetFloat("_BumpScale"));

        bool hasMetallicMap = copyTexture(InSource, material, "_MetallicGlossMap");
        material.SetFloat("_Metallic", hasMetallicMap == true ? 1.0f : InSource.GetFloat("_Metallic"));
        material.SetFloat("_Smoothness", InSource.GetFloat("_Smoothness"));
        copyTexture(InSource, material, "_OcclusionMap");
        material.SetFloat("_OcclusionStrength", InSource.GetFloat("_OcclusionStrength"));
        if (InSource.IsKeywordEnabled("_EMISSION") == true)
        {
            copyTexture(InSource, material, "_EmissionMap");
            material.SetColor("_EmissionColor", InSource.GetColor("_EmissionColor"));
        }
        else
        {
            material.SetTexture("_EmissionMap", null);
            material.SetColor("_EmissionColor", Color.black);
        }

        bool clip = InSource.HasProperty("_AlphaClip") == true && InSource.GetFloat("_AlphaClip") > 0.5f;
        material.SetFloat("_Cutoff", clip == true ? Mathf.Max(0.01f, InSource.GetFloat("_Cutoff")) : 0.0f);
        material.SetFloat("_Cull", InSource.HasProperty("_Cull") == true ? InSource.GetFloat("_Cull") : (float)CullMode.Back);
        material.renderQueue = clip == true ? (int)RenderQueue.AlphaTest : (int)RenderQueue.Geometry;

        bool outline = clip == false && isDetailPart(InSource.name) == false;
        if (InToon == true)
        {
            material.SetFloat("_Toon", 1.0f);
            material.SetFloat("_ToonThreshold", 0.05f);
            material.SetFloat("_ToonSoftness", 0.05f);
            material.SetColor("_ToonShadowColor", new Color(0.72f, 0.6f, 0.68f));
            material.SetColor("_RimColor", new Color(1.0f, 0.86f, 0.8f));
            material.SetFloat("_RimPower", 4.0f);
            material.SetFloat("_RimStrength", 0.35f);
            material.SetFloat("_OutlineWidth", outline == true ? 0.0035f : 0.0f);
        }
        else
        {
            material.SetFloat("_Toon", 0.0f);
            material.SetFloat("_FillLight", 0.45f);
            material.SetColor("_RimColor", new Color(0.95f, 0.75f, 0.68f));
            material.SetFloat("_RimPower", 3.0f);
            material.SetFloat("_RimStrength", 0.8f);
            material.SetFloat("_OutlineWidth", outline == true ? 0.006f : 0.0f);
        }

        material.SetColor("_OutlineColor", new Color(0.06f, 0.03f, 0.04f));
        material.enableInstancing = true;
        EditorUtility.SetDirty(material);
        return material;
    }

    private static bool copyTexture(Material InSource, Material InTarget, string InProperty)
    {
        var texture = InSource.HasProperty(InProperty) == true ? InSource.GetTexture(InProperty) : null;
        InTarget.SetTexture(InProperty, texture);
        return texture != null;
    }

    private static bool isDetailPart(string InName)
    {
        string lower = InName.ToLowerInvariant();
        foreach (var word in NoOutlineWords)
        {
            if (lower.Contains(word) == true)
            {
                return true;
            }
        }

        return false;
    }
}
