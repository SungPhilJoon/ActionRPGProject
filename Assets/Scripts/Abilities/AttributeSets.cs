using System;
using ActionRPG.Core;
using ActionRPG.Core.AbilitySystem;
using UnityEngine;

namespace ActionRPG.Abilities
{
    /// <summary>
    /// 체력. 라이라의 ULyraHealthSet 과 같은 구조다.
    ///
    /// 피해는 Health 를 직접 깎지 않고 메타 어트리뷰트 IncomingDamage 에 쌓인다.
    /// PostGameplayEffectExecute 가 그것을 Health 로 옮기면서 무적 · 사망 판정을 한 곳에서 처리한다.
    /// </summary>
    public sealed class HealthSet : AttributeSet
    {
        public readonly GameplayAttributeData Health = new GameplayAttributeData(100f);
        public readonly GameplayAttributeData MaxHealth = new GameplayAttributeData(100f);
        [MetaAttribute] public readonly GameplayAttributeData IncomingDamage = new GameplayAttributeData(0f);
        [MetaAttribute] public readonly GameplayAttributeData IncomingHealing = new GameplayAttributeData(0f);

        public static readonly GameplayAttribute HealthAttribute = GameplayAttribute.Of<HealthSet>(nameof(Health));
        public static readonly GameplayAttribute MaxHealthAttribute = GameplayAttribute.Of<HealthSet>(nameof(MaxHealth));
        public static readonly GameplayAttribute IncomingDamageAttribute = GameplayAttribute.Of<HealthSet>(nameof(IncomingDamage));
        public static readonly GameplayAttribute IncomingHealingAttribute = GameplayAttribute.Of<HealthSet>(nameof(IncomingHealing));

        /// <summary>서버 전용. (피해 정보, 원인 스펙) 실제로 피해가 들어갔을 때.</summary>
        public event Action<DamageInfo, GameplayEffectSpec> OnDamageTaken;
        /// <summary>서버 전용. 체력이 0 이 된 순간 한 번.</summary>
        public event Action<DamageInfo> OnOutOfHealth;

        private bool _outOfHealth;

        public override void PreAttributeChange(GameplayAttribute attribute, ref float newValue)
        {
            if (attribute == MaxHealthAttribute) newValue = Mathf.Max(1f, newValue);
            else if (attribute == HealthAttribute) newValue = Mathf.Clamp(newValue, 0f, MaxHealth.CurrentValue);
        }

        public override void PostAttributeChange(GameplayAttribute attribute, float oldValue, float newValue)
        {
            var asc = OwningAbilitySystemComponent;
            if (asc == null || !asc.IsAuthority) return;

            if (attribute == MaxHealthAttribute && oldValue > 0f)
            {
                // 최대 체력이 바뀌면 현재 체력 비율을 유지한다.
                float ratio = Health.BaseValue / oldValue;
                SetBase(HealthAttribute, Mathf.Clamp(newValue * ratio, 0f, newValue));
            }
            else if (attribute == HealthAttribute && newValue > 0f)
            {
                _outOfHealth = false;
            }
        }

        public override bool PreGameplayEffectExecute(GameplayEffectModCallbackData data)
        {
            if (data.EvaluatedData.Attribute != IncomingDamageAttribute) return true;
            var target = data.Target;
            // 무적이거나 이미 죽었으면 피해를 받지 않는다.
            if (target.HasMatchingGameplayTag(GameTags.State_Invulnerable) || target.HasMatchingGameplayTag(GameTags.State_Dead))
                return false;
            return true;
        }

        public override void PostGameplayEffectExecute(GameplayEffectModCallbackData data)
        {
            var attribute = data.EvaluatedData.Attribute;

            if (attribute == IncomingDamageAttribute)
            {
                float damage = IncomingDamage.BaseValue;
                SetBase(IncomingDamageAttribute, 0f);
                if (damage <= 0f) return;

                float newHealth = Mathf.Max(0f, Health.BaseValue - damage);
                SetBase(HealthAttribute, newHealth);

                var info = BuildDamageInfo(data.EffectSpec, damage);
                OnDamageTaken?.Invoke(info, data.EffectSpec);
                if (newHealth <= 0f && !_outOfHealth)
                {
                    _outOfHealth = true;
                    OnOutOfHealth?.Invoke(info);
                }
            }
            else if (attribute == IncomingHealingAttribute)
            {
                float healing = IncomingHealing.BaseValue;
                SetBase(IncomingHealingAttribute, 0f);
                if (healing <= 0f) return;
                SetBase(HealthAttribute, Mathf.Min(MaxHealth.CurrentValue, Health.BaseValue + healing));
            }
            else if (attribute == HealthAttribute)
            {
                SetBase(HealthAttribute, Mathf.Clamp(Health.BaseValue, 0f, MaxHealth.CurrentValue));
                if (Health.BaseValue <= 0f && !_outOfHealth)
                {
                    _outOfHealth = true;
                    OnOutOfHealth?.Invoke(BuildDamageInfo(data.EffectSpec, 0f));
                }
            }
        }

