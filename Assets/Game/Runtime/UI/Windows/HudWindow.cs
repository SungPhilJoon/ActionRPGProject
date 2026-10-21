using System.Collections.Generic;
using Framework;
using Framework.UI;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 인게임 HUD: 체력/보호막, 혈액, 어빌리티 바(쿨다운), 낮밤 시계, 햇빛 경고, 보스 체력, 상호작용 프롬프트, 토스트, 사망 오버레이.
/// 데이터는 EventBus 구독 + 매 프레임 폴링(쿨다운처럼 연속 값)으로 갱신한다.
/// </summary>
public class HudWindow : UIWindow
{
    private class AbilitySlotView
    {
        public RectTransform Root;
        public Image Background;
        public Image Icon;
        public RectTransform CooldownMask;
        public TextMeshProUGUI CooldownText;
        public TextMeshProUGUI Name;
        public TextMeshProUGUI Key;
    }

    private static readonly EInputAction[] SlotKeys =
    {
        EInputAction.Primary, EInputAction.WeaponQ, EInputAction.WeaponE, EInputAction.Travel,
        EInputAction.Spell1, EInputAction.Spell2, EInputAction.Ultimate, EInputAction.Feed,
    };

    private UIBar HealthBar;
    private UIBar BloodBar;
    private TextMeshProUGUI BloodLabel;
    private TextMeshProUGUI GearLabel;
    private TextMeshProUGUI ClockLabel;
    private UIBar ClockBar;
    private TextMeshProUGUI SunLabel;
    private RectTransform BossRoot;
    private UIBar BossBar;
    private TextMeshProUGUI BossName;
    private TextMeshProUGUI Prompt;
    private TextMeshProUGUI Toast;
    private float ToastRemaining;
    private RectTransform DeathRoot;
    private TextMeshProUGUI DeathText;
    private readonly List<AbilitySlotView> Slots = new List<AbilitySlotView>();

    private Actor Boss;
    private IInteractable Interactable;

    public override bool BlocksGameInput
    {
        get { return false; }
    }

    protected override void Build()
    {
        buildVitals();
        buildAbilityBar();
        buildClock();
        buildBoss();

        Prompt = UIFactory.CreateText(Rect, "Prompt", string.Empty, 24.0f, TextAlignmentOptions.Center);
        UIFactory.Place(Prompt, new Vector2(0.5f, 0.0f), new Vector2(0.0f, 200.0f), new Vector2(700.0f, 40.0f));

        SunLabel = UIFactory.CreateText(Rect, "Sun", string.Empty, 30.0f, TextAlignmentOptions.Center);
        SunLabel.color = new Color(1.0f, 0.75f, 0.3f);
        UIFactory.Place(SunLabel, new Vector2(0.5f, 1.0f), new Vector2(0.0f, -150.0f), new Vector2(700.0f, 50.0f));

        Toast = UIFactory.CreateText(Rect, "Toast", string.Empty, 30.0f, TextAlignmentOptions.Center);
        UIFactory.Place(Toast, new Vector2(0.5f, 0.5f), new Vector2(0.0f, 220.0f), new Vector2(1100.0f, 60.0f));

        var hints = UIFactory.CreateText(Rect, "Hints", string.Empty, 16.0f, TextAlignmentOptions.BottomRight);
        hints.color = new Color(1.0f, 1.0f, 1.0f, 0.55f);
        hints.text = $"[{UIUtil.KeyLabel(EInputAction.Inventory)}] {Tables.Text(EText.Inventory)}   [{UIUtil.KeyLabel(EInputAction.Craft)}] {Tables.Text(EText.Crafting)}   " +
                     $"[{UIUtil.KeyLabel(EInputAction.Build)}] {Tables.Text(EText.Build)}   [{UIUtil.KeyLabel(EInputAction.Shop)}] {Tables.Text(EText.Shop)}   " +
                     $"[{UIUtil.KeyLabel(EInputAction.Cheat)}] {Tables.Text(EText.CheatTitle)}   [{UIUtil.KeyLabel(EInputAction.BalanceOverlay)}] {Tables.Text(EText.BalanceTitle)}";
        UIFactory.Place(hints, new Vector2(1.0f, 0.0f), new Vector2(-20.0f, 12.0f), new Vector2(1100.0f, 30.0f));

        DeathRoot = UIFactory.CreatePanel(Rect, "Death", new Color(0.25f, 0.0f, 0.0f, 0.45f));
        UIFactory.Stretch(DeathRoot, 0.0f);
        DeathText = UIFactory.CreateText(DeathRoot, "Text", string.Empty, 56.0f, TextAlignmentOptions.Center);
        UIFactory.Stretch(DeathText.rectTransform, 0.0f);
        DeathRoot.gameObject.SetActive(false);
    }

