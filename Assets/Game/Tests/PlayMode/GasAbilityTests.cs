using System.Collections;
using Framework;
using Framework.Ability;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

/// <summary>
/// GAS 그래프 어빌리티 (GAS Tool 로 만드는 Type=Graph) 의 런타임 규칙 검증.
/// 그래프는 코드로 만들고 GraphAbility.Create 로 몬스터에 직접 부여한다 (테이블/에셋에 의존하지 않음).
/// </summary>
public class GasAbilityTests : GamePlayTestBase
{
    private const int InvulnerableEffectId = 8;

    private MonsterCharacter Monster;

    private IEnumerator spawnIdleMonster(float InDistance)
    {
        yield return SpawnMonster(2, new Vector3(0.0f, 0.0f, InDistance), m => Monster = m);
        Monster.Brain.enabled = false;
        Monster.Motor.Stop();
        Monster.Motor.Face(Player.transform.position - Monster.transform.position, true);
    }

    /// <summary>방어: 자신에게 건 GE(무적, 원래 4초)가 "종료 시 제거" 로 어빌리티가 끝나는 순간 사라지고, 실행 중 태그도 빠진다.</summary>
    [UnityTest]
    public IEnumerator Guard_EffectAndOwnedTag_RemovedWhenAbilityEnds()
    {
        yield return spawnIdleMonster(10.0f);
        var graph = MakeGraph(
            new ApplyEffectToSelfStep { EffectId = InvulnerableEffectId, RemoveOnEnd = true },
            new WaitStep { Seconds = 0.5f, ScaleWithAttackSpeed = false });
        graph.ActivationOwnedTags = new[] { ETag.State_Casting };
        var ability = GraphAbility.Create(MakeGraphRow(9001, EAbilitySlot.Spell2, 3.0f, 30.0f), Monster, graph);
        Monster.GrantAbility(ability);

        EActivateFailReason reason;
        Assert.IsTrue(Monster.Asc.TryActivateAbility(ability, AimAt(Monster, Player), out reason), $"발동 실패: {reason}");
        Assert.IsTrue(Monster.HasTag((int)ETag.State_Invulnerable), "가드 GE 태그가 붙어야 함");
        Assert.IsTrue(Monster.HasTag((int)ETag.State_Casting), "ActivationOwnedTags 가 붙어야 함");

        yield return WaitFor(() => ability.IsActive == false, 2.0f, "가드 종료");
        Assert.IsFalse(Monster.HasTag((int)ETag.State_Invulnerable), "종료 시 제거 GE 가 남아 있음");
        Assert.IsFalse(Monster.HasTag((int)ETag.State_Casting), "ActivationOwnedTags 가 남아 있음");
        Assert.Greater(Monster.Asc.GetCooldownRemaining(9001), 0.0f, "쿨다운이 적용되어야 함");
    }

    /// <summary>공격: 부채꼴 피해 단계가 앞의 적에게 이 어빌리티 번호로 피해를 준다.</summary>
    [UnityTest]
    public IEnumerator MeleeHit_DamagesTargetInFront()
    {
        KeepPlayerAlive = false;
        yield return spawnIdleMonster(1.8f);
        var graph = MakeGraph(new FaceAimStep(), new MeleeHitStep { Range = 3.0f, Angle = 120.0f, Damage = 100.0f, HitCue = 0 });
        var ability = GraphAbility.Create(MakeGraphRow(9002, EAbilitySlot.Primary, 0.5f, 3.0f), Monster, graph);
        Monster.GrantAbility(ability);

        bool hit = false;
        System.Action<DamageEvent> listener = e => hit |= e.Source == Monster && e.Target == Player && e.AbilityId == 9002 && e.Amount + e.Absorbed > 0.0f;
        EventBus<DamageEvent>.Subscribe(listener);
        float before = Player.Attributes.Get(EAttribute.Health);
        EActivateFailReason reason;
        bool activated = Monster.Asc.TryActivateAbility(ability, AimAt(Monster, Player), out reason);
        yield return null;
        EventBus<DamageEvent>.Unsubscribe(listener);

        Assert.IsTrue(activated, $"발동 실패: {reason}");
        Assert.IsTrue(hit, "플레이어에게 이 어빌리티의 피해 이벤트가 없음");
        Assert.Less(Player.Attributes.Get(EAttribute.Health), before, "플레이어 체력이 줄어야 함");
    }

    /// <summary>태그 규칙: ActivationBlockedTags 를 가지고 있으면 발동이 막히고, 빠지면 발동된다.</summary>
    [UnityTest]
    public IEnumerator ActivationBlockedTag_PreventsActivation()
    {
        yield return spawnIdleMonster(10.0f);
        var graph = MakeGraph(new WaitStep { Seconds = 0.1f });
        graph.ActivationBlockedTags = new[] { ETag.State_Debuff_Silence };
        var ability = GraphAbility.Create(MakeGraphRow(9003, EAbilitySlot.Spell1, 0.0f, 30.0f), Monster, graph);
        Monster.GrantAbility(ability);

        Monster.AddTag((int)ETag.State_Debuff_Silence);
        EActivateFailReason reason;
        Assert.IsFalse(Monster.Asc.TryActivateAbility(ability, AimAt(Monster, Player), out reason));
        Assert.AreEqual(EActivateFailReason.BlockedByTags, reason);

        Monster.RemoveTag((int)ETag.State_Debuff_Silence);
        Assert.IsTrue(Monster.Asc.TryActivateAbility(ability, AimAt(Monster, Player), out reason), $"발동 실패: {reason}");
    }

    /// <summary>쿨다운: 커밋 뒤 쿨다운 동안 재발동이 막힌다.</summary>
    [UnityTest]
    public IEnumerator Cooldown_BlocksReactivation()
    {
        yield return spawnIdleMonster(10.0f);
        var ability = GraphAbility.Create(MakeGraphRow(9004, EAbilitySlot.WeaponQ, 5.0f, 30.0f), Monster, MakeGraph(new WaitStep { Seconds = 0.05f }));
        Monster.GrantAbility(ability);

        EActivateFailReason reason;
        Assert.IsTrue(Monster.Asc.TryActivateAbility(ability, AimAt(Monster, Player), out reason), $"발동 실패: {reason}");
        yield return WaitFor(() => ability.IsActive == false, 1.0f, "어빌리티 종료");
        Assert.IsFalse(Monster.Asc.TryActivateAbility(ability, AimAt(Monster, Player), out reason));
        Assert.AreEqual(EActivateFailReason.Cooldown, reason);
    }
}
