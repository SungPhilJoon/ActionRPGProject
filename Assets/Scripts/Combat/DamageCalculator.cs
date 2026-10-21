using ActionRPG.Core;
using UnityEngine;

namespace ActionRPG.Combat
{
    /// <summary>
    /// 피해 공식에 쓰는 순수 계산 함수. 실제 피해 계산 흐름은 GAS 의 DamageExecution 이 담당하고,
    /// 여기에는 그 안에서 쓰는 수식만 남겨 둔다. 밸런싱은 이 파일과 DamageExecution 에서 만진다.
    /// </summary>
    public static class DamageCalculator
    {
        /// <summary>방어력 100 당 피해 50% 감소 (수확 체감형).</summary>
        private const float DefenseConstant = 100f;

        /// <summary>대상의 방어력을 적용한 실 피해량.</summary>
        public static float Mitigate(float rawAmount, DamageType type, float defense)
        {
            if (type == DamageType.True) return rawAmount;
            if (defense <= 0f) return rawAmount;
            float reduction = DefenseConstant / (DefenseConstant + defense);
            return rawAmount * reduction;
        }

        /// <summary>거리에 따라 피해를 줄인다 (폭발 등). 0 이면 중심, 1 이면 가장자리.</summary>
        public static float FalloffMultiplier(float normalizedDistance, AnimationCurve curve)
        {
            if (curve == null || curve.length == 0) return 1f;
            return Mathf.Max(0f, curve.Evaluate(Mathf.Clamp01(normalizedDistance)));
        }
    }
}
