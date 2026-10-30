using Framework;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>HUD 어빌리티 칸 1개 (HudWindow 프리팹 안에 8개가 미리 배치되어 있다).</summary>
public class AbilitySlotView : UIBehaviour
{
    public Image Background;
    public Image Icon;
    public RectTransform CooldownMask;
    public TextMeshProUGUI CooldownText;
    public TextMeshProUGUI Name;
    public TextMeshProUGUI Key;
}
