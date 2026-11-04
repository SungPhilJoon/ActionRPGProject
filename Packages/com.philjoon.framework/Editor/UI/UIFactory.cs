using TMPro;
using UnityEditor.Events;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace Framework.UI
{
    /// <summary>
    /// uGUI 조립 함수 모음 (에디터 전용, Factory 패턴). UI 프리팹 빌더가 이 함수로 계층을 만들고 프리팹으로 저장한다.
    /// 런타임은 이 클래스를 쓰지 않는다 — 런타임은 프리팹을 Addressables 로 로드해 Instantiate 할 뿐이다.
    /// <see cref="Skin"/> 이 지정돼 있으면 판넬 · 버튼 · 슬롯 · 게이지에 스킨 스프라이트(9-slice)와 틴트를 쓴다.
    /// 모든 함수는 RectTransform / 컴포넌트를 돌려주므로 Place/Stretch 와 체이닝해서 쓴다.
    /// </summary>
    public static class UIFactory
    {
        public static readonly Vector2 ReferenceResolution = new Vector2(1920.0f, 1080.0f);

        public static Color PanelColor = new Color(0.06f, 0.05f, 0.07f, 0.88f);
        public static Color FrameColor = new Color(0.45f, 0.08f, 0.10f, 1.0f);
        public static Color ButtonColor = new Color(0.22f, 0.07f, 0.09f, 1.0f);
        public static Color TextColor = new Color(0.92f, 0.88f, 0.84f, 1.0f);

        /// <summary>빌더가 굽기 전에 지정한다. null 이면 단색 판넬.</summary>
        public static UISkin Skin;

        public static Canvas CreateCanvas(string InName, int InSortOrder, Transform InParent = null)
        {
            var go = new GameObject(InName, typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            go.layer = 5;
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
            go.layer = 5;
            var rect = go.GetComponent<RectTransform>();
            if (InParent != null)
            {
                rect.SetParent(InParent, false);
            }

            return rect;
        }

        /// <summary>프리팹 루트 (화면 전체로 늘어나는 빈 RectTransform).</summary>
        public static RectTransform CreateRoot(string InName)
        {
            var rect = CreateRect(null, InName);
            Stretch(rect, 0.0f);
            return rect;
        }

        public static RectTransform CreatePanel(Transform InParent, string InName, Color InColor)
        {
            var rect = CreateRect(InParent, InName);
            var image = rect.gameObject.AddComponent<Image>();
            image.color = InColor;
            return rect;
        }

        /// <summary>스킨 스프라이트 판넬 (9-slice). 스프라이트가 없으면 InFallback 단색.</summary>
        public static RectTransform CreateSkinPanel(Transform InParent, string InName, Sprite InSprite, Color InTint, Color InFallback)
        {
            var rect = CreatePanel(InParent, InName, InSprite != null ? InTint : InFallback);
            if (InSprite != null)
            {
                var image = rect.GetComponent<Image>();
                image.sprite = InSprite;
                image.type = Image.Type.Sliced;
                image.pixelsPerUnitMultiplier = 1.0f;
            }

            return rect;
        }

        /// <summary>창 배경 (스킨 Panel, 없으면 단색 + 테두리).</summary>
        public static RectTransform CreateWindowFrame(Transform InParent, string InName)
        {
            if (Skin != null && Skin.Panel != null)
            {
                return CreateSkinPanel(InParent, InName, Skin.Panel, Skin.PanelTint, PanelColor);
            }

            var rect = CreatePanel(InParent, InName, PanelColor);
            var outline = rect.gameObject.AddComponent<Outline>();
            outline.effectColor = FrameColor;
            outline.effectDistance = new Vector2(2.0f, -2.0f);
            return rect;
        }

        /// <summary>안쪽 판넬 (목록 바탕, 행 배경 등).</summary>
        public static RectTransform CreateInset(Transform InParent, string InName, Color InFallback)
        {
            return CreateSkinPanel(InParent, InName, Skin != null ? Skin.PanelInset : null, Skin != null ? Skin.InsetTint : InFallback, InFallback);
        }

        /// <summary>아이템 · 어빌리티 칸 배경.</summary>
        public static RectTransform CreateSlot(Transform InParent, string InName, Color InFallback)
        {
            return CreateSkinPanel(InParent, InName, Skin != null ? Skin.Slot : null, Skin != null ? Skin.SlotTint : InFallback, InFallback);
        }

        public static TextMeshProUGUI CreateText(Transform InParent, string InName, string InText, float InSize, TextAlignmentOptions InAlign)
        {
            var rect = CreateRect(InParent, InName);
            var text = rect.gameObject.AddComponent<TextMeshProUGUI>();
            if (Skin != null && Skin.Font != null)
            {
                text.font = Skin.Font;
            }

            text.text = InText;
            text.fontSize = InSize;
            text.alignment = InAlign;
            text.color = Skin != null ? Skin.TextColor : TextColor;
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

        /// <summary>버튼. InOnClick 은 프리팹에 저장되는 영구 리스너로 연결된다 (대상은 public 메서드여야 함, 인스펙터에서 보임).</summary>
        public static Button CreateButton(Transform InParent, string InName, string InLabel, float InFontSize, UnityAction InOnClick)
        {
            bool skinned = Skin != null && Skin.Button != null;
            var rect = skinned == true ? CreateSkinPanel(InParent, InName, Skin.Button, Skin.ButtonTint, ButtonColor) : CreatePanel(InParent, InName, ButtonColor);
            var button = rect.gameObject.AddComponent<Button>();
            var colors = button.colors;
            colors.normalColor = Color.white;
            colors.highlightedColor = new Color(1.35f, 1.25f, 1.25f, 1.0f);
            colors.pressedColor = new Color(0.75f, 0.7f, 0.7f, 1.0f);
            colors.disabledColor = new Color(0.45f, 0.42f, 0.42f, 0.8f);
            colors.colorMultiplier = 1.5f;
            button.colors = colors;
            button.targetGraphic = rect.GetComponent<Image>();
            if (skinned == true && Skin.ButtonPressed != null)
            {
                button.transition = Selectable.Transition.ColorTint;
                var state = button.spriteState;
                state.pressedSprite = Skin.ButtonPressed;
                button.spriteState = state;
            }

            if (string.IsNullOrEmpty(InLabel) == false)
            {
                var label = CreateText(rect, "Label", InLabel, InFontSize, TextAlignmentOptions.Center);
                Stretch(label.rectTransform, 4.0f);
            }

            AddClick(button, InOnClick);
            return button;
        }

        /// <summary>영구(직렬화) 클릭 리스너. 런타임 AddListener 와 달리 프리팹에 남는다.</summary>
        public static void AddClick(Button InButton, UnityAction InOnClick)
        {
            if (InOnClick != null)
            {
                UnityEventTools.AddPersistentListener(InButton.onClick, InOnClick);
            }
        }

        public static UIBar CreateBar(Transform InParent, string InName, Color InFillColor, Color InBackColor)
        {
            bool skinned = Skin != null && Skin.BarBack != null;
            var back = skinned == true ? CreateSkinPanel(InParent, InName, Skin.BarBack, Skin.BarBackTint, InBackColor) : CreatePanel(InParent, InName, InBackColor);
            var fillArea = CreateRect(back, "FillArea");
            Stretch(fillArea, skinned == true ? 3.0f : 0.0f);
            var fill = skinned == true && Skin.BarFill != null ? CreateSkinPanel(fillArea, "Fill", Skin.BarFill, InFillColor, InFillColor) : CreatePanel(fillArea, "Fill", InFillColor);
            Stretch(fill, 0.0f);
            var bar = back.gameObject.AddComponent<UIBar>();
            bar.Fill = fill;
            bar.FillImage = fill.GetComponent<Image>();
            var label = CreateText(back, "Value", string.Empty, 18.0f, TextAlignmentOptions.Center);
            Stretch(label.rectTransform, 0.0f);
            bar.Label = label;
            return bar;
        }

        public static TMP_InputField CreateInputField(Transform InParent, string InName, string InPlaceholder, float InFontSize)
        {
            var rect = CreateInset(InParent, InName, new Color(0.0f, 0.0f, 0.0f, 0.7f));
            var viewport = CreateRect(rect, "Viewport");
            Stretch(viewport, 8.0f);
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
    }
}
