using System;
using System.Collections.Generic;
using UnityEngine;

namespace ActionRPG.Core.AbilitySystem
{
    /// <summary>언리얼 EGameplayEffectDurationType.</summary>
    public enum GameplayEffectDurationType : byte
    {
        /// <summary>즉시 BaseValue 를 바꾸고 끝난다. 활성 목록에 남지 않는다.</summary>
        Instant = 0,
        /// <summary>정해진 시간 동안 유지된다.</summary>
        HasDuration = 1,
        /// <summary>직접 제거할 때까지 유지된다.</summary>
        Infinite = 2
    }

    /// <summary>언리얼 EGameplayModOp.</summary>
    public enum GameplayModOp : byte
    {
        Additive = 0,
        Multiplicitive = 1,
        Division = 2,
        Override = 3
    }

    /// <summary>언리얼 EGameplayEffectMagnitudeCalculation.</summary>
    public enum GameplayEffectMagnitudeCalculation : byte
    {
        ScalableFloat = 0,
        AttributeBased = 1,
        CustomCalculationClass = 2,
        SetByCaller = 3
    }

    /// <summary>언리얼 EGameplayEffectAttributeCaptureSource.</summary>
    public enum GameplayEffectAttributeCaptureSource : byte
    {
        Source = 0,
        Target = 1
    }

    /// <summary>언리얼 EAttributeBasedFloatCalculationType.</summary>
    public enum AttributeBasedFloatCalculationType : byte
    {
        /// <summary>CurrentValue.</summary>
        AttributeMagnitude = 0,
        /// <summary>BaseValue.</summary>
        AttributeBaseValue = 1,
        /// <summary>CurrentValue - BaseValue.</summary>
        AttributeBonusMagnitude = 2
    }

    /// <summary>언리얼 EGameplayEffectStackingType.</summary>
    public enum GameplayEffectStackingType : byte
    {
        /// <summary>적용할 때마다 별도 인스턴스가 생긴다.</summary>
        None = 0,
        /// <summary>시전자(Source)마다 한 인스턴스에 스택이 쌓인다.</summary>
        AggregateBySource = 1,
        /// <summary>대상(Target)에 한 인스턴스만 있고 누가 걸든 스택이 쌓인다.</summary>
        AggregateByTarget = 2
    }

    public enum GameplayEffectStackingDurationPolicy : byte
    {
        RefreshOnSuccessfulApplication = 0,
        NeverRefresh = 1
    }

    public enum GameplayEffectStackingPeriodPolicy : byte
    {
        ResetOnSuccessfulApplication = 0,
        NeverReset = 1
    }

    public enum GameplayEffectStackingExpirationPolicy : byte
    {
        ClearEntireStack = 0,
        RemoveSingleStackAndRefreshDuration = 1,
        RefreshDuration = 2
    }

    /// <summary>언리얼 FScalableFloat. 값 × 레벨 커브. 커브가 비어 있으면 값 그대로.</summary>
    [Serializable]
    public class ScalableFloat
    {
        public float Value;
        [Tooltip("가로축 = 레벨, 세로축 = 배율. 비워 두면 레벨과 무관하게 Value 를 쓴다.")]
        public AnimationCurve Curve = new AnimationCurve();

        public ScalableFloat() { }
        public ScalableFloat(float value) { Value = value; }

        public float GetValueAtLevel(float level)
        {
            if (Curve == null || Curve.length == 0) return Value;
            return Value * Curve.Evaluate(level);
        }
    }

    /// <summary>언리얼 FGameplayEffectAttributeCaptureDefinition.</summary>
    [Serializable]
    public class GameplayEffectAttributeCaptureDefinition
    {
        public GameplayAttribute AttributeToCapture;
        public GameplayEffectAttributeCaptureSource AttributeSource;
        [Tooltip("켜면 스펙이 만들어지는 순간의 값을 고정한다(시전 시점 스냅샷). 끄면 적용 시점의 값을 읽는다.")]
        public bool bSnapshot;

        public GameplayEffectAttributeCaptureDefinition() { }

        public GameplayEffectAttributeCaptureDefinition(GameplayAttribute attribute, GameplayEffectAttributeCaptureSource source, bool snapshot)
        {
            AttributeToCapture = attribute;
            AttributeSource = source;
            bSnapshot = snapshot;
        }

