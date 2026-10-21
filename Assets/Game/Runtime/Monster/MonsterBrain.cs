using Framework;
using Framework.Ability;
using Framework.StateTree;
using UnityEngine;

/// <summary>
/// 몬스터 AI 호스트 (언리얼 UStateTreeAIComponent + Schema 역할).
/// 행동 자체는 CharacterTable.StateTree 로 지정한 StateTree 에셋이 결정하고,
/// 이 클래스는 노드들이 쓰는 공용 기능(타깃 관리, 어빌리티 선택, 귀환 처리)과 이벤트 전달만 담당한다.
/// </summary>
public class MonsterBrain : MonoBehaviour
{
    private static readonly EAbilitySlot[] DefaultPriority =
    {
        EAbilitySlot.Ultimate, EAbilitySlot.Spell2, EAbilitySlot.Spell1, EAbilitySlot.WeaponE, EAbilitySlot.WeaponQ, EAbilitySlot.Primary,
    };

    private Actor Target;
    private bool Returning;
    private float CombatStartTime;

    public MonsterCharacter Owner { get; private set; }
    public StateTreeInstance Tree { get; private set; }
    public StateTreeBlackboard Blackboard { get; private set; }

    public Actor CurrentTarget
    {
        get { return Target; }
    }

    public bool IsReturning
    {
        get { return Returning; }
    }

    public float TimeInCombat
    {
        get { return Target != null ? Time.time - CombatStartTime : 0.0f; }
    }

    public void Initialize(MonsterCharacter InOwner)
    {
        Owner = InOwner;
        Blackboard = new StateTreeBlackboard();
        Owner.Asc.OnGameplayEvent += onGameplayEvent;
        Owner.Asc.OnAbilityEnded += onAbilityEnded;
        Owner.OnDied += onDied;

        var asset = loadTree();
        if (asset == null)
        {
            Log.Error($"MonsterBrain: StateTree missing for character {Owner.Row.Index}");
            return;
        }

        Tree = new StateTreeInstance(asset, this, Blackboard);
        StateTreeDebugger.Register($"{Tables.Text(Owner.Row.NameId)} #{Owner.ActorId}", this, Tree);
        Tree.Start();
    }

    private StateTreeAsset loadTree()
    {
        string address = Tables.Address(Owner.Row.StateTree);
        if (string.IsNullOrEmpty(address) == true)
        {
            return null;
        }

        return Managers.Get<AssetLoader>().LoadSync<ScriptableObject>(address) as StateTreeAsset;
    }

    private void OnDestroy()
    {
        if (Tree != null)
        {
            StateTreeDebugger.Unregister(Tree);
        }

        if (Owner != null && Owner.Asc != null)
        {
            Owner.Asc.OnGameplayEvent -= onGameplayEvent;
            Owner.Asc.OnAbilityEnded -= onAbilityEnded;
        }
    }

    private void Update()
    {
        if (Tree == null || Owner == null || Owner.IsDead == true)
        {
            return;
        }

        Tree.Tick(Time.deltaTime);
    }

    // ------------------------------------------------------------------ target

    public void SetTarget(Actor InTarget)
    {
        if (InTarget == Target)
        {
            return;
        }

        bool hadTarget = Target != null;
        Target = InTarget;
        if (Target != null && hadTarget == false)
        {
            CombatStartTime = Time.time;
            if (Tree != null)
            {
                Tree.SendEvent((int)EAIEvent.TargetAcquired);
            }
        }
        else if (Target == null && hadTarget == true && Tree != null)
        {
            Tree.SendEvent((int)EAIEvent.TargetLost);
        }

        if (Owner.Row.IsVBlood == true)
        {
            EventBus<BossEngagedEvent>.Publish(new BossEngagedEvent { Boss = Owner, Engaged = Target != null });
        }
    }

    public bool IsTargetValid(float InMaxDistance)
    {
        if (Target == null || Target.IsDead == true)
        {
            return false;
        }

        return MathUtil.FlatDistance(Owner.transform.position, Target.transform.position) <= InMaxDistance;
    }

    public float DistanceToTarget()
    {
        if (Target == null)
        {
            return float.MaxValue;
        }

        return MathUtil.FlatDistance(Owner.transform.position, Target.transform.position) - Target.Radius;
    }

