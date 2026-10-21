using System.Collections;
using System.Collections.Generic;
using ActionRPG.Abilities;
using ActionRPG.Combat;
using ActionRPG.Core;
using ActionRPG.Core.AbilitySystem;
using UnityEngine;

namespace ActionRPG.Skills.Effects
{
    /// <summary>지정한 지점에 원형 피해. 화살비, 폭발 장판 등.</summary>
    [CreateAssetMenu(menuName = "ActionRPG/Skills/Effects/Area Damage", fileName = "EF_AreaDamage")]
    public class AreaDamageEffect : SkillEffect
    {
        [Header("범위")]
        [Min(0.1f)] public float Radius = 4f;
        [Tooltip("중심에서 멀어질수록 피해가 줄어드는 곡선. 비우면 균일 피해.")]
        public AnimationCurve Falloff = AnimationCurve.Constant(0f, 1f, 1f);
        [Tooltip("true 면 시전자 위치, false 면 조준 지점을 중심으로 한다.")]
        public bool CenterOnCaster;

        [Header("피해")]
        public float FlatDamage = 18f;
        public float AttackPowerScale = 0.6f;
        public float AbilityPowerScale = 1f;
        public DamageType DamageType = DamageType.Magical;

        [Header("추가 효과")]
        public GameplayEffect ApplyStatus;

        [Header("연출")]
        public GameObject ImpactVfxPrefab;

        private static readonly List<IDamageable> Buffer = new List<IDamageable>(32);

        public override void Execute(in SkillCastContext ctx)
        {
            Vector3 center = CenterOnCaster ? ctx.CasterTransform.position : ctx.TargetPoint;
            CombatQuery.OverlapHostiles(center, Radius, ctx.Faction, Buffer, GameConstants.Layers.CharacterMask);

            for (int i = 0; i < Buffer.Count; i++)
            {
                var target = Buffer[i].Transform;
                float normalized = Radius > 0f ? Vector3.Distance(center, target.position) / Radius : 0f;
                float multiplier = DamageCalculator.FalloffMultiplier(normalized, Falloff);
                Vector3 hitPoint = target.position + Vector3.up;
                Vector3 hitDirection = (target.position - center).normalized;

                var spec = CombatSpecs.MakeDamageSpec(ctx.SourceASC, ctx.MakeEffectContext(hitPoint, hitDirection),
                    FlatDamage * multiplier, AttackPowerScale * multiplier, AbilityPowerScale * multiplier, DamageType, 0f, 0f, ctx.Level);
                CombatSpecs.ApplyToTarget(ctx.SourceASC, spec, target.gameObject);
                ctx.ApplyEffectToTarget(ApplyStatus, target.gameObject, hitPoint, hitDirection);
            }
        }

        public override void ExecuteVisual(in SkillCastContext ctx)
        {
            if (ImpactVfxPrefab == null) return;
            Vector3 center = CenterOnCaster && ctx.CasterTransform != null ? ctx.CasterTransform.position : ctx.TargetPoint;
            var vfx = Instantiate(ImpactVfxPrefab, center, Quaternion.identity);
            Destroy(vfx, 5f);
        }
    }
}
