using System;
using Framework;
using Framework.Ability;
using UnityEngine;

/// <summary>
/// 플레이어/몬스터 공통 액터. ActorBehaviour(ASC 소유) 위에 GameAttributeSet 과 슬롯 어빌리티를 얹고
/// CharacterTable 행에서 능력치와 어빌리티를 초기화한다.
/// 사망/보호막 같은 상태는 별도 플래그 없이 ASC 태그(State_Dead, State_Buff_Shield)가 원본이다.
/// </summary>
[RequireComponent(typeof(ActorMotor))]
public class Actor : ActorBehaviour
{
    public CharacterRow Row { get; private set; }
    public ETeam Team { get; private set; }
    public GameAttributeSet Attributes { get; private set; }
    public ActorMotor Motor { get; private set; }
    public ActorAnimator Anim { get; private set; }

    public bool IsDead
    {
        get { return Asc != null && HasTag((int)ETag.State_Dead) == true; }
    }

    /// <summary>지정형 공격(근접 부채꼴, 발사체, 대시)의 대상이 될 수 있는가. Immaterial 이면 통과한다 (V Rising).</summary>
    public bool IsTargetable
    {
        get { return IsDead == false && (Asc == null || HasTag((int)ETag.State_Immaterial) == false); }
    }
    public float Radius = 0.5f;

    /// <summary>(죽은 액터, 처치자)</summary>
    public event Action<Actor, Actor> OnDied;
    public event Action<Actor> OnRevived;

    private readonly GameAbility[] Slots = new GameAbility[(int)EAbilitySlot.Count];
    private float RegenAccumulator;

    public int CharacterId
    {
        get { return Row != null ? Row.Index : 0; }
    }

    public Vector3 AimOrigin
    {
        get { return transform.position + Vector3.up * 1.0f; }
    }

    public virtual bool IsPlayer
    {
        get { return false; }
    }

    /// <summary>ASC 의 소유자가 Actor 일 때만 Actor 를 돌려준다 (이펙트 Source, 게임플레이 이벤트 페이로드 등). GetComponent 탐색 없음.</summary>
    public static Actor From(AbilitySystemComponent InAsc)
    {
        if (InAsc == null)
        {
            return null;
        }

        return InAsc.OwnerActor is Actor actor ? actor : null;
    }

    public float HealthRatio
    {
        get
        {
            float max = Attributes.Get(EAttribute.MaxHealth);
            return max > 0.0f ? Attributes.Get(EAttribute.Health) / max : 0.0f;
        }
    }

    public virtual void Initialize(CharacterRow InRow, long InActorId)
    {
        Row = InRow;
        Team = InRow.Team;

        Attributes = new GameAttributeSet();
        InitializeActor(InActorId, Attributes);
        Asc.CooldownScaleProvider = getCooldownScale;

        Motor = GetComponent<ActorMotor>();
        Motor.Initialize(this);
        Anim = GetComponent<ActorAnimator>();
        ComponentUtil.GetOrAdd<CharacterHitFlash>(gameObject).Setup(this);

        applyBaseStats(InRow);
        float scale = InRow.Scale > 0.0f ? InRow.Scale : 1.0f;
        transform.localScale = Vector3.one * scale;
        Radius *= scale;

        for (int i = 0; i < InRow.Abilities.Length; ++i)
        {
            GiveAbility(InRow.Abilities[i]);
        }

        Managers.Get<ActorRegistry>().Add(this);
    }

    protected override void OnUnityDestroy()
    {
        var registry = Managers.Get<ActorRegistry>();
        if (registry != null)
        {
            registry.Remove(this);
        }

        base.OnUnityDestroy();
    }

    protected virtual void Update()
    {
        if (IsDead == true)
        {
            return;
        }

        float regen = Attributes.Get(EAttribute.HealthRegen);
        if (regen > 0.0f)
        {
            RegenAccumulator += regen * Time.deltaTime;
            if (RegenAccumulator >= 1.0f)
            {
                float amount = Mathf.Floor(RegenAccumulator);
                RegenAccumulator -= amount;
                Attributes.Set(EAttribute.Health, Attributes.GetBase((int)EAttribute.Health) + amount);
            }
        }

        if (Anim != null)
        {
            float maxSpeed = Mathf.Max(1.0f, Attributes.Get(EAttribute.MoveSpeed));
            Anim.SetSpeed(Motor.CurrentSpeed / maxSpeed);
        }
    }

