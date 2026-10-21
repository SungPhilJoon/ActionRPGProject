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
    /// <summary>
    /// 조준 방향으로 돌진한다.
    /// 이동 권한은 조종하는 피어에 있으므로 이동은 ExecuteLocal(소유 클라이언트가 예측 실행)에서,
    /// 무적은 서버가 GE_Invulnerable(State.Invulnerable 태그)로 건다.
    /// </summary>
    [CreateAssetMenu(menuName = "ActionRPG/Skills/Effects/Dash", fileName = "EF_Dash")]
    public class DashEffect : SkillEffect
    {
        [Min(0.1f)] public float Distance = 6f;
        [Min(0.05f)] public float Duration = 0.25f;
        [Tooltip("돌진 중 피해를 받지 않게 할지.")]
        public bool InvulnerableDuringDash = true;

        public override void Execute(in SkillCastContext ctx)
        {
            if (!InvulnerableDuringDash || ctx.SourceASC == null) return;
            var effect = ActionRPGGameData.Get()?.InvulnerableGameplayEffect;
            if (effect == null) return;
            var spec = ctx.SourceASC.MakeOutgoingSpec(effect, 1f, ctx.SourceASC.MakeEffectContext());
            spec.SetSetByCallerMagnitude(GameTags.SetByCaller_Duration, Duration);
            ctx.SourceASC.ApplyGameplayEffectSpecToSelf(spec);
        }

        public override void ExecuteLocal(in SkillCastContext ctx)
        {
            if (ctx.CasterObject == null) return;
            var motor = ctx.CasterObject.GetComponent<CharacterMotor>();
            if (motor != null) motor.ForceDash(ctx.Direction, Distance, Duration);
        }
    }
}
