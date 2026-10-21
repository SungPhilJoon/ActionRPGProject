using System;
using System.Collections.Generic;
using UnityEngine;

namespace ActionRPG.Core.AbilitySystem
{
    public partial class AbilitySystemComponent
    {
        private readonly List<ActiveGameplayEffect> _activeEffects = new List<ActiveGameplayEffect>(16);
        private readonly List<ActiveGameplayEffect> _effectScratch = new List<ActiveGameplayEffect>(16);
        private int _nextServerEffectHandle;
        private int _nextLocalEffectHandle;

        /// <summary>(시전자 ASC, 스펙, 핸들) — 이 ASC 에 이펙트가 적용됐을 때. Instant 면 핸들이 무효다.</summary>
        public event Action<AbilitySystemComponent, GameplayEffectSpec, ActiveGameplayEffectHandle> OnGameplayEffectAppliedDelegateToSelf;
        /// <summary>(대상 ASC, 스펙, 핸들) — 이 ASC 가 남에게 이펙트를 적용했을 때.</summary>
        public event Action<AbilitySystemComponent, GameplayEffectSpec, ActiveGameplayEffectHandle> OnGameplayEffectAppliedDelegateToTarget;
        /// <summary>지속형 이펙트가 추가됐을 때 (서버 · 예측).</summary>
        public event Action<ActiveGameplayEffect> OnActiveGameplayEffectAddedDelegateToSelf;
        /// <summary>지속형 이펙트가 제거됐을 때 (서버 · 예측).</summary>
        public event Action<ActiveGameplayEffect> OnAnyGameplayEffectRemovedDelegate;
        /// <summary>활성 이펙트 목록이 바뀌었을 때. 클라이언트의 복제 사본 갱신에도 호출된다. UI 용.</summary>
        public event Action OnActiveGameplayEffectsChanged;

        // ================================================================ 스펙 생성

        /// <summary>언리얼 MakeEffectContext. 이 ASC 를 시전자로 한다.</summary>
        public GameplayEffectContext MakeEffectContext()
        {
            var context = new GameplayEffectContext();
            context.AddInstigator(AbilityActorInfo.OwnerActor, AbilityActorInfo.AvatarActor);
            return context;
        }

        /// <summary>언리얼 MakeOutgoingSpec.</summary>
        public GameplayEffectSpec MakeOutgoingSpec(GameplayEffect effect, float level, GameplayEffectContext context)
        {
            if (effect == null) return null;
            return new GameplayEffectSpec(effect, context ?? MakeEffectContext(), level);
        }

        // ================================================================ 적용

        public ActiveGameplayEffectHandle ApplyGameplayEffectToSelf(GameplayEffect effect, float level, GameplayEffectContext context = null) =>
            ApplyGameplayEffectSpecToSelf(MakeOutgoingSpec(effect, level, context));

        public ActiveGameplayEffectHandle ApplyGameplayEffectToTarget(GameplayEffect effect, AbilitySystemComponent target, float level,
            GameplayEffectContext context = null) =>
            ApplyGameplayEffectSpecToTarget(MakeOutgoingSpec(effect, level, context), target);

        /// <summary>언리얼 ApplyGameplayEffectSpecToTarget. 대상의 ApplyGameplayEffectSpecToSelf 로 넘긴다.</summary>
        public ActiveGameplayEffectHandle ApplyGameplayEffectSpecToTarget(GameplayEffectSpec spec, AbilitySystemComponent target,
            PredictionKey predictionKey = default)
        {
            if (target == null || spec == null) return ActiveGameplayEffectHandle.Invalid;
            if (target != this && !IsAuthority) return ActiveGameplayEffectHandle.Invalid;
            return target.ApplyGameplayEffectSpecToSelf(spec, target == this ? predictionKey : default);
        }

