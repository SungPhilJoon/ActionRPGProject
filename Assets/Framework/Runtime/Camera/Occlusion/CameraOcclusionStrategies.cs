using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace Framework
{
    /// <summary>감지에 필요한 입력. 감지 전략은 이 값만 보고 콜라이더를 모은다.</summary>
    public struct CameraOcclusionQuery
    {
        public Vector3 CameraPosition;
        public Vector3 TargetPosition;
        public CameraOcclusionSettings Settings;
    }

    /// <summary>
    /// 감지 전략 (Strategy): 이번 프레임 카메라를 가리거나 카메라와 겹친 콜라이더를 OutColliders 에 채운다.
    /// 교체 예: 여러 대상(파티원) 모두를 향한 캐스트, 화면 사각형 기반 판정, 렌더러 바운드 판정.
    /// </summary>
    public interface ICameraOcclusionDetector
    {
        void Detect(in CameraOcclusionQuery InQuery, List<Collider> OutColliders);
    }

    /// <summary>
    /// 응답 전략 (Strategy): 가림이 시작/끝날 때 대상 렌더러를 어떻게 처리할지.
    /// Tick 은 가림 처리 중인 대상마다 매 프레임 호출된다 (페이드처럼 시간이 필요한 응답용).
    /// 교체 예: 디더 페이드(머티리얼 프로퍼티), 반투명, 실루엣.
    /// </summary>
    public interface ICameraOcclusionResponse
    {
        void OnOccluded(CameraOccluder InOccluder);
        void OnRevealed(CameraOccluder InOccluder);
        void Tick(CameraOccluder InOccluder, float InDeltaTime);
    }

    /// <summary>
    /// 기본 감지: 카메라 → 대상 SphereCast (시야를 가림) + 카메라 위치 OverlapSphere (카메라와 충돌).
    /// 대상 바로 앞 TargetPadding 구간은 캐스트하지 않아 대상 뒤/옆 물체는 숨기지 않는다.
    /// </summary>
    public class SphereCastOcclusionDetector : ICameraOcclusionDetector
    {
        private RaycastHit[] Hits = new RaycastHit[32];
        private Collider[] Overlaps = new Collider[32];

        public void Detect(in CameraOcclusionQuery InQuery, List<Collider> OutColliders)
        {
            var settings = InQuery.Settings;
            ensureCapacity(settings.MaxHits);

            Vector3 toTarget = InQuery.TargetPosition - InQuery.CameraPosition;
            float distance = toTarget.magnitude - settings.TargetPadding;
            if (distance > 0.01f)
            {
                int count = Physics.SphereCastNonAlloc(InQuery.CameraPosition, settings.CastRadius, toTarget.normalized, Hits, distance, settings.Mask, settings.Triggers);
                for (int i = 0; i < count; ++i)
                {
                    OutColliders.Add(Hits[i].collider);
                }
            }

            if (settings.CameraRadius > 0.0f)
            {
                int count = Physics.OverlapSphereNonAlloc(InQuery.CameraPosition, settings.CameraRadius, Overlaps, settings.Mask, settings.Triggers);
                for (int i = 0; i < count; ++i)
                {
                    OutColliders.Add(Overlaps[i]);
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
    /// 기본 응답: 메시를 카메라에서 숨긴다 (컬링). 그림자는 남긴다 — 숨겨도 땅의 그늘(햇빛 판정)과 화면이 어긋나지 않는다.
    ///  - 원래 그림자를 드리우던 렌더러 → ShadowsOnly
    ///  - 원래 그림자가 없던 렌더러 → forceRenderingOff
    /// 가림이 끝나면 CameraOccluder 가 기억한 원래 모드로 되돌린다.
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

        public void Tick(CameraOccluder InOccluder, float InDeltaTime)
        {
        }
    }
}
