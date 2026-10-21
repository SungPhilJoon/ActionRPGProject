using System.Collections.Generic;
using ActionRPG.Combat;
using ActionRPG.Core.AbilitySystem;
using UnityEngine;

namespace ActionRPG.Abilities
{
    /// <summary>
    /// 회복 공식. SetByCaller 기본 회복량 + 시전자 주문력 × 계수 + 대상 최대 체력 × 비율 → IncomingHealing.
    /// </summary>
    [CreateAssetMenu(menuName = "ActionRPG/Abilities/Heal Execution", fileName = "EXEC_Heal")]
    public class HealExecution : GameplayEffectExecutionCalculation
    {
        private static readonly GameplayEffectAttributeCaptureDefinition AbilityPowerDef =
            new GameplayEffectAttributeCaptureDefinition(CombatSet.AbilityPowerAttribute, GameplayEffectAttributeCaptureSource.Source, true);
        private static readonly GameplayEffectAttributeCaptureDefinition MaxHealthDef =
            new GameplayEffectAttributeCaptureDefinition(HealthSet.MaxHealthAttribute, GameplayEffectAttributeCaptureSource.Target, false);

        private void OnEnable()
        {
            RelevantAttributesToCapture = new List<GameplayEffectAttributeCaptureDefinition> { AbilityPowerDef, MaxHealthDef };
        }

        public override void Execute(GameplayEffectCustomExecutionParameters executionParams, GameplayEffectCustomExecutionOutput output)
        {
            var spec = executionParams.OwningSpec;
            float flat = spec.GetSetByCallerMagnitude(GameTags.SetByCaller_Heal, false);
            float abpScale = spec.GetSetByCallerMagnitude(GameTags.SetByCaller_AbilityPowerScale, false);
            float maxPercent = spec.GetSetByCallerMagnitude(GameTags.SetByCaller_HealMaxHealthPercent, false);

            executionParams.AttemptCalculateCapturedAttributeMagnitude(AbilityPowerDef, out float abilityPower);
            executionParams.AttemptCalculateCapturedAttributeMagnitude(MaxHealthDef, out float maxHealth);

            float amount = flat + abilityPower * abpScale + maxHealth * maxPercent;
            if (amount > 0f)
                output.AddOutputModifier(new GameplayModifierEvaluatedData(HealthSet.IncomingHealingAttribute, GameplayModOp.Additive, amount));
        }
    }
}
