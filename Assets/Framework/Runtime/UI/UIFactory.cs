using System;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace Framework.UI
{
    /// <summary>
    /// 코드로 uGUI 를 조립하는 팩토리 (Factory 패턴).
    /// 프리팹 없이 창/HUD 를 만들 수 있어 UI 수정이 "코드 한 곳" 에서 끝나고,
    /// 에디터의 UI Factory 툴(UIFactoryWindow)도 같은 함수를 호출해 프리팹을 굽는다.
    /// 모든 함수는 RectTransform 을 돌려주므로 Anchor 유틸과 체이닝해서 쓴다.
    /// </summary>
    public static class UIFactory
    {
        public static readonly Vector2 ReferenceResolution = new Vector2(1920.0f, 1080.0f);

        public static Color PanelColor = new Color(0.06f, 0.05f, 0.07f, 0.88f);
        public static Color FrameColor = new Color(0.45f, 0.08f, 0.10f, 1.0f);
        public static Color ButtonColor = new Color(0.22f, 0.07f, 0.09f, 1.0f);
        public static Color ButtonHighlight = new Color(0.45f, 0.12f, 0.14f, 1.0f);
        public static Color TextColor = new Color(0.92f, 0.88f, 0.84f, 1.0f);

        public static Canvas CreateCanvas(string InName, int InSortOrder, Transform InParent = null)
        {
            var go = new GameObject(InName, typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            if (InParent != null)
            {
                go.transform.SetParent(InParent, false);
            }

            var canvas = go.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = InSortOrder;

            var scaler = go.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = ReferenceResolution;
            scaler.matchWidthOrHeight = 0.5f;
            return canvas;
        }

        public static RectTransform CreateRect(Transform InParent, string InName)
        {
            var go = new GameObject(InName, typeof(RectTransform));
            go.layer = InParent != null ? InParent.gameObject.layer : 5;
            var rect = go.GetComponent<RectTransform>();
            rect.SetParent(InParent, false);
            return rect;
        }

        public static RectTransform CreatePanel(Transform InParent, string InName, Color InColor)
        {
            var rect = CreateRect(InParent, InName);
            var image = rect.gameObject.AddComponent<Image>();
            image.color = InColor;
            return rect;
        }

        /// <summary>테두리(Outline) 있는 기본 창 배경.</summary>
        public static RectTransform CreateWindowFrame(Transform InParent, string InName)
        {
            var rect = CreatePanel(InParent, InName, PanelColor);
            var outline = rect.gameObject.AddComponent<Outline>();
            outline.effectColor = FrameColor;
            outline.effectDistance = new Vector2(2.0f, -2.0f);
            return rect;
        }

        public static TextMeshProUGUI CreateText(Transform InParent, string InName, string InText, float InSize, TextAlignmentOptions InAlign)
        {
            var rect = CreateRect(InParent, InName);
            var text = rect.gameObject.AddComponent<TextMeshProUGUI>();
            text.text = InText;
            text.fontSize = InSize;
            text.alignment = InAlign;
            text.color = TextColor;
            text.raycastTarget = false;
            text.textWrappingMode = TextWrappingModes.Normal;
            return text;
        }

        public static Image CreateImage(Transform InParent, string InName, Color InColor, Sprite InSprite = null)
        {
            var rect = CreateRect(InParent, InName);
            var image = rect.gameObject.AddComponent<Image>();
            image.color = InColor;
            image.sprite = InSprite;
            image.preserveAspect = InSprite != null;
            return image;
        }

        public static Button CreateButton(Transform InParent, string InName, string InLabel, float InFontSize, UnityAction InOnClick)
        {
            var rect = CreatePanel(InParent, InName, ButtonColor);
            var button = rect.gameObject.AddComponent<Button>();
            var colors = button.colors;
            colors.normalColor = Color.white;
            colors.highlightedColor = new Color(1.4f, 1.3f, 1.3f, 1.0f);
            colors.pressedColor = new Color(0.7f, 0.7f, 0.7f, 1.0f);
            colors.colorMultiplier = 1.5f;
            button.colors = colors;
            button.targetGraphic = rect.GetComponent<Image>();

            if (string.IsNullOrEmpty(InLabel) == false)
            {
                var label = CreateText(rect, "Label", InLabel, InFontSize, TextAlignmentOptions.Center);
                Stretch(label.rectTransform, 4.0f);
            }

            if (InOnClick != null)
            {
                button.onClick.AddListener(InOnClick);
            }

            return button;
        }

        public static UIBar CreateBar(Transform InParent, string InName, Color InFillColor, Color InBackColor)
        {
            var back = CreatePanel(InParent, InName, InBackColor);
            var fill = CreatePanel(back, "Fill", InFillColor);
            Stretch(fill, 0.0f);
            var bar = back.gameObject.AddComponent<UIBar>();
            bar.Fill = fill;
            var label = CreateText(back, "Value", string.Empty, 18.0f, TextAlignmentOptions.Center);
            Stretch(label.rectTransform, 0.0f);
            bar.Label = label;
            return bar;
        }

        public static TMP_InputField CreateInputField(Transform InParent, string InName, string InPlaceholder, float InFontSize)
        {
            var rect = CreatePanel(InParent, InName, new Color(0.0f, 0.0f, 0.0f, 0.7f));
            var viewport = CreateRect(rect, "Viewport");
            Stretch(viewport, 6.0f);
            viewport.gameObject.AddComponent<RectMask2D>();

            var text = CreateText(viewport, "Text", string.Empty, InFontSize, TextAlignmentOptions.Left);
            text.textWrappingMode = TextWrappingModes.NoWrap;
            Stretch(text.rectTransform, 0.0f);

            var placeholder = CreateText(viewport, "Placeholder", InPlaceholder, InFontSize, TextAlignmentOptions.Left);
            placeholder.color = new Color(1.0f, 1.0f, 1.0f, 0.35f);
            placeholder.fontStyle = FontStyles.Italic;
            Stretch(placeholder.rectTransform, 0.0f);

            var input = rect.gameObject.AddComponent<TMP_InputField>();
            input.textViewport = viewport;
            input.textComponent = text;
            input.placeholder = placeholder;
            input.fontAsset = text.font;
            input.pointSize = InFontSize;
            return input;
        }

        public static GridLayoutGroup AddGrid(RectTransform InRect, Vector2 InCellSize, Vector2 InSpacing, int InColumns)
        {
            var grid = InRect.gameObject.AddComponent<GridLayoutGroup>();
            grid.cellSize = InCellSize;
            grid.spacing = InSpacing;
            grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            grid.constraintCount = InColumns;
            grid.padding = new RectOffset(8, 8, 8, 8);
            return grid;
        }

        public static VerticalLayoutGroup AddVertical(RectTransform InRect, float InSpacing, int InPadding)
        {
            var layout = InRect.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.spacing = InSpacing;
            layout.padding = new RectOffset(InPadding, InPadding, InPadding, InPadding);
            layout.childControlHeight = false;
            layout.childControlWidth = true;
            layout.childForceExpandHeight = false;
            layout.childForceExpandWidth = true;
            return layout;
        }

        public static HorizontalLayoutGroup AddHorizontal(RectTransform InRect, float InSpacing, int InPadding)
        {
            var layout = InRect.gameObject.AddComponent<HorizontalLayoutGroup>();
            layout.spacing = InSpacing;
            layout.padding = new RectOffset(InPadding, InPadding, InPadding, InPadding);
            layout.childControlHeight = true;
            layout.childControlWidth = false;
            layout.childForceExpandHeight = true;
            layout.childForceExpandWidth = false;
            layout.childAlignment = TextAnchor.MiddleCenter;
            return layout;
        }

        public static LayoutElement SetPreferred(Component InTarget, float InWidth, float InHeight)
        {
            var element = ComponentUtil.GetOrAdd<LayoutElement>(InTarget.gameObject);
            if (InWidth > 0.0f)
            {
                element.preferredWidth = InWidth;
            }

            if (InHeight > 0.0f)
            {
                element.preferredHeight = InHeight;
            }

            return element;
        }

        // ------------------------------------------------------------------ anchor helpers

        public static RectTransform Stretch(RectTransform InRect, float InPadding)
        {
            InRect.anchorMin = Vector2.zero;
            InRect.anchorMax = Vector2.one;
            InRect.pivot = new Vector2(0.5f, 0.5f);
            InRect.offsetMin = new Vector2(InPadding, InPadding);
            InRect.offsetMax = new Vector2(-InPadding, -InPadding);
            return InRect;
        }

        /// <summary>InAnchor (0~1) 한 점에 고정. InPivot 도 같은 값을 쓰면 화면 모서리 정렬이 쉽다.</summary>
        public static RectTransform Place(RectTransform InRect, Vector2 InAnchor, Vector2 InPosition, Vector2 InSize)
        {
            InRect.anchorMin = InAnchor;
            InRect.anchorMax = InAnchor;
            InRect.pivot = InAnchor;
            InRect.anchoredPosition = InPosition;
            InRect.sizeDelta = InSize;
            return InRect;
        }

        public static RectTransform Place(Component InComponent, Vector2 InAnchor, Vector2 InPosition, Vector2 InSize)
        {
            return Place(InComponent.GetComponent<RectTransform>(), InAnchor, InPosition, InSize);
        }

        public static void ClearChildren(Transform InParent)
        {
            for (int i = InParent.childCount - 1; i >= 0; --i)
            {
                var child = InParent.GetChild(i).gameObject;
                child.SetActive(false);
                UnityEngine.Object.Destroy(child);
            }
        }
    }
}
