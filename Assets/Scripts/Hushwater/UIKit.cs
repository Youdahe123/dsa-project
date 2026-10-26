using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Hushwater
{
    // Code-built uGUI in the doc's palette: night, silt, drowned teal, rust and lantern amber.
    public static class UIKit
    {
        public static readonly Color Ink = new Color(0.87f, 0.85f, 0.79f);
        public static readonly Color Dim = new Color(0.56f, 0.58f, 0.56f);
        public static readonly Color Amber = new Color(0.94f, 0.76f, 0.42f);
        public static readonly Color Rust = new Color(0.66f, 0.25f, 0.17f);
        public static readonly Color Teal = new Color(0.2f, 0.45f, 0.46f);
        public static readonly Color Night = new Color(0.035f, 0.05f, 0.056f);

        public static Canvas Canvas(string name, int order)
        {
            GameObject go = new GameObject(name);
            Canvas canvas = go.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = order;
            CanvasScaler scaler = go.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;
            go.AddComponent<GraphicRaycaster>();
            EnsureEventSystem();
            return canvas;
        }

        public static void EnsureEventSystem()
        {
            if (Object.FindAnyObjectByType<EventSystem>() != null) return;
            GameObject es = new GameObject("EventSystem");
            es.AddComponent<EventSystem>();
            es.AddComponent<StandaloneInputModule>();
        }

        public static RectTransform Rect(string name, Transform parent, Vector2 anchor, Vector2 pivot, Vector2 position, Vector2 size)
        {
            GameObject go = new GameObject(name, typeof(RectTransform));
            RectTransform rect = (RectTransform)go.transform;
            rect.SetParent(parent, false);
            rect.anchorMin = rect.anchorMax = anchor;
            rect.pivot = pivot;
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
            return rect;
        }

        public static RectTransform Stretch(string name, Transform parent)
        {
            GameObject go = new GameObject(name, typeof(RectTransform));
            RectTransform rect = (RectTransform)go.transform;
            rect.SetParent(parent, false);
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = rect.offsetMax = Vector2.zero;
            return rect;
        }

        public static Text Label(Transform parent, string value, Font font, int size, Color color, TextAnchor align, Vector2 anchor, Vector2 position, Vector2 box, bool shadow = true)
        {
            RectTransform rect = Rect("Text", parent, anchor, anchor, position, box);
            Text text = rect.gameObject.AddComponent<Text>();
            text.font = font;
            text.fontSize = size;
            text.color = color;
            text.alignment = align;
            text.text = value;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            text.raycastTarget = false;
            text.supportRichText = true;
            if (shadow)
            {
                Shadow s = rect.gameObject.AddComponent<Shadow>();
                s.effectColor = new Color(0f, 0f, 0f, 0.8f);
                s.effectDistance = new Vector2(1.5f, -1.5f);
            }
            return text;
        }

        public static Image Panel(Transform parent, Color color, Vector2 anchor, Vector2 pivot, Vector2 position, Vector2 size)
        {
            Image image = Rect("Panel", parent, anchor, pivot, position, size).gameObject.AddComponent<Image>();
            image.color = color;
            image.raycastTarget = false;
            return image;
        }

        public static Image Fill(Transform parent, Color color)
        {
            Image image = Stretch("Fill", parent).gameObject.AddComponent<Image>();
            image.color = color;
            image.raycastTarget = false;
            return image;
        }

        // A thin bar whose fill is driven by anchors (Filled images need a sprite).
        public static RectTransform Bar(Transform parent, Vector2 anchor, Vector2 pivot, Vector2 position, Vector2 size, Color color)
        {
            Image back = Panel(parent, new Color(0f, 0f, 0f, 0.55f), anchor, pivot, position, size);
            Image fill = Fill(back.transform, color);
            return fill.rectTransform;
        }

        public static void SetBar(RectTransform fill, float value) => fill.anchorMax = new Vector2(Mathf.Clamp01(value), 1f);

        public static Button TextButton(Transform parent, string label, Vector2 anchor, Vector2 position, UnityAction onClick, int size = 36, TextAnchor align = TextAnchor.MiddleLeft, float width = 460f)
        {
            Text text = Label(parent, label, HushwaterAssets.TitleFont, size, Ink, align, anchor, position, new Vector2(width, size * 1.5f));
            text.raycastTarget = true;
            Button button = text.gameObject.AddComponent<Button>();
            button.targetGraphic = text;
            ColorBlock colors = button.colors;
            colors.normalColor = Color.white;
            colors.highlightedColor = new Color(1.1f, 0.88f, 0.5f);
            colors.selectedColor = Color.white;
            colors.pressedColor = new Color(0.8f, 0.6f, 0.35f);
            colors.disabledColor = new Color(0.4f, 0.4f, 0.4f, 0.6f);
            colors.colorMultiplier = 1.2f;
            colors.fadeDuration = 0.08f;
            button.colors = colors;
            button.navigation = new Navigation { mode = Navigation.Mode.None };
            button.onClick.AddListener(() => SoundFx.Play2D(ProceduralAudio.UiTick, 0.5f));
            button.onClick.AddListener(onClick);
            EventTrigger trigger = text.gameObject.AddComponent<EventTrigger>();
            var enter = new EventTrigger.Entry { eventID = EventTriggerType.PointerEnter };
            enter.callback.AddListener(_ => SoundFx.Play2D(ProceduralAudio.UiTick, 0.18f, 1.4f));
            trigger.triggers.Add(enter);
            return button;
        }

        public static Slider Slider(Transform parent, Vector2 anchor, Vector2 position, float width, float min, float max, float value, UnityAction<float> onChange)
        {
            GameObject go = DefaultControls.CreateSlider(new DefaultControls.Resources());
            go.transform.SetParent(parent, false);
            RectTransform rect = (RectTransform)go.transform;
            rect.anchorMin = rect.anchorMax = anchor;
            rect.pivot = new Vector2(0f, 0.5f);
            rect.anchoredPosition = position;
            rect.sizeDelta = new Vector2(width, 18f);
            foreach (Image image in go.GetComponentsInChildren<Image>())
            {
                image.color = image.name switch
                {
                    "Background" => new Color(0f, 0f, 0f, 0.6f),
                    "Fill" => Teal,
                    "Handle" => Amber,
                    _ => image.color
                };
            }
            Transform handle = go.transform.Find("Handle Slide Area/Handle");
            if (handle != null) ((RectTransform)handle).sizeDelta = new Vector2(14f, 0f);
            Slider slider = go.GetComponent<Slider>();
            slider.minValue = min;
            slider.maxValue = max;
            slider.value = value;
            slider.navigation = new Navigation { mode = Navigation.Mode.None };
            slider.onValueChanged.AddListener(onChange);
            return slider;
        }

        public static Texture2D Ring(int size, float thickness)
        {
            Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
            float r = size / 2f;
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(r, r)) / r;
                float a = Mathf.Clamp01(1f - Mathf.Abs(d - (1f - thickness)) / thickness);
                texture.SetPixel(x, y, new Color(1f, 1f, 1f, a * a));
            }
            texture.Apply();
            return texture;
        }

        public static Texture2D Gradient(Color left, Color right, int width = 256)
        {
            Texture2D texture = new Texture2D(width, 1, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
            for (int x = 0; x < width; x++) texture.SetPixel(x, 0, Color.Lerp(left, right, x / (width - 1f)));
            texture.Apply();
            return texture;
        }

        public static void SetVisible(Component c, bool visible)
        {
            if (c != null && c.gameObject.activeSelf != visible) c.gameObject.SetActive(visible);
        }
    }
}
