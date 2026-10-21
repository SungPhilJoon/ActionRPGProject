using System.Collections.Generic;
using UnityEngine;

namespace ActionRPG.Core.AbilitySystem
{
    /// <summary>
    /// 언리얼 UGameplayAbility. 캐릭터가 "할 수 있는 행동" 하나를 정의한다.
    ///
    /// 수명:
    ///   CanActivateAbility (태그 · 쿨다운 · 비용 검사)
    ///   → PreActivate (ActivationOwnedTags 부여, Block/Cancel 태그 처리)
    ///   → ActivateAbility (서브클래스 로직. 보통 CommitAbility 로 비용·쿨다운 확정 후 태스크를 건다)
    ///   → EndAbility (태스크 정리, 태그 회수, 서버/클라이언트에 종료 전파)
    ///
    /// 서브클래스는 ScriptableObject 에셋으로 만든다(언리얼의 블루프린트 어빌리티에 해당).
    /// InstancedPerActor/PerExecution 이면 실행 시 에셋의 복제본이 만들어지므로 멤버 변수에 상태를 둘 수 있다.
    /// </summary>
    public abstract class GameplayAbility : ScriptableObject
    {
        [Header("태그")]
        [Tooltip("이 어빌리티를 설명하는 태그. 다른 어빌리티의 Cancel/Block 조건에 쓰인다.")]
        public GameplayTagContainer AbilityTags = new GameplayTagContainer();
        [Tooltip("발동하면 이 태그를 가진 실행 중 어빌리티를 취소한다.")]
        public GameplayTagContainer CancelAbilitiesWithTag = new GameplayTagContainer();
        [Tooltip("실행 중인 동안 이 태그를 가진 어빌리티의 발동을 막는다.")]
        public GameplayTagContainer BlockAbilitiesWithTag = new GameplayTagContainer();
        [Tooltip("실행 중인 동안 시전자가 갖는 태그.")]
        public GameplayTagContainer ActivationOwnedTags = new GameplayTagContainer();
        [Tooltip("시전자가 이 태그를 모두 가져야 발동할 수 있다.")]
        public GameplayTagContainer ActivationRequiredTags = new GameplayTagContainer();
        [Tooltip("시전자가 이 태그 중 하나라도 가지면 발동할 수 없다 (기절, 침묵, 사망 등).")]
        public GameplayTagContainer ActivationBlockedTags = new GameplayTagContainer();

        [Header("비용 / 쿨다운")]
        public GameplayEffect CostGameplayEffect;
        public GameplayEffect CooldownGameplayEffect;

        [Header("실행 정책")]
        public GameplayAbilityInstancingPolicy InstancingPolicy = GameplayAbilityInstancingPolicy.InstancedPerActor;
        public GameplayAbilityNetExecutionPolicy NetExecutionPolicy = GameplayAbilityNetExecutionPolicy.LocalPredicted;
        [Tooltip("실행 중에 다시 발동하면 기존 실행을 끝내고 새로 시작할지.")]
        public bool bRetriggerInstancedAbility;
        [Tooltip("입력 눌림/뗌을 서버에도 전달할지 (WaitInputRelease 등을 서버에서도 쓰려면 켠다).")]
        public bool bReplicateInputDirectly;
        [Tooltip("클라이언트가 보낸 취소 요청을 서버가 받아들일지.")]
        public bool bServerRespectsRemoteAbilityCancellation = true;

        [Header("트리거")]
        public List<AbilityTriggerData> AbilityTriggers = new List<AbilityTriggerData>();

        // ---------------------------------------------------------------- 실행 상태 (인스턴스 전용)

        /// <summary>복제본이면 원본 에셋. 원본이면 null.</summary>
        public GameplayAbility SourceAsset { get; internal set; }
        public bool IsInstantiated => SourceAsset != null;
        public GameplayAbility AssetDefinition => SourceAsset != null ? SourceAsset : this;

        public GameplayAbilitySpecHandle CurrentSpecHandle { get; private set; }
        public GameplayAbilityActorInfo CurrentActorInfo { get; private set; }
        public GameplayAbilityActivationInfo CurrentActivationInfo { get; private set; }
        public GameplayEventData CurrentEventData { get; private set; }
        public bool HasCurrentEventData { get; private set; }
        public bool IsActive { get; private set; }