        /// <summary>
        /// 언리얼 ApplyGameplayEffectSpecToSelf.
        /// 서버면 실제로 적용하고, 클라이언트는 유효한 예측 키가 있을 때만 "예측 적용"한다.
        /// </summary>
        public ActiveGameplayEffectHandle ApplyGameplayEffectSpecToSelf(GameplayEffectSpec spec, PredictionKey predictionKey = default)
        {
            if (spec == null || spec.Def == null) return ActiveGameplayEffectHandle.Invalid;

            bool authority = IsAuthority;
            bool predicting = !authority && predictionKey.IsValidKey;
            if (!authority && !predicting) return ActiveGameplayEffectHandle.Invalid;

            if (IsImmuneTo(spec)) return ActiveGameplayEffectHandle.Invalid;

            var applied = spec.Duplicate();
            applied.TargetAbilitySystemComponent = this;
            applied.CapturedTargetTags.Reset();
            GetOwnedGameplayTags(applied.CapturedTargetTags);

            var def = applied.Def;
            if (!def.ApplicationTagRequirements.RequirementsMet(_ownedTags)) return ActiveGameplayEffectHandle.Invalid;
            if (!def.RemovalTagRequirements.IsEmpty && def.RemovalTagRequirements.RequirementsMet(_ownedTags))
                return ActiveGameplayEffectHandle.Invalid;
            if (authority && applied.ChanceToApplyToTarget < 0.9999f && UnityEngine.Random.value > applied.ChanceToApplyToTarget)
                return ActiveGameplayEffectHandle.Invalid;

            applied.CalculateModifierMagnitudes();

            var handle = ActiveGameplayEffectHandle.Invalid;
            bool instant = def.DurationPolicy == GameplayEffectDurationType.Instant ||
                           (def.DurationPolicy == GameplayEffectDurationType.HasDuration && applied.Duration <= 0f);

            if (instant)
            {
                if (authority) ExecuteGameplayEffect(applied);
                else PredictInstantEffect(applied, predictionKey);
            }
            else
            {
                var active = ApplyDurationEffect(applied, predictionKey, authority);
                if (active != null) handle = active.Handle;
            }

            if (authority && !def.RemoveGameplayEffectsWithTags.IsEmpty)
            {
                var query = GameplayEffectQuery.MakeQuery_MatchAnyOwningTags(def.RemoveGameplayEffectsWithTags);
                var keep = handle;
                query.CustomMatch = e => e.Handle != keep;
                RemoveActiveEffects(query);
            }

            OnGameplayEffectAppliedDelegateToSelf?.Invoke(applied.Context.InstigatorAbilitySystemComponent, applied, handle);
            var source = applied.Context.InstigatorAbilitySystemComponent;
            if (source != null) source.OnGameplayEffectAppliedDelegateToTarget?.Invoke(this, applied, handle);
            return handle;
        }

        /// <summary>활성 이펙트의 면역 태그에 걸리는지.</summary>
        private bool IsImmuneTo(GameplayEffectSpec spec)
        {
            if (_activeEffects.Count == 0) return false;
            _scratchTags.Reset();
            spec.GetAllAssetTags(_scratchTags);
            spec.GetAllGrantedTags(_scratchTags);
            for (int i = 0; i < _activeEffects.Count; i++)
            {
                var active = _activeEffects[i];
                if (active.IsReplicatedProxy || active.IsPendingRemove || active.IsInhibited) continue;
                var immunity = active.Def.GrantedApplicationImmunityTags;
                if (immunity.IsEmpty) continue;
                if (_scratchTags.HasAny(immunity) || spec.CapturedSourceTags.HasAny(immunity)) return true;
            }
            return false;
        }

        // ================================================================ 실행 (Instant / Periodic)

        /// <summary>서버 전용. 모디파이어와 Execution 을 계산해 BaseValue 에 반영한다.</summary>
        private void ExecuteGameplayEffect(GameplayEffectSpec spec)
        {
            var def = spec.Def;
            var evaluated = new List<GameplayModifierEvaluatedData>(def.Modifiers.Count + 2);

            for (int i = 0; i < def.Modifiers.Count; i++)
            {
                var modifier = def.Modifiers[i];
                if (!ModifierTagRequirementsMet(modifier, spec)) continue;
                evaluated.Add(new GameplayModifierEvaluatedData(modifier.Attribute, modifier.ModifierOp, spec.GetModifierMagnitude(i, true)));
            }

            bool cuesHandledManually = false;
            for (int i = 0; i < def.Executions.Count; i++)
            {
                var execution = def.Executions[i];
                if (execution.CalculationClass == null) continue;
                var output = new GameplayEffectCustomExecutionOutput();
                execution.CalculationClass.Execute(new GameplayEffectCustomExecutionParameters(spec, this, execution.PassedInTags), output);
                evaluated.AddRange(output.OutputModifiers);
                cuesHandledManually |= output.AreGameplayCuesHandledManually;

                if (output.ShouldTriggerConditionalGameplayEffects)
                {
                    for (int c = 0; c < execution.ConditionalGameplayEffects.Count; c++)
                    {
                        var conditional = execution.ConditionalGameplayEffects[c];
                        if (conditional == null) continue;
                        var conditionalSpec = new GameplayEffectSpec(conditional, spec.Context.Duplicate(), spec.Level);
                        ApplyGameplayEffectSpecToSelf(conditionalSpec);
                    }
                }
            }

            for (int i = 0; i < evaluated.Count; i++) InternalExecuteMod(spec, evaluated[i]);

            if (!cuesHandledManually) InvokeEffectCues(spec, GameplayCueEvent.Executed, evaluated);
        }

