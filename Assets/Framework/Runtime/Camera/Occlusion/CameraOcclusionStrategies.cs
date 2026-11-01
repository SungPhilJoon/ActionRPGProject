using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace Framework
{
    /// <summary>감지에 필요한 입력. 감지 전략은 이 값만 보고 콜라이더를 모은다.</summary>
    public struct CameraOcclusionQuery
    {
        public Vector3 CameraPosition;
        /// <summary>대상 발밑 위치.</summary>
        public Vector3 TargetPosition;
        public CameraOcclusionSettings Settings;
    }

    /// <summary>감지 결과 하나: 어느 광선(Ray)이 어떤 콜라이더에 막혔는지. Ray &lt; 0 이면 카메라와 겹침 (= 완전히 가림).</summary>
    public struct CameraOcclusionHit
    {
        public Collider Collider;
        public int Ray;
    }

    /// <summary>
    /// 감지 전략 (Strategy): 이번 프레임 대상을 가리는 콜라이더를 광선 번호와 함께 OutHits 에 채운다.
    /// 시스템은 대상마다 "막힌 광선 수 / RayCount" 를 가림 정도(0~1)로 쓴다.
    /// 교체 예: 여러 대상(파티원) 모두를 향한 캐스트, 화면 사각형 기반 판정, 렌더러 바운드 판정.
    /// </summary>
    public interface ICameraOcclusionDetector
    {
        /// <summary>광선 수 (32 이하). 가림 정도의 분모.</summary>
        int RayCount { get; }

        void Detect(in CameraOcclusionQuery InQuery, List<CameraOcclusionHit> OutHits);
    }

    /// <summary>
    /// 응답 전략 (Strategy): 가림 정도(Coverage 0~1)에 따라 대상 렌더러를 처리한다.
    ///  - OnOccluded: 처음 가리기 시작 (상태 준비)
    ///  - Tick: 처리 중인 대상마다 매 프레임. 더 이상 가리지 않으면 InCoverage = 0 으로 계속 불린다 (페이드 인).
    ///    true 를 돌려주는 동안 계속 처리하고, InCoverage 가 0 인데 false 를 돌려주면 OnRevealed 후 목록에서 뺀다.
    ///  - OnRevealed: 원래 상태로 완전히 복구
    /// 교체 예: 메시 숨김(즉시), 알파 페이드, 디더, 실루엣.
    /// </summary>
    public interface ICameraOcclusionResponse
    {
        void OnOccluded(CameraOccluder InOccluder);
        bool Tick(CameraOccluder InOccluder, float InCoverage, float InDeltaTime);
        void OnRevealed(CameraOccluder InOccluder);
    }

    /// <summary>
    /// 기본 감지: 카메라 → 대상 몸의 여러 점(높이 Heights × 좌우 Offsets)으로 광선을 쏘아, 대상을 가리는 콜라이더를 광선별로 모은다.
    /// 많이 가릴수록 많은 광선이 막힌다 → 가림 정도. 대상 바로 앞 TargetPadding 구간은 검사하지 않아 대상 뒤/옆 물체는 무시한다.
    /// 카메라 위치 OverlapSphere(CameraRadius) 에 걸린 것은 카메라가 그 안에 들어간 것이라 완전히 가림(Ray = -1).
    /// </summary>
    public class CoverageOcclusionDetector : ICameraOcclusionDetector
    {
        /// <summary>대상 발밑 기준 높이 (m) — 발 · 허리 · 가슴 · 머리.</summary>
        public float[] Heights = { 0.3f, 0.9f, 1.5f, 1.9f };
        /// <summary>카메라 오른쪽 방향 좌우 오프셋 (m) — 몸 폭.</summary>
        public float[] Offsets = { -0.35f, 0.0f, 0.35f };

        private RaycastHit[] Hits = new RaycastHit[32];
        private Collider[] Overlaps = new Collider[32];

        public int RayCount
        {
            get { return Mathf.Min(32, Heights.Length * Offsets.Length); }
        }

        public void Detect(in CameraOcclusionQuery InQuery, List<CameraOcclusionHit> OutHits)
        {
            var settings = InQuery.Settings;
            ensureCapacity(settings.MaxHits);

            Vector3 forward = InQuery.TargetPosition - InQuery.CameraPosition;
            Vector3 right = Vector3.Cross(Vector3.up, forward).normalized;
            int ray = 0;
            for (int h = 0; h < Heights.Length; ++h)
            {
                for (int o = 0; o < Offsets.Length && ray < 32; ++o)
                {
                    Vector3 point = InQuery.TargetPosition + Vector3.up * Heights[h] + right * Offsets[o];
                    Vector3 toPoint = point - InQuery.CameraPosition;
                    float distance = toPoint.magnitude - settings.TargetPadding;
                    if (distance > 0.01f)
                    {
                        int count = Physics.RaycastNonAlloc(InQuery.CameraPosition, toPoint.normalized, Hits, distance, settings.Mask, settings.Triggers);
                        for (int i = 0; i < count; ++i)
                        {
                            OutHits.Add(new CameraOcclusionHit { Collider = Hits[i].collider, Ray = ray });
                        }
                    }

                    ray += 1;
                }
            }

            if (settings.CameraRadius > 0.0f)
            {
                int count = Physics.OverlapSphereNonAlloc(InQuery.CameraPosition, settings.CameraRadius, Overlaps, settings.Mask, settings.Triggers);
                for (int i = 0; i < count; ++i)
                {
                    OutHits.Add(new CameraOcclusionHit { Collider = Overlaps[i], Ray = -1 });
                }
            }
        }

        private void ensureCapacity(int InCapacity)
        {
            int capacity = Mathf.Max(8, InCapacity);
            if (Hits.Length != capacity)
            {
                Hits = new RaycastHit[capacity];
                Overlaps = new Collider[capacity];
            }
        }
    }

    /// <summary>
    /// 예전 감지: 카메라 → 대상 SphereCast 1회 (가림 정도는 0 아니면 1). CoverageOcclusionDetector 대신 SetDetector 로 쓸 수 있다.
    /// </summary>
    public class SphereCastOcclusionDetector : ICameraOcclusionDetector
    {
        private RaycastHit[] Hits = new RaycastHit[32];

        public int RayCount
        {
            get { return 1; }
        }

        public void Detect(in CameraOcclusionQuery InQuery, List<CameraOcclusionHit> OutHits)
        {
            var settings = InQuery.Settings;
            Vector3 target = InQuery.TargetPosition + Vector3.up * settings.TargetHeight;
            Vector3 toTarget = target - InQuery.CameraPosition;
            float distance = toTarget.magnitude - settings.TargetPadding;
            if (distance <= 0.01f)
            {
                return;
            }

            int count = Physics.SphereCastNonAlloc(InQuery.CameraPosition, settings.CastRadius, toTarget.normalized, Hits, distance, settings.Mask, settings.Triggers);
            for (int i = 0; i < count; ++i)
            {
                OutHits.Add(new CameraOcclusionHit { Collider = Hits[i].collider, Ray = 0 });
            }
        }
    }

    /// <summary>
    /// 메시 숨김(즉시): 가리는 동안 메시를 카메라에서 숨긴다. 그림자는 남긴다 — 땅의 그늘(햇빛 판정)과 화면이 어긋나지 않는다.
    ///  - 원래 그림자를 드리우던 렌더러 → ShadowsOnly, 그림자가 없던 렌더러 → forceRenderingOff
    /// </summary>
    public class HideMeshOcclusionResponse : ICameraOcclusionResponse
    {
        public void OnOccluded(CameraOccluder InOccluder)
        {
            for (int i = 0; i < InOccluder.RendererCount; ++i)
            {
                var renderer = InOccluder.GetRenderer(i);
                if (renderer == null)
                {
                    continue;
                }

                if (InOccluder.GetOriginalShadowMode(i) == ShadowCastingMode.Off)
                {
                    renderer.forceRenderingOff = true;
                }
                else
                {
                    renderer.shadowCastingMode = ShadowCastingMode.ShadowsOnly;
                }
            }
        }

        public bool Tick(CameraOccluder InOccluder, float InCoverage, float InDeltaTime)
        {
            return InCoverage > 0.0f;
        }

        public void OnRevealed(CameraOccluder InOccluder)
        {
            for (int i = 0; i < InOccluder.RendererCount; ++i)
            {
                var renderer = InOccluder.GetRenderer(i);
                if (renderer == null)
                {
                    continue;
                }

                renderer.forceRenderingOff = false;
                renderer.shadowCastingMode = InOccluder.GetOriginalShadowMode(i);
            }
        }
    }

    /// <summary>
    /// 알파 페이드 (기본): 가림 정도만큼 투명해지고(Coverage 1 → MinAlpha), 가리지 않게 되면 다시 불투명해진다. 알파는 FadeSpeed 로 부드럽게 따라간다.
    /// 머티리얼마다 두 방식 중 하나 (셰이더를 보고 고른다, Framework 는 게임 셰이더 이름을 모른다):
    ///  - FadeProperty("_OcclusionFade") 가 있는 셰이더 (예: 게임 나무 셰이더) → MaterialPropertyBlock 으로 그 값만 넘긴다. 셰이더가 알아서 반투명.
    ///  - URP Lit 계열(_Surface · _BaseColor) → 반투명으로 바꾼 사본 머티리얼(원본마다 한 번 만들어 공유)로 바꾸고 _BaseColor 알파를 렌더러별로 넘긴다.
    ///  - 둘 다 아니면 알파 0.5 미만일 때만 메시 숨김.
    /// 완전히 불투명해지면 원래 머티리얼 · 프로퍼티로 되돌린다. 그림자는 그대로 둔다.
    /// </summary>
    public class FadeOcclusionResponse : ICameraOcclusionResponse
    {
        public float MinAlpha = 0.25f;
        /// <summary>초당 알파 변화량.</summary>
        public float FadeSpeed = 3.0f;
        public string FadeProperty = "_OcclusionFade";

        // 셰이더 프로퍼티 ID 는 처음 쓸 때 얻는다: 이 응답은 CameraOcclusionSystem 의 필드 초기화(= MonoBehaviour 생성자)에서 만들어지는데,
        // 거기서는 Shader.PropertyToID · new MaterialPropertyBlock 이 허용되지 않아 응답이 null 이 됐다.
        private int BaseColorId;
        private int SurfaceId;
        private int FadeId;
        private bool Ready;
        private readonly Dictionary<Material, Material> FadeCopies = new Dictionary<Material, Material>();
        private readonly Dictionary<CameraOccluder, float> Alpha = new Dictionary<CameraOccluder, float>();
        private MaterialPropertyBlock Block;

        private void ensureReady()
        {
            if (Ready == true)
            {
                return;
            }

            Ready = true;
            BaseColorId = Shader.PropertyToID("_BaseColor");
            SurfaceId = Shader.PropertyToID("_Surface");
            FadeId = Shader.PropertyToID(FadeProperty);
            Block = new MaterialPropertyBlock();
        }

        /// <summary>대상의 현재 알파 (처리 중이 아니면 1). 테스트 · 디버그용.</summary>
        public float GetAlpha(CameraOccluder InOccluder)
        {
            float alpha;
            return Alpha.TryGetValue(InOccluder, out alpha) == true ? alpha : 1.0f;
        }

        public void OnOccluded(CameraOccluder InOccluder)
        {
            if (Alpha.ContainsKey(InOccluder) == true)
            {
                return;
            }

            ensureReady();
            Alpha.Add(InOccluder, 1.0f);
            for (int i = 0; i < InOccluder.RendererCount; ++i)
            {
                var renderer = InOccluder.GetRenderer(i);
                if (renderer == null)
                {
                    continue;
                }

                var originals = InOccluder.GetOriginalMaterials(i);
                var swapped = new Material[originals.Length];
                for (int m = 0; m < originals.Length; ++m)
                {
                    swapped[m] = getFadeMaterial(originals[m]);
                }

                renderer.sharedMaterials = swapped;
            }
        }

        public bool Tick(CameraOccluder InOccluder, float InCoverage, float InDeltaTime)
        {
            ensureReady();
            float target = Mathf.Lerp(1.0f, MinAlpha, Mathf.Clamp01(InCoverage));
            float current;
            Alpha.TryGetValue(InOccluder, out current);
            current = Mathf.MoveTowards(current, target, FadeSpeed * InDeltaTime);
            Alpha[InOccluder] = current;
            apply(InOccluder, current);
            return current < 0.999f || InCoverage > 0.0f;
        }

        public void OnRevealed(CameraOccluder InOccluder)
        {
            Alpha.Remove(InOccluder);
            for (int i = 0; i < InOccluder.RendererCount; ++i)
            {
                var renderer = InOccluder.GetRenderer(i);
                if (renderer == null)
                {
                    continue;
                }

                renderer.sharedMaterials = InOccluder.GetOriginalMaterials(i);
                renderer.SetPropertyBlock(null);
                for (int m = 0; m < renderer.sharedMaterials.Length; ++m)
                {
                    renderer.SetPropertyBlock(null, m);
                }

                renderer.forceRenderingOff = false;
            }
        }

        private void apply(CameraOccluder InOccluder, float InAlpha)
        {
            for (int i = 0; i < InOccluder.RendererCount; ++i)
            {
                var renderer = InOccluder.GetRenderer(i);
                if (renderer == null)
                {
                    continue;
                }

                var originals = InOccluder.GetOriginalMaterials(i);
                bool hide = false;
                for (int m = 0; m < originals.Length; ++m)
                {
                    var original = originals[m];
                    if (original == null)
                    {
                        continue;
                    }

                    Block.Clear();
                    if (original.HasProperty(FadeId) == true)
                    {
                        Block.SetFloat(FadeId, InAlpha);
                    }
                    else if (isUrpLit(original) == true)
                    {
                        var color = original.GetColor(BaseColorId);
                        color.a *= InAlpha;
                        Block.SetColor(BaseColorId, color);
                    }
                    else
                    {
                        hide = InAlpha < 0.5f;
                        continue;
                    }

                    renderer.SetPropertyBlock(Block, m);
                }

                renderer.forceRenderingOff = hide;
            }
        }

        private bool isUrpLit(Material InMaterial)
        {
            return InMaterial.HasProperty(SurfaceId) == true && InMaterial.HasProperty(BaseColorId) == true;
        }

        /// <summary>URP Lit 원본 → 반투명 사본 (원본마다 하나, 모든 대상이 공유). 나무 셰이더처럼 FadeProperty 가 있으면 원본 그대로.</summary>
        private Material getFadeMaterial(Material InOriginal)
        {
            if (InOriginal == null || InOriginal.HasProperty(FadeId) == true || isUrpLit(InOriginal) == false)
            {
                return InOriginal;
            }

            Material copy;
            if (FadeCopies.TryGetValue(InOriginal, out copy) == true && copy != null)
            {
                return copy;
            }

            copy = new Material(InOriginal);
            copy.name = InOriginal.name + " (Fade)";
            copy.SetFloat(SurfaceId, 1.0f);
            copy.SetFloat("_Blend", 0.0f);
            copy.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
            copy.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
            copy.SetFloat("_SrcBlendAlpha", (float)BlendMode.One);
            copy.SetFloat("_DstBlendAlpha", (float)BlendMode.OneMinusSrcAlpha);
            // 깊이는 쓰지 않는다: 쓰면 깊이를 읽는 풀스크린 대기 안개가 반투명 건물을 가까운 벽으로 보고 화면을 하늘색으로 덮었다.
            copy.SetFloat("_ZWrite", 0.0f);
            copy.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            copy.DisableKeyword("_ALPHAPREMULTIPLY_ON");
            copy.renderQueue = (int)RenderQueue.Transparent;
            copy.SetOverrideTag("RenderType", "Transparent");
            copy.SetShaderPassEnabled("DepthOnly", false);
            FadeCopies[InOriginal] = copy;
            return copy;
        }
    }
}
