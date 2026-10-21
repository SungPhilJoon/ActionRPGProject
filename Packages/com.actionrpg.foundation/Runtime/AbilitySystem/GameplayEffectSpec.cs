using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

namespace ActionRPG.Core.AbilitySystem
{
    /// <summary>
    /// 언리얼 FGameplayEffectContext. "누가, 무엇으로, 어디서" 이펙트를 걸었는지.
    /// 서버 안에서만 쓰이고 네트워크로는 큐 파라미터에 필요한 값만 추려서 보낸다.
    /// </summary>
    public sealed class GameplayEffectContext
    {
        public AbilitySystemComponent InstigatorAbilitySystemComponent { get; private set; }
        public GameObject Instigator { get; private set; }
        public GameObject EffectCauser { get; private set; }
        public Object SourceObject { get; private set; }
        public GameplayAbility Ability { get; private set; }
        public int AbilityLevel { get; private set; } = 1;

        public bool HasOrigin { get; private set; }
        public Vector3 Origin { get; private set; }

        public bool HasHitResult { get; private set; }
        public Vector3 HitLocation { get; private set; }
        /// <summary>맞은 방향(공격자 → 대상). 넉백 방향으로도 쓴다.</summary>
        public Vector3 HitNormal { get; private set; }

        /// <summary>
        /// 프로젝트 확장 필드. 언리얼에서는 FGameplayEffectContext 를 상속해 넣는 값(라이라의 크리티컬 여부 등)이다.
        /// Execution 이 결정하고 AttributeSet / 큐가 읽는다.
        /// </summary>
        public bool IsCriticalHit { get; set; }

        public void AddInstigator(GameObject instigator, GameObject effectCauser)
        {
            Instigator = instigator;
            EffectCauser = effectCauser != null ? effectCauser : instigator;
            InstigatorAbilitySystemComponent = AbilitySystemStatics.GetAbilitySystemComponent(instigator);
        }

        public void SetAbility(GameplayAbility ability, int level)
        {
            Ability = ability;
            AbilityLevel = level;
        }

        public void AddSourceObject(Object sourceObject) => SourceObject = sourceObject;

        public void AddOrigin(Vector3 origin)
        {
            HasOrigin = true;
            Origin = origin;
        }

        public void AddHitResult(Vector3 location, Vector3 normal)
        {
            HasHitResult = true;
            HitLocation = location;
            HitNormal = normal;
        }

        public ulong InstigatorNetworkObjectId => NetworkIdOf(Instigator);
        public ulong EffectCauserNetworkObjectId => NetworkIdOf(EffectCauser);

        private static ulong NetworkIdOf(GameObject go)
        {
            if (go == null) return 0UL;
            var netObject = go.GetComponent<NetworkObject>();
            return netObject != null && netObject.IsSpawned ? netObject.NetworkObjectId : 0UL;
        }

        public GameplayEffectContext Duplicate() => (GameplayEffectContext)MemberwiseClone();
    }

    /// <summary>
    /// 언리얼 FGameplayEffectSpec. "이 이펙트를 이 레벨로, 이 값들로 적용하겠다"는 한 번의 적용 계획.
    /// 시전 시점에 만들어 두었다가(발사체에 실어 보내는 등) 나중에 대상에게 적용할 수 있다.
    /// </summary>
    public sealed class GameplayEffectSpec
    {
        private struct CapturedValue
        {
            public float Base;
            public float Current;
        }

        public GameplayEffect Def { get; }
        public GameplayEffectContext Context { get; private set; }
        public float Level { get; private set; }

        /// <summary>초 단위 지속시간. Instant 는 0, Infinite 는 -1.</summary>
        public float Duration { get; private set; }
        public float Period { get; private set; }
        public float ChanceToApplyToTarget { get; private set; } = 1f;
        public int StackCount { get; set; } = 1;

        /// <summary>스펙을 만든 순간 시전자가 가진 태그.</summary>
        public readonly GameplayTagContainer CapturedSourceTags = new GameplayTagContainer();
        /// <summary>적용 순간 대상이 가진 태그.</summary>
        public readonly GameplayTagContainer CapturedTargetTags = new GameplayTagContainer();
        /// <summary>에셋의 GrantedTags 에 더해 이 스펙만 추가로 부여하는 태그 (스킬별 쿨다운 태그 등).</summary>
        public readonly GameplayTagContainer DynamicGrantedTags = new GameplayTagContainer();
        /// <summary>에셋의 AssetTags 에 더해 이 스펙만 갖는 태그 (피해 속성 등).</summary>
        public readonly GameplayTagContainer DynamicAssetTags = new GameplayTagContainer();

        /// <summary>적용 대상. 적용·실행 중에만 채워진다.</summary>
        public AbilitySystemComponent TargetAbilitySystemComponent { get; internal set; }

