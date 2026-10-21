using System;
using System.Collections.Generic;

namespace Framework.Ability
{
    /// <summary>
    /// 언리얼 UAttributeSet 대응. 속성은 int 인덱스로 접근하는 float 배열이다.
    ///  - BaseValue: 인스턴트 이펙트가 영구적으로 바꾸는 값 (체력 등)
    ///  - CurrentValue: Base 에 지속형 이펙트 모디파이어를 합산한 값 → (Base + ΣAdd) × (1 + ΣPercent/100), Override 가 있으면 그 값
    /// 게임 쪽은 이 클래스를 상속해 클램프(PreAttributeChange)와 메타 속성 처리(PostGameplayEffectExecute)를 구현한다.
    /// </summary>
    public abstract class AttributeSet
    {
        private struct ActiveModifier
        {
            public int EffectHandle;
            public EModifierOp Op;
            public float Magnitude;
        }

        private readonly float[] BaseValues;
        private readonly float[] CurrentValues;
        private readonly List<ActiveModifier>[] Modifiers;

        public AbilitySystemComponent Owner { get; private set; }

        /// <summary>(속성, 이전 값, 새 값)</summary>
        public event Action<int, float, float> OnAttributeChanged;

        protected AttributeSet(int InAttributeCount)
        {
            BaseValues = new float[InAttributeCount];
            CurrentValues = new float[InAttributeCount];
            Modifiers = new List<ActiveModifier>[InAttributeCount];
            for (int i = 0; i < InAttributeCount; ++i)
            {
                Modifiers[i] = new List<ActiveModifier>();
            }
        }

        public int Count
        {
            get { return BaseValues.Length; }
        }

        public void Bind(AbilitySystemComponent InOwner)
        {
            Owner = InOwner;
        }

        public float GetBase(int InAttribute)
        {
            return isValid(InAttribute) == true ? BaseValues[InAttribute] : 0.0f;
        }

        public float GetCurrent(int InAttribute)
        {
            return isValid(InAttribute) == true ? CurrentValues[InAttribute] : 0.0f;
        }

        public float[] SnapshotCurrent()
        {
            var copy = new float[CurrentValues.Length];
            Array.Copy(CurrentValues, copy, CurrentValues.Length);
            return copy;
        }

        /// <summary>초기화/치트용. 클램프를 거쳐 Base 를 바로 설정한다.</summary>
        public void SetBase(int InAttribute, float InValue)
        {
            if (isValid(InAttribute) == false)
            {
                return;
            }

            BaseValues[InAttribute] = InValue;
            Recalculate(InAttribute);
        }

        public void ApplyToBase(int InAttribute, EModifierOp InOp, float InMagnitude)
        {
            if (isValid(InAttribute) == false)
            {
                return;
            }

            float value = BaseValues[InAttribute];
            switch (InOp)
            {
                case EModifierOp.Add:
                    value += InMagnitude;
                    break;
                case EModifierOp.MultiplyPercent:
                    value *= 1.0f + InMagnitude * 0.01f;
                    break;
                case EModifierOp.Override:
                    value = InMagnitude;
                    break;
            }

            BaseValues[InAttribute] = value;
            Recalculate(InAttribute);
        }

        public void AddModifier(int InAttribute, int InEffectHandle, EModifierOp InOp, float InMagnitude)
        {
            if (isValid(InAttribute) == false)
            {
                return;
            }

            var modifier = new ActiveModifier();
            modifier.EffectHandle = InEffectHandle;
            modifier.Op = InOp;
            modifier.Magnitude = InMagnitude;
            Modifiers[InAttribute].Add(modifier);
            Recalculate(InAttribute);
        }

        public void RemoveModifiers(int InEffectHandle)
        {
            for (int attribute = 0; attribute < Modifiers.Length; ++attribute)
            {
                int removed = Modifiers[attribute].RemoveAll(m => m.EffectHandle == InEffectHandle);
                if (removed > 0)
                {
                    Recalculate(attribute);
                }
            }
        }

        public void Recalculate(int InAttribute)
        {
            float add = 0.0f;
            float percent = 0.0f;
            bool hasOverride = false;
            float overrideValue = 0.0f;

            var list = Modifiers[InAttribute];
            for (int i = 0; i < list.Count; ++i)
            {
                switch (list[i].Op)
                {
                    case EModifierOp.Add:
                        add += list[i].Magnitude;
                        break;
                    case EModifierOp.MultiplyPercent:
                        percent += list[i].Magnitude;
                        break;
                    case EModifierOp.Override:
                        hasOverride = true;
                        overrideValue = list[i].Magnitude;
                        break;
                }
            }

            float newValue = hasOverride == true ? overrideValue : (BaseValues[InAttribute] + add) * (1.0f + percent * 0.01f);
            PreAttributeChange(InAttribute, ref newValue);

            float oldValue = CurrentValues[InAttribute];
            CurrentValues[InAttribute] = newValue;
            if (oldValue != newValue)
            {
                OnAttributeChanged?.Invoke(InAttribute, oldValue, newValue);
                PostAttributeChange(InAttribute, oldValue, newValue);
            }
        }

        public void RecalculateAll()
        {
            for (int i = 0; i < BaseValues.Length; ++i)
            {
                Recalculate(i);
            }
        }

        /// <summary>Current 가 바뀌기 직전. 클램프에 사용한다 (예: Health ≤ MaxHealth).</summary>
        protected virtual void PreAttributeChange(int InAttribute, ref float InOutNewValue)
        {
        }

        protected virtual void PostAttributeChange(int InAttribute, float InOldValue, float InNewValue)
        {
        }

        /// <summary>인스턴트/주기 이펙트가 Base 를 바꾼 직후. 메타 속성(IncomingDamage 등)을 실제 속성으로 옮긴다.</summary>
        public virtual void PostGameplayEffectExecute(EffectExecuteData InData)
        {
        }

        /// <summary>메타 속성을 다룰 때 Base 를 직접 바꾸는 용도 (클램프는 Recalculate 에서).</summary>
        protected void SetBaseRaw(int InAttribute, float InValue)
        {
            BaseValues[InAttribute] = InValue;
            Recalculate(InAttribute);
        }

        private bool isValid(int InAttribute)
        {
            return InAttribute >= 0 && InAttribute < BaseValues.Length;
        }
    }
}