        private readonly List<AbilityTask> _activeTasks = new List<AbilityTask>(4);
        private bool _isEnding;
        private bool _tagsRegistered;

        protected virtual void OnEnable()
        {
            _tagsRegistered = false;
            RegisterTags();
            if (SourceAsset == null && !name.EndsWith("(Clone)")) AbilitySystemGlobals.RegisterAsset(this);
        }

        public virtual void RegisterTags()
        {
            if (_tagsRegistered) return;
            _tagsRegistered = true;
            AbilityTags.RegisterAll();
            CancelAbilitiesWithTag.RegisterAll();
            BlockAbilitiesWithTag.RegisterAll();
            ActivationOwnedTags.RegisterAll();
            ActivationRequiredTags.RegisterAll();
            ActivationBlockedTags.RegisterAll();
            for (int i = 0; i < AbilityTriggers.Count; i++)
                if (!string.IsNullOrEmpty(AbilityTriggers[i].TriggerTag.TagName))
                    AbilityTriggers[i].TriggerTag = GameplayTag.Request(AbilityTriggers[i].TriggerTag.TagName);
        }

        /// <summary>네트워크 식별자 (원본 에셋 이름의 해시).</summary>
        public int AbilityId => AbilitySystemGlobals.GetAssetId(AssetDefinition);

        // ================================================================ 발동 가능 검사

        /// <summary>언리얼 CanActivateAbility.</summary>
        public virtual bool CanActivateAbility(GameplayAbilitySpecHandle handle, GameplayAbilityActorInfo actorInfo,
            GameplayTagContainer optionalRelevantTags = null)
        {
            var asc = actorInfo?.AbilitySystemComponent;
            if (asc == null) return false;
            var spec = asc.FindAbilitySpecFromHandle(handle);
            if (spec == null) return false;

            if (spec.IsActive && InstancingPolicy == GameplayAbilityInstancingPolicy.InstancedPerActor && !bRetriggerInstancedAbility)
            {
                optionalRelevantTags?.AddTag(AbilitySystemTags.ActivateFailIsActive);
                return false;
            }

            if (!DoesAbilitySatisfyTagRequirements(asc, optionalRelevantTags)) return false;
            if (!CheckCooldown(handle, actorInfo, optionalRelevantTags)) return false;
            if (!CheckCost(handle, actorInfo, optionalRelevantTags)) return false;
            return true;
        }

        /// <summary>언리얼 DoesAbilitySatisfyTagRequirements.</summary>
        public virtual bool DoesAbilitySatisfyTagRequirements(AbilitySystemComponent asc, GameplayTagContainer optionalRelevantTags = null)
        {
            if (asc.AreAbilityTagsBlocked(AbilityTags))
            {
                optionalRelevantTags?.AddTag(AbilitySystemTags.ActivateFailTagsBlocked);
                return false;
            }
            if (!asc.HasAllMatchingGameplayTags(ActivationRequiredTags))
            {
                optionalRelevantTags?.AddTag(AbilitySystemTags.ActivateFailTagsMissing);
                return false;
            }
            if (asc.HasAnyMatchingGameplayTags(ActivationBlockedTags))
            {
                optionalRelevantTags?.AddTag(AbilitySystemTags.ActivateFailTagsBlocked);
                return false;
            }
            return true;
        }

        /// <summary>언리얼 CheckCooldown. 쿨다운 태그를 시전자가 가지고 있으면 실패.</summary>
        public virtual bool CheckCooldown(GameplayAbilitySpecHandle handle, GameplayAbilityActorInfo actorInfo,
            GameplayTagContainer optionalRelevantTags = null)
        {
            var cooldownTags = GetCooldownTags(handle, actorInfo);
            if (cooldownTags == null || cooldownTags.IsEmpty) return true;
            if (!actorInfo.AbilitySystemComponent.HasAnyMatchingGameplayTags(cooldownTags)) return true;
            optionalRelevantTags?.AddTag(AbilitySystemTags.ActivateFailCooldown);
            return false;
        }

