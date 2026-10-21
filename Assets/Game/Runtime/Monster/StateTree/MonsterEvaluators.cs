using System;
using Framework;
using Framework.StateTree;
using UnityEngine;

/// <summary>
/// 감지 평가기. 일정 주기로 어그로 범위 안 가장 가까운 적을 찾아 타깃으로 잡고,
/// 블랙보드(EBBKey)에 타깃 유무/거리, 집과의 거리, 체력 비율을 매 틱 기록한다.
/// </summary>
[Serializable]
[StateTreeNodeInfo("Perception", "타깃 감지 + 블랙보드 갱신 (HasTarget, TargetDistance, HomeDistance, HealthRatio ...)")]
public class MonsterPerceptionEvaluator : MonsterEvaluator
{
    [Tooltip("0 이면 CharacterTable.AggroRange")]
    public float AggroRange;
    [Tooltip("타깃이 이 배율 × AggroRange 보다 멀어지면 놓친다")]
    public float LoseTargetMultiplier = 2.5f;
    public float ScanInterval = 0.25f;

    [NonSerialized] private float ScanTimer;

    public override void Tick(StateTreeContext InContext)
    {
        var brain = Brain(InContext);
        var owner = brain.Owner;
        float aggro = AggroRange > 0.0f ? AggroRange : owner.Row.AggroRange;

        if (brain.IsReturning == false)
        {
            if (brain.CurrentTarget != null && brain.IsTargetValid(aggro * LoseTargetMultiplier) == false)
            {
                brain.SetTarget(null);
            }

            ScanTimer -= InContext.DeltaTime;
            if (brain.CurrentTarget == null && ScanTimer <= 0.0f)
            {
                ScanTimer = ScanInterval;
                var found = Managers.Get<ActorRegistry>().FindNearestHostile(owner, aggro);
                if (found != null)
                {
                    brain.SetTarget(found);
                }
            }
        }

        var bb = InContext.Blackboard;
        var target = brain.CurrentTarget;
        bb.SetBool((int)EBBKey.HasTarget, target != null);
        bb.SetFloat((int)EBBKey.TargetDistance, target != null ? brain.DistanceToTarget() : 9999.0f);
        bb.SetFloat((int)EBBKey.HomeDistance, MathUtil.FlatDistance(owner.transform.position, owner.Home));
        bb.SetFloat((int)EBBKey.HealthRatio, owner.HealthRatio);
        bb.SetFloat((int)EBBKey.TargetHealthRatio, target != null ? target.HealthRatio : 0.0f);
        bb.SetFloat((int)EBBKey.TimeInCombat, brain.TimeInCombat);
        bb.SetBool((int)EBBKey.IsInvulnerable, owner.Asc.Tags.HasTag((int)ETag.State_Invulnerable));
    }

    public override string Describe()
    {
        return AggroRange > 0.0f ? $"Perception (aggro {AggroRange}m)" : "Perception (aggro = table)";
    }
}
