using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>토스트 한 줄 위젯 (배경 + 아이콘 + 문구). 페이드는 CanvasGroup 알파. 레이아웃은 UIPrefabBuilder.buildToastWindow.</summary>
public class ToastItemView : Framework.UIBehaviour
{
    public Image Background;
    public Image Icon;
    public TextMeshProUGUI Text;
    public CanvasGroup Group;
    /// <summary>왼쪽 강조 띠 (알림 색).</summary>
    public Image Accent;

    public void Show(Sprite InIcon, string InText, Color InAccent)
    {
        Icon.enabled = InIcon != null;
        Icon.sprite = InIcon;
        Icon.preserveAspect = true;
        Text.text = InText;
        Accent.color = InAccent;
    }

    public void SetAlpha(float InAlpha)
    {
        Group.alpha = InAlpha;
    }
}