    private void buildVitals()
    {
        var root = UIFactory.CreateWindowFrame(Rect, "Vitals");
        UIFactory.Place(root, new Vector2(0.0f, 0.0f), new Vector2(20.0f, 20.0f), new Vector2(420.0f, 130.0f));

        HealthBar = UIFactory.CreateBar(root, "Health", UIUtil.HealthColor, UIUtil.BarBackColor);
        UIFactory.Place(HealthBar, new Vector2(0.0f, 1.0f), new Vector2(12.0f, -12.0f), new Vector2(396.0f, 34.0f));

        BloodBar = UIFactory.CreateBar(root, "Blood", UIUtil.BloodColor, UIUtil.BarBackColor);
        UIFactory.Place(BloodBar, new Vector2(0.0f, 1.0f), new Vector2(12.0f, -52.0f), new Vector2(396.0f, 22.0f));

        BloodLabel = UIFactory.CreateText(root, "BloodType", string.Empty, 18.0f, TextAlignmentOptions.Left);
        UIFactory.Place(BloodLabel, new Vector2(0.0f, 0.0f), new Vector2(14.0f, 12.0f), new Vector2(260.0f, 40.0f));

        GearLabel = UIFactory.CreateText(root, "Gear", string.Empty, 18.0f, TextAlignmentOptions.Right);
        GearLabel.color = UIUtil.GoldColor;
        UIFactory.Place(GearLabel, new Vector2(1.0f, 0.0f), new Vector2(-14.0f, 12.0f), new Vector2(150.0f, 40.0f));
    }

    private void buildAbilityBar()
    {
        var bar = UIFactory.CreateRect(Rect, "AbilityBar");
        UIFactory.Place(bar, new Vector2(0.5f, 0.0f), new Vector2(0.0f, 20.0f), new Vector2(8 * 96.0f, 110.0f));
        UIFactory.AddHorizontal(bar, 8.0f, 0);

        for (int i = 0; i < SlotKeys.Length; ++i)
        {
            var view = new AbilitySlotView();
            view.Root = UIFactory.CreatePanel(bar, $"Slot{i}", UIUtil.SlotColor);
            UIFactory.SetPreferred(view.Root, 88.0f, 88.0f);
            view.Background = view.Root.GetComponent<Image>();
            var outline = view.Root.gameObject.AddComponent<Outline>();
            outline.effectColor = UIFactory.FrameColor;

            view.Icon = UIFactory.CreateImage(view.Root, "Icon", Color.white);
            UIFactory.Stretch(view.Icon.rectTransform, 8.0f);
            view.Icon.enabled = false;

            view.Name = UIFactory.CreateText(view.Root, "Name", string.Empty, 13.0f, TextAlignmentOptions.Center);
            UIFactory.Stretch(view.Name.rectTransform, 4.0f);

            view.CooldownMask = UIFactory.CreatePanel(view.Root, "Cooldown", new Color(0.0f, 0.0f, 0.0f, 0.7f));
            view.CooldownMask.anchorMin = Vector2.zero;
            view.CooldownMask.anchorMax = new Vector2(1.0f, 0.0f);
            view.CooldownMask.offsetMin = Vector2.zero;
            view.CooldownMask.offsetMax = Vector2.zero;
            view.CooldownMask.GetComponent<Image>().raycastTarget = false;

            view.CooldownText = UIFactory.CreateText(view.Root, "CooldownText", string.Empty, 26.0f, TextAlignmentOptions.Center);
            UIFactory.Stretch(view.CooldownText.rectTransform, 0.0f);

            view.Key = UIFactory.CreateText(view.Root, "Key", UIUtil.KeyLabel(SlotKeys[i]), 15.0f, TextAlignmentOptions.TopLeft);
            view.Key.color = UIUtil.GoldColor;
            UIFactory.Stretch(view.Key.rectTransform, 4.0f);
            Slots.Add(view);
        }
    }

