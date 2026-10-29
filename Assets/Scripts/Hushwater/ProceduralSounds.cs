using UnityEngine;

namespace Hushwater
{
    // Placeholder sounds generated in code until the FMOD banks exist.
    public static class ProceduralSounds
    {
        private const int Rate = 44100;
        private static AudioClip knock;
        private static AudioClip step;
        private static AudioClip shatter;
        private static AudioClip sting;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => knock = step = shatter = sting = null;

        // The Sexton's knocking: two low tones that die away quickly.
        public static AudioClip Knock => knock != null ? knock : knock = Make("Knock", 0.5f, t =>
            (Mathf.Sin(2f * Mathf.PI * 180f * t) + 0.5f * Mathf.Sin(2f * Mathf.PI * 410f * t)) * Mathf.Exp(-t * 12f) * 0.6f);

        // A soft, muddy footstep: filtered noise with a fast decay.
        public static AudioClip Step => step != null ? step : step = Make("Step", 0.15f, t =>
            Noise() * Mathf.Exp(-t * 40f) * 0.5f + Mathf.Sin(2f * Mathf.PI * 70f * t) * Mathf.Exp(-t * 30f) * 0.4f);

        // Glass breaking: bright noise plus a few ringing partials.
        public static AudioClip Shatter => shatter != null ? shatter : shatter = Make("Shatter", 0.6f, t =>
            (Noise() * 0.6f + Mathf.Sin(2f * Mathf.PI * 3100f * t) * 0.3f + Mathf.Sin(2f * Mathf.PI * 4700f * t) * 0.2f) * Mathf.Exp(-t * 9f));

        // The kill sting: a detuned low drone that swells then cuts.
        public static AudioClip Sting => sting != null ? sting : sting = Make("Sting", 1.8f, t =>
            (Mathf.Sin(2f * Mathf.PI * 55f * t) + Mathf.Sin(2f * Mathf.PI * 58.5f * t) + 0.4f * Noise()) * Mathf.Clamp01(t * 3f) * Mathf.Exp(-t * 1.2f) * 0.5f);

        // Plays a clip at a point in the world (3D) without keeping an AudioSource around.
        public static void PlayAt(AudioClip clip, Vector3 position, float volume = 1f) => AudioSource.PlayClipAtPoint(clip, position, volume);

        private static float Noise() => Random.value * 2f - 1f;

        private static AudioClip Make(string name, float seconds, System.Func<float, float> wave)
        {
            float[] data = new float[(int)(Rate * seconds)];
            for (int i = 0; i < data.Length; i++) data[i] = wave(i / (float)Rate);
            AudioClip clip = AudioClip.Create(name, data.Length, 1, Rate, false);
            clip.SetData(data, 0);
            return clip;
        }
    }
}
