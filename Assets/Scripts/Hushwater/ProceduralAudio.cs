using System;
using System.Collections.Generic;
using UnityEngine;

namespace Hushwater
{
    // Every sound in the demo is synthesised here, so the prototype ships with no audio files.
    public static class ProceduralAudio
    {
        private const int Rate = 44100;
        private static readonly Dictionary<string, AudioClip> Cache = new Dictionary<string, AudioClip>();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => Cache.Clear();

        private static AudioClip Make(string name, float seconds, Func<float, System.Random, float> sample, int seed = 7)
        {
            if (Cache.TryGetValue(name, out AudioClip cached)) return cached;
            int count = Mathf.CeilToInt(seconds * Rate);
            float[] data = new float[count];
            var random = new System.Random(seed);
            float peak = 0.0001f;
            for (int i = 0; i < count; i++)
            {
                data[i] = sample(i / (float)Rate, random);
                peak = Mathf.Max(peak, Mathf.Abs(data[i]));
            }
            float gain = 0.9f / peak;
            for (int i = 0; i < count; i++) data[i] *= gain;
            AudioClip clip = AudioClip.Create(name, count, 1, Rate, false);
            clip.SetData(data, 0);
            Cache[name] = clip;
            return clip;
        }

        private static float Noise(System.Random r) => (float)(r.NextDouble() * 2.0 - 1.0);
        private static float Sine(float hz, float t) => Mathf.Sin(2f * Mathf.PI * hz * t);

        // One-pole low pass state shared by the closures below.
        private sealed class Filter
        {
            private float state;
            public float LowPass(float input, float amount) => state += (input - state) * amount;
        }

        public static AudioClip Knock => Make("knock", 0.7f, (t, r) =>
        {
            float body = Sine(176f, t) * Mathf.Exp(-t * 9f) + 0.6f * Sine(413f, t) * Mathf.Exp(-t * 14f)
                       + 0.4f * Sine(697f, t) * Mathf.Exp(-t * 20f) + 0.25f * Sine(1131f, t) * Mathf.Exp(-t * 28f);
            float click = Noise(r) * Mathf.Exp(-t * 180f);
            return body + click * 0.8f;
        });

        public static AudioClip Toll => Make("toll", 6f, (t, r) =>
        {
            float[] partials = { 0.5f, 1f, 1.19f, 1.5f, 2f, 2.74f, 3.01f, 4.07f };
            float sum = 0f;
            for (int i = 0; i < partials.Length; i++)
                sum += Sine(98f * partials[i], t) * Mathf.Exp(-t * (0.5f + i * 0.35f)) / (1f + i * 0.4f);
            float strike = Noise(r) * Mathf.Exp(-t * 60f) * 0.5f;
            return (sum + strike) * Mathf.Min(1f, t * 400f);
        });

        public static AudioClip Handbell => Make("handbell", 1.4f, (t, r) =>
        {
            float ring = 0f;
            float local = t % 0.35f;
            float[] partials = { 1f, 2.32f, 4.25f, 6.63f };
            for (int i = 0; i < partials.Length; i++) ring += Sine(1320f * partials[i], t) * Mathf.Exp(-local * (6f + i * 4f)) / (1 + i);
            return ring;
        });

        public static AudioClip Scrape
        {
            get
            {
                var f = new Filter();
                return Make("scrape", 2.4f, (t, r) =>
                {
                    float grit = f.LowPass(Noise(r), 0.25f);
                    float rasp = 0.5f + 0.5f * Mathf.PerlinNoise(t * 9f, 0.3f);
                    float edge = Mathf.Min(1f, t * 3f) * Mathf.Min(1f, (2.4f - t) * 3f);
                    return grit * rasp * edge;
                });
            }
        }

        public static AudioClip Hum => Make("hum", 3.4f, (t, r) =>
        {
            float[] notes = { 146.8f, 164.8f, 138.6f, 130.8f };
            int index = Mathf.Min(notes.Length - 1, (int)(t / 0.85f));
            float local = t - index * 0.85f;
            float vibrato = 1f + 0.01f * Sine(5.2f, t);
            float hz = notes[index] * vibrato;
            float voice = Sine(hz, t) + 0.45f * Sine(hz * 2f, t) + 0.2f * Sine(hz * 3f, t);
            float env = Mathf.Min(1f, local * 8f) * Mathf.Min(1f, (0.85f - local) * 6f + 0.2f) * Mathf.Min(1f, (3.4f - t) * 2f);
            return voice * env;
        });