    /// <summary>개체별 접근 각도 슬롯 수와 간격 (같은 쪽에서 오는 몬스터끼리 겹치지 않게).</summary>
    private const int ApproachSlots = 5;
    private const float ApproachSlotDegrees = 25.0f;

    /// <summary>
    /// 추적 목적지. 타깃 중심이 아니라 "타깃에서 나를 향한 방향 × InDistance" 지점을 쓰고, 개체마다 각도를 조금씩 비튼다.
    /// 모든 몬스터가 플레이어 한 점으로 모이면 NavMeshAgent 회피(RVO)가 서로를 밀어 속도가 떨어지고 방향이 떨린다 (Issues 33).
    /// </summary>
    public Vector3 GetApproachPoint(float InDistance)
    {
        if (Target == null)
        {
            return Owner.transform.position;
        }

        Vector3 targetPosition = Target.transform.position;
        Vector3 fromTarget = MathUtil.FlatDirection(targetPosition, Owner.transform.position, -Target.transform.forward);
        int slot = (int)(Owner.ActorId % ApproachSlots) - ApproachSlots / 2;
        fromTarget = Quaternion.Euler(0.0f, slot * ApproachSlotDegrees, 0.0f) * fromTarget;
        return targetPosition + fromTarget * Mathf.Max(0.0f, InDistance + Target.Radius);
    }

    // ------------------------------------------------------------------ abilities

    /// <summary>우선순위 슬롯 중 쿨다운이 끝났고 사거리 안인 어빌리티. InSlots 가 비어 있으면 기본 우선순위.</summary>
    public GameAbility PickAbility(EAbilitySlot[] InSlots, float InDistance)
    {
        var slots = InSlots != null && InSlots.Length > 0 ? InSlots : DefaultPriority;
        for (int i = 0; i < slots.Length; ++i)
        {
            var ability = Owner.GetSlot(slots[i]);
            if (ability == null)
            {
                continue;
            }

            if (InDistance > GetAbilityRange(ability))
            {
                continue;
            }

            EActivateFailReason reason;
            if (ability.CanActivate(out reason) == true)
            {
                return ability;
            }
        }

        return null;
    }

    public float GetAbilityRange(GameAbility InAbility)
    {
        var row = InAbility.Row;
        if (row.Type == EAbilityType.AreaBurst && row.Range <= 0.0f)
        {
            return row.Radius;
        }

        return row.Range > 0.0f ? row.Range : Owner.Row.AttackRange;
    }

    // ------------------------------------------------------------------ return home

    public void BeginReturn()
    {
        if (Returning == true)
        {
            return;
        }

        Returning = true;
        SetTarget(null);
        Owner.Asc.CancelAllAbilities();
        Owner.Asc.Tags.AddTag((int)ETag.State_Invulnerable);
    }

    public void EndReturn(bool InHeal)
    {
        if (Returning == false)
        {
            return;
        }

        Returning = false;
        Owner.Asc.Tags.RemoveTag((int)ETag.State_Invulnerable);
        if (InHeal == true)
        {
            Owner.Attributes.Set(EAttribute.Health, Owner.Attributes.Get(EAttribute.MaxHealth));
        }
    }

    // ------------------------------------------------------------------ events

    private void onGameplayEvent(int InEventTag, GameplayCueParams InPayload)
    {
        if (InEventTag != (int)ETag.Event_Damaged || Owner.IsDead == true || Returning == true)
        {
            return;
        }

        if (Target == null && InPayload.Source != null)
        {
            var attacker = InPayload.Source.GetComponent<Actor>();
            if (attacker != null && Owner.IsHostileTo(attacker) == true)
            {
                SetTarget(attacker);
            }
        }

        if (Tree != null)
        {
            Tree.SendEvent((int)EAIEvent.Damaged);
        }
    }

    private void onAbilityEnded(GameplayAbility InAbility, bool InCancelled)
    {
        if (Tree != null)
        {
            Tree.SendEvent((int)EAIEvent.AbilityEnded);
        }
    }

    private void onDied(Actor InVictim, Actor InKiller)
    {
        if (Owner.Row.IsVBlood == true)
        {
            EventBus<BossEngagedEvent>.Publish(new BossEngagedEvent { Boss = Owner, Engaged = false });
        }

        Target = null;
        if (Tree != null)
        {
            Tree.Stop();
        }
    }
}