        public override string ToString() => $"{AttributeSource}:{AttributeToCapture}{(bSnapshot ? "(snapshot)" : string.Empty)}";
    }

    /// <summary>언리얼 FAttributeBasedFloat. ((어트리뷰트 + Pre) × 계수) + Post.</summary>
    [Serializable]
    public class AttributeBasedFloat
    {
        public ScalableFloat Coefficient = new ScalableFloat(1f);
        public ScalableFloat PreMultiplyAdditiveValue = new ScalableFloat(0f);
        public ScalableFloat PostMultiplyAdditiveValue = new ScalableFloat(0f);
        public GameplayEffectAttributeCaptureDefinition BackingAttribute = new GameplayEffectAttributeCaptureDefinition();
        public AttributeBasedFloatCalculationType AttributeCalculationType = AttributeBasedFloatCalculationType.AttributeMagnitude;

        public bool TryCalculate(GameplayEffectSpec spec, out float magnitude)
        {
            magnitude = 0f;
            if (!spec.AttemptCalculateCapturedAttributeMagnitude(BackingAttribute, AttributeCalculationType, out float attributeValue))
                return false;
            float level = spec.Level;
            magnitude = (attributeValue + PreMultiplyAdditiveValue.GetValueAtLevel(level)) * Coefficient.GetValueAtLevel(level)
                        + PostMultiplyAdditiveValue.GetValueAtLevel(level);
            return true;
        }
    }

    /// <summary>언리얼 FCustomCalculationBasedFloat.</summary>
    [Serializable]
    public class CustomCalculationBasedFloat
    {
        public GameplayModMagnitudeCalculation CalculationClassMagnitude;
        public ScalableFloat Coefficient = new ScalableFloat(1f);
        public ScalableFloat PreMultiplyAdditiveValue = new ScalableFloat(0f);
        public ScalableFloat PostMultiplyAdditiveValue = new ScalableFloat(0f);

        public bool TryCalculate(GameplayEffectSpec spec, out float magnitude)
        {
            magnitude = 0f;
            if (CalculationClassMagnitude == null) return false;
            float level = spec.Level;
            float baseValue = CalculationClassMagnitude.CalculateBaseMagnitude(spec);
            magnitude = (baseValue + PreMultiplyAdditiveValue.GetValueAtLevel(level)) * Coefficient.GetValueAtLevel(level)
                        + PostMultiplyAdditiveValue.GetValueAtLevel(level);
            return true;
        }
    }

    /// <summary>언리얼 FSetByCallerFloat. 스펙을 만든 쪽이 태그로 값을 넣어 준다.</summary>
    [Serializable]
    public class SetByCallerFloat
    {
        public GameplayTag DataTag;
    }

    /// <summary>언리얼 FGameplayEffectModifierMagnitude. 모디파이어 크기 / 지속시간을 계산하는 방법.</summary>
    [Serializable]
    public class GameplayEffectModifierMagnitude
    {
        public GameplayEffectMagnitudeCalculation MagnitudeCalculationType = GameplayEffectMagnitudeCalculation.ScalableFloat;
        public ScalableFloat ScalableFloatMagnitude = new ScalableFloat(0f);
        public AttributeBasedFloat AttributeBasedMagnitude = new AttributeBasedFloat();
        public CustomCalculationBasedFloat CustomMagnitude = new CustomCalculationBasedFloat();
        public SetByCallerFloat SetByCallerMagnitude = new SetByCallerFloat();

        public static GameplayEffectModifierMagnitude FromScalable(float value) => new GameplayEffectModifierMagnitude
        {
            MagnitudeCalculationType = GameplayEffectMagnitudeCalculation.ScalableFloat,
            ScalableFloatMagnitude = new ScalableFloat(value)
        };

        public static GameplayEffectModifierMagnitude FromSetByCaller(GameplayTag tag) => new GameplayEffectModifierMagnitude
        {
            MagnitudeCalculationType = GameplayEffectMagnitudeCalculation.SetByCaller,
            SetByCallerMagnitude = new SetByCallerFloat { DataTag = tag }
        };