        /// <summary>언리얼 CheckCost. 비용 이펙트를 적용하면 어트리뷰트가 음수가 되는지 본다.</summary>
        public virtual bool CheckCost(GameplayAbilitySpecHandle handle, GameplayAbilityActorInfo actorInfo,
            GameplayTagContainer optionalRelevantTags = null)
        {
            var costSpec = MakeCostGameplayEffectSpec(handle, actorInfo);
            if (costSpec == null) return true;
            if (actorInfo.AbilitySystemComponent.CanApplyAttributeModifiers(costSpec)) return true;
            optionalRelevantTags?.AddTag(AbilitySystemTags.ActivateFailCost);
            return false;
        }

        /// <summary>
        /// 쿨다운 판정에 쓰는 태그. 기본은 쿨다운 이펙트의 GrantedTags.
        /// 스킬마다 다른 태그를 붙이는 경우(공용 쿨다운 이펙트 + 동적 태그) 오버라이드한다.
        /// </summary>
        public virtual GameplayTagContainer GetCooldownTags(GameplayAbilitySpecHandle handle, GameplayAbilityActorInfo actorInfo)
        {
            return CooldownGameplayEffect != null ? CooldownGameplayEffect.GrantedTags : null;
        }

        /// <summary>쿨다운 스펙. SetByCaller 나 동적 태그를 넣으려면 오버라이드한다.</summary>
        protected virtual GameplayEffectSpec MakeCooldownGameplayEffectSpec(GameplayAbilitySpecHandle handle, GameplayAbilityActorInfo actorInfo)
        {
            return CooldownGameplayEffect != null ? MakeOutgoingGameplayEffectSpec(handle, actorInfo, CooldownGameplayEffect) : null;
        }

        /// <summary>비용 스펙. SetByCaller 로 비용을 넣으려면 오버라이드한다.</summary>
        protected virtual GameplayEffectSpec MakeCostGameplayEffectSpec(GameplayAbilitySpecHandle handle, GameplayAbilityActorInfo actorInfo)
        {
            return CostGameplayEffect != null ? MakeOutgoingGameplayEffectSpec(handle, actorInfo, CostGameplayEffect) : null;
        }

        // ================================================================ 커밋

        /// <summary>언리얼 CommitAbility. 쿨다운과 비용을 다시 검사하고 확정한다. 실패하면 false.</summary>
        public virtual bool CommitAbility(GameplayAbilitySpecHandle handle, GameplayAbilityActorInfo actorInfo,
            GameplayAbilityActivationInfo activationInfo, GameplayTagContainer optionalRelevantTags = null)
        {
            if (!CommitCheck(handle, actorInfo, optionalRelevantTags)) return false;
            ApplyCooldown(handle, actorInfo, activationInfo);
            ApplyCost(handle, actorInfo, activationInfo);
            return true;
        }

        public virtual bool CommitCheck(GameplayAbilitySpecHandle handle, GameplayAbilityActorInfo actorInfo,
            GameplayTagContainer optionalRelevantTags = null)
        {
            return CheckCooldown(handle, actorInfo, optionalRelevantTags) && CheckCost(handle, actorInfo, optionalRelevantTags);
        }

        public bool CommitAbilityCooldown(GameplayAbilitySpecHandle handle, GameplayAbilityActorInfo actorInfo, GameplayAbilityActivationInfo activationInfo)
        {
            if (!CheckCooldown(handle, actorInfo)) return false;
            ApplyCooldown(handle, actorInfo, activationInfo);
            return true;
        }

        public bool CommitAbilityCost(GameplayAbilitySpecHandle handle, GameplayAbilityActorInfo actorInfo, GameplayAbilityActivationInfo activationInfo)
        {
            if (!CheckCost(handle, actorInfo)) return false;
            ApplyCost(handle, actorInfo, activationInfo);
            return true;
        }

        public virtual void ApplyCooldown(GameplayAbilitySpecHandle handle, GameplayAbilityActorInfo actorInfo, GameplayAbilityActivationInfo activationInfo)
        {
            var spec = MakeCooldownGameplayEffectSpec(handle, actorInfo);
            if (spec != null) ApplyGameplayEffectSpecToOwner(handle, actorInfo, activationInfo, spec);
        }

