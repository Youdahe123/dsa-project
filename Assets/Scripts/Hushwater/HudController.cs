using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Hushwater
{
    // Builds the on-screen HUD in code and updates it each frame.
    // Design doc: "The HUD shows only what Nell could know. The Sexton never appears on a map or marker.
    // You track him by his knocking clapper and the scrape of his hook." So he appears here only as captions.
    public sealed class HudController : MonoBehaviour
    {
        // One expanding ring for a sound Nell made (design doc: "Noise ripple: grows with every sound you make").
        private sealed class Ripple
        {
            public Image Ring;
            public float Age = 99f;
            public float Size;
            public Color Color;
        }

        // One line of captions. Lines with the same key replace each other (so knocks don't pile up).
        private sealed class Caption
        {
            public string Key;
            public string Text;
            public float Time;
        }

        private const float CaptionLife = 3.5f;
        private const float HearingRange = 45f; // matches the Sexton's AudioSource max distance

        private PlayerController player;
        private SextonController sexton;
        private NightDirector director;
        private Transform root;

        private Image vitalityFill;
        private Image breathFill;
        private Text vitalityValue;
        private Text breathValue;
        private Text breathHint;
        private Text dawnTimer;
        private Text evidence;
        private Text objective;
        private Text prompt;
        private Image holdFill;
        private Text toast;
        private Text captions;
        private Text stance;
        private Text lastSound;
        private float lastSoundAge = 99f;
        private Color lastSoundColor;
        private const int MaxSlots = 4;
        private readonly Image[] slotMarks = new Image[MaxSlots];
        private SurveyCamera surveyCamera;
        private NellLantern lantern;
        private Image lanternFill;
        private Text lanternValue;
        private Text exposures;
        private GameObject viewfinder;
        private Text viewfinderInfo;
        private Image chargeFill;
        private Image flashOverlay;
        private Image waterOverlay;
        private Image transmitFill;
        private Text transmitLabel;
        private Text hint;
        private Image crosshair;
        private Image hurtOverlay;
        private Image fadeOverlay;
        private GameObject slats;
        private GameObject endPanel;
        private Text endTitle;
        private Text endBody;
        private Text endStats;
        private Text endKeys;
        private readonly Image[] slotPlates = new Image[MaxSlots];
        private readonly Text[] slotNames = new Text[MaxSlots];
        private readonly Text[] slotCounts = new Text[MaxSlots];
        private readonly Ripple[] ripples = new Ripple[8];
        private readonly List<Caption> captionLines = new List<Caption>();
        private readonly Queue<string> toasts = new Queue<string>();
        private float toastTime;
        private int nextRipple;

        public Transform Root => root;

        // Creates the canvas and every HUD element. Called once by the bootstrap.
        public void Initialize(PlayerController target, SextonController enemy, NightDirector night)
        {
            player = target;
            sexton = enemy;
            director = night;
            surveyCamera = player.GetComponent<SurveyCamera>();
            lantern = player.GetComponent<NellLantern>();
            root = UiKit.Canvas("HUD Canvas");

            // Full-screen layers go first so everything else draws on top of them.
            waterOverlay = UiKit.Overlay(root, new Color(UiKit.Lake.r * 0.4f, UiKit.Lake.g * 0.5f, UiKit.Lake.b * 0.5f, 0f));
            BuildSlats();
            BuildViewfinder();
            hurtOverlay = UiKit.Overlay(root, new Color(0.5f, 0f, 0f, 0f));
            flashOverlay = UiKit.Overlay(root, new Color(1f, 1f, 1f, 0f));
            BuildVitals();
            BuildTopBar();
            BuildCentre();
            BuildToolSlots();
            BuildCaptionsAndHints();
            fadeOverlay = UiKit.Overlay(root, new Color(0f, 0f, 0f, 0f));
            BuildEndPanel();
            gameObject.AddComponent<PauseMenu>().Build(root, director);

            NoisePulse.Emitted += OnNoise;
            sexton.MadeSound += OnSextonSound;
            player.Notified += ShowToast;
            director.BellTolled += OnChapelBell;
            int n = Campaign.Night;
            ShowToast($"Night {n}: {Story.NightDistricts[n]}. {Story.NightPressure[n]}");
            ShowToast(n == 5 ? "Get to the radio room in the dam, start the transmitter, and hold out for 90 seconds."
                             : $"Tonight's evidence: {NeedText()}. Then row out from the boat.");
        }

        // Unsubscribe from the static noise bus so a reloaded scene doesn't call a destroyed HUD.
        private void OnDestroy()
        {
            NoisePulse.Emitted -= OnNoise;
            if (sexton != null) sexton.MadeSound -= OnSextonSound;
            if (player != null) player.Notified -= ShowToast;
            if (director != null) director.BellTolled -= OnChapelBell;
        }

        // Tonight's evidence target as words, e.g. "2 photographs and 2 register pages".
        private static string NeedText()
        {
            int n = Campaign.Night;
            var parts = new List<string>();
            if (Story.PhotosNeeded(n) > 0) parts.Add($"{Story.PhotosNeeded(n)} photograph{(Story.PhotosNeeded(n) > 1 ? "s" : "")}");
            if (Story.PagesNeeded(n) > 0) parts.Add($"{Story.PagesNeeded(n)} register pages");
            if (Story.TapesNeeded(n) > 0) parts.Add("one of Emil's tapes");
            return string.Join(" and ", parts);
        }

        // The survey camera's frame: corner brackets, flash state, exposures and the flash charge.
        private void BuildViewfinder()
        {
            viewfinder = new GameObject("Viewfinder", typeof(RectTransform));
            viewfinder.transform.SetParent(root, false);
            UiKit.Stretch((RectTransform)viewfinder.transform);
            Transform v = viewfinder.transform;
            Color ink = UiKit.Fade(UiKit.Paper, 0.75f);
            foreach (Vector2 corner in new[] { new Vector2(-1f, 1f), new Vector2(1f, 1f), new Vector2(-1f, -1f), new Vector2(1f, -1f) })
            {
                Vector2 at = new Vector2(corner.x * 520f, corner.y * 300f);
                UiKit.Box(v, ink, new Vector2(0.5f, 0.5f), at + new Vector2(-corner.x * 40f, 0f), new Vector2(80f, 3f));
                UiKit.Box(v, ink, new Vector2(0.5f, 0.5f), at + new Vector2(0f, -corner.y * 40f), new Vector2(3f, 80f));
            }
            viewfinderInfo = UiKit.Text(v, "", 20, new Vector2(0.5f, 0.5f), new Vector2(0f, -340f), TextAnchor.MiddleCenter, new Vector2(1000f, 30f), UiKit.Paper);
            chargeFill = UiKit.Bar(v, new Vector2(0.5f, 0.5f), new Vector2(0f, -372f), new Vector2(300f, 6f), UiKit.Paper);
            viewfinder.SetActive(false);
        }

        // ---------- Building ----------

        // Top-left: Vitality and Breath bars with numbers.
        private void BuildVitals()
        {
            Vector2 topLeft = new Vector2(0f, 1f);
            UiKit.Text(root, "VITALITY", 15, topLeft, new Vector2(36f, -28f), TextAnchor.UpperLeft, new Vector2(200f, 22f), UiKit.Paper);
            vitalityValue = UiKit.Text(root, "100", 15, topLeft, new Vector2(256f, -28f), TextAnchor.UpperRight, new Vector2(100f, 22f), UiKit.Paper);
            vitalityFill = UiKit.Bar(root, topLeft, new Vector2(36f, -52f), new Vector2(320f, 12f), UiKit.Rust);

            UiKit.Text(root, "BREATH", 15, topLeft, new Vector2(36f, -74f), TextAnchor.UpperLeft, new Vector2(200f, 22f), UiKit.Paper);
            breathValue = UiKit.Text(root, "30 s", 15, topLeft, new Vector2(256f, -74f), TextAnchor.UpperRight, new Vector2(100f, 22f), UiKit.Paper);
            breathFill = UiKit.Bar(root, topLeft, new Vector2(36f, -98f), new Vector2(320f, 12f), UiKit.Lake);
            breathHint = UiKit.Text(root, "", 15, topLeft, new Vector2(36f, -116f), TextAnchor.UpperLeft, new Vector2(400f, 22f), UiKit.Lantern);
            UiKit.Text(root, "LANTERN", 15, topLeft, new Vector2(36f, -142f), TextAnchor.UpperLeft, new Vector2(200f, 22f), UiKit.Paper);
            lanternValue = UiKit.Text(root, "", 15, topLeft, new Vector2(256f, -142f), TextAnchor.UpperRight, new Vector2(100f, 22f), UiKit.Paper);
            lanternFill = UiKit.Bar(root, topLeft, new Vector2(36f, -166f), new Vector2(320f, 8f), UiKit.Lantern);
        }

        // Top-centre: the dawn bell timer. Top-right: evidence and the current objective.
        private void BuildTopBar()
        {
            Vector2 top = new Vector2(0.5f, 1f);
            UiKit.Text(root, "UNTIL THE DAWN BELL", 13, top, new Vector2(0f, -24f), TextAnchor.UpperCenter, new Vector2(300f, 20f), UiKit.Fade(UiKit.Paper, 0.7f));
            dawnTimer = UiKit.Text(root, "18:00", 34, top, new Vector2(0f, -42f), TextAnchor.UpperCenter, new Vector2(300f, 44f), UiKit.Paper);

            Vector2 topRight = new Vector2(1f, 1f);
            evidence = UiKit.Text(root, "", 26, topRight, new Vector2(-36f, -24f), TextAnchor.UpperRight, new Vector2(500f, 34f), UiKit.Paper);
            objective = UiKit.Text(root, "", 17, topRight, new Vector2(-36f, -60f), TextAnchor.UpperRight, new Vector2(500f, 50f), UiKit.Fade(UiKit.Paper, 0.75f));
            toast = UiKit.Text(root, "", 20, top, new Vector2(0f, -110f), TextAnchor.UpperCenter, new Vector2(900f, 60f), UiKit.Lantern);
            transmitLabel = UiKit.Text(root, "", 20, top, new Vector2(0f, -170f), TextAnchor.UpperCenter, new Vector2(900f, 30f), UiKit.Paper);
            transmitFill = UiKit.Bar(root, top, new Vector2(0f, -200f), new Vector2(500f, 10f), UiKit.Lantern);
        }

        // Centre: crosshair, noise ripples, the interaction prompt and its hold bar.
        private void BuildCentre()
        {
            Vector2 centre = new Vector2(0.5f, 0.5f);
            for (int i = 0; i < ripples.Length; i++)
            {
                Image ring = UiKit.Box(root, Color.clear, centre, Vector2.zero, Vector2.one * 100f);
                ring.sprite = UiKit.Ring;
                ripples[i] = new Ripple { Ring = ring };
            }
            crosshair = UiKit.Box(root, UiKit.Fade(UiKit.Paper, 0.6f), centre, Vector2.zero, new Vector2(4f, 4f));
            prompt = UiKit.Text(root, "", 22, centre, new Vector2(0f, -90f), TextAnchor.MiddleCenter, new Vector2(900f, 34f), UiKit.Paper);
            holdFill = UiKit.Bar(root, centre, new Vector2(0f, -116f), new Vector2(260f, 6f), UiKit.Lantern);
        }

        // Bottom-centre: three tool slots (keys 1-3, Q to use).
        private void BuildToolSlots()
        {
            Vector2 bottom = new Vector2(0.5f, 0f);
            for (int i = 0; i < MaxSlots; i++)
            {
                Vector2 position = new Vector2((i - 1.5f) * 150f, 36f);
                slotPlates[i] = UiKit.Box(root, new Color(0f, 0f, 0f, 0.5f), bottom, position, new Vector2(138f, 70f));
                Transform plate = slotPlates[i].transform;
                // A lantern-amber strip along the top marks the selected slot.
                slotMarks[i] = UiKit.Box(plate, UiKit.Lantern, new Vector2(0.5f, 1f), Vector2.zero, new Vector2(138f, 4f));
                UiKit.Text(plate, (i + 1).ToString(), 14, new Vector2(0f, 1f), new Vector2(8f, -4f), TextAnchor.UpperLeft, new Vector2(30f, 20f), UiKit.Fade(UiKit.Paper, 0.6f));
                slotCounts[i] = UiKit.Text(plate, "0", 28, new Vector2(1f, 1f), new Vector2(-10f, -2f), TextAnchor.UpperRight, new Vector2(60f, 36f), UiKit.Paper);
                slotNames[i] = UiKit.Text(plate, "", 14, new Vector2(0.5f, 0f), new Vector2(0f, 6f), TextAnchor.LowerCenter, new Vector2(130f, 20f), UiKit.Paper);
            }
            UiKit.Text(root, "Q  USE", 13, bottom, new Vector2(0f, 112f), TextAnchor.LowerCenter, new Vector2(200f, 20f), UiKit.Fade(UiKit.Paper, 0.6f));
            // Design doc HUD: "7 / 12  Exposures left".
            exposures = UiKit.Text(root, "", 22, new Vector2(1f, 0f), new Vector2(-36f, 64f), TextAnchor.LowerRight, new Vector2(400f, 30f), UiKit.Paper);
        }

        // Captions above the tool slots, stance bottom-left, key hints bottom-right.
        private void BuildCaptionsAndHints()
        {
            captions = UiKit.Text(root, "", 20, new Vector2(0.5f, 0f), new Vector2(0f, 140f), TextAnchor.LowerCenter, new Vector2(1000f, 140f), UiKit.Paper);
            captions.supportRichText = true;
            stance = UiKit.Text(root, "", 18, Vector2.zero, new Vector2(36f, 30f), TextAnchor.LowerLeft, new Vector2(500f, 26f), UiKit.Paper);
            lastSound = UiKit.Text(root, "", 16, Vector2.zero, new Vector2(36f, 58f), TextAnchor.LowerLeft, new Vector2(500f, 24f), Color.clear);
            hint = UiKit.Text(root, "Esc  pause & controls      Tab  journal & map      F3  noise debug view", 16, new Vector2(1f, 0f), new Vector2(-36f, 30f),
                TextAnchor.LowerRight, new Vector2(700f, 24f), UiKit.Fade(UiKit.Paper, 0.75f));
        }

        // Dark horizontal slats over the screen while Nell is inside a wardrobe.
        private void BuildSlats()
        {
            slats = new GameObject("Wardrobe Slats", typeof(RectTransform));
            slats.transform.SetParent(root, false);
            UiKit.Stretch((RectTransform)slats.transform);
            UiKit.Overlay(slats.transform, new Color(0f, 0f, 0f, 0.35f));
            for (int i = 0; i < 9; i++)
            {
                Image slat = UiKit.Box(slats.transform, new Color(0.03f, 0.025f, 0.02f, 0.96f), new Vector2(0.5f, 1f), new Vector2(0f, -i * 130f), new Vector2(4000f, 96f));
                slat.name = "Slat";
            }
            slats.SetActive(false);
        }

        // The end-of-night card: title, what happened, stats, and which keys do what.
        private void BuildEndPanel()
        {
            Image panel = UiKit.Box(root, UiKit.Fade(UiKit.Night, 0.9f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(900f, 420f));
            endPanel = panel.gameObject;
            Transform p = panel.transform;
            Vector2 top = new Vector2(0.5f, 1f);
            endTitle = UiKit.Text(p, "", 46, top, new Vector2(0f, -40f), TextAnchor.UpperCenter, new Vector2(860f, 60f), UiKit.Paper);
            endBody = UiKit.Text(p, "", 20, top, new Vector2(0f, -112f), TextAnchor.UpperCenter, new Vector2(780f, 60f), UiKit.Fade(UiKit.Paper, 0.8f));
            endStats = UiKit.Text(p, "", 18, top, new Vector2(0f, -190f), TextAnchor.UpperCenter, new Vector2(780f, 120f), UiKit.Lantern);
            endKeys = UiKit.Text(p, "", 20, new Vector2(0.5f, 0f), new Vector2(0f, 36f), TextAnchor.LowerCenter, new Vector2(860f, 30f), UiKit.Paper);
            endPanel.SetActive(false);
        }

        // ---------- Updating ----------

        // Refreshes every element from the player, the night and the noise events.
        private void Update()
        {
            if (player == null) return;
            UpdateVitals();
            UpdateTopBar();
            UpdatePrompt();
            UpdateToolSlots();
            UpdateRipples();
            UpdateCaptions();
            UpdateToast();

            stance.text = Stance();
            hint.enabled = director.Result == NightDirector.Outcome.Playing;
            crosshair.enabled = !player.IsHiding && !player.IsDead;
            slats.SetActive(player.IsHiding);
            hurtOverlay.color = new Color(0.5f, 0f, 0f, player.HurtFlash * 0.45f);
            waterOverlay.color = UiKit.Fade(waterOverlay.color, player.Underwater ? 0.55f : 0f);
            UpdateCamera();
            UpdateTransmission();
            bool over = director.Result != NightDirector.Outcome.Playing;
            fadeOverlay.color = new Color(0f, 0f, 0f, over ? Mathf.Max(0.7f, player.DeathFade) : player.DeathFade);
            UpdateEndPanel();
        }

        // Vitality and Breath; the breath bar turns rust and a warning appears when it runs low.
        private void UpdateVitals()
        {
            vitalityFill.rectTransform.anchorMax = new Vector2(player.Health / 100f, 1f);
            vitalityValue.text = Mathf.CeilToInt(player.Health).ToString();
            float breath = player.BreathNormalized;
            breathFill.rectTransform.anchorMax = new Vector2(breath, 1f);
            breathValue.text = $"{Mathf.CeilToInt(player.BreathSeconds)} s";
            bool low = breath < 0.25f && player.HoldingBreath;
            float pulse = 0.6f + 0.4f * Mathf.Sin(Time.unscaledTime * 10f);
            breathFill.color = low ? Color.Lerp(UiKit.Lake, UiKit.Rust, pulse) : UiKit.Lake;
            breathHint.text = player.Underwater ? (low ? "Out of air. Find the surface." : "Underwater: holding breath")
                : player.IsGripped ? "Hands in the mud! Tap E to pull free (each pull is noise)"
                : player.HoldingBreath ? (low ? "Running out of air..." : "Holding breath: silent") : "";
            breathHint.color = low || player.IsGripped ? UiKit.Rust : UiKit.Lantern;
            if (lantern != null)
            {
                float charge = lantern.Charge / NellLantern.MaxCharge;
                lanternFill.rectTransform.anchorMax = new Vector2(charge, 1f);
                lanternFill.color = lantern.On ? UiKit.Lantern : UiKit.Fade(UiKit.Paper, 0.3f);
                lanternValue.text = !lantern.On ? "dark" : lantern.Cranking ? "cranking" : lantern.Charge <= 0f ? "out" : "";
            }
        }

        // The viewfinder while the camera is up, the white-out after a flash, and the exposure count.
        private void UpdateCamera()
        {
            if (surveyCamera == null) return;
            flashOverlay.color = new Color(1f, 1f, 1f, surveyCamera.FlashGlow * 0.85f);
            string bulbs = Campaign.FlashBulbs > 0 ? $"  ·  bulbs {Campaign.FlashBulbs}" : "";
            exposures.text = $"{surveyCamera.Exposures} / {SurveyCamera.ExposuresPerNight}  EXPOSURES  ·  FLASH {(surveyCamera.FlashOn ? "ON" : "OFF")}{bulbs}";
            bool up = surveyCamera.Raised && director.Result == NightDirector.Outcome.Playing;
            viewfinder.SetActive(up);
            if (!up) return;
            PhotoSubject framed = surveyCamera.Framed;
            string subject = framed == null ? "nothing worth a picture in frame"
                : framed.Spot.Dark && !surveyCamera.FlashOn ? "IN FRAME, but too dark: it may blur without the flash" : "IN FRAME";
            viewfinderInfo.text = surveyCamera.Charging ? "flash charging... it whines" : $"{subject}    ·    click / RT to shoot    ·    R / X flash";
            viewfinderInfo.color = framed != null ? UiKit.Lantern : UiKit.Paper;
            chargeFill.transform.parent.gameObject.SetActive(surveyCamera.Charging);
            chargeFill.rectTransform.anchorMax = new Vector2(surveyCamera.ChargeProgress, 1f);
        }

        // Night 5: the transmission countdown.
        private void UpdateTransmission()
        {
            bool on = director.Transmitting;
            transmitLabel.enabled = on;
            transmitFill.transform.parent.gameObject.SetActive(on);
            if (!on) return;
            float t = Mathf.Max(0f, director.TransmitLeft);
            transmitLabel.text = $"TRANSMITTING  {(int)(t / 60f)}:{(int)(t % 60f):00}   ·   hold out";
            transmitFill.rectTransform.anchorMax = new Vector2(1f - t / NightDirector.TransmitSeconds, 1f);
        }

        // Dawn bell countdown and the evidence counter.
        private void UpdateTopBar()
        {
            float left = director.TimeLeft;
            dawnTimer.text = $"{(int)(left / 60f):00}:{(int)(left % 60f):00}";
            dawnTimer.color = left < 120f ? Color.Lerp(UiKit.Paper, UiKit.Rust, 0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * 4f)) : UiKit.Paper;

            int n = Campaign.Night;
            bool done = Campaign.NightGoalMet(n);
            if (n == 5)
            {
                evidence.text = $"PAGES {Campaign.Count(EvidenceType.Page)}/{Story.TotalPages}  ·  TAPES {Campaign.Count(EvidenceType.Tape)}/{Story.TotalTapes}";
                evidence.color = UiKit.Paper;
                objective.text = director.Transmitting ? "Hold out until the transmission finishes" : "Reach the radio room in the dam and transmit";
                return;
            }
            int found = 0, needed = 0;
            foreach (EvidenceType type in new[] { EvidenceType.Photo, EvidenceType.Page, EvidenceType.Tape })
            {
                int need = type == EvidenceType.Photo ? Story.PhotosNeeded(n) : type == EvidenceType.Page ? Story.PagesNeeded(n) : Story.TapesNeeded(n);
                needed += need;
                found += Mathf.Min(need, Campaign.CountForNight(n, type));
            }
            evidence.text = $"EVIDENCE {found}/{needed}" + (player.Evidence > 0 ? $"   +{player.Evidence} tonight" : "");
            evidence.color = done ? UiKit.Lantern : UiKit.Paper;
            objective.text = done ? "Row out from the boat before the dawn bell" : $"Find {NeedText()}";
        }

        // The hint in the middle of the screen, most important first, with a progress bar for held actions.
        private void UpdatePrompt()
        {
            holdFill.transform.parent.gameObject.SetActive(false);
            if (player.IsDead || player.IsKnockedDown || director.Result != NightDirector.Outcome.Playing)
            {
                prompt.text = player.IsKnockedDown ? "You're down. Get up and get away." : "";
                return;
            }
            if (player.IsHiding)
            {
                string noun = player.CurrentHideSpot != null ? player.CurrentHideSpot.Noun : "wardrobe";
                prompt.text = $"E  come out from the {noun}      Hold Space  hold your breath if he comes close";
                return;
            }
            if (player.NearbyPickup != null)
            {
                Pickup pickup = player.NearbyPickup;
                prompt.text = $"E  pick up the {pickup.Label}";
                return;
            }
            if (player.NearHideSpot && !player.Underwater)
            {
                prompt.text = $"E  {player.NearbyHideSpot.HidePhrase}";
                return;
            }
            Interactable focus = player.FocusedInteractable;
            prompt.text = focus != null ? focus.Prompt : "";
            if (focus != null && player.HoldProgress > 0f)
            {
                holdFill.transform.parent.gameObject.SetActive(true);
                holdFill.rectTransform.anchorMax = new Vector2(player.HoldProgress, 1f);
            }
        }

        // Tool counts, with the selected slot lit in lantern amber and empty slots dimmed.
        private void UpdateToolSlots()
        {
            for (int i = 0; i < MaxSlots; i++)
            {
                bool exists = i < player.SlotCount;
                slotPlates[i].gameObject.SetActive(exists);
                if (!exists) continue;
                slotPlates[i].rectTransform.anchoredPosition = new Vector2((i - (player.SlotCount - 1) * 0.5f) * 150f, 36f);
                PlayerController.ToolStack stack = player.SlotStack(i);
                int count = stack != null ? stack.Count : 0;
                bool selected = i == player.SelectedSlot;
                slotPlates[i].color = new Color(0f, 0f, 0f, selected ? 0.7f : 0.45f);
                slotMarks[i].enabled = selected;
                Color text = selected ? UiKit.Lantern : UiKit.Paper;
                if (count == 0) text = UiKit.Fade(text, 0.45f);
                slotCounts[i].text = count.ToString();
                slotCounts[i].color = text;
                slotNames[i].text = stack != null ? Pickup.SlotName(stack.Kind) : "empty";
                slotNames[i].color = text;
            }
        }

        // What Nell is doing with her body, and how loud that is.
        private string Stance()
        {
            if (director.Result != NightDirector.Outcome.Playing || player.IsDead) return "";
            if (player.IsHiding) return player.HoldingBreath ? "HIDDEN  ·  silent" : "HIDDEN";
            if (player.Underwater) return "SWIMMING  ·  silent";
            if (player.OnRoof) return "ON THE ROOF  ·  he can't climb";
            if (player.WrapTimeLeft > 0f) return $"WRAPPED  ·  2 m  ·  {Mathf.CeilToInt(player.WrapTimeLeft)} s";
            string mud = player.InMud ? "  ·  deep mud, leaving grooves" : "";
            if (player.IsCrouching) return "CREEPING  ·  2 m" + mud;
            if (player.IsSprinting) return "SPRINTING  ·  18 m" + mud;
            return (player.InMud ? "WALKING  ·  10 m" : "WALKING  ·  6 m") + mud;
        }

        // ---------- Noise ripples ----------

        // Every sound Nell makes near herself starts a ring; louder sounds make bigger, redder rings.
        private void OnNoise(NoiseEvent noise)
        {
            if (player == null) return;
            if (noise.IsLure)
            {
                // She can hear her own lures land: a directional caption, but no ripple.
                AddCaption(noise.Kind == NoiseKind.Bottle ? "bottle" : "handbell",
                    noise.Kind == NoiseKind.Bottle ? "glass shatters" : "a handbell rings", noise.Position);
                return;
            }
            if (noise.Kind == NoiseKind.Gasp) AddCaption("gasp", "you gasp for air", player.transform.position);
            if (noise.Kind == NoiseKind.Door) AddCaption("door", "the door bursts open", noise.Position);
            if (noise.Kind == NoiseKind.Cabinet) AddCaption("door", "the cabinet splits open", noise.Position);
            if (noise.Kind == NoiseKind.Wail) AddCaption("wail", "A WAIL rises from the Congregation", noise.Position);
            if (noise.Kind == NoiseKind.Hiss) AddCaption("hiss", "moths hiss around your lantern", noise.Position);
            if (noise.Kind == NoiseKind.Fall) AddCaption("fall", "boards crack and give way", noise.Position);
            if (noise.Kind == NoiseKind.Generator) AddCaption("generator", "the generator hammers", noise.Position);
            if (noise.Kind == NoiseKind.Spillway) AddCaption("spillway", "the spillway wheel groans", noise.Position);
            if (!GameSettings.SoundRipples) return;
            if (Vector3.Distance(noise.Position, player.transform.position) > 3f) return;

            Ripple ripple = ripples[nextRipple];
            nextRipple = (nextRipple + 1) % ripples.Length;
            ripple.Age = 0f;
            ripple.Size = 70f + noise.Radius * 16f;
            ripple.Color = noise.Radius >= NoisePulse.Sprint ? UiKit.Rust : noise.Radius >= NoisePulse.Mud ? UiKit.Lantern : UiKit.Paper;
            lastSound.text = $"Last sound: {NoisePulse.Label(noise.Kind)}, {noise.Radius:0} m";
            lastSoundColor = ripple.Color == UiKit.Rust ? Color.Lerp(UiKit.Rust, Color.white, 0.3f) : ripple.Color;
            lastSoundAge = 0f;
        }

        // Grows and fades each ring over about a second.
        private void UpdateRipples()
        {
            foreach (Ripple ripple in ripples)
            {
                ripple.Age += Time.deltaTime;
                float t = Mathf.Clamp01(ripple.Age / 1.1f);
                float alpha = (1f - t) * 0.6f;
                float size = ripple.Size * Mathf.Lerp(0.25f, 1f, 1f - (1f - t) * (1f - t));
                ripple.Ring.rectTransform.sizeDelta = Vector2.one * size;
                ripple.Ring.color = UiKit.Fade(ripple.Color, alpha);
            }
            lastSoundAge += Time.deltaTime;
            lastSound.color = UiKit.Fade(lastSoundColor, Mathf.Clamp01(2.5f - lastSoundAge));
        }

        // ---------- Captions ----------

        // Turns each sound the Sexton makes into a caption, if Nell is close enough to hear it.
        private void OnSextonSound(SextonSound sound, Vector3 position)
        {
            switch (sound)
            {
                case SextonSound.SlowKnock: AddCaption("knock", "slow knocking", position); break;
                case SextonSound.FastKnock: AddCaption("knock", "FAST KNOCKING", position); break;
                case SextonSound.Scrape: AddCaption("scrape", "a hook scrapes stone", position); break;
                case SextonSound.Hymn: AddCaption("hymn", "someone hums a funeral hymn", position); break;
                case SextonSound.Bell: AddCaption("bell", "the bell tolls; it drowns out every sound", position); break;
                case SextonSound.Smash: AddCaption("smash", "wood splinters", position); break;
                case SextonSound.Feeling: AddCaption("feel", "fingers at a wardrobe door", position); break;
            }
        }

        // Night 5: "The bell tolls every 60 s."
        private void OnChapelBell() => AddCaption("chapelbell", "the chapel bell tolls; every sound is drowned", new Vector3(8f, 0f, 49f));

        // Adds or refreshes a caption line, with the direction and distance of the sound from where Nell is looking.
        private void AddCaption(string key, string text, Vector3 position)
        {
            if (!GameSettings.Captions || player == null) return;
            Vector3 to = position - player.transform.position;
            to.y = 0f;
            float distance = to.magnitude;
            if (distance > HearingRange && key != "chapelbell") return;

            string where = distance < 1.5f ? "" : $"  ·  {Direction(to)}{(distance < 8f ? ", close" : distance > 25f ? ", far" : "")}";
            string line = $"[{text}]{where}";
            Caption existing = captionLines.Find(c => c.Key == key);
            if (existing == null)
            {
                existing = new Caption { Key = key };
                captionLines.Add(existing);
                if (captionLines.Count > 4) captionLines.RemoveAt(0);
            }
            existing.Text = line;
            existing.Time = Time.time;
        }

        // Ahead / behind / left / right relative to the camera.
        private string Direction(Vector3 flatOffset)
        {
            Vector3 forward = player.View.transform.forward;
            forward.y = 0f;
            float angle = Vector3.SignedAngle(forward, flatOffset, Vector3.up);
            if (Mathf.Abs(angle) < 45f) return "ahead";
            if (Mathf.Abs(angle) > 135f) return "behind you";
            return angle > 0f ? "to your right" : "to your left";
        }

        // Draws the caption lines, each fading out as it ages.
        private void UpdateCaptions()
        {
            captionLines.RemoveAll(c => Time.time - c.Time > CaptionLife);
            if (!GameSettings.Captions) captionLines.Clear();
            var builder = new StringBuilder();
            foreach (Caption caption in captionLines)
            {
                float alpha = Mathf.Clamp01((CaptionLife - (Time.time - caption.Time)) / 1f);
                Color color = caption.Text.Contains("FAST") || caption.Text.Contains("WAIL") || caption.Key == "bell" ? UiKit.Rust : UiKit.Paper;
                // Rust red is too dark to read on its own; lighten it a little for text.
                if (color == UiKit.Rust) color = Color.Lerp(UiKit.Rust, Color.white, 0.25f);
                builder.Append($"<color=#{UiKit.Hex(UiKit.Fade(color, alpha))}>{caption.Text}</color>\n");
            }
            captions.text = builder.ToString();
        }

        // ---------- Toasts ----------

        // Queues a short message under the timer.
        private void ShowToast(string message) => toasts.Enqueue(message);

        // Shows each queued message for a few seconds, fading out at the end.
        private void UpdateToast()
        {
            toastTime -= Time.deltaTime;
            if (toastTime <= 0f && toasts.Count > 0)
            {
                toast.text = toasts.Dequeue();
                toastTime = 3.5f;
            }
            toast.color = UiKit.Fade(UiKit.Lantern, Mathf.Clamp01(toastTime / 0.6f));
        }

        // ---------- End of night ----------

        // Shows the end card once the night is over, with what happened and the night's numbers.
        private void UpdateEndPanel()
        {
            bool over = director.Result != NightDirector.Outcome.Playing;
            endPanel.SetActive(over);
            if (!over) return;
            prompt.text = "";
            switch (director.Result)
            {
                case NightDirector.Outcome.Escaped:
                    endTitle.text = "You rowed out of Hushwater.";
                    endBody.text = Campaign.BoatMessage;
                    break;
                case NightDirector.Outcome.Caught:
                    endTitle.text = "The Sexton found you.";
                    endBody.text = string.IsNullOrEmpty(player.DeathCause) ? "He never saw you. He heard you." : player.DeathCause + " Whatever you found tonight goes back into the silt.";
                    break;
                case NightDirector.Outcome.Transmitted:
                    endTitle.text = "The transmission went out.";
                    endBody.text = "Every word of it, out over the valley. Somewhere behind you the bell is still ringing.";
                    break;
                default:
                    endTitle.text = "The dawn bell rang.";
                    endBody.text = Campaign.Night == 5
                        ? "The rains came before the radio ever spoke. The lake comes back up over the chapel roof."
                        : "The light came up over the reservoir before you reached the boat. Tonight's finds are lost.";
                    break;
            }
            float t = director.Elapsed;
            endStats.text =
                $"Time in town  {(int)(t / 60f):00}:{(int)(t % 60f):00}      Evidence found tonight  {player.FoundTonight}\n" +
                $"Times he hunted you  {sexton.Hunts}      Knocked down  {player.Knockdowns}      Gasps  {player.Gasps}\n" +
                $"Difficulty  {GameSettings.Name(GameSettings.Active)}";
            string next = director.Result == NightDirector.Outcome.Transmitted || (director.Result == NightDirector.Outcome.Dawn && Campaign.Night == 5)
                ? "E / Enter  the ending" : "E / Enter  back to the boat";
            endKeys.text = director.CanRetryFromPost ? $"R  wake at your lantern post        {next}" : next;
        }
    }
}
