using System;
using System.Collections.Generic;
using UnityEngine;

namespace Framework.Ability
{
    /// <summary>
    /// 언리얼 UAbilitySystemComponent 대응. 한 액터의 속성 / 태그 / 지속 이펙트 / 어빌리티를 소유한다.
    ///
    /// 현재는 오프라인(로컬 권한) 기준으로 동작한다. 멀티플레이 단계에서는
    /// ApplyGameplayEffectSpecToSelf / TryActivateAbility 를 서버 권한 경로로 감싸는 방식으로 확장한다.
    /// </summary>
    public class AbilitySystemComponent : MonoBehaviour
    {
        public AttributeSet Attributes { get; private set; }
        public readonly GameplayTagCountContainer Tags = new GameplayTagCountContainer();

        /// <summary>쿨다운 배율 (공격속도/쿨감 속성을 게임 쪽에서 연결).</summary>
        public Func<float> CooldownScaleProvider;

        public event Action<ActiveGameplayEffect> OnEffectAdded;
        public event Action<ActiveGameplayEffect> OnEffectRemoved;
        /// <summary>인스턴트/주기 실행으로 속성이 바뀐 직후 (스펙, 바뀐 속성).</summary>
        public event Action<GameplayEffectSpec, EvaluatedModifier> OnEffectExecuted;
        public event Action<GameplayAbility> OnAbilityActivated;
        public event Action<GameplayAbility, bool> OnAbilityEnded;
        /// <summary>(이벤트 태그, 페이로드) 언리얼 SendGameplayEventToActor.</summary>
        public event Action<int, GameplayCueParams> OnGameplayEvent;

        private readonly List<ActiveGameplayEffect> ActiveEffects = new List<ActiveGameplayEffect>();
        private readonly List<ActiveGameplayEffect> EffectBuffer = new List<ActiveGameplayEffect>();
        private readonly List<GameplayAbility> Abilities = new List<GameplayAbility>();
        private readonly List<EvaluatedModifier> EvaluatedBuffer = new List<EvaluatedModifier>();
        private readonly GameplayTagCountContainer BlockedAbilityTags = new GameplayTagCountContainer();
        private int NextHandle = 1;

        public IReadOnlyList<ActiveGameplayEffect> Effects
        {
            get { return ActiveEffects; }
        }

        public IReadOnlyList<GameplayAbility> GrantedAbilities
        {
            get { return Abilities; }
        }

        public void InitAttributes(AttributeSet InAttributes)
        {
            Attributes = InAttributes;
            Attributes.Bind(this);
        }

        protected virtual void Update()
        {
            Tick(Time.deltaTime);
        }

        public void Tick(float InDeltaTime)
        {
            tickEffects(InDeltaTime);

            for (int i = 0; i < Abilities.Count; ++i)
            {
                Abilities[i].Tick(InDeltaTime);
            }
        }

        // ------------------------------------------------------------------ Effects

        public GameplayEffectSpec MakeOutgoingSpec(GameplayEffectDef InDef, int InAbilityId)
        {
            return new GameplayEffectSpec(InDef, this, InAbilityId);
        }

        public int ApplyGameplayEffectSpecToTarget(GameplayEffectSpec InSpec, AbilitySystemComponent InTarget)
        {
            if (InTarget == null)
            {
                return 0;
            }

            return InTarget.ApplyGameplayEffectSpecToSelf(InSpec);
        }