        public virtual void ApplyCost(GameplayAbilitySpecHandle handle, GameplayAbilityActorInfo actorInfo, GameplayAbilityActivationInfo activationInfo)
        {
            var spec = MakeCostGameplayEffectSpec(handle, actorInfo);
            if (spec != null) ApplyGameplayEffectSpecToOwner(handle, actorInfo, activationInfo, spec);
        }

        /// <summary>현재 실행 정보로 커밋한다 (블루프린트의 K2_CommitAbility).</summary>
        protected bool CommitAbility() => CommitAbility(CurrentSpecHandle, CurrentActorInfo, CurrentActivationInfo);

        // ================================================================ 발동 / 종료

        /// <summary>ASC 가 호출한다. 실행 상태를 세팅하고 ActivateAbility 로 넘긴다.</summary>
        internal void CallActivateAbility(GameplayAbilitySpecHandle handle, GameplayAbilityActorInfo actorInfo,
            GameplayAbilityActivationInfo activationInfo, GameplayEventData? triggerEventData)
        {
            PreActivate(handle, actorInfo, activationInfo, triggerEventData);
            ActivateAbility(handle, actorInfo, activationInfo, triggerEventData);
        }

        protected virtual void PreActivate(GameplayAbilitySpecHandle handle, GameplayAbilityActorInfo actorInfo,
            GameplayAbilityActivationInfo activationInfo, GameplayEventData? triggerEventData)
        {
            CurrentSpecHandle = handle;
            CurrentActorInfo = actorInfo;
            CurrentActivationInfo = activationInfo;
            HasCurrentEventData = triggerEventData.HasValue;
            CurrentEventData = triggerEventData ?? default;
            IsActive = true;
            _isEnding = false;

            var asc = actorInfo.AbilitySystemComponent;
            asc.AddLooseGameplayTags(ActivationOwnedTags);
            asc.ApplyAbilityBlockAndCancelTags(AbilityTags, this, true, BlockAbilitiesWithTag, true, CancelAbilitiesWithTag);
        }

        /// <summary>
        /// 언리얼 ActivateAbility. 서브클래스가 실제 동작을 구현한다.
        /// 기본 구현은 커밋만 하고 바로 끝낸다.
        /// </summary>
        protected virtual void ActivateAbility(GameplayAbilitySpecHandle handle, GameplayAbilityActorInfo actorInfo,
            GameplayAbilityActivationInfo activationInfo, GameplayEventData? triggerEventData)
        {
            if (!CommitAbility(handle, actorInfo, activationInfo))
            {
                EndAbility(handle, actorInfo, activationInfo, true, true);
                return;
            }
            EndAbility(handle, actorInfo, activationInfo, true, false);
        }

        /// <summary>언리얼 EndAbility.</summary>
        public virtual void EndAbility(GameplayAbilitySpecHandle handle, GameplayAbilityActorInfo actorInfo,
            GameplayAbilityActivationInfo activationInfo, bool bReplicateEndAbility, bool bWasCancelled)
        {
            if (!IsActive || _isEnding) return;
            _isEnding = true;

            OnEndAbility(bWasCancelled);

            for (int i = _activeTasks.Count - 1; i >= 0; i--)
            {
                var task = _activeTasks[i];
                if (task != null && !task.IsFinished) task.InternalEnd(abilityEnded: true);
            }
            _activeTasks.Clear();

            IsActive = false;
            var asc = actorInfo?.AbilitySystemComponent;
            if (asc != null)
            {
                if (bReplicateEndAbility) asc.ReplicateEndOrCancelAbility(handle, activationInfo, this, bWasCancelled);
                asc.RemoveLooseGameplayTags(ActivationOwnedTags);
                asc.ApplyAbilityBlockAndCancelTags(AbilityTags, this, false, BlockAbilitiesWithTag, false, null);
                asc.NotifyAbilityEnded(handle, this, bWasCancelled);
            }
            _isEnding = false;
        }

        /// <summary>EndAbility 직전에 서브클래스가 정리할 기회 (이동 잠금 해제 등).</summary>
        protected virtual void OnEndAbility(bool wasCancelled) { }

