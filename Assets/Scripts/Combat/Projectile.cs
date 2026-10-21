using System;
using System.Collections.Generic;
using ActionRPG.Core;
using ActionRPG.Core.AbilitySystem;
using ActionRPG.Core.Lifecycle;
using Unity.Netcode;
using UnityEngine;

namespace ActionRPG.Combat
{
    /// <summary>발사 순간의 정보. 스폰 페이로드에 실려 모든 피어가 같은 궤적을 그리게 한다.</summary>
    public struct ProjectileLaunch : INetworkSerializable, IEquatable<ProjectileLaunch>
    {
        public Vector3 Origin;
        public Vector3 Direction;
        public float Speed;
        public float Gravity;
        public double ServerTime;

        public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
        {
            serializer.SerializeValue(ref Origin);
            serializer.SerializeValue(ref Direction);
            serializer.SerializeValue(ref Speed);
            serializer.SerializeValue(ref Gravity);
            serializer.SerializeValue(ref ServerTime);
        }

        public bool Equals(ProjectileLaunch other) =>
            Origin == other.Origin && Direction == other.Direction &&
            Mathf.Approximately(Speed, other.Speed) && ServerTime.Equals(other.ServerTime);
    }

    /// <summary>
    /// 화살/마법탄. 궤적은 모든 피어가 같은 식으로 계산하고, 명중 판정은 서버만 한다.
    /// NetworkTransform 을 쓰지 않으므로 빠른 발사체도 끊기지 않는다.
    ///
    /// 피해는 발사 순간 만들어 둔 GameplayEffectSpec 을 맞은 대상에게 적용한다 (언리얼에서 발사체에
    /// FGameplayEffectSpecHandle 을 실어 보내는 방식). 공격자 스탯은 발사 시점 값으로 스냅샷된다.
    /// </summary>
    [RequireComponent(typeof(NetworkObject))]
    public class Projectile : NetworkBehaviour, ITrackable
    {
        [Header("궤적")]
        [SerializeField] private float defaultSpeed = 28f;
        [SerializeField] private float gravity = 9.8f;
        [SerializeField] private float lifetime = 5f;
        [SerializeField] private float hitRadius = 0.25f;
        [SerializeField] private bool alignToVelocity = true;

        [Header("명중")]
        [Tooltip("몇 명까지 관통할지. 1 이면 첫 대상에서 소멸.")]
        [Min(1)] [SerializeField] private int maxHits = 1;
        [SerializeField] private LayerMask blockingLayers = 1; // Default

        [Header("연출")]
        [SerializeField] private GameObject impactVfxPrefab;
        [SerializeField] private GameObject trailRoot;

        private readonly NetworkVariable<ProjectileLaunch> _launch = new NetworkVariable<ProjectileLaunch>();

        // 서버 전용 상태
        private Faction _faction = Faction.Neutral;
        private GameplayEffectSpec _damageSpec;
        private GameplayEffectSpec _onHitSpec;
        private readonly HashSet<int> _alreadyHit = new HashSet<int>();
        private Vector3 _previousPosition;
        private bool _consumed;

        /// <summary>
        /// 서버 전용. Spawn() 직전에 호출한다.
        /// damageSpec 은 맞은 대상마다 복제되어 적용되고, onHitSpec(상태이상 등)은 있으면 함께 적용된다.
        /// </summary>
        public void Configure(Vector3 origin, Vector3 direction, Faction faction, GameplayEffectSpec damageSpec,
            GameplayEffectSpec onHitSpec = null, float speedOverride = -1f)
        {
            _faction = faction;
            _damageSpec = damageSpec;
            _onHitSpec = onHitSpec;

            transform.position = origin;
            if (direction.sqrMagnitude > 0.0001f) transform.rotation = Quaternion.LookRotation(direction.normalized, Vector3.up);

            _launch.Value = new ProjectileLaunch
            {
                Origin = origin,
                Direction = direction.sqrMagnitude > 0.0001f ? direction.normalized : Vector3.forward,
                Speed = speedOverride > 0f ? speedOverride : defaultSpeed,
                Gravity = gravity,
                ServerTime = NetworkManager.Singleton != null ? NetworkManager.Singleton.ServerTime.Time : 0d
            };
        }

        // --- ITrackable: 화면에 발사체가 몇 개나 떠 있는지 Lifecycle Tracker 창에서 확인할 수 있다 ---
        string ITrackable.TrackableName => gameObject.name;
        string ITrackable.TrackableCategory => "Projectile";
        private LifecycleHandle _lifecycleHandle;

        public override void OnNetworkSpawn()
        {
            _previousPosition = _launch.Value.Origin;
            transform.position = _launch.Value.Origin;
            if (trailRoot != null) trailRoot.SetActive(true);
            if (IsServer) _lifecycleHandle = LifecycleTracker.Register(this);
        }

