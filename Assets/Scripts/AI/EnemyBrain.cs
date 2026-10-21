using System;
using System.Collections;
using System.Collections.Generic;
using ActionRPG.Abilities;
using ActionRPG.Character;
using ActionRPG.Combat;
using ActionRPG.Core;
using ActionRPG.Core.AbilitySystem;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.AI;

namespace ActionRPG.AI
{
    public enum EnemyState : byte
    {
        Idle = 0,
        Patrol = 1,
        Chase = 2,
        Attack = 3,
        Return = 4,
        Stagger = 5,
        Dead = 6
    }

    /// <summary>
    /// 적 AI. 판단과 이동 결정은 전부 서버에서만 하고,
    /// 클라이언트는 NetworkTransform 으로 복제된 위치와 상태값만 본다.
    ///
    /// NavMeshAgent 는 경로 계산에만 쓰고 실제 이동은 CharacterMotor 가 한다.
    /// (플레이어와 같은 이동 규칙을 쓰기 위해서다.)
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(AbilitySystemComponent))]
    [RequireComponent(typeof(Health))]
    public class EnemyBrain : NetworkBehaviour
    {
        [SerializeField] private EnemyDefinition definition;
        [SerializeField] private NavMeshAgent agent;
        [Tooltip("경로 재계산 주기. 낮출수록 반응이 빠르지만 비싸다.")]
        [SerializeField] private float repathInterval = 0.25f;

        private readonly NetworkVariable<EnemyState> _state = new NetworkVariable<EnemyState>(EnemyState.Idle);
        private readonly ThreatTable _threat = new ThreatTable();
        private readonly List<IDamageable> _scanBuffer = new List<IDamageable>(GameConstants.MaxPlayers);

        private CharacterMotor _motor;
        private AbilitySystemComponent _asc;
        private CharacterAnimatorDriver _animator;
        private Health _health;

        private Vector3 _homePosition;
        private Vector3 _patrolTarget;
        private float _patrolWaitUntil;
        private float _nextRepath;
        private float _nextScan;
        private float _stateBusyUntil;
        private float[] _attackCooldowns = Array.Empty<float>();
        private IDamageable _target;

        public EnemyState State => _state.Value;
        public EnemyDefinition Definition => definition;
        /// <summary>서버에서 현재 대상이 바뀔 때. 보스 UI 등이 참고한다.</summary>
        public event Action<IDamageable> TargetChanged;

        private void Awake()
        {
            _motor = GetComponent<CharacterMotor>();
            _asc = GetComponent<AbilitySystemComponent>();
            _animator = GetComponent<CharacterAnimatorDriver>();
            _health = GetComponent<Health>();
            if (agent == null) agent = GetComponent<NavMeshAgent>();
        }

        public override void OnNetworkSpawn()
        {
            if (!IsServer)
            {
                // 클라이언트에서는 AI 도 이동 시뮬레이션도 돌릴 필요가 없다.
                // 모터를 그대로 두면 CharacterController 가 중력을 적용해 복제된 위치와 싸운다.
                if (agent != null) agent.enabled = false;
                if (_motor != null) _motor.enabled = false;
                enabled = false;
                return;
            }

            _homePosition = transform.position;
            _patrolTarget = _homePosition;

            if (agent != null)
            {
                // 이동은 CharacterMotor 가 하므로 에이전트는 경로만 뽑는다.
                agent.updatePosition = false;
                agent.updateRotation = false;
            }

            // 이동 속도는 CombatSet.MoveSpeed 어트리뷰트를 따른다 (둔화 이펙트가 그대로 반영된다).
            _asc.GetGameplayAttributeValueChangeDelegate(CombatSet.MoveSpeedAttribute).OnValueChanged += OnMoveSpeedChanged;
            ApplyDefinition();

            _health.Damaged += OnDamagedServer;
            _health.Died += OnDiedServer;
        }

        public override void OnNetworkDespawn()
        {
            if (!IsServer) return;
            _asc.GetGameplayAttributeValueChangeDelegate(CombatSet.MoveSpeedAttribute).OnValueChanged -= OnMoveSpeedChanged;
            _health.Damaged -= OnDamagedServer;
            _health.Died -= OnDiedServer;
        }

        /// <summary>서버 전용. 스폰 시 정의를 바꿔 끼울 때 사용. 스폰 전에 불러도 되며, 스폰 시점에 적용된다.</summary>
        public void Configure(EnemyDefinition newDefinition)
        {
            definition = newDefinition;
            if (IsSpawned && IsServer) ApplyDefinition();
        }

        /// <summary>정의의 StatBlock 을 ASC 어트리뷰트로 옮긴다.</summary>
        private void ApplyDefinition()
        {
            if (definition == null) return;
            AttributeInitializer.ApplyStatBlock(_asc, definition.Stats);
            _attackCooldowns = new float[Mathf.Max(1, definition.Attacks.Length)];
            OnMoveSpeedChanged(default);
        }

        private void OnMoveSpeedChanged(OnAttributeChangeData _)
        {
            float speed = _asc.GetNumericAttribute(CombatSet.MoveSpeedAttribute);
            _motor.SetBaseSpeed(speed);
            if (agent != null) agent.speed = speed;
        }

        private bool IsStunned => _asc.HasMatchingGameplayTag(GameTags.State_Debuff_Stun);

        private void OnDamagedServer(DamageInfo info)
        {
            if (!IsServer || _state.Value == EnemyState.Dead) return;

            var attacker = FindActorByNetworkId(info.SourceObjectId);
            if (attacker != null) _threat.Add(attacker, info.Amount);

            if (info.KnockbackForce > 0f) _motor.ApplyKnockback(info.HitDirection, info.KnockbackForce);
            if (info.StaggerDuration > 0f) EnterStagger(info.StaggerDuration);
        }

        private void OnDiedServer(DamageInfo _)
        {
            if (!IsServer) return;
            _state.Value = EnemyState.Dead;
            _threat.Clear();
            _motor.SetMoveInput(Vector3.zero);
            if (agent != null) agent.enabled = false;

            var collider = GetComponent<Collider>();
            if (collider != null) collider.enabled = false;
        }

        private void Update()
        {
            if (!IsServer || !IsSpawned) return;
            if (_state.Value == EnemyState.Dead) return;

            float dt = Time.deltaTime;

            if (IsStunned)
            {
                _motor.SetMoveInput(Vector3.zero);
                return;
            }

            UpdateTargeting(dt);

            if (Time.time < _stateBusyUntil)
            {
                if (_state.Value == EnemyState.Attack || _state.Value == EnemyState.Stagger)
                {
                    _motor.SetMoveInput(Vector3.zero);
                    return;
                }
            }

            switch (_state.Value)
            {
                case EnemyState.Idle: TickIdle(); break;
                case EnemyState.Patrol: TickPatrol(); break;
                case EnemyState.Chase: TickChase(); break;
                case EnemyState.Attack: TickChase(); break;
                case EnemyState.Return: TickReturn(); break;
                case EnemyState.Stagger: _state.Value = _target != null ? EnemyState.Chase : EnemyState.Idle; break;
            }

            if (agent != null && agent.enabled) agent.nextPosition = transform.position;
        }

        // ------------------------------------------------------------ 타겟팅

        private void UpdateTargeting(float dt)
        {
            if (definition == null) return;

            if (Time.time >= _nextScan)
            {
                _nextScan = Time.time + 0.3f;
                ScanForTargets();
            }

            var next = _threat.Tick(dt, transform.position, definition.LeashRadius, _homePosition);
            if (!ReferenceEquals(next, _target))
            {
                _target = next;
                TargetChanged?.Invoke(_target);
            }

            if (_target == null && _state.Value == EnemyState.Chase)
                _state.Value = definition.Patrol ? EnemyState.Return : EnemyState.Idle;
        }

        /// <summary>감지 범위 안의 플레이어를 찾아 최소 어그로를 준다.</summary>
        private void ScanForTargets()
        {
            ActorRegistry.QueryHostiles(transform.position, definition.DetectionRadius, _health.Faction, _scanBuffer);

            for (int i = 0; i < _scanBuffer.Count; i++)
            {
                var candidate = _scanBuffer[i];
                Vector3 toTarget = candidate.Transform.position - transform.position;
                float distance = toTarget.magnitude;

                // 시야 밖이면 절반 거리까지만 감지한다.
                toTarget.y = 0f;
                float angle = Vector3.Angle(transform.forward, toTarget);
                float effectiveRange = angle <= definition.FieldOfView * 0.5f
                    ? definition.DetectionRadius
                    : definition.DetectionRadius * 0.5f;

                if (distance > effectiveRange) continue;
                _threat.Add(candidate, 1f);
            }
        }

        private IDamageable FindActorByNetworkId(ulong networkObjectId)
        {
            if (networkObjectId == 0UL || NetworkManager.Singleton == null) return null;
            if (!NetworkManager.Singleton.SpawnManager.SpawnedObjects.TryGetValue(networkObjectId, out var netObject)) return null;
            return netObject != null ? netObject.GetComponent<IDamageable>() : null;
        }

        // ------------------------------------------------------------ 상태별 동작

        private void TickIdle()
        {
            _motor.SetMoveInput(Vector3.zero);

            if (_target != null) { _state.Value = EnemyState.Chase; return; }
            if (definition.Patrol && Time.time >= _patrolWaitUntil) PickPatrolPoint();
        }

        private void TickPatrol()
        {
            if (_target != null) { _state.Value = EnemyState.Chase; return; }

            if (Vector3.Distance(transform.position, _patrolTarget) < 1f)
            {
                _motor.SetMoveInput(Vector3.zero);
                _patrolWaitUntil = Time.time + definition.PatrolWaitSeconds;
                _state.Value = EnemyState.Idle;
                return;
            }

            MoveTowards(_patrolTarget, 0.5f);
        }

        private void TickChase()
        {
            if (_target == null || !_target.IsAlive)
            {
                _state.Value = EnemyState.Return;
                return;
            }

            Vector3 targetPosition = _target.Transform.position;
            float distance = Vector3.Distance(transform.position, targetPosition);

            // 리쉬를 벗어났으면 귀환.
            if (definition.LeashRadius > 0f && Vector3.Distance(transform.position, _homePosition) > definition.LeashRadius)
            {
                _threat.Clear();
                _target = null;
                _state.Value = EnemyState.Return;
                return;
            }

            var attack = definition.PickAttack(distance, _attackCooldowns, Time.time);
            if (attack != null)
            {
                StartAttack(attack, targetPosition);
                return;
            }

            _state.Value = EnemyState.Chase;

            if (distance <= definition.PreferredDistance)
            {
                _motor.SetMoveInput(Vector3.zero);
                _motor.FaceDirection(targetPosition - transform.position);
                return;
            }

            MoveTowards(targetPosition, 1f);
        }

        private void TickReturn()
        {
            if (_target != null) { _state.Value = EnemyState.Chase; return; }

            if (Vector3.Distance(transform.position, _homePosition) < 1.2f)
            {
                _motor.SetMoveInput(Vector3.zero);
                _state.Value = EnemyState.Idle;
                return;
            }

            MoveTowards(_homePosition, 1f);
        }

        private void PickPatrolPoint()
        {
            Vector2 circle = UnityEngine.Random.insideUnitCircle * definition.PatrolRadius;
            Vector3 candidate = _homePosition + new Vector3(circle.x, 0f, circle.y);

            if (NavMesh.SamplePosition(candidate, out var hit, definition.PatrolRadius, NavMesh.AllAreas))
            {
                _patrolTarget = hit.position;
                _state.Value = EnemyState.Patrol;
            }
        }

        /// <summary>NavMesh 경로의 다음 지점을 향해 모터에 입력을 준다.</summary>
        private void MoveTowards(Vector3 destination, float speedScale)
        {
            _motor.SetSpeedMultiplier(speedScale);

            if (agent == null || !agent.enabled || !agent.isOnNavMesh)
            {
                // NavMesh 가 없으면 직선으로라도 다가간다.
                Vector3 direct = destination - transform.position;
                direct.y = 0f;
                _motor.SetMoveInput(direct.normalized);
                return;
            }

            if (Time.time >= _nextRepath)
            {
                _nextRepath = Time.time + repathInterval;
                agent.SetDestination(destination);
            }

            Vector3 desired = agent.desiredVelocity;
            desired.y = 0f;
            _motor.SetMoveInput(desired.sqrMagnitude > 0.01f ? desired.normalized : Vector3.zero);
        }

        // ------------------------------------------------------------ 공격

        private void StartAttack(EnemyDefinition.AttackPattern attack, Vector3 targetPosition)
        {
            int index = definition.IndexOf(attack);
            if (index >= 0 && index < _attackCooldowns.Length) _attackCooldowns[index] = Time.time + attack.Cooldown;

            _state.Value = EnemyState.Attack;
            _stateBusyUntil = Time.time + attack.WindupTime + attack.RecoveryTime;
            _motor.SetMoveInput(Vector3.zero);
            _motor.FaceDirection(targetPosition - transform.position);

            if (_animator != null) _animator.PlayAttack(attack.AnimationIndex);
            StartCoroutine(ResolveAttack(attack));
        }

        private IEnumerator ResolveAttack(EnemyDefinition.AttackPattern attack)
        {
            yield return new WaitForSeconds(attack.WindupTime);
            if (!IsSpawned || _state.Value == EnemyState.Dead) yield break;
            if (IsStunned) yield break;

            Vector3 origin = transform.position + Vector3.up * 1f;
            Vector3 direction = transform.forward;

            if (attack.ProjectilePrefab != null)
            {
                // 발사 순간 공격력을 스냅샷한 피해 스펙을 화살에 실어 보낸다.
                var damageSpec = CombatSpecs.MakeDamageSpec(_asc, CombatSpecs.MakeHitContext(_asc, null, origin, direction),
                    attack.FlatDamage, attack.AttackPowerScale, 0f, attack.DamageType);
                var statusSpec = attack.ApplyStatus != null ? _asc.MakeOutgoingSpec(attack.ApplyStatus, 1f, _asc.MakeEffectContext()) : null;
                Projectile.SpawnConfigured(attack.ProjectilePrefab, origin, direction, _health.Faction, damageSpec, statusSpec, attack.ProjectileSpeed);
            }
            else
            {
                CombatQuery.OverlapHostileArc(origin, direction, attack.HitRadius, attack.ArcDegrees, _health.Faction, _scanBuffer, GameConstants.Layers.CharacterMask);
                for (int i = 0; i < _scanBuffer.Count; i++)
                {
                    var victim = _scanBuffer[i].Transform;
                    Vector3 hitPoint = victim.position + Vector3.up;
                    Vector3 hitDirection = (victim.position - transform.position).normalized;
                    var spec = CombatSpecs.MakeDamageSpec(_asc, CombatSpecs.MakeHitContext(_asc, null, hitPoint, hitDirection),
                        attack.FlatDamage, attack.AttackPowerScale, 0f, attack.DamageType, attack.Knockback);
                    CombatSpecs.ApplyToTarget(_asc, spec, victim.gameObject);
                    CombatSpecs.ApplyEffectToTarget(_asc, attack.ApplyStatus, victim.gameObject);
                }
            }

            yield return new WaitForSeconds(attack.RecoveryTime);
            if (IsSpawned && _state.Value == EnemyState.Attack) _state.Value = EnemyState.Chase;
        }

        private void EnterStagger(float duration)
        {
            _state.Value = EnemyState.Stagger;
            _stateBusyUntil = Time.time + duration;
            _motor.SetMoveInput(Vector3.zero);
        }

        /// <summary>도발 스킬에서 호출 (서버 전용).</summary>
        public void Taunt(IDamageable source)
        {
            if (!IsServer) return;
            _threat.Taunt(source);
        }

        private void OnDrawGizmosSelected()
        {
            if (definition == null) return;
            Vector3 center = Application.isPlaying ? _homePosition : transform.position;

            Gizmos.color = new Color(1f, 0.6f, 0.2f, 0.5f);
            Gizmos.DrawWireSphere(center, definition.DetectionRadius);

            if (definition.LeashRadius > 0f)
            {
                Gizmos.color = new Color(1f, 0.2f, 0.2f, 0.4f);
                Gizmos.DrawWireSphere(center, definition.LeashRadius);
            }

            if (definition.Patrol)
            {
                Gizmos.color = new Color(0.3f, 0.9f, 0.4f, 0.4f);
                Gizmos.DrawWireSphere(center, definition.PatrolRadius);
            }
        }
    }
}
