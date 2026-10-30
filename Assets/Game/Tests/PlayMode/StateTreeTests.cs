using System.Collections;
using Framework;
using Framework.StateTree;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

/// <summary>
/// 몬스터 AI (StateTree) 검증. 기본 트리의 전투 진입 + StateTree Tool 의 "조건부 어빌리티 사용" 블록과 같은 구성을 코드로 만들어 돌린다.
/// </summary>
public class StateTreeTests : GamePlayTestBase
{
    private MonsterCharacter Monster;

    /// <summary>기본 근접 트리: 감지 → Combat 진입 → 플레이어에게 피해.</summary>
    [UnityTest]
    public IEnumerator DefaultTree_AcquiresTarget_AndAttacks()
    {
        yield return SpawnMonster(2, new Vector3(0.0f, 0.0f, 5.0f), m => Monster = m);
        yield return WaitFor(() => IsInState(Monster, "Combat"), 5.0f, "Combat 상태 진입");
        Assert.AreEqual(Player, Monster.Brain.CurrentTarget, "타깃이 플레이어여야 함");

        bool damaged = false;
        System.Action<DamageEvent> listener = e => damaged |= e.Source == Monster && e.Target == Player;
        EventBus<DamageEvent>.Subscribe(listener);
        yield return WaitFor(() => damaged == true, 10.0f, "몬스터가 플레이어 공격");
        EventBus<DamageEvent>.Unsubscribe(listener);
    }

    /// <summary>
    /// 조건부 어빌리티: 체력 50% 이하일 때만 Spell1(가드)을 쓰고, 그 전에는 공격만 한다.
    /// (Root → Combat[HasTarget] → UseSpell1[HealthRatio ≤ 0.5, Ready] / Attack[Primary] / Hold)
    /// </summary>
    [UnityTest]
    public IEnumerator ConditionalAbilityState_UsesGuardOnlyBelowHalfHealth()
    {
        yield return SpawnMonster(2, new Vector3(0.0f, 0.0f, 4.0f), m => Monster = m);
        var guard = GraphAbility.Create(MakeGraphRow(9101, EAbilitySlot.Spell1, 10.0f, 30.0f), Monster,
            MakeGraph(new ApplyEffectToSelfStep { EffectId = 8, RemoveOnEnd = true }, new WaitStep { Seconds = 1.0f, ScaleWithAttackSpeed = false }));
        Monster.GiveAbility(guard);
        Monster.Brain.RunTree(buildTree());

        // 체력이 높으면 가드를 쓰지 않는다.
        yield return WaitSeconds(2.0f);
        Assert.IsFalse(guard.IsActive == true || Monster.Asc.GetCooldownRemaining(9101) > 0.0f, "체력이 높은데 가드를 사용함");

        Monster.Attributes.Set(EAttribute.Health, Monster.Attributes.Get(EAttribute.MaxHealth) * 0.4f);
        yield return WaitFor(() => Monster.HasTag((int)ETag.State_Invulnerable) == true, 5.0f, "체력 40% 에서 가드 사용");
        Assert.IsTrue(IsInState(Monster, "UseSpell1"), "UseSpell1 상태여야 함");
    }

    private static StateTreeAsset buildTree()
    {
        var tree = ScriptableObject.CreateInstance<StateTreeAsset>();
        tree.Evaluators.Add(new MonsterPerceptionEvaluator());
        var root = tree.AddState("Root", 0);
        var combat = tree.AddState("Combat", root.Id);
        combat.EnterConditions.Add(new HasTargetCondition());

        var guard = tree.AddState("UseSpell1", combat.Id);
        guard.EnterConditions.Add(new BlackboardCompareCondition { Key = EBBKey.HealthRatio, Op = ECompareOp.LessOrEqual, Value = 0.5f });
        guard.EnterConditions.Add(new AbilityReadyCondition { Slots = new[] { EAbilitySlot.Spell1 } });
        guard.Tasks.Add(new UseAbilityTask { Slots = new[] { EAbilitySlot.Spell1 } });
        Goto(guard, combat.Id);

        var attack = tree.AddState("Attack", combat.Id);
        attack.EnterConditions.Add(new AbilityReadyCondition { Slots = new[] { EAbilitySlot.Primary } });
        attack.Tasks.Add(new UseAbilityTask { Slots = new[] { EAbilitySlot.Primary } });
        Goto(attack, combat.Id);

        var hold = tree.AddState("Hold", combat.Id);
        hold.Tasks.Add(new FaceTargetTask());
        hold.Tasks.Add(new WaitTask { Duration = 0.2f });
        Goto(hold, combat.Id);

        var idle = tree.AddState("Idle", root.Id);
        idle.Tasks.Add(new WaitTask { Duration = 0.5f });
        idle.Transitions.Add(new StateTreeTransition { Trigger = ETransitionTrigger.OnStateCompleted, Type = ETransitionType.Root });
        return tree;
    }
}
