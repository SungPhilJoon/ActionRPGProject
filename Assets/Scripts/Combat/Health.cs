using System;
using ActionRPG.Abilities;
using ActionRPG.Core;
using ActionRPG.Core.AbilitySystem;
using ActionRPG.Core.Lifecycle;
using Unity.Netcode;
using UnityEngine;

namespace ActionRPG.Combat
{
    /// <summary>
    /// 체력 컴포넌트 (라이라 ULyraHealthComponent 에 해당).
    ///
    /// 체력 값 자체는 ASC 의 <see cref="HealthSet"/> 이 가진다. 이 컴포넌트는
    ///  - HealthSet 의 변화를 게임 코드가 쓰기 쉬운 이벤트(Changed / Damaged / Died / Revived)로 바꾸고,
    ///  - 사망 처리(State.Dead 태그, 어빌리티 취소, 디버프 제거, 디스폰)를 맡으며,
    ///  - 기존 코드가 쓰던 <see cref="IDamageable"/> 계약을 유지한다.
    ///
    /// 사망 여부는 복제되는 State.Dead 태그로 판단하므로 모든 피어에서 같다.
    /// 피격 연출은 GameplayCue.Character.Hit 큐로 전파된다.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(NetworkObject))]
    [RequireComponent(typeof(AbilitySystemComponent))]
    public class Health : NetworkBehaviour, IDamageable, ITrackable, IGameplayCueInterface
    {
        [SerializeField] private Faction faction = Faction.Enemy;
        [Tooltip("사망 후 자동으로 디스폰하기까지의 시간. 0 이하면 디스폰하지 않는다.")]
        [SerializeField] private float despawnDelay = 4f;

        private AbilitySystemComponent _asc;
        private HealthSet _healthSet;
        private GameplayTagCountContainer.TagEvent _deadEvent;
        private DamageInfo _lastDamage;
        private float _deathTime = float.MaxValue;
        private LifecycleHandle _lifecycleHandle;

        public float Current => _healthSet != null ? _healthSet.Health.CurrentValue : 0f;
        public float Max => _healthSet != null ? _healthSet.MaxHealth.CurrentValue : 0f;
        public float Normalized => Max > 0f ? Current / Max : 0f;
        public bool IsAlive => _asc == null || !_asc.HasMatchingGameplayTag(GameTags.State_Dead);
        public Faction Faction => faction;
        public Transform Transform => transform;
        public AbilitySystemComponent AbilitySystem => _asc;

        string ITrackable.TrackableName => gameObject.name;
        string ITrackable.TrackableCategory => $"Health/{faction}";

        /// <summary>(current, max)</summary>
        public event Action<float, float> Changed;
        /// <summary>
        /// 피격. 서버에서는 넉백 · 경직까지 담긴 전체 정보로, 다른 피어에서는 큐로 받은 정보로 호출된다.
        /// </summary>
        public event Action<DamageInfo> Damaged;
        public event Action<DamageInfo> Died;
        public event Action Revived;

        private void Awake()
        {
            _asc = GetComponent<AbilitySystemComponent>();
        }

        public override void OnNetworkSpawn()
        {
            _healthSet = _asc.GetSet<HealthSet>();
            if (_healthSet == null)
            {
                Debug.LogError($"[Health] {name} 의 ASC 에 HealthSet 이 없습니다. AbilitySet 의 GrantedAttributes 를 확인하세요.", this);
                return;
            }

            _asc.GetGameplayAttributeValueChangeDelegate(HealthSet.HealthAttribute).OnValueChanged += OnHealthAttributeChanged;
            _asc.GetGameplayAttributeValueChangeDelegate(HealthSet.MaxHealthAttribute).OnValueChanged += OnHealthAttributeChanged;
            _deadEvent = _asc.RegisterGameplayTagEvent(GameTags.State_Dead, GameplayTagEventType.NewOrRemoved);
            _deadEvent.OnEvent += OnDeadTagChanged;

            if (IsServer)
            {
                _healthSet.OnDamageTaken += OnDamageTakenServer;
                _healthSet.OnOutOfHealth += HandleOutOfHealth;
                _lifecycleHandle = LifecycleTracker.Register(this);
            }

            ActorRegistry.Register(this);
            RaiseChanged();
        }

        public override void OnNetworkDespawn()
        {
            if (_healthSet != null)
            {
                _asc.GetGameplayAttributeValueChangeDelegate(HealthSet.HealthAttribute).OnValueChanged -= OnHealthAttributeChanged;
                _asc.GetGameplayAttributeValueChangeDelegate(HealthSet.MaxHealthAttribute).OnValueChanged -= OnHealthAttributeChanged;
                _healthSet.OnDamageTaken -= OnDamageTakenServer;
                _healthSet.OnOutOfHealth -= HandleOutOfHealth;
            }
            if (_deadEvent != null) _deadEvent.OnEvent -= OnDeadTagChanged;
            ActorRegistry.Unregister(this);
            _lifecycleHandle.Dispose();
        }

        private void Update()
        {
            if (!IsServer || IsAlive || despawnDelay <= 0f) return;
            if (Time.time - _deathTime < despawnDelay) return;
            _deathTime = float.MaxValue;
            if (NetworkObject != null && NetworkObject.IsSpawned) NetworkObject.Despawn();
        }

        // ---------------------------------------------------------------- 어트리뷰트 → 이벤트

