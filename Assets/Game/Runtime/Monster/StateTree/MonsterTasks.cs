using System;
using Framework;
using Framework.StateTree;
using UnityEngine;

/// <summary>집 주변 무작위 지점으로 이동 후 성공.</summary>
[Serializable]
[StateTreeNodeInfo("Movement", "집 주변 Radius 안 무작위 지점으로 이동 → 도착하면 Succeeded")]
public class WanderTask : MonsterTask
{
    public float Radius = 6.0f;
    public float Timeout = 8.0f;

    [NonSerialized] private Vector3 Destination;

    public override ETaskStatus EnterState(StateTreeContext InContext)
    {
        var brain = Brain(InContext);
        Vector2 offset = UnityEngine.Random.insideUnitCircle * Radius;
        Destination = brain.Owner.Home + new Vector3(offset.x, 0.0f, offset.y);
        brain.Owner.Motor.SetDestination(Destination);
        return ETaskStatus.Running;
    }

    public override ETaskStatus Tick(StateTreeContext InContext)
    {
        var owner = Brain(InContext).Owner;
        if (MathUtil.FlatDistance(owner.transform.position, Destination) < 1.0f || InContext.StateElapsed > Timeout)
        {
            return ETaskStatus.Succeeded;
        }

        return ETaskStatus.Running;
    }

    public override void ExitState(StateTreeContext InContext)
    {
        Brain(InContext).Owner.Motor.Stop();
    }

    public override string Describe()
    {
        return $"Wander r={Radius}m";
    }
}

/// <summary>타깃에게 접근. AttackRange × StopMultiplier 안에 들어오면 성공, 타깃이 없으면 실패.</summary>
[Serializable]
[StateTreeNodeInfo("Movement", "타깃 추적 → 사거리 안이면 Succeeded, 타깃 없음 Failed")]
public class ChaseTargetTask : MonsterTask
{
    public float StopMultiplier = 0.8f;
    public float RepathInterval = 0.25f;
    [Tooltip("접근 지점 = 사거리 × StopMultiplier × ApproachRatio (사거리보다 조금 안쪽을 목표로 해야 도착 직전 감속 없이 사거리에 들어온다)")]
    public float ApproachRatio = 0.6f;

    [NonSerialized] private float RepathTimer;

    public override ETaskStatus EnterState(StateTreeContext InContext)
    {
        RepathTimer = 0.0f;
        return ETaskStatus.Running;
    }

    public override ETaskStatus Tick(StateTreeContext InContext)
    {
        var brain = Brain(InContext);
        var target = brain.CurrentTarget;
        if (target == null)
        {
            return ETaskStatus.Failed;
        }

        if (brain.DistanceToTarget() <= brain.Owner.Row.AttackRange * StopMultiplier)
        {
            brain.Owner.Motor.Stop();
            return ETaskStatus.Succeeded;
        }

        RepathTimer -= InContext.DeltaTime;
        // 계산 중인 경로가 있으면 기다린다 (재요청하면 계산이 처음부터 다시 시작돼 먼 거리는 영영 끝나지 않는다).
        if (RepathTimer <= 0.0f && brain.Owner.Motor.IsPathPending == false)
        {
            RepathTimer = RepathInterval;
            // 타깃 중심이 아니라 사거리 안쪽의 개체별 접근 지점으로 (여럿이 한 점에 몰려 회피로 떨리는 것 방지).
            float range = brain.Owner.Row.AttackRange * StopMultiplier;
            brain.Owner.Motor.SetDestination(brain.GetApproachPoint(range * ApproachRatio));
        }

        return ETaskStatus.Running;
    }

    public override void ExitState(StateTreeContext InContext)
    {
        Brain(InContext).Owner.Motor.Stop();
    }

    public override string Describe()
    {
        return $"Chase Target (stop ×{StopMultiplier})";
    }
}

/// <summary>
/// 어빌리티 사용. Slots(우선순위) 중 쓸 수 있는 것을 발동하고 끝날 때까지 Running, 끝나면 Succeeded.
/// 쓸 수 있는 어빌리티가 없으면 Failed.
/// </summary>
[Serializable]
[StateTreeNodeInfo("Ability", "Slots 우선순위로 어빌리티 발동 → 종료 시 Succeeded, 사용 불가 Failed")]
public class UseAbilityTask : MonsterTask
{
    public EAbilitySlot[] Slots = new EAbilitySlot[0];

    [NonSerialized] private GameAbility Active;

    public override ETaskStatus EnterState(StateTreeContext InContext)
    {
        Active = null;
        var brain = Brain(InContext);
        var target = brain.CurrentTarget;
        if (target == null)
        {
            return ETaskStatus.Failed;
        }

        var ability = brain.PickAbility(Slots, brain.DistanceToTarget());
        if (ability == null)
        {
            return ETaskStatus.Failed;
        }

        brain.Owner.Motor.Stop();
        if (brain.Owner.TryActivate(ability.Row.Slot, target.transform.position, target) == false)
        {
            return ETaskStatus.Failed;
        }

        Active = ability;
        return ETaskStatus.Running;
    }

    public override ETaskStatus Tick(StateTreeContext InContext)
    {
        if (Active == null)
        {
            return ETaskStatus.Failed;
        }

        return Active.IsActive == true ? ETaskStatus.Running : ETaskStatus.Succeeded;
    }

