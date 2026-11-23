using UnityEngine;

namespace Hushwater
{
    // The three difficulties from the design doc's "Gameplay Rules" and "Per-night pressure" tables.
    public enum Difficulty { StillWater, Standard, Undertow }

    // Player-facing settings. Static so they survive a scene reload between nights; stored in PlayerPrefs.
    public static class GameSettings
    {
        // What the menu has selected. It only takes effect when a night starts (see BeginNight).
        public static Difficulty Selected = Difficulty.Standard;
        // The difficulty the current night is actually running on.
        public static Difficulty Active { get; private set; } = Difficulty.Standard;

        // Settings screen from the doc: volume, accessibility toggles, brightness.
        public static bool SoundRipples = true;
        public static bool Captions = true;
        public static float MasterVolume = 0.8f;
        public static float Brightness = 1f;
        public static float LookSensitivity = 1f;
        // Ours: the real microphone as in-game noise, and the Sexton's dot on the minimap.
        public static bool MicEnabled = true;
        public static string MicDevice = "";
        public static float MicSensitivity = 1f;
        public static bool SextonOnMinimap = true;

        private static bool loaded;

        // Reads the saved settings once per session.
        public static void LoadPrefs()
        {
            if (loaded) return;
            loaded = true;
            Selected = (Difficulty)Mathf.Clamp(PlayerPrefs.GetInt("Hushwater.Difficulty", 1), 0, 2);
            SoundRipples = PlayerPrefs.GetInt("Hushwater.Ripples", 1) == 1;
            Captions = PlayerPrefs.GetInt("Hushwater.Captions", 1) == 1;
            MasterVolume = PlayerPrefs.GetFloat("Hushwater.Volume", 0.8f);
            Brightness = PlayerPrefs.GetFloat("Hushwater.Brightness", 1f);
            LookSensitivity = PlayerPrefs.GetFloat("Hushwater.Look", 1f);
            MicEnabled = PlayerPrefs.GetInt("Hushwater.Mic", 1) == 1;
            MicDevice = PlayerPrefs.GetString("Hushwater.MicDevice", "");
            MicSensitivity = PlayerPrefs.GetFloat("Hushwater.MicSensitivity", 1f);
            SextonOnMinimap = PlayerPrefs.GetInt("Hushwater.SextonDot", 1) == 1;
            Apply();
        }

        // Writes the settings and applies volume straight away.
        public static void SavePrefs()
        {
            PlayerPrefs.SetInt("Hushwater.Difficulty", (int)Selected);
            PlayerPrefs.SetInt("Hushwater.Ripples", SoundRipples ? 1 : 0);
            PlayerPrefs.SetInt("Hushwater.Captions", Captions ? 1 : 0);
            PlayerPrefs.SetFloat("Hushwater.Volume", MasterVolume);
            PlayerPrefs.SetFloat("Hushwater.Brightness", Brightness);
            PlayerPrefs.SetFloat("Hushwater.Look", LookSensitivity);
            PlayerPrefs.SetInt("Hushwater.Mic", MicEnabled ? 1 : 0);
            PlayerPrefs.SetString("Hushwater.MicDevice", MicDevice ?? "");
            PlayerPrefs.SetFloat("Hushwater.MicSensitivity", MicSensitivity);
            PlayerPrefs.SetInt("Hushwater.SextonDot", SextonOnMinimap ? 1 : 0);
            PlayerPrefs.Save();
            Apply();
        }

        private static void Apply()
        {
            AudioListener.volume = MasterVolume;
            // Flat ambient: in the default Skybox mode the colour below is ignored and Unity's bright default sky lights everything.
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.11f, 0.13f, 0.16f) * Brightness;
        }

        // Called at the start of every night to lock in the selected difficulty.
        public static void BeginNight() => Active = Selected;

        // Sexton hearing: 0.75x / 1.0x / 1.3x.
        public static float HearingMultiplier => Pick(0.75f, 1f, 1.3f);

        // Damage from one hook swipe: 20 / 35 / 50.
        public static float SwipeDamage => Pick(20f, 35f, 50f);

        // A full grab knocks Nell down for 60 / 80 Vitality; on Undertow it loses the night outright.
        public static float GrabDamage => Pick(60f, 80f, float.PositiveInfinity);

        // Time before the dawn bell: 24 / 18 / 14 minutes.
        public static float DawnSeconds => Pick(24f, 18f, 14f) * 60f;

        // Lantern posts per district: 4 / 3 / 1.
        public static int LanternPosts => (int)Pick(4f, 3f, 1f);

        // Chance that searching a supply cache finds something: 60% / 40% / 20%.
        public static float CacheChance => Pick(0.6f, 0.4f, 0.2f);

        // Silt Hands grip damage per second over the 4 s hold: 3 / 5 / 8.
        public static float SiltGripDamage => Pick(3f, 5f, 8f);

        // Hazards (falls, debris): 10 / 20 / 30.
        public static float HazardDamage => Pick(10f, 20f, 30f);

        // Display name, as written in the doc.
        public static string Name(Difficulty difficulty) =>
            difficulty == Difficulty.StillWater ? "Still Water" : difficulty == Difficulty.Standard ? "Standard" : "Undertow";

        // One line on what each difficulty changes, using the doc's numbers.
        public static string Blurb(Difficulty difficulty)
        {
            switch (difficulty)
            {
                case Difficulty.StillWater: return "Hearing 0.75×  ·  swipe 20  ·  a grab knocks you down  ·  24 min  ·  4 lantern posts";
                case Difficulty.Standard: return "Hearing 1.0×  ·  swipe 35  ·  a grab knocks you down  ·  18 min  ·  3 lantern posts";
                default: return "Hearing 1.3×  ·  swipe 50  ·  a grab ends the night  ·  14 min  ·  1 lantern post";
            }
        }

        // Returns the value for the active difficulty.
        private static float Pick(float stillWater, float standard, float undertow) =>
            Active == Difficulty.StillWater ? stillWater : Active == Difficulty.Standard ? standard : undertow;
    }
}
