using System.Collections.Generic;

/// <summary>
/// GAS Tool "새 Ability" 템플릿. 각 템플릿은 언리얼에서 자주 쓰는 어빌리티 구성(태그 규칙 + 단계)을 미리 채운다.
/// 숫자는 기존 몬스터 어빌리티(501~514)와 비슷한 수준이며, 만든 뒤 툴에서 고친다.
/// </summary>
public static class AbilityTemplates
{
    public enum EKind
    {
        Melee = 0,
        Ranged = 1,
        Area = 2,
        Charge = 3,
        Guard = 4,
        SelfBuff = 5,
        Empty = 6,
    }

    public static readonly string[] Labels =
    {
        "근접 공격 (부채꼴)", "원거리 공격 (발사체)", "범위 공격 (원형)", "돌진 공격", "방어 (가드)", "자기 강화 (버프)", "빈 어빌리티",
    };

    public class Template
    {
        public EAbilitySlot Slot;
        public float Cooldown;
        /// <summary>AbilityTable.Range — AI 가 이 거리 안에서 사용.</summary>
        public float AiRange;
        public ETag[] ActivationOwnedTags = new ETag[0];
        public ETag[] BlockAbilitiesWithTag = new ETag[0];
        public ETag[] CancelAbilitiesWithTag = new ETag[0];
        public List<AbilityStep> Steps = new List<AbilityStep>();
        public string Description;
        /// <summary>방어 템플릿: 가드 GE 를 새로 만들어 ApplyEffectToSelfStep 에 넣는다.</summary>
        public bool NeedsGuardEffect;
    }

    /// <summary>공격 중에는 다른 공격 어빌리티가 겹쳐 나가지 않게 (기존 GameAbility 기본 규칙과 같음).</summary>
    private static readonly ETag[] AttackBlock = { ETag.Ability_Primary, ETag.Ability_Weapon, ETag.Ability_Spell, ETag.Ability_Ultimate, ETag.Ability_Monster };

