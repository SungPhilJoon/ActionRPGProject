using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace Framework.Editor
{
    /// <summary>
    /// Built-in(Standard / Legacy / 커스텀 Built-in) 재질을 URP Lit 사본으로 변환한다.
    /// 외부 에셋 원본은 건드리지 않고 InOutputFolder 에 사본을 만들어 캐시한다 (같은 원본은 한 번만 변환).
    /// 지원: _MainTex/_Color, _BumpMap, _MetallicGlossMap/_Metallic/_Glossiness, _OcclusionMap, _EmissionMap/_EmissionColor,
    ///       _Cutoff(알파 클립), 렌더 큐로 투명 판단, _Cull(양면)
    /// </summary>
    public static class MaterialUpgradeUtil
    {
        private static readonly Dictionary<Material, Material> Cache = new Dictionary<Material, Material>();

        public static void ClearCache()
        {
            Cache.Clear();
        }

        /// <summary>URP 에서 그대로 쓸 수 있는 셰이더인가.</summary>
        public static bool IsUrpCompatible(Material InMaterial)
        {
            if (InMaterial == null || InMaterial.shader == null)
            {
                return true;
            }

            string shader = InMaterial.shader.name;
            return shader.StartsWith("Universal Render Pipeline") == true
                || shader.StartsWith("Shader Graphs") == true
                || shader.StartsWith("WeatherMaker") == true
                || shader.StartsWith("Skybox") == true
                || shader.StartsWith("Hidden") == true;
        }

        /// <summary>InRoot 아래 모든 렌더러의 비호환 재질을 URP 사본으로 교체한다.</summary>
        public static int UpgradeRenderers(GameObject InRoot, string InOutputFolder, bool InForceDoubleSided = false)
        {
            int count = 0;
            foreach (var renderer in InRoot.GetComponentsInChildren<Renderer>(true))
            {
                if (renderer is ParticleSystemRenderer)
                {
                    continue;
                }

                var materials = renderer.sharedMaterials;
                bool changed = false;
                for (int i = 0; i < materials.Length; ++i)
                {
                    if (IsUrpCompatible(materials[i]) == true)
                    {
                        continue;
                    }

                    materials[i] = Upgrade(materials[i], InOutputFolder, InForceDoubleSided);
                    changed = true;
                    count += 1;
                }

                if (changed == true)
                {
                    renderer.sharedMaterials = materials;
                }
            }

            return count;
        }

        public static Material Upgrade(Material InSource, string InOutputFolder, bool InForceDoubleSided)
        {
            Material cached;
            if (Cache.TryGetValue(InSource, out cached) == true && cached != null)
            {
                return cached;
            }

            EditorAssetUtil.EnsureFolder(InOutputFolder);
            string path = $"{InOutputFolder}/{sanitize(InSource.name)}_URP.mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                material = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                AssetDatabase.CreateAsset(material, path);
            }

            copy(InSource, material, InForceDoubleSided);
            EditorUtility.SetDirty(material);
            Cache[InSource] = material;
            return material;
        }

        private static void copy(Material InSource, Material InTarget, bool InForceDoubleSided)
        {
            var mainTex = getTexture(InSource, "_MainTex", "_BaseMap", "_Albedo");
            if (mainTex != null)
            {
                InTarget.SetTexture("_BaseMap", mainTex);
                InTarget.SetTextureScale("_BaseMap", InSource.HasProperty("_MainTex") == true ? InSource.GetTextureScale("_MainTex") : Vector2.one);
            }

            InTarget.SetColor("_BaseColor", InSource.HasProperty("_Color") == true ? InSource.GetColor("_Color") : Color.white);

            var normal = getTexture(InSource, "_BumpMap", "_NormalMap");
            if (normal != null)
            {
                InTarget.SetTexture("_BumpMap", normal);
                InTarget.SetFloat("_BumpScale", InSource.HasProperty("_BumpScale") == true ? InSource.GetFloat("_BumpScale") : 1.0f);
                InTarget.EnableKeyword("_NORMALMAP");
            }

            var metallic = getTexture(InSource, "_MetallicGlossMap");
            if (metallic != null)
            {
                InTarget.SetTexture("_MetallicGlossMap", metallic);
                InTarget.EnableKeyword("_METALLICSPECGLOSSMAP");
            }

            InTarget.SetFloat("_Metallic", InSource.HasProperty("_Metallic") == true ? InSource.GetFloat("_Metallic") : 0.0f);
            float smoothness = 0.25f;
            if (InSource.HasProperty("_Glossiness") == true)
            {
                smoothness = InSource.GetFloat("_Glossiness");
            }

            InTarget.SetFloat("_Smoothness", smoothness);

            var occlusion = getTexture(InSource, "_OcclusionMap");
            if (occlusion != null)
            {
                InTarget.SetTexture("_OcclusionMap", occlusion);
                InTarget.EnableKeyword("_OCCLUSIONMAP");
            }

            var emission = getTexture(InSource, "_EmissionMap");
            bool hasEmissionColor = InSource.HasProperty("_EmissionColor") == true && InSource.GetColor("_EmissionColor").maxColorComponent > 0.01f;
            if (emission != null || (hasEmissionColor == true && InSource.IsKeywordEnabled("_EMISSION") == true))
            {
                InTarget.SetTexture("_EmissionMap", emission);
                InTarget.SetColor("_EmissionColor", InSource.HasProperty("_EmissionColor") == true ? InSource.GetColor("_EmissionColor") : Color.white);
                InTarget.EnableKeyword("_EMISSION");
            }

            bool transparent = InSource.renderQueue >= (int)RenderQueue.Transparent || InSource.shader.name.Contains("Transparent") == true;
            bool cutout = InSource.HasProperty("_Cutoff") == true && (InSource.IsKeywordEnabled("_ALPHATEST_ON") == true || InSource.renderQueue == (int)RenderQueue.AlphaTest);
            if (transparent == true)
            {
                InTarget.SetFloat("_Surface", 1.0f);
                InTarget.SetFloat("_Blend", 0.0f);
                InTarget.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
                InTarget.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
                InTarget.SetFloat("_ZWrite", 0.0f);
                InTarget.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
                InTarget.renderQueue = (int)RenderQueue.Transparent;
            }
            else if (cutout == true)
            {
                InTarget.SetFloat("_AlphaClip", 1.0f);
                InTarget.SetFloat("_Cutoff", InSource.GetFloat("_Cutoff"));
                InTarget.EnableKeyword("_ALPHATEST_ON");
                InTarget.renderQueue = (int)RenderQueue.AlphaTest;
            }

            bool doubleSided = InForceDoubleSided == true || InSource.shader.name.Contains("Double") == true || (InSource.HasProperty("_Cull") == true && InSource.GetFloat("_Cull") < 0.5f);
            InTarget.SetFloat("_Cull", doubleSided == true ? 0.0f : 2.0f);
        }

        private static Texture getTexture(Material InMaterial, params string[] InNames)
        {
            for (int i = 0; i < InNames.Length; ++i)
            {
                if (InMaterial.HasProperty(InNames[i]) == true)
                {
                    var texture = InMaterial.GetTexture(InNames[i]);
                    if (texture != null)
                    {
                        return texture;
                    }
                }
            }

            return null;
        }

        private static string sanitize(string InName)
        {
            foreach (char c in Path.GetInvalidFileNameChars())
            {
                InName = InName.Replace(c, '_');
            }

            return InName;
        }
    }
}
