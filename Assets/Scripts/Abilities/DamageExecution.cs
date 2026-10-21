using System.Collections.Generic;
using ActionRPG.Combat;
using ActionRPG.Core.AbilitySystem;
using UnityEngine;

namespace ActionRPG.Abilities
{
    /// <summary>
    /// 피해 공식 (언리얼 UGameplayEffectExecutionCalculation).
    /// SetByCaller 로 받은 기본 피해 · 계수에 공격자 공격력/주문력(스냅샷)을 곱하고, 치명타를 굴린 뒤,
    /// 대상 방어력(적용 시점 값)으로 감쇠해 IncomingDamage 메타 어트리뷰트에 넣는다.
    /// </summary>
    [CreateAssetMenu(menuName = "ActionRPG/Abilities/Damage Execution", fileName = "EXEC_Damage")]
    public class DamageExecution : GameplayEffectExecutionCalculation
    {
        private static readonly GameplayEffectAttributeCaptureDefinition AttackPowerDef =
            new GameplayEffectAttributeCaptureDefinition(CombatSet.AttackPowerAttribute, GameplayEffectAttributeCaptureSource.Source, true);
        private static readonly GameplayEffectAttributeCaptureDefinition AbilityPowerDef =
            new GameplayEffectAttributeCaptureDefinition(CombatSet.AbilityPowerAttribute, GameplayEffectAttributeCaptureSource.Source, true);
        private static readonly GameplayEffectAttributeCaptureDefinition CritChanceDef =
            new GameplayEffectAttributeCaptureDefinition(CombatSet.CritChanceAttribute, GameplayEffectAttributeCaptureSource.Source, true);
        private static readonly GameplayEffectAttributeCaptureDefinition CritDamageDef =
            new GameplayEffectAttributeCaptureDefinition(CombatSet.CritDamageAttribute, GameplayEffectAttributeCaptureSource.Source, true);
        private static readonly GameplayEffectAttributeCaptureDefinition DefenseDef =
            new GameplayEffectAttributeCaptureDefinition(CombatSet.DefenseAttribute, GameplayEffectAttributeCaptureSource.Target, false);

        private void OnEnable()
        {
            RelevantAttributesToCapture = new List<GameplayEffectAttributeCaptureDefinition>
            {
                AttackPowerDef, AbilityPowerDef, CritChanceDef, CritDamageDef, DefenseDef
            };
        }

        public override void Execute(GameplayEffectCustomExecutionParameters executionParams, GameplayEffectCustomExecutionOutput output)
        {
            var spec = executionParams.OwningSpec;
            float flat = spec.GetSetByCallerMagnitude(GameTags.SetByCaller_Damage, false);
            float apScale = spec.GetSetByCallerMagnitude(GameTags.SetByCaller_AttackPowerScale, false);
            float abpScale = spec.GetSetByCallerMagnitude(GameTags.SetByCaller_AbilityPowerScale, false);

            executionParams.AttemptCalculateCapturedAttributeMagnitude(AttackPowerDef, out float attackPower);
            executionParams.AttemptCalculateCapturedAttributeMagnitude(AbilityPowerDef, out float abilityPower);
            executionParams.AttemptCalculateCapturedAttributeMagnitude(CritChanceDef, out float critChance);
            if (!executionParams.AttemptCalculateCapturedAttributeMagnitude(CritDamageDef, out float critDamage)) critDamage = 1.5f;
            executionParams.AttemptCalculateCapturedAttributeMagnitude(DefenseDef, out float defense);

            float raw = flat + attackPower * apScale + abilityPower * abpScale;
            bool crit = critChance > 0f && Random.value < critChance;
            if (crit) raw *= Mathf.Max(1f, critDamage);

            var tags = new GameplayTagContainer();
            spec.GetAllAssetTags(tags);
            float mitigated = DamageCalculator.Mitigate(Mathf.Max(0f, raw), GameTags.DamageTypeFromTags(tags), defense);
            spec.Context.IsCriticalHit = crit;

            if (mitigated > 0f)
                output.AddOutputModifier(new GameplayModifierEvaluatedData(HealthSet.IncomingDamageAttribute, GameplayModOp.Additive, mitigated));
        }
    }
}
