using System.Collections;
using Framework;
using Framework.UI;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

/// <summary>
/// 미니맵 · 월드맵 · 입력 맵:
///  - 입력을 막는 창(월드맵)이 열리면 Combat 맵(어빌리티 · 카메라 줌)이 꺼지고 Movement 는 유지, 닫으면 다시 켜진다.
///  - 미니맵은 카메라가 보는 방향이 위 (화면과 같은 방향), 플레이어가 가운데.
///  - 미니맵에 주변 몬스터 표식이 생긴다.
/// </summary>
public class MapTests : GamePlayTestBase
{
    private const int Medusa = 2;

    [UnityTest]
    public IEnumerator WorldMap_DisablesCombatInput()
    {
        var ui = Managers.Get<UIManager>();
        var input = Managers.Get<GameInput>();
        var worldMap = ui.Get<WorldMapWindow>();
        Assert.IsNotNull(worldMap, "WorldMapWindow 미생성");

        yield return null;
        Assert.IsTrue(input.IsMapEnabled(EInputMap.Combat), "창이 없을 때 Combat 켜짐");

        worldMap.Open();
        yield return null;
        yield return null;
        Assert.IsFalse(input.IsMapEnabled(EInputMap.Combat), "월드맵이 열리면 Combat 꺼짐 (휠 줌 · 공격 무시)");
        Assert.IsTrue(input.IsMapEnabled(EInputMap.Movement), "이동은 유지");
        Assert.IsTrue(input.IsMapEnabled(EInputMap.System), "창 열기 · 닫기 키는 유지");
        Assert.IsFalse(input.Pressed(EInputAction.Primary), "꺼진 맵의 액션은 눌리지 않음");
        Assert.AreEqual(0.0f, input.ReadFloat(EInputAction.Zoom), "꺼진 맵의 줌 값은 0");

        var minimap = ui.Get<MinimapWindow>();
        Assert.IsFalse(minimap.Frame.gameObject.activeSelf, "월드맵이 열려 있는 동안 미니맵 숨김");

        worldMap.Close();
        yield return null;
        yield return null;
        Assert.IsTrue(input.IsMapEnabled(EInputMap.Combat), "닫으면 Combat 다시 켜짐");
        Assert.IsTrue(minimap.Frame.gameObject.activeSelf, "미니맵 다시 표시");
    }

    [UnityTest]
    public IEnumerator Minimap_FollowsPlayerAndCamera()
    {
        var minimap = Managers.Get<UIManager>().Get<MinimapWindow>();
        Assert.IsNotNull(minimap, "MinimapWindow 미생성");
        Assert.IsTrue(minimap.IsOpen, "게임 시작 시 미니맵 열림");
        yield return null;

        var view = minimap.View;
        Vector2 center = view.WorldToMap(Player.transform.position);
        Assert.Less(center.magnitude, 1.0f, "플레이어가 미니맵 가운데");

        Vector3 forward = SceneContext.Current.Camera.transform.forward;
        forward.y = 0.0f;
        Vector2 ahead = view.WorldToMap(Player.transform.position + forward.normalized * 10.0f);
        Assert.Less(Mathf.Abs(ahead.x), 1.0f, "카메라 앞쪽은 미니맵 위쪽 (x ≈ 0)");
        Assert.Greater(ahead.y, 0.0f, "카메라 앞쪽은 미니맵 위쪽 (y > 0)");

        int before = activeMarkers(view);
        MonsterCharacter monster = null;
        yield return SpawnMonster(Medusa, new Vector3(0.0f, 0.0f, 8.0f), m => monster = m);
        yield return null;
        Assert.Greater(activeMarkers(view), before, "주변 몬스터 표식 추가");
    }

    /// <summary>퀘스트 가이드: 상점 퀘스트 → 상인 쪽 선 · 표식, 채집 → 채집 지점, 이동 퀘스트(장소 없음) → 선 없음.</summary>
    [UnityTest]
    public IEnumerator Minimap_ShowsQuestGuide()
    {
        var quests = Managers.Get<QuestManager>();
        var minimap = Managers.Get<UIManager>().Get<MinimapWindow>();
        var view = minimap.View;
        Vector3 guide;

        Assert.IsTrue(quests.Begin(6), "상점 퀘스트");
        Assert.AreEqual(EQuestObjective.Buy, quests.Current.Objective);
        Assert.IsTrue(quests.TryGetGuidePosition(Player.transform.position, out guide), "상점 퀘스트 가이드");
        Merchant merchant = Object.FindAnyObjectByType<Merchant>();
        Assert.Less(Vector3.Distance(guide, merchant.transform.position), 0.1f, "가이드 = 상인 위치");
        yield return null;
        Assert.IsTrue(view.GuideLine.gameObject.activeSelf, "미니맵 가이드 선");

        Assert.IsTrue(quests.Begin(7), "채집 퀘스트");
        Assert.IsTrue(quests.TryGetGuidePosition(Player.transform.position, out guide), "채집 퀘스트 가이드 (채집 지점)");

        // 먼 곳(보스): 미니맵 밖 → 가장자리 화살표.
        Assert.IsTrue(quests.Begin(17), "보스 퀘스트");
        Assert.IsTrue(quests.TryGetGuidePosition(Player.transform.position, out guide), "보스 퀘스트 가이드 (리스폰 대기 중이면 아레나)");
        Assert.Greater(Vector3.Distance(guide, Player.transform.position), Tables.Const(EConst.MinimapRadius), "보스는 미니맵 반경 밖");
        yield return null;
        Assert.IsTrue(view.GuideLine.gameObject.activeSelf, "밖이어도 가장자리까지 선");
        Assert.IsTrue(hasMarkerWithSprite(view, view.GuideArrow), "가장자리 화살표 표식");

        Assert.IsTrue(quests.Begin(1), "이동 퀘스트");
        Assert.IsFalse(quests.TryGetGuidePosition(Player.transform.position, out guide), "이동 퀘스트는 장소 없음");
        yield return null;
        Assert.IsFalse(view.GuideLine.gameObject.activeSelf, "가이드 선 숨김");
    }

    private static bool hasMarkerWithSprite(MapView InView, Sprite InSprite)
    {
        foreach (Transform child in InView.MarkerRoot)
        {
            var image = child.GetComponent<UnityEngine.UI.Image>();
            if (child.gameObject.activeSelf == true && image != null && image != InView.GuideLine && image.sprite == InSprite && image.color == InView.MarkerColors[(int)EMapMarker.Quest])
            {
                return true;
            }
        }

        return false;
    }

    private static int activeMarkers(MapView InView)
    {
        int count = 0;
        foreach (Transform child in InView.MarkerRoot)
        {
            if (child.gameObject.activeSelf == true)
            {
                count += 1;
            }
        }

        return count;
    }
}
