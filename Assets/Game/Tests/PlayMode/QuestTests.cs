using System;
using System.Collections;
using System.Reflection;
using Framework;
using Framework.UI;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

/// <summary>
/// 퀘스트 체인 전체 (QuestTable 1 → 17): 튜토리얼(이동 · 어빌리티 · 상점 · 채집 · 제작 · 장비 · 건축) → 사냥 → 보스 → QuestsCompletedEvent.
/// 플레이어 행동은 게임 API 로 흉내 낸다 (상점 · 제작 버튼은 창의 private 메서드를 리플렉션으로 호출).
/// 건축 퀘스트는 성 짓기 테스트(PlayScenarioTests.Castle)와 성 심장 1개 제한이 겹치므로 치트 완료로 넘긴다.
/// </summary>
public class QuestTests : GamePlayTestBase
{
    private const int Medusa = 2;
    private const int Cobra = 3;
    private const int Troll = 4;
    private const int Boss = 5;
    private const float StepTimeout = 15.0f;

    private QuestManager Quests;

    [UnityTest]
    public IEnumerator Scenario_QuestChain()
    {
        Player.AddTag((int)ETag.Cheat_God);
        Quests = Managers.Get<QuestManager>();
        Assert.IsNotNull(Quests, "QuestManager 미등록");
        Assert.IsTrue(Quests.Begin(Tables.ConstInt(EConst.FirstQuestId)), "첫 퀘스트 시작");
        Assert.AreEqual(EQuestObjective.Move, Quests.Current.Objective);

        var hud = Managers.Get<UIManager>().Get<HudWindow>();
        Assert.IsTrue(hud.QuestRoot.gameObject.activeSelf, "HUD 퀘스트 추적기 표시");

        bool allDone = false;
        Action<QuestsCompletedEvent> onAllDone = e => allDone = true;
        EventBus<QuestsCompletedEvent>.Subscribe(onAllDone);
        try
        {
            // 1. 이동: 프레임마다 1m 씩 (순간 이동 판정 3m 미만).
            Vector3 origin = Player.transform.position;
            int step = 0;
            yield return advance(2, () =>
            {
                step += 1;
                Player.Motor.Teleport(origin + new Vector3(Mathf.PingPong(step, 12.0f), 0.0f, 0.0f));
            }, "이동");

            // 2~5. 어빌리티 사용.
            yield return advance(3, () => use(EAbilitySlot.Primary), "평타 3회");
            yield return advance(4, () => use(EAbilitySlot.WeaponQ), "무기 스킬 Q");
            yield return advance(5, () => use(EAbilitySlot.Travel), "Veil of Blood");
            yield return advance(6, () => use(EAbilitySlot.Spell1), "주문 R");

            // 6. 상점 (ShopWindow.buy).
            var shop = Managers.Get<UIManager>().Get<ShopWindow>();
            invoke(shop, "buy", Tables.Root.Shop.All[0]);
            yield return advance(7, null, "상점");

            // 7. 채집: 가장 가까운 채집 노드를 계속 친다 (진행도는 얻은 개수).
            Assert.AreEqual(EQuestObjective.Gather, Quests.Current.Objective);
            int rewardBefore = Player.Inventory.Count(Quests.Current.RewardItem);
            yield return advance(8, harvestNearest, "채집");
            Assert.Greater(Player.Inventory.Count(30), rewardBefore, "채집 퀘스트 보상(피의 물약)");

            // 8. 제작 (CraftWindow.craft) — 재료를 넣고 첫 손 제작 레시피.
            Player.Inventory.Add(3, 100);
            Player.Inventory.Add(5, 20);
            var craft = Managers.Get<UIManager>().Get<CraftWindow>();
            invoke(craft, "craft", findHandRecipe());
            yield return advance(9, null, "제작");

            // 9. 흉갑 장착.
            Player.Equipment.Equip(20);
            yield return advance(10, null, "흉갑 장착");

            // 10~11. 건축 (위 설명 참고).
            Assert.AreEqual(EQuestObjective.Build, Quests.Current.Objective);
            Quests.CompleteCurrent();
            yield return advance(11, null, "성 심장");
            Quests.CompleteCurrent();
            yield return advance(12, null, "관");

            // 12. 메두사 3.
            yield return killMonsters(Medusa, 3, 13, false);

            // 13. 흡혈.
            yield return killMonsters(Cobra, 1, 14, true);

            // 14. 코브라 3.
            yield return killMonsters(Cobra, 3, 15, false);

            // 15. 기어 레벨 16: 구리 무기 + 강화 방어구 4종.
            int[] copperSet = { 11, 24, 25, 26, 27 };
            foreach (int item in copperSet)
            {
                Player.Equipment.Equip(item);
            }

            yield return advance(16, null, "기어 레벨 16");

            // 16. 트롤 3.
            yield return killMonsters(Troll, 3, 17, false);

            // 17. 보스 — 흡혈로 처치해도 처치로 센다.
            Assert.AreEqual(EQuestChapter.Boss, Quests.Current.Chapter);
            Assert.AreEqual(0, Quests.Current.Next, "보스가 마지막 퀘스트");
            yield return killMonsters(Boss, 1, 0, true);
            yield return WaitFor(() => allDone == true, StepTimeout, "모든 퀘스트 완료 이벤트");

            Assert.IsTrue(Quests.IsAllCompleted);
            Assert.IsNull(Quests.Current);
            Assert.IsTrue(hud.VictoryRoot.gameObject.activeSelf, "클리어 오버레이 표시");
            Assert.IsTrue(Player.IsUnlocked(Tables.Root.Character.Get(Boss).VBloodUnlocks[0]), "보스 흡혈로 V Blood 해금");
        }
        finally
        {
            EventBus<QuestsCompletedEvent>.Unsubscribe(onAllDone);
            Player.RemoveTag((int)ETag.Cheat_God);
        }
    }

