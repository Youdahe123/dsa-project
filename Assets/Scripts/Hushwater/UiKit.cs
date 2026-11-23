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

        private static Font font, titleFont;
        private static Sprite ring, vignette;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            font = titleFont = null;
            ring = vignette = null;
        }

        // Body text: Special Elite, a worn typewriter face (Nell's survey notes, 1950s paperwork). Apache 2.0.
        public static Font Font => font != null ? font : font = Load("Hushwater/Fonts/SpecialElite");
        // Titles: IM Fell English SC, cut from 17th-century type (the chapel, the register). SIL OFL.
        public static Font TitleFont => titleFont != null ? titleFont : titleFont = Load("Hushwater/Fonts/IMFellEnglishSC");

        // Falls back to the built-in font if the file is missing, so text never disappears.
        private static Font Load(string path)
        {
            Font f = Resources.Load<Font>(path);
            return f != null ? f : Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        }

        // A text label pinned to an anchor at a pixel offset, with a soft shadow so it reads over any background.
        public static Text Text(Transform parent, string value, int size, Vector2 anchor, Vector2 position, TextAnchor alignment, Vector2 box, Color color)
        {
            Text text = new GameObject("Text").AddComponent<Text>();
            text.transform.SetParent(parent, false);
            text.text = value;
            // Big lines (screen titles, the timer, end cards) are set in the title face.
            text.font = size >= 30 ? TitleFont : Font;
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
            colors.normalColor = new Color(0.12f, 0.12f, 0.1f, 0.82f);
            colors.highlightedColor = new Color(Lantern.r, Lantern.g, Lantern.b, 0.35f);
            colors.pressedColor = new Color(Lantern.r, Lantern.g, Lantern.b, 0.6f);
            colors.selectedColor = colors.highlightedColor;
            button.colors = colors;
            button.onClick.AddListener(onClick);
            // A thin brass frame, like a label holder on a drawer.
            Frame(plate.transform, new Color(Lantern.r, Lantern.g, Lantern.b, 0.3f), 1.5f);
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

        // A dark panel with a brass hairline, for grouping text over a 3D backdrop.
        public static Image Panel(Transform parent, Vector2 anchor, Vector2 position, Vector2 size)
        {
            Image panel = Box(parent, new Color(0.035f, 0.045f, 0.05f, 0.82f), anchor, position, size);
            Frame(panel.transform, new Color(Lantern.r, Lantern.g, Lantern.b, 0.25f), 1.5f);
            return panel;
        }

        // Four hairlines just inside a rect. (uGUI's Outline effect redraws the whole quad, which tints see-through panels.)
        public static void Frame(Transform rect, Color color, float width)
        {
            Vector2[,] edges =
            {
                { new Vector2(0f, 1f), new Vector2(1f, 1f) }, { new Vector2(0f, 0f), new Vector2(1f, 0f) },
                { new Vector2(0f, 0f), new Vector2(0f, 1f) }, { new Vector2(1f, 0f), new Vector2(1f, 1f) },
            };
            for (int i = 0; i < 4; i++)
            {
                Image line = new GameObject("Frame").AddComponent<Image>();
                line.transform.SetParent(rect, false);
                line.color = color;
                line.raycastTarget = false;
                RectTransform r = line.rectTransform;
                r.anchorMin = edges[i, 0];
                r.anchorMax = edges[i, 1];
                r.pivot = edges[i, 0];
                r.anchoredPosition = Vector2.zero;
                r.sizeDelta = i < 2 ? new Vector2(0f, width) : new Vector2(width, 0f);
                if (i == 0) r.pivot = new Vector2(0f, 1f);
                if (i == 3) r.pivot = new Vector2(1f, 0f);
            }
        }

        // Darkened corners over the whole screen: the edges of a lantern's reach.
        public static Image Vignette(Transform parent, float strength)
        {
            if (vignette == null)
            {
                const int size = 256;
                Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
                for (int y = 0; y < size; y++)
                    for (int x = 0; x < size; x++)
                    {
                        float dx = (x + 0.5f) / size * 2f - 1f, dy = (y + 0.5f) / size * 2f - 1f;
                        float d = Mathf.Sqrt(dx * dx + dy * dy) / 1.414f;
                        tex.SetPixel(x, y, new Color(0f, 0f, 0f, Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.45f, 1f, d))));
                    }
                tex.Apply();
                vignette = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f));
            }
            Image image = Overlay(parent, new Color(1f, 1f, 1f, strength));
            image.sprite = vignette;
            image.name = "Vignette";
            return image;
        }

        // A soft round glow (lantern light behind an icon).
        public static Image Glow(Transform parent, Color color, Vector2 anchor, Vector2 position, float size)
        {
            Image image = Box(parent, color, anchor, position, new Vector2(size, size));
            image.name = "Glow";
            Texture2D tex = (Texture2D)Fx.Particle.mainTexture;
            image.sprite = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f));
            return image;
        }

        // An icon from Icons, tinted.
        public static Image Icon(Transform parent, Icons.Kind kind, Color color, Vector2 anchor, Vector2 position, float size)
        {
            Image image = Box(parent, color, anchor, position, new Vector2(size, size));
            image.name = "Icon " + kind;
            image.sprite = Icons.Get(kind);
            image.preserveAspect = true;
            return image;
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
