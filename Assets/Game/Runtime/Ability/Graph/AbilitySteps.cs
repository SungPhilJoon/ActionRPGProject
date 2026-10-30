using System;
using Framework;
using UnityEngine;

/// <summary>GAS Tool 의 "단계 추가" 메뉴 분류와 설명.</summary>
[AttributeUsage(AttributeTargets.Class, Inherited = false)]
public sealed class AbilityStepInfoAttribute : Attribute
{
    public readonly string Category;
    public readonly string Description;

    public AbilityStepInfoAttribute(string InCategory, string InDescription)
    {
        Category = InCategory;
        Description = InDescription;
    }
}

/// <summary>타임라인 표시용 단계 종류.</summary>
public enum EAbilityStepKind
{
    Other = 0,
    Wait = 1,
    Hit = 2,
    Effect = 3,
    Motion = 4,
}

/// <summary>
/// 어빌리티 그래프의 한 단계 (언리얼 AbilityTask / Blueprint 노드 대응).
/// 에셋은 여러 액터가 공유하므로 단계 객체에는 실행 상태를 두지 않는다 (상태는 GraphAbility 가 가진다).
/// </summary>
[Serializable]
public abstract class AbilityStep
{
    /// <summary>실행. 반환값 = 다음 단계까지 기다릴 시간(초). 0 이면 바로 다음 단계, 음수면 그래프 중단(어빌리티는 단계가 직접 종료).</summary>
    public abstract float Execute(GraphAbility InAbility);

    /// <summary>어빌리티가 끝날 때 (취소 포함) — 정리용.</summary>
    public virtual void OnAbilityEnd(GraphAbility InAbility, bool InWasCancelled)
    {
    }

    /// <summary>에디터 타임라인용 대기 시간 (공격 속도 배율 1 기준).</summary>
    public virtual float PlannedWait()
    {
        return 0.0f;
    }

    public virtual EAbilityStepKind Kind
    {
        get { return EAbilityStepKind.Other; }
    }

    public abstract string Describe();
}

// ====================================================================== 흐름

[Serializable]
[AbilityStepInfo("흐름", "다음 단계까지 기다린다 (UAbilityTask_WaitDelay). 공격 속도 적용 시 그만큼 짧아진다.")]
public class WaitStep : AbilityStep
{
    public float Seconds = 0.3f;
    public bool ScaleWithAttackSpeed = true;

    public override float Execute(GraphAbility InAbility)
    {
        return ScaleWithAttackSpeed == true ? Seconds / InAbility.AttackSpeedScale : Seconds;
    }

    public override float PlannedWait()
    {
        return Seconds;
    }

    public override EAbilityStepKind Kind
    {
        get { return EAbilityStepKind.Wait; }
    }

    public override string Describe()
    {
        return $"{Seconds:0.##}초 대기";
    }
}

[Serializable]
[AbilityStepInfo("흐름", "여기서 어빌리티를 끝낸다 (EndAbility). 없으면 마지막 단계 뒤 자동 종료.")]
public class EndAbilityStep : AbilityStep
{
    public override float Execute(GraphAbility InAbility)
    {
        InAbility.EndAbility(false);
        return -1.0f;
    }

    public override string Describe()
    {
        return "어빌리티 종료";
    }
}

// ====================================================================== 애니메이션 / 이동

[Serializable]
[AbilityStepInfo("애니메이션", "AnimationTable 행을 재생한다 (PlayMontage). 애니메이션은 몬스터(StateTree Tool)에서 클립을 연결한다.")]
public class PlayAnimationStep : AbilityStep
{
    [Tooltip("AnimationTable Index")]
    public int AnimationId = 1;

    public override float Execute(GraphAbility InAbility)
    {
        InAbility.PlayAnimation(AnimationId);
        return 0.0f;
    }

    public override EAbilityStepKind Kind
    {
        get { return EAbilityStepKind.Motion; }
    }

    public override string Describe()
    {
        return $"애니메이션 #{AnimationId}";
    }
}