    private void buildClock()
    {
        var root = UIFactory.CreateWindowFrame(Rect, "Clock");
        UIFactory.Place(root, new Vector2(1.0f, 1.0f), new Vector2(-20.0f, -20.0f), new Vector2(260.0f, 70.0f));
        ClockLabel = UIFactory.CreateText(root, "Label", string.Empty, 20.0f, TextAlignmentOptions.Center);
        UIFactory.Place(ClockLabel, new Vector2(0.5f, 1.0f), new Vector2(0.0f, -6.0f), new Vector2(240.0f, 30.0f));
        ClockBar = UIFactory.CreateBar(root, "Progress", new Color(0.95f, 0.75f, 0.35f), UIUtil.BarBackColor);
        UIFactory.Place(ClockBar, new Vector2(0.5f, 0.0f), new Vector2(0.0f, 10.0f), new Vector2(236.0f, 14.0f));
    }

    private void buildBoss()
    {
        BossRoot = UIFactory.CreateRect(Rect, "Boss");
        UIFactory.Place(BossRoot, new Vector2(0.5f, 1.0f), new Vector2(0.0f, -30.0f), new Vector2(720.0f, 70.0f));
        BossName = UIFactory.CreateText(BossRoot, "Name", string.Empty, 24.0f, TextAlignmentOptions.Center);
        BossName.color = new Color(1.0f, 0.35f, 0.35f);
        UIFactory.Place(BossName, new Vector2(0.5f, 1.0f), Vector2.zero, new Vector2(720.0f, 30.0f));
        BossBar = UIFactory.CreateBar(BossRoot, "Bar", new Color(0.6f, 0.05f, 0.1f), UIUtil.BarBackColor);
        UIFactory.Place(BossBar, new Vector2(0.5f, 0.0f), Vector2.zero, new Vector2(720.0f, 28.0f));
        BossRoot.gameObject.SetActive(false);
    }

    private void OnEnable()
    {
        EventBus<AbilitySlotsChangedEvent>.Subscribe(onSlotsChanged);
        EventBus<NotifyEvent>.Subscribe(onNotify);
        EventBus<BossEngagedEvent>.Subscribe(onBoss);
        EventBus<InteractTargetChangedEvent>.Subscribe(onInteractTarget);
        EventBus<BloodChangedEvent>.Subscribe(onBlood);
        EventBus<PlayerSpawnedEvent>.Subscribe(onPlayerSpawned);
    }

    private void OnDisable()
    {
        EventBus<AbilitySlotsChangedEvent>.Unsubscribe(onSlotsChanged);
        EventBus<NotifyEvent>.Unsubscribe(onNotify);
        EventBus<BossEngagedEvent>.Unsubscribe(onBoss);
        EventBus<InteractTargetChangedEvent>.Unsubscribe(onInteractTarget);
        EventBus<BloodChangedEvent>.Unsubscribe(onBlood);
        EventBus<PlayerSpawnedEvent>.Unsubscribe(onPlayerSpawned);
    }

