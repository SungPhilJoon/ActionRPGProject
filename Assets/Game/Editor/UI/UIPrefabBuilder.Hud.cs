using Framework.UI;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>HUD 프리팹 (UI/Windows/HudWindow): 체력 · 혈액, 어빌리티 바 8칸, 시계, 보스 바, 퀘스트 추적기, 안내 · 토스트 · 사망 · 클리어 오버레이.</summary>
public static partial class UIPrefabBuilder
{
    private static void buildHud()
    {
        RectTransform root;
        var hud = createWindow<HudWindow>(out root);

        hud.Prompt = UIFactory.CreateText(root, "Prompt", string.Empty, 24.0f, TextAlignmentOptions.Center);
        UIFactory.Place(hud.Prompt, new Vector2(0.5f, 0.0f), new Vector2(0.0f, 200.0f), new Vector2(700.0f, 40.0f));

        hud.SunLabel = UIFactory.CreateText(root, "Sun", string.Empty, 30.0f, TextAlignmentOptions.Center);
        hud.SunLabel.color = new Color(1.0f, 0.75f, 0.3f);
        // 보스 바(-30) · 대상 정보창(-118) 아래.
        UIFactory.Place(hud.SunLabel, new Vector2(0.5f, 1.0f), new Vector2(0.0f, -200.0f), new Vector2(700.0f, 50.0f));

        hud.Toast = UIFactory.CreateText(root, "Toast", string.Empty, 30.0f, TextAlignmentOptions.Center);
        UIFactory.Place(hud.Toast, new Vector2(0.5f, 0.5f), new Vector2(0.0f, 220.0f), new Vector2(1100.0f, 60.0f));

        hud.Hints = UIFactory.CreateText(root, "Hints", string.Empty, 16.0f, TextAlignmentOptions.BottomRight);
        hud.Hints.color = new Color(1.0f, 1.0f, 1.0f, 0.55f);
        UIFactory.Place(hud.Hints, new Vector2(1.0f, 0.0f), new Vector2(-20.0f, 12.0f), new Vector2(1100.0f, 30.0f));

        buildVitals(hud, root);
        buildAbilityBar(hud, root);
        buildClock(hud, root);
        buildBoss(hud, root);
        buildTarget(hud, root);
        buildQuest(hud, root);

        hud.DeathRoot = UIFactory.CreatePanel(root, "Death", new Color(0.25f, 0.0f, 0.0f, 0.45f));
        UIFactory.Stretch(hud.DeathRoot, 0.0f);
        hud.DeathText = UIFactory.CreateText(hud.DeathRoot, "Text", string.Empty, 56.0f, TextAlignmentOptions.Center);
        UIFactory.Stretch(hud.DeathText.rectTransform, 0.0f);
        hud.DeathRoot.gameObject.SetActive(false);

        hud.VictoryRoot = UIFactory.CreatePanel(root, "Victory", new Color(0.12f, 0.0f, 0.02f, 0.55f));
        UIFactory.Place(hud.VictoryRoot, new Vector2(0.5f, 0.5f), new Vector2(0.0f, 40.0f), new Vector2(1000.0f, 200.0f));
        hud.VictoryRoot.GetComponent<Image>().raycastTarget = false;
        hud.VictoryText = UIFactory.CreateText(hud.VictoryRoot, "Text", string.Empty, 44.0f, TextAlignmentOptions.Center);
        hud.VictoryText.color = new Color(1.0f, 0.35f, 0.35f);
        UIFactory.Stretch(hud.VictoryText.rectTransform, 12.0f);
        hud.VictoryRoot.gameObject.SetActive(false);
        save<HudWindow>(root, "Windows/HudWindow");
    }

    private static void buildVitals(HudWindow InHud, RectTransform InRoot)
    {
        var frame = UIFactory.CreateWindowFrame(InRoot, "Vitals");
        UIFactory.Place(frame, new Vector2(0.0f, 0.0f), new Vector2(20.0f, 20.0f), new Vector2(440.0f, 140.0f));

        InHud.HealthBar = UIFactory.CreateBar(frame, "Health", UIUtil.HealthColor, UIUtil.BarBackColor);
        UIFactory.Place(InHud.HealthBar, new Vector2(0.0f, 1.0f), new Vector2(16.0f, -16.0f), new Vector2(408.0f, 34.0f));

        InHud.BloodBar = UIFactory.CreateBar(frame, "Blood", UIUtil.BloodColor, UIUtil.BarBackColor);
        UIFactory.Place(InHud.BloodBar, new Vector2(0.0f, 1.0f), new Vector2(16.0f, -56.0f), new Vector2(408.0f, 24.0f));

        InHud.BloodLabel = UIFactory.CreateText(frame, "BloodType", string.Empty, 18.0f, TextAlignmentOptions.Left);
        UIFactory.Place(InHud.BloodLabel, new Vector2(0.0f, 0.0f), new Vector2(18.0f, 14.0f), new Vector2(260.0f, 40.0f));

        InHud.GearLabel = UIFactory.CreateText(frame, "Gear", string.Empty, 18.0f, TextAlignmentOptions.Right);
        InHud.GearLabel.color = UIUtil.GoldColor;
        UIFactory.Place(InHud.GearLabel, new Vector2(1.0f, 0.0f), new Vector2(-18.0f, 14.0f), new Vector2(150.0f, 40.0f));
    }

