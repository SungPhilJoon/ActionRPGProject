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
    /// <summary>순수 연출용. 모든 피어에서 로컬 VFX 를 재생한다 (GameplayCue.Skill.Effect 경유).</summary>
    [CreateAssetMenu(menuName = "ActionRPG/Skills/Effects/Play VFX", fileName = "EF_Vfx")]
    public class PlayVfxEffect : SkillEffect
    {
        public enum Anchor : byte { CasterPosition, TargetPoint, ForwardOffset }

        public GameObject VfxPrefab;
        public Anchor SpawnAt = Anchor.CasterPosition;
        public float ForwardDistance = 2f;
        [Tooltip("시전자에게 붙일지. 켜면 캐릭터를 따라다닌다.")]
        public bool AttachToCaster;
        [Min(0.1f)] public float DestroyAfter = 4f;

        public override void ExecuteVisual(in SkillCastContext ctx)
        {
            if (VfxPrefab == null) return;
            Vector3 casterPosition = ctx.CasterTransform != null ? ctx.CasterTransform.position : ctx.Origin;
            Vector3 position = SpawnAt switch
            {
                Anchor.ForwardOffset => casterPosition + ctx.Direction * ForwardDistance,
                Anchor.TargetPoint => ctx.TargetPoint,
                _ => casterPosition
            };
            var rotation = ctx.Direction.sqrMagnitude > 0.0001f
                ? Quaternion.LookRotation(ctx.Direction, Vector3.up)
                : Quaternion.identity;
            var vfx = Instantiate(VfxPrefab, position, rotation, AttachToCaster ? ctx.CasterTransform : null);
            Destroy(vfx, DestroyAfter);
        }
    }
}
