using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Hushwater
{
    // Main menu, settings, pause, journal and end-of-night screens (design doc p.6 and p.9).
    public sealed class MenuController : MonoBehaviour
    {
        private NightDirector director;
        private Canvas canvas;
        private GameObject main;
        private GameObject settings;
        private GameObject pause;
        private GameObject journal;
        private GameObject end;
        private GameObject settingsReturn;
        private readonly List<Text> difficultyButtons = new List<Text>();
        private Text difficultyNote;
        private Text ripplesToggle;
        private Text captionsToggle;
        private RectTransform journalPhotos;
        private Text journalList;
        private Text endTitle;
        private Text endLine;
        private Text endStats;
        private Button endPrimary;
        private Text endPrimaryLabel;

        public bool SettingsOpen => settings.activeSelf;
        public bool JournalOpen => journal.activeSelf;

        public void Build(NightDirector owner)
        {
            director = owner;
            canvas = UIKit.Canvas("Menus", 20);
            canvas.transform.SetParent(transform, false);
            backdropImage = UIKit.Stretch("Backdrop", canvas.transform).gameObject.AddComponent<RawImage>();
            backdropImage.texture = UIKit.Gradient(new Color(0.02f, 0.03f, 0.035f, 0.94f), new Color(0.02f, 0.03f, 0.035f, 0.15f));
            backdropImage.raycastTarget = false;

            main = BuildMain();
            settings = BuildSettings();
            pause = BuildPause();
            journal = BuildJournal();
            end = BuildEnd();
            HideAll();
        }

        private RawImage backdropImage;

        private GameObject Screen(string name) => UIKit.Stretch(name, canvas.transform).gameObject;

        private GameObject BuildMain()
        {
            GameObject screen = Screen("Main menu");
            Transform t = screen.transform;
            Vector2 left = new Vector2(0f, 0.5f);
            UIKit.Label(t, "Hushwater", HushwaterAssets.TitleFont, 150, UIKit.Ink, TextAnchor.MiddleLeft, left, new Vector2(560f, 250f), new Vector2(1000f, 180f));
            UIKit.Label(t, "The water took the town. It did not take him.", HushwaterAssets.BodyFont, 24, UIKit.Dim, TextAnchor.MiddleLeft, left, new Vector2(560f, 150f), new Vector2(1000f, 40f));
            UIKit.Panel(t, new Color(UIKit.Amber.r, UIKit.Amber.g, UIKit.Amber.b, 0.6f), left, new Vector2(0f, 0.5f), new Vector2(110f, 110f), new Vector2(90f, 2f));
            UIKit.TextButton(t, "New Game", left, new Vector2(340f, 40f), () => director.NewGame(), 44);
            UIKit.TextButton(t, "Settings", left, new Vector2(340f, -30f), () => ShowSettings(main), 44);
            UIKit.TextButton(t, "Quit", left, new Vector2(340f, -100f), Quit, 44);
            UIKit.Label(t, "NIGHT ONE · MILLROW  —  playable prototype\nCreated by Youdahe & Ibraheem  ·  Models and textures: Poly Haven (CC0)", HushwaterAssets.BodyFont, 15, UIKit.Dim,
                TextAnchor.LowerLeft, new Vector2(0f, 0f), new Vector2(560f, 70f), new Vector2(1000f, 60f));

            string controls =
                "WASD  move        Shift  sprint\n" +
                "Ctrl / C  creep (toggle)\n" +
                "Space  hold breath (30 s)\n" +
                "E  interact · hide · hold to force\n" +
                "F  crank lantern (loud)\n" +
                "Right mouse  raise camera\n" +
                "Left mouse  photograph     R  flash\n" +
                "1–3  choose tool     Q  throw / use\n" +
                "Tab  journal         Esc  pause";
            Image card = UIKit.Panel(t, new Color(0f, 0f, 0f, 0.45f), new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(-60f, 60f), new Vector2(470f, 330f));
            UIKit.Label(card.transform, "CONTROLS", HushwaterAssets.BodyFont, 16, UIKit.Amber, TextAnchor.UpperLeft, new Vector2(0f, 1f), new Vector2(225f, -28f), new Vector2(410f, 24f));
            UIKit.Label(card.transform, controls, HushwaterAssets.BodyFont, 17, UIKit.Ink, TextAnchor.UpperLeft, new Vector2(0f, 1f), new Vector2(225f, -170f), new Vector2(410f, 260f));
            return screen;
        }

        private GameObject BuildSettings()
        {
            GameObject screen = Screen("Settings");
            Transform t = screen.transform;
            Vector2 left = new Vector2(0f, 0.5f);
            UIKit.Label(t, "Settings", HushwaterAssets.TitleFont, 84, UIKit.Ink, TextAnchor.MiddleLeft, left, new Vector2(560f, 330f), new Vector2(1000f, 110f));
            UIKit.Label(t, "DIFFICULTY — everyone hears differently", HushwaterAssets.BodyFont, 16, UIKit.Amber, TextAnchor.MiddleLeft, left, new Vector2(560f, 240f), new Vector2(1000f, 26f));
            string[] names = { "Still Water", "Standard", "Undertow" };
            for (int i = 0; i < 3; i++)
            {
                Difficulty d = (Difficulty)i;
                Button b = UIKit.TextButton(t, names[i], left, new Vector2(340f + i * 250f, 195f), () => { GameSettings.Difficulty = d; Refresh(); }, 34, TextAnchor.MiddleLeft, 240f);
                difficultyButtons.Add(b.GetComponent<Text>());
            }
            difficultyNote = UIKit.Label(t, "", HushwaterAssets.BodyFont, 16, UIKit.Dim, TextAnchor.UpperLeft, left, new Vector2(560f, 140f), new Vector2(1000f, 50f));

            float y = 70f;
            void SliderRow(string label, float min, float max, float value, UnityEngine.Events.UnityAction<float> change)
            {
                UIKit.Label(t, label, HushwaterAssets.BodyFont, 18, UIKit.Ink, TextAnchor.MiddleLeft, left, new Vector2(260f, y), new Vector2(400f, 30f));
                UIKit.Slider(t, left, new Vector2(400f, y), 380f, min, max, value, change);
                y -= 56f;
            }
            SliderRow("MASTER VOLUME", 0f, 1f, GameSettings.MasterVolume, v => GameSettings.MasterVolume = v);
            SliderRow("BRIGHTNESS", 0.6f, 1.6f, GameSettings.Brightness, v => GameSettings.Brightness = v);
            SliderRow("MOUSE SENSITIVITY", 0.5f, 5f, GameSettings.MouseSensitivity, v => GameSettings.MouseSensitivity = v);

            UIKit.Label(t, "ACCESSIBILITY", HushwaterAssets.BodyFont, 16, UIKit.Amber, TextAnchor.MiddleLeft, left, new Vector2(560f, y - 6f), new Vector2(1000f, 26f));
            ripplesToggle = UIKit.TextButton(t, "", left, new Vector2(340f, y - 50f), () => { GameSettings.SoundRipples = !GameSettings.SoundRipples; Refresh(); }, 28).GetComponent<Text>();
            captionsToggle = UIKit.TextButton(t, "", left, new Vector2(340f, y - 95f), () => { GameSettings.Captions = !GameSettings.Captions; Refresh(); }, 28).GetComponent<Text>();
            UIKit.TextButton(t, "Back", left, new Vector2(340f, y - 175f), CloseSettings, 40);
            return screen;
        }

        private GameObject BuildPause()
        {
            GameObject screen = Screen("Pause menu");
            Transform t = screen.transform;
            Vector2 left = new Vector2(0f, 0.5f);
            UIKit.Label(t, "Paused", HushwaterAssets.TitleFont, 96, UIKit.Ink, TextAnchor.MiddleLeft, left, new Vector2(560f, 220f), new Vector2(1000f, 120f));
            UIKit.Label(t, "He is frozen mid-step.", HushwaterAssets.BodyFont, 20, UIKit.Dim, TextAnchor.MiddleLeft, left, new Vector2(560f, 145f), new Vector2(1000f, 30f));
            UIKit.TextButton(t, "Resume", left, new Vector2(340f, 60f), () => director.Resume(), 42);
            UIKit.TextButton(t, "Journal & Map", left, new Vector2(340f, -5f), () => director.OpenJournal(), 42);
            UIKit.TextButton(t, "Settings", left, new Vector2(340f, -70f), () => ShowSettings(pause), 42);
            UIKit.TextButton(t, "Main Menu", left, new Vector2(340f, -135f), () => director.ReturnToMenu(), 42);
            UIKit.TextButton(t, "Quit", left, new Vector2(340f, -200f), Quit, 42);
            return screen;
        }

        private GameObject BuildJournal()
        {
            GameObject screen = Screen("Journal");
            Transform t = screen.transform;
            Image paper = UIKit.Panel(t, new Color(0.8f, 0.77f, 0.69f, 0.97f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(1500f, 860f));
            Color ink = new Color(0.15f, 0.13f, 0.11f);
            UIKit.Label(paper.transform, "Field Journal — N. Varga", HushwaterAssets.TitleFont, 48, ink, TextAnchor.UpperLeft, new Vector2(0f, 1f), new Vector2(390f, -50f), new Vector2(700f, 60f), false);
            journalList = UIKit.Label(paper.transform, "", HushwaterAssets.BodyFont, 18, ink, TextAnchor.UpperLeft, new Vector2(0f, 1f), new Vector2(330f, -400f), new Vector2(580f, 600f), false);
            journalPhotos = UIKit.Rect("Photos", paper.transform, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-40f, -40f), new Vector2(820f, 780f));
            UIKit.Label(paper.transform, "Tab or Esc to close", HushwaterAssets.BodyFont, 15, new Color(0.35f, 0.32f, 0.28f), TextAnchor.LowerLeft, new Vector2(0f, 0f), new Vector2(330f, 30f), new Vector2(580f, 24f), false);
            return screen;
        }

        private GameObject BuildEnd()
        {
            GameObject screen = Screen("End of night");
            Transform t = screen.transform;
            Vector2 c = new Vector2(0.5f, 0.5f);
            endTitle = UIKit.Label(t, "", HushwaterAssets.TitleFont, 110, UIKit.Ink, TextAnchor.MiddleCenter, c, new Vector2(0f, 200f), new Vector2(1600f, 140f));
            endLine = UIKit.Label(t, "", HushwaterAssets.BodyFont, 26, UIKit.Ink, TextAnchor.MiddleCenter, c, new Vector2(0f, 90f), new Vector2(1300f, 80f));
            endStats = UIKit.Label(t, "", HushwaterAssets.BodyFont, 20, UIKit.Dim, TextAnchor.UpperCenter, c, new Vector2(0f, -20f), new Vector2(1200f, 140f));
            endPrimary = UIKit.TextButton(t, "", c, new Vector2(0f, -170f), () => { }, 40, TextAnchor.MiddleCenter, 700f);
            endPrimaryLabel = endPrimary.GetComponent<Text>();
            UIKit.TextButton(t, "Main Menu", c, new Vector2(0f, -235f), () => director.ReturnToMenu(), 34, TextAnchor.MiddleCenter, 700f);
            return screen;
        }

        public void HideAll()
        {
            main.SetActive(false);
            settings.SetActive(false);
            pause.SetActive(false);
            journal.SetActive(false);
            end.SetActive(false);
            backdropImage.enabled = false;
        }

        public void ShowMain()
        {
            HideAll();
            main.SetActive(true);
            backdropImage.enabled = true;
        }

        public void ShowPause()
        {
            HideAll();
            pause.SetActive(true);
            backdropImage.enabled = true;
        }

        private void ShowSettings(GameObject from)
        {
            settingsReturn = from;
            HideAll();
            settings.SetActive(true);
            backdropImage.enabled = true;
            Refresh();
        }

        public void CloseSettings()
        {
            settings.SetActive(false);
            if (settingsReturn != null) settingsReturn.SetActive(true);
        }

        private void Refresh()
        {
            for (int i = 0; i < difficultyButtons.Count; i++)
            {
                bool selected = (int)GameSettings.Difficulty == i;
                difficultyButtons[i].color = selected ? UIKit.Amber : UIKit.Dim;
                difficultyButtons[i].fontStyle = selected ? FontStyle.Normal : FontStyle.Normal;
            }
            difficultyNote.text = GameSettings.Difficulty switch
            {
                Difficulty.StillWater => "Swipe 20 · full grab knocks you down (60) · 24 min to dawn · 4 lantern posts · he hears at 0.75×",
                Difficulty.Undertow => "Swipe 50 · a full grab loses the night · 14 min to dawn · 1 lantern post · he hears at 1.3×",
                _ => "Swipe 35 · full grab knocks you down (80) · 18 min to dawn · 3 lantern posts · he hears at 1.0×"
            } + "\n(Difficulty applies from the next New Game.)";
            ripplesToggle.text = "Sound ripples: " + (GameSettings.SoundRipples ? "ON" : "OFF");
            captionsToggle.text = "Captions: " + (GameSettings.Captions ? "ON" : "OFF");
        }

        public void ShowJournal()
        {
            HideAll();
            journal.SetActive(true);
            backdropImage.enabled = true;
            var sb = new System.Text.StringBuilder();
            sb.AppendLine("TONIGHT");
            sb.AppendLine($"Photograph {director.RequiredEvidence} pieces of evidence in Millrow,");
            sb.AppendLine("then row out before the dawn bell.");
            sb.AppendLine();
            sb.AppendLine("EVIDENCE");
            foreach (EvidenceSite site in director.Evidence)
            {
                string mark = site.Found ? "[x]" : "[ ]";
                string name = site.Found ? site.Title : site.Required ? "????" : "???? (something behind a barred door?)";
                sb.AppendLine($"{mark} {name}");
            }
            sb.AppendLine();
            sb.AppendLine("NOTES");
            sb.AppendLine("He is blind. He hears everything.");
            sb.AppendLine("2 sounds in 5 s and he comes to look.");
            sb.AppendLine("3 and he hunts.");
            sb.AppendLine("Slow knocks: there is time.");
            sb.AppendLine("Fast knocks: hide now.");
            sb.AppendLine("Hold your breath when he searches.");
            journalList.text = sb.ToString();

            foreach (Transform child in journalPhotos) Destroy(child.gameObject);
            int index = 0;
            foreach (NightDirector.Photo photo in director.Photos)
            {
                if (index >= 9) break;
                int col = index % 3, row = index / 3;
                Image frame = UIKit.Panel(journalPhotos, new Color(0.93f, 0.91f, 0.85f), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(col * 272f, -row * 258f), new Vector2(256f, 244f));
                frame.rectTransform.localRotation = Quaternion.Euler(0f, 0f, (index * 37 % 7 - 3) * 0.8f);
                RawImage image = UIKit.Rect("Image", frame.transform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -10f), new Vector2(236f, 177f)).gameObject.AddComponent<RawImage>();
                image.texture = photo.Image;
                image.color = photo.Blurry ? new Color(0.6f, 0.58f, 0.55f) : Color.white;
                UIKit.Label(frame.transform, photo.Title, HushwaterAssets.BodyFont, 14, new Color(0.15f, 0.13f, 0.11f), TextAnchor.MiddleLeft, new Vector2(0.5f, 0f), new Vector2(0f, 26f), new Vector2(236f, 40f), false);
                index++;
            }
            if (index == 0)
                UIKit.Label(journalPhotos, "No photographs yet.\nRaise the camera with the right mouse button.", HushwaterAssets.BodyFont, 20, new Color(0.3f, 0.27f, 0.23f), TextAnchor.UpperLeft, new Vector2(0f, 1f), new Vector2(300f, -60f), new Vector2(600f, 80f), false);
        }

        public void ShowEnd(bool won, string title, string line, string stats, string primary, UnityEngine.Events.UnityAction primaryAction)
        {
            HideAll();
            end.SetActive(true);
            backdropImage.enabled = true;
            endTitle.text = title;
            endTitle.color = won ? UIKit.Amber : UIKit.Rust;
            endLine.text = line;
            endStats.text = stats;
            endPrimaryLabel.text = primary;
            endPrimary.gameObject.SetActive(!string.IsNullOrEmpty(primary));
            endPrimary.onClick.RemoveAllListeners();
            endPrimary.onClick.AddListener(() => SoundFx.Play2D(ProceduralAudio.UiTick, 0.5f));
            endPrimary.onClick.AddListener(primaryAction);
        }

        private static void Quit()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }
    }
}