    private static void buildAbilityBar(HudWindow InHud, RectTransform InRoot)
    {
        int count = HudWindow.SlotKeys.Length;
        var bar = UIFactory.CreateWindowFrame(InRoot, "AbilityBar");
        UIFactory.Place(bar, new Vector2(0.5f, 0.0f), new Vector2(0.0f, 16.0f), new Vector2(count * 96.0f + 24.0f, 118.0f));
        UIFactory.AddHorizontal(bar, 8.0f, 14);

        InHud.Slots = new AbilitySlotView[count];
        for (int i = 0; i < count; ++i)
        {
            var slot = UIFactory.CreateSlot(bar, $"Slot{i}", UIUtil.SlotColor);
            slot.sizeDelta = new Vector2(88.0f, 88.0f);
            UIFactory.SetPreferred(slot, 88.0f, 88.0f);
            var view = slot.gameObject.AddComponent<AbilitySlotView>();
            view.Background = slot.GetComponent<Image>();

            view.Icon = UIFactory.CreateImage(slot, "Icon", Color.white);
            UIFactory.Stretch(view.Icon.rectTransform, 10.0f);
            view.Icon.enabled = false;

            view.Name = UIFactory.CreateText(slot, "Name", string.Empty, 13.0f, TextAlignmentOptions.Center);
            UIFactory.Stretch(view.Name.rectTransform, 6.0f);

            view.CooldownMask = UIFactory.CreatePanel(slot, "Cooldown", new Color(0.0f, 0.0f, 0.0f, 0.7f));
            view.CooldownMask.anchorMin = Vector2.zero;
            view.CooldownMask.anchorMax = new Vector2(1.0f, 0.0f);
            view.CooldownMask.offsetMin = Vector2.zero;
            view.CooldownMask.offsetMax = Vector2.zero;
            view.CooldownMask.GetComponent<Image>().raycastTarget = false;

            view.CooldownText = UIFactory.CreateText(slot, "CooldownText", string.Empty, 26.0f, TextAlignmentOptions.Center);
            UIFactory.Stretch(view.CooldownText.rectTransform, 0.0f);

            view.Key = UIFactory.CreateText(slot, "Key", "?", 15.0f, TextAlignmentOptions.TopLeft);
            view.Key.color = UIUtil.GoldColor;
            UIFactory.Stretch(view.Key.rectTransform, 6.0f);
            InHud.Slots[i] = view;
        }
    }

    private static void buildClock(HudWindow InHud, RectTransform InRoot)
    {
        var frame = UIFactory.CreateWindowFrame(InRoot, "Clock");
        // 오른쪽 위 미니맵(MinimapWindow, 270 x 270) 바로 아래.
        UIFactory.Place(frame, new Vector2(1.0f, 1.0f), new Vector2(-20.0f, -298.0f), new Vector2(270.0f, 80.0f));
        InHud.ClockLabel = UIFactory.CreateText(frame, "Label", string.Empty, 20.0f, TextAlignmentOptions.Center);
        UIFactory.Place(InHud.ClockLabel, new Vector2(0.5f, 1.0f), new Vector2(0.0f, -10.0f), new Vector2(240.0f, 30.0f));
        InHud.ClockBar = UIFactory.CreateBar(frame, "Progress", new Color(0.95f, 0.75f, 0.35f), UIUtil.BarBackColor);
        UIFactory.Place(InHud.ClockBar, new Vector2(0.5f, 0.0f), new Vector2(0.0f, 14.0f), new Vector2(236.0f, 16.0f));
        InHud.ClockBar.Label.text = string.Empty;
    }