        public static AudioClip Organ => Make("organ", 8f, (t, r) =>
        {
            // A minor-key hymn on a drowned, out-of-tune organ.
            float[] chord = t < 4f ? new[] { 110f, 130.8f, 164.8f } : new[] { 98f, 116.5f, 146.8f };
            float sum = 0f;
            foreach (float hz in chord)
            {
                float detune = hz * 1.013f;
                for (int h = 1; h <= 4; h++) sum += (Sine(hz * h, t) + Sine(detune * h, t)) / (h * h);
            }
            float swell = 0.6f + 0.4f * Sine(0.25f, t);
            float edge = Mathf.Min(1f, t * 4f) * Mathf.Min(1f, (8f - t) * 4f);
            return sum * swell * edge;
        });

        public static AudioClip Wind
        {
            get
            {
                var f = new Filter();
                var g = new Filter();
                return Make("wind", 10f, (t, r) =>
                {
                    float gust = 0.35f + 0.65f * Mathf.PerlinNoise(t * 0.35f, 1.7f);
                    float low = g.LowPass(f.LowPass(Noise(r), 0.02f), 0.2f);
                    float whistle = 0.04f * Sine(620f + 80f * Mathf.PerlinNoise(t * 0.5f, 4f), t) * gust;
                    float edge = Mathf.Min(1f, t) * Mathf.Min(1f, 10f - t);
                    return (low * 3f + whistle) * gust * edge;
                }, 11);
            }
        }

        public static AudioClip Lapping
        {
            get
            {
                var f = new Filter();
                return Make("lapping", 8f, (t, r) =>
                {
                    float wave = Mathf.Pow(Mathf.Max(0f, Sine(0.42f, t + 0.3f * Mathf.PerlinNoise(t * 0.2f, 0f))), 3f);
                    float edge = Mathf.Min(1f, t) * Mathf.Min(1f, 8f - t);
                    return f.LowPass(Noise(r), 0.08f) * (0.15f + wave) * edge;
                }, 21);
            }
        }

        public static AudioClip Drip(int variant) => Make("drip" + variant, 0.25f, (t, r) =>
        {
            float hz = 900f + variant * 260f - t * 2400f;
            return Sine(Mathf.Max(200f, hz), t) * Mathf.Exp(-t * 30f);
        }, 30 + variant);

        public static AudioClip Footstep(int variant, bool mud)
        {
            var f = new Filter();
            return Make($"step{variant}{mud}", 0.32f, (t, r) =>
            {
                float thud = Sine(70f + variant * 6f, t) * Mathf.Exp(-t * 25f);
                float body = f.LowPass(Noise(r), mud ? 0.08f : 0.3f) * Mathf.Exp(-t * (mud ? 12f : 30f));
                float squelch = mud ? 0.35f * Sine(180f + 120f * Mathf.Exp(-t * 10f), t) * Mathf.Exp(-t * 14f) * Mathf.Min(1f, t * 40f) : 0f;
                return thud * 0.8f + body * 1.4f + squelch;
            }, 40 + variant + (mud ? 10 : 0));
        }

        public static AudioClip HeavyStep(int variant)
        {
            var f = new Filter();
            return Make("heavy" + variant, 0.5f, (t, r) =>
                Sine(48f + variant * 4f, t) * Mathf.Exp(-t * 12f) + f.LowPass(Noise(r), 0.05f) * Mathf.Exp(-t * 9f) * 2f, 60 + variant);
        }

        public static AudioClip Shutter => Make("shutter", 0.22f, (t, r) =>
            Noise(r) * (Mathf.Exp(-t * 300f) + 0.7f * Mathf.Exp(-Mathf.Max(0f, t - 0.09f) * 260f) * (t > 0.09f ? 1f : 0f)));

        public static AudioClip FlashWhine => Make("whine", 1.2f, (t, r) =>
            Sine(1800f + t * t * 3500f, t) * Mathf.Min(1f, t * 3f) * 0.6f);

        public static AudioClip FlashPop => Make("pop", 0.3f, (t, r) => Noise(r) * Mathf.Exp(-t * 40f));

