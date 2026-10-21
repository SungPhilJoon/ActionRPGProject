using Framework;
using Framework.Ability;
using UnityEngine;

/// <summary>
/// 게임 속성 세트. 클램프와 메타 속성(IncomingDamage / IncomingHeal) 처리를 담당한다.
/// 피해는 Execution 이 IncomingDamage 에 쌓고, 여기서 Shield → Health 순으로 분배한 뒤 0 으로 되돌린다.
/// (언리얼 Lyra 의 HealthSet 과 같은 구조)
/// </summary>
public class GameAttributeSet : AttributeSet
{
    public Actor Actor;

    public GameAttributeSet() : base((int)EAttribute.Count)
    {
    }

    public float Get(EAttribute InAttribute)
    {
        return GetCurrent((int)InAttribute);
    }

    public void Set(EAttribute InAttribute, float InValue)
    {
        SetBase((int)InAttribute, InValue);
    }

    protected override void PreAttributeChange(int InAttribute, ref float InOutNewValue)
    {
        switch ((EAttribute)InAttribute)
        {
            case EAttribute.Health:
                InOutNewValue = Mathf.Clamp(InOutNewValue, 0.0f, GetCurrent((int)EAttribute.MaxHealth));
                break;
            case EAttribute.BloodPool:
                InOutNewValue = Mathf.Clamp(InOutNewValue, 0.0f, GetCurrent((int)EAttribute.MaxBloodPool));
                break;
            case EAttribute.Shield:
            case EAttribute.MaxHealth:
                InOutNewValue = Mathf.Max(0.0f, InOutNewValue);
                break;
            case EAttribute.MoveSpeed:
                InOutNewValue = Mathf.Max(0.5f, InOutNewValue);
                break;
            case EAttribute.PhysicalResist:
            case EAttribute.SpellResist:
            case EAttribute.SunResist:
                InOutNewValue = Mathf.Clamp(InOutNewValue, -100.0f, 90.0f);
                break;
        }
    }

    protected override void PostAttributeChange(int InAttribute, float InOldValue, float InNewValue)
    {
        // MaxHealth 가 줄면 Health 도 다시 클램프한다.
        if (InAttribute == (int)EAttribute.MaxHealth)
        {
            Recalculate((int)EAttribute.Health);
        }
        else if (InAttribute == (int)EAttribute.MaxBloodPool)
        {
            Recalculate((int)EAttribute.BloodPool);
        }
    }

    public override void PostGameplayEffectExecute(EffectExecuteData InData)
    {
        int attribute = InData.Evaluated.Attribute;
        if (attribute == (int)EAttribute.IncomingDamage)
        {
            handleDamage(InData);
        }
        else if (attribute == (int)EAttribute.IncomingHeal)
        {
            handleHeal(InData);
        }
    }