        private void InternalExecuteMod(GameplayEffectSpec spec, GameplayModifierEvaluatedData evaluated)
        {
            if (evaluated == null || !evaluated.IsValid) return;
            var entry = FindEntry(evaluated.Attribute);
            if (entry == null) return;

            var data = new GameplayEffectModCallbackData(spec, evaluated, this);
            if (!entry.Set.PreGameplayEffectExecute(data) || !evaluated.IsValid) return;

            float baseValue = entry.Data.BaseValue;
            float newBase = evaluated.ModifierOp switch
            {
                GameplayModOp.Additive => baseValue + evaluated.Magnitude,
                GameplayModOp.Multiplicitive => baseValue * evaluated.Magnitude,
                GameplayModOp.Division => Mathf.Approximately(evaluated.Magnitude, 0f) ? baseValue : baseValue / evaluated.Magnitude,
                GameplayModOp.Override => evaluated.Magnitude,
                _ => baseValue
            };
            InternalSetBase(entry, newBase, data);
            entry.Set.PostGameplayEffectExecute(data);
        }

        private bool ModifierTagRequirementsMet(GameplayModifierInfo modifier, GameplayEffectSpec spec)
        {
            if (!modifier.SourceTags.IsEmpty && !modifier.SourceTags.RequirementsMet(spec.CapturedSourceTags)) return false;
            if (!modifier.TargetTags.IsEmpty && !modifier.TargetTags.RequirementsMet(_ownedTags)) return false;
            return true;
        }

        /// <summary>
        /// 언리얼 ApplyModToAttribute. 서버 전용. 에셋 없이 어트리뷰트 하나를 Instant 로 바꾼다.
        /// Pre/PostGameplayEffectExecute 를 거치므로 메타 어트리뷰트(받을 피해량)에도 쓸 수 있다.
        /// </summary>
        public void ApplyModToAttribute(GameplayAttribute attribute, GameplayModOp op, float magnitude, GameplayEffectContext context = null)
        {
            if (!IsAuthority) return;
            var transient = ScriptableObject.CreateInstance<GameplayEffect>();
            transient.name = "GE_Transient";
            transient.DurationPolicy = GameplayEffectDurationType.Instant;
            transient.Modifiers.Add(new GameplayModifierInfo
            {
                Attribute = attribute,
                ModifierOp = op,
                ModifierMagnitude = GameplayEffectModifierMagnitude.FromScalable(magnitude)
            });
            var spec = new GameplayEffectSpec(transient, context ?? MakeEffectContext(), 1f);
            spec.TargetAbilitySystemComponent = this;
            spec.CalculateModifierMagnitudes();
            ExecuteGameplayEffect(spec);
            Destroy(transient);
        }

        /// <summary>언리얼 CanApplyAttributeModifiers. Additive 모디파이어를 적용해도 음수가 되지 않으면 true (비용 검사).</summary>
        public bool CanApplyAttributeModifiers(GameplayEffectSpec spec)
        {
            if (spec == null || spec.Def == null) return true;
            var test = spec.Duplicate();
            test.TargetAbilitySystemComponent = this;
            test.CalculateModifierMagnitudes();
            for (int i = 0; i < test.Def.Modifiers.Count; i++)
            {
                var modifier = test.Def.Modifiers[i];
                if (modifier.ModifierOp != GameplayModOp.Additive) continue;
                var entry = FindEntry(modifier.Attribute);
                if (entry == null) continue;
                float magnitude = test.GetModifierMagnitude(i, true);
                if (entry.Data.CurrentValue + magnitude < 0f) return false;
            }
            return true;
        }

