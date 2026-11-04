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
        [Tooltip("대상 발밑 기준 조준 높이 (m) — SphereCastOcclusionDetector 용")]
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
    /// 카메라와 대상 사이를 가리거나 카메라와 겹친 CameraOccluder 를 찾아 응답 전략으로 처리한다 (기본: 가림 정도만큼 알파 페이드).
    ///  - 가림 정도(Coverage) = 그 대상에 막힌 광선 수 / 감지 광선 수 (카메라와 겹치면 1). 대상 몸을 많이 가릴수록 투명해진다.
    ///  - 가리지 않게 되면 RevealDelay 동안은 마지막 가림 정도를 유지하고, 그 뒤 0 으로 응답에 넘겨 다시 불투명해질 때까지 처리한다.
    ///  - 꺼진(enabled = false) CameraOccluder 는 무시한다 (채집 노드처럼 가려도 그대로 보여야 하는 대상).
    ///  - 감지(ICameraOcclusionDetector)와 응답(ICameraOcclusionResponse)은 SetDetector / SetResponse 로 교체한다.
    ///  - 카메라 이동(LateUpdate) 뒤에 돌도록 실행 순서를 늦춘다.
    /// 문서: Documentation/VRising/CameraOcclusion.md
    /// </summary>
    [DefaultExecutionOrder(1000)]
    [DisallowMultipleComponent]
    public class CameraOcclusionSystem : BaseBehaviour
    {
        public CameraOcclusionSettings Settings = new CameraOcclusionSettings();

        private readonly List<CameraOcclusionHit> Hits = new List<CameraOcclusionHit>();
        private readonly Dictionary<Collider, CameraOccluder> Resolved = new Dictionary<Collider, CameraOccluder>();
        private readonly Dictionary<CameraOccluder, float> LastHitTime = new Dictionary<CameraOccluder, float>();
        /// <summary>대상별 마지막 가림 정도 (RevealDelay 동안 유지).</summary>
        private readonly Dictionary<CameraOccluder, float> LastCoverage = new Dictionary<CameraOccluder, float>();
        /// <summary>이번 프레임 대상별 막힌 광선 비트 (Ray &lt; 0 이면 전부).</summary>
        private readonly Dictionary<CameraOccluder, uint> FrameRays = new Dictionary<CameraOccluder, uint>();
        private readonly List<CameraOccluder> Active = new List<CameraOccluder>();
        private ICameraOcclusionDetector Detector = new CoverageOcclusionDetector();
        private ICameraOcclusionResponse Response = new FadeOcclusionResponse();
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

        public ICameraOcclusionResponse CurrentResponse
        {
            get { return Response; }
        }

        /// <summary>대상의 마지막 가림 정도 (0~1, 처리 중이 아니면 0).</summary>
        public float GetCoverage(CameraOccluder InOccluder)
        {
            float coverage;
            return LastCoverage.TryGetValue(InOccluder, out coverage) == true ? coverage : 0.0f;
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
            Detector = InDetector != null ? InDetector : new CoverageOcclusionDetector();
        }

        /// <summary>응답 전략 교체. 이전 전략으로 숨긴 대상은 먼저 그 전략으로 되돌린다.</summary>
        public void SetResponse(ICameraOcclusionResponse InResponse)
        {
            RevealAll();
            Response = InResponse != null ? InResponse : new FadeOcclusionResponse();
        }

        /// <summary>가림 처리 중인 대상을 모두 원래대로 되돌린다.</summary>
        public void RevealAll()
        {
            for (int i = Active.Count - 1; i >= 0; --i)
            {
                reveal(i);
            }

            LastHitTime.Clear();
            LastCoverage.Clear();
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
            Hits.Clear();
            FrameRays.Clear();
            var query = new CameraOcclusionQuery
            {
                CameraPosition = transform.position,
                TargetPosition = Target.position,
                Settings = Settings,
            };
            Detector.Detect(query, Hits);

            // 대상별 막힌 광선 모으기 (한 광선이 같은 대상의 콜라이더 여러 개에 걸려도 한 번).
            for (int i = 0; i < Hits.Count; ++i)
            {
                var occluder = resolve(Hits[i].Collider);
                if (occluder == null)
                {
                    continue;
                }

                uint rays;
                FrameRays.TryGetValue(occluder, out rays);
                rays |= Hits[i].Ray < 0 ? uint.MaxValue : 1u << Hits[i].Ray;
                FrameRays[occluder] = rays;
            }

            int rayCount = Mathf.Max(1, Detector.RayCount);
            foreach (var pair in FrameRays)
            {
                var occluder = pair.Key;
                float coverage = pair.Value == uint.MaxValue ? 1.0f : Mathf.Clamp01((float)countBits(pair.Value) / rayCount);
                if (LastHitTime.ContainsKey(occluder) == false)
                {
                    Active.Add(occluder);
                    Response.OnOccluded(occluder);
                }

                LastHitTime[occluder] = now;
                LastCoverage[occluder] = coverage;
            }

            for (int i = Active.Count - 1; i >= 0; --i)
            {
                var occluder = Active[i];
                if (occluder == null)
                {
                    reveal(i);
                    continue;
                }

                // RevealDelay 동안은 마지막 가림 정도를 유지 (경계에서 깜빡임 방지), 그 뒤 0 → 페이드 인.
                float coverage = now - LastHitTime[occluder] > Settings.RevealDelay ? 0.0f : LastCoverage[occluder];
                LastCoverage[occluder] = coverage;
                occluder.SetOccludedState(coverage > 0.0f);
                bool busy = Response.Tick(occluder, coverage, Time.deltaTime);
                if (coverage <= 0.0f && busy == false)
                {
                    reveal(i);
                }
            }
        }

        private static int countBits(uint InValue)
        {
            int count = 0;
            while (InValue != 0)
            {
                InValue &= InValue - 1;
                count += 1;
            }

            return count;
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

            return occluder != null && occluder.enabled == true ? occluder : null;
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
            LastCoverage.Remove(occluder);
            if (occluder == null)
            {
                return;
            }

            occluder.SetOccludedState(false);
            Response.OnRevealed(occluder);
        }
    }
}
