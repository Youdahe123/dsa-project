using UnityEngine;

namespace Hushwater
{
    public enum Difficulty { StillWater, Standard, Undertow }

    // Numbers come straight from the design doc's Gameplay Rules and Five Nights pages.
    public static class GameSettings
    {
        public static Difficulty Difficulty
        {
            get => (Difficulty)PlayerPrefs.GetInt("hw.difficulty", 1);
            set => PlayerPrefs.SetInt("hw.difficulty", (int)value);
        }

        public static float MasterVolume
        {
            get => PlayerPrefs.GetFloat("hw.volume", 0.85f);
            set { PlayerPrefs.SetFloat("hw.volume", value); AudioListener.volume = value; }
        }

        public static float Brightness
        {
            get => PlayerPrefs.GetFloat("hw.brightness", 1f);
            set => PlayerPrefs.SetFloat("hw.brightness", value);
        }

        public static float MouseSensitivity
        {
            get => PlayerPrefs.GetFloat("hw.mouse", 2f);
            set => PlayerPrefs.SetFloat("hw.mouse", value);
        }

        public static bool SoundRipples
        {
            get => PlayerPrefs.GetInt("hw.ripples", 1) == 1;
            set => PlayerPrefs.SetInt("hw.ripples", value ? 1 : 0);
        }

        public static bool Captions
        {
            get => PlayerPrefs.GetInt("hw.captions", 1) == 1;
            set => PlayerPrefs.SetInt("hw.captions", value ? 1 : 0);
        }

        public static string DifficultyName => Difficulty switch
        {
            Difficulty.StillWater => "Still Water",
            Difficulty.Undertow => "Undertow",
            _ => "Standard"
        };

        public static float SwipeDamage => Pick(20f, 35f, 50f);
        public static float GrabDamage => Pick(60f, 80f, float.PositiveInfinity);
        public static float SiltHandsDps => Pick(3f, 5f, 8f);
        public static float HearingMultiplier => Pick(0.75f, 1f, 1.3f);
        public static float NightSeconds => Pick(24f, 18f, 14f) * 60f;
        public static int LanternPosts => (int)Pick(4f, 3f, 1f);

        private static float Pick(float stillWater, float standard, float undertow) => Difficulty switch
        {
            Difficulty.StillWater => stillWater,
            Difficulty.Undertow => undertow,
            _ => standard
        };
    }
}