        /// <summary>적용된 지속형 이펙트 핸들을 돌려준다. 인스턴트이거나 거부되면 0.</summary>
        public int ApplyGameplayEffectSpecToSelf(GameplayEffectSpec InSpec)
        {
            if (InSpec == null || InSpec.Def == null || Attributes == null)
            {
                return 0;
            }

            var def = InSpec.Def;
            var owned = Tags;
            if (owned.HasAll(def.ApplicationRequiredTags) == false)
            {
                return 0;
            }

            if (def.ApplicationBlockedTags.IsEmpty == false && owned.HasAny(def.ApplicationBlockedTags) == true)
            {
                return 0;
            }

            if (def.RemoveEffectsWithTags.IsEmpty == false)
            {
                RemoveEffectsWithGrantedTags(def.RemoveEffectsWithTags);
            }

            if (def.DurationPolicy == EDurationPolicy.Instant)
            {
                executeEffect(InSpec);
                GameplayCueManager.Execute(def.CueId, makeCueParams(InSpec));
                return 0;
            }

            var existing = findStackTarget(InSpec);
            if (existing != null)
            {
                if (def.StackPolicy == EStackPolicy.Stack && existing.StackCount < def.MaxStack)
                {
                    existing.StackCount += 1;
                    Attributes.RemoveModifiers(existing.Handle);
                    addModifiers(existing);
                }

                existing.Remaining = InSpec.Duration;
                return existing.Handle;
            }

            var active = new ActiveGameplayEffect();
            active.Handle = NextHandle++;
            active.Spec = InSpec;
            active.Remaining = InSpec.Duration;
            active.Magnitudes = new float[def.Modifiers.Length];
            for (int i = 0; i < def.Modifiers.Length; ++i)
            {
                active.Magnitudes[i] = InSpec.CalculateMagnitude(def.Modifiers[i], this);
            }

            ActiveEffects.Add(active);

            bool isPeriodic = def.Period > 0.0f;
            if (isPeriodic == false)
            {
                addModifiers(active);
            }

            Tags.AddTags(def.GrantedTags);
            GameplayCueManager.Add(def.CueId, makeCueParams(InSpec));
            OnEffectAdded?.Invoke(active);

            if (isPeriodic == true && def.ExecutePeriodicOnApply == true)
            {
                executeEffect(InSpec);
            }

            return active.Handle;
        }

        public void RemoveActiveEffect(int InHandle)
        {
            for (int i = 0; i < ActiveEffects.Count; ++i)
            {
                if (ActiveEffects[i].Handle == InHandle)
                {
                    removeAt(i);
                    return;
                }
            }
        }

        public void RemoveEffectsWithGrantedTags(GameplayTagContainer InTags)
        {
            for (int i = ActiveEffects.Count - 1; i >= 0; --i)
            {
                if (i >= ActiveEffects.Count)
                {
                    continue;
                }

                if (ActiveEffects[i].Spec.Def.GrantedTags.HasAny(InTags) == true)
                {
                    removeAt(i);
                }
            }
        }

        public void RemoveEffectsByDefId(int InDefId)
        {
            for (int i = ActiveEffects.Count - 1; i >= 0; --i)
            {
                if (i < ActiveEffects.Count && ActiveEffects[i].Spec.Def.Id == InDefId)
                {
                    removeAt(i);
                }
            }
        }

        public void RemoveAllEffects()
        {
            for (int i = ActiveEffects.Count - 1; i >= 0; --i)
            {
                if (i < ActiveEffects.Count)
                {
                    removeAt(i);
                }
            }
        }

        public ActiveGameplayEffect FindEffectByDefId(int InDefId)
        {
            for (int i = 0; i < ActiveEffects.Count; ++i)
            {
                if (ActiveEffects[i].Spec.Def.Id == InDefId)
                {
                    return ActiveEffects[i];
                }
            }

            return null;
        }

        public float GetCooldownRemaining(int InAbilityId)
        {
            float remaining = 0.0f;
            for (int i = 0; i < ActiveEffects.Count; ++i)
            {
                var effect = ActiveEffects[i];
                if (effect.Spec.Def.IsCooldown == true && effect.Spec.AbilityId == InAbilityId)
                {
                    remaining = Mathf.Max(remaining, effect.Remaining);
                }
            }

            return remaining;
        }

        public float GetCooldownDuration(int InAbilityId)
        {
            for (int i = 0; i < ActiveEffects.Count; ++i)
            {
                var effect = ActiveEffects[i];
                if (effect.Spec.Def.IsCooldown == true && effect.Spec.AbilityId == InAbilityId)
                {
                    return effect.Spec.Duration;
                }
            }

            return 0.0f;
        }

