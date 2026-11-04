using System.Collections.Generic;
using UnityEngine;

namespace Framework.Ability
{
    /// <summary>어빌리티 발동 시점의 조준 정보 (언리얼 FGameplayAbilityTargetData 의 단순화 버전).</summary>
    public struct AbilityActivationInfo
    {
        public Vector3 AimPoint;
        public Vector3 AimDirection;
        public int InputSlot;
        public AbilitySystemComponent Target;
    }

    public enum EActivateFailReason
    {
        None = 0,
        AlreadyActive,
        Cooldown,
        Cost,
        BlockedByTags,
        MissingRequiredTags,
        Custom,
    }

    /// <summary>
    /// 언리얼 UGameplayAbility 대응 (InstancedPerActor).
    /// 공통 흐름(태그 검사 → 커밋(비용/쿨다운) → 태스크 진행 → 종료)은 여기서 처리하고,
    /// 실제 행동은 하위 클래스가 OnActivate 와 AbilityTask 로 구현한다 (Template Method 패턴).
    /// </summary>
    public abstract class GameplayAbility
    {
        public int AbilityId;
        public AbilitySystemComponent Owner { get; private set; }

        public GameplayTagContainer AbilityTags = new GameplayTagContainer();
        public readonly GameplayTagContainer ActivationOwnedTags = new GameplayTagContainer();
        public readonly GameplayTagContainer ActivationRequiredTags = new GameplayTagContainer();
        public readonly GameplayTagContainer ActivationBlockedTags = new GameplayTagContainer();
        public readonly GameplayTagContainer CancelAbilitiesWithTags = new GameplayTagContainer();
        public readonly GameplayTagContainer BlockAbilitiesWithTags = new GameplayTagContainer();

        public GameplayEffectDef CooldownEffect;
        public float CooldownDuration;
        public GameplayEffectDef CostEffect;
        public int CostAttribute = -1;
        public float CostAmount;
        /// <summary>SetByCaller 키: 쿨다운 시간, 비용 크기를 공용 이펙트에 실어 보낼 때 사용.</summary>
        public int SetByCallerCooldownKey;
        public int SetByCallerCostKey;

        public bool IsActive { get; private set; }

        protected AbilityActivationInfo ActivationInfo;

        private readonly List<AbilityTask> Tasks = new List<AbilityTask>();
        private readonly List<AbilityTask> TaskBuffer = new List<AbilityTask>();

        public void Bind(AbilitySystemComponent InOwner)
        {
            Owner = InOwner;
            OnGiven();
        }

        public virtual bool CanActivate(out EActivateFailReason OutReason)
        {
            OutReason = EActivateFailReason.None;
            if (IsActive == true)
            {
                OutReason = EActivateFailReason.AlreadyActive;
                return false;
            }

            var owned = Owner.Tags;
            if (owned.HasAny(ActivationBlockedTags) == true || Owner.IsAbilityBlocked(AbilityTags) == true)
            {
                OutReason = EActivateFailReason.BlockedByTags;
                return false;
            }

            if (owned.HasAll(ActivationRequiredTags) == false)
            {
                OutReason = EActivateFailReason.MissingRequiredTags;
                return false;
            }

            if (Owner.GetCooldownRemaining(AbilityId) > 0.0f)
            {
                OutReason = EActivateFailReason.Cooldown;
                return false;
            }

            if (CheckCost() == false)
            {
                OutReason = EActivateFailReason.Cost;
                return false;
            }

            return true;
        }

        public bool CheckCost()
        {
            if (CostAttribute < 0 || CostAmount <= 0.0f)
            {
                return true;
            }

            return Owner.Attributes.GetCurrent(CostAttribute) >= CostAmount;
        }

        /// <summary>ASC 만 호출한다.</summary>
        public void InternalActivate(AbilityActivationInfo InInfo)
        {
            IsActive = true;
            ActivationInfo = InInfo;
            Owner.Tags.AddTags(ActivationOwnedTags);
            Owner.AddBlockedAbilityTags(BlockAbilitiesWithTags);
            Owner.CancelAbilitiesWithTags(CancelAbilitiesWithTags, this);
            OnActivate(InInfo);
        }

        /// <summary>비용과 쿨다운을 실제로 적용한다. 비용이 모자라면 false.</summary>
        protected bool CommitAbility()
        {
            if (CheckCost() == false)
            {
                return false;
            }

            if (CostEffect != null && CostAmount > 0.0f)
            {
                var cost = Owner.MakeOutgoingSpec(CostEffect, AbilityId);
                cost.SetByCaller(SetByCallerCostKey, -CostAmount);
                Owner.ApplyGameplayEffectSpecToSelf(cost);
            }

            if (CooldownEffect != null && CooldownDuration > 0.0f)
            {
                var cooldown = Owner.MakeOutgoingSpec(CooldownEffect, AbilityId);
                cooldown.DurationOverride = CooldownDuration * Owner.GetCooldownScale();
                cooldown.SetByCaller(SetByCallerCooldownKey, CooldownDuration);
                Owner.ApplyGameplayEffectSpecToSelf(cooldown);
            }

            return true;
        }

        public void EndAbility(bool InWasCancelled)
        {
            if (IsActive == false)
            {
                return;
            }

            IsActive = false;

            TaskBuffer.Clear();
            TaskBuffer.AddRange(Tasks);
            Tasks.Clear();
            for (int i = 0; i < TaskBuffer.Count; ++i)
            {
                TaskBuffer[i].Cancel();
            }

            Owner.Tags.RemoveTags(ActivationOwnedTags);
            Owner.RemoveBlockedAbilityTags(BlockAbilitiesWithTags);
            OnEnd(InWasCancelled);
            Owner.NotifyAbilityEnded(this, InWasCancelled);
        }

        public void CancelAbility()
        {
            EndAbility(true);
        }

        public void Tick(float InDeltaTime)
        {
            if (IsActive == false)
            {
                return;
            }

            TaskBuffer.Clear();
            TaskBuffer.AddRange(Tasks);
            for (int i = 0; i < TaskBuffer.Count; ++i)
            {
                var task = TaskBuffer[i];
                if (task.IsFinished == true)
                {
                    continue;
                }

                task.Tick(InDeltaTime);
            }

            Tasks.RemoveAll(t => t.IsFinished == true);
            if (IsActive == true)
            {
                OnTick(InDeltaTime);
            }
        }

        protected T AddTask<T>(T InTask) where T : AbilityTask
        {
            InTask.Ability = this;
            Tasks.Add(InTask);
            InTask.Start();
            return InTask;
        }

        protected virtual void OnGiven()
        {
        }

        protected abstract void OnActivate(AbilityActivationInfo InInfo);

        protected virtual void OnTick(float InDeltaTime)
        {
        }

        protected virtual void OnEnd(bool InWasCancelled)
        {
        }

        /// <summary>시전 중 입력이 다시 들어왔을 때 (콤보 등). 기본은 무시.</summary>
        public virtual void OnInputPressedWhileActive(AbilityActivationInfo InInfo)
        {
        }

        /// <summary>입력을 뗐을 때 (차징/채널링 어빌리티용).</summary>
        public virtual void OnInputReleased()
        {
        }
    }
}