        private readonly Dictionary<int, float> _setByCaller = new Dictionary<int, float>(4);
        private readonly Dictionary<string, CapturedValue> _sourceSnapshot = new Dictionary<string, CapturedValue>(4);
        private float[] _modifierMagnitudes;
        private bool _durationLocked;

        public GameplayEffectSpec(GameplayEffect def, GameplayEffectContext context, float level)
        {
            Def = def;
            Context = context ?? new GameplayEffectContext();
            Level = level;
            Duration = def == null ? 0f : def.DurationPolicy == GameplayEffectDurationType.Infinite ? -1f : 0f;
            if (def == null) return;

            Period = def.Period.GetValueAtLevel(level);
            ChanceToApplyToTarget = def.ChanceToApplyToTarget.GetValueAtLevel(level);

            var source = Context.InstigatorAbilitySystemComponent;
            if (source != null)
            {
                source.GetOwnedGameplayTags(CapturedSourceTags);
                CaptureSourceSnapshots(source);
            }
        }

        private GameplayEffectSpec(GameplayEffectSpec other)
        {
            Def = other.Def;
            Context = other.Context.Duplicate();
            Level = other.Level;
            Duration = other.Duration;
            Period = other.Period;
            ChanceToApplyToTarget = other.ChanceToApplyToTarget;
            StackCount = other.StackCount;
            CapturedSourceTags.AppendTags(other.CapturedSourceTags);
            DynamicGrantedTags.AppendTags(other.DynamicGrantedTags);
            DynamicAssetTags.AppendTags(other.DynamicAssetTags);
            foreach (var pair in other._setByCaller) _setByCaller[pair.Key] = pair.Value;
            foreach (var pair in other._sourceSnapshot) _sourceSnapshot[pair.Key] = pair.Value;
            _durationLocked = other._durationLocked;
        }

        /// <summary>깊은 복사. 발사체가 맞힐 때마다 맞은 위치를 다르게 넣으려면 복사해서 쓴다.</summary>
        public GameplayEffectSpec Duplicate() => new GameplayEffectSpec(this);

        public void SetContext(GameplayEffectContext context) => Context = context ?? new GameplayEffectContext();
        public void SetLevel(float level) => Level = level;

        /// <summary>지속시간을 직접 정한다. lockDuration 이면 적용 시 다시 계산하지 않는다.</summary>
        public void SetDuration(float duration, bool lockDuration)
        {
            Duration = duration;
            _durationLocked = lockDuration;
        }

        // ---------------------------------------------------------------- SetByCaller

        public void SetSetByCallerMagnitude(GameplayTag dataTag, float magnitude)
        {
            if (!dataTag.IsValid) return;
            _setByCaller[dataTag.Hash] = magnitude;
        }

        public float GetSetByCallerMagnitude(GameplayTag dataTag, bool warnIfNotFound = true, float defaultIfNotFound = 0f)
        {
            if (dataTag.IsValid && _setByCaller.TryGetValue(dataTag.Hash, out float value)) return value;
            if (warnIfNotFound)
                Debug.LogWarning($"[GAS] {(Def != null ? Def.name : "?")} 스펙에 SetByCaller '{dataTag}' 값이 없습니다. {defaultIfNotFound} 을 씁니다.");
            return defaultIfNotFound;
        }

        public bool HasSetByCallerMagnitude(GameplayTag dataTag) => dataTag.IsValid && _setByCaller.ContainsKey(dataTag.Hash);

        // ---------------------------------------------------------------- 태그

        public void GetAllGrantedTags(GameplayTagContainer output)
        {
            if (Def != null) output.AppendTags(Def.GrantedTags);
            output.AppendTags(DynamicGrantedTags);
        }

        public void GetAllAssetTags(GameplayTagContainer output)
        {
            if (Def != null) output.AppendTags(Def.AssetTags);
            output.AppendTags(DynamicAssetTags);
        }

        // ---------------------------------------------------------------- 계산

        /// <summary>
        /// 적용 직전(지속시간 포함), 그리고 주기 실행 때마다(지속시간 제외) 호출된다.
        /// 스냅샷이 아닌 어트리뷰트 캡처는 이때의 값을 다시 읽는다 (언리얼과 같은 동작).
        /// </summary>
        internal void CalculateModifierMagnitudes(bool includeDuration = true)
        {
            if (Def == null) return;
            var modifiers = Def.Modifiers;
            if (_modifierMagnitudes == null || _modifierMagnitudes.Length != modifiers.Count)
                _modifierMagnitudes = new float[modifiers.Count];
            for (int i = 0; i < modifiers.Count; i++)
            {
                if (!modifiers[i].ModifierMagnitude.AttemptCalculateMagnitude(this, out float magnitude))
                {
                    magnitude = 0f;
                    Debug.LogWarning($"[GAS] {Def.name} 의 {i}번 모디파이어({modifiers[i].Attribute}) 크기를 계산하지 못했습니다.");
                }
                _modifierMagnitudes[i] = magnitude;
            }
            if (includeDuration) CalculateDuration();
        }

