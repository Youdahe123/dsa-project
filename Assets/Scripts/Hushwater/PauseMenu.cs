using UnityEngine;
using UnityEngine.UI;

namespace Hushwater
{
    // The Esc menu. Design doc: "Esc pauses the game at any time and freezes the Sexton mid-step."
    // "Pause menu: Resume, Journal & Map, Settings, Main Menu, Quit." Restart night is ours, for playtesting.
    // Also owns the journal, which Tab opens straight from play.
    public sealed class PauseMenu : MonoBehaviour
    {
        private const string Controls =
            "WASD / left stick  move\n" +
            "Shift / L3  sprint (18 m)\n" +
            "Ctrl or C / B  crouch and creep (2 m)\n" +
            "Space / RB  hold your breath (30 s)\n" +
            "E / A  pick up · hide · hold to force, search, row out\n" +
            "F / Y  hold to crank the lantern (12 m) or a lantern post\n" +
            "L / R3  turn the lantern down to nothing, or back up\n" +
            "Right mouse / LT  raise the camera\n" +
            "Left mouse / RT  take a photo (4 m, 20 m with flash)\n" +
            "R / X  flash on or off\n" +
            "1-4, scroll / D-pad  choose a tool\n" +
            "Q / LB  throw or use the tool\n" +
            "Tab / View  journal & map\n" +
            "F3  noise debug view";

        private NightDirector director;
        private GameObject panel;
        private Button resume;
        private SettingsPanel settings;
        private JournalView journal;

        public void Build(Transform canvas, NightDirector night)
        {
            director = night;
            Image backdrop = UiKit.Overlay(canvas, UiKit.Fade(UiKit.Night, 0.85f));
            backdrop.raycastTarget = true;
            panel = backdrop.gameObject;
            Transform p = panel.transform;

            Vector2 centre = new Vector2(0.5f, 0.5f);
            UiKit.Text(p, "HUSHWATER", 54, centre, new Vector2(-300f, 330f), TextAnchor.MiddleCenter, new Vector2(600f, 70f), UiKit.Paper);
            UiKit.Text(p, $"Paused. He is frozen mid-step.  ·  Night {Campaign.Night}", 18, centre, new Vector2(-300f, 280f), TextAnchor.MiddleCenter, new Vector2(600f, 30f), UiKit.Fade(UiKit.Paper, 0.7f));

            resume = UiKit.Button(p, "Resume", new Vector2(-300f, 190f), new Vector2(420f, 56f), () => director.SetPaused(false));
            UiKit.Button(p, "Journal & Map", new Vector2(-300f, 120f), new Vector2(420f, 56f), () => director.SetJournal(true));
            UiKit.Button(p, "Settings", new Vector2(-300f, 50f), new Vector2(420f, 56f), () => settings.Show(true));
            UiKit.Button(p, "Restart night (from the boat)", new Vector2(-300f, -20f), new Vector2(420f, 56f), () => director.Restart());
            UiKit.Button(p, "Main Menu", new Vector2(-300f, -90f), new Vector2(420f, 56f), () => director.MainMenu());
            UiKit.Button(p, "Quit", new Vector2(-300f, -160f), new Vector2(420f, 56f), NightDirector.Quit);
            UiKit.Text(p, "Leaving the night loses anything found tonight. The boat is where the game saves.", 16, centre,
                new Vector2(-300f, -230f), TextAnchor.UpperCenter, new Vector2(520f, 60f), UiKit.Fade(UiKit.Lantern, 0.85f));

            UiKit.Text(p, "CONTROLS", 18, centre, new Vector2(330f, 340f), TextAnchor.UpperLeft, new Vector2(560f, 30f), UiKit.Lantern);
            UiKit.Text(p, Controls, 17, centre, new Vector2(330f, 120f), TextAnchor.UpperLeft, new Vector2(560f, 420f), UiKit.Paper).lineSpacing = 1.2f;
            UiKit.Text(p, "Every sound you make has a radius. Two sounds in five seconds and he comes to look. Three and he hunts.\n" +
                          "Slow knocks mean there is time. Fast knocks mean hide now.",
                16, centre, new Vector2(330f, -190f), TextAnchor.UpperLeft, new Vector2(560f, 120f), UiKit.Fade(UiKit.Paper, 0.7f));

            settings = new SettingsPanel(canvas, () => UiKit.Select(resume));
            journal = new JournalView(canvas, () => director.SetJournal(false));
            panel.SetActive(false);
            director.PauseChanged += OnPauseChanged;
            director.JournalChanged += OnJournalChanged;
        }

        private void OnDestroy()
        {
            if (director == null) return;
            director.PauseChanged -= OnPauseChanged;
            director.JournalChanged -= OnJournalChanged;
        }

        private void OnPauseChanged(bool paused)
        {
            panel.SetActive(paused);
            if (!paused && settings.Visible) settings.Show(false);
            if (paused) UiKit.Select(resume);
        }

        private void OnJournalChanged(bool open) => journal.Show(open);
    }
}