    /// <summary>루트 모션 행(메두사 Attack1)이 끝나면 다시 끈다 (ActorAnimator.StopRootMotion).</summary>
    protected override void OnAbilityEnded(GameplayAbility InAbility, bool InWasCancelled)
    {
        base.OnAbilityEnded(InAbility, InWasCancelled);
        if (Anim != null)
        {
            Anim.StopRootMotion();
        }
    }

    /// <summary>보호막은 State_Buff_Shield 태그(지속 이펙트)가 있는 동안만 유지된다 → 마지막 태그가 빠지는 순간 소멸.</summary>
    protected override void OnTagChanged(int InTag, bool InAdded)
    {
        if (InTag == (int)ETag.State_Buff_Shield && InAdded == false)
        {
            Attributes.Set(EAttribute.Shield, 0.0f);
        }
    }

    // ------------------------------------------------------------------ abilities

    /// <summary>AbilityTable 의 Slot 컬럼 위치에 어빌리티를 부여한다 (같은 슬롯의 기존 어빌리티는 제거).</summary>
    public GameAbility GiveAbility(int InAbilityId)
    {
        AbilityRow row;
        if (InAbilityId == 0 || Tables.Root.Ability.TryGet(InAbilityId, out row) == false)
        {
            return null;
        }

        return GiveAbility(AbilityFactory.Create(row, this));
    }

    /// <summary>이미 만든 어빌리티(예: GraphAbility.Create)를 Row.Slot 위치에 부여한다 (같은 슬롯의 기존 어빌리티는 제거).</summary>
    public GameAbility GiveAbility(GameAbility InAbility)
    {
        var ability = InAbility;
        if (ability == null)
        {
            return null;
        }

        int slot = (int)ability.Row.Slot;
        if (slot >= 0 && slot < Slots.Length)
        {
            if (Slots[slot] != null)
            {
                RemoveAbility(Slots[slot]);
            }

            Slots[slot] = ability;
        }

        GrantAbility(ability);
        EventBus<AbilitySlotsChangedEvent>.Publish(new AbilitySlotsChangedEvent { Actor = this });
        return ability;
    }

    public void ClearSlot(EAbilitySlot InSlot)
    {
        int slot = (int)InSlot;
        if (Slots[slot] == null)
        {
            return;
        }

        RemoveAbility(Slots[slot]);
        Slots[slot] = null;
        EventBus<AbilitySlotsChangedEvent>.Publish(new AbilitySlotsChangedEvent { Actor = this });
    }

    public GameAbility GetSlot(EAbilitySlot InSlot)
    {
        int slot = (int)InSlot;
        if (slot < 0 || slot >= Slots.Length)
        {
            return null;
        }

        return Slots[slot];
    }

    public bool TryActivate(EAbilitySlot InSlot, Vector3 InAimPoint, Actor InTarget = null)
    {
        var ability = GetSlot(InSlot);
        if (ability != null && ability.IsActive == true)
        {
            ability.OnInputPressedWhileActive(makeActivationInfo(InSlot, InAimPoint, InTarget));
            return true;
        }

        EActivateFailReason reason;
        return TryActivate(InSlot, InAimPoint, InTarget, out reason);
    }

    /// <summary>실패 이유를 돌려주는 버전 (입력 버퍼용). 같은 어빌리티가 실행 중이면 AlreadyActive 로 실패한다.</summary>
    public bool TryActivate(EAbilitySlot InSlot, Vector3 InAimPoint, Actor InTarget, out EActivateFailReason OutReason)
    {
        OutReason = EActivateFailReason.Custom;
        if (IsDead == true)
        {
            return false;
        }

        var ability = GetSlot(InSlot);
        if (ability == null)
        {
            return false;
        }

        return Asc.TryActivateAbility(ability, makeActivationInfo(InSlot, InAimPoint, InTarget), out OutReason);
    }