        private void CalculateDuration()
        {
            if (_durationLocked || Def == null) return;
            switch (Def.DurationPolicy)
            {
                case GameplayEffectDurationType.Instant:
                    Duration = 0f;
                    break;
                case GameplayEffectDurationType.Infinite:
                    Duration = -1f;
                    break;
                default:
                    Duration = Def.DurationMagnitude.AttemptCalculateMagnitude(this, out float duration) ? Mathf.Max(0f, duration) : 0f;
                    break;
            }
        }

        /// <summary>언리얼 GetModifierMagnitude.</summary>
        public float GetModifierMagnitude(int index, bool factorInStackCount)
        {
            if (_modifierMagnitudes == null || index < 0 || index >= _modifierMagnitudes.Length) return 0f;
            float magnitude = _modifierMagnitudes[index];
            if (!factorInStackCount || StackCount <= 1) return magnitude;
            var op = Def.Modifiers[index].ModifierOp;
            return op switch
            {
                GameplayModOp.Additive => magnitude * StackCount,
                GameplayModOp.Multiplicitive => 1f + (magnitude - 1f) * StackCount,
                GameplayModOp.Division => 1f + (magnitude - 1f) * StackCount,
                _ => magnitude
            };
        }

        /// <summary>어트리뷰트 캡처 값을 읽는다. 소스 스냅샷이면 저장된 값, 아니면 지금 값.</summary>
        public bool AttemptCalculateCapturedAttributeMagnitude(GameplayEffectAttributeCaptureDefinition definition,
            AttributeBasedFloatCalculationType calculationType, out float value)
        {
            value = 0f;
            if (definition == null || !definition.AttributeToCapture.IsValid) return false;

            float baseValue, currentValue;
            if (definition.AttributeSource == GameplayEffectAttributeCaptureSource.Source)
            {
                if (definition.bSnapshot && _sourceSnapshot.TryGetValue(definition.AttributeToCapture.Name, out var captured))
                {
                    baseValue = captured.Base;
                    currentValue = captured.Current;
                }
                else
                {
                    var source = Context.InstigatorAbilitySystemComponent;
                    if (source == null || !source.HasAttributeSetForAttribute(definition.AttributeToCapture))
                    {
                        if (!_sourceSnapshot.TryGetValue(definition.AttributeToCapture.Name, out captured)) return false;
                        baseValue = captured.Base;
                        currentValue = captured.Current;
                    }
                    else
                    {
                        baseValue = source.GetNumericAttributeBase(definition.AttributeToCapture);
                        currentValue = source.GetNumericAttribute(definition.AttributeToCapture);
                    }
                }
            }
            else
            {
                var target = TargetAbilitySystemComponent;
                if (target == null || !target.HasAttributeSetForAttribute(definition.AttributeToCapture)) return false;
                baseValue = target.GetNumericAttributeBase(definition.AttributeToCapture);
                currentValue = target.GetNumericAttribute(definition.AttributeToCapture);
            }

            value = calculationType switch
            {
                AttributeBasedFloatCalculationType.AttributeBaseValue => baseValue,
                AttributeBasedFloatCalculationType.AttributeBonusMagnitude => currentValue - baseValue,
                _ => currentValue
            };
            return true;
        }

        private void CaptureSourceSnapshots(AbilitySystemComponent source)
        {
            var definitions = new List<GameplayEffectAttributeCaptureDefinition>(8);
            Def.DurationMagnitude.GetAttributeCaptureDefinitions(definitions);
            for (int i = 0; i < Def.Modifiers.Count; i++) Def.Modifiers[i].ModifierMagnitude.GetAttributeCaptureDefinitions(definitions);
            for (int i = 0; i < Def.Executions.Count; i++)
            {
                var calc = Def.Executions[i].CalculationClass;
                if (calc != null) definitions.AddRange(calc.RelevantAttributesToCapture);
            }

            for (int i = 0; i < definitions.Count; i++)
            {
                var definition = definitions[i];
                if (definition == null || definition.AttributeSource != GameplayEffectAttributeCaptureSource.Source) continue;
                var attribute = definition.AttributeToCapture;
                if (!attribute.IsValid || !source.HasAttributeSetForAttribute(attribute)) continue;
                _sourceSnapshot[attribute.Name] = new CapturedValue
                {
                    Base = source.GetNumericAttributeBase(attribute),
                    Current = source.GetNumericAttribute(attribute)
                };
            }
        }

        public override string ToString() => $"Spec({(Def != null ? Def.name : "null")}, Lv {Level}, x{StackCount})";
    }
}
