using System.Collections;
using System.Collections.Generic;
using ActionRPG.Abilities;
using ActionRPG.Combat;
using ActionRPG.Core;
using ActionRPG.Core.AbilitySystem;
using UnityEngine;

namespace ActionRPG.Skills.Effects
{
    /// <summary>발사체를 쏜다. 여러 발을 부채꼴로 퍼뜨릴 수 있다. 피해 스펙은 발사 순간 스냅샷된다.</summary>
    [CreateAssetMenu(menuName = "ActionRPG/Skills/Effects/Projectile", fileName = "EF_Projectile")]
    public class ProjectileEffect : SkillEffect
    {
        [Header("발사체")]
        public GameObject ProjectilePrefab;
        [Min(1)] public int Count = 1;
        [Tooltip("여러 발일 때 좌우로 벌어지는 전체 각도.")]
        public float SpreadDegrees = 20f;
        public float Speed = 32f;
        [Tooltip("발 사이의 간격(초). 0 이면 동시 발사.")]
        [Min(0f)] public float IntervalSeconds;

        [Header("피해")]
        public float FlatDamage = 8f;
        public float AttackPowerScale = 1f;
        public float AbilityPowerScale = 0f;
        public DamageType DamageType = DamageType.Physical;

        [Header("추가 효과")]
        public GameplayEffect OnHitStatus;

        public override void Execute(in SkillCastContext ctx)
        {
            if (ProjectilePrefab == null || ctx.SourceASC == null) return;
            if (IntervalSeconds <= 0f)
            {
                for (int i = 0; i < Count; i++) SpawnOne(ctx, i);
                return;
            }
            // 연사는 ASC(MonoBehaviour)의 코루틴으로 돌린다.
            ctx.SourceASC.StartCoroutine(Volley(ctx));
        }

        private IEnumerator Volley(SkillCastContext ctx)
        {
            for (int i = 0; i < Count; i++)
            {
                if (ctx.SourceASC == null || !ctx.SourceASC.IsSpawned) yield break;
                SpawnOne(ctx, i);
                yield return new WaitForSeconds(IntervalSeconds);
            }
        }

        public void SpawnOne(in SkillCastContext ctx, int index)
        {
            Vector3 direction = ctx.Direction;
            if (Count > 1 && SpreadDegrees > 0f)
            {
                float step = SpreadDegrees / (Count - 1);
                float angle = -SpreadDegrees * 0.5f + step * index;
                direction = Quaternion.AngleAxis(angle, Vector3.up) * ctx.Direction;
            }

            var damageSpec = CombatSpecs.MakeDamageSpec(ctx.SourceASC, ctx.MakeEffectContext(ctx.Origin, direction),
                FlatDamage, AttackPowerScale, AbilityPowerScale, DamageType, 0f, 0f, ctx.Level);
            var statusSpec = OnHitStatus != null
                ? ctx.SourceASC.MakeOutgoingSpec(OnHitStatus, Mathf.Max(1f, ctx.Level), ctx.MakeEffectContext(ctx.Origin, direction))
                : null;
            Projectile.SpawnConfigured(ProjectilePrefab, ctx.Origin, direction, ctx.Faction, damageSpec, statusSpec, Speed);
        }
    }
}
