using System.Collections;
using Framework;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

/// <summary>
/// 실제 게임 흐름 시나리오: 성 짓기(PlayScenarioTests.Castle.cs), 일반 몬스터 전투, 보스 전투.
/// 입력 대신 게임 API(BuildManager.PlaceAt, Actor.TryActivate)로 플레이어 행동을 흉내 낸다.
/// </summary>
public partial class PlayScenarioTests : GamePlayTestBase
{
    private const int BanditThug = 2;
    private const int BossGrimgor = 5;

    /// <summary>
    /// 일반 몬스터 전투: 스폰된 몬스터가 플레이어를 감지·공격 → 플레이어가 평타로 처치 → 사망 이벤트(처치자 = 플레이어).
    /// </summary>
    [UnityTest]
    public IEnumerator Scenario_MonsterFight()
    {
        MonsterCharacter monster = null;
        yield return SpawnMonster(BanditThug, new Vector3(0.0f, 0.0f, 6.0f), m => monster = m);

        bool playerHit = false;
        Actor killer = null;
        System.Action<DamageEvent> onDamage = e => playerHit |= e.Source == monster && e.Target == Player;
        System.Action<ActorDiedEvent> onDied = e =>
        {
            if (e.Victim == monster)
            {
                killer = e.Killer;
            }
        };
        EventBus<DamageEvent>.Subscribe(onDamage);
        EventBus<ActorDiedEvent>.Subscribe(onDied);
        try
        {
            yield return WaitFor(() => playerHit == true, 12.0f, "몬스터가 플레이어 공격");

            // 처치 시간을 줄이기 위해 몬스터 체력을 낮춘 뒤 플레이어 평타.
            monster.Attributes.Set(EAttribute.Health, monster.Attributes.Get(EAttribute.MaxHealth) * 0.15f);
            yield return WaitFor(() => monster.IsDead == true, 20.0f, "플레이어가 몬스터 처치", () => attack(monster));
            Assert.AreEqual(Player, killer, "처치자는 플레이어");
        }
        finally
        {
            EventBus<DamageEvent>.Unsubscribe(onDamage);
            EventBus<ActorDiedEvent>.Unsubscribe(onDied);
        }
    }

    /// <summary>
    /// 보스 전투: 교전 시작(BossEngagedEvent) → 체력 50% 이하에서 2페이즈 진입(Phase2 상태 + 격노 버프) → 보스가 어빌리티 사용 → 처치.
    /// </summary>
    [UnityTest]
    public IEnumerator Scenario_BossFight()
    {
        Player.AddTag((int)ETag.Cheat_God);
        MonsterCharacter boss = null;
        bool engaged = false;
        int abilityUses = 0;

        // 보스는 스폰 직후 첫 감지에서 교전을 시작할 수 있으므로 스폰 전에 구독한다.
        System.Action<BossEngagedEvent> onEngaged = e => engaged |= e.Boss is MonsterCharacter && e.Boss.CharacterId == BossGrimgor && e.Engaged == true;
        System.Action<Framework.Ability.GameplayAbility> onActivated = a => abilityUses += 1;
        EventBus<BossEngagedEvent>.Subscribe(onEngaged);
        yield return SpawnMonster(BossGrimgor, new Vector3(0.0f, 0.0f, 8.0f), m => boss = m);
        boss.Asc.OnAbilityActivated += onActivated;
        try
        {
            yield return WaitFor(() => engaged == true, 10.0f, "보스 교전 시작");
            Assert.IsTrue(IsInState(boss, "Phase1"), "처음엔 Phase1");

            boss.Attributes.Set(EAttribute.Health, boss.Attributes.Get(EAttribute.MaxHealth) * 0.45f);
            yield return WaitFor(() => IsInState(boss, "Phase2") == true, 5.0f, "체력 45% 에서 Phase2 진입");
            yield return WaitFor(() => boss.HasTag((int)ETag.State_Buff) == true, 3.0f, "2페이즈 격노 버프");
            yield return WaitFor(() => abilityUses > 0, 10.0f, "보스가 어빌리티 사용");

            boss.Attributes.Set(EAttribute.Health, boss.Attributes.Get(EAttribute.MaxHealth) * 0.02f);
            yield return WaitFor(() => boss.IsDead == true, 25.0f, "보스 처치", () => attack(boss));
        }
        finally
        {
            EventBus<BossEngagedEvent>.Unsubscribe(onEngaged);
            if (boss != null)
            {
                boss.Asc.OnAbilityActivated -= onActivated;
            }
        }
    }

    /// <summary>플레이어 평타: 대상에게 다가가며(사거리 밖이면 순간 이동) Primary 슬롯 발동.</summary>
    private void attack(MonsterCharacter InTarget)
    {
        if (InTarget == null || InTarget.IsDead == true)
        {
            return;
        }

        float distance = MathUtil.FlatDistance(Player.transform.position, InTarget.transform.position);
        if (distance > 2.2f + InTarget.Radius)
        {
            var direction = MathUtil.FlatDirection(InTarget.transform.position, Player.transform.position, Vector3.forward);
            Player.Motor.Teleport(InTarget.transform.position + direction * (1.6f + InTarget.Radius));
        }

        Player.TryActivate(EAbilitySlot.Primary, InTarget.transform.position, InTarget);
    }
}