    public override string Describe()
    {
        return Slots.Length == 0 ? "Use Ability (auto)" : $"Use Ability [{string.Join(",", Slots)}]";
    }
}

/// <summary>원거리형: 타깃이 MinDistance 안으로 들어오면 반대 방향으로 물러난다. 거리가 확보되면 성공.</summary>
[Serializable]
[StateTreeNodeInfo("Movement", "타깃과 MinDistance 이상 거리 확보 → Succeeded")]
public class KeepDistanceTask : MonsterTask
{
    public float MinDistance = 6.0f;
    public float StepDistance = 5.0f;
    public float Timeout = 2.5f;

    public override ETaskStatus EnterState(StateTreeContext InContext)
    {
        retreat(InContext);
        return ETaskStatus.Running;
    }

    public override ETaskStatus Tick(StateTreeContext InContext)
    {
        var brain = Brain(InContext);
        if (brain.CurrentTarget == null)
        {
            return ETaskStatus.Failed;
        }

        if (brain.DistanceToTarget() >= MinDistance || InContext.StateElapsed > Timeout)
        {
            return ETaskStatus.Succeeded;
        }

        return ETaskStatus.Running;
    }

    public override void ExitState(StateTreeContext InContext)
    {
        Brain(InContext).Owner.Motor.Stop();
    }

    private void retreat(StateTreeContext InContext)
    {
        var brain = Brain(InContext);
        if (brain.CurrentTarget == null)
        {
            return;
        }

        var owner = brain.Owner;
        Vector3 away = MathUtil.FlatDirection(brain.CurrentTarget.transform.position, owner.transform.position, -owner.transform.forward);
        owner.Motor.SetDestination(owner.transform.position + away * StepDistance);
    }

    public override string Describe()
    {
        return $"Keep Distance >= {MinDistance}m";
    }
}

/// <summary>집으로 귀환 (무적). 도착하면 체력 회복 후 성공.</summary>
[Serializable]
[StateTreeNodeInfo("Movement", "무적 상태로 집 귀환 → 도착 시 체력 회복, Succeeded")]
public class ReturnHomeTask : MonsterTask
{
    public bool HealOnArrive = true;
    public float ArriveDistance = 2.0f;

    public override ETaskStatus EnterState(StateTreeContext InContext)
    {
        var brain = Brain(InContext);
        brain.BeginReturn();
        brain.Owner.Motor.SetDestination(brain.Owner.Home);
        return ETaskStatus.Running;
    }

    public override ETaskStatus Tick(StateTreeContext InContext)
    {
        var owner = Brain(InContext).Owner;
        if (MathUtil.FlatDistance(owner.transform.position, owner.Home) <= ArriveDistance)
        {
            return ETaskStatus.Succeeded;
        }

        // 집은 움직이지 않으므로 진입 때 한 번만 요청한다. 경로를 잃었을 때만(피격 정지·순간이동 등) 다시 요청.
        // (예전: 매 프레임 SetDestination → 240m 경로 계산이 매 프레임 처음부터 다시 시작돼 pathPending 상태로 멈춰 있었다. Issues 36)
        if (owner.Motor.HasPathOrPending == false)
        {
            owner.Motor.SetDestination(owner.Home);
        }

        return ETaskStatus.Running;
    }

    public override void ExitState(StateTreeContext InContext)
    {
        Brain(InContext).EndReturn(HealOnArrive);
    }

    public override string Describe()
    {
        return HealOnArrive == true ? "Return Home (heal)" : "Return Home";
    }
}

/// <summary>타깃을 바라보며 대기 (Running 유지 — Wait 와 함께 쓰거나 트랜지션으로 빠져나간다).</summary>
[Serializable]
[StateTreeNodeInfo("Movement", "정지 후 타깃 바라보기 (Running)")]
public class FaceTargetTask : MonsterTask
{
    public override ETaskStatus EnterState(StateTreeContext InContext)
    {
        Brain(InContext).Owner.Motor.Stop();
        return ETaskStatus.Running;
    }

    public override ETaskStatus Tick(StateTreeContext InContext)
    {
        var brain = Brain(InContext);
        if (brain.CurrentTarget != null)
        {
            brain.Owner.Motor.Face(brain.CurrentTarget.transform.position - brain.Owner.transform.position, false);
        }

        return ETaskStatus.Running;
    }

    public override string Describe()
    {
        return "Face Target";
    }
}

/// <summary>진입 시 자신에게 이펙트 적용 (예: 보스 2페이즈 진입 버프). OncePerLife 면 한 번만.</summary>
[Serializable]
[StateTreeNodeInfo("Ability", "진입 시 EffectTable 이펙트를 자신에게 적용")]
public class ApplyEffectToSelfTask : MonsterTask
{
    public int EffectId;
    public bool OncePerLife = true;
    public bool CompleteImmediately = false;

    [NonSerialized] private bool Applied;

    public override ETaskStatus EnterState(StateTreeContext InContext)
    {
        if (OncePerLife == false || Applied == false)
        {
            var owner = Brain(InContext).Owner;
            owner.ApplyEffect(EffectId, owner, 0);
            Applied = true;
        }

        return CompleteImmediately == true ? ETaskStatus.Succeeded : ETaskStatus.Running;
    }

    public override string Describe()
    {
        return $"Apply Effect {EffectId}{(OncePerLife == true ? " (once)" : string.Empty)}";
    }
}