        /// <summary>서버 전용. 부활 등으로 다시 살아났을 때 사망 플래그를 지운다.</summary>
        public void ResetOutOfHealth() => _outOfHealth = false;

        private static DamageInfo BuildDamageInfo(GameplayEffectSpec spec, float amount)
        {
            var context = spec.Context;
            var tags = new GameplayTagContainer();
            spec.GetAllAssetTags(tags);
            var instigatorDamageable = context.Instigator != null ? context.Instigator.GetComponent<IDamageable>() : null;
            var info = DamageInfo.Create(amount, GameTags.DamageTypeFromTags(tags),
                instigatorDamageable != null ? instigatorDamageable.Faction : Faction.Neutral,
                context.InstigatorNetworkObjectId);
            info.IsCritical = context.IsCriticalHit;
            if (context.HasHitResult)
            {
                info.HitPoint = context.HitLocation;
                info.HitDirection = context.HitNormal;
            }
            info.KnockbackForce = spec.GetSetByCallerMagnitude(GameTags.SetByCaller_Knockback, false);
            info.StaggerDuration = spec.GetSetByCallerMagnitude(GameTags.SetByCaller_Stagger, false);
            return info;
        }
    }

    /// <summary>마나. 스킬 비용(GE_Cost_Mana)이 깎고, 마나 재생 이펙트(GE_ManaRegen)가 채운다.</summary>
    public sealed class ManaSet : AttributeSet
    {
        public readonly GameplayAttributeData Mana = new GameplayAttributeData(100f);
        public readonly GameplayAttributeData MaxMana = new GameplayAttributeData(100f);
        public readonly GameplayAttributeData ManaRegen = new GameplayAttributeData(6f);

        public static readonly GameplayAttribute ManaAttribute = GameplayAttribute.Of<ManaSet>(nameof(Mana));
        public static readonly GameplayAttribute MaxManaAttribute = GameplayAttribute.Of<ManaSet>(nameof(MaxMana));
        public static readonly GameplayAttribute ManaRegenAttribute = GameplayAttribute.Of<ManaSet>(nameof(ManaRegen));

        public override void PreAttributeChange(GameplayAttribute attribute, ref float newValue)
        {
            if (attribute == MaxManaAttribute || attribute == ManaRegenAttribute) newValue = Mathf.Max(0f, newValue);
            else if (attribute == ManaAttribute) newValue = Mathf.Clamp(newValue, 0f, MaxMana.CurrentValue);
        }

        public override void PreAttributeBaseChange(GameplayAttribute attribute, ref float newValue)
        {
            if (attribute == ManaAttribute) newValue = Mathf.Clamp(newValue, 0f, MaxMana.CurrentValue);
        }

        public override void PostAttributeChange(GameplayAttribute attribute, float oldValue, float newValue)
        {
            var asc = OwningAbilitySystemComponent;
            if (asc == null || !asc.IsAuthority) return;
            if (attribute == MaxManaAttribute && Mana.BaseValue > newValue) SetBase(ManaAttribute, newValue);
        }
    }

    /// <summary>전투 능력치. 기존 StatBlock 의 전투 관련 값을 그대로 옮겼다.</summary>
    public sealed class CombatSet : AttributeSet
    {
        public readonly GameplayAttributeData AttackPower = new GameplayAttributeData(12f);
        public readonly GameplayAttributeData AbilityPower = new GameplayAttributeData(10f);
        public readonly GameplayAttributeData Defense = new GameplayAttributeData(5f);
        public readonly GameplayAttributeData MoveSpeed = new GameplayAttributeData(5.2f);
        public readonly GameplayAttributeData AttackSpeed = new GameplayAttributeData(1f);
        public readonly GameplayAttributeData CritChance = new GameplayAttributeData(0.1f);
        public readonly GameplayAttributeData CritDamage = new GameplayAttributeData(1.6f);
        public readonly GameplayAttributeData CooldownReduction = new GameplayAttributeData(0f);

        public static readonly GameplayAttribute AttackPowerAttribute = GameplayAttribute.Of<CombatSet>(nameof(AttackPower));
        public static readonly GameplayAttribute AbilityPowerAttribute = GameplayAttribute.Of<CombatSet>(nameof(AbilityPower));
        public static readonly GameplayAttribute DefenseAttribute = GameplayAttribute.Of<CombatSet>(nameof(Defense));
        public static readonly GameplayAttribute MoveSpeedAttribute = GameplayAttribute.Of<CombatSet>(nameof(MoveSpeed));
        public static readonly GameplayAttribute AttackSpeedAttribute = GameplayAttribute.Of<CombatSet>(nameof(AttackSpeed));
        public static readonly GameplayAttribute CritChanceAttribute = GameplayAttribute.Of<CombatSet>(nameof(CritChance));
        public static readonly GameplayAttribute CritDamageAttribute = GameplayAttribute.Of<CombatSet>(nameof(CritDamage));
        public static readonly GameplayAttribute CooldownReductionAttribute = GameplayAttribute.Of<CombatSet>(nameof(CooldownReduction));