        /// <summary>언리얼 CancelAbility.</summary>
        public virtual void CancelAbility(GameplayAbilitySpecHandle handle, GameplayAbilityActorInfo actorInfo,
            GameplayAbilityActivationInfo activationInfo, bool bReplicateCancelAbility)
        {
            if (!CanBeCanceled()) return;
            EndAbility(handle, actorInfo, activationInfo, bReplicateCancelAbility, true);
        }

        public virtual bool CanBeCanceled() => true;

        /// <summary>현재 실행 정보로 종료한다 (K2_EndAbility).</summary>
        public void K2_EndAbility() => EndAbility(CurrentSpecHandle, CurrentActorInfo, CurrentActivationInfo, true, false);

        /// <summary>현재 실행 정보로 취소한다 (K2_CancelAbility).</summary>
        public void K2_CancelAbility() => CancelAbility(CurrentSpecHandle, CurrentActorInfo, CurrentActivationInfo, true);

        /// <summary>서버가 예측을 승인했을 때 ASC 가 부른다.</summary>
        internal void SetActivationConfirmed()
        {
            var info = CurrentActivationInfo;
            info.ActivationMode = GameplayAbilityActivationMode.Confirmed;
            CurrentActivationInfo = info;
        }

        // ================================================================ 입력

        /// <summary>실행 중에 같은 입력이 다시 눌렸을 때.</summary>
        public virtual void InputPressed(GameplayAbilitySpecHandle handle, GameplayAbilityActorInfo actorInfo, GameplayAbilityActivationInfo activationInfo) { }

        /// <summary>실행 중에 입력이 떼어졌을 때.</summary>
        public virtual void InputReleased(GameplayAbilitySpecHandle handle, GameplayAbilityActorInfo actorInfo, GameplayAbilityActivationInfo activationInfo) { }

        // ================================================================ 어빌리티 태스크

        internal void RegisterTask(AbilityTask task)
        {
            if (InstancingPolicy == GameplayAbilityInstancingPolicy.NonInstanced)
                Debug.LogError($"[GAS] {name} 은 NonInstanced 라 어빌리티 태스크를 쓸 수 없습니다.");
            _activeTasks.Add(task);
        }

        internal void UnregisterTask(AbilityTask task) => _activeTasks.Remove(task);

        internal void TickTasks(float deltaTime)
        {
            if (_activeTasks.Count == 0) return;
            // 등록 순서대로 틱한다. 같은 프레임에 끝나는 태스크는 먼저 건 것이 먼저 완료된다
            // (예: 판정 타이머가 종료 타이머보다 먼저).
            var snapshot = _activeTasks.ToArray();
            for (int i = 0; i < snapshot.Length; i++)
            {
                if (!IsActive) return;
                var task = snapshot[i];
                if (task != null && task.bTickingTask && !task.IsFinished) task.TickTask(deltaTime);
            }
        }

        internal void NotifyInputReleasedToTasks(float timeHeld)
        {
            for (int i = _activeTasks.Count - 1; i >= 0; i--)
                if (i < _activeTasks.Count && _activeTasks[i] is AbilityTask_WaitInputRelease release) release.NotifyReleased();
        }

        internal void NotifyInputPressedToTasks()
        {
            for (int i = _activeTasks.Count - 1; i >= 0; i--)
                if (i < _activeTasks.Count && _activeTasks[i] is AbilityTask_WaitInputPress press) press.NotifyPressed();
        }

        // ================================================================ 헬퍼

        public AbilitySystemComponent GetAbilitySystemComponentFromActorInfo() => CurrentActorInfo?.AbilitySystemComponent;
        public GameObject GetAvatarActorFromActorInfo() => CurrentActorInfo?.AvatarActor;
        public GameObject GetOwningActorFromActorInfo() => CurrentActorInfo?.OwnerActor;

        public bool HasAuthority() => CurrentActorInfo != null && CurrentActorInfo.IsNetAuthority;
        public bool IsLocallyControlled() => CurrentActorInfo != null && CurrentActorInfo.IsLocallyControlled;
        public bool IsPredictingClient() => CurrentActivationInfo.ActivationMode == GameplayAbilityActivationMode.Predicting;