[Serializable]
[AbilityStepInfo("이동", "조준 방향(몬스터는 타깃 방향)으로 즉시 회전한다.")]
public class FaceAimStep : AbilityStep
{
    public override float Execute(GraphAbility InAbility)
    {
        InAbility.Actor.Motor.Face(InAbility.Aim, true);
        return 0.0f;
    }

    public override string Describe()
    {
        return "조준 방향으로 회전";
    }
}

[Serializable]
[AbilityStepInfo("이동", "지정 시간 동안 이동을 막는다 (시전 경직).")]
public class LockMovementStep : AbilityStep
{
    public float Seconds = 0.5f;

    public override float Execute(GraphAbility InAbility)
    {
        InAbility.Actor.Motor.LockMovement(Seconds / InAbility.AttackSpeedScale);
        return 0.0f;
    }

    public override string Describe()
    {
        return $"이동 잠금 {Seconds:0.##}초";
    }
}

[Serializable]
[AbilityStepInfo("이동", "조준 방향으로 돌진/도약한다. 조준 지점까지 체크 시 타깃 위치(최대 Distance)로 이동.")]
public class DashStep : AbilityStep
{
    public float Distance = 6.0f;
    public float Duration = 0.35f;
    [Tooltip("켜면 조준 지점까지만 이동 (도약). 끄면 항상 Distance.")]
    public bool ToAimPoint;
    [Tooltip("켜면 도착할 때까지 다음 단계를 기다린다.")]
    public bool WaitUntilArrive = true;

    public override float Execute(GraphAbility InAbility)
    {
        var actor = InAbility.Actor;
        float distance = Distance;
        if (ToAimPoint == true)
        {
            distance = Mathf.Min(Distance, MathUtil.FlatDistance(actor.transform.position, InAbility.AimPoint));
        }

        actor.Motor.Dash(InAbility.Aim, distance, Duration);
        return WaitUntilArrive == true ? Duration : 0.0f;
    }

    public override void OnAbilityEnd(GraphAbility InAbility, bool InWasCancelled)
    {
        if (InWasCancelled == true)
        {
            InAbility.Actor.Motor.CancelDash();
        }
    }

    public override float PlannedWait()
    {
        return WaitUntilArrive == true ? Duration : 0.0f;
    }

    public override EAbilityStepKind Kind
    {
        get { return EAbilityStepKind.Motion; }
    }

    public override string Describe()
    {
        return $"{(ToAimPoint == true ? "도약" : "돌진")} {Distance:0.#}m / {Duration:0.##}초";
    }
}

// ====================================================================== 공격

/// <summary>피해 판정 단계 공통 값 (CombatUtil 이 쓰는 AbilityRow 형태로 바꿔 넘긴다).</summary>
[Serializable]
public abstract class HitStep : AbilityStep
{
    [Tooltip("피해 계수 (% × 공격력). 0 이면 피해 없이 HitEffects 만.")]
    public float Damage = 100.0f;
    public EDamageType DamageType = EDamageType.Physical;
    [Tooltip("맞은 대상에게 거는 GameplayEffect (EffectTable Index).")]
    public int[] HitEffects = new int[0];
    [Tooltip("판정 순간 연출 (CueTable Index).")]
    public int CastCue;
    [Tooltip("맞은 위치 연출 (CueTable Index).")]
    public int HitCue = 2;

    public override EAbilityStepKind Kind
    {
        get { return EAbilityStepKind.Hit; }
    }

    /// <summary>CombatUtil / ProjectileSystem 이 읽는 형태. AbilityId 는 이 어빌리티 (전투 로그·쿨다운 집계용).</summary>
    protected AbilityRow makeProfile(GraphAbility InAbility)
    {
        var row = new AbilityRow();
        row.Index = InAbility.AbilityId;
        row.NameId = InAbility.Row.NameId;
        row.Type = EAbilityType.Graph;
        row.Slot = InAbility.Row.Slot;
        row.Damage = Damage;
        row.DamageType = DamageType;
        row.HitEffects = HitEffects;
        row.SelfEffects = new int[0];
        row.StartEffects = new int[0];
        row.ConsumeEffects = new int[0];
        row.ConsumeTag = ETag.None;
        row.HitCue = HitCue;
        return row;
    }

