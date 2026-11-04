using TMPro;
using UnityEngine;

namespace Framework.UI
{
    /// <summary>
    /// UI 스킨 (스프라이트 + 색). 에디터의 UI 프리팹 빌더가 프리팹을 구울 때 읽는다 — 런타임은 이 에셋을 모른다
    /// (프리팹이 스프라이트를 직접 참조). 텍스처를 바꾸려면 이 에셋의 스프라이트를 바꾸고 프리팹을 다시 굽는다.
    /// 스프라이트는 9-slice(Sprite Border) 로 임포트해야 크기가 달라도 테두리가 늘어나지 않는다.
    /// </summary>
    [CreateAssetMenu(menuName = "Framework/UI Skin", fileName = "UISkin")]
    public class UISkin : ScriptableObject
    {
        [Header("Sprites (9-slice)")]
        public Sprite Panel;
        public Sprite PanelInset;
        public Sprite Button;
        public Sprite ButtonPressed;
        public Sprite Slot;
        public Sprite BarBack;
        public Sprite BarFill;
        public Sprite Divider;
        public Sprite CloseIcon;

        [Header("Tint (스프라이트에 곱해지는 색)")]
        public Color PanelTint = new Color(0.62f, 0.36f, 0.34f, 0.97f);
        public Color InsetTint = new Color(0.36f, 0.24f, 0.25f, 0.95f);
        public Color ButtonTint = new Color(0.72f, 0.22f, 0.22f, 1.0f);
        public Color SlotTint = new Color(0.44f, 0.31f, 0.32f, 1.0f);
        public Color BarBackTint = new Color(0.26f, 0.17f, 0.18f, 1.0f);

        [Header("Text")]
        public TMP_FontAsset Font;
        public Color TextColor = new Color(0.92f, 0.88f, 0.84f, 1.0f);
        public Color TitleColor = new Color(0.95f, 0.78f, 0.55f, 1.0f);
        public Color AccentColor = new Color(0.85f, 0.12f, 0.15f, 1.0f);
    }
}
