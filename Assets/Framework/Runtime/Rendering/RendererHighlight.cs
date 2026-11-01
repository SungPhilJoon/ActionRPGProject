using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace Framework
{
    /// <summary>
    /// 대상 하이라이트: 대상의 메시를 하이라이트 머티리얼로 한 번 더 그리는 겹 렌더러를 만들어 켜고 끈다.
    ///  - 원본 머티리얼을 건드리지 않는다 (페이드 · 피격 플래시 등 다른 효과와 충돌하지 않음).
    ///  - MeshRenderer → 같은 메시의 MeshRenderer 자식, SkinnedMeshRenderer → 같은 본을 쓰는 SkinnedMeshRenderer (서브메시 전부 덮음).
    ///  - LODGroup 이 있으면 LOD0 렌더러만 (여러 LOD 가 겹쳐 그려지지 않게).
    ///  - 원본이 알파 컷아웃(AlphaTest 큐)이면 원본 _BaseMap · _Cutoff 를 넘겨 잎 사각형이 보이지 않게 하고 외곽선은 끈다.
    ///  - 원본 렌더러가 꺼지면(채집 노드 고갈 등) 겹 렌더러도 꺼진다.
    /// 겹 렌더러는 처음 Show 할 때 한 번 만든다. 색은 MaterialPropertyBlock(ColorProperty).
    /// 머티리얼(셰이더)은 게임이 넘긴다 — Framework 는 셰이더를 모른다.
    /// </summary>
    [DisallowMultipleComponent]
    public class RendererHighlight : BaseBehaviour
    {
        public string ColorProperty = "_HighlightColor";
        public string OutlineProperty = "_OutlineWidth";

        // 컴포넌트 생성 중(AddComponent)에는 Shader.PropertyToID 를 부를 수 없어 build 에서 얻는다.
        private int BaseMapId;
        private int CutoffId;

        private readonly List<Renderer> Sources = new List<Renderer>();
        private readonly List<Renderer> Overlays = new List<Renderer>();
        private MaterialPropertyBlock Block;
        private Material BuiltMaterial;
        private int ColorId;
        private int OutlineId;

        public bool IsShown { get; private set; }

        public Color CurrentColor { get; private set; }

        /// <summary>InMaterial 로 InColor 하이라이트를 켠다 (같은 색이면 아무 일도 없음).</summary>
        public void Show(Material InMaterial, Color InColor)
        {
            if (InMaterial == null)
            {
                return;
            }

            if (BuiltMaterial != InMaterial)
            {
                build(InMaterial);
            }

            if (IsShown == true && CurrentColor == InColor)
            {
                return;
            }

            IsShown = true;
            CurrentColor = InColor;
            applyColor();
            syncEnabled();
        }

        public void Hide()
        {
            if (IsShown == false)
            {
                return;
            }

            IsShown = false;
            syncEnabled();
        }

        private void LateUpdate()
        {
            if (IsShown == true)
            {
                syncEnabled();
            }
        }

        protected override void OnUnityDestroy()
        {
            clearOverlays();
            base.OnUnityDestroy();
        }

        private void syncEnabled()
        {
            for (int i = 0; i < Overlays.Count; ++i)
            {
                var source = Sources[i];
                bool visible = IsShown == true && source != null && source.enabled == true && source.gameObject.activeInHierarchy == true;
                if (Overlays[i] != null && Overlays[i].enabled != visible)
                {
                    Overlays[i].enabled = visible;
                }
            }
        }

        private void applyColor()
        {
            for (int i = 0; i < Overlays.Count; ++i)
            {
                var overlay = Overlays[i];
                if (overlay == null)
                {
                    continue;
                }

                var originals = Sources[i] != null ? Sources[i].sharedMaterials : new Material[0];
                int count = overlay.sharedMaterials.Length;
                for (int m = 0; m < count; ++m)
                {
                    var original = m < originals.Length ? originals[m] : null;
                    Block.Clear();
                    Block.SetColor(ColorId, CurrentColor);
                    if (isCutout(original) == true)
                    {
                        Block.SetTexture(BaseMapId, original.GetTexture(BaseMapId));
                        Block.SetFloat(CutoffId, original.GetFloat(CutoffId));
                        Block.SetFloat(OutlineId, 0.0f);
                    }

                    overlay.SetPropertyBlock(Block, m);
                }
            }
        }

        private bool isCutout(Material InMaterial)
        {
            if (InMaterial == null || InMaterial.HasProperty(BaseMapId) == false || InMaterial.HasProperty(CutoffId) == false)
            {
                return false;
            }

            int queue = InMaterial.renderQueue;
            return queue >= (int)RenderQueue.AlphaTest && queue < (int)RenderQueue.Transparent;
        }

        private void build(Material InMaterial)
        {
            clearOverlays();
            BuiltMaterial = InMaterial;
            Block = new MaterialPropertyBlock();
            BaseMapId = Shader.PropertyToID("_BaseMap");
            CutoffId = Shader.PropertyToID("_Cutoff");
            ColorId = Shader.PropertyToID(ColorProperty);
            OutlineId = Shader.PropertyToID(OutlineProperty);

            var lod = GetComponentInChildren<LODGroup>();
            var lod0 = new HashSet<Renderer>();
            if (lod != null && lod.GetLODs().Length > 0)
            {
                foreach (var renderer in lod.GetLODs()[0].renderers)
                {
                    lod0.Add(renderer);
                }
            }

            foreach (var source in GetComponentsInChildren<Renderer>(true))
            {
                if (lod0.Count > 0 && lod0.Contains(source) == false)
                {
                    continue;
                }

                var overlay = createOverlay(source, InMaterial);
                if (overlay != null)
                {
                    Sources.Add(source);
                    Overlays.Add(overlay);
                }
            }
        }

        private static Renderer createOverlay(Renderer InSource, Material InMaterial)
        {
            var skinned = InSource as SkinnedMeshRenderer;
            var meshRenderer = InSource as MeshRenderer;
            Mesh mesh = null;
            if (skinned != null)
            {
                mesh = skinned.sharedMesh;
            }
            else if (meshRenderer != null)
            {
                var filter = meshRenderer.GetComponent<MeshFilter>();
                mesh = filter != null ? filter.sharedMesh : null;
            }

            if (mesh == null)
            {
                return null;
            }

            var materials = new Material[Mathf.Max(1, mesh.subMeshCount)];
            for (int i = 0; i < materials.Length; ++i)
            {
                materials[i] = InMaterial;
            }

            var go = new GameObject("Highlight");
            go.layer = InSource.gameObject.layer;
            Renderer overlay;
            if (skinned != null)
            {
                // 스킨 메시는 본 위치로 그려지므로 원본과 같은 부모 · 같은 로컬 변환에 둔다.
                go.transform.SetParent(InSource.transform.parent, false);
                go.transform.localPosition = InSource.transform.localPosition;
                go.transform.localRotation = InSource.transform.localRotation;
                go.transform.localScale = InSource.transform.localScale;
                var copy = go.AddComponent<SkinnedMeshRenderer>();
                copy.sharedMesh = mesh;
                copy.bones = skinned.bones;
                copy.rootBone = skinned.rootBone;
                copy.localBounds = skinned.localBounds;
                copy.quality = skinned.quality;
                overlay = copy;
            }
            else
            {
                go.transform.SetParent(InSource.transform, false);
                go.AddComponent<MeshFilter>().sharedMesh = mesh;
                overlay = go.AddComponent<MeshRenderer>();
            }

            overlay.sharedMaterials = materials;
            overlay.shadowCastingMode = ShadowCastingMode.Off;
            overlay.receiveShadows = false;
            overlay.lightProbeUsage = LightProbeUsage.Off;
            overlay.enabled = false;
            return overlay;
        }

        private void clearOverlays()
        {
            for (int i = 0; i < Overlays.Count; ++i)
            {
                if (Overlays[i] != null)
                {
                    Destroy(Overlays[i].gameObject);
                }
            }

            Overlays.Clear();
            Sources.Clear();
            BuiltMaterial = null;
            IsShown = false;
        }
    }
}