        public int GetAbilityLevel(GameplayAbilitySpecHandle handle, GameplayAbilityActorInfo actorInfo)
        {
            var spec = actorInfo?.AbilitySystemComponent?.FindAbilitySpecFromHandle(handle);
            return spec?.Level ?? 1;
        }

        public int GetAbilityLevel() => GetAbilityLevel(CurrentSpecHandle, CurrentActorInfo);

        /// <summary>현재 실행 중인 스펙. 실행 중이 아니면 null.</summary>
        public GameplayAbilitySpec GetCurrentAbilitySpec() =>
            CurrentActorInfo?.AbilitySystemComponent?.FindAbilitySpecFromHandle(CurrentSpecHandle);

        /// <summary>언리얼 MakeEffectContext. 시전자 = 소유자, 원인 = 아바타, 어빌리티 정보 포함.</summary>
        public virtual GameplayEffectContext MakeEffectContext(GameplayAbilitySpecHandle handle, GameplayAbilityActorInfo actorInfo)
        {
            var context = new GameplayEffectContext();
            context.AddInstigator(actorInfo.OwnerActor, actorInfo.AvatarActor);
            context.SetAbility(AssetDefinition, GetAbilityLevel(handle, actorInfo));
            context.AddSourceObject(AssetDefinition);
            return context;
        }

        public GameplayEffectSpec MakeOutgoingGameplayEffectSpec(GameplayAbilitySpecHandle handle, GameplayAbilityActorInfo actorInfo,
            GameplayEffect effect, float level = -1f)
        {
            if (effect == null || actorInfo == null) return null;
            if (level < 0f) level = GetAbilityLevel(handle, actorInfo);
            return actorInfo.AbilitySystemComponent.MakeOutgoingSpec(effect, level, MakeEffectContext(handle, actorInfo));
        }

        /// <summary>현재 실행 정보로 스펙을 만든다.</summary>
        public GameplayEffectSpec MakeOutgoingGameplayEffectSpec(GameplayEffect effect, float level = -1f) =>
            MakeOutgoingGameplayEffectSpec(CurrentSpecHandle, CurrentActorInfo, effect, level);

        /// <summary>
        /// 언리얼 ApplyGameplayEffectSpecToOwner. 서버이거나 예측 키가 있으면 적용한다.
        /// 예측 중인 클라이언트는 "예측 이펙트"로 먼저 걸고, 서버 결과가 오면 정리된다.
        /// </summary>
        public ActiveGameplayEffectHandle ApplyGameplayEffectSpecToOwner(GameplayAbilitySpecHandle handle, GameplayAbilityActorInfo actorInfo,
            GameplayAbilityActivationInfo activationInfo, GameplayEffectSpec spec)
        {
            var asc = actorInfo?.AbilitySystemComponent;
            if (asc == null || spec == null) return ActiveGameplayEffectHandle.Invalid;
            if (!asc.IsAuthority && !asc.ScopedPredictionKey.IsValidKey) return ActiveGameplayEffectHandle.Invalid;
            return asc.ApplyGameplayEffectSpecToSelf(spec, asc.ScopedPredictionKey);
        }

        public ActiveGameplayEffectHandle ApplyGameplayEffectSpecToOwner(GameplayEffectSpec spec) =>
            ApplyGameplayEffectSpecToOwner(CurrentSpecHandle, CurrentActorInfo, CurrentActivationInfo, spec);

        public ActiveGameplayEffectHandle ApplyGameplayEffectToOwner(GameplayEffect effect, float level = -1f) =>
            ApplyGameplayEffectSpecToOwner(MakeOutgoingGameplayEffectSpec(effect, level));

        /// <summary>
        /// 언리얼 ApplyGameplayEffectSpecToTarget. 다른 대상에 대한 적용은 서버만 한다.
        /// (예측 클라이언트가 남에게 피해를 미리 주지 않는다 — 언리얼과 같은 규칙.)
        /// </summary>
        public ActiveGameplayEffectHandle ApplyGameplayEffectSpecToTarget(GameplayEffectSpec spec, AbilitySystemComponent target)
        {
            var asc = GetAbilitySystemComponentFromActorInfo();
            if (asc == null || spec == null || target == null) return ActiveGameplayEffectHandle.Invalid;
            if (target == asc) return ApplyGameplayEffectSpecToOwner(spec);
            if (!asc.IsAuthority) return ActiveGameplayEffectHandle.Invalid;
            return asc.ApplyGameplayEffectSpecToTarget(spec, target);
        }