        public static GameplayEffectModifierMagnitude FromAttribute(GameplayAttribute attribute, GameplayEffectAttributeCaptureSource source,
            bool snapshot, float coefficient = 1f, float pre = 0f, float post = 0f) => new GameplayEffectModifierMagnitude
        {
            MagnitudeCalculationType = GameplayEffectMagnitudeCalculation.AttributeBased,
            AttributeBasedMagnitude = new AttributeBasedFloat
            {
                BackingAttribute = new GameplayEffectAttributeCaptureDefinition(attribute, source, snapshot),
                Coefficient = new ScalableFloat(coefficient),
                PreMultiplyAdditiveValue = new ScalableFloat(pre),
                PostMultiplyAdditiveValue = new ScalableFloat(post)
            }
        };

        public static GameplayEffectModifierMagnitude FromCustom(GameplayModMagnitudeCalculation calculation, float coefficient = 1f) =>
            new GameplayEffectModifierMagnitude
            {
                MagnitudeCalculationType = GameplayEffectMagnitudeCalculation.CustomCalculationClass,
                CustomMagnitude = new CustomCalculationBasedFloat
                {
                    CalculationClassMagnitude = calculation,
                    Coefficient = new ScalableFloat(coefficient)
                }
            };

        /// <summary>언리얼 AttemptCalculateMagnitude.</summary>
        public bool AttemptCalculateMagnitude(GameplayEffectSpec spec, out float magnitude)
        {
            switch (MagnitudeCalculationType)
            {
                case GameplayEffectMagnitudeCalculation.ScalableFloat:
                    magnitude = ScalableFloatMagnitude.GetValueAtLevel(spec.Level);
                    return true;
                case GameplayEffectMagnitudeCalculation.AttributeBased:
                    return AttributeBasedMagnitude.TryCalculate(spec, out magnitude);
                case GameplayEffectMagnitudeCalculation.CustomCalculationClass:
                    return CustomMagnitude.TryCalculate(spec, out magnitude);
                case GameplayEffectMagnitudeCalculation.SetByCaller:
                    magnitude = spec.GetSetByCallerMagnitude(SetByCallerMagnitude.DataTag, warnIfNotFound: true);
                    return true;
            }
            magnitude = 0f;
            return false;
        }

        /// <summary>이 계산이 필요로 하는 어트리뷰트 캡처 목록. 스펙이 소스 스냅샷을 찍을 때 쓴다.</summary>
        public void GetAttributeCaptureDefinitions(List<GameplayEffectAttributeCaptureDefinition> output)
        {
            if (MagnitudeCalculationType == GameplayEffectMagnitudeCalculation.AttributeBased)
                output.Add(AttributeBasedMagnitude.BackingAttribute);
            else if (MagnitudeCalculationType == GameplayEffectMagnitudeCalculation.CustomCalculationClass && CustomMagnitude.CalculationClassMagnitude != null)
                output.AddRange(CustomMagnitude.CalculationClassMagnitude.RelevantAttributesToCapture);
        }

        internal void RegisterTags()
        {
            if (!string.IsNullOrEmpty(SetByCallerMagnitude.DataTag.TagName))
                SetByCallerMagnitude.DataTag = GameplayTag.Request(SetByCallerMagnitude.DataTag.TagName);
        }
    }

    /// <summary>언리얼 FGameplayModifierInfo. 어트리뷰트 하나를 어떻게 바꿀지.</summary>
    [Serializable]
    public class GameplayModifierInfo
    {
        public GameplayAttribute Attribute;
        public GameplayModOp ModifierOp = GameplayModOp.Additive;
        public GameplayEffectModifierMagnitude ModifierMagnitude = new GameplayEffectModifierMagnitude();
        [Tooltip("시전자가 이 태그 조건을 만족할 때만 적용된다.")]
        public GameplayTagRequirements SourceTags = new GameplayTagRequirements();
        [Tooltip("대상이 이 태그 조건을 만족할 때만 적용된다.")]
        public GameplayTagRequirements TargetTags = new GameplayTagRequirements();
    }