    protected string damageText()
    {
        return Damage > 0.0f ? $"피해 {Damage:0}% {DamageType}" : "피해 없음";
    }
}

[Serializable]
[AbilityStepInfo("공격", "앞쪽 부채꼴 안의 적에게 피해 + HitEffects (근접 공격).")]
public class MeleeHitStep : HitStep
{
    public float Range = 2.5f;
    public float Angle = 120.0f;

    public override float Execute(GraphAbility InAbility)
    {
        var actor = InAbility.Actor;
        var center = actor.transform.position;
        if (CastCue != 0)
        {
            CueSpawner.Spawn(CastCue, center + InAbility.Aim * (Range * 0.5f) + Vector3.up, InAbility.Aim);
        }

        CombatUtil.HitArc(actor, makeProfile(InAbility), center, actor.transform.forward, Range, Angle, 1.0f);
        return 0.0f;
    }

    public override string Describe()
    {
        return $"부채꼴 {Range:0.#}m {Angle:0}° — {damageText()}";
    }
}

[Serializable]
[AbilityStepInfo("공격", "원형 범위 안의 적에게 피해 + HitEffects. 중심 = 자신 또는 조준 지점.")]
public class AreaHitStep : HitStep
{
    public float Radius = 3.0f;
    [Tooltip("켜면 조준 지점(최대 MaxRange)이 중심, 끄면 자신이 중심.")]
    public bool AtAimPoint;
    public float MaxRange = 10.0f;

    public override float Execute(GraphAbility InAbility)
    {
        var actor = InAbility.Actor;
        var center = actor.transform.position;
        if (AtAimPoint == true)
        {
            var offset = Vector3.ClampMagnitude(MathUtil.Flat(InAbility.AimPoint - center), MaxRange);
            center = CombatUtil.SnapToGround(center + offset);
        }

        if (CastCue != 0)
        {
            CueSpawner.Spawn(CastCue, center + Vector3.up * 0.1f, InAbility.Aim);
        }

        CombatUtil.HitCircle(actor, makeProfile(InAbility), center, Radius, 1.0f);
        return 0.0f;
    }

    public override string Describe()
    {
        return $"원형 {Radius:0.#}m ({(AtAimPoint == true ? "조준 지점" : "자신")}) — {damageText()}";
    }
}

[Serializable]
[AbilityStepInfo("공격", "발사체를 쏜다. 명중 시 피해 + HitEffects.")]
public class ProjectileStep : HitStep
{
    [Tooltip("발사체 프리팹 (AssetTable Index, 예: 122 Arrow).")]
    public int Projectile = 122;
    public int Count = 1;
    [Tooltip("Count > 1 일 때 부채꼴 전체 각도.")]
    public float Spread;
    public float Speed = 18.0f;
    public float Range = 14.0f;
    [Tooltip("발사체 판정 반지름.")]
    public float Radius = 0.4f;

    public override float Execute(GraphAbility InAbility)
    {
        var actor = InAbility.Actor;
        var row = makeProfile(InAbility);
        row.Projectile = Projectile;
        row.Speed = Speed;
        row.Range = Range;
        row.Radius = Radius;

        int count = Mathf.Max(1, Count);
        Vector3 forward = InAbility.Aim;
        Vector3 origin = actor.AimOrigin + forward * 0.6f;
        if (CastCue != 0)
        {
            CueSpawner.Spawn(CastCue, origin, forward);
        }

        for (int i = 0; i < count; ++i)
        {
            float t = count == 1 ? 0.5f : (float)i / (count - 1);
            float angle = Mathf.Lerp(-Spread * 0.5f, Spread * 0.5f, t);
            ProjectileSystem.Fire(actor, row, origin, Quaternion.Euler(0.0f, angle, 0.0f) * forward);
        }

        return 0.0f;
    }

    public override string Describe()
    {
        return $"발사체 #{Projectile} ×{Mathf.Max(1, Count)} ({Range:0}m, {Speed:0}m/s) — {damageText()}";
    }
}

