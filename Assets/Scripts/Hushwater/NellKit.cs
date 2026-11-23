using System;
using System.Collections.Generic;
using UnityEngine;

namespace Hushwater
{
    // Something worth photographing: a scene that proves the town was lived in when it drowned.
    public sealed class PhotoSubject : MonoBehaviour
    {
        private static readonly List<PhotoSubject> All = new List<PhotoSubject>();

        public EvidenceSpot Spot { get; private set; }
        // The point the camera has to be aimed at.
        public Vector3 Focus => transform.position + Vector3.up * 0.7f;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => All.Clear();

        private void OnEnable() => All.Add(this);
        private void OnDisable() => All.Remove(this);

        public static PhotoSubject Attach(GameObject scene, EvidenceSpot spot)
        {
            PhotoSubject subject = scene.AddComponent<PhotoSubject>();
            subject.Spot = spot;
            return subject;
        }

        // The subject closest to the centre of the frame, if one is close enough, in view and not yet photographed.
        public static PhotoSubject InFrame(Camera camera, float maxDistance, float maxAngle)
        {
            PhotoSubject best = null;
            float bestAngle = maxAngle;
            foreach (PhotoSubject subject in All)
            {
                if (Campaign.Has(subject.Spot.Id)) continue;
                Vector3 to = subject.Focus - camera.transform.position;
                if (to.magnitude > maxDistance) continue;
                float angle = Vector3.Angle(camera.transform.forward, to);
                if (angle > bestAngle) continue;
                // Line of sight: nothing solid between the lens and the subject.
                if (Physics.Raycast(camera.transform.position, to.normalized, out RaycastHit hit, to.magnitude - 0.4f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore)
                    && hit.collider.GetComponentInParent<PhotoSubject>() != subject) continue;
                best = subject;
                bestAngle = angle;
            }
            return best;
        }
    }

    // The survey camera. Design doc: "Photograph evidence with the survey camera. 12 exposures a night.
    // Use the flash for a clear shot, at the cost of noise." "Dark rooms need the flash, which whines as it charges.
    // Shoot without it and the photo may be too blurry to count."
    public sealed class SurveyCamera : MonoBehaviour
    {
        public const int ExposuresPerNight = 12;
        // Not in the doc: how likely a no-flash shot of a dark scene is to come out sharp enough. Tune in playtests.
        private const float DarkShotChance = 0.35f;
        private const float Reach = 7f;

        private PlayerController player;
        private Camera view;
        private Light flashLight;
        private AudioSource source;
        private float baseFov;
        private float whineLeft;
        private float whinePulse;
        private float flashGlow;

        public bool Raised { get; private set; }
        public bool FlashOn { get; private set; } = true;
        public int Exposures { get; private set; } = ExposuresPerNight;
        public bool Charging => whineLeft > 0f;
        public float ChargeProgress { get; private set; }
        public PhotoSubject Framed { get; private set; }
        // 1 right after a flash, fading out: the HUD whites out the screen with it.
        public float FlashGlow => flashGlow;

        // Every flash, for the Congregation (who turn toward light).
        public static event Action<Vector3> Flashed;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => Flashed = null;

        private void Awake()
        {
            player = GetComponent<PlayerController>();
            view = GetComponentInChildren<Camera>();
            baseFov = view.fieldOfView;
            source = gameObject.AddComponent<AudioSource>();
            flashLight = new GameObject("Camera Flash").AddComponent<Light>();
            flashLight.transform.SetParent(view.transform, false);
            flashLight.type = LightType.Spot;
            flashLight.spotAngle = 70f;
            flashLight.range = 14f;
            flashLight.intensity = 0f;
            flashLight.color = new Color(0.95f, 0.97f, 1f);
        }

        private void Update()
        {
            flashGlow = Mathf.MoveTowards(flashGlow, 0f, Time.deltaTime * 4f);
            flashLight.intensity = flashGlow * 6f;
            bool usable = !player.InputLocked && !player.IsDead && !player.IsKnockedDown && !player.IsHiding && !player.Underwater;
            Raised = usable && GameInput.CameraHeld;
            player.CameraRaised = Raised;
            view.fieldOfView = Mathf.Lerp(view.fieldOfView, Raised ? baseFov * 0.7f : baseFov, Time.deltaTime * 10f);
            Framed = Raised ? PhotoSubject.InFrame(view, Reach, 14f) : null;

            if (Charging)
            {
                UpdateCharge();
                return;
            }
            if (!usable) return;
            if (GameInput.Down(KeyCode.R))
            {
                FlashOn = !FlashOn;
                player.Notify(FlashOn ? "Flash on: a clear shot, but 20 m of noise." : "Flash off: 4 m of noise, but dark scenes may blur.");
            }
            if (Raised && GameInput.ShutterDown) Shoot();
        }

        // Left mouse with the camera up. With the flash on, it charges first (unless there is a spare bulb).
        private void Shoot()
        {
            if (Exposures <= 0)
            {
                player.Notify("No exposures left tonight.");
                return;
            }
            if (!FlashOn)
            {
                TakePhoto(false);
                return;
            }
            if (Campaign.FlashBulbs > 0)
            {
                Campaign.FlashBulbs--;
                TakePhoto(true);
                return;
            }
            // Design doc upgrade: "Quiet shutter: flash whine 2 s -> 1 s."
            whineLeft = Campaign.Owns(Upgrade.Shutter) ? 1f : 2f;
            ChargeProgress = 0f;
            whinePulse = 0f;
            source.clip = ProceduralSounds.Whine;
            source.pitch = Campaign.Owns(Upgrade.Shutter) ? 2f : 1f;
            source.Play();
        }

