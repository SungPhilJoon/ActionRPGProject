using System.Collections;
using System.Collections.Generic;
using ActionRPG.Abilities;
using ActionRPG.Combat;
using ActionRPG.Core;
using ActionRPG.Core.AbilitySystem;
using UnityEngine;

namespace ActionRPG.Skills.Effects
{
    /// <summary>시전자 앞 부채꼴을 때린다. 검 스킬의 기본형.</summary>
    [CreateAssetMenu(menuName = "ActionRPG/Skills/Effects/Arc Damage", fileName = "EF_ArcDamage")]
    public class ArcDamageEffect : SkillEffect
    {
        [Header("범위")]
        [Min(0.1f)] public float Radius = 3f;
        [Range(10f, 360f)] public float ArcDegrees = 120f;
        [Tooltip("시전자 앞쪽으로 판정 중심을 얼마나 밀지.")]
        public float ForwardOffset = 0.5f;

        [Header("피해 (GE_Damage 의 SetByCaller 로 전달)")]
        public float FlatDamage = 10f;
        public float AttackPowerScale = 1.2f;
        public float AbilityPowerScale = 0f;
        public DamageType DamageType = DamageType.Physical;
        public float Knockback = 3f;
        [Tooltip("0 이하면 무제한.")]
        public int MaxTargets = 0;

        [Header("추가 효과")]
        [Tooltip("맞은 대상에게 걸 게임플레이 이펙트 (기절 · 둔화 등).")]
        public GameplayEffect ApplyStatus;

        private static readonly List<IDamageable> Buffer = new List<IDamageable>(32);

        public override void Execute(in SkillCastContext ctx)
        {
            Vector3 center = ctx.Origin + ctx.Direction * ForwardOffset;
            CombatQuery.OverlapHostileArc(center, ctx.Direction, Radius, ArcDegrees, ctx.Faction, Buffer, GameConstants.Layers.CharacterMask);

            int limit = MaxTargets > 0 ? Mathf.Min(MaxTargets, Buffer.Count) : Buffer.Count;
            for (int i = 0; i < limit; i++)
            {
                var target = Buffer[i].Transform;
                Vector3 hitPoint = target.position + Vector3.up;
                Vector3 hitDirection = (target.position - ctx.CasterTransform.position).normalized;
                var spec = CombatSpecs.MakeDamageSpec(ctx.SourceASC, ctx.MakeEffectContext(hitPoint, hitDirection),
                    FlatDamage, AttackPowerScale, AbilityPowerScale, DamageType, Knockback, 0f, ctx.Level);
                CombatSpecs.ApplyToTarget(ctx.SourceASC, spec, target.gameObject);
                ctx.ApplyEffectToTarget(ApplyStatus, target.gameObject, hitPoint, hitDirection);
            }
        }
    }
}