        // ================================================================ 지속형 이펙트

        private ActiveGameplayEffect ApplyDurationEffect(GameplayEffectSpec spec, PredictionKey predictionKey, bool authority)
        {
            var def = spec.Def;
            var existing = def.StackingType != GameplayEffectStackingType.None ? FindStackableEffect(spec, authority) : null;
            double now = WorldTime;

            if (existing != null)
            {
                int limit = Mathf.Max(1, def.StackLimitCount);
                bool stackChanged = false;
                if (existing.Spec.StackCount < limit)
                {
                    existing.Spec.StackCount = Mathf.Min(limit, existing.Spec.StackCount + spec.StackCount);
                    stackChanged = true;
                }
                if (def.StackDurationRefreshPolicy == GameplayEffectStackingDurationPolicy.RefreshOnSuccessfulApplication)
                    existing.StartWorldTime = now;
                if (def.StackPeriodResetPolicy == GameplayEffectStackingPeriodPolicy.ResetOnSuccessfulApplication && existing.Spec.Period > 0f)
                    existing.NextPeriodTime = now + existing.Spec.Period;

                if (stackChanged && !existing.IsInhibited) UpdateAttributesModifiedBy(existing);
                MarkEffectsDirty();
                return existing;
            }

            var handle = authority
                ? new ActiveGameplayEffectHandle(++_nextServerEffectHandle)
                : new ActiveGameplayEffectHandle(--_nextLocalEffectHandle);
            var active = new ActiveGameplayEffect(handle, spec, now, predictionKey, authority, false);
            active.NextPeriodTime = spec.Period > 0f
                ? (def.bExecutePeriodicEffectOnApplication ? now : now + spec.Period)
                : double.MaxValue;
            _activeEffects.Add(active);

            bool inhibited = !def.OngoingTagRequirements.IsEmpty && !def.OngoingTagRequirements.RequirementsMet(_ownedTags);
            active.IsInhibited = true; // OnEffectActivated 가 false 로 바꾼다
            if (!inhibited) SetInhibited(active, false);

            if (authority)
            {
                for (int i = 0; i < def.GrantedAbilities.Count; i++)
                {
                    var ability = def.GrantedAbilities[i];
                    if (ability == null) continue;
                    active.GrantedAbilityHandles.Add(GiveAbility(new GameplayAbilitySpec(ability, Mathf.RoundToInt(spec.Level), -1, def)));
                }
            }
            else
            {
                RegisterPredictedEffect(predictionKey, active);
            }

            MarkEffectsDirty();
            OnActiveGameplayEffectAddedDelegateToSelf?.Invoke(active);

            if (authority && spec.Period > 0f && def.bExecutePeriodicEffectOnApplication && !active.IsInhibited)
            {
                active.NextPeriodTime = now + spec.Period;
                ExecutePeriodicEffect(active);
            }
            return active;
        }

        /// <summary>주기 실행. 매번 모디파이어 크기를 다시 계산한다 (스냅샷이 아닌 캡처는 현재 값을 읽는다).</summary>
        private void ExecutePeriodicEffect(ActiveGameplayEffect active)
        {
            active.Spec.TargetAbilitySystemComponent = this;
            active.Spec.CalculateModifierMagnitudes(includeDuration: false);
            ExecuteGameplayEffect(active.Spec);
        }

        private ActiveGameplayEffect FindStackableEffect(GameplayEffectSpec spec, bool authority)
        {
            var source = spec.Context.InstigatorAbilitySystemComponent;
            for (int i = 0; i < _activeEffects.Count; i++)
            {
                var active = _activeEffects[i];
                if (active.IsReplicatedProxy || active.IsPendingRemove || active.Def != spec.Def) continue;
                if (active.IsAuthorityOwned != authority) continue;
                if (spec.Def.StackingType == GameplayEffectStackingType.AggregateBySource &&
                    active.Spec.Context.InstigatorAbilitySystemComponent != source) continue;
                return active;
            }
            return null;
        }

