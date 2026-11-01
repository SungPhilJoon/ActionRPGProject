using System.Text;
using Framework;
using Framework.UI;
using TMPro;
using UnityEngine;

/// <summary>
/// 인게임 HUD: 체력/보호막, 혈액, 어빌리티 바(쿨다운), 낮밤 시계, 햇빛 경고, 보스 체력, 퀘스트 추적기, 상호작용 프롬프트, 토스트, 사망 · 클리어 오버레이.
/// 데이터는 EventBus 구독 + 매 프레임 폴링(쿨다운처럼 연속 값)으로 갱신한다. 레이아웃은 프리팹 UI/Windows/HudWindow.
/// </summary>
public class HudWindow : UIWindow
{
    /// <summary>어빌리티 칸 순서 = EAbilitySlot 순서. 키 라벨 표시에 쓴다 (프리팹 빌더도 같은 개수로 칸을 만든다).</summary>
    public static readonly EInputAction[] SlotKeys =
    {
        EInputAction.Primary, EInputAction.WeaponQ, EInputAction.WeaponE, EInputAction.Travel,
        EInputAction.Spell1, EInputAction.Spell2, EInputAction.Ultimate, EInputAction.Feed,
    };

    [Header("Vitals")]
    public UIBar HealthBar;
    public UIBar BloodBar;
    public TextMeshProUGUI BloodLabel;
    public TextMeshProUGUI GearLabel;

    [Header("Clock / Sun")]
    public TextMeshProUGUI ClockLabel;
    public UIBar ClockBar;
    public TextMeshProUGUI SunLabel;

    [Header("Target (중상단, 지정한 대상 상태)")]
    public RectTransform TargetRoot;
    public TextMeshProUGUI TargetName;
    public TextMeshProUGUI TargetLevel;
    public UIBar TargetBar;
    public TextMeshProUGUI TargetDetail;

    [Header("Boss")]
    public RectTransform BossRoot;
    public UIBar BossBar;
    public TextMeshProUGUI BossName;

    [Header("Messages")]
    public TextMeshProUGUI Prompt;
    public TextMeshProUGUI Toast;
    public TextMeshProUGUI Hints;
    public RectTransform DeathRoot;
    public TextMeshProUGUI DeathText;

    [Header("Quest")]
    public RectTransform QuestRoot;
    public TextMeshProUGUI QuestChapter;
    public TextMeshProUGUI QuestTitle;
    public TextMeshProUGUI QuestDesc;
    public TextMeshProUGUI QuestProgress;
    public RectTransform VictoryRoot;
    public TextMeshProUGUI VictoryText;

    [Header("Ability Bar (SlotKeys 순서)")]
    public AbilitySlotView[] Slots = new AbilitySlotView[0];

    private const float VictorySeconds = 10.0f;

    private float ToastRemaining;
    private float VictoryRemaining;
    private string QuestProgressText = string.Empty;
    private readonly StringBuilder TargetText = new StringBuilder();
    private Actor Boss;
    private IInteractable Interactable;

    public override bool BlocksGameInput
    {
        get { return false; }
    }

    /// <summary>키 배치는 InputTable.csv 라 실행 중에 정해진다 → 키 라벨과 안내 문구만 여기서 채운다.</summary>
    protected override void OnInitialize()
    {
        for (int i = 0; i < Slots.Length && i < SlotKeys.Length; ++i)
        {
            Slots[i].Key.text = UIUtil.KeyLabel(SlotKeys[i]);
        }

        refreshHints();
        DeathRoot.gameObject.SetActive(false);
        BossRoot.gameObject.SetActive(false);
        VictoryRoot.gameObject.SetActive(false);
        QuestRoot.gameObject.SetActive(false);
        TargetRoot.gameObject.SetActive(false);
    }

