using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Framework.UI
{
    /// <summary>게이지 (Fill 의 anchorMax.x 로 비율 표시 — 9-slice 스프라이트도 늘어지지 않고 잘린다). 참조는 프리팹에 직렬화.</summary>
    public class UIBar : UIBehaviour
    {
        public RectTransform Fill;
        public Image FillImage;
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

        public void SetFillColor(Color InColor)
        {
            if (FillImage != null)
            {
                FillImage.color = InColor;
            }
        }
    }
}
