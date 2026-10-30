using Framework;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

/// <summary>목록 창(제작/건축/상점)의 한 줄 위젯 (프리팹 UI/Widgets/ListRow). 아이콘 | 이름 + 설명 | 버튼.</summary>
public class ListRowView : UIBehaviour
{
    public Image Icon;
    public TextMeshProUGUI Title;
    public TextMeshProUGUI Detail;
    public Button Button;
    public TextMeshProUGUI ButtonLabel;

    public void Set(Sprite InIcon, Color InIconColor, string InTitle, string InDetail, string InButtonLabel, UnityAction InOnClick, bool InInteractable)
    {
        Icon.sprite = InIcon;
        Icon.color = InIcon != null ? Color.white : InIconColor;
        Icon.preserveAspect = InIcon != null;
        Title.text = InTitle;
        Detail.text = InDetail;
        ButtonLabel.text = InButtonLabel;

        // 재사용되는 행이라 이전 행의 (코드) 리스너를 지운다. 프리팹에 저장된 영구 리스너는 남는다.
        Button.onClick.RemoveAllListeners();
        if (InOnClick != null)
        {
            Button.onClick.AddListener(InOnClick);
        }

        Button.interactable = InInteractable;
    }
}
