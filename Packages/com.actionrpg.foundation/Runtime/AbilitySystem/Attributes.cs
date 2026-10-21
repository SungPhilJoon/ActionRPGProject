using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

namespace ActionRPG.Core.AbilitySystem
{
    /// <summary>
    /// 언리얼 FGameplayAttributeData. 기본값(BaseValue)과 현재값(CurrentValue)을 따로 가진다.
    ///  - BaseValue    : Instant / Periodic 이펙트가 영구적으로 바꾸는 값.
    ///  - CurrentValue : BaseValue 에 지속형(Duration/Infinite) 이펙트의 모디파이어를 합친 결과.
    /// </summary>
    [Serializable]
    public sealed class GameplayAttributeData
    {
        [SerializeField] private float baseValue;
        [NonSerialized] private float currentValue;

        public GameplayAttributeData() { }

        public GameplayAttributeData(float defaultValue)
        {
            baseValue = defaultValue;
            currentValue = defaultValue;
        }

        public float BaseValue => baseValue;
        public float CurrentValue => currentValue;

        internal void SetBaseValueInternal(float value) => baseValue = value;
        internal void SetCurrentValueInternal(float value) => currentValue = value;

        public override string ToString() => $"{currentValue:0.##} (base {baseValue:0.##})";
    }

    /// <summary>
    /// 이 표시가 붙은 어트리뷰트는 "메타 어트리뷰트"다. 이펙트 계산의 중간값(받을 피해량 등)으로만 쓰이고
    /// 네트워크로 복제하지 않는다. 언리얼에서 IncomingDamage 같은 어트리뷰트를 복제하지 않는 관례를 따른다.
    /// </summary>
    [AttributeUsage(AttributeTargets.Field)]
    public sealed class MetaAttributeAttribute : Attribute
    {
    }

    /// <summary>
    /// 언리얼 FGameplayAttribute. 어트리뷰트를 가리키는 식별자다. "HealthSet.Health" 형식의 이름으로 저장한다.
    /// 이펙트 에셋의 모디파이어가 어떤 어트리뷰트를 바꿀지 지정할 때 쓴다.
    /// </summary>
    [Serializable]
    public struct GameplayAttribute : IEquatable<GameplayAttribute>
    {
        [SerializeField] private string attributeName;

        public GameplayAttribute(string setName, string fieldName)
        {
            attributeName = $"{setName}.{fieldName}";
        }

        public static GameplayAttribute Of<TSet>(string fieldName) where TSet : AttributeSet =>
            new GameplayAttribute(typeof(TSet).Name, fieldName);

        public static GameplayAttribute FromName(string fullName)
        {
            var attribute = new GameplayAttribute();
            attribute.attributeName = fullName;
            return attribute;
        }

        public string Name => attributeName ?? string.Empty;
        public bool IsValid => !string.IsNullOrEmpty(attributeName);

        public string SetName
        {
            get
            {
                int dot = Name.IndexOf('.');
                return dot > 0 ? Name.Substring(0, dot) : string.Empty;
            }
        }

        public string FieldName
        {
            get
            {
                int dot = Name.IndexOf('.');
                return dot > 0 ? Name.Substring(dot + 1) : Name;
            }
        }

        public bool Equals(GameplayAttribute other) => string.Equals(Name, other.Name, StringComparison.Ordinal);
        public override bool Equals(object obj) => obj is GameplayAttribute other && Equals(other);
        public override int GetHashCode() => Name.GetHashCode();
        public override string ToString() => IsValid ? Name : "None";

        public static bool operator ==(GameplayAttribute a, GameplayAttribute b) => a.Equals(b);
        public static bool operator !=(GameplayAttribute a, GameplayAttribute b) => !a.Equals(b);
    }

    /// <summary>
    /// 언리얼 UAttributeSet. 서브클래스에 <see cref="GameplayAttributeData"/> 타입의 public 필드를 선언하면
    /// 그 필드 하나하나가 어트리뷰트가 된다.
    ///
    /// <code>
    /// public sealed class HealthSet : AttributeSet
    /// {
    ///     public readonly GameplayAttributeData Health = new GameplayAttributeData(100f);
    ///     public static readonly GameplayAttribute HealthAttribute = GameplayAttribute.Of&lt;HealthSet&gt;(nameof(Health));
    /// }
    /// </code>
    ///
    /// AttributeSet 은 ASC 가 모든 피어에서 같은 순서로 만들어야 한다. 복제 인덱스가 생성 순서로 정해지기 때문이다.
    /// </summary>
    public abstract class AttributeSet
    {
        /// <summary>리플렉션 결과 캐시. 타입당 한 번만 계산한다.</summary>
        internal sealed class FieldInfoCache
        {
            public readonly FieldInfo[] Fields;
            public readonly bool[] IsMeta;

