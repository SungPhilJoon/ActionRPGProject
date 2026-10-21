using System.Collections.Generic;
using ActionRPG.Core;
using UnityEngine;

namespace ActionRPG.Combat
{
    /// <summary>
    /// 범위 판정을 모아 둔 헬퍼. 모든 결과는 미리 할당된 버퍼에 담아 GC 를 만들지 않는다.
    /// 서버에서만 호출한다.
    /// </summary>
    public static class CombatQuery
    {
        private const int MaxHits = 64;
        private static readonly Collider[] HitBuffer = new Collider[MaxHits];
        private static readonly HashSet<int> SeenRoots = new HashSet<int>();

        /// <summary>구 범위 안의 적대 대상. 같은 캐릭터의 콜라이더가 여러 개여도 한 번만 담긴다.</summary>
        public static int OverlapHostiles(Vector3 center, float radius, Faction source, List<IDamageable> results, int layerMask = ~0)
        {
            results.Clear();
            SeenRoots.Clear();

            int count = Physics.OverlapSphereNonAlloc(center, radius, HitBuffer, layerMask, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < count; i++)
            {
                var damageable = Resolve(HitBuffer[i]);
                if (damageable == null || !damageable.IsAlive) continue;
                if (!source.IsHostileTo(damageable.Faction)) continue;
                if (!SeenRoots.Add(damageable.Transform.GetInstanceID())) continue;
                results.Add(damageable);
            }
            return results.Count;
        }

        /// <summary>부채꼴(전방 arcDegrees) 범위 안의 적대 대상. 검 휘두르기 판정용.</summary>
        public static int OverlapHostileArc(
            Vector3 center, Vector3 forward, float radius, float arcDegrees,
            Faction source, List<IDamageable> results, int layerMask = ~0)
        {
            results.Clear();
            SeenRoots.Clear();

            forward.y = 0f;
            if (forward.sqrMagnitude < 0.0001f) forward = Vector3.forward;
            forward.Normalize();
            float cosHalf = Mathf.Cos(arcDegrees * 0.5f * Mathf.Deg2Rad);

            int count = Physics.OverlapSphereNonAlloc(center, radius, HitBuffer, layerMask, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < count; i++)
            {
                var damageable = Resolve(HitBuffer[i]);
                if (damageable == null || !damageable.IsAlive) continue;
                if (!source.IsHostileTo(damageable.Faction)) continue;

                Vector3 toTarget = damageable.Transform.position - center;
                toTarget.y = 0f;
                if (toTarget.sqrMagnitude > 0.0001f && Vector3.Dot(forward, toTarget.normalized) < cosHalf) continue;
                if (!SeenRoots.Add(damageable.Transform.GetInstanceID())) continue;
                results.Add(damageable);
            }
            return results.Count;
        }

        /// <summary>구 범위 안의 아군(자신 포함 가능). 힐/버프 스킬용.</summary>
        public static int OverlapAllies(Vector3 center, float radius, Faction source, List<IDamageable> results, bool includeSelfFaction = true, int layerMask = ~0)
        {
            results.Clear();
            SeenRoots.Clear();

            int count = Physics.OverlapSphereNonAlloc(center, radius, HitBuffer, layerMask, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < count; i++)
            {
                var damageable = Resolve(HitBuffer[i]);
                if (damageable == null || !damageable.IsAlive) continue;
                bool ally = includeSelfFaction ? damageable.Faction == source : damageable.Faction.IsAllyOf(source);
                if (!ally) continue;
                if (!SeenRoots.Add(damageable.Transform.GetInstanceID())) continue;
                results.Add(damageable);
            }
            return results.Count;
        }

        /// <summary>조준선을 따라 첫 번째 적대 대상을 찾는다. 화살 즉발형 스킬에 사용.</summary>
        public static IDamageable RaycastFirstHostile(Vector3 origin, Vector3 direction, float distance, Faction source, int layerMask = ~0)
        {
            if (!Physics.Raycast(origin, direction.normalized, out var hit, distance, layerMask, QueryTriggerInteraction.Ignore))
                return null;

            var damageable = Resolve(hit.collider);
            if (damageable == null || !damageable.IsAlive) return null;
            return source.IsHostileTo(damageable.Faction) ? damageable : null;
        }

        /// <summary>콜라이더가 어느 캐릭터의 것인지 거슬러 올라가 찾는다.</summary>
        public static IDamageable Resolve(Collider collider)
        {
            if (collider == null) return null;
            // Rigidbody 가 있으면 그쪽이 캐릭터 루트일 확률이 높다.
            if (collider.attachedRigidbody != null)
            {
                var fromBody = collider.attachedRigidbody.GetComponent<IDamageable>();
                if (fromBody != null) return fromBody;
            }
            return collider.GetComponentInParent<IDamageable>();
        }

        /// <summary>바닥 지점을 구한다. 지면 타겟 스킬의 조준점 계산용.</summary>
        public static bool TryGetGroundPoint(Vector3 origin, Vector3 direction, float maxDistance, out Vector3 point)
        {
            if (Physics.Raycast(origin, direction.normalized, out var hit, maxDistance, GameConstants.Layers.GroundMask, QueryTriggerInteraction.Ignore))
            {
                point = hit.point;
                return true;
            }
            point = origin + direction.normalized * maxDistance;
            return false;
        }
    }
}
