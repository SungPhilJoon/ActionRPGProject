using ActionRPG.Core;
using ActionRPG.Core.AbilitySystem;
using UnityEngine;

namespace ActionRPG.Abilities
{
    /// <summary>
    /// 게임 전역에서 공유하는 이펙트 모음 (라이라 ULyraGameData 에 해당). Resources/ActionRPGGameData.asset.
    /// 스킬 · 평타 · 적 공격이 같은 피해 / 회복 / 비용 / 쿨다운 이펙트를 SetByCaller 값만 바꿔 재사용한다.
    /// </summary>
    [CreateAssetMenu(menuName = "ActionRPG/Abilities/Game Data", fileName = "ActionRPGGameData")]
    public class ActionRPGGameData : ScriptableObject
    {
        public const string ResourcePath = "ActionRPGGameData";

        [Tooltip("Instant. DamageExecution 으로 피해를 계산한다. SetByCaller: Damage, AttackPowerScale, AbilityPowerScale, Knockback.")]
        public GameplayEffect DamageGameplayEffect_SetByCaller;
        [Tooltip("Instant. HealExecution 으로 회복량을 계산한다. SetByCaller: Heal, AbilityPowerScale, HealMaxHealthPercent.")]
        public GameplayEffect HealGameplayEffect_SetByCaller;
        [Tooltip("Instant. Mana 에 SetByCaller.ManaCost 를 더한다 (음수).")]
        public GameplayEffect ManaCostGameplayEffect;
        [Tooltip("HasDuration. 지속시간 = MMC_Cooldown(SetByCaller.Cooldown × 쿨다운 감소). 스킬별 태그는 동적으로 붙는다.")]
        public GameplayEffect SkillCooldownGameplayEffect;
        [Tooltip("HasDuration. Cooldown.Global 태그를 잠깐 부여한다.")]
        public GameplayEffect GlobalCooldownGameplayEffect;
        [Tooltip("HasDuration. 지속시간 = SetByCaller.Duration. State.Invulnerable 을 부여한다.")]
        public GameplayEffect InvulnerableGameplayEffect;

        private static ActionRPGGameData _instance;

        public static ActionRPGGameData Get()
        {
            if (_instance != null) return _instance;
            _instance = Resources.Load<ActionRPGGameData>(ResourcePath);
            if (_instance == null)
                Debug.LogError($"[ActionRPG] Resources/{ResourcePath}.asset 이 없습니다. ActionRPG > 샘플 콘텐츠 생성 메뉴로 만드세요.");
            return _instance;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => _instance = null;
    }

    /// <summary>자주 쓰는 스펙을 만드는 헬퍼. 모든 피해 · 회복은 이 함수를 거쳐 GAS 로 들어간다.</summary>
    public static class CombatSpecs
    {
        /// <summary>
        /// 피해 스펙. 공격자 스탯은 스펙을 만드는 순간 스냅샷된다(발사체가 날아가는 동안 버프가 끝나도 유지).
        /// </summary>
        public static GameplayEffectSpec MakeDamageSpec(AbilitySystemComponent source, GameplayEffectContext context,
            float flatDamage, float attackPowerScale, float abilityPowerScale, DamageType damageType,
            float knockback = 0f, float stagger = 0f, float level = 1f)
        {
            var data = ActionRPGGameData.Get();
            if (source == null || data == null || data.DamageGameplayEffect_SetByCaller == null) return null;
            var spec = source.MakeOutgoingSpec(data.DamageGameplayEffect_SetByCaller, level, context ?? source.MakeEffectContext());
            spec.SetSetByCallerMagnitude(GameTags.SetByCaller_Damage, flatDamage);
            spec.SetSetByCallerMagnitude(GameTags.SetByCaller_AttackPowerScale, attackPowerScale);
            spec.SetSetByCallerMagnitude(GameTags.SetByCaller_AbilityPowerScale, abilityPowerScale);
            spec.SetSetByCallerMagnitude(GameTags.SetByCaller_Knockback, knockback);
            spec.SetSetByCallerMagnitude(GameTags.SetByCaller_Stagger, stagger);
            spec.DynamicAssetTags.AddTag(GameTags.DamageTypeTag(damageType));
            return spec;
        }

        public static GameplayEffectSpec MakeHealSpec(AbilitySystemComponent source, GameplayEffectContext context,
            float flatHeal, float abilityPowerScale, float maxHealthPercent, float level = 1f)
        {
            var data = ActionRPGGameData.Get();
            if (source == null || data == null || data.HealGameplayEffect_SetByCaller == null) return null;
            var spec = source.MakeOutgoingSpec(data.HealGameplayEffect_SetByCaller, level, context ?? source.MakeEffectContext());
            spec.SetSetByCallerMagnitude(GameTags.SetByCaller_Heal, flatHeal);
            spec.SetSetByCallerMagnitude(GameTags.SetByCaller_AbilityPowerScale, abilityPowerScale);
            spec.SetSetByCallerMagnitude(GameTags.SetByCaller_HealMaxHealthPercent, maxHealthPercent);
            return spec;
        }

        /// <summary>맞은 위치 · 방향을 담은 컨텍스트.</summary>
        public static GameplayEffectContext MakeHitContext(AbilitySystemComponent source, GameplayAbility ability, Vector3 hitPoint, Vector3 hitDirection)
        {
            var context = source.MakeEffectContext();
            if (ability != null) context.SetAbility(ability.AssetDefinition, ability.GetAbilityLevel());
            context.AddHitResult(hitPoint, hitDirection);
            return context;
        }

        /// <summary>대상에게 스펙을 적용한다. 서버 전용. 대상 ASC 가 없으면 무시한다.</summary>
        public static void ApplyToTarget(AbilitySystemComponent source, GameplayEffectSpec spec, GameObject target)
        {
            if (source == null || spec == null || target == null || !source.IsAuthority) return;
            var targetAsc = AbilitySystemStatics.GetAbilitySystemComponent(target);
            if (targetAsc != null) source.ApplyGameplayEffectSpecToTarget(spec, targetAsc);
        }

        /// <summary>상태이상 이펙트(에셋)를 대상에게 건다. 서버 전용.</summary>
        public static void ApplyEffectToTarget(AbilitySystemComponent source, GameplayEffect effect, GameObject target, float level = 1f)
        {
            if (effect == null) return;
            ApplyToTarget(source, source.MakeOutgoingSpec(effect, level, source.MakeEffectContext()), target);
        }
    }
}
