namespace Framework.Ability
{
    /// <summary>한 속성에 걸린 모디파이어 합계 (ModifierOperation.Accumulate 가 채운다).</summary>
    public struct ModifierSum
    {
        public float Add;
        public float Percent;
        public bool HasOverride;
        public float Override;

        /// <summary>Override 가 있으면 그 값, 없으면 (기본값 + 합) × (1 + 퍼센트).</summary>
        public float Resolve(float InBase)
        {
            return HasOverride == true ? Override : (InBase + Add) * (1.0f + Percent * 0.01f);
        }
    }

    /// <summary>
    /// 모디파이어 연산 (EModifierOp 마다 한 클래스, 2026-11-01 switch → 다형성).
    /// 기본값에 바로 적용(Instant 이펙트)과 지속 모디파이어 합산(Recalculate) 두 경우를 각 연산이 안다.
    /// </summary>
    public abstract class ModifierOperation
    {
        public static ModifierOperation Of(EModifierOp InOp)
        {
            var operation = KindRegistry<ModifierOperation>.Get((int)InOp);
            return operation != null ? operation : KindRegistry<ModifierOperation>.Get((int)EModifierOp.Add);
        }

        public abstract float ApplyToBase(float InValue, float InMagnitude);
        public abstract void Accumulate(ref ModifierSum InOutSum, float InMagnitude);
    }

    [Kind(EModifierOp.Add)]
    public sealed class AddModifierOperation : ModifierOperation
    {
        public override float ApplyToBase(float InValue, float InMagnitude)
        {
            return InValue + InMagnitude;
        }

        public override void Accumulate(ref ModifierSum InOutSum, float InMagnitude)
        {
            InOutSum.Add += InMagnitude;
        }
    }

    [Kind(EModifierOp.MultiplyPercent)]
    public sealed class MultiplyPercentModifierOperation : ModifierOperation
    {
        public override float ApplyToBase(float InValue, float InMagnitude)
        {
            return InValue * (1.0f + InMagnitude * 0.01f);
        }

        public override void Accumulate(ref ModifierSum InOutSum, float InMagnitude)
        {
            InOutSum.Percent += InMagnitude;
        }
    }

    [Kind(EModifierOp.Override)]
    public sealed class OverrideModifierOperation : ModifierOperation
    {
        public override float ApplyToBase(float InValue, float InMagnitude)
        {
            return InMagnitude;
        }

        public override void Accumulate(ref ModifierSum InOutSum, float InMagnitude)
        {
            InOutSum.HasOverride = true;
            InOutSum.Override = InMagnitude;
        }
    }

    /// <summary>모디파이어 크기 계산 (EMagnitudeType 마다 한 클래스). 없는 종류는 Scalar(고정값).</summary>
    public abstract class MagnitudeCalculation
    {
        public static MagnitudeCalculation Of(EMagnitudeType InType)
        {
            var calculation = KindRegistry<MagnitudeCalculation>.Get((int)InType);
            return calculation != null ? calculation : KindRegistry<MagnitudeCalculation>.Get((int)EMagnitudeType.Scalar);
        }

        public abstract float Calculate(GameplayEffectSpec InSpec, ModifierDef InModifier, AbilitySystemComponent InTarget);
    }

    [Kind(EMagnitudeType.Scalar)]
    public sealed class ScalarMagnitude : MagnitudeCalculation
    {
        public override float Calculate(GameplayEffectSpec InSpec, ModifierDef InModifier, AbilitySystemComponent InTarget)
        {
            return InModifier.Value;
        }
    }

    /// <summary>호출 쪽이 넘긴 값 × Value (Value 0 이면 1).</summary>
    [Kind(EMagnitudeType.SetByCaller)]
    public sealed class SetByCallerMagnitude : MagnitudeCalculation
    {
        public override float Calculate(GameplayEffectSpec InSpec, ModifierDef InModifier, AbilitySystemComponent InTarget)
        {
            return InSpec.GetSetByCaller(InModifier.Key) * (InModifier.Value == 0.0f ? 1.0f : InModifier.Value);
        }
    }

    /// <summary>스펙 생성 시점 시전자 속성 × Value.</summary>
    [Kind(EMagnitudeType.SourceAttribute)]
    public sealed class SourceAttributeMagnitude : MagnitudeCalculation
    {
        public override float Calculate(GameplayEffectSpec InSpec, ModifierDef InModifier, AbilitySystemComponent InTarget)
        {
            return InSpec.GetSourceAttribute(InModifier.Key) * InModifier.Value;
        }
    }

    /// <summary>대상 현재 속성 × Value.</summary>
    [Kind(EMagnitudeType.TargetAttribute)]
    public sealed class TargetAttributeMagnitude : MagnitudeCalculation
    {
        public override float Calculate(GameplayEffectSpec InSpec, ModifierDef InModifier, AbilitySystemComponent InTarget)
        {
            return InTarget == null ? 0.0f : InTarget.Attributes.GetCurrent(InModifier.Key) * InModifier.Value;
        }
    }
}
