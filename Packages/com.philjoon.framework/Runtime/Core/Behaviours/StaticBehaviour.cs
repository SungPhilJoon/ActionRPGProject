using System.Collections.Generic;
using UnityEngine;

namespace Framework
{
    /// <summary>
    /// 절대 움직이지 않는 물체 (지형 장식, 건물, 바위 …). URP 정적 배칭(Static Batching)으로 같은 머티리얼의 메시를 합쳐 드로우 콜을 줄인다.
    ///  - 에디터: 붙이거나 값을 바꾸면 자신(+ 자식)에 Batching Static 플래그를 켠다 → 씬에 놓인 물체는 빌드 · Play 진입 때 Unity 가 정적 배칭한다.
    ///  - 런타임에 생성된 물체: 정적 플래그는 효과가 없으므로 StaticBatching 에 등록 → 그 프레임 LateUpdate 에 StaticBatchingUtility.Combine 으로 한꺼번에 합친다.
    ///  - 합쳐진 메시는 transform 을 따라가지 않는다. 에디터 · 개발 빌드에서 움직임을 감지하면 경고한다 (움직이거나 사라질 물체는 DynamicBehaviour).
    ///  - 자식 중 다른 StaticBehaviour / DynamicBehaviour 아래 렌더러는 그쪽이 맡는다.
    /// 런타임 합치기는 메시가 Read/Write 가능해야 한다 (빌드에서 읽을 수 없는 메시는 경고 후 건너뜀).
    /// </summary>
    [DisallowMultipleComponent]
    public class StaticBehaviour : BaseBehaviour
    {
        [Tooltip("자식 오브젝트도 같은 정적 물체로 다룬다 (정적 플래그 · 런타임 배칭).")]
        public bool IncludeChildren = true;
        [Tooltip("Occluder / Occludee Static 도 켠다 (오클루전 컬링 베이크에 포함).")]
        public bool OcclusionStatic = true;
        [Tooltip("Contribute GI 도 켠다 (라이트맵 베이크 대상이 된다 — 켜면 다시 베이크 필요).")]
        public bool ContributeGI = false;

        private bool MoveWarned;

        /// <summary>렌더러가 하나라도 정적 배치에 들어갔는가 (씬 정적 배칭 또는 런타임 Combine).</summary>
        public bool IsBatched { get; private set; }

        protected override void OnUnityAwake()
        {
            base.OnUnityAwake();
            if (Application.isPlaying == true)
            {
                StaticBatching.Request(this);
            }
        }

        protected override void OnUnityDestroy()
        {
            StaticBatching.Unregister(this);
            base.OnUnityDestroy();
        }

        /// <summary>이 물체가 맡는 렌더러의 GameObject 중 아직 배치에 들어가지 않은 것을 InOutTargets 에 더한다. 이미 배칭된 것이 있으면 IsBatched.</summary>
        internal void CollectBatchTargets(List<GameObject> InOutTargets)
        {
            var renderers = IncludeChildren == true ? GetComponentsInChildren<MeshRenderer>(false) : GetComponents<MeshRenderer>();
            foreach (var renderer in renderers)
            {
                if (BehaviourScope.IsOwnedBy(renderer.transform, this) == false)
                {
                    continue;
                }

                if (renderer.isPartOfStaticBatch == true)
                {
                    IsBatched = true;
                    continue;
                }

                var filter = renderer.GetComponent<MeshFilter>();
                var mesh = filter != null ? filter.sharedMesh : null;
                if (mesh == null)
                {
                    continue;
                }

#if UNITY_EDITOR == false
                if (mesh.isReadable == false)
                {
                    Log.Warning($"StaticBehaviour {name}: '{mesh.name}' 메시가 Read/Write 불가라 런타임 정적 배칭에서 제외됩니다 (모델 Import Settings 에서 Read/Write 켜기).");
                    continue;
                }
#endif
                InOutTargets.Add(renderer.gameObject);
            }
        }

        /// <summary>배칭 직후 호출 — 이동 감지 기준점을 지금 위치로.</summary>
        internal void MarkBatched()
        {
            IsBatched = true;
            transform.hasChanged = false;
        }

        /// <summary>에디터 · 개발 빌드 검증: 배칭 뒤 움직였으면 한 번 경고한다.</summary>
        internal void CheckMoved()
        {
            if (MoveWarned == true || IsBatched == false || transform.hasChanged == false)
            {
                return;
            }

            MoveWarned = true;
            Log.Warning($"StaticBehaviour {name} 가 배칭 뒤 움직였습니다. 합쳐진 메시는 따라가지 않습니다 — 움직이거나 사라질 물체는 DynamicBehaviour 를 쓰세요.", this);
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
