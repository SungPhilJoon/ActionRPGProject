using System.Collections.Generic;
using Framework;
using Framework.Ability;
using UnityEngine;

/// <summary>
/// 피해 계산 (언리얼 DamageExecutionCalculation).
///
///   기본 = Flat(SetByCaller.Damage) + Power × Coefficient%      (Power: 물리/주문 공격력, 스펙 생성 시점 스냅샷)
///   레벨 = clamp(1 + (공격자 Level − 대상 Level) × LevelDamageScalePerLevel, Min, Max)    ← V Rising 의 기어 레벨 차이 보정
///   치명 = CritChance% 확률로 × (CritBaseMultiplier + CritDamage%)
///   저항 = × (1 − Resist%)
///   결과 → IncomingDamage (메타 속성) 으로 출력, Shield/Health 분배는 GameAttributeSet 이 담당.
/// </summary>
public class DamageExecution : IGameplayEffectExecution
{
    public void Execute(GameplayEffectSpec InSpec, AbilitySystemComponent InTarget, List<EvaluatedModifier> OutModifiers)
    {
        var damageType = (EDamageType)Mathf.RoundToInt(InSpec.GetSetByCaller((int)ESetByCaller.DamageType));
        float coefficient = InSpec.GetSetByCaller((int)ESetByCaller.Coefficient);
        float flat = InSpec.GetSetByCaller((int)ESetByCaller.Damage);

        float power = damageType == EDamageType.Spell
            ? InSpec.GetSourceAttribute((int)EAttribute.SpellPower)
            : InSpec.GetSourceAttribute((int)EAttribute.PhysicalPower);

        float raw = flat + MathUtil.Percent(power, coefficient);
        raw *= LevelScale(InSpec.GetSourceAttribute((int)EAttribute.Level), InTarget.Attributes.GetCurrent((int)EAttribute.Level));

        bool isCritical = false;
        float critChance = InSpec.GetSourceAttribute((int)EAttribute.CritChance);
        if (critChance > 0.0f && Random.value * 100.0f < critChance)
        {
            isCritical = true;
            raw *= Tables.Const(EConst.CritBaseMultiplier) + InSpec.GetSourceAttribute((int)EAttribute.CritDamage) * 0.01f;
        }

        float resist = 0.0f;
        if (damageType == EDamageType.Physical)
        {
            resist = InTarget.Attributes.GetCurrent((int)EAttribute.PhysicalResist);
        }
        else if (damageType == EDamageType.Spell)
        {
            resist = InTarget.Attributes.GetCurrent((int)EAttribute.SpellResist);
        }

        float final = raw * (1.0f - resist * 0.01f);
        InSpec.SetByCaller((int)ESetByCaller.Critical, isCritical == true ? 1.0f : 0.0f);
        InSpec.SetByCaller((int)ESetByCaller.RawDamage, raw);

        var output = new EvaluatedModifier();
        output.Attribute = (int)EAttribute.IncomingDamage;
        output.Op = EModifierOp.Add;
        output.Magnitude = Mathf.Max(0.0f, final);
        OutModifiers.Add(output);
    }

    public static float LevelScale(float InSourceLevel, float InTargetLevel)
    {
        float perLevel = Tables.Const(EConst.LevelDamageScalePerLevel);
        float scale = 1.0f + (InSourceLevel - InTargetLevel) * perLevel;
        return Mathf.Clamp(scale, Tables.Const(EConst.LevelDamageScaleMin), Tables.Const(EConst.LevelDamageScaleMax));
    }
}

/// <summary>회복 = Flat(SetByCaller.Heal) + SpellPower × Coefficient%.</summary>
public class HealExecution : IGameplayEffectExecution
{
    public void Execute(GameplayEffectSpec InSpec, AbilitySystemComponent InTarget, List<EvaluatedModifier> OutModifiers)
    {
        float flat = InSpec.GetSetByCaller((int)ESetByCaller.Heal);
        float coefficient = InSpec.GetSetByCaller((int)ESetByCaller.Coefficient);
        float heal = flat + MathUtil.Percent(InSpec.GetSourceAttribute((int)EAttribute.SpellPower), coefficient);

        var output = new EvaluatedModifier();
        output.Attribute = (int)EAttribute.IncomingHeal;
        output.Op = EModifierOp.Add;
        output.Magnitude = heal;
        OutModifiers.Add(output);
    }
}