    /// <summary>언리얼 FGameplayEffectExecutionDefinition.</summary>
    [Serializable]
    public class GameplayEffectExecutionDefinition
    {
        public GameplayEffectExecutionCalculation CalculationClass;
        [Tooltip("Execution 에 함께 넘길 태그.")]
        public GameplayTagContainer PassedInTags = new GameplayTagContainer();
        [Tooltip("Execution 이 성공하면 대상에게 추가로 적용할 이펙트.")]
        public List<GameplayEffect> ConditionalGameplayEffects = new List<GameplayEffect>();
    }

    /// <summary>언리얼 FGameplayEffectCue. 이 이펙트가 적용·유지·제거될 때 띄울 게임플레이 큐.</summary>
    [Serializable]
    public class GameplayEffectCue
    {
        public GameplayTagContainer GameplayCueTags = new GameplayTagContainer();
        public float MinLevel;
        public float MaxLevel;
        [Tooltip("큐 파라미터의 NormalizedMagnitude 를 계산할 어트리뷰트. 비워 두면 첫 모디파이어의 크기를 쓴다.")]
        public GameplayAttribute MagnitudeAttribute;

        public float NormalizeLevel(float level)
        {
            float range = MaxLevel - MinLevel;
            if (range <= 0f) return 1f;
            return Mathf.Clamp01((level - MinLevel) / range);
        }
    }

    /// <summary>
    /// 언리얼 UGameplayEffect. 데이터만 담은 에셋이며 코드로 상속하지 않는다.
    /// 무엇을(어트리뷰트 · 태그 · 큐 · 어빌리티) 얼마나(크기) 얼마 동안(지속) 바꿀지를 정의한다.
    /// 실제 적용 상태는 <see cref="ActiveGameplayEffect"/> 가, 적용 직전 값은 <see cref="GameplayEffectSpec"/> 이 가진다.
    /// </summary>
    [CreateAssetMenu(menuName = "ActionRPG/Ability System/Gameplay Effect", fileName = "GE_New")]
    public class GameplayEffect : ScriptableObject
    {
        [Header("지속")]
        public GameplayEffectDurationType DurationPolicy = GameplayEffectDurationType.Instant;
        [Tooltip("HasDuration 일 때의 지속시간(초).")]
        public GameplayEffectModifierMagnitude DurationMagnitude = GameplayEffectModifierMagnitude.FromScalable(0f);
        [Tooltip("0 보다 크면 이 간격마다 Instant 처럼 실행된다 (도트, 리젠).")]
        public ScalableFloat Period = new ScalableFloat(0f);
        public bool bExecutePeriodicEffectOnApplication = true;

        [Header("모디파이어 / 실행")]
        public List<GameplayModifierInfo> Modifiers = new List<GameplayModifierInfo>();
        public List<GameplayEffectExecutionDefinition> Executions = new List<GameplayEffectExecutionDefinition>();

        [Header("적용 확률")]
        public ScalableFloat ChanceToApplyToTarget = new ScalableFloat(1f);

        [Header("스택")]
        public GameplayEffectStackingType StackingType = GameplayEffectStackingType.None;
        [Min(1)] public int StackLimitCount = 1;
        public GameplayEffectStackingDurationPolicy StackDurationRefreshPolicy = GameplayEffectStackingDurationPolicy.RefreshOnSuccessfulApplication;
        public GameplayEffectStackingPeriodPolicy StackPeriodResetPolicy = GameplayEffectStackingPeriodPolicy.ResetOnSuccessfulApplication;
        public GameplayEffectStackingExpirationPolicy StackExpirationPolicy = GameplayEffectStackingExpirationPolicy.ClearEntireStack;

        [Header("태그")]
        [Tooltip("이펙트 자체를 설명하는 태그 (언리얼 AssetTags). 대상에게 부여되지는 않는다.")]
        public GameplayTagContainer AssetTags = new GameplayTagContainer();
        [Tooltip("이 이펙트가 활성인 동안 대상이 갖게 되는 태그 (언리얼 GrantedTags).")]
        public GameplayTagContainer GrantedTags = new GameplayTagContainer();
        [Tooltip("대상이 이 조건을 만족해야 적용된다.")]
        public GameplayTagRequirements ApplicationTagRequirements = new GameplayTagRequirements();
        [Tooltip("조건을 만족하지 못하는 동안은 꺼진다(Inhibited). 다시 만족하면 켜진다.")]
        public GameplayTagRequirements OngoingTagRequirements = new GameplayTagRequirements();
        [Tooltip("이 조건을 만족하면 제거된다.")]
        public GameplayTagRequirements RemovalTagRequirements = new GameplayTagRequirements();
        [Tooltip("적용될 때 대상의 활성 이펙트 중 이 태그를 가진 것을 제거한다.")]
        public GameplayTagContainer RemoveGameplayEffectsWithTags = new GameplayTagContainer();
        [Tooltip("활성인 동안 이 태그를 가진 다른 이펙트의 적용을 막는다 (면역).")]
        public GameplayTagContainer GrantedApplicationImmunityTags = new GameplayTagContainer();

