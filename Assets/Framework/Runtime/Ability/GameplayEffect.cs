using System.Collections.Generic;

namespace Framework.Ability
{
    public enum EDurationPolicy
    {
        Instant = 0,
        HasDuration = 1,
        Infinite = 2,
    }

    public enum EModifierOp
    {
        /// <summary>더하기.</summary>
        Add = 0,
        /// <summary>퍼센트 가산 곱 (10 이면 +10%). 같은 속성의 퍼센트는 서로 더한 뒤 한 번 곱한다.</summary>
        MultiplyPercent = 1,
        /// <summary>덮어쓰기.</summary>
        Override = 2,
    }

    public enum EMagnitudeType
    {
        /// <summary>Value 그대로 (× 레벨 계수 없음).</summary>
        Scalar = 0,
        /// <summary>Spec 에 실려 온 SetByCaller[Key] 값 × Value.</summary>
        SetByCaller = 1,
        /// <summary>시전자 속성[Key] × Value (스펙 생성 시점 스냅샷).</summary>
        SourceAttribute = 2,
        /// <summary>대상 속성[Key] × Value (적용 시점).</summary>
        TargetAttribute = 3,
    }

    public enum EStackPolicy
    {
        /// <summary>적용할 때마다 별개의 인스턴스.</summary>
        None = 0,
        /// <summary>같은 이펙트가 있으면 지속시간만 갱신.</summary>
        Refresh = 1,
        /// <summary>같은 이펙트가 있으면 스택 +1 (MaxStack 까지) 하고 지속시간 갱신.</summary>
        Stack = 2,
    }

    [System.Serializable]
    public struct ModifierDef
    {
        public int Attribute;
        public EModifierOp Op;
        public EMagnitudeType MagnitudeType;
        public int Key;
        public float Value;
    }

    /// <summary>
    /// 언리얼 UGameplayEffect 대응 (정적 정의). 게임 쪽 CSV(EffectTable) 행에서 만들어진다.
    /// 런타임에는 수정하지 않는다.
    /// </summary>
    public class GameplayEffectDef
    {
        public int Id;
        public EDurationPolicy DurationPolicy;
        public float Duration;
        public float Period;
        public bool ExecutePeriodicOnApply;
        public ModifierDef[] Modifiers = new ModifierDef[0];
        public int ExecutionId;
        public GameplayTagContainer GrantedTags = new GameplayTagContainer();
        public GameplayTagContainer ApplicationRequiredTags = new GameplayTagContainer();
        public GameplayTagContainer ApplicationBlockedTags = new GameplayTagContainer();
        public GameplayTagContainer RemoveEffectsWithTags = new GameplayTagContainer();
        public EStackPolicy StackPolicy;
        public int MaxStack = 1;
        public int CueId;
        /// <summary>쿨다운 이펙트 여부. ASC.GetCooldownRemaining 이 AbilityId 로 찾는다.</summary>
        public bool IsCooldown;
    }

    /// <summary>
    /// 언리얼 FGameplayEffectSpec 대응. "누가, 어떤 어빌리티로, 어떤 수치로" 를 담는 적용 직전의 인스턴스.
    /// </summary>
    public class GameplayEffectSpec
    {
        public GameplayEffectDef Def;
        public AbilitySystemComponent Source;
        public int AbilityId;
        public float Level = 1.0f;
        public float DurationOverride = -1.0f;
        public UnityEngine.Vector3 HitPoint;
        public UnityEngine.Vector3 HitDirection;

        private readonly Dictionary<int, float> SetByCallerValues = new Dictionary<int, float>();
        private float[] SourceSnapshot;

        public GameplayEffectSpec(GameplayEffectDef InDef, AbilitySystemComponent InSource, int InAbilityId)
        {
            Def = InDef;
            Source = InSource;
            AbilityId = InAbilityId;
            if (InSource != null)
            {
                SourceSnapshot = InSource.Attributes.SnapshotCurrent();
            }
        }

        public float Duration
        {
            get { return DurationOverride >= 0.0f ? DurationOverride : Def.Duration; }
        }

        public void SetByCaller(int InKey, float InValue)
        {
            SetByCallerValues[InKey] = InValue;
        }

        public float GetSetByCaller(int InKey, float InDefault = 0.0f)
        {
            float value;
            if (SetByCallerValues.TryGetValue(InKey, out value) == true)
            {
                return value;
            }

            return InDefault;
        }

        /// <summary>스펙 생성 시점 시전자 속성 (시전자가 이후 죽거나 버프가 빠져도 값이 유지된다).</summary>
        public float GetSourceAttribute(int InAttribute)
        {
            if (SourceSnapshot == null || InAttribute < 0 || InAttribute >= SourceSnapshot.Length)
            {
                return 0.0f;
            }

            return SourceSnapshot[InAttribute];
        }

        public float CalculateMagnitude(ModifierDef InModifier, AbilitySystemComponent InTarget)
        {
            switch (InModifier.MagnitudeType)
            {
                case EMagnitudeType.SetByCaller:
                    return GetSetByCaller(InModifier.Key) * (InModifier.Value == 0.0f ? 1.0f : InModifier.Value);
                case EMagnitudeType.SourceAttribute:
                    return GetSourceAttribute(InModifier.Key) * InModifier.Value;
                case EMagnitudeType.TargetAttribute:
                    return InTarget == null ? 0.0f : InTarget.Attributes.GetCurrent(InModifier.Key) * InModifier.Value;
                default:
                    return InModifier.Value;
            }
        }
    }

    /// <summary>대상에 붙어 있는 지속형 이펙트.</summary>
    public class ActiveGameplayEffect
    {
        public int Handle;
        public GameplayEffectSpec Spec;
        public float Remaining;
        public float PeriodTimer;
        public int StackCount = 1;
        /// <summary>적용 시 계산해 둔 모디파이어 크기 (스택 1 기준).</summary>
        public float[] Magnitudes;
    }

    /// <summary>이펙트 실행 결과로 바뀐 속성 1건 (언리얼 FGameplayModifierEvaluatedData).</summary>
    public struct EvaluatedModifier
    {
        public int Attribute;
        public EModifierOp Op;
        public float Magnitude;
    }

    /// <summary>PostGameplayEffectExecute 로 넘기는 데이터.</summary>
    public struct EffectExecuteData
    {
        public GameplayEffectSpec Spec;
        public AbilitySystemComponent Target;
        public EvaluatedModifier Evaluated;
    }
}