        /// <summary>억제 상태를 바꾼다. 억제되면 태그 · 모디파이어 · 큐가 빠지고, 풀리면 다시 들어간다.</summary>
        private void SetInhibited(ActiveGameplayEffect active, bool inhibited)
        {
            if (active.IsInhibited == inhibited) return;
            active.IsInhibited = inhibited;
            int delta = inhibited ? -1 : 1;

            if (active.IsAuthorityOwned) UpdateReplicatedTags(active.GrantedTags, delta);
            else _ownedTags.UpdateTagCount(active.GrantedTags, delta);

            UpdateAttributesModifiedBy(active);

            if (active.IsAuthorityOwned)
            {
                if (inhibited) RemoveEffectCues(active);
                else AddEffectCues(active);
            }
            MarkEffectsDirty();
        }

        // ================================================================ 제거

        /// <summary>언리얼 RemoveActiveGameplayEffect. stacksToRemove 가 -1 이면 전부.</summary>
        public bool RemoveActiveGameplayEffect(ActiveGameplayEffectHandle handle, int stacksToRemove = -1)
        {
            var active = GetActiveGameplayEffect(handle);
            if (active == null || active.IsReplicatedProxy) return false;
            if (active.IsAuthorityOwned && !IsAuthority) return false;
            InternalRemoveActiveGameplayEffect(active, stacksToRemove);
            return true;
        }

        /// <summary>쿼리에 맞는 활성 이펙트를 제거하고 개수를 돌려준다. 서버 전용.</summary>
        public int RemoveActiveEffects(GameplayEffectQuery query, int stacksToRemove = -1)
        {
            if (!IsAuthority || query == null) return 0;
            _effectScratch.Clear();
            for (int i = 0; i < _activeEffects.Count; i++)
            {
                var active = _activeEffects[i];
                if (!active.IsAuthorityOwned) continue;
                if (query.Matches(active)) _effectScratch.Add(active);
            }
            for (int i = 0; i < _effectScratch.Count; i++) InternalRemoveActiveGameplayEffect(_effectScratch[i], stacksToRemove);
            int count = _effectScratch.Count;
            _effectScratch.Clear();
            return count;
        }

        /// <summary>언리얼 RemoveActiveEffectsWithTags. AssetTags 또는 GrantedTags 가 맞는 이펙트를 제거한다.</summary>
        public int RemoveActiveEffectsWithTags(GameplayTagContainer tags) =>
            RemoveActiveEffects(GameplayEffectQuery.MakeQuery_MatchAnyOwningTags(tags));

        /// <summary>언리얼 RemoveActiveEffectsWithGrantedTags.</summary>
        public int RemoveActiveEffectsWithGrantedTags(GameplayTagContainer tags) =>
            RemoveActiveEffects(GameplayEffectQuery.MakeQuery_Custom(e => e.GrantedTags.HasAny(tags)));

        /// <summary>언리얼 RemoveActiveGameplayEffectBySourceEffect.</summary>
        public void RemoveActiveGameplayEffectBySourceEffect(GameplayEffect effect, AbilitySystemComponent instigator, int stacksToRemove = -1)
        {
            var query = GameplayEffectQuery.MakeQuery_EffectDefinition(effect);
            query.EffectSource = instigator;
            RemoveActiveEffects(query, stacksToRemove);
        }

        private void InternalRemoveActiveGameplayEffect(ActiveGameplayEffect active, int stacksToRemove)
        {
            if (active == null || active.IsPendingRemove) return;

            if (stacksToRemove > 0 && active.Spec.StackCount > stacksToRemove)
            {
                active.Spec.StackCount -= stacksToRemove;
                if (!active.IsInhibited) UpdateAttributesModifiedBy(active);
                MarkEffectsDirty();
                return;
            }

            if (!active.IsInhibited) SetInhibited(active, true);
            active.IsPendingRemove = true;
            _activeEffects.Remove(active);

            if (active.IsAuthorityOwned)
            {
                for (int i = 0; i < active.GrantedAbilityHandles.Count; i++) ClearAbility(active.GrantedAbilityHandles[i]);
                active.GrantedAbilityHandles.Clear();
            }

            MarkEffectsDirty();
            OnAnyGameplayEffectRemovedDelegate?.Invoke(active);
        }

