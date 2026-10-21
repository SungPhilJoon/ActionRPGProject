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
    /// <summary>게임플레이 이펙트(버프/디버프)를 건다. 대상 진영을 골라 쓸 수 있다.</summary>
    [CreateAssetMenu(menuName = "ActionRPG/Skills/Effects/Apply Status", fileName = "EF_ApplyStatus")]
    public class ApplyStatusEffect : SkillEffect
    {
        public enum TargetGroup : byte { Self, AlliesInRadius, HostilesInRadius, HostilesInArc }

        public GameplayEffect Status;
        public TargetGroup Targets = TargetGroup.Self;
        [Min(0f)] public float Radius = 5f;
        [Range(10f, 360f)] public float ArcDegrees = 120f;
        [Tooltip("0 이하면 무제한.")]
        public int MaxTargets = 0;

        private static readonly List<IDamageable> Buffer = new List<IDamageable>(16);

        public override void Execute(in SkillCastContext ctx)
        {
            if (Status == null) return;
            if (Targets == TargetGroup.Self)
            {
                ctx.ApplyEffectToTarget(Status, ctx.CasterObject, ctx.CasterTransform.position, Vector3.up);
                return;
            }

            switch (Targets)
            {
                case TargetGroup.AlliesInRadius:
                    CombatQuery.OverlapAllies(ctx.CasterTransform.position, Radius, ctx.Faction, Buffer, true, GameConstants.Layers.CharacterMask);
                    break;
                case TargetGroup.HostilesInRadius:
                    CombatQuery.OverlapHostiles(ctx.CasterTransform.position, Radius, ctx.Faction, Buffer, GameConstants.Layers.CharacterMask);
                    break;
                case TargetGroup.HostilesInArc:
                    CombatQuery.OverlapHostileArc(ctx.Origin, ctx.Direction, Radius, ArcDegrees, ctx.Faction, Buffer, GameConstants.Layers.CharacterMask);
                    break;
            }

            int limit = MaxTargets > 0 ? Mathf.Min(MaxTargets, Buffer.Count) : Buffer.Count;
            for (int i = 0; i < limit; i++)
            {
                var target = Buffer[i].Transform;
                ctx.ApplyEffectToTarget(Status, target.gameObject, target.position, (target.position - ctx.CasterTransform.position).normalized);
            }
        }
    }
}
