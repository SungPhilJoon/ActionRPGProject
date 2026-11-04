using Framework;
using UnityEngine;

/// <summary>Animator 파라미터 적용 (EAnimParamType 마다 한 클래스, 2026-11-01 switch → 다형성).</summary>
public abstract class AnimationParameterOp
{
    public abstract void Apply(Animator InAnimator, int InHash, float InValue);
}

[Kind(EAnimParamType.Trigger)]
public sealed class TriggerParameterOp : AnimationParameterOp
{
    public override void Apply(Animator InAnimator, int InHash, float InValue)
    {
        InAnimator.SetTrigger(InHash);
    }
}

/// <summary>0 이 아니면 true.</summary>
[Kind(EAnimParamType.Bool)]
public sealed class BoolParameterOp : AnimationParameterOp
{
    public override void Apply(Animator InAnimator, int InHash, float InValue)
    {
        InAnimator.SetBool(InHash, InValue != 0.0f);
    }
}

[Kind(EAnimParamType.Float)]
public sealed class FloatParameterOp : AnimationParameterOp
{
    public override void Apply(Animator InAnimator, int InHash, float InValue)
    {
        InAnimator.SetFloat(InHash, InValue);
    }
}

/// <summary>반올림.</summary>
[Kind(EAnimParamType.Int)]
public sealed class IntParameterOp : AnimationParameterOp
{
    public override void Apply(Animator InAnimator, int InHash, float InValue)
    {
        InAnimator.SetInteger(InHash, Mathf.RoundToInt(InValue));
    }
}
