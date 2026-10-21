using Framework;
using Framework.Ability;
using UnityEngine;

/// <summary>
/// 게임 어빌리티 공통 부모. AbilityTable 행으로 태그/쿨다운을 설정하고
/// 조준 회전, 애니메이션, 이동 잠금 같은 공통 동작을 제공한다.
/// </summary>
public abstract class GameAbility : GameplayAbility
{
    public AbilityRow Row { get; private set; }
    public Actor Actor { get; private set; }

    public void Setup(AbilityRow InRow, Actor InActor)
    {
        Row = InRow;
        Actor = InActor;
        AbilityId = InRow.Index;
        AbilityTags = TagUtil.ToContainer(InRow.AbilityTags);

        ActivationBlockedTags.Add((int)ETag.State_Dead);
        ActivationBlockedTags.Add((int)ETag.State_Debuff_Stun);
        ActivationBlockedTags.Add((int)ETag.State_Feeding);

        bool isSpell = InRow.Slot == EAbilitySlot.Spell1 || InRow.Slot == EAbilitySlot.Spell2 || InRow.Slot == EAbilitySlot.Ultimate;
        if (isSpell == true)
        {
            ActivationBlockedTags.Add((int)ETag.State_Debuff_Silence);
        }

        bool isTravel = InRow.Slot == EAbilitySlot.Travel;
        if (isTravel == true)
        {
            // 이동기는 다른 시전을 끊고 즉시 나간다 (V Rising 의 회피 캔슬).
            ActivationOwnedTags.Add((int)ETag.State_Dashing);
            CancelAbilitiesWithTags.Add((int)ETag.Ability);
        }
        else
        {
            ActivationOwnedTags.Add((int)ETag.State_Attacking);
            // 공격 중에는 다른 공격이 겹쳐 나가지 않는다. 막힌 입력은 플레이어의 AbilityInputBuffer 가 쌓아 두었다가 끝나면 순서대로 실행.
            // (회피 Ability_Travel 은 막지 않는다 → 언제든 끊고 나갈 수 있음)
            BlockAbilitiesWithTags.Add((int)ETag.Ability_Primary);
            BlockAbilitiesWithTags.Add((int)ETag.Ability_Weapon);
            BlockAbilitiesWithTags.Add((int)ETag.Ability_Spell);
            BlockAbilitiesWithTags.Add((int)ETag.Ability_Ultimate);
            BlockAbilitiesWithTags.Add((int)ETag.Ability_Feed);
            BlockAbilitiesWithTags.Add((int)ETag.Ability_Monster);
        }

        CooldownEffect = EffectLibrary.Get(EffectLibrary.CooldownEffectId);
        CooldownDuration = InRow.Cooldown;
        SetByCallerCooldownKey = (int)ESetByCaller.Cooldown;
    }

    /// <summary>공격 속도 배율 (AttackSpeed 속성 %).</summary>
    protected float SpeedScale
    {
        get { return 1.0f + Actor.Attributes.Get(EAttribute.AttackSpeed) * 0.01f; }
    }

    protected Vector3 AimDirection
    {
        get
        {
            var dir = ActivationInfo.AimDirection;
            return dir.sqrMagnitude > 0.001f ? dir : Actor.transform.forward;
        }
    }

    protected void FaceAim()
    {
        Actor.Motor.Face(AimDirection, true);
    }

    /// <summary>AnimationTable Index 재생 (공격 속도 배율 적용).</summary>
    protected void PlayAnim(int InAnimId)
    {
        if (Actor.Anim != null)
        {
            Actor.Anim.Play(InAnimId, SpeedScale);
        }
    }

    protected void LockMovement(float InSeconds)
    {
        if (InSeconds > 0.0f)
        {
            Actor.Motor.LockMovement(InSeconds);
        }
    }

    /// <summary>비용/쿨다운 커밋 + StartEffects(발동 즉시 자신에게, 예: Blood Rite·Veil 의 Immaterial).</summary>
    protected bool Commit()
    {
        if (CommitAbility() == false)
        {
            return false;
        }

        var effects = Row.StartEffects;
        if (effects != null)
        {
            for (int i = 0; i < effects.Length; ++i)
            {
                Actor.ApplyEffect(effects[i], Actor, Row.Index);
            }
        }

        return true;
    }

    protected void ApplySelfEffects()
    {
        var effects = Row.SelfEffects;
        for (int i = 0; i < effects.Length; ++i)
        {
            Actor.ApplyEffect(effects[i], Actor, Row.Index);
        }
    }

    protected void PlayCastCue(Vector3 InPosition)
    {
        if (Row.CastCue != 0)
        {
            CueSpawner.Spawn(Row.CastCue, InPosition, AimDirection);
        }
    }

    protected void EndAfter(float InSeconds)
    {
        AddTask(new WaitDelayTask(Mathf.Max(0.0f, InSeconds), () => EndAbility(false)));
    }

    protected void Delay(float InSeconds, System.Action InAction)
    {
        AddTask(new WaitDelayTask(Mathf.Max(0.0f, InSeconds), InAction));
    }
}

/// <summary>EAbilityType → 구현 클래스 (Factory 패턴).</summary>
public static class AbilityFactory
{
    public static GameAbility Create(AbilityRow InRow, Actor InActor)
    {
        GameAbility ability = null;
        switch (InRow.Type)
        {
            case EAbilityType.MeleeCombo:
                ability = new MeleeComboAbility();
                break;
            case EAbilityType.Projectile:
                ability = new ProjectileAbility();
                break;
            case EAbilityType.Dash:
                ability = new DashAbility();
                break;
            case EAbilityType.AreaBurst:
                ability = new AreaBurstAbility();
                break;
            case EAbilityType.SelfBuff:
                ability = new SelfBuffAbility();
                break;
            case EAbilityType.Feed:
                ability = new FeedAbility();
                break;
            case EAbilityType.Leap:
                ability = new LeapAbility();
                break;
        }

        if (ability == null)
        {
            Log.Error($"AbilityFactory: unsupported type {InRow.Type} ({InRow.Index})");
            return null;
        }

        ability.Setup(InRow, InActor);
        return ability;
    }
}
