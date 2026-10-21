using System.Collections.Generic;
using Framework;
using Framework.UI;
using TMPro;
using UnityEngine;

/// <summary>
/// 몬스터 머리 위 체력바. 월드 좌표를 매 프레임 스크린 좌표로 옮기는 오버레이 방식(DamageNumberLayer 와 동일)이며,
/// 바 객체는 액터 ID 별로 재사용한다.
///  - 표시 조건: 살아 있는 적대 몬스터 중 (피해를 입었거나 전투 중) 이고 플레이어와 ShowDistance 이내
///  - V Blood 보스는 HUD 상단 보스 바가 따로 있으므로 제외
///  - 왼쪽에 레벨 숫자 (V Rising 처럼 기어 레벨 비교용)
/// </summary>
public class WorldHealthBarLayer : MonoBehaviour
{
    private class Entry
    {
        public RectTransform Root;
        public UIBar Bar;
        public TextMeshProUGUI Level;
        public bool Used;
    }

    private const float ShowDistance = 30.0f;
    private const float HeadHeight = 2.2f;
    private static readonly Vector2 BarSize = new Vector2(120.0f, 14.0f);
    private static readonly Vector2 BadgeSize = new Vector2(30.0f, 22.0f);
    private static readonly Color FillColor = new Color(0.8f, 0.12f, 0.12f, 1.0f);
    private static readonly Color LevelHigherColor = new Color(1.0f, 0.4f, 0.35f, 1.0f);
    private static readonly Color LevelLowerColor = new Color(0.75f, 0.75f, 0.75f, 1.0f);

    private readonly Dictionary<long, Entry> Entries = new Dictionary<long, Entry>();
    private readonly Stack<Entry> Free = new Stack<Entry>();
    private readonly List<long> RemoveBuffer = new List<long>();
    private RectTransform Rect;
    private Camera WorldCamera;

    public void Setup(Camera InCamera)
    {
        Rect = GetComponent<RectTransform>();
        WorldCamera = InCamera;
    }

    private void LateUpdate()
    {
        var player = PlayerCharacter.Local;
        var registry = Managers.Get<ActorRegistry>();
        if (WorldCamera == null || Rect == null || registry == null || player == null)
        {
            hideAll();
            return;
        }

        foreach (var pair in Entries)
        {
            pair.Value.Used = false;
        }

        var actors = registry.All;
        for (int i = 0; i < actors.Count; ++i)
        {
            var actor = actors[i];
            if (shouldShow(actor, player) == false)
            {
                continue;
            }

            Vector3 world = actor.transform.position + Vector3.up * HeadHeight * actor.transform.localScale.y;
            Vector3 screen = WorldCamera.WorldToScreenPoint(world);
            if (screen.z < 0.0f)
            {
                continue;
            }

            Entry entry;
            if (Entries.TryGetValue(actor.ActorId, out entry) == false)
            {
                entry = Free.Count > 0 ? Free.Pop() : create();
                Entries.Add(actor.ActorId, entry);
            }

            entry.Used = true;
            entry.Root.gameObject.SetActive(true);

            Vector2 local;
            RectTransformUtility.ScreenPointToLocalPointInRectangle(Rect, screen, CanvasUtil.GetEventCamera(Rect), out local);
            entry.Root.anchoredPosition = local;
            entry.Bar.SetRatio(actor.HealthRatio);

            float level = actor.Attributes.Get(EAttribute.Level);
            entry.Level.text = Mathf.RoundToInt(level).ToString();
            entry.Level.color = level > player.Attributes.Get(EAttribute.Level) ? LevelHigherColor : LevelLowerColor;
        }

        RemoveBuffer.Clear();
        foreach (var pair in Entries)
        {
            if (pair.Value.Used == false)
            {
                RemoveBuffer.Add(pair.Key);
            }
        }

        for (int i = 0; i < RemoveBuffer.Count; ++i)
        {
            release(RemoveBuffer[i]);
        }
    }

    private bool shouldShow(Actor InActor, PlayerCharacter InPlayer)
    {
        if (InActor == null || InActor.IsDead == true || InActor.IsPlayer == true)
        {
            return false;
        }

        if (InActor.Row.IsVBlood == true || InPlayer.IsHostileTo(InActor) == false)
        {
            return false;
        }

        if (MathUtil.FlatDistance(InActor.transform.position, InPlayer.transform.position) > ShowDistance)
        {
            return false;
        }

        bool damaged = InActor.HealthRatio < 0.999f;
        var monster = InActor as MonsterCharacter;
        bool inCombat = monster != null && monster.Brain != null && monster.Brain.CurrentTarget != null;
        return damaged == true || inCombat == true;
    }

    private Entry create()
    {
        var entry = new Entry();
        entry.Root = UIFactory.CreateRect(transform, "HealthBar");
        entry.Root.sizeDelta = BarSize;

        entry.Bar = UIFactory.CreateBar(entry.Root, "Bar", FillColor, UIUtil.BarBackColor);
        UIFactory.Stretch(entry.Bar.GetComponent<RectTransform>(), 0.0f);
        entry.Bar.SetLabel(string.Empty);
        entry.Bar.GetComponent<UnityEngine.UI.Image>().raycastTarget = false;
        entry.Bar.Fill.GetComponent<UnityEngine.UI.Image>().raycastTarget = false;

        // 레벨 배지: 바 왼쪽에 붙는 어두운 사각형 + 숫자.
        var badge = UIFactory.CreatePanel(entry.Root, "LevelBadge", new Color(0.0f, 0.0f, 0.0f, 0.75f));
        badge.anchorMin = new Vector2(0.0f, 0.5f);
        badge.anchorMax = new Vector2(0.0f, 0.5f);
        badge.pivot = new Vector2(1.0f, 0.5f);
        badge.anchoredPosition = new Vector2(-2.0f, 0.0f);
        badge.sizeDelta = BadgeSize;
        badge.GetComponent<UnityEngine.UI.Image>().raycastTarget = false;

        entry.Level = UIFactory.CreateText(badge, "Level", string.Empty, 18.0f, TextAlignmentOptions.Center);
        UIFactory.Stretch(entry.Level.rectTransform, 0.0f);
        entry.Level.textWrappingMode = TextWrappingModes.NoWrap;
        entry.Level.fontStyle = FontStyles.Bold;
        return entry;
    }

    private void release(long InActorId)
    {
        Entry entry;
        if (Entries.TryGetValue(InActorId, out entry) == false)
        {
            return;
        }

        Entries.Remove(InActorId);
        entry.Root.gameObject.SetActive(false);
        Free.Push(entry);
    }

    private void hideAll()
    {
        if (Entries.Count == 0)
        {
            return;
        }

        RemoveBuffer.Clear();
        RemoveBuffer.AddRange(Entries.Keys);
        for (int i = 0; i < RemoveBuffer.Count; ++i)
        {
            release(RemoveBuffer[i]);
        }
    }
}
