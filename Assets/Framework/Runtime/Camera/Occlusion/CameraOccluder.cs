using UnityEngine;
using UnityEngine.Rendering;

namespace Framework
{
    /// <summary>
    /// 카메라 가림 처리 대상 표시. 이 컴포넌트가 붙은 오브젝트(와 자식)의 콜라이더가 카메라 시야를 막으면
    /// CameraOcclusionSystem 이 응답 전략(ICameraOcclusionResponse)으로 자식 렌더러를 처리한다.
    ///  - 렌더러와 원래 그림자 모드는 처음 사용할 때 한 번 모아 둔다 (응답 전략이 복구에 쓴다).
    ///  - 붙이지 않은 오브젝트는 가려도 처리하지 않는다 (지형, 캐릭터 등은 대상이 아님 — 명시적 opt-in).
    /// 문서: Documentation/VRising/CameraOcclusion.md
    /// </summary>
    [DisallowMultipleComponent]
    public class CameraOccluder : BaseBehaviour
    {
        private Renderer[] Renderers;
        private ShadowCastingMode[] OriginalShadowModes;

        /// <summary>현재 가림 처리 중인지 (CameraOcclusionSystem 이 설정).</summary>
        public bool IsOccluded { get; private set; }

        public int RendererCount
        {
            get
            {
                ensureCache();
                return Renderers.Length;
            }
        }

        public Renderer GetRenderer(int InIndex)
        {
            ensureCache();
            return Renderers[InIndex];
        }

        public ShadowCastingMode GetOriginalShadowMode(int InIndex)
        {
            ensureCache();
            return OriginalShadowModes[InIndex];
        }

        /// <summary>자식 구성이 바뀌었을 때(런타임에 렌더러 추가 등) 다시 모은다. 가림 처리 중이 아닐 때만 호출한다.</summary>
        public void RefreshRenderers()
        {
            Renderers = null;
            ensureCache();
        }

        internal void SetOccludedState(bool InOccluded)
        {
            IsOccluded = InOccluded;
        }

        private void ensureCache()
        {
            if (Renderers != null)
            {
                return;
            }

            Renderers = GetComponentsInChildren<Renderer>(true);
            OriginalShadowModes = new ShadowCastingMode[Renderers.Length];
            for (int i = 0; i < Renderers.Length; ++i)
            {
                OriginalShadowModes[i] = Renderers[i].shadowCastingMode;
            }
        }
    }
}
