using System;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace ShinyMinds.Menu
{
    /// <summary>
    /// Small factory for the widgets the main menu is made of.
    ///
    /// The menu is assembled in code so there is no Canvas hierarchy inside a .unity
    /// file. Unity scenes are large YAML documents that conflict badly when more than
    /// one person edits them; a C# file does not.
    /// </summary>
    public static class MenuUI
    {
        /// <summary>Creates the root canvas, its scaler and an EventSystem if needed.</summary>
        public static Canvas CreateCanvas(string name, Transform parent = null)
        {
            GameObject go = new GameObject(name, typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));

            if (parent != null)
            {
                go.transform.SetParent(parent, false);
            }

            Canvas canvas = go.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;

            CanvasScaler scaler = go.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = MenuTheme.ReferenceResolution;
            // Match on height: the background art is 16:9, and matching height keeps the
            // centre column in the clear area on wider and narrower screens alike.
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 1f;

            EnsureEventSystem();

            return canvas;
        }

        public static void EnsureEventSystem()
        {
            if (EventSystem.current != null)
            {
                return;
            }

            GameObject go = new GameObject("EventSystem", typeof(EventSystem));

#if ENABLE_INPUT_SYSTEM && !ENABLE_LEGACY_INPUT_MANAGER
            go.AddComponent<UnityEngine.InputSystem.UI.InputSystemUIInputModule>();
#else
            go.AddComponent<StandaloneInputModule>();
#endif
        }

        /// <summary>Full-screen background image, scaled to cover without distortion.</summary>
        public static Image CreateBackground(Transform parent, Sprite sprite)
        {
            GameObject go = new GameObject("Background", typeof(Image));
            go.transform.SetParent(parent, false);

            RectTransform rect = go.GetComponent<RectTransform>();
            Stretch(rect);

            Image image = go.GetComponent<Image>();
            image.sprite = sprite;
            image.type = Image.Type.Simple;
            image.raycastTarget = false;

            if (sprite == null)
            {
                image.color = new Color(0.53f, 0.78f, 0.92f, 1f);

                return image;
            }

            // EnvelopeParent scales the art to cover the screen and crops the overflow,
            // rather than stretching the characters or leaving bars at other aspect ratios.
            AspectRatioFitter fitter = go.AddComponent<AspectRatioFitter>();
            fitter.aspectMode = AspectRatioFitter.AspectMode.EnvelopeParent;
            fitter.aspectRatio = sprite.rect.width / sprite.rect.height;

            return image;
        }

        /// <summary>A centred vertical column of content on a translucent card.</summary>
        public static RectTransform CreatePanel(Transform parent, string name, float width, float spacing = 16f)
        {
            GameObject go = new GameObject(name, typeof(Image), typeof(VerticalLayoutGroup), typeof(ContentSizeFitter));
            go.transform.SetParent(parent, false);

            RectTransform rect = go.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = new Vector2(width, 0f);

            Image background = go.GetComponent<Image>();
            background.sprite = MenuTheme.RoundedRect;
            background.type = Image.Type.Sliced;
            background.color = MenuTheme.Panel;

            VerticalLayoutGroup layout = go.GetComponent<VerticalLayoutGroup>();
            layout.padding = new RectOffset(40, 40, 36, 36);
            layout.spacing = spacing;
            layout.childAlignment = TextAnchor.UpperCenter;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;

            ContentSizeFitter fitter = go.GetComponent<ContentSizeFitter>();
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            return rect;
        }

        public static TextMeshProUGUI CreateText(
            Transform parent,
            string content,
            int fontSize,
            Color color,
            TextAlignmentOptions alignment = TextAlignmentOptions.Center,
            FontStyles style = FontStyles.Normal)
        {
            GameObject go = new GameObject("Text", typeof(TextMeshProUGUI));
            go.transform.SetParent(parent, false);

            TextMeshProUGUI text = go.GetComponent<TextMeshProUGUI>();
            text.text = content;
            text.fontSize = fontSize;
            text.color = color;
            text.alignment = alignment;
            text.fontStyle = style;
            text.raycastTarget = false;

            return text;
        }

        public static Button CreateButton(
            Transform parent,
            string label,
            Color color,
            Action onClick,
            float height = MenuTheme.ButtonHeight)
        {
            GameObject go = new GameObject($"Button_{label}", typeof(Image), typeof(Button), typeof(LayoutElement));
            go.transform.SetParent(parent, false);

            Image background = go.GetComponent<Image>();
            background.sprite = MenuTheme.RoundedRect;
            background.type = Image.Type.Sliced;
            background.color = color;

            LayoutElement element = go.GetComponent<LayoutElement>();
            element.minHeight = height;
            element.preferredHeight = height;

            TextMeshProUGUI text = CreateText(
                go.transform,
                label,
                MenuTheme.ButtonFontSize,
                Color.white,
                TextAlignmentOptions.Center,
                FontStyles.Bold);

            Stretch(text.rectTransform, 16f);

            Button button = go.GetComponent<Button>();
            button.targetGraphic = background;

            ColorBlock colors = button.colors;
            colors.normalColor = Color.white;
            colors.highlightedColor = new Color(0.90f, 0.90f, 0.90f, 1f);
            colors.pressedColor = new Color(0.80f, 0.80f, 0.80f, 1f);
            colors.disabledColor = new Color(1f, 1f, 1f, 0.45f);
            colors.fadeDuration = 0.08f;
            button.colors = colors;

            if (onClick != null)
            {
                button.onClick.AddListener(() => onClick());
            }

            return button;
        }

        /// <summary>A labelled text field. Returns the input so callers can read it.</summary>
        public static TMP_InputField CreateField(
            Transform parent,
            string placeholder,
            bool isPassword = false,
            TMP_InputField.CharacterValidation validation = TMP_InputField.CharacterValidation.None)
        {
            GameObject go = new GameObject($"Field_{placeholder}", typeof(Image), typeof(TMP_InputField), typeof(LayoutElement));
            go.transform.SetParent(parent, false);

            Image background = go.GetComponent<Image>();
            background.sprite = MenuTheme.RoundedRect;
            background.type = Image.Type.Sliced;
            background.color = MenuTheme.Field;

            LayoutElement element = go.GetComponent<LayoutElement>();
            element.minHeight = MenuTheme.FieldHeight;
            element.preferredHeight = MenuTheme.FieldHeight;

            // TMP_InputField requires a dedicated viewport with a RectMask2D, otherwise
            // long text draws outside the field.
            GameObject viewport = new GameObject("TextArea", typeof(RectTransform), typeof(RectMask2D));
            viewport.transform.SetParent(go.transform, false);
            Stretch(viewport.GetComponent<RectTransform>(), 18f, 10f);

            TextMeshProUGUI text = CreateText(
                viewport.transform,
                string.Empty,
                MenuTheme.BodyFontSize,
                MenuTheme.Ink,
                TextAlignmentOptions.Left);
            text.raycastTarget = true;
            Stretch(text.rectTransform);

            TextMeshProUGUI hint = CreateText(
                viewport.transform,
                placeholder,
                MenuTheme.BodyFontSize,
                MenuTheme.InkMuted,
                TextAlignmentOptions.Left,
                FontStyles.Italic);
            Stretch(hint.rectTransform);

            TMP_InputField field = go.GetComponent<TMP_InputField>();
            field.textViewport = viewport.GetComponent<RectTransform>();
            field.textComponent = text;
            field.placeholder = hint;
            field.characterValidation = validation;
            field.lineType = TMP_InputField.LineType.SingleLine;

            if (isPassword)
            {
                field.contentType = TMP_InputField.ContentType.Password;
                field.asteriskChar = '*';
            }

            return field;
        }

        /// <summary>A horizontal row that lays its children out side by side.</summary>
        public static RectTransform CreateRow(Transform parent, float spacing = 12f, float height = MenuTheme.ButtonHeight)
        {
            GameObject go = new GameObject("Row", typeof(HorizontalLayoutGroup), typeof(LayoutElement));
            go.transform.SetParent(parent, false);

            HorizontalLayoutGroup layout = go.GetComponent<HorizontalLayoutGroup>();
            layout.spacing = spacing;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = true;

            LayoutElement element = go.GetComponent<LayoutElement>();
            element.minHeight = height;
            element.preferredHeight = height;

            return go.GetComponent<RectTransform>();
        }

        /// <summary>A vertically scrolling list. Returns the content transform to fill.</summary>
        public static RectTransform CreateScrollList(Transform parent, float height, float spacing = 12f)
        {
            GameObject scrollGo = new GameObject("ScrollView", typeof(Image), typeof(ScrollRect), typeof(RectMask2D), typeof(LayoutElement));
            scrollGo.transform.SetParent(parent, false);

            scrollGo.GetComponent<Image>().color = new Color(1f, 1f, 1f, 0f);

            LayoutElement element = scrollGo.GetComponent<LayoutElement>();
            element.minHeight = height;
            element.preferredHeight = height;
            element.flexibleHeight = 0f;

            GameObject contentGo = new GameObject("Content", typeof(VerticalLayoutGroup), typeof(ContentSizeFitter));
            contentGo.transform.SetParent(scrollGo.transform, false);

            RectTransform content = contentGo.GetComponent<RectTransform>();
            content.anchorMin = new Vector2(0f, 1f);
            content.anchorMax = new Vector2(1f, 1f);
            content.pivot = new Vector2(0.5f, 1f);
            content.offsetMin = new Vector2(0f, 0f);
            content.offsetMax = new Vector2(0f, 0f);

            VerticalLayoutGroup layout = contentGo.GetComponent<VerticalLayoutGroup>();
            layout.spacing = spacing;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;

            ContentSizeFitter fitter = contentGo.GetComponent<ContentSizeFitter>();
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            ScrollRect scroll = scrollGo.GetComponent<ScrollRect>();
            scroll.content = content;
            scroll.viewport = scrollGo.GetComponent<RectTransform>();
            scroll.horizontal = false;
            scroll.vertical = true;
            scroll.movementType = ScrollRect.MovementType.Elastic;
            scroll.scrollSensitivity = 30f;

            return content;
        }

        public static void Stretch(RectTransform rect, float horizontalPadding = 0f, float verticalPadding = 0f)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = new Vector2(horizontalPadding, verticalPadding);
            rect.offsetMax = new Vector2(-horizontalPadding, -verticalPadding);
        }

        public static void SetSpacer(Transform parent, float height)
        {
            GameObject go = new GameObject("Spacer", typeof(LayoutElement));
            go.transform.SetParent(parent, false);

            LayoutElement element = go.GetComponent<LayoutElement>();
            element.minHeight = height;
            element.preferredHeight = height;
        }
    }
}
