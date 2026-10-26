using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Hushwater
{
    // "The HUD shows only what Nell could know." The Sexton never appears on it.
    public sealed class HudController : MonoBehaviour
    {
        private sealed class Ripple { public RawImage Image; public float Age; public float Radius; }
        private sealed class CaptionLine { public Text Text; public Image Back; public float Life; }

        private NightDirector director;
        private Canvas canvas;
        private RectTransform root;
        private Text timer;
        private Text timerLabel;
        private Text tollText;
        private Text evidence;
        private RectTransform vitalityFill;
        private RectTransform breathFill;
        private RectTransform lanternFill;
        private Text exposures;
        private Text flashText;
        private readonly Text[] slots = new Text[3];
        private readonly Image[] slotBacks = new Image[3];
        private Image crosshair;
        private Text prompt;
        private RectTransform holdFill;
        private GameObject holdBar;
        private readonly List<CaptionLine> captions = new List<CaptionLine>();
        private RectTransform captionRoot;
        private readonly List<Ripple> ripples = new List<Ripple>();
        private Texture2D ringTexture;
        private GameObject viewfinder;
        private Text viewfinderInfo;
        private Text inFrame;
        private RectTransform whineFill;
        private GameObject hideOverlay;
        private RectTransform photoCard;
        private RawImage photoImage;
        private Text photoTitle;
        private Text photoNote;
        private Image photoStamp;
        private Text photoStampText;
        private float photoTimer;
        private Image fade;
        private float fadeTarget;
        private float fadeSpeed = 1f;
        private GameObject titleCard;
        private Text titleCardMain;
        private Text titleCardSub;
        private Text titleCardBody;
        private float titleTimer;

        public void Build(NightDirector owner)
        {
            director = owner;
            canvas = UIKit.Canvas("HUD", 10);
            canvas.transform.SetParent(transform, false);
            root = UIKit.Stretch("HUD root", canvas.transform);
            ringTexture = UIKit.Ring(256, 0.04f);
            Font body = HushwaterAssets.BodyFont;
            Font title = HushwaterAssets.TitleFont;

            hideOverlay = BuildHideOverlay();

            // Top centre: the dawn bell.
            timer = UIKit.Label(root, "18:00", body, 40, UIKit.Ink, TextAnchor.MiddleCenter, new Vector2(0.5f, 1f), new Vector2(0f, -48f), new Vector2(300f, 50f));
            timerLabel = UIKit.Label(root, "UNTIL THE DAWN BELL", body, 14, UIKit.Dim, TextAnchor.MiddleCenter, new Vector2(0.5f, 1f), new Vector2(0f, -82f), new Vector2(400f, 24f));
            tollText = UIKit.Label(root, "", body, 18, UIKit.Amber, TextAnchor.MiddleCenter, new Vector2(0.5f, 1f), new Vector2(0f, -112f), new Vector2(700f, 30f));

            // Top right: evidence.
            evidence = UIKit.Label(root, "EVIDENCE 0/3", body, 26, UIKit.Ink, TextAnchor.MiddleRight, new Vector2(1f, 1f), new Vector2(-190f, -48f), new Vector2(340f, 40f));

            // Bottom left: vitality, breath, lantern.
            UIKit.Label(root, "VITALITY", body, 14, UIKit.Dim, TextAnchor.LowerLeft, new Vector2(0f, 0f), new Vector2(200f, 128f), new Vector2(300f, 20f));
            vitalityFill = UIKit.Bar(root, new Vector2(0f, 0f), new Vector2(0f, 0.5f), new Vector2(52f, 108f), new Vector2(300f, 8f), UIKit.Rust);
            UIKit.Label(root, "BREATH", body, 14, UIKit.Dim, TextAnchor.LowerLeft, new Vector2(0f, 0f), new Vector2(200f, 84f), new Vector2(300f, 20f));
            breathFill = UIKit.Bar(root, new Vector2(0f, 0f), new Vector2(0f, 0.5f), new Vector2(52f, 64f), new Vector2(300f, 8f), UIKit.Teal);
            UIKit.Label(root, "LANTERN  ·  hold F to crank", body, 14, UIKit.Dim, TextAnchor.LowerLeft, new Vector2(0f, 0f), new Vector2(200f, 40f), new Vector2(300f, 20f));
            lanternFill = UIKit.Bar(root, new Vector2(0f, 0f), new Vector2(0f, 0.5f), new Vector2(52f, 22f), new Vector2(300f, 6f), UIKit.Amber);

            // Bottom right: camera and tools.
            exposures = UIKit.Label(root, "12 / 12", body, 34, UIKit.Ink, TextAnchor.LowerRight, new Vector2(1f, 0f), new Vector2(-180f, 108f), new Vector2(300f, 44f));
            flashText = UIKit.Label(root, "EXPOSURES · FLASH ON (R)", body, 14, UIKit.Dim, TextAnchor.LowerRight, new Vector2(1f, 0f), new Vector2(-180f, 88f), new Vector2(340f, 20f));
            string[] names = { "HANDBELL", "BOTTLE", "SALTS" };
            for (int i = 0; i < 3; i++)
            {
                slotBacks[i] = UIKit.Panel(root, new Color(0f, 0f, 0f, 0.45f), new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(-52f - (2 - i) * 132f, 24f), new Vector2(124f, 50f));
                slots[i] = UIKit.Label(slotBacks[i].transform, $"{i + 1}  {names[i]}\n×0", body, 14, UIKit.Ink, TextAnchor.MiddleCenter, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(120f, 48f));
            }
            UIKit.Label(root, "Q  throw / use", body, 13, UIKit.Dim, TextAnchor.LowerRight, new Vector2(1f, 0f), new Vector2(-180f, 76f), new Vector2(300f, 18f)).rectTransform.anchoredPosition = new Vector2(-52f - 150f, 78f);

            // Centre.
            crosshair = UIKit.Panel(root, new Color(1f, 1f, 1f, 0.55f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(4f, 4f));
            prompt = UIKit.Label(root, "", body, 22, UIKit.Ink, TextAnchor.MiddleCenter, new Vector2(0.5f, 0.5f), new Vector2(0f, -120f), new Vector2(1100f, 34f));
            Image holdBack = UIKit.Panel(root, new Color(0f, 0f, 0f, 0.6f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, -146f), new Vector2(220f, 5f));
            holdFill = UIKit.Fill(holdBack.transform, UIKit.Amber).rectTransform;
            holdBar = holdBack.gameObject;
            captionRoot = UIKit.Rect("Captions", root, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 180f), new Vector2(1200f, 160f));

            viewfinder = BuildViewfinder();
            BuildPhotoCard();

            fade = UIKit.Fill(canvas.transform, Color.black);
            fade.color = new Color(0f, 0f, 0f, 1f);
            BuildTitleCard();
            PlayerController.MadeNoise += OnNoise;
        }

        private void OnDestroy() => PlayerController.MadeNoise -= OnNoise;

        private GameObject BuildHideOverlay()
        {
            RectTransform overlay = UIKit.Stretch("Wardrobe slats", root);
            // Dark doors with a slit of light down the middle.
            Image left = UIKit.Fill(overlay, new Color(0.01f, 0.01f, 0.01f, 0.96f));
            left.rectTransform.anchorMax = new Vector2(0.47f, 1f);
            Image right = UIKit.Fill(overlay, new Color(0.01f, 0.01f, 0.01f, 0.96f));
            right.rectTransform.anchorMin = new Vector2(0.53f, 0f);
            for (int i = 0; i < 9; i++)
            {
                Image slat = UIKit.Fill(overlay, new Color(0.02f, 0.015f, 0.01f, 0.85f));
                slat.rectTransform.anchorMin = new Vector2(0.47f, i / 9f);
                slat.rectTransform.anchorMax = new Vector2(0.53f, i / 9f + 0.035f);
            }
            UIKit.Label(overlay, "HIDDEN", HushwaterAssets.BodyFont, 18, UIKit.Dim, TextAnchor.MiddleCenter, new Vector2(0.5f, 1f), new Vector2(0f, -150f), new Vector2(300f, 30f));
            overlay.gameObject.SetActive(false);
            return overlay.gameObject;
        }

        private GameObject BuildViewfinder()
        {
            RectTransform vf = UIKit.Stretch("Viewfinder", root);
            Color line = new Color(0.9f, 0.88f, 0.8f, 0.85f);
            Vector2[] corners = { new Vector2(0.2f, 0.18f), new Vector2(0.8f, 0.18f), new Vector2(0.2f, 0.82f), new Vector2(0.8f, 0.82f) };
            foreach (Vector2 c in corners)
            {
                float sx = c.x < 0.5f ? 1f : -1f, sy = c.y < 0.5f ? 1f : -1f;
                UIKit.Panel(vf, line, c, new Vector2(c.x < 0.5f ? 0f : 1f, 0.5f), Vector2.zero, new Vector2(70f, 3f));
                UIKit.Panel(vf, line, c, new Vector2(0.5f, c.y < 0.5f ? 0f : 1f), Vector2.zero, new Vector2(3f, 70f));
                _ = sx; _ = sy;
            }
            UIKit.Panel(vf, line, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(36f, 2f));
            UIKit.Panel(vf, line, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(2f, 36f));
            viewfinderInfo = UIKit.Label(vf, "", HushwaterAssets.BodyFont, 18, line, TextAnchor.UpperLeft, new Vector2(0.2f, 0.82f), new Vector2(170f, -30f), new Vector2(600f, 30f));
            inFrame = UIKit.Label(vf, "", HushwaterAssets.BodyFont, 22, UIKit.Amber, TextAnchor.MiddleCenter, new Vector2(0.5f, 0.18f), new Vector2(0f, 40f), new Vector2(900f, 34f));
            Image whineBack = UIKit.Panel(vf, new Color(0f, 0f, 0f, 0.6f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, -60f), new Vector2(160f, 4f));
            whineFill = UIKit.Fill(whineBack.transform, new Color(0.85f, 0.92f, 1f)).rectTransform;
            vf.gameObject.SetActive(false);
            return vf.gameObject;
        }

        private void BuildPhotoCard()
        {
            Image card = UIKit.Panel(root, new Color(0.86f, 0.84f, 0.77f, 0.97f), new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(-48f, 60f), new Vector2(400f, 470f));
            photoCard = card.rectTransform;
            photoCard.localRotation = Quaternion.Euler(0f, 0f, -2.5f);
            Shadow cardShadow = card.gameObject.AddComponent<Shadow>();
            cardShadow.effectColor = new Color(0f, 0f, 0f, 0.5f);
            cardShadow.effectDistance = new Vector2(8f, -8f);
            RectTransform imageRect = UIKit.Rect("Photo", card.transform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -18f), new Vector2(364f, 273f));
            photoImage = imageRect.gameObject.AddComponent<RawImage>();
            photoImage.color = new Color(1f, 0.97f, 0.9f);
            photoTitle = UIKit.Label(card.transform, "", HushwaterAssets.TitleFont, 30, new Color(0.14f, 0.12f, 0.1f), TextAnchor.UpperLeft, new Vector2(0.5f, 1f), new Vector2(0f, -310f), new Vector2(364f, 40f), false);
            photoNote = UIKit.Label(card.transform, "", HushwaterAssets.BodyFont, 15, new Color(0.22f, 0.2f, 0.17f), TextAnchor.UpperLeft, new Vector2(0.5f, 1f), new Vector2(0f, -385f), new Vector2(364f, 100f), false);
            photoStamp = UIKit.Panel(imageRect, new Color(0f, 0f, 0f, 0f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(300f, 54f));
            photoStamp.rectTransform.localRotation = Quaternion.Euler(0f, 0f, 8f);
            photoStampText = UIKit.Label(photoStamp.transform, "", HushwaterAssets.BodyFont, 26, UIKit.Rust, TextAnchor.MiddleCenter, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(300f, 54f), false);
            photoCard.gameObject.SetActive(false);
        }

        private void BuildTitleCard()
        {
            RectTransform card = UIKit.Stretch("Title card", canvas.transform);
            titleCard = card.gameObject;
            titleCardSub = UIKit.Label(card, "NIGHT ONE", HushwaterAssets.BodyFont, 22, UIKit.Dim, TextAnchor.MiddleCenter, new Vector2(0.5f, 0.5f), new Vector2(0f, 110f), new Vector2(900f, 34f));
            titleCardMain = UIKit.Label(card, "Millrow", HushwaterAssets.TitleFont, 96, UIKit.Ink, TextAnchor.MiddleCenter, new Vector2(0.5f, 0.5f), new Vector2(0f, 30f), new Vector2(1200f, 120f));
            titleCardBody = UIKit.Label(card, "", HushwaterAssets.BodyFont, 22, UIKit.Ink, TextAnchor.UpperCenter, new Vector2(0.5f, 0.5f), new Vector2(0f, -80f), new Vector2(980f, 140f));
            titleCard.SetActive(false);
        }

        public void ShowTitle(string sub, string main, string bodyText, float seconds)
        {
            titleCardSub.text = sub;
            titleCardMain.text = main;
            titleCardBody.text = bodyText;
            titleTimer = seconds;
            titleCard.SetActive(true);
        }

        public void Fade(float target, float speed)
        {
            fadeTarget = target;
            fadeSpeed = speed;
        }

        public void SetFade(float value)
        {
            fadeTarget = value;
            fade.color = new Color(0f, 0f, 0f, value);
        }

        public void SetGameplayVisible(bool visible) => UIKit.SetVisible(root, visible);

        public void Caption(string text, float seconds)
        {
            foreach (CaptionLine existing in captions)
            {
                if (existing.Text.text != text) continue;
                existing.Life = seconds;
                return;
            }
            Image back = UIKit.Panel(captionRoot, new Color(0f, 0f, 0f, 0.55f), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), Vector2.zero, new Vector2(Mathf.Min(1180f, text.Length * 12.5f + 40f), 34f));
            Text line = UIKit.Label(back.transform, text, HushwaterAssets.BodyFont, 22, UIKit.Ink, TextAnchor.MiddleCenter, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(1200f, 32f));
            captions.Add(new CaptionLine { Text = line, Back = back, Life = seconds });
            while (captions.Count > 4)
            {
                Destroy(captions[0].Back.gameObject);
                captions.RemoveAt(0);
            }
        }

        public void ShowPhoto(Texture image, string title, string note, string stamp)
        {
            photoImage.texture = image;
            photoTitle.text = title;
            photoNote.text = note;
            photoStampText.text = stamp ?? "";
            photoStamp.color = string.IsNullOrEmpty(stamp) ? Color.clear : new Color(0.86f, 0.84f, 0.77f, 0.75f);
            photoTimer = 6f;
            photoCard.gameObject.SetActive(true);
            photoCard.anchoredPosition = new Vector2(420f, 60f);
        }

        private void OnNoise(float radius, NoiseKind kind)
        {
            if (!GameSettings.SoundRipples || radius < 1.9f) return;
            Ripple ripple = ripples.Find(r => r.Age >= 1f);
            if (ripple == null)
            {
                RectTransform rect = UIKit.Rect("Ripple", root, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.one * 100f);
                rect.SetSiblingIndex(1);
                ripple = new Ripple { Image = rect.gameObject.AddComponent<RawImage>() };
                ripple.Image.texture = ringTexture;
                ripple.Image.raycastTarget = false;
                ripples.Add(ripple);
            }
            ripple.Age = 0f;
            ripple.Radius = radius;
        }

        private void Update()
        {
            float dt = Time.unscaledDeltaTime;
            fade.color = new Color(0f, 0f, 0f, Mathf.MoveTowards(fade.color.a, fadeTarget, dt * fadeSpeed));
            fade.raycastTarget = fade.color.a > 0.9f;

            if (titleCard.activeSelf)
            {
                titleTimer -= dt;
                float a = Mathf.Clamp01(titleTimer);
                titleCardMain.color = new Color(UIKit.Ink.r, UIKit.Ink.g, UIKit.Ink.b, a);
                titleCardSub.color = new Color(UIKit.Dim.r, UIKit.Dim.g, UIKit.Dim.b, a);
                titleCardBody.color = new Color(UIKit.Ink.r, UIKit.Ink.g, UIKit.Ink.b, a);
                if (titleTimer <= 0f) titleCard.SetActive(false);
            }

            for (int i = captions.Count - 1; i >= 0; i--)
            {
                CaptionLine c = captions[i];
                c.Life -= dt;
                c.Back.rectTransform.anchoredPosition = Vector2.Lerp(c.Back.rectTransform.anchoredPosition, new Vector2(0f, (captions.Count - 1 - i) * 40f), dt * 10f);
                float alpha = Mathf.Clamp01(c.Life * 2f);
                c.Text.color = new Color(UIKit.Ink.r, UIKit.Ink.g, UIKit.Ink.b, alpha);
                c.Back.color = new Color(0f, 0f, 0f, 0.55f * alpha);
                if (c.Life <= 0f)
                {
                    Destroy(c.Back.gameObject);
                    captions.RemoveAt(i);
                }
            }

            foreach (Ripple r in ripples)
            {
                if (r.Age >= 1f) { r.Image.color = Color.clear; continue; }
                r.Age += dt / Mathf.Lerp(0.6f, 1.6f, r.Radius / 30f);
                float size = Mathf.Lerp(30f, 40f + r.Radius * 26f, Mathf.Sqrt(r.Age));
                r.Image.rectTransform.sizeDelta = Vector2.one * size;
                Color tint = r.Radius >= 12f ? UIKit.Rust : r.Radius >= 6f ? UIKit.Amber : UIKit.Ink;
                r.Image.color = new Color(tint.r, tint.g, tint.b, (1f - r.Age) * 0.55f);
            }

            if (photoCard.gameObject.activeSelf)
            {
                photoTimer -= dt;
                Vector2 target = photoTimer > 0f ? new Vector2(-48f, 60f) : new Vector2(460f, 60f);
                photoCard.anchoredPosition = Vector2.Lerp(photoCard.anchoredPosition, target, dt * 6f);
                if (photoTimer < -1f) photoCard.gameObject.SetActive(false);
            }

            PlayerController player = director.Player;
            if (player == null) return;
            SurveyCamera survey = director.Survey;
            PlayerLantern lantern = director.Lantern;

            float t = Mathf.Max(0f, director.TimeLeft);
            timer.text = $"{(int)(t / 60f):00}:{(int)(t % 60f):00}";
            timer.color = t < 120f ? Color.Lerp(UIKit.Rust, UIKit.Ink, Mathf.PingPong(Time.unscaledTime, 1f)) : UIKit.Ink;
            tollText.text = director.DeafTimeLeft > 0f ? $"THE BELL DROWNS ALL SOUND · {Mathf.CeilToInt(director.DeafTimeLeft)}" : "";

            int bonus = director.TotalFound - director.RequiredFound;
            evidence.text = $"EVIDENCE {director.RequiredFound}/{director.RequiredEvidence}" + (bonus > 0 ? $"  <color=#F0C36A>+{bonus}</color>" : "");
            evidence.color = director.RequiredFound >= director.RequiredEvidence ? UIKit.Amber : UIKit.Ink;
            timerLabel.text = director.RequiredFound >= director.RequiredEvidence ? "ROW OUT BEFORE THE DAWN BELL" : "UNTIL THE DAWN BELL";

            UIKit.SetBar(vitalityFill, player.Vitality / 100f);
            UIKit.SetBar(breathFill, player.BreathNormalized);
            UIKit.SetBar(lanternFill, lantern != null ? lantern.Charge / 100f : 0f);
            exposures.text = $"{survey.Exposures} / 12";
            flashText.text = survey.FlashOn ? "EXPOSURES · FLASH ON (R)" : "EXPOSURES · FLASH OFF (R)";
            for (int i = 0; i < 3; i++)
            {
                PickupKind kind = (PickupKind)i;
                int count = player.ToolCount(kind);
                string[] names = { "HANDBELL", "BOTTLE", "SALTS" };
                slots[i].text = $"{i + 1}  {names[i]}\n×{count}";
                slots[i].color = count > 0 ? UIKit.Ink : UIKit.Dim;
                slotBacks[i].color = player.SelectedTool == kind ? new Color(0.94f, 0.76f, 0.42f, 0.28f) : new Color(0f, 0f, 0f, 0.45f);
            }

            hideOverlay.SetActive(player.IsHiding);
            viewfinder.SetActive(survey.Raised);
            crosshair.enabled = !survey.Raised && !player.IsHiding;
            if (survey.Raised)
            {
                viewfinderInfo.text = $"SURVEY CAMERA   {survey.Exposures}/12   {(survey.FlashOn ? "FLASH" : "NO FLASH")}";
                inFrame.text = survey.InFrame != null ? $"EVIDENCE IN FRAME — {survey.InFrame.Title.ToUpperInvariant()}" + (survey.InFrame.Dark && !survey.FlashOn ? "  (too dark without flash)" : "") : "";
                UIKit.SetBar(whineFill, survey.WhineProgress);
            }

            prompt.text = player.Prompt ?? "";
            holdBar.SetActive(player.HoldProgress > 0f);
            UIKit.SetBar(holdFill, player.HoldProgress);
        }
    }
}
