using System.Collections.Generic;
using Framework;
using Framework.Ability;
using UnityEngine;

/// <summary>
/// 어빌리티/발사체가 공유하는 히트 처리 유틸.
/// "범위 판정 → 피해 스펙 + 부가 이펙트 적용 → 히트 큐" 를 한 곳에서 처리한다.
/// </summary>
public static class CombatUtil
{
    private static readonly List<Actor> Buffer = new List<Actor>(32);

    /// <summary>InRow 의 피해(Damage 계수) 와 HitEffects 를 대상에 적용한다.</summary>
    public static void ApplyAbilityHit(Actor InSource, AbilityRow InRow, Actor InTarget, float InDamageScale, Vector3 InHitPoint)
    {
        if (InSource == null || InTarget == null || InTarget.IsDead == true)
        {
            return;
        }

        Vector3 direction = MathUtil.FlatDirection(InSource.transform.position, InTarget.transform.position, InSource.transform.forward);
        if (InRow.Damage > 0.0f)
        {
            var spec = EffectLibrary.MakeDamageSpec(InSource.Asc, InRow, InDamageScale);
            spec.HitPoint = InHitPoint;
            spec.HitDirection = direction;
            InSource.Asc.ApplyGameplayEffectSpecToTarget(spec, InTarget.Asc);
        }

        // V Rising "Consume": 대상이 ConsumeTag 상태면 그 상태를 지우고 HitEffects 대신 ConsumeEffects (예: Chill → Freeze, Static → 기절).
        var effects = InRow.HitEffects;
        if (tryConsume(InRow, InTarget) == true)
        {
            effects = InRow.ConsumeEffects;
        }

        applyEffects(InSource, InRow.Index, InTarget, effects, InHitPoint, direction);

        if (InRow.HitCue != 0)
        {
            CueSpawner.Spawn(InRow.HitCue, InHitPoint, direction);
        }

        var payload = new GameplayCueParams();
        payload.Source = InSource.Asc;
        payload.Target = InTarget.Asc;
        payload.Location = InHitPoint;
        InSource.Asc.SendGameplayEvent((int)ETag.Event_Hit, payload);
    }

    private static bool tryConsume(AbilityRow InRow, Actor InTarget)
    {
        if (InRow.ConsumeTag == ETag.None || InRow.ConsumeEffects == null || InRow.ConsumeEffects.Length == 0)
        {
            return false;
        }

        if (InTarget.Asc.Tags.HasTag((int)InRow.ConsumeTag) == false || hasAgedEffect(InTarget.Asc, InRow.ConsumeTag) == false)
        {
            return false;
        }

        InTarget.Asc.RemoveEffectsWithGrantedTags(TagUtil.ToContainer(InRow.ConsumeTag));
        return true;
    }

    /// <summary>
    /// 소모할 상태가 ConsumeMinAgeSeconds 이상 지났는가.
    /// 같은 시전의 여러 발(Chaos Volley 2발 등)이 방금 건 상태를 바로 소모하지 않게 한다 — "이전에 걸어 둔 상태" 만 터뜨린다.
    /// </summary>
    private static bool hasAgedEffect(AbilitySystemComponent InAsc, ETag InTag)
    {
        float minAge = Tables.Const(EConst.ConsumeMinAgeSeconds);
        var effects = InAsc.Effects;
        for (int i = 0; i < effects.Count; ++i)
        {
            var effect = effects[i];
            if (effect.Spec.Def.GrantedTags.HasTag((int)InTag) == false)
            {
                continue;
            }

            if (effect.Spec.Duration - effect.Remaining >= minAge)
            {
                return true;
            }
        }

        return false;
    }

    private static void applyEffects(Actor InSource, int InAbilityId, Actor InTarget, int[] InEffects, Vector3 InHitPoint, Vector3 InDirection)
    {
        if (InEffects == null)
        {
            return;
        }

        for (int i = 0; i < InEffects.Length; ++i)
        {
            if (InTarget.IsDead == true)
            {
                break;
            }

            var spec = EffectLibrary.MakeSpec(InEffects[i], InSource.Asc, InAbilityId);
            if (spec == null)
            {
                continue;
            }

            spec.HitPoint = InHitPoint;
            spec.HitDirection = InDirection;
            InSource.Asc.ApplyGameplayEffectSpecToTarget(spec, InTarget.Asc);
        }
    }

    /// <summary>지정형 공격용: Immaterial(대상 지정 불가) 액터를 목록에서 뺀다. 범위(원형) 공격은 쓰지 않는다.</summary>
    public static void RemoveUntargetable(List<Actor> InOutActors)
    {
        for (int i = InOutActors.Count - 1; i >= 0; --i)
        {
            if (InOutActors[i].IsTargetable == false)
            {
                InOutActors.RemoveAt(i);
            }
        }
    }

    /// <summary>부채꼴 판정. 맞은 수를 돌려준다.</summary>
    public static int HitArc(Actor InSource, AbilityRow InRow, Vector3 InCenter, Vector3 InForward, float InRange, float InAngle, float InDamageScale)
    {
        var registry = Managers.Get<ActorRegistry>();
        registry.FindHostiles(InSource, InCenter, InRange, Buffer);
        RemoveUntargetable(Buffer);
        int hits = 0;
        for (int i = 0; i < Buffer.Count; ++i)
        {
            var target = Buffer[i];
            Vector3 toTarget = target.transform.position - InCenter;
            bool closeEnough = MathUtil.Flat(toTarget).magnitude <= target.Radius + 0.3f;
            if (closeEnough == false && MathUtil.IsInArc(InForward, toTarget, InAngle) == false)
            {
                continue;
            }

            ApplyAbilityHit(InSource, InRow, target, InDamageScale, target.AimOrigin);
            hits += 1;
        }

        return hits;
    }

    public static int HitCircle(Actor InSource, AbilityRow InRow, Vector3 InCenter, float InRadius, float InDamageScale)
    {
        var registry = Managers.Get<ActorRegistry>();
        registry.FindHostiles(InSource, InCenter, InRadius, Buffer);
        int hits = Buffer.Count;
        for (int i = 0; i < Buffer.Count; ++i)
        {
            ApplyAbilityHit(InSource, InRow, Buffer[i], InDamageScale, Buffer[i].AimOrigin);
        }

        return hits;
    }

    /// <summary>InFrom 에서 InTo 로 지면을 따라 이동 가능한 지점 (지형 높이 보정).</summary>
    public static Vector3 SnapToGround(Vector3 InPoint)
    {
        RaycastHit hit;
        if (Physics.Raycast(InPoint + Vector3.up * 30.0f, Vector3.down, out hit, 80.0f, 1 << LayerId.Ground, QueryTriggerInteraction.Ignore) == true)
        {
            return hit.point;
        }

        return InPoint;
    }
}