    private void refreshHints()
    {
        Hints.text = $"[{UIUtil.KeyLabel(EInputAction.Inventory)}] {Tables.Text(EText.Inventory)}   [{UIUtil.KeyLabel(EInputAction.Craft)}] {Tables.Text(EText.Crafting)}   " +
                     $"[{UIUtil.KeyLabel(EInputAction.Build)}] {Tables.Text(EText.Build)}   [{UIUtil.KeyLabel(EInputAction.Shop)}] {Tables.Text(EText.Shop)}   " +
                     $"[{UIUtil.KeyLabel(EInputAction.WorldMap)}] {Tables.Text(EText.WorldMap)}   " +
                     $"[{UIUtil.KeyLabel(EInputAction.Cheat)}] {Tables.Text(EText.CheatTitle)}   [{UIUtil.KeyLabel(EInputAction.BalanceOverlay)}] {Tables.Text(EText.BalanceTitle)}";
    }

    private void OnEnable()
    {
        EventBus<LanguageChangedEvent>.Subscribe(onLanguageChanged);
        EventBus<AbilitySlotsChangedEvent>.Subscribe(onSlotsChanged);
        EventBus<NotifyEvent>.Subscribe(onNotify);
        EventBus<BossEngagedEvent>.Subscribe(onBoss);
        EventBus<InteractTargetChangedEvent>.Subscribe(onInteractTarget);
        EventBus<BloodChangedEvent>.Subscribe(onBlood);
        EventBus<PlayerSpawnedEvent>.Subscribe(onPlayerSpawned);
        EventBus<QuestChangedEvent>.Subscribe(onQuestChanged);
        EventBus<QuestsCompletedEvent>.Subscribe(onQuestsCompleted);
    }

    private void OnDisable()
    {
        EventBus<LanguageChangedEvent>.Unsubscribe(onLanguageChanged);
        EventBus<AbilitySlotsChangedEvent>.Unsubscribe(onSlotsChanged);
        EventBus<NotifyEvent>.Unsubscribe(onNotify);
        EventBus<BossEngagedEvent>.Unsubscribe(onBoss);
        EventBus<InteractTargetChangedEvent>.Unsubscribe(onInteractTarget);
        EventBus<BloodChangedEvent>.Unsubscribe(onBlood);
        EventBus<PlayerSpawnedEvent>.Unsubscribe(onPlayerSpawned);
        EventBus<QuestChangedEvent>.Unsubscribe(onQuestChanged);
        EventBus<QuestsCompletedEvent>.Unsubscribe(onQuestsCompleted);
    }