        // ================================================================ 시간 진행

        private void TickActiveEffects()
        {
            if (_activeEffects.Count == 0) return;
            bool authority = IsAuthority;
            double now = WorldTime;

            _effectScratch.Clear();
            _effectScratch.AddRange(_activeEffects);
            for (int i = 0; i < _effectScratch.Count; i++)
            {
                var active = _effectScratch[i];
                if (active.IsReplicatedProxy || active.IsPendingRemove) continue;
                if (active.IsAuthorityOwned && !authority) continue;

                // 주기 실행 (서버만 BaseValue 를 바꾼다)
                if (authority && active.Spec.Period > 0f && !active.IsInhibited)
                {
                    int guard = 0;
                    while (now >= active.NextPeriodTime && guard++ < 8 && !active.IsPendingRemove)
                    {
                        active.NextPeriodTime += active.Spec.Period;
                        ExecutePeriodicEffect(active);
                    }
                }

                // 만료
                if (active.Spec.Duration > 0f && now >= active.EndTime && !active.IsPendingRemove)
                {
                    switch (active.Def.StackExpirationPolicy)
                    {
                        case GameplayEffectStackingExpirationPolicy.RemoveSingleStackAndRefreshDuration when active.Spec.StackCount > 1:
                            active.Spec.StackCount--;
                            active.StartWorldTime = now;
                            if (!active.IsInhibited) UpdateAttributesModifiedBy(active);
                            MarkEffectsDirty();
                            break;
                        case GameplayEffectStackingExpirationPolicy.RefreshDuration:
                            active.StartWorldTime = now;
                            MarkEffectsDirty();
                            break;
                        default:
                            InternalRemoveActiveGameplayEffect(active, -1);
                            break;
                    }
                }
            }
            _effectScratch.Clear();
        }

        /// <summary>태그가 바뀐 뒤 Ongoing / Removal 조건을 다시 본다. 무언가 바뀌었으면 true.</summary>
        private bool ReevaluateEffectTagRequirements()
        {
            bool changed = false;
            _effectScratch.Clear();
            _effectScratch.AddRange(_activeEffects);
            for (int i = 0; i < _effectScratch.Count; i++)
            {
                var active = _effectScratch[i];
                if (active.IsReplicatedProxy || active.IsPendingRemove) continue;
                if (active.IsAuthorityOwned && !IsAuthority) continue;
                var def = active.Def;

                if (!def.RemovalTagRequirements.IsEmpty && def.RemovalTagRequirements.RequirementsMet(_ownedTags))
                {
                    InternalRemoveActiveGameplayEffect(active, -1);
                    changed = true;
                    continue;
                }
                if (def.OngoingTagRequirements.IsEmpty) continue;
                bool shouldInhibit = !def.OngoingTagRequirements.RequirementsMet(_ownedTags);
                if (shouldInhibit == active.IsInhibited) continue;
                SetInhibited(active, shouldInhibit);
                changed = true;
            }
            _effectScratch.Clear();
            return changed;
        }

        // ================================================================ 집계

        /// <summary>서버: ((Base + ΣAdd) × (1 + Σ(Mul-1))) / (1 + Σ(Div-1)), Override 가 있으면 마지막 Override.</summary>
        private float AggregateAuthority(AttributeEntry entry)
        {
            float baseValue = entry.Data.BaseValue;
            float add = 0f, mul = 1f, div = 1f;
            bool hasOverride = false;
            float overrideValue = 0f;

            for (int e = 0; e < _activeEffects.Count; e++)
            {
                var active = _activeEffects[e];
                if (!active.IsAuthorityOwned || active.IsInhibited || active.IsPendingRemove) continue;
                AccumulateModifiers(active, entry.Attribute, ref add, ref mul, ref div, ref hasOverride, ref overrideValue);
            }

            if (hasOverride) return overrideValue;
            float result = (baseValue + add) * mul;
            return Mathf.Approximately(div, 0f) ? result : result / div;
        }

