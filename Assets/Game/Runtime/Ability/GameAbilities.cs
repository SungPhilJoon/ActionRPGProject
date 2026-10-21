using System.Collections.Generic;
using Framework;
using Framework.Ability;
using UnityEngine;

/// <summary>
/// 근접 콤보 (평타).
/// 컬럼: CastTime = 타격 시점, MoveLock = 동작 길이, Range/Angle = 부채꼴, Count = 콤보 단계 수, ComboWindow = 다음 단계 입력 유예.
/// 마지막 단계는 피해 ×1.5.
/// </summary>
public class MeleeComboAbility : GameAbility
{
    private const float FinisherScale = 1.5f;

    private int ComboIndex;
    private float LastEndTime = -100.0f;

    protected override void OnActivate(AbilityActivationInfo InInfo)
    {
        if (Commit() == false)
        {
            EndAbility(true);
            return;
        }

        // 콤보 간격은 "실제로 발동한 시점" 기준. 입력 버퍼에서 앞의 스킬을 먼저 처리하느라 ComboWindow 를 넘겨 발동하면
        // 콤보를 이어가지 않고 1타부터 다시 시작한다.
        int count = Mathf.Max(1, Row.Count);
        bool continueCombo = Time.time - LastEndTime <= Row.ComboWindow;
        ComboIndex = continueCombo == true ? (ComboIndex + 1) % count : 0;

        float speed = SpeedScale;
        FaceAim();
        LockMovement(Row.MoveLock / speed);
        PlayAnim(pickAnim());

        bool isFinisher = count > 1 && ComboIndex == count - 1;
        float scale = isFinisher == true ? FinisherScale : 1.0f;
        Delay(Row.CastTime / speed, () =>
        {
            var center = Actor.transform.position;
            PlayCastCue(center + AimDirection * (Row.Range * 0.5f) + Vector3.up);
            CombatUtil.HitArc(Actor, Row, center, Actor.transform.forward, Row.Range, Row.Angle, scale);
            var player = Actor as PlayerCharacter;
            if (player != null)
            {
                ResourceNode.HarvestArc(player, center, Actor.transform.forward, Row.Range, Row.Angle);
            }
        });

        EndAfter(Row.MoveLock / speed);
    }

    protected override void OnEnd(bool InWasCancelled)
    {
        LastEndTime = Time.time;
    }

    /// <summary>
    /// 콤보 단계별 공격 모션 (AbilityTable.Anims 의 ComboIndex 번째). 플레이어가 적 없이 채집 노드를 때릴 때는
    /// 노드의 채집 모션(ResourceNodeTable.Anim)을 쓴다.
    /// </summary>
    private int pickAnim()
    {
        var player = Actor as PlayerCharacter;
        if (player != null)
        {
            int harvest = ResourceNode.FindHarvestAnim(player, Actor.transform.position, AimDirection, Row.Range, Row.Angle);
            if (harvest != 0 && Managers.Get<ActorRegistry>().FindNearestHostile(player, Row.Range + 1.0f) == null)
            {
                return harvest;
            }
        }

        return Row.GetAnim(ComboIndex);
    }
}

/// <summary>
/// 발사체. 컬럼: CastTime = 발사 시점, Count = 발사 수, Spread = 부채꼴 각도, Speed/Range = 속도/사거리, Radius = 판정 반지름, Projectile = AssetTable 인덱스.
/// </summary>
public class ProjectileAbility : GameAbility
{
    protected override void OnActivate(AbilityActivationInfo InInfo)
    {
        if (Commit() == false)
        {
            EndAbility(true);
            return;
        }

        float speed = SpeedScale;
        FaceAim();
        LockMovement(Row.MoveLock / speed);
        PlayAnim(Row.GetAnim(0));

        Delay(Row.CastTime / speed, fire);
        EndAfter(Mathf.Max(Row.MoveLock, Row.CastTime) / speed);
    }

    private void fire()
    {
        int count = Mathf.Max(1, Row.Count);
        Vector3 forward = AimDirection;
        Vector3 origin = Actor.AimOrigin + forward * 0.6f;
        PlayCastCue(origin);

        for (int i = 0; i < count; ++i)
        {
            float t = count == 1 ? 0.5f : (float)i / (count - 1);
            float angle = Mathf.Lerp(-Row.Spread * 0.5f, Row.Spread * 0.5f, t);
            Vector3 direction = Quaternion.Euler(0.0f, angle, 0.0f) * forward;
            ProjectileSystem.Fire(Actor, Row, origin, direction);
        }
    }
}