            public FieldInfoCache(Type type)
            {
                var list = new List<FieldInfo>();
                foreach (var field in type.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic))
                    if (field.FieldType == typeof(GameplayAttributeData)) list.Add(field);
                // 선언 순서를 보장하기 위해 메타데이터 토큰으로 정렬한다.
                list.Sort((a, b) => a.MetadataToken.CompareTo(b.MetadataToken));
                Fields = list.ToArray();
                IsMeta = new bool[Fields.Length];
                for (int i = 0; i < Fields.Length; i++)
                    IsMeta[i] = Fields[i].GetCustomAttribute<MetaAttributeAttribute>() != null;
            }
        }

        private static readonly Dictionary<Type, FieldInfoCache> Cache = new Dictionary<Type, FieldInfoCache>();

        internal static FieldInfoCache GetCache(Type type)
        {
            if (!Cache.TryGetValue(type, out var cache))
            {
                cache = new FieldInfoCache(type);
                Cache[type] = cache;
            }
            return cache;
        }

        /// <summary>이 세트를 소유한 ASC.</summary>
        public AbilitySystemComponent OwningAbilitySystemComponent { get; internal set; }

        public string SetName => GetType().Name;

        /// <summary>
        /// 언리얼 PreAttributeChange. CurrentValue 가 바뀌기 직전. 값을 범위 안으로 자를 때 쓴다.
        /// 여기서 다른 어트리뷰트를 바꾸면 안 된다 (모디파이어 재계산 중일 수 있다).
        /// </summary>
        public virtual void PreAttributeChange(GameplayAttribute attribute, ref float newValue) { }

        /// <summary>언리얼 PostAttributeChange. CurrentValue 가 바뀐 직후.</summary>
        public virtual void PostAttributeChange(GameplayAttribute attribute, float oldValue, float newValue) { }

        /// <summary>언리얼 PreAttributeBaseChange. BaseValue 가 바뀌기 직전.</summary>
        public virtual void PreAttributeBaseChange(GameplayAttribute attribute, ref float newValue) { }

        /// <summary>언리얼 PostAttributeBaseChange. BaseValue 가 바뀐 직후.</summary>
        public virtual void PostAttributeBaseChange(GameplayAttribute attribute, float oldValue, float newValue) { }

        /// <summary>
        /// 언리얼 PreGameplayEffectExecute. Instant/Periodic 이펙트가 BaseValue 를 바꾸기 직전 (서버 전용).
        /// false 를 돌려주면 이번 모디파이어 적용을 취소한다 (무적 등).
        /// </summary>
        public virtual bool PreGameplayEffectExecute(GameplayEffectModCallbackData data) => true;

        /// <summary>
        /// 언리얼 PostGameplayEffectExecute. Instant/Periodic 이펙트가 BaseValue 를 바꾼 직후 (서버 전용).
        /// 메타 어트리뷰트(받을 피해량)를 실제 어트리뷰트(체력)로 옮기는 곳이다.
        /// </summary>
        public virtual void PostGameplayEffectExecute(GameplayEffectModCallbackData data) { }

        /// <summary>ASC 에 등록된 직후 한 번 호출된다.</summary>
        public virtual void OnRegistered() { }

        // ---------------------------------------------------------------- 편의 함수

        /// <summary>서버 전용. 세트 안의 어트리뷰트 BaseValue 를 바꾼다 (언리얼 SetNumericAttributeBase).</summary>
        protected void SetBase(GameplayAttribute attribute, float value)
        {
            if (OwningAbilitySystemComponent != null) OwningAbilitySystemComponent.SetNumericAttributeBase(attribute, value);
        }

        protected float GetCurrent(GameplayAttribute attribute) =>
            OwningAbilitySystemComponent != null ? OwningAbilitySystemComponent.GetNumericAttribute(attribute) : 0f;
    }

    /// <summary>언리얼 FOnAttributeChangeData.</summary>
    public readonly struct OnAttributeChangeData
    {
        public readonly GameplayAttribute Attribute;
        public readonly float NewValue;
        public readonly float OldValue;
        /// <summary>Instant/Periodic 이펙트가 원인이면 그 정보. 아니면 null.</summary>
        public readonly GameplayEffectModCallbackData GEModData;

        public OnAttributeChangeData(GameplayAttribute attribute, float newValue, float oldValue, GameplayEffectModCallbackData modData)
        {
            Attribute = attribute;
            NewValue = newValue;
            OldValue = oldValue;
            GEModData = modData;
        }
    }

    /// <summary>
    /// 언리얼 GetGameplayAttributeValueChangeDelegate 가 돌려주는 델리게이트.
    /// <c>asc.GetGameplayAttributeValueChangeDelegate(HealthSet.HealthAttribute).OnValueChanged += ...</c>
    /// </summary>
    public sealed class GameplayAttributeValueChangeDelegate
    {
        public event Action<OnAttributeChangeData> OnValueChanged;
        internal void Broadcast(in OnAttributeChangeData data) => OnValueChanged?.Invoke(data);
    }
}