        /// <summary>치트/리스폰용. 모든 쿨다운 이펙트 제거.</summary>
        public void ClearCooldowns()
        {
            for (int i = ActiveEffects.Count - 1; i >= 0; --i)
            {
                if (i < ActiveEffects.Count && ActiveEffects[i].Spec.Def.IsCooldown == true)
                {
                    removeAt(i);
                }
            }
        }

        public float GetCooldownScale()
        {
            if (CooldownScaleProvider == null)
            {
                return 1.0f;
            }

            return CooldownScaleProvider.Invoke();
        }

        // ------------------------------------------------------------------ Abilities

        public T GiveAbility<T>(T InAbility) where T : GameplayAbility
        {
            Abilities.Add(InAbility);
            InAbility.Bind(this);
            return InAbility;
        }

        public void ClearAbility(GameplayAbility InAbility)
        {
            if (InAbility == null)
            {
                return;
            }

            InAbility.CancelAbility();
            Abilities.Remove(InAbility);
        }

        public GameplayAbility FindAbility(int InAbilityId)
        {
            for (int i = 0; i < Abilities.Count; ++i)
            {
                if (Abilities[i].AbilityId == InAbilityId)
                {
                    return Abilities[i];
                }
            }

            return null;
        }

        public bool TryActivateAbility(GameplayAbility InAbility, AbilityActivationInfo InInfo, out EActivateFailReason OutReason)
        {
            OutReason = EActivateFailReason.Custom;
            if (InAbility == null || InAbility.Owner != this)
            {
                return false;
            }

            if (InAbility.CanActivate(out OutReason) == false)
            {
                return false;
            }

            InAbility.InternalActivate(InInfo);
            OnAbilityActivated?.Invoke(InAbility);
            return true;
        }

        public void CancelAbilitiesWithTags(GameplayTagContainer InTags, GameplayAbility InIgnore)
        {
            if (InTags.IsEmpty == true)
            {
                return;
            }

            for (int i = 0; i < Abilities.Count; ++i)
            {
                var ability = Abilities[i];
                if (ability == InIgnore || ability.IsActive == false)
                {
                    continue;
                }

                if (ability.AbilityTags.HasAny(InTags) == true)
                {
                    ability.CancelAbility();
                }
            }
        }

        public void CancelAllAbilities()
        {
            for (int i = 0; i < Abilities.Count; ++i)
            {
                if (Abilities[i].IsActive == true)
                {
                    Abilities[i].CancelAbility();
                }
            }
        }

        /// <summary>
        /// 언리얼 AreAbilityTagsBlocked: 어빌리티 태그(부모 포함)가 "명시적으로 차단된 태그" 중 하나에 매칭되면 차단.
        /// 차단 목록은 부모로 펼치지 않는다 → Ability_Weapon 차단은 무기 어빌리티만, Ability 차단은 전부 막는다.
        /// </summary>
        public bool IsAbilityBlocked(GameplayTagContainer InAbilityTags)
        {
            return InAbilityTags != null && InAbilityTags.HasAnyExplicitOf(BlockedAbilityTags);
        }

        public void AddBlockedAbilityTags(GameplayTagContainer InTags)
        {
            BlockedAbilityTags.AddTags(InTags);
        }

        public void RemoveBlockedAbilityTags(GameplayTagContainer InTags)
        {
            BlockedAbilityTags.RemoveTags(InTags);
        }

        public void NotifyAbilityEnded(GameplayAbility InAbility, bool InWasCancelled)
        {
            OnAbilityEnded?.Invoke(InAbility, InWasCancelled);
        }

        public void SendGameplayEvent(int InEventTag, GameplayCueParams InPayload)
        {
            OnGameplayEvent?.Invoke(InEventTag, InPayload);
        }

        // ------------------------------------------------------------------ internal

