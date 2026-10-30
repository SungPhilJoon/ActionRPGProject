using System.Collections.Generic;
using Framework.Ability;
using UnityEngine;

namespace Framework
{
    /// <summary>
    /// 액터(플레이어/몬스터 등) 공통 부모. AbilitySystemComponent 를 상태의 단일 원본으로 삼는다.
    ///  - 태그 / 이펙트 / 어빌리티는 ASC 프로퍼티(Tags, Effects, GrantedAbilities)를 그대로 노출한다 (액터에 사본을 두지 않음).
    ///  - ASC 이벤트(태그 0↔1, 이펙트 추가/제거/실행, 어빌리티 시작/종료, 게임플레이 이벤트)를 구독해
    ///    가상 훅(OnTagChanged 등)으로 넘긴다 → 파생 클래스는 매 프레임 폴링 대신 변화에 반응한다.
    /// 프레임워크는 게임 타입을 모르므로 구체 AttributeSet / 어빌리티 생성은 파생 클래스가 결정한다.
    /// </summary>
    [RequireComponent(typeof(AbilitySystemComponent))]
    public abstract class ActorBehaviour : BaseBehaviour
    {
        public long ActorId { get; private set; }
        public AbilitySystemComponent Asc { get; private set; }

        public GameplayTagCountContainer Tags
        {
            get { return Asc.Tags; }
        }

        public IReadOnlyList<ActiveGameplayEffect> Effects
        {
            get { return Asc.Effects; }
        }

        public IReadOnlyList<GameplayAbility> Abilities
        {
            get { return Asc.GrantedAbilities; }
        }

        /// <summary>파생 클래스의 Initialize 에서 가장 먼저 호출한다. ASC 소유자/속성을 연결하고 ASC 이벤트를 구독한다.</summary>
        protected void InitializeActor(long InActorId, AttributeSet InAttributes)
        {
            ActorId = InActorId;
            if (Asc == null)
            {
                Asc = GetComponent<AbilitySystemComponent>();
                bindAscEvents(true);
            }

            Asc.BindOwner(this);
            Asc.InitAttributes(InAttributes);
        }

        protected override void OnUnityDestroy()
        {
            if (Asc != null)
            {
                bindAscEvents(false);
            }

            base.OnUnityDestroy();
        }

        // ------------------------------------------------------------------ tags

        public bool HasTag(int InTag)
        {
            return Asc.Tags.HasTag(InTag);
        }

        public void AddTag(int InTag)
        {
            Asc.Tags.AddTag(InTag);
        }

        public void RemoveTag(int InTag)
        {
            Asc.Tags.RemoveTag(InTag);
        }

        // ------------------------------------------------------------------ effects

        /// <summary>자신이 Source 인 스펙을 만들어 자신에게 적용한다 (장비/혈액/상태 등 상시 이펙트). 지속형이면 핸들을 돌려준다.</summary>
        public int ApplyEffectToSelf(GameplayEffectDef InDef, int InAbilityId)
        {
            if (InDef == null)
            {
                return 0;
            }

            return Asc.ApplyGameplayEffectSpecToSelf(Asc.MakeOutgoingSpec(InDef, InAbilityId));
        }

        public void RemoveEffect(int InHandle)
        {
            if (InHandle == 0)
            {
                return;
            }

            Asc.RemoveActiveEffect(InHandle);
        }

        // ------------------------------------------------------------------ abilities

        public virtual T GrantAbility<T>(T InAbility) where T : GameplayAbility
        {
            return Asc.GiveAbility(InAbility);
        }

        public virtual void RemoveAbility(GameplayAbility InAbility)
        {
            Asc.ClearAbility(InAbility);
        }

        // ------------------------------------------------------------------ ASC event hooks

        /// <summary>부모 포함 태그 카운트가 0↔1 로 바뀌었을 때. InAdded == false 면 마지막 부여가 빠진 것.</summary>
        protected virtual void OnTagChanged(int InTag, bool InAdded)
        {
        }

        protected virtual void OnEffectAdded(ActiveGameplayEffect InEffect)
        {
        }

        protected virtual void OnEffectRemoved(ActiveGameplayEffect InEffect)
        {
        }

        protected virtual void OnEffectExecuted(GameplayEffectSpec InSpec, EvaluatedModifier InModifier)
        {
        }

        protected virtual void OnAbilityActivated(GameplayAbility InAbility)
        {
        }

        protected virtual void OnAbilityEnded(GameplayAbility InAbility, bool InWasCancelled)
        {
        }

        protected virtual void OnGameplayEvent(int InEventTag, GameplayCueParams InPayload)
        {
        }

        private void bindAscEvents(bool InBind)
        {
            if (InBind == true)
            {
                Asc.Tags.OnTagChanged += handleTagChanged;
                Asc.OnEffectAdded += OnEffectAdded;
                Asc.OnEffectRemoved += OnEffectRemoved;
                Asc.OnEffectExecuted += OnEffectExecuted;
                Asc.OnAbilityActivated += OnAbilityActivated;
                Asc.OnAbilityEnded += OnAbilityEnded;
                Asc.OnGameplayEvent += OnGameplayEvent;
            }
            else
            {
                Asc.Tags.OnTagChanged -= handleTagChanged;
                Asc.OnEffectAdded -= OnEffectAdded;
                Asc.OnEffectRemoved -= OnEffectRemoved;
                Asc.OnEffectExecuted -= OnEffectExecuted;
                Asc.OnAbilityActivated -= OnAbilityActivated;
                Asc.OnAbilityEnded -= OnAbilityEnded;
                Asc.OnGameplayEvent -= OnGameplayEvent;
            }
        }

        private void handleTagChanged(int InTag, int InCount)
        {
            OnTagChanged(InTag, InCount > 0);
        }
    }
}
