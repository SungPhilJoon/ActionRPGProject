using Framework.UI;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 제목 + 스크롤 목록 형태 창의 공통 부모 (제작/건축/상점).
/// 레이아웃은 프리팹(UI/Windows/*Window)에 있고, 행은 RowPrefab(UI/Widgets/ListRow)을 UIItemList 로 띄운다.
/// 하위 클래스는 Fill 에서 AddRow 로 행을 채운다.
/// </summary>
public abstract class ListWindow : UIWindow
{
    public TextMeshProUGUI Title;
    public RectTransform Content;
    public ListRowView RowPrefab;

    private UIItemList<ListRowView> Rows;

    /// <summary>창 제목 StringTable Index. 프리팹 빌더가 제목 UIText 에 넣는다.</summary>
    public abstract int TitleTextId { get; }

    protected override void OnInitialize()
    {
        Rows = new UIItemList<ListRowView>(RowPrefab, Content);
    }

    protected override void OnOpen()
    {
        Refresh();
    }

    public void Refresh()
    {
        if (Rows == null)
        {
            return;
        }

        Rows.Clear();
        Fill();
    }

    protected abstract void Fill();

    /// <summary>아이콘 | 이름+설명 | 버튼 한 줄.</summary>
    protected Button AddRow(Sprite InIcon, Color InIconColor, string InTitle, string InDetail, string InButtonLabel, UnityEngine.Events.UnityAction InOnClick, bool InInteractable)
    {
        var row = Rows.Add();
        row.Set(InIcon, InIconColor, InTitle, InDetail, InButtonLabel, InOnClick, InInteractable);
        return row.Button;
    }
}