/// <summary>최대 체력 비율 회복 (SetByCaller.Magnitude %).</summary>
public class PercentHealExecution : IGameplayEffectExecution
{
    public void Execute(GameplayEffectSpec InSpec, AbilitySystemComponent InTarget, List<EvaluatedModifier> OutModifiers)
    {
        float percent = InSpec.GetSetByCaller((int)ESetByCaller.Magnitude);
        var output = new EvaluatedModifier();
        output.Attribute = (int)EAttribute.IncomingHeal;
        output.Op = EModifierOp.Add;
        output.Magnitude = MathUtil.Percent(InTarget.Attributes.GetCurrent((int)EAttribute.MaxHealth), percent);
        OutModifiers.Add(output);
    }
}

/// <summary>
/// 햇빛 피해 = MaxHealth × SunDamagePercentPerSec × Period × 램프(노출 시간 비율 0~1, SetByCaller.Magnitude) × (1 − SunResist%).
/// 고정 피해(저항/레벨 무시)이며 Shield 는 그대로 흡수한다.
/// </summary>
public class SunDamageExecution : IGameplayEffectExecution
{
    public void Execute(GameplayEffectSpec InSpec, AbilitySystemComponent InTarget, List<EvaluatedModifier> OutModifiers)
    {
        var attributes = InTarget.Attributes;
        float ramp = Mathf.Clamp01(InSpec.GetSetByCaller((int)ESetByCaller.Magnitude, 1.0f));
        float percentPerSec = Tables.Const(EConst.SunDamagePercentPerSec);
        float period = InSpec.Def.Period > 0.0f ? InSpec.Def.Period : 1.0f;
        float resist = attributes.GetCurrent((int)EAttribute.SunResist);
        float damage = MathUtil.Percent(attributes.GetCurrent((int)EAttribute.MaxHealth), percentPerSec) * period * ramp * (1.0f - resist * 0.01f);

        InSpec.SetByCaller((int)ESetByCaller.DamageType, (float)EDamageType.Sun);
        InSpec.SetByCaller((int)ESetByCaller.RawDamage, damage);

        var output = new EvaluatedModifier();
        output.Attribute = (int)EAttribute.IncomingDamage;
        output.Op = EModifierOp.Add;
        output.Magnitude = Mathf.Max(0.0f, damage);
        OutModifiers.Add(output);
    }
}

/// <summary>최대 체력 비율 고정 피해 (혈액 고갈 등): MaxHealth × Magnitude% × Period.</summary>
public class PercentDamageExecution : IGameplayEffectExecution
{
    public void Execute(GameplayEffectSpec InSpec, AbilitySystemComponent InTarget, List<EvaluatedModifier> OutModifiers)
    {
        float percent = InSpec.GetSetByCaller((int)ESetByCaller.Magnitude);
        float period = InSpec.Def.Period > 0.0f ? InSpec.Def.Period : 1.0f;
        float damage = MathUtil.Percent(InTarget.Attributes.GetCurrent((int)EAttribute.MaxHealth), percent) * period;
        InSpec.SetByCaller((int)ESetByCaller.DamageType, (float)EDamageType.True);
        InSpec.SetByCaller((int)ESetByCaller.RawDamage, damage);

        var output = new EvaluatedModifier();
        output.Attribute = (int)EAttribute.IncomingDamage;
        output.Op = EModifierOp.Add;
        output.Magnitude = damage;
        OutModifiers.Add(output);
    }
}

public static class GameExecutions
{
    public static void RegisterAll()
    {
        ExecutionRegistry.Clear();
        ExecutionRegistry.Register((int)EExecution.Damage, new DamageExecution());
        ExecutionRegistry.Register((int)EExecution.Heal, new HealExecution());
        ExecutionRegistry.Register((int)EExecution.SunDamage, new SunDamageExecution());
        ExecutionRegistry.Register((int)EExecution.PercentHeal, new PercentHealExecution());
        ExecutionRegistry.Register((int)EExecution.PercentDamage, new PercentDamageExecution());
    }
}
