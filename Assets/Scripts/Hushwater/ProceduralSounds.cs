using System.Collections.Generic;
using UnityEngine;

namespace Hushwater
{
    // Placeholder sounds generated in code until the FMOD banks exist.
    public static class ProceduralSounds
    {
        private const int Rate = 44100;
        private static readonly Dictionary<string, AudioClip> Cache = new Dictionary<string, AudioClip>();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => Cache.Clear();

        // The Sexton's knocking: two low tones that die away quickly.
        public static AudioClip Knock => Get("Knock", 0.5f, t =>
            (Sin(180f, t) + 0.5f * Sin(410f, t)) * Mathf.Exp(-t * 12f) * 0.6f);

        // A soft, muddy footstep: filtered noise with a fast decay.
        public static AudioClip Step => Get("Step", 0.15f, t =>
            Noise() * Mathf.Exp(-t * 40f) * 0.5f + Sin(70f, t) * Mathf.Exp(-t * 30f) * 0.4f);

        // A wet, sucking step in deep mud: lower and longer than a normal step.
        public static AudioClip MudStep => Get("MudStep", 0.3f, t =>
            Noise() * Mathf.Exp(-t * 18f) * 0.35f + Sin(55f + 40f * t, t) * Mathf.Exp(-t * 12f) * 0.5f);

        // Glass breaking: bright noise plus a few ringing partials.
        public static AudioClip Shatter => Get("Shatter", 0.6f, t =>
            (Noise() * 0.6f + Sin(3100f, t) * 0.3f + Sin(4700f, t) * 0.2f) * Mathf.Exp(-t * 9f));

        // The kill sting: a detuned low drone that swells then cuts.
        public static AudioClip Sting => Get("Sting", 1.8f, t =>
            (Sin(55f, t) + Sin(58.5f, t) + 0.4f * Noise()) * Mathf.Clamp01(t * 3f) * Mathf.Exp(-t * 1.2f) * 0.5f);

        // The chapel bell he tolls: inharmonic partials with a long ring.
        public static AudioClip Bell => Get("Bell", 3.5f, t =>
            (Sin(196f, t) + 0.6f * Sin(392f * 1.19f, t) + 0.4f * Sin(196f * 2.76f, t) + 0.25f * Sin(196f * 5.4f, t))
            * Mathf.Exp(-t * 1.1f) * Mathf.Clamp01(t * 200f) * 0.3f);

        // A small brass handbell: bright and short.
        public static AudioClip Handbell => Get("Handbell", 0.9f, t =>
            (Sin(1320f, t) + 0.5f * Sin(1320f * 2.4f, t) + 0.3f * Sin(1320f * 3.9f, t)) * Mathf.Exp(-t * 5f) * 0.3f);

        // Iron dragging on stone: a noisy scrape with a wobbling pitch.
        public static AudioClip Scrape => Get("Scrape", 0.9f, t =>
            (Noise() * 0.5f + Sin(900f + 200f * Mathf.Sin(t * 30f), t) * 0.15f) * Mathf.Sin(Mathf.PI * t / 0.9f) * 0.4f);

        // One bar of a funeral hymn, hummed low as he gives up the chase.
        public static AudioClip Hymn => Get("Hymn", 4f, t =>
        {
            float[] notes = { 146.8f, 164.8f, 174.6f, 164.8f, 146.8f, 130.8f, 146.8f, 146.8f };
            int index = Mathf.Min(notes.Length - 1, (int)(t / 0.5f));
            float inNote = t - index * 0.5f;
            float envelope = Mathf.Clamp01(inNote * 10f) * Mathf.Clamp01((0.5f - inNote) * 10f);
            return (Sin(notes[index], t) + 0.3f * Sin(notes[index] * 2f, t)) * envelope * 0.3f;
        });

        // The lantern post crank: a ratchet of quick metal clicks.
        public static AudioClip Crank => Get("Crank", 0.5f, t =>
            Noise() * Mathf.Exp(-(t % 0.08f) * 90f) * 0.45f);

        // A door giving way: a heavy thud and splintering wood.
        public static AudioClip Crash => Get("Crash", 1.2f, t =>
            (Sin(60f, t) * Mathf.Exp(-t * 6f) + Noise() * Mathf.Exp(-t * 3.5f) * 0.6f) * 0.7f);

        // A sharp sniff of smelling salts.
        public static AudioClip Sniff => Get("Sniff", 0.35f, t =>
            Noise() * Mathf.Sin(Mathf.PI * t / 0.35f) * 0.35f);

        // A loud gasp when held breath runs out.
        public static AudioClip GaspSound => Get("Gasp", 0.6f, t =>
            Noise() * Mathf.Sin(Mathf.PI * t / 0.6f) * (0.5f + 0.3f * Sin(320f, t)));

        // Warped wood groaning: a slow, wavering low tone with grit.
        public static AudioClip Creak => Get("Creak", 0.7f, t =>
            (Sin(110f + 60f * Mathf.Sin(t * 9f), t) * 0.4f + Noise() * 0.15f) * Mathf.Sin(Mathf.PI * t / 0.7f) * 0.5f);