        private void OnHealthAttributeChanged(OnAttributeChangeData _) => RaiseChanged();

        private void RaiseChanged()
        {
            Changed?.Invoke(Current, Max);
            GameEventBus.RaiseHealthChanged(gameObject, Current, Max);
        }

        private void OnDeadTagChanged(GameplayTag _, int count)
        {
            if (count > 0)
            {
                Died?.Invoke(_lastDamage);
                GameEventBus.RaiseEntityDied(gameObject, _lastDamage);
            }
            else
            {
                Revived?.Invoke();
            }
        }

        // ---------------------------------------------------------------- 피격

        /// <summary>서버: HealthSet 이 실제 피해를 반영한 직후.</summary>
        private void OnDamageTakenServer(DamageInfo info, GameplayEffectSpec spec)
        {
            _lastDamage = info;
            Damaged?.Invoke(info);
            GameEventBus.RaiseDamageDealt(gameObject, info);

            // 다른 피어에는 게임플레이 큐로 알린다.
            var parameters = new GameplayCueParameters
            {
                RawMagnitude = info.Amount,
                Location = info.HitPoint != Vector3.zero ? info.HitPoint : transform.position + Vector3.up * 1.6f,
                Normal = info.HitDirection,
                InstigatorId = info.SourceObjectId,
                IsCriticalHit = info.IsCritical,
                UserData = (int)info.Type
            };
            _asc.ExecuteGameplayCue(GameTags.Cue_Character_Hit, parameters);
        }

        /// <summary>클라이언트: 서버가 보낸 피격 큐를 받아 같은 이벤트를 낸다. 서버는 이미 냈으므로 무시한다.</summary>
        void IGameplayCueInterface.HandleGameplayCue(GameplayTag cueTag, GameplayCueEvent eventType, in GameplayCueParameters parameters)
        {
            if (eventType != GameplayCueEvent.Executed || !cueTag.MatchesTagExact(GameTags.Cue_Character_Hit)) return;
            if (_asc != null && _asc.IsAuthority) return;

            var info = DamageInfo.Create(parameters.RawMagnitude, (DamageType)parameters.UserData, Faction.Neutral, parameters.InstigatorId);
            info.HitPoint = parameters.Location;
            info.HitDirection = parameters.Normal;
            info.IsCritical = parameters.IsCriticalHit;
            _lastDamage = info;
            Damaged?.Invoke(info);
            GameEventBus.RaiseDamageDealt(gameObject, info);
        }

        // ---------------------------------------------------------------- 사망 / 부활 (서버)

        private void HandleOutOfHealth(DamageInfo info)
        {
            if (!IsServer || !IsAlive) return;
            _lastDamage = info;
            _deathTime = Time.time;

            // 언리얼 샘플들처럼: 사망 태그 → 진행 중 어빌리티 취소 → 지속형 디버프 정리.
            _asc.AddReplicatedLooseGameplayTag(GameTags.State_Dead);
            _asc.CancelAllAbilities();
            _asc.RemoveActiveEffects(GameplayEffectQuery.MakeQuery_Custom(e => e.Spec.Duration > 0f));
        }

        /// <summary>서버 전용. 즉사시킨다.</summary>
        public void Kill(DamageInfo info)
        {
            if (!IsServer || !IsAlive || _healthSet == null) return;
            _asc.SetNumericAttributeBase(HealthSet.HealthAttribute, 0f);
            HandleOutOfHealth(info);
        }

        /// <summary>서버 전용 부활. 던전에서 파티원이 살려 줄 때 · 리스폰할 때 사용.</summary>
        public void Revive(float healthPercent = 0.5f)
        {
            if (!IsServer || IsAlive || _healthSet == null) return;
            _deathTime = float.MaxValue;
            _healthSet.ResetOutOfHealth();
            _asc.SetNumericAttributeBase(HealthSet.HealthAttribute, Mathf.Clamp(Max * Mathf.Clamp01(healthPercent), 1f, Max));
            for (int guard = 0; guard < 8 && _asc.HasMatchingGameplayTag(GameTags.State_Dead); guard++)
                _asc.RemoveReplicatedLooseGameplayTag(GameTags.State_Dead);
        }

        // ---------------------------------------------------------------- IDamageable

        /// <summary>
        /// 서버 전용. 이미 계산된 피해량을 방어력만 적용해 넣는다.
        /// 새 코드는 CombatSpecs.MakeDamageSpec 으로 스펙을 만들어 ASC 에 적용하는 것이 정석이다.
        /// </summary>
        public void ApplyDamage(DamageInfo info)
        {
            if (!IsServer || !IsAlive || info.Amount <= 0f) return;
            float defense = _asc.GetNumericAttribute(CombatSet.DefenseAttribute);
            var context = _asc.MakeEffectContext();
            context.AddHitResult(info.HitPoint, info.HitDirection);
            context.IsCriticalHit = info.IsCritical;
            _asc.ApplyModToAttribute(HealthSet.IncomingDamageAttribute, GameplayModOp.Additive,
                DamageCalculator.Mitigate(info.Amount, info.Type, defense), context);
        }

        /// <summary>서버 전용. 회복량을 그대로 넣는다.</summary>
        public void ApplyHeal(float amount, ulong sourceObjectId)
        {
            if (!IsServer || !IsAlive || amount <= 0f) return;
            _asc.ApplyModToAttribute(HealthSet.IncomingHealingAttribute, GameplayModOp.Additive, amount);
        }
    }
}
