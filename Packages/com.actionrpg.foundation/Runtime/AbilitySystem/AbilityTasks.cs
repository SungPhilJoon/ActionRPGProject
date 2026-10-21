using System;

namespace ActionRPG.Core.AbilitySystem
{
    /// <summary>
    /// 언리얼 UAbilityTask. 어빌리티 안에서 "기다렸다가 무언가를 하는" 비동기 작업.
    /// 어빌리티가 끝나면 남은 태스크도 함께 끝난다. ASC 가 매 프레임 틱을 돌려 준다.
    ///
    /// 사용법 (언리얼과 같은 형태):
    /// <code>
    /// var wait = AbilityTask_WaitDelay.WaitDelay(this, 0.3f);
    /// wait.OnFinish += OnWindupFinished;
    /// wait.ReadyForActivation();
    /// </code>
    /// </summary>
    public abstract class AbilityTask
    {
        public GameplayAbility Ability { get; private set; }
        public bool IsFinished { get; private set; }

        /// <summary>true 면 TickTask 가 매 프레임 호출된다.</summary>
        public bool bTickingTask { get; protected set; }

        protected AbilitySystemComponent AbilitySystemComponent => Ability?.CurrentActorInfo?.AbilitySystemComponent;

        protected static T NewAbilityTask<T>(GameplayAbility owningAbility) where T : AbilityTask, new()
        {
            var task = new T();
            task.Ability = owningAbility;
            owningAbility.RegisterTask(task);
            return task;
        }

        /// <summary>델리게이트를 다 연결한 뒤 호출한다. 이때 실제로 시작된다.</summary>
        public void ReadyForActivation()
        {
            if (IsFinished) return;
            Activate();
        }

        protected virtual void Activate() { }

        internal virtual void TickTask(float deltaTime) { }

        /// <summary>태스크를 스스로 끝낸다.</summary>
        public void EndTask() => InternalEnd(abilityEnded: false);

        internal void InternalEnd(bool abilityEnded)
        {
            if (IsFinished) return;
            IsFinished = true;
            OnDestroy(abilityEnded);
            if (!abilityEnded) Ability?.UnregisterTask(this);
        }

        protected virtual void OnDestroy(bool abilityEnded) { }

        /// <summary>어빌리티가 아직 실행 중일 때만 결과 델리게이트를 쏴야 한다.</summary>
        protected bool ShouldBroadcastAbilityTaskDelegates() => Ability != null && Ability.IsActive && !IsFinished;
    }

    /// <summary>언리얼 UAbilityTask_WaitDelay.</summary>
    public sealed class AbilityTask_WaitDelay : AbilityTask
    {
        public event Action OnFinish;
        private float _remaining;

        public static AbilityTask_WaitDelay WaitDelay(GameplayAbility owningAbility, float time)
        {
            var task = NewAbilityTask<AbilityTask_WaitDelay>(owningAbility);
            task._remaining = Math.Max(0f, time);
            return task;
        }

        protected override void Activate()
        {
            if (_remaining <= 0f)
            {
                Finish();
                return;
            }
            bTickingTask = true;
        }

        internal override void TickTask(float deltaTime)
        {
            _remaining -= deltaTime;
            if (_remaining <= 0f) Finish();
        }

        private void Finish()
        {
            bool broadcast = ShouldBroadcastAbilityTaskDelegates();
            EndTask();
            if (broadcast) OnFinish?.Invoke();
        }
    }

    /// <summary>언리얼 UAbilityTask_WaitGameplayEvent. ASC 로 들어오는 게임플레이 이벤트를 기다린다.</summary>
    public sealed class AbilityTask_WaitGameplayEvent : AbilityTask
    {
        public event Action<GameplayEventData> EventReceived;
        private GameplayTag _tag;
        private bool _onlyTriggerOnce;
        private bool _onlyMatchExact;

        public static AbilityTask_WaitGameplayEvent WaitGameplayEvent(GameplayAbility owningAbility, GameplayTag eventTag,
            bool onlyTriggerOnce = false, bool onlyMatchExact = true)
        {
            var task = NewAbilityTask<AbilityTask_WaitGameplayEvent>(owningAbility);
            task._tag = eventTag;
            task._onlyTriggerOnce = onlyTriggerOnce;
            task._onlyMatchExact = onlyMatchExact;
            return task;
        }

        protected override void Activate()
        {
            if (AbilitySystemComponent != null) AbilitySystemComponent.GenericGameplayEvent += OnEvent;
        }

        private void OnEvent(GameplayTag tag, GameplayEventData payload)
        {
            bool match = _onlyMatchExact ? tag.MatchesTagExact(_tag) : tag.MatchesTag(_tag);
            if (!match || !ShouldBroadcastAbilityTaskDelegates()) return;
            EventReceived?.Invoke(payload);
            if (_onlyTriggerOnce) EndTask();
        }

        protected override void OnDestroy(bool abilityEnded)
        {
            if (AbilitySystemComponent != null) AbilitySystemComponent.GenericGameplayEvent -= OnEvent;
        }
    }

    /// <summary>언리얼 UAbilityTask_WaitInputRelease. 발동시킨 입력이 떼어질 때까지 기다린다.</summary>
    public sealed class AbilityTask_WaitInputRelease : AbilityTask
    {
        /// <summary>누르고 있던 시간(초).</summary>
        public event Action<float> OnRelease;
        private float _held;

        public static AbilityTask_WaitInputRelease WaitInputRelease(GameplayAbility owningAbility, bool testAlreadyReleased = true)
        {
            var task = NewAbilityTask<AbilityTask_WaitInputRelease>(owningAbility);
            task._testAlreadyReleased = testAlreadyReleased;
            return task;
        }

