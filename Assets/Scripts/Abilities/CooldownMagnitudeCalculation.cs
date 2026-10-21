using System.Collections.Generic;
using ActionRPG.Combat;
using ActionRPG.Core.AbilitySystem;
using UnityEngine;

namespace ActionRPG.Abilities
{
    /// <summary>
    /// 쿨다운 길이 계산 (언리얼 UGameplayModMagnitudeCalculation).
    /// SetByCaller 로 받은 기본 쿨다운에 쿨다운 감소 또는 공격 속도를 반영한다.
    /// 공용 쿨다운 이펙트 하나로 모든 스킬의 쿨다운을 처리하기 위한 것이다.
    /// </summary>
    [CreateAssetMenu(menuName = "ActionRPG/Abilities/Cooldown Magnitude Calculation", fileName = "MMC_Cooldown")]
    public class CooldownMagnitudeCalculation : GameplayModMagnitudeCalculation
    {
        [Tooltip("시전자의 쿨다운 감소(CooldownReduction)를 반영한다. 스킬용.")]
        public bool ApplyCooldownReduction = true;
        [Tooltip("시전자의 공격 속도(AttackSpeed)로 나눈다. 평타용.")]
        public bool DivideByAttackSpeed;

        private static readonly GameplayEffectAttributeCaptureDefinition CooldownReductionDef =
            new GameplayEffectAttributeCaptureDefinition(CombatSet.CooldownReductionAttribute, GameplayEffectAttributeCaptureSource.Source, true);
        private static readonly GameplayEffectAttributeCaptureDefinition AttackSpeedDef =
            new GameplayEffectAttributeCaptureDefinition(CombatSet.AttackSpeedAttribute, GameplayEffectAttributeCaptureSource.Source, true);

        private void OnEnable()
        {
            RelevantAttributesToCapture = new List<GameplayEffectAttributeCaptureDefinition> { CooldownReductionDef, AttackSpeedDef };
        }

        public override float CalculateBaseMagnitude(GameplayEffectSpec spec)
        {
            float cooldown = GetSetByCallerMagnitudeByTag(spec, GameTags.SetByCaller_Cooldown);
            if (ApplyCooldownReduction && GetCapturedAttributeMagnitude(CooldownReductionDef, spec, out float cdr))
                cooldown *= 1f - Mathf.Clamp(cdr, 0f, 0.8f);
            if (DivideByAttackSpeed && GetCapturedAttributeMagnitude(AttackSpeedDef, spec, out float attackSpeed))
                cooldown /= Mathf.Max(0.2f, attackSpeed);
            return Mathf.Max(0f, cooldown);
        }
    }
}
