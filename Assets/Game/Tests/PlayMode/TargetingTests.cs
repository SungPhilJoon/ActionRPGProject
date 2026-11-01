using System.Collections;
using Framework;
using Framework.UI;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

/// <summary>
/// 대상 지정 (Targeting.md): 몬스터 · 채집 노드를 지정하면 하이라이트 색이 바뀌고 HUD 중상단 정보창이 뜬다. 몬스터가 죽으면 해제된다.
/// 마우스 호버 자체는 화면 좌표가 필요해 여기서는 지정 API(TargetSelector.Select)와 하이라이트 · 정보창 연결을 검사한다.
/// </summary>
public class TargetingTests : GamePlayTestBase
{
    private const int Medusa = 2;

    [UnityTest]
    public IEnumerator SelectMonster_HighlightsAndShowsPanel()
    {
        var selector = Managers.Get<TargetSelector>();
        var hud = Managers.Get<UIManager>().Get<HudWindow>();
        MonsterCharacter monster = null;
        yield return SpawnMonster(Medusa, new Vector3(0.0f, 0.0f, 8.0f), m => monster = m);
        Assert.IsTrue(TargetSelector.IsTargetable(monster));

        selector.Select(monster);
        yield return null;
        var highlight = monster.GetComponent<RendererHighlight>();
        Assert.IsNotNull(highlight, "하이라이트 겹 렌더러 생성");
        Assert.IsTrue(highlight.IsShown);
        Assert.AreEqual(TargetSelector.HostileColor, highlight.CurrentColor, "선택한 몬스터 = 적대 색");
        Assert.IsTrue(hud.TargetRoot.gameObject.activeSelf, "HUD 대상 정보창 표시");

        monster.Die(Player);
        yield return null;
        yield return null;
        Assert.IsNull(selector.Selected, "죽으면 선택 해제");
        Assert.IsFalse(highlight.IsShown, "하이라이트 끔");
        Assert.IsFalse(hud.TargetRoot.gameObject.activeSelf, "정보창 숨김");
    }

    [UnityTest]
    public IEnumerator SelectNode_UsesNodeColor()
    {
        var selector = Managers.Get<TargetSelector>();
        ResourceNode node = null;
        float best = float.MaxValue;
        foreach (var candidate in Object.FindObjectsByType<ResourceNode>(FindObjectsSortMode.None))
        {
            float distance = Vector3.Distance(candidate.transform.position, Player.transform.position);
            if (candidate.IsDepleted == false && distance < best)
            {
                best = distance;
                node = candidate;
            }
        }

        Assert.IsNotNull(node, "채집 노드가 없습니다");
        Player.Motor.Teleport(CombatUtil.SnapToGround(node.transform.position + Vector3.back * 4.0f));
        yield return null;

        selector.Select(node);
        yield return null;
        var highlight = node.GetComponent<RendererHighlight>();
        Assert.IsNotNull(highlight);
        Assert.AreEqual(TargetSelector.NodeColor, highlight.CurrentColor, "선택한 채집 노드 = 노드 색");
        Assert.IsTrue(Managers.Get<UIManager>().Get<HudWindow>().TargetRoot.gameObject.activeSelf);

        selector.Select(null);
        yield return null;
        Assert.IsFalse(highlight.IsShown, "선택 해제 → 하이라이트 끔");
        Player.Motor.Teleport(StartPosition);
    }
}