        private void tickEffects(float InDeltaTime)
        {
            if (ActiveEffects.Count == 0)
            {
                return;
            }

            EffectBuffer.Clear();
            EffectBuffer.AddRange(ActiveEffects);
            for (int i = 0; i < EffectBuffer.Count; ++i)
            {
                var effect = EffectBuffer[i];
                if (ActiveEffects.Contains(effect) == false)
                {
                    continue;
                }

                var def = effect.Spec.Def;
                if (def.Period > 0.0f)
                {
                    effect.PeriodTimer += InDeltaTime;
                    while (effect.PeriodTimer >= def.Period)
                    {
                        effect.PeriodTimer -= def.Period;
                        // 주기 실행은 지속 큐(OnActive)로 이미 표시 중이므로 Execute 큐를 다시 내지 않는다.
                        for (int s = 0; s < effect.StackCount; ++s)
                        {
                            executeEffect(effect.Spec);
                        }
                    }
                }

                if (def.DurationPolicy != EDurationPolicy.HasDuration)
                {
                    continue;
                }

                effect.Remaining -= InDeltaTime;
                if (effect.Remaining <= 0.0f)
                {
                    RemoveActiveEffect(effect.Handle);
                }
            }
        }

        private void executeEffect(GameplayEffectSpec InSpec)
        {
            var def = InSpec.Def;
            EvaluatedBuffer.Clear();
            for (int i = 0; i < def.Modifiers.Length; ++i)
            {
                var modifier = def.Modifiers[i];
                var evaluated = new EvaluatedModifier();
                evaluated.Attribute = modifier.Attribute;
                evaluated.Op = modifier.Op;
                evaluated.Magnitude = InSpec.CalculateMagnitude(modifier, this);
                EvaluatedBuffer.Add(evaluated);
            }

            if (def.ExecutionId != 0)
            {
                var execution = ExecutionRegistry.Get(def.ExecutionId);
                if (execution != null)
                {
                    execution.Execute(InSpec, this, EvaluatedBuffer);
                }
                else
                {
                    Log.Warning($"ExecutionId {def.ExecutionId} is not registered");
                }
            }

            // 실행 중 콜백에서 다른 이펙트가 적용될 수 있으므로 복사본으로 순회한다.
            var evaluatedCopy = EvaluatedBuffer.ToArray();
            for (int i = 0; i < evaluatedCopy.Length; ++i)
            {
                var evaluated = evaluatedCopy[i];
                Attributes.ApplyToBase(evaluated.Attribute, evaluated.Op, evaluated.Magnitude);

                var data = new EffectExecuteData();
                data.Spec = InSpec;
                data.Target = this;
                data.Evaluated = evaluated;
                Attributes.PostGameplayEffectExecute(data);
                OnEffectExecuted?.Invoke(InSpec, evaluated);
            }
        }

        private void addModifiers(ActiveGameplayEffect InEffect)
        {
            var modifiers = InEffect.Spec.Def.Modifiers;
            for (int i = 0; i < modifiers.Length; ++i)
            {
                Attributes.AddModifier(modifiers[i].Attribute, InEffect.Handle, modifiers[i].Op, InEffect.Magnitudes[i] * InEffect.StackCount);
            }
        }

        private ActiveGameplayEffect findStackTarget(GameplayEffectSpec InSpec)
        {
            if (InSpec.Def.StackPolicy == EStackPolicy.None)
            {
                return null;
            }

            for (int i = 0; i < ActiveEffects.Count; ++i)
            {
                if (ActiveEffects[i].Spec.Def == InSpec.Def)
                {
                    return ActiveEffects[i];
                }
            }

            return null;
        }

        private void removeAt(int InIndex)
        {
            var effect = ActiveEffects[InIndex];
            ActiveEffects.RemoveAt(InIndex);
            Attributes.RemoveModifiers(effect.Handle);
            Tags.RemoveTags(effect.Spec.Def.GrantedTags);
            GameplayCueManager.Remove(effect.Spec.Def.CueId, makeCueParams(effect.Spec));
            OnEffectRemoved?.Invoke(effect);
        }

        private GameplayCueParams makeCueParams(GameplayEffectSpec InSpec)
        {
            var cue = new GameplayCueParams();
            cue.Source = InSpec.Source;
            cue.Target = this;
            cue.Location = InSpec.HitPoint == Vector3.zero ? transform.position : InSpec.HitPoint;
            cue.Direction = InSpec.HitDirection;
            return cue;
        }
    }
}
