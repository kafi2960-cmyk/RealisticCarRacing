using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace RealisticCarRacing.UI
{
    /// <summary>Shared look for all game UI.</summary>
    public static class UiTheme
    {
        public static readonly Color Panel = new Color(0.04f, 0.05f, 0.07f, 0.9f);
        public static readonly Color PanelLight = new Color(0.11f, 0.12f, 0.15f, 0.92f);
        public static readonly Color ButtonHover = new Color(0.22f, 0.24f, 0.3f, 1f);
        public static readonly Color Accent = new Color(0.9f, 0.18f, 0.1f, 1f);
        public static readonly Color AccentDark = new Color(0.55f, 0.1f, 0.06f, 1f);
        public static readonly Color Text = new Color(0.96f, 0.96f, 0.96f, 1f);
        public static readonly Color TextDim = new Color(0.66f, 0.68f, 0.73f, 1f);
        public static readonly Color Gold = new Color(1f, 0.78f, 0.2f, 1f);
        public static readonly Color Good = new Color(0.35f, 0.85f, 0.45f, 1f);

        static Font font;
        public static Font Font => font != null ? font : font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
    }

    /// <summary>
    /// Small toolkit for building uGUI screens in code (canvas, panels, text, buttons, layouts, bars, ring gauges),
    /// so every menu and HUD shares one style and works with mouse, touch, keyboard and gamepad navigation.
    /// </summary>
    public static class UiFactory
    {
        static Sprite white;
        static Sprite ring;
        static Sprite disc;

        /// <summary>Plain white sprite (needed for filled images).</summary>
        public static Sprite White => white != null ? white : white = MakeSprite(4, (x, y) => 1f);

        /// <summary>Anti-aliased ring for gauges.</summary>
        public static Sprite Ring => ring != null ? ring : ring = MakeSprite(256, (x, y) =>
        {
            float r = Mathf.Sqrt(x * x + y * y);
            return Mathf.Clamp01((1f - r) * 128f) * Mathf.Clamp01((r - 0.8f) * 128f);
        });

        /// <summary>Anti-aliased filled circle.</summary>
        public static Sprite Disc => disc != null ? disc : disc = MakeSprite(256, (x, y) => Mathf.Clamp01((1f - Mathf.Sqrt(x * x + y * y)) * 128f));

        public static Canvas Canvas(string name, int sortingOrder)
        {
            var go = new GameObject(name, typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            var canvas = go.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = sortingOrder;
            var scaler = go.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;
            return canvas;
        }

        public static RectTransform Rect(string name, Transform parent)
        {
            var rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            return rect;
        }

        /// <summary>Anchors and sizes a rect. Size is added to the anchored area (use zero with stretched anchors).</summary>
        public static RectTransform Place(this RectTransform rect, Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot, Vector2 position, Vector2 size)
        {
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.pivot = pivot;
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
            return rect;
        }

        public static RectTransform Stretch(this RectTransform rect, float margin = 0f)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = new Vector2(margin, margin);
            rect.offsetMax = new Vector2(-margin, -margin);
            return rect;
        }

        public static Image Image(Transform parent, string name, Color color, Sprite sprite = null)
        {
            var rect = Rect(name, parent);
            var image = rect.gameObject.AddComponent<Image>();
            image.sprite = sprite;
            image.color = color;
            image.raycastTarget = false;
            return image;
        }

        public static Text Text(Transform parent, string content, int size, Color color, TextAnchor anchor = TextAnchor.MiddleLeft,
            FontStyle style = FontStyle.Normal, string name = "Text")
        {
            var rect = Rect(name, parent).Stretch();
            var text = rect.gameObject.AddComponent<Text>();
            text.font = UiTheme.Font;
            text.text = content;
            text.fontSize = size;
            text.fontStyle = style;
            text.color = color;
            text.alignment = anchor;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            text.raycastTarget = false;
            return text;
        }

        /// <summary>Themed button: dark when idle, lighter on hover, accent-red when selected by keyboard/gamepad.</summary>
        public static Button Button(Transform parent, string label, UnityAction onClick, int fontSize = 32, float height = 84f, string name = null)
        {
            var image = Image(parent, name ?? $"Button {label}", Color.white, White);
            image.raycastTarget = true;
            var button = image.gameObject.AddComponent<Button>();
            button.targetGraphic = image;
            button.colors = new ColorBlock
            {
                normalColor = UiTheme.PanelLight,
                highlightedColor = UiTheme.ButtonHover,
                selectedColor = UiTheme.Accent,
                pressedColor = UiTheme.AccentDark,
                disabledColor = new Color(0.1f, 0.1f, 0.1f, 0.5f),
                colorMultiplier = 1f,
                fadeDuration = 0.08f
            };
            if (onClick != null)
                button.onClick.AddListener(onClick);
            var text = Text(image.transform, label, fontSize, UiTheme.Text, TextAnchor.MiddleCenter, FontStyle.Bold, "Label");
            text.rectTransform.offsetMin = new Vector2(16f, 0f);
            text.rectTransform.offsetMax = new Vector2(-16f, 0f);
            Size(button, height: height);
            return button;
        }

        public static VerticalLayoutGroup Column(RectTransform rect, float spacing, int padding = 0, TextAnchor alignment = TextAnchor.UpperLeft)
        {
            var layout = rect.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.spacing = spacing;
            layout.padding = new RectOffset(padding, padding, padding, padding);
            layout.childAlignment = alignment;
            layout.childControlWidth = layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;
            return layout;
        }

        public static HorizontalLayoutGroup Row(RectTransform rect, float spacing, TextAnchor alignment = TextAnchor.MiddleLeft)
        {
            var layout = rect.gameObject.AddComponent<HorizontalLayoutGroup>();
            layout.spacing = spacing;
            layout.childAlignment = alignment;
            layout.childControlWidth = layout.childControlHeight = true;
            layout.childForceExpandWidth = false;
            layout.childForceExpandHeight = true;
            return layout;
        }

        public static LayoutElement Size(Component target, float width = -1f, float height = -1f, float flexibleWidth = -1f)
        {
            if (!target.TryGetComponent(out LayoutElement element))
                element = target.gameObject.AddComponent<LayoutElement>();
            element.preferredWidth = width;
            element.preferredHeight = height;
            element.flexibleWidth = flexibleWidth;
            return element;
        }

        /// <summary>A label with a horizontal 0..1 bar; returns the fill image.</summary>
        public static Image StatBar(Transform parent, string label)
        {
            var row = Rect($"Stat {label}", parent);
            Row(row, 16f);
            Size(row, height: 34f);
            Size(Text(row, label, 22, UiTheme.TextDim, TextAnchor.MiddleLeft, FontStyle.Bold), width: 170f);
            var track = Image(row, "Bar", new Color(1f, 1f, 1f, 0.12f), White);
            Size(track, height: 14f, flexibleWidth: 1f);
            var fill = Image(track.transform, "Fill", UiTheme.Accent, White);
            fill.rectTransform.Stretch();
            fill.type = UnityEngine.UI.Image.Type.Filled;
            fill.fillMethod = UnityEngine.UI.Image.FillMethod.Horizontal;
            return fill;
        }

        static Sprite MakeSprite(int size, System.Func<float, float, float> alpha)
        {
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, name = "UiFactory" };
            var pixels = new Color32[size * size];
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float u = (x + 0.5f) / size * 2f - 1f, v = (y + 0.5f) / size * 2f - 1f;
                pixels[y * size + x] = new Color32(255, 255, 255, (byte)(Mathf.Clamp01(alpha(u, v)) * 255f));
            }
            texture.SetPixels32(pixels);
            texture.Apply(false, true);
            return Sprite.Create(texture, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f);
        }
    }
}