// ====================================================================== 방어 / 효과

[Serializable]
[AbilityStepInfo("방어 · 효과", "자신에게 GameplayEffect 를 건다 (방어 버프, 보호막, 무적 등). 어빌리티 종료 시 제거 옵션.")]
public class ApplyEffectToSelfStep : AbilityStep
{
    [Tooltip("EffectTable Index")]
    public int EffectId;
    [Tooltip("켜면 어빌리티가 끝날 때(취소 포함) 이 효과를 제거한다 — 가드처럼 '시전 중에만' 유지되는 방어에 사용.")]
    public bool RemoveOnEnd = true;

    public override float Execute(GraphAbility InAbility)
    {
        var spec = EffectLibrary.MakeSpec(EffectId, InAbility.Actor.Asc, InAbility.AbilityId);
        if (spec == null)
        {
            return 0.0f;
        }

        int handle = InAbility.Actor.Asc.ApplyGameplayEffectSpecToSelf(spec);
        if (RemoveOnEnd == true)
        {
            InAbility.TrackEffect(handle);
        }

        return 0.0f;
    }

    public override EAbilityStepKind Kind
    {
        get { return EAbilityStepKind.Effect; }
    }

    public override string Describe()
    {
        return $"자신에게 GE #{EffectId}{(RemoveOnEnd == true ? " (종료 시 제거)" : string.Empty)}";
    }
}

[Serializable]
[AbilityStepInfo("방어 · 효과", "발동 대상(몬스터는 현재 타깃)에게 GameplayEffect 를 건다 (디버프, 표식 등).")]
public class ApplyEffectToTargetStep : AbilityStep
{
    [Tooltip("EffectTable Index")]
    public int EffectId;
    [Tooltip("대상과의 최대 거리. 더 멀면 걸지 않는다.")]
    public float MaxRange = 15.0f;

    public override float Execute(GraphAbility InAbility)
    {
        var target = InAbility.TargetActor;
        if (target == null || target.IsDead == true || MathUtil.FlatDistance(InAbility.Actor.transform.position, target.transform.position) > MaxRange)
        {
            return 0.0f;
        }

        target.ApplyEffect(EffectId, InAbility.Actor, InAbility.AbilityId);
        return 0.0f;
    }

    public override EAbilityStepKind Kind
    {
        get { return EAbilityStepKind.Effect; }
    }

    public override string Describe()
    {
        return $"대상에게 GE #{EffectId} ({MaxRange:0}m 이내)";
    }
}

[Serializable]
[AbilityStepInfo("방어 · 효과", "자신의 체력을 최대 체력의 % 만큼 회복한다.")]
public class HealSelfStep : AbilityStep
{
    public float PercentOfMaxHealth = 20.0f;

    public override float Execute(GraphAbility InAbility)
    {
        var actor = InAbility.Actor;
        actor.HealInstant(actor.Attributes.Get(EAttribute.MaxHealth) * PercentOfMaxHealth * 0.01f);
        return 0.0f;
    }

    public override EAbilityStepKind Kind
    {
        get { return EAbilityStepKind.Effect; }
    }

    public override string Describe()
    {
        return $"체력 {PercentOfMaxHealth:0}% 회복";
    }
}

// ====================================================================== 연출

[Serializable]
[AbilityStepInfo("연출", "GameplayCue(VFX)를 재생한다. 위치 = 자신 또는 조준 지점.")]
public class PlayCueStep : AbilityStep
{
    [Tooltip("CueTable Index")]
    public int CueId;
    public bool AtAimPoint;

    public override float Execute(GraphAbility InAbility)
    {
        var position = AtAimPoint == true ? InAbility.AimPoint : InAbility.Actor.transform.position + Vector3.up;
        CueSpawner.Spawn(CueId, position, InAbility.Aim);
        return 0.0f;
    }

    public override string Describe()
    {
        return $"Cue #{CueId} ({(AtAimPoint == true ? "조준 지점" : "자신")})";
    }
}