        public override void PreAttributeChange(GameplayAttribute attribute, ref float newValue)
        {
            if (attribute == MoveSpeedAttribute) newValue = Mathf.Clamp(newValue, 0f, 30f);
            else if (attribute == AttackSpeedAttribute) newValue = Mathf.Clamp(newValue, 0.2f, 3f);
            else if (attribute == CritChanceAttribute) newValue = Mathf.Clamp01(newValue);
            else if (attribute == CritDamageAttribute) newValue = Mathf.Max(1f, newValue);
            else if (attribute == CooldownReductionAttribute) newValue = Mathf.Clamp(newValue, 0f, 0.8f);
            else newValue = Mathf.Max(0f, newValue);
        }
    }

    /// <summary>
    /// StatBlock(기존 스탯 데이터)을 어트리뷰트 BaseValue 로 옮긴다. 서버 전용.
    /// 적은 정의마다 스탯이 달라 이펙트 에셋 대신 이 함수로 초기화한다 (언리얼 SetNumericAttributeBase 경로).
    /// </summary>
    public static class AttributeInitializer
    {
        public static void ApplyStatBlock(AbilitySystemComponent asc, in StatBlock stats, bool refillVitals = true)
        {
            if (asc == null || !asc.IsAuthority) return;
            var clamped = stats;
            clamped.Clamp();

            if (asc.HasAttributeSetForAttribute(HealthSet.MaxHealthAttribute))
            {
                asc.SetNumericAttributeBase(HealthSet.MaxHealthAttribute, clamped.MaxHealth);
                if (refillVitals) asc.SetNumericAttributeBase(HealthSet.HealthAttribute, clamped.MaxHealth);
            }
            if (asc.HasAttributeSetForAttribute(ManaSet.MaxManaAttribute))
            {
                asc.SetNumericAttributeBase(ManaSet.MaxManaAttribute, clamped.MaxMana);
                asc.SetNumericAttributeBase(ManaSet.ManaRegenAttribute, clamped.ManaRegen);
                if (refillVitals) asc.SetNumericAttributeBase(ManaSet.ManaAttribute, clamped.MaxMana);
            }
            if (asc.HasAttributeSetForAttribute(CombatSet.AttackPowerAttribute))
            {
                asc.SetNumericAttributeBase(CombatSet.AttackPowerAttribute, clamped.AttackPower);
                asc.SetNumericAttributeBase(CombatSet.AbilityPowerAttribute, clamped.AbilityPower);
                asc.SetNumericAttributeBase(CombatSet.DefenseAttribute, clamped.Defense);
                asc.SetNumericAttributeBase(CombatSet.MoveSpeedAttribute, clamped.MoveSpeed);
                asc.SetNumericAttributeBase(CombatSet.AttackSpeedAttribute, clamped.AttackSpeed);
                asc.SetNumericAttributeBase(CombatSet.CritChanceAttribute, clamped.CritChance);
                asc.SetNumericAttributeBase(CombatSet.CritDamageAttribute, clamped.CritDamage);
                asc.SetNumericAttributeBase(CombatSet.CooldownReductionAttribute, clamped.CooldownReduction);
            }
        }

        /// <summary>현재 어트리뷰트 값을 StatBlock 으로 읽는다 (디버그 · UI 용).</summary>
        public static StatBlock ReadStatBlock(AbilitySystemComponent asc)
        {
            return new StatBlock
            {
                MaxHealth = asc.GetNumericAttribute(HealthSet.MaxHealthAttribute),
                MaxMana = asc.GetNumericAttribute(ManaSet.MaxManaAttribute),
                ManaRegen = asc.GetNumericAttribute(ManaSet.ManaRegenAttribute),
                AttackPower = asc.GetNumericAttribute(CombatSet.AttackPowerAttribute),
                AbilityPower = asc.GetNumericAttribute(CombatSet.AbilityPowerAttribute),
                Defense = asc.GetNumericAttribute(CombatSet.DefenseAttribute),
                MoveSpeed = asc.GetNumericAttribute(CombatSet.MoveSpeedAttribute),
                AttackSpeed = asc.GetNumericAttribute(CombatSet.AttackSpeedAttribute),
                CritChance = asc.GetNumericAttribute(CombatSet.CritChanceAttribute),
                CritDamage = asc.GetNumericAttribute(CombatSet.CritDamageAttribute),
                CooldownReduction = asc.GetNumericAttribute(CombatSet.CooldownReductionAttribute)
            };
        }
    }
}