        [Header("게임플레이 큐")]
        public List<GameplayEffectCue> GameplayCues = new List<GameplayEffectCue>();

        [Header("부여 어빌리티 (서버)")]
        [Tooltip("활성인 동안 대상에게 이 어빌리티를 준다. 제거되면 회수한다.")]
        public List<GameplayAbility> GrantedAbilities = new List<GameplayAbility>();

        [Header("UI")]
        public string DisplayName;
        [TextArea(2, 4)] public string Description;
        public Sprite Icon;

        private bool _tagsRegistered;

        protected virtual void OnEnable()
        {
            _tagsRegistered = false;
            RegisterTags();
            AbilitySystemGlobals.RegisterAsset(this);
        }

        /// <summary>인스펙터에서 입력한 태그 이름을 태그 매니저에 등록한다.</summary>
        public void RegisterTags()
        {
            if (_tagsRegistered) return;
            _tagsRegistered = true;
            AssetTags.RegisterAll();
            GrantedTags.RegisterAll();
            ApplicationTagRequirements.RegisterAll();
            OngoingTagRequirements.RegisterAll();
            RemovalTagRequirements.RegisterAll();
            RemoveGameplayEffectsWithTags.RegisterAll();
            GrantedApplicationImmunityTags.RegisterAll();
            DurationMagnitude.RegisterTags();
            for (int i = 0; i < Modifiers.Count; i++)
            {
                Modifiers[i].ModifierMagnitude.RegisterTags();
                Modifiers[i].SourceTags.RegisterAll();
                Modifiers[i].TargetTags.RegisterAll();
            }
            for (int i = 0; i < Executions.Count; i++) Executions[i].PassedInTags.RegisterAll();
            for (int i = 0; i < GameplayCues.Count; i++) GameplayCues[i].GameplayCueTags.RegisterAll();
        }

        /// <summary>네트워크 식별자 (에셋 이름 해시).</summary>
        public int EffectId => AbilitySystemGlobals.GetAssetId(this);

        public bool IsInstant => DurationPolicy == GameplayEffectDurationType.Instant;
    }

    // ==================================================================== 계산 클래스

    /// <summary>
    /// 언리얼 UGameplayModMagnitudeCalculation (MMC). 모디파이어 하나의 크기를 코드로 계산한다.
    /// 에셋으로 만들어 두고 <see cref="CustomCalculationBasedFloat"/> 에 꽂는다.
    /// </summary>
    public abstract class GameplayModMagnitudeCalculation : ScriptableObject
    {
        [Tooltip("계산에 쓸 어트리뷰트. Source + Snapshot 이면 스펙 생성 시점 값으로 고정된다.")]
        public List<GameplayEffectAttributeCaptureDefinition> RelevantAttributesToCapture = new List<GameplayEffectAttributeCaptureDefinition>();

        public abstract float CalculateBaseMagnitude(GameplayEffectSpec spec);

        protected bool GetCapturedAttributeMagnitude(GameplayEffectAttributeCaptureDefinition definition, GameplayEffectSpec spec, out float magnitude) =>
            spec.AttemptCalculateCapturedAttributeMagnitude(definition, AttributeBasedFloatCalculationType.AttributeMagnitude, out magnitude);

        protected float GetSetByCallerMagnitudeByTag(GameplayEffectSpec spec, GameplayTag tag, float defaultValue = 0f) =>
            spec.GetSetByCallerMagnitude(tag, true, defaultValue);
    }

