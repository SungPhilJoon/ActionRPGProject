using System.Collections.Generic;
using UnityEngine;

namespace Framework
{
    /// <summary>카메라 가림 설정. 게임(카메라 설정 코드)이 레이어 마스크 등을 채운다.</summary>
    [System.Serializable]
    public class CameraOcclusionSettings
    {
        [Tooltip("가림 판정 대상 레이어")]
        public LayerMask Mask = ~0;
        [Tooltip("카메라 → 대상 캐스트 반지름 (m)")]
        public float CastRadius = 0.5f;
        [Tooltip("카메라와 '충돌' 로 보는 반지름 (m). 0 이면 끔")]
        public float CameraRadius = 1.5f;
        [Tooltip("대상 발밑 기준 조준 높이 (m)")]
        public float TargetHeight = 1.2f;
        [Tooltip("대상 앞 이 거리(m)는 캐스트하지 않는다 (대상 뒤/옆 물체 제외)")]
        public float TargetPadding = 1.0f;
        [Tooltip("더 이상 가리지 않은 뒤 다시 보이기까지 (초). 경계에서 깜빡임 방지")]
        public float RevealDelay = 0.15f;
        [Tooltip("캐스트/오버랩 최대 결과 수")]
        public int MaxHits = 32;
        public QueryTriggerInteraction Triggers = QueryTriggerInteraction.Collide;
    }

    /// <summary>
    /// 카메라와 대상 사이를 가리거나 카메라와 겹친 CameraOccluder 를 찾아 응답 전략으로 처리한다 (기본: 메시 숨김).
    /// 가리지 않게 되면 RevealDelay 뒤 원래대로 되돌린다 — 충돌하지 않는 물체는 숨기지 않는다.
    ///  - 감지(ICameraOcclusionDetector)와 응답(ICameraOcclusionResponse)은 SetDetector / SetResponse 로 교체한다.
    ///  - 카메라 이동(LateUpdate) 뒤에 돌도록 실행 순서를 늦춘다.
    /// 문서: Documentation/VRising/CameraOcclusion.md
    /// </summary>
    [DefaultExecutionOrder(1000)]
    [DisallowMultipleComponent]
    public class CameraOcclusionSystem : BaseBehaviour
    {
        public CameraOcclusionSettings Settings = new CameraOcclusionSettings();

        private readonly List<Collider> Colliders = new List<Collider>();
        private readonly Dictionary<Collider, CameraOccluder> Resolved = new Dictionary<Collider, CameraOccluder>();
        private readonly Dictionary<CameraOccluder, float> LastHitTime = new Dictionary<CameraOccluder, float>();
        private readonly List<CameraOccluder> Active = new List<CameraOccluder>();
        private ICameraOcclusionDetector Detector = new SphereCastOcclusionDetector();
        private ICameraOcclusionResponse Response = new HideMeshOcclusionResponse();
        private Transform Target;

        public Transform CurrentTarget
        {
            get { return Target; }
        }

        /// <summary>현재 가림 처리 중인 대상 수 (디버그/테스트용).</summary>
        public int OccludedCount
        {
            get { return Active.Count; }
        }

        public CameraOccluder GetOccluded(int InIndex)
        {
            return Active[InIndex];
        }

        public void SetTarget(Transform InTarget)
        {
            Target = InTarget;
            if (Target == null)
            {
                RevealAll();
            }
        }

        public void SetDetector(ICameraOcclusionDetector InDetector)
        {
            Detector = InDetector != null ? InDetector : new SphereCastOcclusionDetector();
        }

        /// <summary>응답 전략 교체. 이전 전략으로 숨긴 대상은 먼저 그 전략으로 되돌린다.</summary>
        public void SetResponse(ICameraOcclusionResponse InResponse)
        {
            RevealAll();
            Response = InResponse != null ? InResponse : new HideMeshOcclusionResponse();
        }

        /// <summary>가림 처리 중인 대상을 모두 원래대로 되돌린다.</summary>
        public void RevealAll()
        {
            for (int i = Active.Count - 1; i >= 0; --i)
            {
                reveal(i);
            }

            LastHitTime.Clear();
        }

        private void OnDisable()
        {
            RevealAll();
        }

        protected override void OnUnityDestroy()
        {
            RevealAll();
            Resolved.Clear();
            base.OnUnityDestroy();
        }

        private void LateUpdate()
        {
            if (Target == null)
            {
                return;
            }

            float now = Time.time;
            Colliders.Clear();
            var query = new CameraOcclusionQuery
            {
                CameraPosition = transform.position,
                TargetPosition = Target.position + Vector3.up * Settings.TargetHeight,
                Settings = Settings,
            };
            Detector.Detect(query, Colliders);

            for (int i = 0; i < Colliders.Count; ++i)
            {
                var occluder = resolve(Colliders[i]);
                if (occluder == null)
                {
                    continue;
                }

                if (LastHitTime.ContainsKey(occluder) == false)
                {
                    Active.Add(occluder);
                    occluder.SetOccludedState(true);
                    Response.OnOccluded(occluder);
                }

                LastHitTime[occluder] = now;
            }

            for (int i = Active.Count - 1; i >= 0; --i)
            {
                var occluder = Active[i];
                if (occluder == null || now - LastHitTime[occluder] > Settings.RevealDelay)
                {
                    reveal(i);
                    continue;
                }

                Response.Tick(occluder, Time.deltaTime);
            }
        }

        private CameraOccluder resolve(Collider InCollider)
        {
            if (InCollider == null)
            {
                return null;
            }

            CameraOccluder occluder;
            if (Resolved.TryGetValue(InCollider, out occluder) == false)
            {
                occluder = InCollider.GetComponentInParent<CameraOccluder>();
                Resolved.Add(InCollider, occluder);
            }

            return occluder;
        }

        private void reveal(int InIndex)
        {
            var occluder = Active[InIndex];
            Active.RemoveAt(InIndex);
            if (ReferenceEquals(occluder, null) == true)
            {
                return;
            }

            LastHitTime.Remove(occluder);
            if (occluder == null)
            {
                return;
            }

            occluder.SetOccludedState(false);
            Response.OnRevealed(occluder);
        }
    }
}
