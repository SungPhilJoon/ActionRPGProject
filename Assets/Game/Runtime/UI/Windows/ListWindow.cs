using Framework.UI;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 제목 + 스크롤 목록 형태 창의 공통 부모 (제작/건축/상점).
/// 하위 클래스는 Refresh 에서 AddRow 로 행을 채운다.
/// </summary>
public abstract class ListWindow : UIWindow
{
    protected RectTransform Content;
    protected TextMeshProUGUI Title;

    protected abstract int TitleTextId { get; }

    protected override void Build()
    {
        var frame = UIFactory.CreateWindowFrame(Rect, "Frame");
        UIFactory.Place(frame, new Vector2(0.5f, 0.5f), new Vector2(0.0f, 20.0f), new Vector2(860.0f, 620.0f));

        Title = UIFactory.CreateText(frame, "Title", Tables.Text(TitleTextId), 30.0f, TextAlignmentOptions.Left);
        UIFactory.Place(Title, new Vector2(0.0f, 1.0f), new Vector2(24.0f, -12.0f), new Vector2(600.0f, 44.0f));

        var close = UIFactory.CreateButton(frame, "Close", Tables.Text(EText.Close), 18.0f, Close);
        UIFactory.Place(close, new Vector2(1.0f, 1.0f), new Vector2(-16.0f, -14.0f), new Vector2(110.0f, 38.0f));

        var viewport = UIFactory.CreateRect(frame, "Viewport");
        UIFactory.Place(viewport, new Vector2(0.5f, 0.0f), new Vector2(0.0f, 16.0f), new Vector2(828.0f, 530.0f));
        viewport.gameObject.AddComponent<RectMask2D>();
        viewport.gameObject.AddComponent<Image>().color = new Color(0.0f, 0.0f, 0.0f, 0.01f);

        Content = UIFactory.CreateRect(viewport, "Content");
        Content.anchorMin = new Vector2(0.0f, 1.0f);
        Content.anchorMax = new Vector2(1.0f, 1.0f);
        Content.pivot = new Vector2(0.5f, 1.0f);
        Content.offsetMin = Vector2.zero;
        Content.offsetMax = Vector2.zero;
        UIFactory.AddVertical(Content, 6.0f, 4);
        var fitter = Content.gameObject.AddComponent<ContentSizeFitter>();
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        var scroll = viewport.gameObject.AddComponent<ScrollRect>();
        scroll.content = Content;
        scroll.horizontal = false;
        scroll.viewport = viewport;
        scroll.scrollSensitivity = 30.0f;
    }

    protected override void OnOpen()
    {
        Refresh();
    }

    public void Refresh()
    {
        UIFactory.ClearChildren(Content);
        Fill();
    }

    protected abstract void Fill();

    /// <summary>아이콘 | 이름+설명 | 버튼 한 줄.</summary>
    protected Button AddRow(Sprite InIcon, Color InIconColor, string InTitle, string InDetail, string InButtonLabel, UnityEngine.Events.UnityAction InOnClick, bool InInteractable)
    {
        var row = UIFactory.CreatePanel(Content, "Row", UIUtil.SlotColor);
        UIFactory.SetPreferred(row, 0.0f, 84.0f);

        var icon = UIFactory.CreateImage(row, "Icon", InIcon != null ? Color.white : InIconColor, InIcon);
        UIFactory.Place(icon, new Vector2(0.0f, 0.5f), new Vector2(10.0f, 0.0f), new Vector2(68.0f, 68.0f));

        var title = UIFactory.CreateText(row, "Title", InTitle, 22.0f, TextAlignmentOptions.TopLeft);
        UIFactory.Place(title, new Vector2(0.0f, 1.0f), new Vector2(92.0f, -6.0f), new Vector2(560.0f, 30.0f));

        var detail = UIFactory.CreateText(row, "Detail", InDetail, 16.0f, TextAlignmentOptions.TopLeft);
        UIFactory.Place(detail, new Vector2(0.0f, 1.0f), new Vector2(92.0f, -38.0f), new Vector2(560.0f, 44.0f));

        var button = UIFactory.CreateButton(row, "Action", InButtonLabel, 18.0f, InOnClick);
        UIFactory.Place(button, new Vector2(1.0f, 0.5f), new Vector2(-12.0f, 0.0f), new Vector2(130.0f, 48.0f));
        button.interactable = InInteractable;
        return button;
    }
}