        /// <summary>남은 쿨다운(초). 쿨다운이 없으면 0.</summary>
        public float GetCooldownTimeRemaining(GameplayAbilitySpecHandle handle, GameplayAbilityActorInfo actorInfo)
        {
            var asc = actorInfo?.AbilitySystemComponent;
            var tags = GetCooldownTags(handle, actorInfo);
            if (asc == null || tags == null || tags.IsEmpty) return 0f;
            asc.GetActiveEffectsTimeRemainingAndDuration(GameplayEffectQuery.MakeQuery_MatchAnyOwningTags(tags), out float remaining, out _);
            return remaining;
        }

        public void GetCooldownTimeRemainingAndDuration(GameplayAbilitySpecHandle handle, GameplayAbilityActorInfo actorInfo,
            out float remaining, out float duration)
        {
            remaining = 0f;
            duration = 0f;
            var asc = actorInfo?.AbilitySystemComponent;
            var tags = GetCooldownTags(handle, actorInfo);
            if (asc == null || tags == null || tags.IsEmpty) return;
            asc.GetActiveEffectsTimeRemainingAndDuration(GameplayEffectQuery.MakeQuery_MatchAnyOwningTags(tags), out remaining, out duration);
        }

        /// <summary>
        /// 게임플레이 큐를 실행한다. 서버면 모든 피어에 전파하고, 예측 중인 클라이언트면 자기 화면에서 먼저 재생한다.
        /// 서버가 나중에 같은 예측 키로 보낸 큐는 소유 클라이언트가 건너뛴다.
        /// </summary>
        public void ExecuteGameplayCue(GameplayTag cueTag, GameplayCueParameters parameters)
        {
            var asc = GetAbilitySystemComponentFromActorInfo();
            if (asc == null) return;
            parameters.PredictionKey = CurrentActivationInfo.ActivationPredictionKey.Current;
            if (parameters.SourceObjectId == 0) parameters.SourceObjectId = AbilityId;
            if (parameters.AbilityLevel == 0) parameters.AbilityLevel = GetAbilityLevel();
            asc.ExecuteGameplayCueFromAbility(cueTag, parameters, this);
        }

        /// <summary>애니메이션을 재생하고 다른 피어에도 전파한다 (PlayMontageAndWait 가 쓴다).</summary>
        internal void PlayAnimation(int triggerHash, int indexParamHash, int index)
        {
            var asc = GetAbilitySystemComponentFromActorInfo();
            if (asc != null) asc.PlayAbilityAnimation(this, triggerHash, indexParamHash, index);
        }

        public void SendGameplayEvent(GameplayTag eventTag, GameplayEventData payload)
        {
            var asc = GetAbilitySystemComponentFromActorInfo();
            if (asc == null) return;
            payload.EventTag = eventTag;
            asc.HandleGameplayEvent(eventTag, payload);
        }
    }

    /// <summary>프레임워크가 쓰는 기본 태그. 언리얼 AbilitySystemGlobals 의 ActivateFail 태그들에 해당한다.</summary>
    public static class AbilitySystemTags
    {
        public static readonly GameplayTag ActivateFailIsActive = GameplayTag.Request("Ability.ActivateFail.IsActive");
        public static readonly GameplayTag ActivateFailCooldown = GameplayTag.Request("Ability.ActivateFail.Cooldown");
        public static readonly GameplayTag ActivateFailCost = GameplayTag.Request("Ability.ActivateFail.Cost");
        public static readonly GameplayTag ActivateFailTagsBlocked = GameplayTag.Request("Ability.ActivateFail.TagsBlocked");
        public static readonly GameplayTag ActivateFailTagsMissing = GameplayTag.Request("Ability.ActivateFail.TagsMissing");
        public static readonly GameplayTag ActivateFailNetworking = GameplayTag.Request("Ability.ActivateFail.Networking");
    }
}