    protected override void OnOpen()
    {
        // 플레이어 스폰 이벤트는 HUD 가 열리기 전에 발행되므로 열릴 때 한 번 동기화한다.
        var player = PlayerCharacter.Local;
        if (player != null)
        {
            onPlayerSpawned(new PlayerSpawnedEvent { Player = player });
        }
    }

    private void Update()
    {
        var player = PlayerCharacter.Local;
        if (player == null)
        {
            return;
        }

        var attributes = player.Attributes;
        float health = attributes.Get(EAttribute.Health);
        float shield = attributes.Get(EAttribute.Shield);
        HealthBar.SetValue(health, attributes.Get(EAttribute.MaxHealth));
        if (shield > 0.5f)
        {
            HealthBar.SetLabel($"{Mathf.CeilToInt(health)} / {Mathf.CeilToInt(attributes.Get(EAttribute.MaxHealth))}  <color=#9CC8F0>+{Mathf.CeilToInt(shield)}</color>");
        }

        BloodBar.SetValue(attributes.Get(EAttribute.BloodPool), attributes.Get(EAttribute.MaxBloodPool));
        GearLabel.text = $"{Tables.Text(EText.GearLevel)} {attributes.Get(EAttribute.Level):0}";

        updateSlots(player);
        updateClock(player);
        updateBoss();

        if (ToastRemaining > 0.0f)
        {
            ToastRemaining -= Time.deltaTime;
            var color = Toast.color;
            color.a = Mathf.Clamp01(ToastRemaining);
            Toast.color = color;
        }

        DeathRoot.gameObject.SetActive(player.IsDead);
        if (player.IsDead == true)
        {
            DeathText.text = $"{Tables.Text(EText.YouDied)}\n<size=28>{Tables.Text(EText.Respawning)} {Mathf.CeilToInt(player.RespawnRemaining)}</size>";
        }

        if (Interactable != null)
        {
            Prompt.text = $"[{UIUtil.KeyLabel(EInputAction.Interact)}] {Tables.Text(Interactable.PromptTextId)}";
        }
        else
        {
            var feed = player.GetSlot(EAbilitySlot.Feed) as FeedAbility;
            bool canFeed = feed != null && feed.IsActive == false && feed.HasVictimInRange() == true;
            Prompt.text = canFeed == true ? $"[{UIUtil.KeyLabel(EInputAction.Feed)}] {Tables.Text(EText.PressToFeed)}" : string.Empty;
        }
    }

    private void updateSlots(PlayerCharacter InPlayer)
    {
        for (int i = 0; i < Slots.Count; ++i)
        {
            var view = Slots[i];
            var ability = InPlayer.GetSlot((EAbilitySlot)i);
            if (ability == null)
            {
                view.CooldownMask.anchorMax = new Vector2(1.0f, 0.0f);
                view.CooldownText.text = string.Empty;
                continue;
            }

            float remaining = InPlayer.Asc.GetCooldownRemaining(ability.AbilityId);
            float duration = InPlayer.Asc.GetCooldownDuration(ability.AbilityId);
            float ratio = duration > 0.0f ? Mathf.Clamp01(remaining / duration) : 0.0f;
            view.CooldownMask.anchorMax = new Vector2(1.0f, ratio);
            view.CooldownText.text = remaining > 0.05f ? (remaining >= 1.0f ? Mathf.CeilToInt(remaining).ToString() : remaining.ToString("0.0")) : string.Empty;
            view.Background.color = ability.IsActive == true ? Color.Lerp(ability.Row.IconColor, Color.white, 0.35f) : ability.Row.IconColor * 0.75f;
        }
    }

