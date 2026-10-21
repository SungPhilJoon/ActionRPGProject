using System;
using System.Collections.Generic;
using ActionRPG.Character;
using ActionRPG.Combat;
using ActionRPG.Core;
using ActionRPG.Core.AbilitySystem;
using UnityEngine;

namespace ActionRPG.Abilities
{
    /// <summary>
    /// 평타(기본 공격) 어빌리티. 기존 BasicAttackController 를 GAS 로 옮긴 것이다.
    ///
    ///  - LocalPredicted: 소유 클라이언트가 먼저 애니메이션 · 이동 잠금 · 쿨다운을 보여 주고, 서버가 검증 후 판정한다.
    ///  - 쿨다운: GE_Cooldown_BasicAttack (MMC 가 SetByCaller.Cooldown ÷ 공격 속도로 길이를 계산).
    ///  - 판정: 서버에서만. 검은 부채꼴 범위에 GE_Damage, 활은 피해 스펙을 실은 화살을 스폰한다.
    ///  - 막힘: 사망 · 기절 · 무장해제 태그가 있으면 발동하지 않는다.
    /// </summary>
    [CreateAssetMenu(menuName = "ActionRPG/Abilities/GA_BasicAttack", fileName = "GA_BasicAttack")]
    public class GA_BasicAttack : GameplayAbility
    {
        [Serializable]
        public class AttackProfile
        {
            public WeaponType Weapon = WeaponType.Sword;

            [Header("타이밍")]
            [Tooltip("공격 입력 후 실제 판정이 나가기까지의 시간.")]
            public float WindupTime = 0.18f;
            [Tooltip("공격 사이 최소 간격. 공격속도 어트리뷰트로 나눠진다 (쿨다운 이펙트의 SetByCaller).")]
            public float Cooldown = 0.55f;
            [Tooltip("이 시간 동안 이동이 묶인다.")]
            public float MovementLockTime = 0.25f;
            [Tooltip("마지막 타격 후 이 시간이 지나면 콤보가 처음으로 돌아간다.")]
            public float ComboResetTime = 1.1f;

            [Header("피해 (GE_Damage 의 SetByCaller 로 전달)")]
            public float FlatDamage = 4f;
            public float AttackPowerScale = 1f;
            public DamageType DamageType = DamageType.Physical;
            [Tooltip("콤보 단계별 피해 배율. 길이가 곧 콤보 길이다.")]
            public float[] ComboMultipliers = { 1f, 1.1f, 1.45f };

            [Header("근접 판정 (검)")]
            public float Range = 2.3f;
            [Range(10f, 360f)] public float ArcDegrees = 120f;
            public float Knockback = 2f;

            [Header("발사체 (활)")]
            public GameObject ProjectilePrefab;
            public float ProjectileSpeed = 30f;
        }

        public AttackProfile[] Profiles =
        {
            new AttackProfile { Weapon = WeaponType.Sword },
            new AttackProfile
            {
                Weapon = WeaponType.Bow,
                WindupTime = 0.28f,
                Cooldown = 0.75f,
                MovementLockTime = 0.35f,
                Range = 30f,
                AttackPowerScale = 1.15f,
                ComboMultipliers = new[] { 1f }
            }
        };

        // ---------------------------------------------------------------- 실행 상태 (인스턴스)

        [NonSerialized] private int _comboIndex;
        [NonSerialized] private float _comboExpireTime;
        [NonSerialized] private AttackProfile _profile;
        [NonSerialized] private int _combo;
        [NonSerialized] private WeaponType _weapon;
        [NonSerialized] private Vector3 _aim;
        [NonSerialized] private CharacterMotor _lockedMotor;

        private static readonly List<IDamageable> HitBuffer = new List<IDamageable>(16);

        protected override void OnEnable()
        {
            AbilityTags.AddTag(GameTags.Ability_BasicAttack);
            ActivationOwnedTags.AddTag(GameTags.State_Attacking);
            ActivationBlockedTags.AddTag(GameTags.State_Dead);
            ActivationBlockedTags.AddTag(GameTags.State_Debuff_Stun);
            ActivationBlockedTags.AddTag(GameTags.State_Debuff_Disarm);
            base.OnEnable();
        }

        // ---------------------------------------------------------------- 발동 조건

        public override bool CanActivateAbility(GameplayAbilitySpecHandle handle, GameplayAbilityActorInfo actorInfo,
            GameplayTagContainer optionalRelevantTags = null)
        {
            if (!base.CanActivateAbility(handle, actorInfo, optionalRelevantTags)) return false;
            // 회피 · 무기 교체 중에는 못 친다. 이 판정은 이동 권한을 가진 조종 피어에서만 의미가 있다.
            if (actorInfo.IsLocallyControlled)
            {
                var motor = actorInfo.GetAvatarComponent<CharacterMotor>();
                if (motor != null && motor.IsDodging) return false;
                var weapons = actorInfo.GetAvatarComponent<WeaponController>();
                if (weapons != null && weapons.IsSwapping) return false;
            }
            return GetProfile(actorInfo) != null;
        }

        protected override GameplayEffectSpec MakeCooldownGameplayEffectSpec(GameplayAbilitySpecHandle handle, GameplayAbilityActorInfo actorInfo)
        {
            var profile = GetProfile(actorInfo);
            if (CooldownGameplayEffect == null || profile == null) return null;
            var spec = MakeOutgoingGameplayEffectSpec(handle, actorInfo, CooldownGameplayEffect);
            spec.SetSetByCallerMagnitude(GameTags.SetByCaller_Cooldown, profile.Cooldown);
            return spec;
        }

        // ---------------------------------------------------------------- 발동