    private void handleDamage(EffectExecuteData InData)
    {
        float damage = GetBase((int)EAttribute.IncomingDamage);
        SetBaseRaw((int)EAttribute.IncomingDamage, 0.0f);
        if (damage <= 0.0f || Actor == null || Actor.IsDead == true)
        {
            return;
        }

        var spec = InData.Spec;
        Actor source = spec.Source != null ? spec.Source.GetComponent<Actor>() : null;
        var damageType = (EDamageType)Mathf.RoundToInt(spec.GetSetByCaller((int)ESetByCaller.DamageType));
        damage = applyStatusModifiers(damage, source, damageType);

        var tags = Owner.Tags;
        bool immune = tags.HasTag((int)ETag.State_Invulnerable) == true || tags.HasTag((int)ETag.Cheat_God) == true;
        if (immune == true)
        {
            damage = 0.0f;
        }

        float shield = GetBase((int)EAttribute.Shield);
        float absorbed = Mathf.Min(shield, damage);
        if (absorbed > 0.0f)
        {
            SetBaseRaw((int)EAttribute.Shield, shield - absorbed);
        }

        float final = damage - absorbed;
        float health = GetBase((int)EAttribute.Health);
        SetBaseRaw((int)EAttribute.Health, health - final);

        bool isCritical = spec.GetSetByCaller((int)ESetByCaller.Critical) > 0.5f;

        var record = new CombatRecord();
        record.Time = Time.time;
        record.SourceId = source != null ? source.ActorId : 0;
        record.SourceCharacter = source != null ? source.CharacterId : 0;
        record.TargetId = Actor.ActorId;
        record.TargetCharacter = Actor.CharacterId;
        record.AbilityId = spec.AbilityId;
        record.Raw = spec.GetSetByCaller((int)ESetByCaller.RawDamage, damage);
        record.Final = final;
        record.Absorbed = absorbed;
        record.IsCritical = isCritical;
        record.DamageType = damageType;
        CombatLog.Add(record);

        var evt = new DamageEvent();
        evt.Source = source;
        evt.Target = Actor;
        evt.AbilityId = spec.AbilityId;
        evt.Amount = final;
        evt.Absorbed = absorbed;
        evt.IsCritical = isCritical;
        evt.DamageType = damageType;
        evt.Position = Actor.transform.position + Vector3.up * 2.0f;
        EventBus<DamageEvent>.Publish(evt);

        Owner.SendGameplayEvent((int)ETag.Event_Damaged, new GameplayCueParams { Source = spec.Source, Target = Owner, Magnitude = final });

        // 흡혈: 공격자의 LifeLeech% + (대상이 Leech 상태이고 물리 피해면) LeechHealPercent% 만큼 공격자 회복.
        if (source != null && source.IsDead == false && final > 0.0f)
        {
            float leech = source.Attributes.Get(EAttribute.LifeLeech);
            if (damageType == EDamageType.Physical && tags.HasTag((int)ETag.State_Debuff_Leech) == true)
            {
                leech += Tables.Const(EConst.LeechHealPercent);
            }

            if (leech > 0.0f)
            {
                source.HealInstant(MathUtil.Percent(final, leech));
            }
        }

        // Freeze: 체력이 깎이면 얼음이 깨진다 (보호막 흡수만으로는 유지).
        if (final > 0.0f && tags.HasTag((int)ETag.State_Debuff_Stun_Frozen) == true)
        {
            Owner.RemoveEffectsWithGrantedTags(TagUtil.ToContainer(ETag.State_Debuff_Stun_Frozen));
        }

        if (GetBase((int)EAttribute.Health) <= 0.0f)
        {
            Actor.Die(source);
        }
    }

    /// <summary>
    /// V Rising 상태이상의 피해 배율.
    ///  - Condemn (대상): 받는 피해 +CondemnDamageTakenPercent%
    ///  - Weaken (공격자): 주는 피해 −WeakenDamageDealtPercent%
    ///  - Static (대상): 물리 피해일 때 +StaticBonusPercent% 추가 (감전)
    /// 햇빛·고정 피해(Sun/True)는 상태이상 영향을 받지 않는다.
    /// </summary>
    private float applyStatusModifiers(float InDamage, Actor InSource, EDamageType InType)
    {
        if (InType == EDamageType.Sun || InType == EDamageType.True)
        {
            return InDamage;
        }

        float scale = 1.0f;
        var tags = Owner.Tags;
        if (tags.HasTag((int)ETag.State_Debuff_Condemn) == true)
        {
            scale += Tables.Const(EConst.CondemnDamageTakenPercent) * 0.01f;
        }

        if (InType == EDamageType.Physical && tags.HasTag((int)ETag.State_Debuff_Static) == true)
        {
            scale += Tables.Const(EConst.StaticBonusPercent) * 0.01f;
        }

        if (InSource != null && InSource.Asc.Tags.HasTag((int)ETag.State_Debuff_Weaken) == true)
        {
            scale -= Tables.Const(EConst.WeakenDamageDealtPercent) * 0.01f;
        }

        return InDamage * Mathf.Max(0.0f, scale);
    }

    private void handleHeal(EffectExecuteData InData)
    {
        float heal = GetBase((int)EAttribute.IncomingHeal);
        SetBaseRaw((int)EAttribute.IncomingHeal, 0.0f);
        if (heal <= 0.0f || Actor == null || Actor.IsDead == true)
        {
            return;
        }

        float before = GetCurrent((int)EAttribute.Health);
        SetBaseRaw((int)EAttribute.Health, GetBase((int)EAttribute.Health) + heal);
        float applied = GetCurrent((int)EAttribute.Health) - before;
        if (applied <= 0.5f)
        {
            return;
        }

        var record = new CombatRecord();
        record.Time = Time.time;
        record.TargetId = Actor.ActorId;
        record.TargetCharacter = Actor.CharacterId;
        record.AbilityId = InData.Spec.AbilityId;
        record.Raw = heal;
        record.Final = applied;
        record.IsHeal = true;
        CombatLog.Add(record);

        var evt = new HealEvent();
        evt.Target = Actor;
        evt.Amount = applied;
        evt.Position = Actor.transform.position + Vector3.up * 2.0f;
        EventBus<HealEvent>.Publish(evt);
    }
}