        // Water bursting through the spillway: a long roar of low noise.
        public static AudioClip Roar => Get("Roar", 4f, t =>
            (Noise() * 0.7f + Sin(40f, t) * 0.3f) * Mathf.Clamp01(t * 2f) * Mathf.Clamp01((4f - t) * 0.8f) * 0.6f);

        // The survey camera's shutter: two quick clicks.
        public static AudioClip Shutter => Get("Shutter", 0.12f, t =>
            Noise() * (Mathf.Exp(-t * 200f) + Mathf.Exp(-Mathf.Max(0f, t - 0.06f) * 200f) * (t > 0.06f ? 1f : 0f)) * 0.6f);

        // The flash: a sharp pop.
        public static AudioClip FlashPop => Get("FlashPop", 0.25f, t =>
            (Noise() * 0.8f + Sin(2400f, t) * 0.3f) * Mathf.Exp(-t * 25f) * 0.7f);

        // The flash charging: a thin whine that rises in pitch over 2 s.
        public static AudioClip Whine => Get("Whine", 2f, t =>
            Mathf.Sin(2f * Mathf.PI * (900f * t + 900f * t * t)) * 0.12f * Mathf.Clamp01(t * 8f));

        // A Congregation figure's wail: a rising, breathy human-ish moan.
        public static AudioClip Wail => Get("Wail", 2.2f, t =>
            (Sin(240f + 140f * t, t) * 0.4f + Sin(361f + 150f * t, t) * 0.2f + Noise() * 0.15f) * Mathf.Sin(Mathf.PI * t / 2.2f) * 0.5f);

        // Lantern moths: a dry, fluttering hiss.
        public static AudioClip Hiss => Get("Hiss", 1f, t =>
            Noise() * (0.5f + 0.5f * Mathf.Sin(t * 120f)) * Mathf.Sin(Mathf.PI * t) * 0.25f);

        // A thick, wet squelch: the silt hands gripping, and Nell pulling against them.
        public static AudioClip Squelch => Get("Squelch", 0.4f, t =>
            (Noise() * 0.4f + Sin(80f - 50f * t, t) * 0.5f) * Mathf.Exp(-t * 8f) * 0.6f);

        // Breaking the surface of a flooded passage.
        public static AudioClip Splash => Get("Splash", 0.8f, t =>
            Noise() * Mathf.Exp(-t * 5f) * 0.5f);

        // The Night 5 generator: a loopable mains hum with a rattle.
        public static AudioClip Hum => Get("Hum", 1f, t =>
            (Sin(50f, t) * 0.5f + Sin(100f, t) * 0.3f + Sin(150f, t) * 0.1f + Noise() * 0.05f) * 0.5f);

        // A single drip of water somewhere nearby.
        public static AudioClip Drip => Get("Drip", 0.25f, t =>
            Sin(1400f - 2400f * t, t) * Mathf.Exp(-t * 30f) * 0.3f);

        // Loopable wind through broken windows.
        public static AudioClip Wind => Get("Wind", 4f, t =>
        {
            float loop = Mathf.Sin(Mathf.PI * t / 4f);
            return Noise() * (0.08f + 0.05f * Mathf.Sin(t * 1.3f)) * (0.6f + 0.4f * loop);
        });

        // Design doc: "Chases get a score: a single hymn on a waterlogged, out-of-tune organ." Loopable, 8 s.
        public static AudioClip Organ => Get("Organ", 8f, t =>
        {
            float[] chords = { 146.8f, 130.8f, 116.5f, 110f };
            int index = Mathf.Min(chords.Length - 1, (int)(t / 2f));
            float root = chords[index] * (1f + 0.012f * Mathf.Sin(t * 0.7f)); // waterlogged: slowly out of tune
            float inChord = t - index * 2f;
            float envelope = Mathf.Clamp01(inChord * 4f) * Mathf.Clamp01((2f - inChord) * 4f);
            return (Sin(root, t) + 0.6f * Sin(root * 1.19f, t) + 0.5f * Sin(root * 1.5f, t) + 0.3f * Sin(root * 2.02f, t)) * envelope * 0.12f;
        });

        // Tape hiss for Emil's recordings at the boat.
        public static AudioClip TapeHiss => Get("TapeHiss", 3f, t => Noise() * 0.06f * Mathf.Clamp01(t * 4f) * Mathf.Clamp01((3f - t) * 4f));

        // Plays a clip at a point in the world (3D) without keeping an AudioSource around.
        public static void PlayAt(AudioClip clip, Vector3 position, float volume = 1f) => AudioSource.PlayClipAtPoint(clip, position, volume);

        // Sine wave at a frequency.
        private static float Sin(float hz, float t) => Mathf.Sin(2f * Mathf.PI * hz * t);

        // White noise sample between -1 and 1.
        private static float Noise() => Random.value * 2f - 1f;

        // Builds a clip the first time it is asked for, then reuses it.
        private static AudioClip Get(string name, float seconds, System.Func<float, float> wave)
        {
            if (Cache.TryGetValue(name, out AudioClip clip) && clip != null) return clip;
            return Cache[name] = Make(name, seconds, wave);
        }

        // Samples a wave function (time in seconds -> -1..1) into a mono AudioClip.
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