        private bool _testAlreadyReleased;

        protected override void Activate()
        {
            bTickingTask = true;
            if (!_testAlreadyReleased) return;
            var spec = Ability.GetCurrentAbilitySpec();
            if (spec != null && !spec.InputPressed) NotifyReleased();
        }

        internal override void TickTask(float deltaTime) => _held += deltaTime;

        internal void NotifyReleased()
        {
            if (IsFinished) return;
            bool broadcast = ShouldBroadcastAbilityTaskDelegates();
            float held = _held;
            EndTask();
            if (broadcast) OnRelease?.Invoke(held);
        }
    }

    /// <summary>언리얼 UAbilityTask_WaitInputPress. 실행 중에 입력이 다시 눌리기를 기다린다 (콤보 등).</summary>
    public sealed class AbilityTask_WaitInputPress : AbilityTask
    {
        public event Action<float> OnPress;
        private float _waited;

        public static AbilityTask_WaitInputPress WaitInputPress(GameplayAbility owningAbility) =>
            NewAbilityTask<AbilityTask_WaitInputPress>(owningAbility);

        protected override void Activate() => bTickingTask = true;

        internal override void TickTask(float deltaTime) => _waited += deltaTime;

        internal void NotifyPressed()
        {
            if (IsFinished) return;
            bool broadcast = ShouldBroadcastAbilityTaskDelegates();
            float waited = _waited;
            EndTask();
            if (broadcast) OnPress?.Invoke(waited);
        }
    }

    /// <summary>
    /// 언리얼 UAbilityTask_PlayMontageAndWait. 애니메이션을 재생하고 지정 시간이 지나면 완료된다.
    /// 재생 신호는 ASC 가 다른 피어에도 전파한다 (언리얼의 RepAnimMontageInfo 에 해당).
    /// Unity 애니메이터에는 몽타주 길이 개념이 없으므로 완료 시간은 호출자가 넘긴다.
    /// </summary>
    public sealed class AbilityTask_PlayMontageAndWait : AbilityTask
    {
        public event Action OnCompleted;
        public event Action OnInterrupted;
        public event Action OnCancelled;

        private int _triggerHash;
        private int _indexParamHash;
        private int _index;
        private float _remaining;

        public static AbilityTask_PlayMontageAndWait CreatePlayMontageAndWaitProxy(GameplayAbility owningAbility,
            int triggerHash, int indexParamHash, int index, float durationSeconds)
        {
            var task = NewAbilityTask<AbilityTask_PlayMontageAndWait>(owningAbility);
            task._triggerHash = triggerHash;
            task._indexParamHash = indexParamHash;
            task._index = index;
            task._remaining = Math.Max(0f, durationSeconds);
            return task;
        }

        protected override void Activate()
        {
            Ability.PlayAnimation(_triggerHash, _indexParamHash, _index);
            bTickingTask = true;
            if (_remaining <= 0f) Complete();
        }

        internal override void TickTask(float deltaTime)
        {
            _remaining -= deltaTime;
            if (_remaining <= 0f) Complete();
        }

        private void Complete()
        {
            bool broadcast = ShouldBroadcastAbilityTaskDelegates();
            EndTask();
            if (broadcast) OnCompleted?.Invoke();
        }

        protected override void OnDestroy(bool abilityEnded)
        {
            if (!abilityEnded) return;
            // 어빌리티가 먼저 끝났다 = 몽타주가 중간에 끊겼다.
            if (Ability != null && Ability.IsActive) OnInterrupted?.Invoke();
            else OnCancelled?.Invoke();
        }
    }

    /// <summary>언리얼 UAbilityTask_WaitGameplayTagAdded / Removed.</summary>
    public sealed class AbilityTask_WaitGameplayTag : AbilityTask
    {
        public event Action Changed;
        private GameplayTag _tag;
        private bool _waitForAdded;
        private bool _onlyTriggerOnce;
        private GameplayTagCountContainer.TagEvent _event;

        public static AbilityTask_WaitGameplayTag WaitGameplayTagAdd(GameplayAbility owningAbility, GameplayTag tag, bool onlyTriggerOnce = true) =>
            Create(owningAbility, tag, true, onlyTriggerOnce);

        public static AbilityTask_WaitGameplayTag WaitGameplayTagRemove(GameplayAbility owningAbility, GameplayTag tag, bool onlyTriggerOnce = true) =>
            Create(owningAbility, tag, false, onlyTriggerOnce);

        private static AbilityTask_WaitGameplayTag Create(GameplayAbility owningAbility, GameplayTag tag, bool added, bool once)
        {
            var task = NewAbilityTask<AbilityTask_WaitGameplayTag>(owningAbility);
            task._tag = tag;
            task._waitForAdded = added;
            task._onlyTriggerOnce = once;
            return task;
        }

        protected override void Activate()
        {
            var asc = AbilitySystemComponent;
            if (asc == null) return;
            bool has = asc.HasMatchingGameplayTag(_tag);
            if (has == _waitForAdded)
            {
                Fire();
                if (IsFinished) return;
            }
            _event = asc.RegisterGameplayTagEvent(_tag, GameplayTagEventType.NewOrRemoved);
            _event.OnEvent += OnTagChanged;
        }

        private void OnTagChanged(GameplayTag tag, int count)
        {
            if ((count > 0) == _waitForAdded) Fire();
        }

        private void Fire()
        {
            if (!ShouldBroadcastAbilityTaskDelegates()) return;
            Changed?.Invoke();
            if (_onlyTriggerOnce) EndTask();
        }

        protected override void OnDestroy(bool abilityEnded)
        {
            if (_event != null) _event.OnEvent -= OnTagChanged;
        }
    }
}
