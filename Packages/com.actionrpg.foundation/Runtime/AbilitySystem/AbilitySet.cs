using System;
using System.Collections.Generic;
using UnityEngine;
using Object = UnityEngine.Object;

namespace ActionRPG.Core.AbilitySystem
{
    /// <summary>
    /// 라이라(Lyra) ULyraAbilitySet 에 해당한다. 어트리뷰트 세트 · 어빌리티 · 이펙트를 묶어서 한 번에 부여한다.
    /// 캐릭터 종류별(플레이어, 적) 기본 구성을 에셋 하나로 관리할 수 있다.
    /// </summary>
    [CreateAssetMenu(menuName = "ActionRPG/Ability System/Ability Set", fileName = "AS_New")]
    public class AbilitySet : ScriptableObject
    {
        [Serializable]
        public class AbilityEntry
        {
            public GameplayAbility Ability;
            [Min(1)] public int AbilityLevel = 1;
            [Tooltip("입력 슬롯. -1 이면 입력과 연결하지 않는다.")]
            public int InputID = -1;
        }

        [Serializable]
        public class EffectEntry
        {
            public GameplayEffect GameplayEffect;
            public float EffectLevel = 1f;
        }

        [Serializable]
        public class AttributeSetEntry
        {
            [Tooltip("AttributeSet 서브클래스의 '네임스페이스.타입, 어셈블리' 이름.")]
            [AttributeSetType] public string AttributeSetType;
        }

        [Tooltip("모든 피어가 같은 순서로 만든다. 순서를 바꾸면 복제 인덱스가 달라지므로 빌드 간에 맞춰야 한다.")]
        public AttributeSetEntry[] GrantedAttributes = Array.Empty<AttributeSetEntry>();
        [Tooltip("서버가 스폰 시 부여한다.")]
        public AbilityEntry[] GrantedGameplayAbilities = Array.Empty<AbilityEntry>();
        [Tooltip("서버가 스폰 시 적용한다. 순서대로 적용되므로 초기 스탯 이펙트를 앞에 둔다.")]
        public EffectEntry[] GrantedGameplayEffects = Array.Empty<EffectEntry>();

        /// <summary>부여한 것들의 핸들. TakeFromAbilitySystem 으로 회수할 때 쓴다.</summary>
        public sealed class GrantedHandles
        {
            public readonly List<GameplayAbilitySpecHandle> AbilitySpecHandles = new List<GameplayAbilitySpecHandle>();
            public readonly List<ActiveGameplayEffectHandle> GameplayEffectHandles = new List<ActiveGameplayEffectHandle>();

            public void TakeFromAbilitySystem(AbilitySystemComponent asc)
            {
                if (asc == null || !asc.IsAuthority) return;
                for (int i = 0; i < AbilitySpecHandles.Count; i++) asc.ClearAbility(AbilitySpecHandles[i]);
                for (int i = 0; i < GameplayEffectHandles.Count; i++) asc.RemoveActiveGameplayEffect(GameplayEffectHandles[i]);
                AbilitySpecHandles.Clear();
                GameplayEffectHandles.Clear();
            }
        }

        /// <summary>모든 피어에서 호출. 어트리뷰트 세트를 만든다.</summary>
        public void CreateAttributeSets(AbilitySystemComponent asc)
        {
            for (int i = 0; i < GrantedAttributes.Length; i++)
            {
                string typeName = GrantedAttributes[i]?.AttributeSetType;
                if (string.IsNullOrEmpty(typeName)) continue;
                var type = Type.GetType(typeName);
                if (type == null || !typeof(AttributeSet).IsAssignableFrom(type) || type.IsAbstract)
                {
                    Debug.LogError($"[GAS] {name}: 어트리뷰트 세트 타입 '{typeName}' 을 찾지 못했습니다.", this);
                    continue;
                }
                asc.GetOrCreateAttributeSet(type);
            }
        }

        /// <summary>서버 전용. 어빌리티를 주고 이펙트를 적용한다.</summary>
        public void GiveToAbilitySystem(AbilitySystemComponent asc, GrantedHandles outHandles, Object sourceObject = null)
        {
            if (asc == null || !asc.IsAuthority) return;
            for (int i = 0; i < GrantedGameplayAbilities.Length; i++)
            {
                var entry = GrantedGameplayAbilities[i];
                if (entry?.Ability == null) continue;
                var handle = asc.GiveAbility(new GameplayAbilitySpec(entry.Ability, entry.AbilityLevel, entry.InputID, sourceObject != null ? sourceObject : this));
                outHandles?.AbilitySpecHandles.Add(handle);
            }
            for (int i = 0; i < GrantedGameplayEffects.Length; i++)
            {
                var entry = GrantedGameplayEffects[i];
                if (entry?.GameplayEffect == null) continue;
                var spec = asc.MakeOutgoingSpec(entry.GameplayEffect, entry.EffectLevel, asc.MakeEffectContext());
                var handle = asc.ApplyGameplayEffectSpecToSelf(spec);
                if (handle.IsValid) outHandles?.GameplayEffectHandles.Add(handle);
            }
        }
    }

    /// <summary>인스펙터에서 AttributeSet 타입을 고르는 드롭다운을 띄우기 위한 표시.</summary>
    public sealed class AttributeSetTypeAttribute : PropertyAttribute
    {
    }
}