        // The whine is a noise of its own while it charges; then the flash fires where the camera points.
        private void UpdateCharge()
        {
            float total = Campaign.Owns(Upgrade.Shutter) ? 1f : 2f;
            whineLeft -= Time.deltaTime;
            ChargeProgress = 1f - Mathf.Clamp01(whineLeft / total);
            whinePulse -= Time.deltaTime;
            if (whinePulse <= 0f)
            {
                whinePulse = 0.5f;
                NoisePulse.Emit(transform.position, NoisePulse.Whine, NoiseKind.Whine);
            }
            if (whineLeft > 0f) return;
            source.Stop();
            source.pitch = 1f;
            if (Raised && !player.IsDead) TakePhoto(true);
            else player.Notify("You lowered the camera. The flash charge is wasted.");
        }

        // One exposure. Counts as evidence if a subject is framed, and (in the dark) if it isn't blurred.
        private void TakePhoto(bool flash)
        {
            Exposures--;
            source.PlayOneShot(ProceduralSounds.Shutter, 0.8f);
            if (flash)
            {
                flashGlow = 1f;
                source.PlayOneShot(ProceduralSounds.FlashPop, 0.7f);
                NoisePulse.Emit(transform.position, NoisePulse.Flash, NoiseKind.Flash);
                Flashed?.Invoke(transform.position);
            }
            else NoisePulse.Emit(transform.position, NoisePulse.Photo, NoiseKind.Photo);

            PhotoSubject subject = PhotoSubject.InFrame(view, Reach, 14f);
            if (subject == null)
            {
                player.Notify($"Nothing worth a picture there. {Exposures} exposures left.");
                return;
            }
            if (subject.Spot.Dark && !flash && UnityEngine.Random.value > DarkShotChance)
            {
                player.Notify($"Too dark. It'll come out a blur. {Exposures} exposures left.");
                return;
            }
            player.CollectEvidence(subject.Spot, subject.transform.position);
        }
    }

    // Nell's crank lantern. Design doc: cranking makes 12 m of noise (7 m with the geared crank), lamp oil refills it
    // with no cranking, moths drain it, and turning the light to zero scatters them.
    public sealed class NellLantern : MonoBehaviour
    {
        // Not in the doc: the lantern holds 10 charges and burns one every 30 s, so a full lantern lasts 5 minutes.
        public const float MaxCharge = 10f;
        private const float SecondsPerCharge = 30f;
        private const float ChargePerHalfTurn = 0.5f;

        private PlayerController player;
        private Light lamp;
        private float crankTimer;
        private float baseIntensity;

        public float Charge { get; private set; } = 6f;
        public bool On { get; private set; } = true;
        public bool Lit => On && Charge > 0f && !player.Underwater;
        public bool Full => Charge >= MaxCharge - 0.01f;
        public bool Cranking { get; private set; }

        private void Awake()
        {
            player = GetComponent<PlayerController>();
            lamp = player.GetComponentInChildren<Camera>().GetComponent<Light>();
            baseIntensity = lamp != null ? lamp.intensity : 1f;
        }

        private void Update()
        {
            Cranking = false;
            bool free = !player.InputLocked && !player.IsDead && !player.IsKnockedDown;
            if (free && GameInput.Down(KeyCode.L))
            {
                On = !On;
                player.Notify(On ? "Lantern up." : "Lantern down to nothing. Moths scatter, and so does your sight.");
            }
            // F cranks the lantern unless a lantern post is in reach (that uses F too).
            Interactable focus = player.FocusedInteractable;
            bool postInReach = focus != null && focus.Key == KeyCode.F;
            if (free && !player.IsHiding && !postInReach && GameInput.Held(KeyCode.F) && !Full) Crank();

            if (Lit) Charge = Mathf.Max(0f, Charge - Time.deltaTime / SecondsPerCharge);
            if (lamp != null)
            {
                float strength = Lit ? Mathf.Lerp(0.35f, 1f, Mathf.Clamp01(Charge / 2f)) : 0f;
                // A guttering flicker as it runs low.
                if (Lit && Charge < 1f) strength *= 0.75f + 0.25f * Mathf.PerlinNoise(Time.time * 8f, 0f);
                lamp.intensity = baseIntensity * strength;
            }
        }

        // Each half-turn of the crank is a noise pulse and a little charge.
        private void Crank()
        {
            Cranking = true;
            crankTimer -= Time.deltaTime;
            if (crankTimer > 0f) return;
            crankTimer = 0.5f;
            Charge = Mathf.Min(MaxCharge, Charge + ChargePerHalfTurn);
            NoisePulse.Emit(transform.position, Campaign.Owns(Upgrade.Crank) ? NoisePulse.CrankGeared : NoisePulse.Crank, NoiseKind.Crank);
            ProceduralSounds.PlayAt(ProceduralSounds.Crank, transform.position, 0.5f);
        }

        public void Refill() => Charge = MaxCharge;

        // Lantern moths feeding.
        public void Drain(float charges) => Charge = Mathf.Max(0f, Charge - charges);
    }
}
