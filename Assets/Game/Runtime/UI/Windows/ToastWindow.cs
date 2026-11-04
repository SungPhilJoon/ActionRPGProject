using System.Collections.Generic;
using Framework;
using Framework.UI;
using UnityEngine;

/// <summary>
/// 토스트 알림 창 (Toast 레이어, 오른쪽 아래에 위로 쌓임, 2026-11-01).
///  - 아이템을 얻으면(ItemObtainedEvent: 채집 · 제작 · 상점 · 전리품 · 보상) ItemToast, 그 밖의 알림(NotifyEvent)은 TextToast.
///  - 같은 알림이 아직 떠 있으면 새 줄 대신 합친다 (ToastMessage.TryMerge — 목재 +2 → +5) 그리고 시간을 다시 센다.
///  - 들어올 때 FadeIn, 사라질 때 FadeOut. 최대 MaxCount 줄 (넘치면 가장 오래된 것부터).
/// 예전에는 HUD 가운데 글자 한 줄을 덮어써서, 연달아 얻은 아이템이 앞의 알림을 지웠다.
/// </summary>
public class ToastWindow : UIWindow
{
    private const int MaxCount = 6;
    private const float FadeIn = 0.15f;
    private const float FadeOut = 0.5f;

    /// <summary>토스트 줄의 부모 (VerticalLayoutGroup, 아래 정렬).</summary>
    public RectTransform Stack;
    /// <summary>줄 복제 원본 (비활성 자식).</summary>
    public ToastItemView Template;

    private class Entry
    {
        public ToastMessage Message;
        public ToastItemView View;
        public float Elapsed;
    }

    private readonly List<Entry> Entries = new List<Entry>();
    private readonly List<ToastItemView> Pool = new List<ToastItemView>();

    public override UILayer GetLayer()
    {
        return UILayer.Toast;
    }

    /// <summary>지금 떠 있는 토스트 수 (테스트용).</summary>
    public int Count
    {
        get { return Entries.Count; }
    }

    private void OnEnable()
    {
        EventBus<ItemObtainedEvent>.Subscribe(onItemObtained);
        EventBus<NotifyEvent>.Subscribe(onNotify);
    }

    private void OnDisable()
    {
        EventBus<ItemObtainedEvent>.Unsubscribe(onItemObtained);
        EventBus<NotifyEvent>.Unsubscribe(onNotify);
    }

    /// <summary>토스트를 띄운다 (떠 있는 같은 알림이 있으면 합친다).</summary>
    public void Show(ToastMessage InMessage)
    {
        for (int i = Entries.Count - 1; i >= 0; --i)
        {
            var entry = Entries[i];
            if (entry.Message.TryMerge(InMessage) == true)
            {
                entry.Message.Apply(entry.View);
                entry.Elapsed = Mathf.Min(entry.Elapsed, FadeIn);
                return;
            }
        }

        if (Entries.Count >= MaxCount)
        {
            remove(0);
        }

        var view = rent();
        view.transform.SetAsLastSibling();
        InMessage.Apply(view);
        view.SetAlpha(0.0f);
        Entries.Add(new Entry { Message = InMessage, View = view, Elapsed = 0.0f });
    }

    private void Update()
    {
        float deltaTime = Time.unscaledDeltaTime;
        for (int i = Entries.Count - 1; i >= 0; --i)
        {
            var entry = Entries[i];
            entry.Elapsed += deltaTime;
            float duration = entry.Message.Duration;
            if (entry.Elapsed >= duration)
            {
                remove(i);
                continue;
            }

            float fadeIn = Mathf.Clamp01(entry.Elapsed / FadeIn);
            float fadeOut = Mathf.Clamp01((duration - entry.Elapsed) / FadeOut);
            entry.View.SetAlpha(Mathf.Min(fadeIn, fadeOut));
        }
    }

    private void onItemObtained(ItemObtainedEvent InEvent)
    {
        if (InEvent.Count > 0 && InEvent.Player == PlayerCharacter.Local)
        {
            Show(new ItemToast(InEvent.ItemId, InEvent.Count, InEvent.Source));
        }
    }

    private void onNotify(NotifyEvent InEvent)
    {
        Show(new TextToast(InEvent.TextId, InEvent.Arg, InEvent.Color));
    }

    private ToastItemView rent()
    {
        for (int i = 0; i < Pool.Count; ++i)
        {
            if (Pool[i].gameObject.activeSelf == false)
            {
                Pool[i].gameObject.SetActive(true);
                return Pool[i];
            }
        }

        var view = Instantiate(Template, Stack, false);
        view.name = Template.name;
        view.gameObject.SetActive(true);
        Pool.Add(view);
        return view;
    }

    private void remove(int InIndex)
    {
        Entries[InIndex].View.gameObject.SetActive(false);
        Entries.RemoveAt(InIndex);
    }
}