    /// <summary>InNextId 퀘스트가 시작될 때까지 (완료 → 2.5초 뒤 다음) 매 프레임 InTick 을 실행한다. 0 이면 체인 끝.</summary>
    private IEnumerator advance(int InNextId, Action InTick, string InWhat)
    {
        yield return WaitFor(() => InNextId == 0 ? Quests.IsAllCompleted : (Quests.Current != null && Quests.Current.Index == InNextId && Quests.IsCompleted == false), StepTimeout, InWhat, () =>
        {
            if (InTick != null && Quests.IsCompleted == false)
            {
                InTick();
            }
        });
    }

    private void use(EAbilitySlot InSlot)
    {
        Player.Asc.ClearCooldowns();
        Player.TryActivate(InSlot, Player.transform.position + Player.transform.forward * 5.0f);
    }

    private void harvestNearest()
    {
        ResourceNode best = null;
        float bestDistance = float.MaxValue;
        foreach (var node in UnityEngine.Object.FindObjectsByType<ResourceNode>(FindObjectsSortMode.None))
        {
            float distance = Vector3.Distance(node.transform.position, Player.transform.position);
            if (node.IsDepleted == false && distance < bestDistance)
            {
                best = node;
                bestDistance = distance;
            }
        }

        Assert.IsNotNull(best, "채집 노드가 없음");
        best.Harvest(Player);
    }

    private static RecipeRow findHandRecipe()
    {
        foreach (var recipe in Tables.Root.Recipe.All)
        {
            if (recipe.Station == EStation.Hand)
            {
                return recipe;
            }
        }

        Assert.Fail("손 제작 레시피 없음");
        return null;
    }

    /// <summary>InCharacterId 몬스터를 InCount 마리 스폰해 처치(InFeed 면 흡혈 후 처치)하고 InNextId 퀘스트를 기다린다.</summary>
    private IEnumerator killMonsters(int InCharacterId, int InCount, int InNextId, bool InFeed)
    {
        Assert.AreEqual(InFeed == true && InCharacterId != Boss ? EQuestObjective.Feed : EQuestObjective.Kill, Quests.Current.Objective, $"퀘스트 {Quests.Current.Index} 목표");
        for (int i = 0; i < InCount; ++i)
        {
            MonsterCharacter monster = null;
            yield return SpawnMonster(InCharacterId, new Vector3(0.0f, 0.0f, 6.0f), m => monster = m);
            float distance;
            Assert.IsTrue(Quests.TryGetNearestTarget(Player.transform.position, out distance), "HUD 안내용 가장 가까운 대상");
            if (InFeed == true)
            {
                Player.Blood.Consume(monster);
            }

            monster.Die(Player);
            yield return null;
        }

        yield return advance(InNextId, null, $"몬스터 {InCharacterId} x{InCount}");
    }

    private static void invoke(object InTarget, string InMethod, object InArg)
    {
        var method = InTarget.GetType().GetMethod(InMethod, BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.IsNotNull(method, $"{InTarget.GetType().Name}.{InMethod} 없음");
        method.Invoke(InTarget, new[] { InArg });
    }
}