        public static AudioClip Gasp
        {
            get
            {
                var f = new Filter();
                return Make("gasp", 1.1f, (t, r) =>
                {
                    float env = t < 0.35f ? Mathf.Sin(t / 0.35f * Mathf.PI) : 0.6f * Mathf.Sin(Mathf.Clamp01((t - 0.4f) / 0.7f) * Mathf.PI);
                    float breath = f.LowPass(Noise(r), t < 0.35f ? 0.35f : 0.15f);
                    float voice = t < 0.35f ? 0.2f * Sine(320f + t * 300f, t) : 0f;
                    return (breath + voice) * env;
                });
            }
        }

        public static AudioClip Heartbeat => Make("heart", 0.9f, (t, r) =>
            Sine(52f, t) * (Mathf.Exp(-t * 18f) + 0.7f * Mathf.Exp(-Mathf.Max(0f, t - 0.22f) * 18f) * (t > 0.22f ? 1f : 0f)));

        public static AudioClip Crank => Make("crank", 0.8f, (t, r) =>
        {
            float tick = (t * 14f) % 1f;
            return Noise(r) * Mathf.Exp(-tick * 25f) * 0.7f + Sine(2400f, t) * Mathf.Exp(-tick * 40f) * 0.3f;
        });

        public static AudioClip Smash => Make("smash", 1f, (t, r) =>
        {
            float crack = Noise(r) * Mathf.Exp(-t * 18f);
            float glass = (Sine(3100f, t) + Sine(4370f, t) + Sine(5900f, t)) * Mathf.Exp(-t * 8f) * 0.25f * (r.NextDouble() > 0.6 ? 1f : 0.2f);
            return crack + glass;
        });

        public static AudioClip DoorForce
        {
            get
            {
                var f = new Filter();
                return Make("door", 1.4f, (t, r) =>
                {
                    float creak = Sine(140f + 60f * Mathf.PerlinNoise(t * 6f, 2f), t) * 0.4f * Mathf.Exp(-t * 1.5f);
                    float splinter = f.LowPass(Noise(r), 0.4f) * (t > 0.7f ? Mathf.Exp(-(t - 0.7f) * 8f) * 2f : 0.1f);
                    return creak + splinter;
                });
            }
        }

        public static AudioClip UiTick => Make("tick", 0.06f, (t, r) => Sine(1800f, t) * Mathf.Exp(-t * 90f));

        public static AudioClip Sting => Make("sting", 2.5f, (t, r) =>
        {
            float cluster = Sine(220f, t) + Sine(233f, t) + Sine(311f, t) * 0.6f + Noise(r) * 0.15f;
            return cluster * Mathf.Exp(-t * 1.5f) * Mathf.Min(1f, t * 30f);
        });
    }

    public static class SoundFx
    {
        public static AudioSource PlayAt(AudioClip clip, Vector3 position, float volume = 1f, float maxDistance = 30f, float pitch = 1f)
        {
            GameObject go = new GameObject("Sfx " + clip.name);
            go.transform.position = position;
            AudioSource source = go.AddComponent<AudioSource>();
            source.clip = clip;
            source.volume = volume;
            source.pitch = pitch;
            source.spatialBlend = 1f;
            source.rolloffMode = AudioRolloffMode.Linear;
            source.minDistance = 1f;
            source.maxDistance = maxDistance;
            source.dopplerLevel = 0f;
            source.Play();
            UnityEngine.Object.Destroy(go, clip.length / Mathf.Max(0.1f, pitch) + 0.1f);
            return source;
        }

        public static void Play2D(AudioClip clip, float volume = 1f, float pitch = 1f)
        {
            GameObject go = new GameObject("Sfx2D " + clip.name);
            AudioSource source = go.AddComponent<AudioSource>();
            source.clip = clip;
            source.volume = volume;
            source.pitch = pitch;
            source.spatialBlend = 0f;
            source.ignoreListenerPause = true;
            source.Play();
            UnityEngine.Object.Destroy(go, clip.length / Mathf.Max(0.1f, pitch) + 0.1f);
        }

        public static AudioSource Loop(GameObject owner, AudioClip clip, float volume, float maxDistance, bool spatial = true)
        {
            AudioSource source = owner.AddComponent<AudioSource>();
            source.clip = clip;
            source.loop = true;
            source.volume = volume;
            source.spatialBlend = spatial ? 1f : 0f;
            source.rolloffMode = AudioRolloffMode.Linear;
            source.minDistance = 1f;
            source.maxDistance = maxDistance;
            source.dopplerLevel = 0f;
            source.Play();
            return source;
        }
    }
}
