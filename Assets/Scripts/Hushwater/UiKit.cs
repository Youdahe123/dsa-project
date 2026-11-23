using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace Hushwater
{
    // Small helpers for building uGUI in code, plus the design doc's colour palette.
    public static class UiKit
    {
        // Design doc, "Feel of the Game": Night #0E1416, Lake #2E6B6E, Silt #6B6450, Rust #A8402C, Lantern #F0C36A.
        public static readonly Color Night = new Color32(0x0E, 0x14, 0x16, 0xFF);
        public static readonly Color Lake = new Color32(0x2E, 0x6B, 0x6E, 0xFF);
        public static readonly Color Silt = new Color32(0x6B, 0x64, 0x50, 0xFF);
        public static readonly Color Rust = new Color32(0xA8, 0x40, 0x2C, 0xFF);
        public static readonly Color Lantern = new Color32(0xF0, 0xC3, 0x6A, 0xFF);
        public static readonly Color Paper = new Color(0.86f, 0.84f, 0.78f);

        private static Font font;
        private static Sprite ring;

        // The built-in font, loaded once.
        public static Font Font => font != null ? font : font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

        // A text label pinned to an anchor at a pixel offset, with a soft shadow so it reads over any background.
        public static Text Text(Transform parent, string value, int size, Vector2 anchor, Vector2 position, TextAnchor alignment, Vector2 box, Color color)
        {
            Text text = new GameObject("Text").AddComponent<Text>();
            text.transform.SetParent(parent, false);
            text.text = value;
            text.font = Font;
            text.fontSize = size;
            text.alignment = alignment;
            text.color = color;
            text.raycastTarget = false;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            Place(text.rectTransform, anchor, position, box);
            Shadow shadow = text.gameObject.AddComponent<Shadow>();
            shadow.effectColor = new Color(0f, 0f, 0f, 0.8f);
            shadow.effectDistance = new Vector2(1.5f, -1.5f);
            return text;
        }

        // A flat-coloured rectangle.
        public static Image Box(Transform parent, Color color, Vector2 anchor, Vector2 position, Vector2 size)
        {
            Image image = new GameObject("Box").AddComponent<Image>();
            image.transform.SetParent(parent, false);
            image.color = color;
            image.raycastTarget = false;
            Place(image.rectTransform, anchor, position, size);
            return image;
        }

        // A full-screen image (hurt flash, fade, wardrobe slats).
        public static Image Overlay(Transform parent, Color color)
        {
            Image image = new GameObject("Overlay").AddComponent<Image>();
            image.transform.SetParent(parent, false);
            image.color = color;
            image.raycastTarget = false;
            Stretch(image.rectTransform);
            return image;
        }

        // A dark track with a coloured fill; callers shrink the fill by moving its anchorMax.x.
        public static Image Bar(Transform parent, Vector2 anchor, Vector2 position, Vector2 size, Color color)
        {
            Image track = Box(parent, new Color(0f, 0f, 0f, 0.55f), anchor, position, size);
            Image fill = Box(track.transform, color, Vector2.zero, Vector2.zero, Vector2.zero);
            Stretch(fill.rectTransform);
            return fill;
        }

        // A menu button: dark plate, lantern-amber highlight, centred label.
        public static Button Button(Transform parent, string label, Vector2 position, Vector2 size, UnityAction onClick)
        {
            Image plate = Box(parent, Color.white, new Vector2(0.5f, 0.5f), position, size);
            plate.raycastTarget = true;
            Button button = plate.gameObject.AddComponent<Button>();
            button.targetGraphic = plate; // Reset() doesn't run for components added at runtime
            ColorBlock colors = button.colors;
            colors.normalColor = new Color(Silt.r, Silt.g, Silt.b, 0.35f);
            colors.highlightedColor = new Color(Lantern.r, Lantern.g, Lantern.b, 0.35f);
            colors.pressedColor = new Color(Lantern.r, Lantern.g, Lantern.b, 0.6f);
            colors.selectedColor = colors.highlightedColor;
            button.colors = colors;
            button.onClick.AddListener(onClick);
            Text text = Text(plate.transform, label, 24, new Vector2(0.5f, 0.5f), Vector2.zero, TextAnchor.MiddleCenter, size, Paper);
            text.name = "Label";
            return button;
        }

        // A screen-space canvas scaled for 1920x1080, plus an EventSystem if the scene has none.
        public static Transform Canvas(string name, int order = 0)
        {
            Canvas canvas = new GameObject(name).AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = order;
            CanvasScaler scaler = canvas.gameObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;
            canvas.gameObject.AddComponent<GraphicRaycaster>();
            if (Object.FindAnyObjectByType<UnityEngine.EventSystems.EventSystem>() == null)
                new GameObject("EventSystem", typeof(UnityEngine.EventSystems.EventSystem), typeof(UnityEngine.EventSystems.StandaloneInputModule));
            return canvas.transform;
        }

        // Gives a button keyboard/gamepad focus so menus work without a mouse.
        public static void Select(Selectable selectable)
        {
            if (selectable == null || UnityEngine.EventSystems.EventSystem.current == null) return;
            UnityEngine.EventSystems.EventSystem.current.SetSelectedGameObject(null);
            UnityEngine.EventSystems.EventSystem.current.SetSelectedGameObject(selectable.gameObject);
        }

        // A plain camera for screens with no world in them.
        public static Camera BackdropCamera()
        {
            Camera camera = new GameObject("Backdrop Camera").AddComponent<Camera>();
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = Night;
            camera.gameObject.AddComponent<AudioListener>();
            return camera;
        }

        // Sets a button's label text.
        public static void Label(Button button, string text) => button.GetComponentInChildren<Text>().text = text;

        // A thin ring sprite generated once, used for the noise ripple.
        public static Sprite Ring
        {
            get
            {
                if (ring != null) return ring;
                const int size = 128;
                Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
                Color[] pixels = new Color[size * size];
                for (int y = 0; y < size; y++)
                    for (int x = 0; x < size; x++)
                    {
                        float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(size / 2f, size / 2f));
                        float alpha = Mathf.Clamp01(1f - Mathf.Abs(d - 58f) / 3f);
                        pixels[y * size + x] = new Color(1f, 1f, 1f, alpha);
                    }
                texture.SetPixels(pixels);
                texture.Apply();
                return ring = Sprite.Create(texture, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f));
            }
        }

        // Colour with a different alpha.
        public static Color Fade(Color color, float alpha) => new Color(color.r, color.g, color.b, alpha);

        // Colour as a rich-text hex string (RRGGBBAA).
        public static string Hex(Color color) => ColorUtility.ToHtmlStringRGBA(color);

        // Anchors and pivots a rect at one point with a size and offset.
        private static void Place(RectTransform rect, Vector2 anchor, Vector2 position, Vector2 size)
        {
            rect.anchorMin = rect.anchorMax = rect.pivot = anchor;
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
        }

        // Makes a rect fill its parent.
        public static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = rect.offsetMax = Vector2.zero;
        }
    }
}