        public override void OnNetworkDespawn() => _lifecycleHandle.Dispose();

        private void Update()
        {
            if (!IsSpawned || _consumed) return;

            var launch = _launch.Value;
            double now = NetworkManager.Singleton != null ? NetworkManager.Singleton.ServerTime.Time : launch.ServerTime;
            float t = Mathf.Max(0f, (float)(now - launch.ServerTime));

            Vector3 position = launch.Origin
                               + launch.Direction * (launch.Speed * t)
                               + Vector3.down * (0.5f * launch.Gravity * t * t);
            Vector3 velocity = launch.Direction * launch.Speed + Vector3.down * (launch.Gravity * t);

            if (alignToVelocity && velocity.sqrMagnitude > 0.0001f)
                transform.rotation = Quaternion.LookRotation(velocity.normalized, Vector3.up);

            if (IsServer) SweepForHits(_previousPosition, position);

            transform.position = position;
            _previousPosition = position;

            if (IsServer && t > lifetime) Despawn(false, position);
        }

        /// <summary>프레임 사이를 건너뛰지 않도록 이전 위치에서 현재 위치까지 스윕 판정한다.</summary>
        private void SweepForHits(Vector3 from, Vector3 to)
        {
            Vector3 delta = to - from;
            float distance = delta.magnitude;
            if (distance <= 0.0001f) return;
            Vector3 dir = delta / distance;

            int mask = GameConstants.Layers.CharacterMask | blockingLayers.value;
            var hits = Physics.SphereCastAll(from, hitRadius, dir, distance, mask, QueryTriggerInteraction.Ignore);
            if (hits.Length == 0) return;
            Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));

            for (int i = 0; i < hits.Length; i++)
            {
                var hit = hits[i];
                var damageable = CombatQuery.Resolve(hit.collider);
                if (damageable == null)
                {
                    // 벽이나 지형에 박혔다.
                    if (((1 << hit.collider.gameObject.layer) & blockingLayers.value) != 0)
                    {
                        Despawn(true, hit.point);
                        return;
                    }
                    continue;
                }

                if (!damageable.IsAlive || !_faction.IsHostileTo(damageable.Faction)) continue;
                if (!_alreadyHit.Add(damageable.Transform.GetInstanceID())) continue;

                ApplyHit(damageable.Transform.gameObject, hit.point, dir);

                if (_alreadyHit.Count >= maxHits)
                {
                    Despawn(true, hit.point);
                    return;
                }
            }
        }

        private void ApplyHit(GameObject target, Vector3 point, Vector3 direction)
        {
            var targetAsc = AbilitySystemStatics.GetAbilitySystemComponent(target);
            if (targetAsc == null) return;

            if (_damageSpec != null)
            {
                // 맞은 위치를 담기 위해 대상마다 스펙을 복제한다.
                var spec = _damageSpec.Duplicate();
                spec.Context.AddHitResult(point, direction);
                targetAsc.ApplyGameplayEffectSpecToSelf(spec);
            }
            if (_onHitSpec != null)
            {
                var spec = _onHitSpec.Duplicate();
                spec.Context.AddHitResult(point, direction);
                targetAsc.ApplyGameplayEffectSpecToSelf(spec);
            }
        }

        private void Despawn(bool impact, Vector3 point)
        {
            if (_consumed) return;
            _consumed = true;
            if (impact) PlayImpactRpc(point);
            if (NetworkObject != null && NetworkObject.IsSpawned) NetworkObject.Despawn();
        }

        [Rpc(SendTo.Everyone)]
        private void PlayImpactRpc(Vector3 point)
        {
            if (impactVfxPrefab == null) return;
            var vfx = Instantiate(impactVfxPrefab, point, Quaternion.LookRotation(-transform.forward, Vector3.up));
            Destroy(vfx, 3f);
        }

        /// <summary>서버 전용. 프리팹을 만들고 설정한 뒤 스폰까지 한 번에 한다.</summary>
        public static Projectile SpawnConfigured(GameObject prefab, Vector3 origin, Vector3 direction, Faction faction,
            GameplayEffectSpec damageSpec, GameplayEffectSpec onHitSpec = null, float speed = -1f)
        {
            if (prefab == null) return null;
            var instance = Instantiate(prefab, origin, Quaternion.LookRotation(direction.sqrMagnitude > 0.0001f ? direction : Vector3.forward, Vector3.up));
            var projectile = instance.GetComponent<Projectile>();
            var netObject = instance.GetComponent<NetworkObject>();
            if (projectile == null || netObject == null)
            {
                Debug.LogError($"[Projectile] {prefab.name} 에 Projectile / NetworkObject 가 없습니다.", prefab);
                Destroy(instance);
                return null;
            }
            projectile.Configure(origin, direction, faction, damageSpec, onHitSpec, speed);
            netObject.Spawn(true);
            return projectile;
        }
    }
}
