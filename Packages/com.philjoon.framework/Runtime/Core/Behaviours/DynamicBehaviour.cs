using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace Framework
{
    /// <summary>
    /// 평소에는 제자리에 있지만 사라지거나 새로 생길 수 있는 물체 (채집 노드, 부서지는 소품, 건축으로 치워지는 나무 …).
    /// 정적 배칭 대신 URP SRP Batcher 로 그린다.
    ///  - 에디터: Batching Static · Occluder Static 플래그를 끈다 (합쳐진 메시에 묶이면 생성 · 제거가 불가능하고, 사라진 뒤에도 가림 데이터가 남는다).
    ///  - SRP Batcher 를 깨는 MaterialPropertyBlock 을 쓰지 않는다. 색 변경(SetTint)은 같은 셰이더 배리언트의 공유 머티리얼 변형(SharedMaterialVariants)으로
    ///    → 같은 색끼리는 머티리얼까지 공유, 다른 색이어도 셰이더가 같으면 같은 SRP 배치.
    ///  - Show / Hide 는 SetActive 대신 renderer.enabled (+ 선택적으로 collider.enabled) — 활성화 이벤트 전파 · 계층 갱신 없이 그리기만 끈다.
    ///  - IPoolable: GameObjectPool 에서 꺼낼 때 Show, 넣을 때 Hide.
    ///  - 에디터 · 개발 빌드: SRP Batcher 꺼짐, MaterialPropertyBlock, 정적 배치 포함을 감지하면 경고한다.
    /// 자식 중 다른 StaticBehaviour / DynamicBehaviour 아래 렌더러는 그쪽이 맡는다.
    /// </summary>
    [DisallowMultipleComponent]
    public class DynamicBehaviour : BaseBehaviour, IPoolable
    {
        [Tooltip("시작할 때 숨긴 상태로 둔다 (풀에서 꺼낼 때 Show).")]
        public bool StartHidden = false;
        [Tooltip("Show / Hide 때 콜라이더도 함께 켜고 끈다.")]
        public bool ToggleColliders = true;
        [Tooltip("SetTint 가 바꾸는 셰이더 색 속성 (URP Lit · Simple Lit · Unlit = _BaseColor).")]
        public string TintProperty = "_BaseColor";

        private static bool BatcherWarned;

        private readonly List<Renderer> Renderers = new List<Renderer>();
        private readonly List<Collider> Colliders = new List<Collider>();
        private readonly List<Material[]> BaseMaterials = new List<Material[]>();
        private int TintId;
        private bool Cached;

        public bool IsShown { get; private set; } = true;

        /// <summary>현재 틴트 (기본 머티리얼이면 null).</summary>
        public Color? Tint { get; private set; }

        public IReadOnlyList<Renderer> OwnedRenderers
        {
            get
            {
                ensureCached();
                return Renderers;
            }
        }

        protected override void OnUnityAwake()
        {
            base.OnUnityAwake();
            ensureCached();
            if (StartHidden == true)
            {
                SetShown(false);
            }

            validate();
        }

        // ------------------------------------------------------------------ show / hide

        public void Show()
        {
            SetShown(true);
        }

        public void Hide()
        {
            SetShown(false);
        }

        public virtual void SetShown(bool InShown)
        {
            ensureCached();
            if (IsShown == InShown)
            {
                return;
            }

            IsShown = InShown;
            for (int i = 0; i < Renderers.Count; ++i)
            {
                if (Renderers[i] != null)
                {
                    Renderers[i].enabled = InShown;
                }
            }

            if (ToggleColliders == true)
            {
                for (int i = 0; i < Colliders.Count; ++i)
                {
                    if (Colliders[i] != null)
                    {
                        Colliders[i].enabled = InShown;
                    }
                }
            }

            OnShownChanged(InShown);
        }

        /// <summary>Show / Hide 직후 (파생 클래스: 이펙트 · 사운드 · 네비게이션 갱신 등).</summary>
        protected virtual void OnShownChanged(bool InShown)
        {
        }

        public virtual void OnSpawn()
        {
            Show();
        }

        public virtual void OnDespawn()
        {
            Hide();
        }

        // ------------------------------------------------------------------ tint

        /// <summary>
        /// 모든 머티리얼의 TintProperty 를 InColor 로 바꾼 공유 변형으로 교체한다 (MaterialPropertyBlock 미사용 → SRP Batcher 유지).
        /// 같은 (원본 머티리얼, 색) 은 프로젝트 전체에서 한 머티리얼을 공유한다. 색은 8비트 채널로 양자화된다.
        /// </summary>
        public void SetTint(Color InColor)
        {
            ensureCached();
            Tint = InColor;
            for (int i = 0; i < Renderers.Count; ++i)
            {
                var renderer = Renderers[i];
                var baseMaterials = BaseMaterials[i];
                if (renderer == null || baseMaterials == null)
                {
                    continue;
                }

                var variants = new Material[baseMaterials.Length];
                for (int m = 0; m < baseMaterials.Length; ++m)
                {
                    variants[m] = SharedMaterialVariants.Get(baseMaterials[m], TintId, InColor);
                }

                renderer.sharedMaterials = variants;
            }
        }

        /// <summary>원래 공유 머티리얼로 되돌린다.</summary>
        public void ClearTint()
        {
            ensureCached();
            if (Tint.HasValue == false)
            {
                return;
            }

            Tint = null;
            for (int i = 0; i < Renderers.Count; ++i)
            {
                if (Renderers[i] != null && BaseMaterials[i] != null)
                {
                    Renderers[i].sharedMaterials = BaseMaterials[i];
                }
            }
        }

        // ------------------------------------------------------------------ cache / validate

        /// <summary>렌더러 · 콜라이더 구성을 바꾼 뒤(자식 추가 등) 다시 모은다.</summary>
        public void Recache()
        {
            Cached = false;
            ensureCached();
        }

        private void ensureCached()
        {
            if (Cached == true)
            {
                return;
            }

            Cached = true;
            TintId = Shader.PropertyToID(TintProperty);
            Renderers.Clear();
            Colliders.Clear();
            BaseMaterials.Clear();
            foreach (var renderer in GetComponentsInChildren<Renderer>(true))
            {
                if (BehaviourScope.IsOwnedBy(renderer.transform, this) == true)
                {
                    Renderers.Add(renderer);
                    BaseMaterials.Add(renderer.sharedMaterials);
                }
            }

            foreach (var collider in GetComponentsInChildren<Collider>(true))
            {
                if (BehaviourScope.IsOwnedBy(collider.transform, this) == true)
                {
                    Colliders.Add(collider);
                }
            }
        }

        [System.Diagnostics.Conditional("UNITY_EDITOR"), System.Diagnostics.Conditional("DEVELOPMENT_BUILD")]
        private void validate()
        {
            if (BatcherWarned == false && GraphicsSettings.useScriptableRenderPipelineBatching == false)
            {
                BatcherWarned = true;
                Log.Warning("DynamicBehaviour: SRP Batcher 가 꺼져 있습니다 (URP Asset → Rendering → SRP Batcher).");
            }

            for (int i = 0; i < Renderers.Count; ++i)
            {
                var renderer = Renderers[i];
                if (renderer.HasPropertyBlock() == true)
                {
                    Log.Warning($"DynamicBehaviour {name}: '{renderer.name}' 에 MaterialPropertyBlock 이 있어 SRP Batcher 에서 빠집니다 — SetTint(공유 머티리얼 변형)를 쓰세요.", renderer);
                }

                if (renderer.isPartOfStaticBatch == true)
                {
                    Log.Warning($"DynamicBehaviour {name}: '{renderer.name}' 가 정적 배치에 들어가 있습니다 — Batching Static 플래그를 끄세요 (컴포넌트를 다시 붙이면 자동으로 꺼짐).", renderer);
                }
            }
        }

#if UNITY_EDITOR
        protected virtual void Reset()
        {
            StaticEditorFlagsUtil.ApplyDeferred(this);
        }

        protected virtual void OnValidate()
        {
            StaticEditorFlagsUtil.ApplyDeferred(this);
        }
#endif
    }
}