    /// <summary>퀘스트 추적기: 미니맵 · 시계 아래 오른쪽. 묶음 이름 · 제목([키]) · 설명(+보상) · 진행도(+가장 가까운 대상 거리).</summary>
    private static void buildQuest(HudWindow InHud, RectTransform InRoot)
    {
        var frame = UIFactory.CreateWindowFrame(InRoot, "Quest");
        UIFactory.Place(frame, new Vector2(1.0f, 1.0f), new Vector2(-20.0f, -386.0f), new Vector2(400.0f, 190.0f));
        InHud.QuestRoot = frame;

        InHud.QuestChapter = UIFactory.CreateText(frame, "Chapter", string.Empty, 15.0f, TextAlignmentOptions.TopLeft);
        InHud.QuestChapter.color = new Color(0.85f, 0.35f, 0.35f);
        UIFactory.Place(InHud.QuestChapter, new Vector2(0.0f, 1.0f), new Vector2(16.0f, -12.0f), new Vector2(368.0f, 22.0f));

        InHud.QuestTitle = UIFactory.CreateText(frame, "Title", string.Empty, 21.0f, TextAlignmentOptions.TopLeft);
        InHud.QuestTitle.color = UIUtil.GoldColor;
        UIFactory.Place(InHud.QuestTitle, new Vector2(0.0f, 1.0f), new Vector2(16.0f, -34.0f), new Vector2(368.0f, 30.0f));

        InHud.QuestDesc = UIFactory.CreateText(frame, "Desc", string.Empty, 15.0f, TextAlignmentOptions.TopLeft);
        InHud.QuestDesc.textWrappingMode = TextWrappingModes.Normal;
        UIFactory.Place(InHud.QuestDesc, new Vector2(0.0f, 1.0f), new Vector2(16.0f, -66.0f), new Vector2(368.0f, 84.0f));

        InHud.QuestProgress = UIFactory.CreateText(frame, "Progress", string.Empty, 17.0f, TextAlignmentOptions.BottomLeft);
        UIFactory.Place(InHud.QuestProgress, new Vector2(0.0f, 0.0f), new Vector2(16.0f, 12.0f), new Vector2(368.0f, 26.0f));
        frame.gameObject.SetActive(false);
    }

    /// <summary>대상 정보창 (중상단, 보스 바 아래): 이름 · 레벨/내구도 · 체력 바 · 혈액 · 상태이상 / 획득물.</summary>
    private static void buildTarget(HudWindow InHud, RectTransform InRoot)
    {
        var frame = UIFactory.CreateWindowFrame(InRoot, "Target");
        UIFactory.Place(frame, new Vector2(0.5f, 1.0f), new Vector2(0.0f, -118.0f), new Vector2(480.0f, 96.0f));
        InHud.TargetRoot = frame;

        InHud.TargetName = UIFactory.CreateText(frame, "Name", string.Empty, 21.0f, TextAlignmentOptions.Left);
        InHud.TargetName.fontStyle = FontStyles.Bold;
        UIFactory.Place(InHud.TargetName, new Vector2(0.0f, 1.0f), new Vector2(16.0f, -10.0f), new Vector2(330.0f, 28.0f));

        InHud.TargetLevel = UIFactory.CreateText(frame, "Level", string.Empty, 17.0f, TextAlignmentOptions.Right);
        InHud.TargetLevel.color = UIUtil.GoldColor;
        UIFactory.Place(InHud.TargetLevel, new Vector2(1.0f, 1.0f), new Vector2(-16.0f, -12.0f), new Vector2(130.0f, 26.0f));

        InHud.TargetBar = UIFactory.CreateBar(frame, "Health", UIUtil.HealthColor, UIUtil.BarBackColor);
        UIFactory.Place(InHud.TargetBar, new Vector2(0.5f, 1.0f), new Vector2(0.0f, -42.0f), new Vector2(448.0f, 20.0f));

        InHud.TargetDetail = UIFactory.CreateText(frame, "Detail", string.Empty, 15.0f, TextAlignmentOptions.Left);
        InHud.TargetDetail.textWrappingMode = TextWrappingModes.NoWrap;
        UIFactory.Place(InHud.TargetDetail, new Vector2(0.0f, 0.0f), new Vector2(16.0f, 8.0f), new Vector2(448.0f, 24.0f));
        frame.gameObject.SetActive(false);
    }

    private static void buildBoss(HudWindow InHud, RectTransform InRoot)
    {
        InHud.BossRoot = UIFactory.CreateRect(InRoot, "Boss");
        UIFactory.Place(InHud.BossRoot, new Vector2(0.5f, 1.0f), new Vector2(0.0f, -30.0f), new Vector2(720.0f, 70.0f));
        InHud.BossName = UIFactory.CreateText(InHud.BossRoot, "Name", string.Empty, 24.0f, TextAlignmentOptions.Center);
        InHud.BossName.color = new Color(1.0f, 0.35f, 0.35f);
        UIFactory.Place(InHud.BossName, new Vector2(0.5f, 1.0f), Vector2.zero, new Vector2(720.0f, 30.0f));
        InHud.BossBar = UIFactory.CreateBar(InHud.BossRoot, "Bar", new Color(0.6f, 0.05f, 0.1f), UIUtil.BarBackColor);
        UIFactory.Place(InHud.BossBar, new Vector2(0.5f, 0.0f), Vector2.zero, new Vector2(720.0f, 30.0f));
        InHud.BossRoot.gameObject.SetActive(false);
    }
}
