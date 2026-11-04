using UnityEngine;

/// <summary>
/// 토스트 알림 한 건 (2026-11-01). 종류마다 한 클래스 — 어떻게 그릴지(Apply), 같은 알림을 합칠지(TryMerge)를 각자 안다.
///  - ItemToast: 아이템 획득 (아이콘 + 이름 + 개수 + 얻은 경로), 같은 아이템이 연달아 들어오면 개수를 합친다
///  - TextToast: 문구 알림 (퀘스트 · 해금 · 경고 …)
/// ToastWindow 가 쌓아서 보여 주고, 시간이 지나면 사라진다.
/// </summary>
public abstract class ToastMessage
{
    /// <summary>표시 시간 (초).</summary>
    public virtual float Duration
    {
        get { return 3.0f; }
    }

    public abstract void Apply(ToastItemView InView);

    /// <summary>InOther 를 이 알림에 합칠 수 있으면 합치고 true (같은 아이템 연속 획득 등).</summary>
    public virtual bool TryMerge(ToastMessage InOther)
    {
        return false;
    }
}

/// <summary>아이템 획득: "[아이콘] 목재 +3  채집".</summary>
public sealed class ItemToast : ToastMessage
{
    /// <summary>얻은 경로 문구 (EItemSource 순서). 동작 없는 단순 대응이라 표로 둔다.</summary>
    private static readonly EText[] SourceTexts =
    {
        EText.ToastAcquired, EText.ToastHarvest, EText.ToastCraft, EText.ToastShop, EText.ToastLoot, EText.ToastReward,
    };

    private readonly int ItemId;
    private readonly EItemSource Source;
    private int Count;

    public ItemToast(int InItemId, int InCount, EItemSource InSource)
    {
        ItemId = InItemId;
        Count = InCount;
        Source = InSource;
    }

    public override float Duration
    {
        get { return 3.5f; }
    }

    public override void Apply(ToastItemView InView)
    {
        ItemRow row;
        Sprite sprite = null;
        string name = string.Empty;
        if (Tables.Root.Item.TryGet(ItemId, out row) == true)
        {
            sprite = UIUtil.LoadSprite(row.Icon);
            name = Tables.Text(row.NameId);
        }

        int source = (int)Source;
        string sourceText = Tables.Text(source >= 0 && source < SourceTexts.Length ? SourceTexts[source] : EText.ToastAcquired);
        InView.Show(sprite, $"<b><color=#F2CC66>{name}</color></b>  +{Count}   <size=80%><color=#B8B8B8>{sourceText}</color></size>", UIUtil.GoldColor);
    }

    public override bool TryMerge(ToastMessage InOther)
    {
        var other = InOther as ItemToast;
        if (other == null || other.ItemId != ItemId || other.Source != Source)
        {
            return false;
        }

        Count += other.Count;
        return true;
    }
}

/// <summary>문구 알림: StringTable 문구 + 덧붙일 말, 색.</summary>
public sealed class TextToast : ToastMessage
{
    private readonly int TextId;
    private readonly string Arg;
    private readonly Color Color;

    public TextToast(int InTextId, string InArg, Color InColor)
    {
        TextId = InTextId;
        Arg = InArg;
        Color = InColor.a > 0.0f ? InColor : Color.white;
    }

    public override void Apply(ToastItemView InView)
    {
        string text = Tables.Text(TextId);
        InView.Show(null, string.IsNullOrEmpty(Arg) == true ? text : $"{text} {Arg}", Color);
    }

    /// <summary>똑같은 문구가 곧바로 또 오면 하나로 (재료 부족 연타 등).</summary>
    public override bool TryMerge(ToastMessage InOther)
    {
        var other = InOther as TextToast;
        return other != null && other.TextId == TextId && other.Arg == Arg;
    }
}