/// <summary>
/// 대시 (Veil of Blood 등). 컬럼: Range = 거리, CastTime = 이동 시간, Radius = 경로 피해 반지름(Damage > 0 일 때),
/// SelfEffects = 대시 직후 부여 (강화/무적 등).
/// </summary>
public class DashAbility : GameAbility
{
    private readonly HashSet<Actor> AlreadyHit = new HashSet<Actor>();
    private readonly List<Actor> Buffer = new List<Actor>();

    protected override void OnActivate(AbilityActivationInfo InInfo)
    {
        if (Commit() == false)
        {
            EndAbility(true);
            return;
        }

        AlreadyHit.Clear();
        Vector3 direction = AimDirection;
        PlayCastCue(Actor.transform.position + Vector3.up * 0.5f);
        PlayAnim(Row.GetAnim(0));
        Actor.Motor.Dash(direction, Row.Range, Row.CastTime);

        if (Row.Damage > 0.0f)
        {
            AddTask(new WaitDurationTask(Row.CastTime, onDashTick, null));
        }

        Delay(Row.CastTime, ApplySelfEffects);
        EndAfter(Row.CastTime);
    }

    private void onDashTick(float InRatio, float InDeltaTime)
    {
        Managers.Get<ActorRegistry>().FindHostiles(Actor, Actor.transform.position, Row.Radius, Buffer);
        CombatUtil.RemoveUntargetable(Buffer);
        for (int i = 0; i < Buffer.Count; ++i)
        {
            if (AlreadyHit.Add(Buffer[i]) == true)
            {
                CombatUtil.ApplyAbilityHit(Actor, Row, Buffer[i], 1.0f, Buffer[i].AimOrigin);
            }
        }
    }

    protected override void OnEnd(bool InWasCancelled)
    {
        if (InWasCancelled == true)
        {
            Actor.Motor.CancelDash();
        }
    }
}

/// <summary>
/// 범위 폭발 / 지속 범위. 컬럼: Range > 0 이면 조준 지점(최대 Range), 0 이면 자신 중심.
/// CastTime = 첫 타격, Count = 타격 횟수, Spread = 타격 간격(초), Radius = 반지름, MoveLock = 시전 중 이동 잠금.
/// </summary>
public class AreaBurstAbility : GameAbility
{
    private Vector3 Center;
    private int PulsesLeft;

    protected override void OnActivate(AbilityActivationInfo InInfo)
    {
        if (Commit() == false)
        {
            EndAbility(true);
            return;
        }

        float speed = SpeedScale;
        FaceAim();
        PlayAnim(Row.GetAnim(0));
        LockMovement(Row.MoveLock / speed);

        if (Row.Range > 0.0f)
        {
            Vector3 offset = Vector3.ClampMagnitude(MathUtil.Flat(InInfo.AimPoint - Actor.transform.position), Row.Range);
            Center = CombatUtil.SnapToGround(Actor.transform.position + offset);
        }

        PulsesLeft = Mathf.Max(1, Row.Count);
        Delay(Row.CastTime / speed, pulse);
    }

    private void pulse()
    {
        Vector3 center = Row.Range > 0.0f ? Center : Actor.transform.position;
        PlayCastCue(center + Vector3.up * 0.1f);
        CombatUtil.HitCircle(Actor, Row, center, Row.Radius, 1.0f);

        PulsesLeft -= 1;
        if (PulsesLeft > 0)
        {
            Delay(Mathf.Max(0.1f, Row.Spread), pulse);
            return;
        }

        ApplySelfEffects();
        EndAfter(0.1f);
    }
}

/// <summary>자기 강화 (보호막/가속 등). CastTime 뒤 SelfEffects 부여.</summary>
public class SelfBuffAbility : GameAbility
{
    protected override void OnActivate(AbilityActivationInfo InInfo)
    {
        if (Commit() == false)
        {
            EndAbility(true);
            return;
        }

        PlayAnim(Row.GetAnim(0));
        LockMovement(Row.MoveLock);
        Delay(Row.CastTime, () =>
        {
            PlayCastCue(Actor.transform.position + Vector3.up);
            ApplySelfEffects();
        });
        EndAfter(Mathf.Max(Row.CastTime, Row.MoveLock) + 0.05f);
    }
}