    private void refreshSlotVisuals(PlayerCharacter InPlayer)
    {
        for (int i = 0; i < Slots.Count; ++i)
        {
            var view = Slots[i];
            var ability = InPlayer.GetSlot((EAbilitySlot)i);
            if (ability == null)
            {
                view.Icon.enabled = false;
                view.Name.text = string.Empty;
                view.Background.color = UIUtil.SlotColor;
                continue;
            }

            var sprite = UIUtil.LoadSprite(ability.Row.Icon);
            view.Icon.enabled = sprite != null;
            view.Icon.sprite = sprite;
            view.Name.text = sprite == null ? Tables.Text(ability.Row.NameId) : string.Empty;
            view.Background.color = ability.Row.IconColor * 0.75f;
        }
    }

    private void updateClock(PlayerCharacter InPlayer)
    {
        var dayNight = Managers.Get<DayNightManager>();
        if (dayNight == null)
        {
            return;
        }

        int hour = Mathf.FloorToInt(dayNight.Hour);
        int minute = Mathf.FloorToInt((dayNight.Hour - hour) * 60.0f);
        ClockLabel.text = $"{(dayNight.IsDay == true ? Tables.Text(EText.Day) : Tables.Text(EText.Night))}  {hour:00}:{minute:00}";
        ClockBar.SetRatio(dayNight.PhaseProgress);
        ClockBar.Fill.GetComponent<Image>().color = dayNight.IsDay == true ? new Color(0.95f, 0.75f, 0.35f) : new Color(0.35f, 0.45f, 0.85f);

        if (InPlayer.Sun.IsExposed == true)
        {
            SunLabel.text = $"{Tables.Text(EText.InSunlight)}  {Mathf.RoundToInt(InPlayer.Sun.Exposure01 * 100.0f)}%";
        }
        else
        {
            SunLabel.text = string.Empty;
        }
    }

    private void updateBoss()
    {
        bool show = Boss != null && Boss.IsDead == false;
        BossRoot.gameObject.SetActive(show);
        if (show == true)
        {
            BossBar.SetValue(Boss.Attributes.Get(EAttribute.Health), Boss.Attributes.Get(EAttribute.MaxHealth));
        }
    }

    private void onSlotsChanged(AbilitySlotsChangedEvent InEvent)
    {
        if (InEvent.Actor == PlayerCharacter.Local && InEvent.Actor != null)
        {
            refreshSlotVisuals(PlayerCharacter.Local);
        }
    }

    private void onPlayerSpawned(PlayerSpawnedEvent InEvent)
    {
        refreshSlotVisuals(InEvent.Player);
        onBlood(new BloodChangedEvent { BloodType = InEvent.Player.Blood.BloodType, Quality = InEvent.Player.Blood.Quality });
    }

    private void onNotify(NotifyEvent InEvent)
    {
        string text = Tables.Text(InEvent.TextId);
        Toast.text = string.IsNullOrEmpty(InEvent.Arg) == true ? text : $"{text} {InEvent.Arg}";
        Toast.color = InEvent.Color.a > 0.0f ? InEvent.Color : Color.white;
        ToastRemaining = 3.0f;
    }

    private void onBoss(BossEngagedEvent InEvent)
    {
        Boss = InEvent.Engaged == true ? InEvent.Boss : null;
        if (Boss != null)
        {
            BossName.text = Tables.Text(Boss.Row.NameId);
        }
    }

    private void onInteractTarget(InteractTargetChangedEvent InEvent)
    {
        Interactable = InEvent.Target;
    }

    private void onBlood(BloodChangedEvent InEvent)
    {
        BloodTypeRow row;
        if (InEvent.BloodType == EBloodType.None || Tables.Root.BloodType.TryGet((int)InEvent.BloodType, out row) == false)
        {
            BloodLabel.text = Tables.Text(EText.BloodTypeNone);
            BloodBar.Fill.GetComponent<Image>().color = UIUtil.BloodColor;
            return;
        }

        BloodLabel.text = $"<color=#{ColorUtility.ToHtmlStringRGB(row.Color)}>{Tables.Text(row.NameId)}</color> {Tables.Text(EText.Quality)} {InEvent.Quality:0}%";
        BloodBar.Fill.GetComponent<Image>().color = row.Color * 0.8f;
    }
}
