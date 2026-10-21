using System.Collections.Generic;
using Framework;
using Framework.Ability;

/// <summary>
/// EffectTable 행 → GameplayEffectDef 변환 캐시 (Flyweight 패턴).
/// 같은 이펙트 정의는 한 번만 만들고 모든 스펙이 공유한다.
/// </summary>
public static class EffectLibrary
{
    /// <summary>EffectTable 예약 인덱스. 범용 피해/회복/쿨다운 이펙트.</summary>
    public const int DamageEffectId = 1;
    public const int HealEffectId = 2;
    public const int CooldownEffectId = 3;
    public const int CostEffectId = 4;
    public const int SunBurnEffectId = 5;
    public const int BloodStarveEffectId = 6;
    public const int FeedStunEffectId = 7;
    public const int RespawnShieldEffectId = 8;

    private static readonly Dictionary<int, GameplayEffectDef> Cache = new Dictionary<int, GameplayEffectDef>();

    public static void Clear()
    {
        Cache.Clear();
    }

    public static GameplayEffectDef Get(int InEffectId)
    {
        if (InEffectId == 0)
        {
            return null;
        }

        GameplayEffectDef def;
        if (Cache.TryGetValue(InEffectId, out def) == true)
        {
            return def;
        }

        EffectRow row;
        if (Tables.Root.Effect.TryGet(InEffectId, out row) == false)
        {
            Log.Error($"EffectLibrary: effect {InEffectId} not found");
            return null;
        }

        def = build(row);
        Cache.Add(InEffectId, def);
        return def;
    }

    /// <summary>
    /// 이펙트 스펙 생성. EffectTable 의 Coefficient 가 있으면 피해 계수/타입을 SetByCaller 로 싣는다
    /// → 도트(Ignite)나 폭발처럼 Execution=Damage 인 이펙트도 시전자 공격력 스냅샷으로 피해를 계산한다.
    /// </summary>
    public static GameplayEffectSpec MakeSpec(int InEffectId, AbilitySystemComponent InSource, int InAbilityId)
    {
        var def = Get(InEffectId);
        if (def == null)
        {
            return null;
        }

        var spec = new GameplayEffectSpec(def, InSource, InAbilityId);
        EffectRow row;
        if (Tables.Root.Effect.TryGet(InEffectId, out row) == true && row.Coefficient > 0.0f)
        {
            spec.SetByCaller((int)ESetByCaller.Coefficient, row.Coefficient);
            spec.SetByCaller((int)ESetByCaller.DamageType, (float)row.DamageType);
        }

        return spec;
    }

    /// <summary>어빌리티 행의 피해 계수/속성을 실은 범용 피해 스펙.</summary>
    public static GameplayEffectSpec MakeDamageSpec(AbilitySystemComponent InSource, AbilityRow InAbility, float InDamageScale)
    {
        var spec = new GameplayEffectSpec(Get(DamageEffectId), InSource, InAbility.Index);
        spec.SetByCaller((int)ESetByCaller.Coefficient, InAbility.Damage * InDamageScale);
        spec.SetByCaller((int)ESetByCaller.DamageType, (float)InAbility.DamageType);
        return spec;
    }

    private static GameplayEffectDef build(EffectRow InRow)
    {
        var def = new GameplayEffectDef();
        def.Id = InRow.Index;
        def.DurationPolicy = InRow.DurationPolicy;
        def.Duration = InRow.Duration;
        def.Period = InRow.Period;
        def.ExecutePeriodicOnApply = InRow.ExecuteOnApply;
        def.ExecutionId = (int)InRow.Execution;
        def.GrantedTags = TagUtil.ToContainer(InRow.GrantedTags);
        def.ApplicationRequiredTags = TagUtil.ToContainer(InRow.RequiredTags);
        def.ApplicationBlockedTags = TagUtil.ToContainer(InRow.BlockedTags);
        def.RemoveEffectsWithTags = TagUtil.ToContainer(InRow.RemoveTags);
        def.StackPolicy = InRow.Stack;
        def.MaxStack = InRow.MaxStack < 1 ? 1 : InRow.MaxStack;
        def.CueId = InRow.Cue;
        def.IsCooldown = InRow.IsCooldown;

        var modifiers = new List<ModifierDef>(3);
        addModifier(modifiers, InRow.Mod1Attr, InRow.Mod1Op, InRow.Mod1Type, InRow.Mod1Key, InRow.Mod1Value);
        addModifier(modifiers, InRow.Mod2Attr, InRow.Mod2Op, InRow.Mod2Type, InRow.Mod2Key, InRow.Mod2Value);
        addModifier(modifiers, InRow.Mod3Attr, InRow.Mod3Op, InRow.Mod3Type, InRow.Mod3Key, InRow.Mod3Value);
        def.Modifiers = modifiers.ToArray();
        return def;
    }

    private static void addModifier(List<ModifierDef> InList, EAttribute InAttribute, EModifierOp InOp, EMagnitudeType InType, int InKey, float InValue)
    {
        bool empty = InValue == 0.0f && InType != EMagnitudeType.SetByCaller;
        if (empty == true)
        {
            return;
        }

        var modifier = new ModifierDef();
        modifier.Attribute = (int)InAttribute;
        modifier.Op = InOp;
        modifier.MagnitudeType = InType;
        modifier.Key = InKey;
        modifier.Value = InValue;
        InList.Add(modifier);
    }
}

public static class TagUtil
{
    /// <summary>CSV 의 ETag 배열 → 태그 컨테이너 (명시 태그만 넣는다. 부모 매칭은 컨테이너가 비교 때 처리).</summary>
    public static GameplayTagContainer ToContainer(ETag[] InTags)
    {
        var container = new GameplayTagContainer();
        if (InTags == null)
        {
            return container;
        }

        for (int i = 0; i < InTags.Length; ++i)
        {
            if (InTags[i] != ETag.None)
            {
                container.Add((int)InTags[i]);
            }
        }

        return container;
    }

    public static GameplayTagContainer ToContainer(ETag InTag)
    {
        var container = new GameplayTagContainer();
        if (InTag != ETag.None)
        {
            container.Add((int)InTag);
        }

        return container;
    }

    public static bool Has(AbilitySystemComponent InAsc, ETag InTag)
    {
        return InAsc != null && InAsc.Tags.HasTag((int)InTag) == true;
    }
}