    /// <summary>
    /// 언리얼 UGameplayEffectExecutionCalculation. 여러 어트리뷰트를 읽어 여러 어트리뷰트를 한 번에 바꾼다.
    /// 피해 공식처럼 "공격자 공격력 + 대상 방어력 → 받는 피해" 같은 계산을 여기 둔다. 서버에서만 실행된다.
    /// </summary>
    public abstract class GameplayEffectExecutionCalculation : ScriptableObject
    {
        public List<GameplayEffectAttributeCaptureDefinition> RelevantAttributesToCapture = new List<GameplayEffectAttributeCaptureDefinition>();

        public abstract void Execute(GameplayEffectCustomExecutionParameters executionParams, GameplayEffectCustomExecutionOutput output);
    }

    /// <summary>언리얼 FGameplayEffectCustomExecutionParameters.</summary>
    public sealed class GameplayEffectCustomExecutionParameters
    {
        public GameplayEffectSpec OwningSpec { get; }
        public AbilitySystemComponent TargetAbilitySystemComponent { get; }
        public AbilitySystemComponent SourceAbilitySystemComponent => OwningSpec.Context?.InstigatorAbilitySystemComponent;
        public GameplayTagContainer PassedInTags { get; }

        public GameplayEffectCustomExecutionParameters(GameplayEffectSpec spec, AbilitySystemComponent target, GameplayTagContainer passedInTags)
        {
            OwningSpec = spec;
            TargetAbilitySystemComponent = target;
            PassedInTags = passedInTags ?? GameplayTagContainer.Empty;
        }

        public bool AttemptCalculateCapturedAttributeMagnitude(GameplayEffectAttributeCaptureDefinition definition, out float magnitude) =>
            OwningSpec.AttemptCalculateCapturedAttributeMagnitude(definition, AttributeBasedFloatCalculationType.AttributeMagnitude, out magnitude);

        public bool AttemptCalculateCapturedAttributeBaseValue(GameplayEffectAttributeCaptureDefinition definition, out float magnitude) =>
            OwningSpec.AttemptCalculateCapturedAttributeMagnitude(definition, AttributeBasedFloatCalculationType.AttributeBaseValue, out magnitude);
    }

    /// <summary>언리얼 FGameplayEffectCustomExecutionOutput.</summary>
    public sealed class GameplayEffectCustomExecutionOutput
    {
        internal readonly List<GameplayModifierEvaluatedData> OutputModifiers = new List<GameplayModifierEvaluatedData>(4);

        public bool ShouldTriggerConditionalGameplayEffects { get; private set; }
        public bool AreGameplayCuesHandledManually { get; private set; }

        public void AddOutputModifier(GameplayModifierEvaluatedData data) => OutputModifiers.Add(data);
        public void MarkConditionalGameplayEffectsToTrigger() => ShouldTriggerConditionalGameplayEffects = true;
        public void MarkGameplayCuesHandledManually() => AreGameplayCuesHandledManually = true;
    }

    /// <summary>언리얼 FGameplayModifierEvaluatedData. 계산을 끝낸 모디파이어 하나.</summary>
    public sealed class GameplayModifierEvaluatedData
    {
        public GameplayAttribute Attribute;
        public GameplayModOp ModifierOp;
        public float Magnitude;
        public bool IsValid = true;

        public GameplayModifierEvaluatedData(GameplayAttribute attribute, GameplayModOp op, float magnitude)
        {
            Attribute = attribute;
            ModifierOp = op;
            Magnitude = magnitude;
        }

        public override string ToString() => $"{Attribute} {ModifierOp} {Magnitude:0.##}";
    }

    /// <summary>언리얼 FGameplayEffectModCallbackData. Pre/PostGameplayEffectExecute 에 넘어가는 정보.</summary>
    public sealed class GameplayEffectModCallbackData
    {
        public readonly GameplayEffectSpec EffectSpec;
        public readonly GameplayModifierEvaluatedData EvaluatedData;
        public readonly AbilitySystemComponent Target;

        public GameplayEffectModCallbackData(GameplayEffectSpec spec, GameplayModifierEvaluatedData evaluated, AbilitySystemComponent target)
        {
            EffectSpec = spec;
            EvaluatedData = evaluated;
            Target = target;
        }
    }
}