        protected override void ActivateAbility(GameplayAbilitySpecHandle handle, GameplayAbilityActorInfo actorInfo,
            GameplayAbilityActivationInfo activationInfo, GameplayEventData? triggerEventData)
        {
            _profile = GetProfile(actorInfo);
            if (_profile == null || !CommitAbility(handle, actorInfo, activationInfo))
            {
                EndAbility(handle, actorInfo, activationInfo, true, true);
                return;
            }

            var avatar = actorInfo.AvatarActor;
            var weapons = avatar.GetComponent<WeaponController>();
            _weapon = weapons != null ? weapons.Current : WeaponType.Sword;

            _aim = triggerEventData.HasValue && triggerEventData.Value.HasTargetData
                ? triggerEventData.Value.TargetData.Direction
                : avatar.transform.forward;
            _aim.y = 0f;
            if (_aim.sqrMagnitude < 0.0001f) _aim = avatar.transform.forward;
            _aim.Normalize();

            // 콤보 단계 (서버와 소유 클라이언트가 각자 같은 규칙으로 센다. 피해 배율은 서버 값이 기준).
            if (Time.time > _comboExpireTime) _comboIndex = 0;
            int comboLength = Mathf.Max(1, _profile.ComboMultipliers.Length);
            _combo = _comboIndex % comboLength;
            _comboIndex = (_combo + 1) % comboLength;
            _comboExpireTime = Time.time + _profile.ComboResetTime;

            if (IsLocallyControlled())
            {
                var motor = avatar.GetComponent<CharacterMotor>();
                if (motor != null)
                {
                    motor.FaceDirection(_aim);
                    motor.SetMovementLocked(true);
                    _lockedMotor = motor;
                }
                GameEventBus.RaisePlayerAttacked(avatar, _weapon);
            }

            AbilityTask_PlayMontageAndWait.CreatePlayMontageAndWaitProxy(this, GameConstants.Anim.Attack, GameConstants.Anim.AttackIndex,
                _combo, _profile.MovementLockTime).ReadyForActivation();

            var windup = AbilityTask_WaitDelay.WaitDelay(this, _profile.WindupTime);
            windup.OnFinish += OnWindupFinished;
            windup.ReadyForActivation();

            var unlock = AbilityTask_WaitDelay.WaitDelay(this, _profile.MovementLockTime);
            unlock.OnFinish += ReleaseMovementLock;
            unlock.ReadyForActivation();

            var end = AbilityTask_WaitDelay.WaitDelay(this, Mathf.Max(_profile.WindupTime, _profile.MovementLockTime));
            end.OnFinish += K2_EndAbility;
            end.ReadyForActivation();
        }

        private void OnWindupFinished()
        {
            if (!HasAuthority() || _profile == null) return;
            var avatar = GetAvatarActorFromActorInfo();
            var health = avatar.GetComponent<Health>();
            if (health != null && !health.IsAlive) return;

            float multiplier = _profile.ComboMultipliers.Length > 0
                ? _profile.ComboMultipliers[Mathf.Clamp(_combo, 0, _profile.ComboMultipliers.Length - 1)]
                : 1f;
            var faction = health != null ? health.Faction : Faction.Neutral;
            var asc = GetAbilitySystemComponentFromActorInfo();
            var weapons = avatar.GetComponent<WeaponController>();

            if (_weapon == WeaponType.Bow && _profile.ProjectilePrefab != null)
            {
                Vector3 muzzle = weapons != null ? weapons.GetMuzzlePosition() : avatar.transform.position + Vector3.up * 1.4f;
                var spec = CombatSpecs.MakeDamageSpec(asc, CombatSpecs.MakeHitContext(asc, this, muzzle, _aim),
                    _profile.FlatDamage * multiplier, _profile.AttackPowerScale * multiplier, 0f, _profile.DamageType, 0f, 0f, GetAbilityLevel());
                Projectile.SpawnConfigured(_profile.ProjectilePrefab, muzzle, _aim, faction, spec, null, _profile.ProjectileSpeed);
                return;
            }

            Vector3 origin = weapons != null ? weapons.GetMeleeOrigin() : avatar.transform.position + Vector3.up;
            CombatQuery.OverlapHostileArc(origin, _aim, _profile.Range, _profile.ArcDegrees, faction, HitBuffer, GameConstants.Layers.CharacterMask);
            for (int i = 0; i < HitBuffer.Count; i++)
            {
                var target = HitBuffer[i].Transform;
                Vector3 hitPoint = target.position + Vector3.up;
                Vector3 hitDirection = (target.position - avatar.transform.position).normalized;
                var spec = CombatSpecs.MakeDamageSpec(asc, CombatSpecs.MakeHitContext(asc, this, hitPoint, hitDirection),
                    _profile.FlatDamage * multiplier, _profile.AttackPowerScale * multiplier, 0f, _profile.DamageType,
                    _profile.Knockback, 0f, GetAbilityLevel());
                CombatSpecs.ApplyToTarget(asc, spec, target.gameObject);
            }
        }

        protected override void OnEndAbility(bool wasCancelled) => ReleaseMovementLock();

        private void ReleaseMovementLock()
        {
            if (_lockedMotor == null) return;
            _lockedMotor.SetMovementLocked(false);
            _lockedMotor = null;
        }

        private AttackProfile GetProfile(GameplayAbilityActorInfo actorInfo)
        {
            var weapons = actorInfo?.GetAvatarComponent<WeaponController>();
            var weapon = weapons != null ? weapons.Current : WeaponType.Sword;
            for (int i = 0; i < Profiles.Length; i++)
                if (Profiles[i].Weapon == weapon) return Profiles[i];
            return Profiles.Length > 0 ? Profiles[0] : null;
        }
    }
}
