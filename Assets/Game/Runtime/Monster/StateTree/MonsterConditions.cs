using System;
using Framework.StateTree;
using UnityEngine;

[Serializable]
[StateTreeNodeInfo("Target", "타깃이 있는가 (블랙보드 HasTarget)")]
public class HasTargetCondition : MonsterCondition
{
    protected override bool Test(StateTreeContext InContext)
    {
        return InContext.Blackboard.GetBool((int)EBBKey.HasTarget);
    }

    public override string Describe()
    {
        return Invert == true ? "No Target" : "Has Target";
    }
}

/// <summary>블랙보드 float 비교 (언리얼 Float Compare 조건). 거리/체력/전투 시간 등 대부분의 판정을 이것 하나로 만든다.</summary>
[Serializable]
[StateTreeNodeInfo("Blackboard", "블랙보드 값 비교 (Key Op Value)")]
public class BlackboardCompareCondition : MonsterCondition
{
    public EBBKey Key = EBBKey.TargetDistance;
    public ECompareOp Op = ECompareOp.LessOrEqual;
    public float Value;

    protected override bool Test(StateTreeContext InContext)
    {
        return CompareUtil.Compare(InContext.Blackboard.GetFloat((int)Key), Op, Value);
    }

    public override string Describe()
    {
        return $"{(Invert == true ? "NOT " : string.Empty)}{Key} {CompareUtil.Symbol(Op)} {Value}";
    }
}

/// <summary>타깃이 공격 사거리(CharacterTable.AttackRange × 배율) 안인가.</summary>
[Serializable]
[StateTreeNodeInfo("Target", "타깃 거리 <= AttackRange × Multiplier")]
public class InAttackRangeCondition : MonsterCondition
{
    public float Multiplier = 1.0f;

    protected override bool Test(StateTreeContext InContext)
    {
        var brain = Brain(InContext);
        float range = brain.Owner.Row.AttackRange * Multiplier;
        return InContext.Blackboard.GetFloat((int)EBBKey.TargetDistance) <= range;
    }

    public override string Describe()
    {
        return $"{(Invert == true ? "NOT " : string.Empty)}In Attack Range ×{Multiplier}";
    }
}

/// <summary>집에서 너무 멀어졌는가 (0 이면 ConstTable.MonsterLeashRange).</summary>
[Serializable]
[StateTreeNodeInfo("Movement", "HomeDistance > LeashRange")]
public class LeashExceededCondition : MonsterCondition
{
    public float LeashRange;

    protected override bool Test(StateTreeContext InContext)
    {
        float leash = LeashRange > 0.0f ? LeashRange : Tables.Const(EConst.MonsterLeashRange);
        return InContext.Blackboard.GetFloat((int)EBBKey.HomeDistance) > leash;
    }

    public override string Describe()
    {
        return LeashRange > 0.0f ? $"Leash > {LeashRange}m" : "Leash > table";
    }
}

/// <summary>사거리 안에서 쓸 수 있는 어빌리티가 있는가 (UseAbilityTask 와 같은 슬롯 목록).</summary>
[Serializable]
[StateTreeNodeInfo("Ability", "Slots 중 쿨다운이 끝났고 사거리 안인 어빌리티가 있는가")]
public class AbilityReadyCondition : MonsterCondition
{
    public EAbilitySlot[] Slots = new EAbilitySlot[0];

    protected override bool Test(StateTreeContext InContext)
    {
        var brain = Brain(InContext);
        if (brain.CurrentTarget == null)
        {
            return false;
        }

        return brain.PickAbility(Slots, brain.DistanceToTarget()) != null;
    }

    public override string Describe()
    {
        return Slots.Length == 0 ? "Any Ability Ready" : $"Ability Ready [{string.Join(",", Slots)}]";
    }
}

[Serializable]
[StateTreeNodeInfo("Utility", "현재 상태 경과 시간 >= Seconds")]
public class TimeInStateCondition : MonsterCondition
{
    public float Seconds = 1.0f;

    protected override bool Test(StateTreeContext InContext)
    {
        return InContext.StateElapsed >= Seconds;
    }

    public override string Describe()
    {
        return $"Time In State >= {Seconds}s";
    }
}

[Serializable]
[StateTreeNodeInfo("Utility", "확률 (0~1). 평가할 때마다 새로 굴린다")]
public class RandomChanceCondition : MonsterCondition
{
    [Range(0.0f, 1.0f)] public float Chance = 0.5f;

    protected override bool Test(StateTreeContext InContext)
    {
        return UnityEngine.Random.value < Chance;
    }

    public override string Describe()
    {
        return $"Random {Chance * 100.0f:0}%";
    }
}