/// <summary>
/// 도약 강타 (보스). 조준 지점으로 CastTime 동안 뛰어올라 착지 시 Radius 범위 피해.
/// </summary>
public class LeapAbility : GameAbility
{
    protected override void OnActivate(AbilityActivationInfo InInfo)
    {
        if (Commit() == false)
        {
            EndAbility(true);
            return;
        }

        FaceAim();
        PlayAnim(Row.GetAnim(0));
        float distance = Mathf.Min(Row.Range, MathUtil.FlatDistance(Actor.transform.position, InInfo.AimPoint));
        Actor.Motor.Dash(AimDirection, distance, Row.CastTime);
        Delay(Row.CastTime, () =>
        {
            PlayCastCue(Actor.transform.position + Vector3.up * 0.1f);
            CombatUtil.HitCircle(Actor, Row, Actor.transform.position, Row.Radius, 1.0f);
        });
        LockMovement(Row.CastTime + Row.MoveLock);
        EndAfter(Row.CastTime + Row.MoveLock);
    }
}

/// <summary>
/// 흡혈 (V Rising 의 Feed). 체력이 FeedHealthRatio 이하인 적에게 FeedDuration 동안 채널링 →
/// 대상 처치 + 혈액 획득. V Blood 보유자라면 어빌리티 해금.
/// </summary>
public class FeedAbility : GameAbility
{
    private Actor Victim;

    public override bool CanActivate(out EActivateFailReason OutReason)
    {
        if (base.CanActivate(out OutReason) == false)
        {
            return false;
        }

        Victim = findVictim();
        if (Victim == null)
        {
            OutReason = EActivateFailReason.Custom;
            return false;
        }

        return true;
    }

    protected override void OnActivate(AbilityActivationInfo InInfo)
    {
        if (Victim == null || Commit() == false)
        {
            EndAbility(true);
            return;
        }

        Actor.Asc.Tags.AddTag((int)ETag.State_Feeding);
        Actor.Motor.Face(Victim.transform.position - Actor.transform.position, true);
        Victim.ApplyEffect(EffectLibrary.FeedStunEffectId, Actor, Row.Index);
        PlayAnim(Row.GetAnim(0));
        PlayCastCue(Victim.AimOrigin);

        float duration = Tables.Const(EConst.FeedDuration);
        AddTask(new WaitDurationTask(duration, onChannel, complete));
    }

    private void onChannel(float InRatio, float InDeltaTime)
    {
        if (Victim == null || Victim.IsDead == true)
        {
            EndAbility(true);
        }
    }

    private void complete()
    {
        var victim = Victim;
        Victim = null;
        if (victim == null || victim.IsDead == true)
        {
            EndAbility(true);
            return;
        }

        var blood = Actor.GetComponent<BloodSystem>();
        if (blood != null)
        {
            blood.Consume(victim);
        }

        victim.Die(Actor);
        Actor.HealInstant(Actor.Attributes.Get(EAttribute.MaxHealth) * 0.2f);
        EndAbility(false);
    }

    protected override void OnEnd(bool InWasCancelled)
    {
        Actor.Asc.Tags.RemoveTag((int)ETag.State_Feeding);
        if (Victim != null && Victim.IsDead == false)
        {
            Victim.Asc.RemoveEffectsByDefId(EffectLibrary.FeedStunEffectId);
        }

        Victim = null;
    }

    private Actor findVictim()
    {
        float threshold = Tables.Const(EConst.FeedHealthRatio);
        var actors = Managers.Get<ActorRegistry>().All;
        Actor best = null;
        float bestDistance = Row.Range;
        for (int i = 0; i < actors.Count; ++i)
        {
            var actor = actors[i];
            if (actor == null || actor.IsDead == true || Actor.IsHostileTo(actor) == false)
            {
                continue;
            }

            if (actor.HealthRatio > threshold)
            {
                continue;
            }

            float distance = MathUtil.FlatDistance(Actor.transform.position, actor.transform.position) - actor.Radius;
            if (distance <= bestDistance)
            {
                bestDistance = distance;
                best = actor;
            }
        }

        return best;
    }

    /// <summary>HUD 의 "F 흡혈" 프롬프트용.</summary>
    public bool HasVictimInRange()
    {
        return findVictim() != null;
    }
}
