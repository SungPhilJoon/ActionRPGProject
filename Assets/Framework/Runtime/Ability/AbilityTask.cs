using System;

namespace Framework.Ability
{
    /// <summary>
    /// 언리얼 UAbilityTask 대응. 어빌리티 안에서 "시간이 걸리는 일" 을 하나의 객체로 표현한다.
    /// 어빌리티가 끝나면 남아 있는 태스크는 Cancel 된다.
    /// </summary>
    public abstract class AbilityTask
    {
        public GameplayAbility Ability;
        public bool IsFinished { get; private set; }

        public virtual void Start()
        {
        }

        public abstract void Tick(float InDeltaTime);

        public void Cancel()
        {
            if (IsFinished == true)
            {
                return;
            }

            IsFinished = true;
            OnCancel();
        }

        protected void Finish()
        {
            IsFinished = true;
        }

        protected virtual void OnCancel()
        {
        }
    }

    /// <summary>InDelay 초 뒤 콜백 (UAbilityTask_WaitDelay).</summary>
    public class WaitDelayTask : AbilityTask
    {
        private float Remaining;
        private readonly Action OnFinished;

        public WaitDelayTask(float InDelay, Action InOnFinished)
        {
            Remaining = InDelay;
            OnFinished = InOnFinished;
        }

        public override void Tick(float InDeltaTime)
        {
            Remaining -= InDeltaTime;
            if (Remaining > 0.0f)
            {
                return;
            }

            Finish();
            OnFinished?.Invoke();
        }
    }

    /// <summary>InDuration 동안 매 프레임 (경과 비율 0~1) 콜백, 끝나면 완료 콜백.</summary>
    public class WaitDurationTask : AbilityTask
    {
        private readonly float Duration;
        private float Elapsed;
        private readonly Action<float, float> OnUpdate;
        private readonly Action OnFinished;

        public WaitDurationTask(float InDuration, Action<float, float> InOnUpdate, Action InOnFinished)
        {
            Duration = Math.Max(0.0001f, InDuration);
            OnUpdate = InOnUpdate;
            OnFinished = InOnFinished;
        }

        public override void Tick(float InDeltaTime)
        {
            Elapsed += InDeltaTime;
            float ratio = Math.Min(1.0f, Elapsed / Duration);
            OnUpdate?.Invoke(ratio, InDeltaTime);
            if (ratio < 1.0f)
            {
                return;
            }

            Finish();
            OnFinished?.Invoke();
        }
    }
}
