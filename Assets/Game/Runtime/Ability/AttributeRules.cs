using UnityEngine;

/// <summary>
/// 속성 값 규칙 (2026-11-01, GameAttributeSet.PreAttributeChange 의 EAttribute switch → 규칙 객체).
/// 속성마다 규칙 객체 하나를 표(GameAttributeSet.Rules)에 둔다. 같은 규칙 클래스를 매개변수만 바꿔 여러 속성이 쓴다.
/// </summary>
public abstract class AttributeRule
{
    public abstract float Apply(GameAttributeSet InSet, float InValue);
}

/// <summary>0 ~ 다른 속성(최대치) 사이로 (Health ≤ MaxHealth, BloodPool ≤ MaxBloodPool).</summary>
public sealed class ClampToAttributeRule : AttributeRule
{
    private readonly EAttribute Max;

    public ClampToAttributeRule(EAttribute InMax)
    {
        Max = InMax;
    }

    public override float Apply(GameAttributeSet InSet, float InValue)
    {
        return Mathf.Clamp(InValue, 0.0f, InSet.GetCurrent((int)Max));
    }
}

/// <summary>최솟값만 (Shield · MaxHealth ≥ 0, MoveSpeed ≥ 0.5).</summary>
public sealed class MinimumRule : AttributeRule
{
    private readonly float Min;

    public MinimumRule(float InMin)
    {
        Min = InMin;
    }

    public override float Apply(GameAttributeSet InSet, float InValue)
    {
        return Mathf.Max(Min, InValue);
    }
}

/// <summary>범위 (저항 −100 ~ 90).</summary>
public sealed class RangeRule : AttributeRule
{
    private readonly float Min;
    private readonly float Max;

    public RangeRule(float InMin, float InMax)
    {
        Min = InMin;
        Max = InMax;
    }

    public override float Apply(GameAttributeSet InSet, float InValue)
    {
        return Mathf.Clamp(InValue, Min, Max);
    }
}