        /// <summary>
        /// 클라이언트: 서버가 보낸 CurrentValue 에 이 클라이언트가 예측한 값(비용 차감, 예측 버프)을 얹는다.
        /// 서버 결과가 도착하면 예측분은 지워지고 서버 값만 남는다.
        /// </summary>
        private float AggregateClient(AttributeEntry entry)
        {
            float predictedBaseDelta = GetPredictedBaseDelta(entry);
            float baseValue = (entry.HasRepValue ? entry.RepBase : entry.Data.BaseValue) + predictedBaseDelta;
            entry.Data.SetBaseValueInternal(baseValue);
            float current = (entry.HasRepValue ? entry.RepCurrent : entry.Data.CurrentValue) + predictedBaseDelta;

            float add = 0f, mul = 1f, div = 1f;
            bool hasOverride = false;
            float overrideValue = 0f;
            bool any = false;
            for (int e = 0; e < _activeEffects.Count; e++)
            {
                var active = _activeEffects[e];
                if (!active.IsPredicted || active.IsInhibited || active.IsPendingRemove) continue;
                any |= AccumulateModifiers(active, entry.Attribute, ref add, ref mul, ref div, ref hasOverride, ref overrideValue);
            }
            if (!any) return current;
            if (hasOverride) return overrideValue;
            float result = (current + add) * mul;
            return Mathf.Approximately(div, 0f) ? result : result / div;
        }

        private bool AccumulateModifiers(ActiveGameplayEffect active, GameplayAttribute attribute,
            ref float add, ref float mul, ref float div, ref bool hasOverride, ref float overrideValue)
        {
            bool any = false;
            var modifiers = active.Def.Modifiers;
            for (int m = 0; m < modifiers.Count; m++)
            {
                var modifier = modifiers[m];
                if (modifier.Attribute != attribute) continue;
                if (!ModifierTagRequirementsMet(modifier, active.Spec)) continue;
                float magnitude = active.Spec.GetModifierMagnitude(m, true);
                any = true;
                switch (modifier.ModifierOp)
                {
                    case GameplayModOp.Additive: add += magnitude; break;
                    case GameplayModOp.Multiplicitive: mul += magnitude - 1f; break;
                    case GameplayModOp.Division: div += magnitude - 1f; break;
                    case GameplayModOp.Override: hasOverride = true; overrideValue = magnitude; break;
                }
            }
            return any;
        }

        // ================================================================ 조회

        public ActiveGameplayEffect GetActiveGameplayEffect(ActiveGameplayEffectHandle handle)
        {
            if (!handle.IsValid) return null;
            for (int i = 0; i < _activeEffects.Count; i++)
                if (_activeEffects[i].Handle == handle) return _activeEffects[i];
            return null;
        }

        /// <summary>현재 활성 이펙트. 클라이언트에서는 복제 사본과 예측 이펙트가 섞여 있다.</summary>
        public IReadOnlyList<ActiveGameplayEffect> GetActiveGameplayEffects() => _activeEffects;

        public List<ActiveGameplayEffectHandle> GetActiveEffects(GameplayEffectQuery query)
        {
            var result = new List<ActiveGameplayEffectHandle>();
            for (int i = 0; i < _activeEffects.Count; i++)
                if (query == null || query.Matches(_activeEffects[i])) result.Add(_activeEffects[i].Handle);
            return result;
        }

        public int GetCurrentStackCount(ActiveGameplayEffectHandle handle) => GetActiveGameplayEffect(handle)?.StackCount ?? 0;

        /// <summary>쿼리에 맞는 이펙트 중 가장 늦게 끝나는 것의 남은 시간과 전체 지속시간.</summary>
        public void GetActiveEffectsTimeRemainingAndDuration(GameplayEffectQuery query, out float remaining, out float duration)
        {
            remaining = 0f;
            duration = 0f;
            double now = WorldTime;
            for (int i = 0; i < _activeEffects.Count; i++)
            {
                var active = _activeEffects[i];
                if (active.Spec.Duration <= 0f || !query.Matches(active)) continue;
                float r = active.GetTimeRemaining(now);
                if (r > remaining)
                {
                    remaining = r;
                    duration = active.Spec.Duration;
                }
            }
        }

        public float GetActiveEffectsTimeRemaining(GameplayEffectQuery query)
        {
            GetActiveEffectsTimeRemainingAndDuration(query, out float remaining, out _);
            return remaining;
        }

        private void MarkEffectsDirty()
        {
            _effectsDirty = true;
            OnActiveGameplayEffectsChanged?.Invoke();
        }
    }
}