    public static Template Create(EKind InKind)
    {
        var template = new Template();
        switch (InKind)
        {
            case EKind.Melee:
                template.Slot = EAbilitySlot.Primary;
                template.Cooldown = 1.5f;
                template.AiRange = 2.6f;
                template.ActivationOwnedTags = new[] { ETag.State_Attacking };
                template.BlockAbilitiesWithTag = AttackBlock;
                template.Description = "회전 → 이동 잠금 → 공격 모션 → 0.45초 뒤 부채꼴 타격 → 동작 마무리";
                template.Steps.Add(new FaceAimStep());
                template.Steps.Add(new LockMovementStep { Seconds = 0.9f });
                template.Steps.Add(new PlayAnimationStep { AnimationId = 1 });
                template.Steps.Add(new WaitStep { Seconds = 0.45f });
                template.Steps.Add(new MeleeHitStep { Range = 2.6f, Angle = 120.0f, Damage = 110.0f, CastCue = 1, HitCue = 2 });
                template.Steps.Add(new WaitStep { Seconds = 0.45f });
                break;
            case EKind.Ranged:
                template.Slot = EAbilitySlot.WeaponE;
                template.Cooldown = 3.0f;
                template.AiRange = 14.0f;
                template.ActivationOwnedTags = new[] { ETag.State_Attacking };
                template.BlockAbilitiesWithTag = AttackBlock;
                template.Description = "회전 → 시전 모션 → 0.5초 뒤 발사 → 경직";
                template.Steps.Add(new FaceAimStep());
                template.Steps.Add(new LockMovementStep { Seconds = 0.8f });
                template.Steps.Add(new PlayAnimationStep { AnimationId = 3 });
                template.Steps.Add(new WaitStep { Seconds = 0.5f });
                template.Steps.Add(new ProjectileStep { Projectile = 122, Count = 1, Speed = 20.0f, Range = 14.0f, Radius = 0.4f, Damage = 90.0f, HitCue = 2 });
                template.Steps.Add(new WaitStep { Seconds = 0.3f });
                break;
            case EKind.Area:
                template.Slot = EAbilitySlot.WeaponQ;
                template.Cooldown = 8.0f;
                template.AiRange = 3.5f;
                template.ActivationOwnedTags = new[] { ETag.State_Attacking };
                template.BlockAbilitiesWithTag = AttackBlock;
                template.Description = "내려찍기 모션 → 0.6초 뒤 자신 주변 원형 타격(둔화) → 경직";
                template.Steps.Add(new FaceAimStep());
                template.Steps.Add(new LockMovementStep { Seconds = 1.0f });
                template.Steps.Add(new PlayAnimationStep { AnimationId = 7 });
                template.Steps.Add(new WaitStep { Seconds = 0.6f });
                template.Steps.Add(new AreaHitStep { Radius = 3.5f, Damage = 130.0f, HitEffects = new[] { 12 }, CastCue = 7, HitCue = 2 });
                template.Steps.Add(new WaitStep { Seconds = 0.4f });
                break;
            case EKind.Charge:
                template.Slot = EAbilitySlot.WeaponQ;
                template.Cooldown = 9.0f;
                template.AiRange = 8.0f;
                template.ActivationOwnedTags = new[] { ETag.State_Attacking, ETag.State_Dashing };
                template.BlockAbilitiesWithTag = AttackBlock;
                template.Description = "돌진 모션 → 8m 돌진 → 도착 지점 원형 타격(기절)";
                template.Steps.Add(new FaceAimStep());
                template.Steps.Add(new PlayAnimationStep { AnimationId = 8 });
                template.Steps.Add(new DashStep { Distance = 8.0f, Duration = 0.45f, ToAimPoint = true, WaitUntilArrive = true });
                template.Steps.Add(new AreaHitStep { Radius = 2.0f, Damage = 120.0f, HitEffects = new[] { 11 }, CastCue = 4, HitCue = 2 });
                template.Steps.Add(new LockMovementStep { Seconds = 0.4f });
                template.Steps.Add(new WaitStep { Seconds = 0.4f });
                break;
            case EKind.Guard:
                template.Slot = EAbilitySlot.Spell1;
                template.Cooldown = 10.0f;
                template.AiRange = 30.0f;
                template.ActivationOwnedTags = new[] { ETag.State_Casting };
                template.BlockAbilitiesWithTag = AttackBlock;
                template.Description = "가드 자세 2초: 저항 +60 (GE, 어빌리티가 끝나면 제거). 가드 중 다른 공격 어빌리티 차단";
                template.NeedsGuardEffect = true;
                template.Steps.Add(new LockMovementStep { Seconds = 2.0f });
                template.Steps.Add(new PlayAnimationStep { AnimationId = 3 });
                template.Steps.Add(new ApplyEffectToSelfStep { EffectId = 0, RemoveOnEnd = true });
                template.Steps.Add(new PlayCueStep { CueId = 5 });
                template.Steps.Add(new WaitStep { Seconds = 2.0f, ScaleWithAttackSpeed = false });
                break;
            case EKind.SelfBuff:
                template.Slot = EAbilitySlot.Spell2;
                template.Cooldown = 20.0f;
                template.AiRange = 30.0f;
                template.ActivationOwnedTags = new[] { ETag.State_Casting };
                template.Description = "포효 모션 → 0.5초 뒤 공격력 강화 GE (#23)";
                template.Steps.Add(new LockMovementStep { Seconds = 0.8f });
                template.Steps.Add(new PlayAnimationStep { AnimationId = 3 });
                template.Steps.Add(new WaitStep { Seconds = 0.5f });
                template.Steps.Add(new ApplyEffectToSelfStep { EffectId = 23, RemoveOnEnd = false });
                template.Steps.Add(new WaitStep { Seconds = 0.3f });
                break;
            default:
                template.Slot = EAbilitySlot.Primary;
                template.Cooldown = 1.0f;
                template.AiRange = 3.0f;
                template.Description = string.Empty;
                break;
        }

        return template;
    }
}
