using System.Collections;
using System.Collections.Generic;
using ActionRPG.Abilities;
using ActionRPG.Character;
using ActionRPG.Combat;
using ActionRPG.Core;
using ActionRPG.Core.AbilitySystem;
using Unity.Netcode;
using UnityEngine;

namespace ActionRPG.Skills.Effects
{
    /// <summary>자신 또는 주변 아군을 회복한다 (GE_Heal + HealExecution).</summary>
    [CreateAssetMenu(menuName = "ActionRPG/Skills/Effects/Heal", fileName = "EF_Heal")]
    public class HealEffect : SkillEffect
    {
        [Header("대상")]
        public bool IncludeSelf = true;
        [Tooltip("0 이면 자신에게만.")]
        [Min(0f)] public float AllyRadius = 0f;

        [Header("회복량")]
        public float FlatHeal = 25f;
        public float AbilityPowerScale = 0.8f;
        [Tooltip("대상 최대 체력의 몇 %를 추가로 회복할지. 0.1 == 10%.")]
        [Range(0f, 1f)] public float MaxHealthPercent = 0f;

        private static readonly List<IDamageable> Buffer = new List<IDamageable>(8);

        public override void Execute(in SkillCastContext ctx)
        {
            if (AllyRadius <= 0f)
            {
                if (IncludeSelf) HealTarget(ctx, ctx.CasterObject);
                return;
            }

            CombatQuery.OverlapAllies(ctx.CasterTransform.position, AllyRadius, ctx.Faction, Buffer, true, GameConstants.Layers.CharacterMask);
            for (int i = 0; i < Buffer.Count; i++)
            {
                if (!IncludeSelf && Buffer[i].Transform == ctx.CasterTransform) continue;
                HealTarget(ctx, Buffer[i].Transform.gameObject);
            }
        }

        private void HealTarget(in SkillCastContext ctx, GameObject target)
        {
            if (target == null) return;
            var spec = CombatSpecs.MakeHealSpec(ctx.SourceASC, ctx.MakeEffectContext(target.transform.position, Vector3.up),
                FlatHeal, AbilityPowerScale, MaxHealthPercent, ctx.Level);
            CombatSpecs.ApplyToTarget(ctx.SourceASC, spec, target);
        }
    }
}
