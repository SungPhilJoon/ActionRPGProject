using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace ActionRPG.EditorTools
{
    /// <summary>
    /// 에디터에서 uGUI 계층을 코드로 빠르게 쌓기 위한 헬퍼.
    /// 최종 아트가 들어오기 전까지 쓸 임시 UI를 만드는 용도다.
    /// </summary>
    public static class UIBuilder
    {
        public static readonly Color PanelColor = new Color(0.07f, 0.08f, 0.11f, 0.86f);
        public static readonly Color AccentColor = new Color(0.85f, 0.72f, 0.38f);
        public static readonly Color HealthColor = new Color(0.78f, 0.24f, 0.24f);
        public static readonly Color ManaColor = new Color(0.28f, 0.48f, 0.85f);

        public static Canvas CreateCanvas(string name, int sortOrder = 0)
        {
            var go = new GameObject(name, typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            var canvas = go.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = sortOrder;

            var scaler = go.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;
            return canvas;
        }

        public static RectTransform CreateRect(Transform parent, string name, Vector2 anchorMin, Vector2 anchorMax, Vector2 offsetMin, Vector2 offsetMax)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);

            var rect = go.GetComponent<RectTransform>();
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.offsetMin = offsetMin;
            rect.offsetMax = offsetMax;
            return rect;
        }

        /// <summary>앵커/피벗 대신 크기와 위치로 지정하는 간편 버전.</summary>
        public static RectTransform CreatePanel(Transform parent, string name, Vector2 anchor, Vector2 pivot, Vector2 position, Vector2 size, Color? color = null)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);

            var rect = go.GetComponent<RectTransform>();
            rect.anchorMin = anchor;
            rect.anchorMax = anchor;
            rect.pivot = pivot;
            rect.anchoredPosition = position;
            rect.sizeDelta = size;

            if (color.HasValue)
            {
                var image = go.AddComponent<Image>();
                image.color = color.Value;
            }
            return rect;
        }

        public static Image CreateImage(Transform parent, string name, Color color, Vector2 anchor, Vector2 pivot, Vector2 position, Vector2 size)
        {
            var rect = CreatePanel(parent, name, anchor, pivot, position, size);
            var image = rect.gameObject.AddComponent<Image>();
            image.color = color;
            return image;
        }

        /// <summary>가로로 채워지는 게이지. 반환값의 fillAmount 를 조절하면 된다.</summary>
        public static Image CreateBar(Transform parent, string name, Color fillColor, Vector2 anchor, Vector2 pivot, Vector2 position, Vector2 size, out Image background)
        {
            background = CreateImage(parent, name, new Color(0.1f, 0.1f, 0.12f, 0.9f), anchor, pivot, position, size);

            var fillRect = CreateRect(background.transform, "Fill", Vector2.zero, Vector2.one, new Vector2(2f, 2f), new Vector2(-2f, -2f));
            var fill = fillRect.gameObject.AddComponent<Image>();
            fill.color = fillColor;
            fill.type = Image.Type.Filled;
            fill.fillMethod = Image.FillMethod.Horizontal;
            fill.fillOrigin = (int)Image.OriginHorizontal.Left;
            fill.fillAmount = 1f;
            return fill;
        }

        public static TMP_Text CreateText(Transform parent, string name, string content, float fontSize, TextAlignmentOptions alignment,
            Vector2 anchor, Vector2 pivot, Vector2 position, Vector2 size)
        {
            var rect = CreatePanel(parent, name, anchor, pivot, position, size);
            var text = rect.gameObject.AddComponent<TextMeshProUGUI>();
            text.text = content;
            text.fontSize = fontSize;
            text.alignment = alignment;
            text.color = Color.white;
            text.raycastTarget = false;
            return text;
        }

        public static Button CreateButton(Transform parent, string name, string label, Vector2 anchor, Vector2 pivot, Vector2 position, Vector2 size, out TMP_Text labelText)
        {
            var rect = CreatePanel(parent, name, anchor, pivot, position, size, new Color(0.16f, 0.17f, 0.22f, 0.95f));
            var button = rect.gameObject.AddComponent<Button>();

            var colors = button.colors;
            colors.highlightedColor = new Color(0.26f, 0.27f, 0.34f);
            colors.pressedColor = new Color(0.12f, 0.13f, 0.17f);
            button.colors = colors;
            button.targetGraphic = rect.GetComponent<Image>();

            labelText = CreateText(rect, "Label", label, 26f, TextAlignmentOptions.Center, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, size);
            return button;
        }

        public static TMP_InputField CreateInputField(Transform parent, string name, string placeholder, Vector2 anchor, Vector2 pivot, Vector2 position, Vector2 size)
        {
            var rect = CreatePanel(parent, name, anchor, pivot, position, size, new Color(0.12f, 0.13f, 0.17f, 0.95f));
            var field = rect.gameObject.AddComponent<TMP_InputField>();

            var viewport = CreateRect(rect, "TextArea", Vector2.zero, Vector2.one, new Vector2(10f, 6f), new Vector2(-10f, -6f));
            viewport.gameObject.AddComponent<RectMask2D>();

            var text = CreateText(viewport, "Text", string.Empty, 24f, TextAlignmentOptions.Left, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, size);
            text.raycastTarget = true;

            var hint = CreateText(viewport, "Placeholder", placeholder, 24f, TextAlignmentOptions.Left, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, size);
            hint.color = new Color(1f, 1f, 1f, 0.35f);

            field.textViewport = viewport;
            field.textComponent = (TextMeshProUGUI)text;
            field.placeholder = hint;
            field.targetGraphic = rect.GetComponent<Image>();
            return field;
        }

        /// <summary>월드 스페이스 캔버스. 적 머리 위 체력바 등에 쓴다.</summary>
        public static Canvas CreateWorldCanvas(Transform parent, string name, Vector3 localPosition, Vector2 size, float scale = 0.01f)
        {
            var go = new GameObject(name, typeof(Canvas), typeof(CanvasScaler));
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPosition;
            go.transform.localScale = Vector3.one * scale;

            var canvas = go.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;

            var rect = go.GetComponent<RectTransform>();
            rect.sizeDelta = size;
            return canvas;
        }
    }
}