    private AbilityActivationInfo makeActivationInfo(EAbilitySlot InSlot, Vector3 InAimPoint, Actor InTarget)
    {
        var info = new AbilityActivationInfo();
        info.AimPoint = InAimPoint;
        info.AimDirection = MathUtil.FlatDirection(transform.position, InAimPoint, transform.forward);
        info.InputSlot = (int)InSlot;
        info.Target = InTarget != null ? InTarget.Asc : null;
        return info;
    }

    // ------------------------------------------------------------------ combat

    public bool IsHostileTo(Actor InOther)
    {
        if (InOther == null || InOther == this)
        {
            return false;
        }

        if (Team == ETeam.Neutral || InOther.Team == ETeam.Neutral)
        {
            return false;
        }

        return Team != InOther.Team;
    }

    public void ApplyEffect(int InEffectId, Actor InSource, int InAbilityId)
    {
        var def = EffectLibrary.Get(InEffectId);
        if (def == null || IsDead == true)
        {
            return;
        }

        var sourceAsc = InSource != null ? InSource.Asc : Asc;
        Asc.ApplyGameplayEffectSpecToSelf(EffectLibrary.MakeSpec(InEffectId, sourceAsc, InAbilityId));
    }

    public void HealInstant(float InAmount)
    {
        if (InAmount <= 0.0f || IsDead == true)
        {
            return;
        }

        var spec = Asc.MakeOutgoingSpec(EffectLibrary.Get(EffectLibrary.HealEffectId), 0);
        spec.SetByCaller((int)ESetByCaller.Heal, InAmount);
        Asc.ApplyGameplayEffectSpecToSelf(spec);
    }

    public virtual void Die(Actor InKiller)
    {
        if (IsDead == true)
        {
            return;
        }

        // 태그를 먼저 붙여 취소/이펙트 제거 콜백 안에서도 IsDead 가 true 가 되게 한다.
        AddTag((int)ETag.State_Dead);
        Asc.CancelAllAbilities();
        Asc.RemoveAllEffects();
        Motor.Stop();
        Motor.CancelDash();
        if (Anim != null)
        {
            Anim.StopRootMotion();
            Anim.SetDead(true);
        }

        OnDied?.Invoke(this, InKiller);
        EventBus<ActorDiedEvent>.Publish(new ActorDiedEvent { Victim = this, Killer = InKiller });
    }

    public virtual void Revive(Vector3 InPosition, float InHealthRatio)
    {
        RemoveTag((int)ETag.State_Dead);
        Motor.Teleport(InPosition);
        Attributes.Set(EAttribute.Shield, 0.0f);
        Attributes.Set(EAttribute.Health, Attributes.Get(EAttribute.MaxHealth) * InHealthRatio);
        if (Anim != null)
        {
            Anim.SetDead(false);
        }

        OnRevived?.Invoke(this);
    }

    protected virtual void applyBaseStats(CharacterRow InRow)
    {
        Attributes.Set(EAttribute.MaxHealth, InRow.MaxHealth);
        Attributes.Set(EAttribute.Health, InRow.MaxHealth);
        Attributes.Set(EAttribute.PhysicalPower, InRow.PhysicalPower);
        Attributes.Set(EAttribute.SpellPower, InRow.SpellPower);
        Attributes.Set(EAttribute.PhysicalResist, InRow.PhysicalResist);
        Attributes.Set(EAttribute.SpellResist, InRow.SpellResist);
        Attributes.Set(EAttribute.MoveSpeed, InRow.MoveSpeed);
        Attributes.Set(EAttribute.Level, InRow.Level);
        Attributes.Set(EAttribute.CritChance, 5.0f);
        Attributes.Set(EAttribute.CritDamage, 0.0f);
        Attributes.Set(EAttribute.AttackSpeed, 0.0f);
    }

    private float getCooldownScale()
    {
        if (HasTag((int)ETag.Cheat_NoCooldown) == true)
        {
            return 0.0f;
        }

        return Mathf.Clamp(1.0f - Attributes.Get(EAttribute.CooldownReduction) * 0.01f, 0.2f, 1.0f);
    }
}
