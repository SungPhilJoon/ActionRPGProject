using TMPro;
using UnityEngine;

namespace Framework.UI
{
    /// <summary>스프라이트 없이 동작하는 게이지 (Fill 의 anchorMax.x 로 비율 표시).</summary>
    public class UIBar : MonoBehaviour
    {
        public RectTransform Fill;
        public TextMeshProUGUI Label;

        public void SetRatio(float InRatio)
        {
            if (Fill == null)
            {
                return;
            }

            var max = Fill.anchorMax;
            max.x = Mathf.Clamp01(InRatio);
            Fill.anchorMax = max;
        }

        public void SetValue(float InCurrent, float InMax)
        {
            SetRatio(InMax > 0.0f ? InCurrent / InMax : 0.0f);
            if (Label != null)
            {
                Label.text = $"{Mathf.CeilToInt(InCurrent)} / {Mathf.CeilToInt(InMax)}";
            }
        }

        public void SetLabel(string InText)
        {
            if (Label != null)
            {
                Label.text = InText;
            }
        }
    }
}