    protected override void OnOpen()
    {
        // 플레이어 스폰 이벤트는 HUD 가 열리기 전에 발행되므로 열릴 때 한 번 동기화한다.
        var player = PlayerCharacter.Local;
        if (player != null)
        {
            onPlayerSpawned(new PlayerSpawnedEvent { Player = player });
        }

        var quests = Managers.Get<QuestManager>();
        if (quests != null)
        {
            refreshQuest(quests.Current, quests.Progress, quests.IsCompleted, quests.IsAllCompleted);
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
        updateTarget(player);
        updateQuest(player);

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
        for (int i = 0; i < Slots.Length; ++i)
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
        for (int i = 0; i < Slots.Length; ++i)
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
        ClockBar.SetFillColor(dayNight.IsDay == true ? new Color(0.95f, 0.75f, 0.35f) : new Color(0.35f, 0.45f, 0.85f));

        if (InPlayer.Sun.IsExposed == true)
        {
            SunLabel.text = $"{Tables.Text(EText.InSunlight)}  {Mathf.RoundToInt(InPlayer.Sun.Exposure01 * 100.0f)}%";
        }
        else
        {
            SunLabel.text = string.Empty;
        }
    }

    /// <summary>지정한 대상(TargetSelector.Selected) 상태: 몬스터 = 이름 · 레벨 · 체력 · 혈액 · 상태이상, 채집 노드 = 이름 · 내구도 · 획득물 · 재생 시간.</summary>
    private void updateTarget(PlayerCharacter InPlayer)
    {
        var selector = Managers.Get<TargetSelector>();
        var target = selector != null ? selector.Selected : null;
        bool show = target != null;
        if (TargetRoot.gameObject.activeSelf != show)
        {
            TargetRoot.gameObject.SetActive(show);
        }

        if (show == false)
        {
            return;
        }

        var monster = target as MonsterCharacter;
        if (monster != null)
        {
            showMonster(InPlayer, monster);
            return;
        }

        var node = target as ResourceNode;
        if (node != null)
        {
            showNode(node);
        }
    }

    private void showMonster(PlayerCharacter InPlayer, MonsterCharacter InMonster)
    {
        var row = InMonster.Row;
        TargetName.text = Tables.Text(row.NameId);
        TargetName.color = row.IsVBlood == true ? new Color(1.0f, 0.35f, 0.35f) : Color.white;
        bool stronger = row.Level > InPlayer.Attributes.Get(EAttribute.Level) + 0.5f;
        TargetLevel.text = $"{Tables.Text(EText.TargetLevel)} {row.Level:0}";
        TargetLevel.color = stronger == true ? new Color(1.0f, 0.4f, 0.35f) : UIUtil.GoldColor;
        TargetBar.SetValue(InMonster.Attributes.Get(EAttribute.Health), InMonster.Attributes.Get(EAttribute.MaxHealth));
        TargetBar.SetFillColor(UIUtil.HealthColor);

        TargetText.Clear();
        BloodTypeRow blood;
        if (row.BloodType != EBloodType.None && Tables.Root.BloodType.TryGet((int)row.BloodType, out blood) == true)
        {
            TargetText.Append($"<color=#{ColorUtility.ToHtmlStringRGB(blood.Color)}>{Tables.Text(blood.NameId)}</color> {row.BloodQuality:0}%");
        }

        if (row.IsVBlood == true)
        {
            TargetText.Append($"  <color=#FF5555>{Tables.Text(EText.TargetVBlood)}</color>");
        }

        UIUtil.AppendStatuses(InMonster, TargetText);
        TargetDetail.text = TargetText.ToString();
    }

    private void showNode(ResourceNode InNode)
    {
        var row = InNode.Row;
        TargetName.text = Tables.Text(row.NameId);
        TargetName.color = Color.white;
        TargetLevel.text = Tables.Text(EText.TargetDurability);
        TargetLevel.color = new Color(0.6f, 0.9f, 1.0f);
        TargetBar.SetValue(InNode.IsDepleted == true ? 0.0f : InNode.Health, row.Health);
        TargetBar.SetFillColor(TargetSelector.NodeColor * 0.8f);
        TargetDetail.text = InNode.IsDepleted == true
            ? $"{Tables.Text(EText.TargetDepleted)} {Mathf.CeilToInt(InNode.RespawnRemaining)}s"
            : $"{Tables.Text(EText.TargetYield)} {UIUtil.ItemName(row.DropItem)} {row.DropMin}~{row.DropMax}";
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

    /// <summary>진행도 줄 + 퀘스트 장소까지 걸어갈 거리 (지도 경로와 같은 값). 거리는 매 프레임 바뀐다.</summary>
    private void updateQuest(PlayerCharacter InPlayer)
    {
        if (VictoryRemaining > 0.0f)
        {
            VictoryRemaining -= Time.deltaTime;
            VictoryRoot.gameObject.SetActive(VictoryRemaining > 0.0f);
        }

        // 남은 거리 = 길 안내 경로 길이 (QuestNavigator, 직선이 아니라 걸어갈 길).
        var navigator = Managers.Get<QuestNavigator>();
        if (navigator == null || navigator.HasGuide == false)
        {
            QuestProgress.text = QuestProgressText;
            return;
        }

        float distance = navigator.Distance;

        QuestProgress.text = $"{QuestProgressText}   <color=#B0B0B0>{Tables.Text(EText.QuestDistance)} {Mathf.RoundToInt(distance)}m</color>";
    }

    private void refreshQuest(QuestRow InQuest, int InProgress, bool InCompleted, bool InAllCompleted)
    {
        if (InQuest == null)
        {
            QuestRoot.gameObject.SetActive(InAllCompleted);
            QuestChapter.text = Tables.Text(EText.Quest);
            QuestTitle.text = Tables.Text(EText.QuestAllComplete);
            QuestDesc.text = string.Empty;
            QuestProgressText = string.Empty;
            QuestProgress.text = string.Empty;
            return;
        }

        QuestRoot.gameObject.SetActive(true);
        QuestChapter.text = $"{Tables.Text(EText.Quest)} - {Tables.Text(UIUtil.QuestChapterText(InQuest.Chapter))}";
        QuestTitle.text = InQuest.Key == EInputAction.Count ? Tables.Text(InQuest.TitleId) : $"<color=#F2CC66>[{UIUtil.KeyLabel(InQuest.Key)}]</color> {Tables.Text(InQuest.TitleId)}";
        string reward = InQuest.RewardItem != 0 && InQuest.RewardCount > 0 ? $"\n<color=#B0B0B0>{Tables.Text(EText.QuestReward)} {UIUtil.ItemName(InQuest.RewardItem)} x{InQuest.RewardCount}</color>" : string.Empty;
        QuestDesc.text = Tables.Text(InQuest.DescId) + reward;
        int goal = Mathf.Max(1, InQuest.Count);
        QuestProgressText = InCompleted == true ? $"<color=#8CF28C>{Tables.Text(EText.QuestComplete)}</color>" : $"{InProgress} / {goal}";
        QuestProgress.text = QuestProgressText;
    }

    private void onQuestChanged(QuestChangedEvent InEvent)
    {
        var quests = Managers.Get<QuestManager>();
        refreshQuest(InEvent.Quest, InEvent.Progress, InEvent.Completed, quests != null && quests.IsAllCompleted == true);
    }

    private void onQuestsCompleted(QuestsCompletedEvent InEvent)
    {
        VictoryText.text = Tables.Text(EText.QuestVictory);
        VictoryRemaining = VictorySeconds;
        VictoryRoot.gameObject.SetActive(true);
    }

    private void onSlotsChanged(AbilitySlotsChangedEvent InEvent)
    {
        if (InEvent.Actor == PlayerCharacter.Local && InEvent.Actor != null)
        {
            refreshSlotVisuals(PlayerCharacter.Local);
        }
    }

    /// <summary>언어가 바뀌면 캐시한 문구(안내 · 퀘스트 · 혈액 · 보스 이름 · 어빌리티 이름)를 다시 채운다. 매 프레임 갱신하는 문구는 그대로 따라온다.</summary>
    private void onLanguageChanged(LanguageChangedEvent InEvent)
    {
        refreshHints();
        if (Boss != null)
        {
            BossName.text = Tables.Text(Boss.Row.NameId);
        }

        var quests = Managers.Get<QuestManager>();
        if (quests != null)
        {
            refreshQuest(quests.Current, quests.Progress, quests.IsCompleted, quests.IsAllCompleted);
        }

        if (PlayerCharacter.Local != null)
        {
            onPlayerSpawned(new PlayerSpawnedEvent { Player = PlayerCharacter.Local });
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
            BloodBar.SetFillColor(UIUtil.BloodColor);
            return;
        }

        BloodLabel.text = $"<color=#{ColorUtility.ToHtmlStringRGB(row.Color)}>{Tables.Text(row.NameId)}</color> {Tables.Text(EText.Quality)} {InEvent.Quality:0}%";
        BloodBar.SetFillColor(row.Color * 0.8f);
    }
}
