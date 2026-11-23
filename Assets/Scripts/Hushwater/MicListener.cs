using UnityEngine;

namespace Hushwater
{
    // Real-world sound as game noise (ours, not the doc's). Reads the system microphone (AirPods, a headset or the
    // built-in mic, whichever is picked in Settings) and turns how loud the room is into a noise radius at Nell's
    // position: breathing a few metres, talking 10-15 m, shouting 30 m. The Sexton hears it like any other sound.
    // Flow: Begin() picks and starts the mic once per night; Update() measures, calibrates, converts and emits.
    public sealed class MicListener : MonoBehaviour
    {
        private const int Window = 1024;         // ~20-60 ms of audio per reading, depending on the device
        private const float MaxRadius = 30f;     // a shout: as loud as a gasp
        private const float MinRadius = 1.5f;    // quieter than this is ignored
        private const float EmitEvery = 0.6f;    // repeated sounds still trip his 2-and-3 rule

        public static MicListener Instance { get; private set; }

        // 0..1 for the HUD bar, and the radius the current sound carries (0 when quiet).
        public float Level { get; private set; }
        // 0..1: how loud the raw input is over the room's background, even when too quiet to count.
        public float Input { get; private set; }
        // The level (0..1 on the same scale as Input) where sound starts to count as noise.
        public static float Threshold => Margin / InputSpan;
        private const float Margin = 5f, InputSpan = 45f;
        public float Radius { get; private set; }
        public bool Active => clip != null;
        public string Status { get; private set; } = "";

        private AudioClip clip;
        private string device;
        private readonly float[] samples = new float[Window];
        private float floorDb = 0f;        // snaps down to the room on the first reading
        private float listening;
        private float silentFor;
        private string deviceName;
        private float peakRadius;
        private float emitTimer;
        private PlayerController player;

        // Picks the device, asks for a sample rate it supports, and records into a looping 1 s buffer.
        public void Begin(PlayerController target)
        {
            Instance = this;
            player = target;
            if (!GameSettings.MicEnabled) { Status = "Microphone off (Settings)"; return; }
            if (Microphone.devices.Length == 0) { Status = "No microphone found"; return; }
            device = PickDevice();
            deviceName = device ?? Microphone.devices[0];
            // Ask for a rate the device actually supports (AirPods in headset mode only do 16 or 24 kHz).
            Microphone.GetDeviceCaps(device, out int min, out int max);
            int rate = max == 0 ? 44100 : Mathf.Clamp(44100, min, max);
            clip = Microphone.Start(device, true, 1, rate);
            Status = clip == null ? "Microphone blocked: allow Unity in macOS Privacy & Security > Microphone" : deviceName;
            Debug.Log($"[Hushwater mic] devices: {string.Join(", ", Microphone.devices)}; using '{deviceName}' at {rate} Hz (caps {min}-{max})");
        }

        // The device chosen in Settings; on "System default" an AirPods/headset mic is preferred if one is connected.
        private static string PickDevice()
        {
            string[] devices = Microphone.devices;
            if (!string.IsNullOrEmpty(GameSettings.MicDevice) && System.Array.IndexOf(devices, GameSettings.MicDevice) >= 0) return GameSettings.MicDevice;
            foreach (string d in devices)
                if (d.IndexOf("AirPods", System.StringComparison.OrdinalIgnoreCase) >= 0) return d;
            foreach (string d in devices)
                if (d.IndexOf("Headset", System.StringComparison.OrdinalIgnoreCase) >= 0 || d.IndexOf("Hands-Free", System.StringComparison.OrdinalIgnoreCase) >= 0) return d;
            return null;
        }

        private void OnDestroy()
        {
            if (clip != null) Microphone.End(device);
            if (Instance == this) Instance = null;
        }

        private void Update()
        {
            if (clip == null) return;
            // 1. Measure: RMS of the newest samples, converted to decibels.
            int position = Microphone.GetPosition(device);
            if (position < Window) return; // just wrapped round; read next frame
            clip.GetData(samples, position - Window);

            float sum = 0f;
            for (int i = 0; i < Window; i++) sum += samples[i] * samples[i];
            float db = 20f * Mathf.Log10(Mathf.Sqrt(sum / Window) + 1e-6f);
            // Pure digital silence (the mic still waking up, muted, or macOS blocking it) is no reading at all.
            if (db < -90f)
            {
                silentFor += Time.unscaledDeltaTime;
                if (silentFor > 3f) Status = $"No sound from {deviceName}: allow Unity in macOS Privacy & Security > Microphone, then restart Unity";
                Input = 0f;
                return;
            }
            silentFor = 0f;
            Status = deviceName;

            // 2. Calibrate: the room's own hum sets the floor: it drops straight to any quieter reading and creeps up slowly,
            // so a fan or traffic doesn't count as Nell making noise.
            // For the first 3 s it settles quickly (calibrating to the room), then drifts at 0.5 dB a second.
            listening += Time.unscaledDeltaTime;
            float creep = listening < 3f ? 6f : 0.5f;
            floorDb = Mathf.Clamp(Mathf.Min(floorDb + creep * Time.unscaledDeltaTime, db), -75f, -30f);
            // 3. Convert: decibels above the floor become a noise radius in metres.
            Input = Mathf.Clamp01((db - floorDb) / InputSpan);
            float above = db - floorDb - Margin;
            float radius = above <= 0f ? 0f : Mathf.Min(MaxRadius, above * 0.75f * GameSettings.MicSensitivity);

            // Fast attack, slow release, so the bar reads smoothly.
            Radius = radius > Radius ? radius : Mathf.MoveTowards(Radius, radius, 20f * Time.unscaledDeltaTime);
            Level = Mathf.Clamp01(Radius / MaxRadius);

            // 4. Emit: only during play, the loudest moment of each 0.6 s window goes out as one noise.
            NightDirector director = NightDirector.Instance;
            bool playing = director != null && director.Result == NightDirector.Outcome.Playing && !director.Paused && player != null && !player.IsDead;
            if (!playing || listening < 3f) { peakRadius = 0f; return; }
            peakRadius = Mathf.Max(peakRadius, radius);
            emitTimer -= Time.deltaTime;
            if (emitTimer > 0f) return;
            emitTimer = EmitEvery;
            if (peakRadius >= MinRadius) NoisePulse.Emit(player.transform.position, peakRadius, NoiseKind.Voice);
            peakRadius = 0f;
        }
    }
}
