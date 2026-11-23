using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.UI;

namespace Hushwater
{
    // On-screen help during a night (ours, not the doc's: players asked for it):
    // 1. "What to do": tonight's steps with live counts, and which way the next target is.
    // 2. Tips: a short step-by-step tutorial on Night 1, then one tip for each later night's new danger.
    // 3. The warning: when the Sexton is near, a banner says how close and which way, the screen edges
    //    pulse rust and a heartbeat quickens. (The doc keeps him unseen; this tells you, it doesn't show him.)
    public sealed class Guide : MonoBehaviour
    {
        private const float Near = 24f, Close = 13f, VeryClose = 7f;

        private sealed class Tip
        {
            public Icons.Kind Icon;
            public string Text;
            public System.Func<bool> Done;
            public float MinSeconds = 4f;
        }

        private PlayerController player;
        private SextonController sexton;
        private NightDirector director;

        private GameObject stepsPanel;
        private Text stepsText;
        private Text directionText;
        private Image directionArrow;
        private RectTransform rootRect;
        private Image waypointIcon, waypointGlow;
        private Text waypointText;
        private EvidenceSpot nextEvidence;

        private GameObject tipPanel;
        private Image tipIcon;
        private Text tipText, tipCount;
        private readonly List<Tip> tips = new List<Tip>();
        private int tipIndex;
        private float tipShown;

        private GameObject dangerPanel;
        private Image dangerIcon;
        private Text dangerText;
        private Image dangerArrow;
        private Image dangerEdges;
        private AudioSource heartbeat;
        private float beatTimer;

        private Vector3 start;
        private bool tookEvidence, openedJournal;
        private bool visible = true;

        public void Build(Transform root, PlayerController target, SextonController enemy, NightDirector night)
        {
            player = target;
            sexton = enemy;
            director = night;
            start = player.transform.position;
            player.EvidenceTaken += _ => tookEvidence = true;
            director.JournalChanged += open => { if (open) openedJournal = true; };

            dangerEdges = UiKit.Vignette(root, 0f);
            dangerEdges.color = new Color(0.75f, 0.12f, 0.06f, 0f);
            BuildSteps(root);
            BuildWaypoint(root);
            BuildTips(root);
            BuildDanger(root);
            heartbeat = gameObject.AddComponent<AudioSource>();
            heartbeat.clip = ProceduralSounds.Heartbeat;
            heartbeat.spatialBlend = 0f;
            heartbeat.playOnAwake = false;
            LoadTips();
        }

        // ---------- Layout ----------

        private void BuildSteps(Transform root)
        {
            Vector2 topRight = new Vector2(1f, 1f);
            // (Positions are the edge nearest the anchor: pivot = anchor.)
            // Below the minimap.
            Image panel = UiKit.Panel(root, topRight, new Vector2(-30f, -400f), new Vector2(470f, 230f));
            stepsPanel = panel.gameObject;
            UiKit.Text(panel.transform, "WHAT TO DO", 20, new Vector2(0f, 1f), new Vector2(18f, -12f), TextAnchor.MiddleLeft, new Vector2(240f, 28f), UiKit.Lantern);
            UiKit.Text(panel.transform, "H hide", 14, new Vector2(1f, 1f), new Vector2(-14f, -14f), TextAnchor.MiddleRight, new Vector2(100f, 24f), UiKit.Fade(UiKit.Paper, 0.5f));
            stepsText = UiKit.Text(panel.transform, "", 17, new Vector2(0.5f, 1f), new Vector2(0f, -46f), TextAnchor.UpperLeft, new Vector2(436f, 130f), UiKit.Paper);
            stepsText.supportRichText = true;
            stepsText.lineSpacing = 1.1f;
            UiKit.Box(panel.transform, UiKit.Fade(UiKit.Lantern, 0.25f), new Vector2(0.5f, 0f), new Vector2(0f, 50f), new Vector2(436f, 1f));
            directionArrow = UiKit.Icon(panel.transform, Icons.Kind.Arrow, UiKit.Lantern, new Vector2(0f, 0f), new Vector2(34f, 26f), 32f);
            directionArrow.rectTransform.pivot = new Vector2(0.5f, 0.5f); // turn about its centre
            directionText = UiKit.Text(panel.transform, "", 17, new Vector2(0f, 0f), new Vector2(62f, 10f), TextAnchor.MiddleLeft, new Vector2(390f, 32f), UiKit.Lantern);
        }

        private void BuildTips(Transform root)
        {
            Vector2 topLeft = new Vector2(0f, 1f);
            Image panel = UiKit.Panel(root, topLeft, new Vector2(30f, -190f), new Vector2(480f, 160f));
            tipPanel = panel.gameObject;
            UiKit.Text(panel.transform, "TIP", 16, new Vector2(0f, 1f), new Vector2(14f, -10f), TextAnchor.MiddleCenter, new Vector2(70f, 22f), UiKit.Lantern);
            tipIcon = UiKit.Icon(panel.transform, Icons.Kind.Lantern, UiKit.Lantern, new Vector2(0f, 0.5f), new Vector2(16f, -6f), 66f);
            tipText = UiKit.Text(panel.transform, "", 17, new Vector2(0f, 0.5f), new Vector2(98f, 0f), TextAnchor.MiddleLeft, new Vector2(368f, 140f), UiKit.Paper);
            tipCount = UiKit.Text(panel.transform, "", 13, new Vector2(1f, 0f), new Vector2(-12f, 6f), TextAnchor.MiddleRight, new Vector2(110f, 20f), UiKit.Fade(UiKit.Paper, 0.5f));
        }

        private void BuildDanger(Transform root)
        {
            Vector2 centre = new Vector2(0.5f, 0.5f);
            Image panel = UiKit.Box(root, new Color(0.08f, 0.02f, 0.01f, 0.72f), centre, new Vector2(0f, 230f), new Vector2(660f, 76f));
            UiKit.Frame(panel.transform, new Color(0.8f, 0.25f, 0.15f, 0.8f), 2f);
            dangerPanel = panel.gameObject;
            dangerIcon = UiKit.Icon(panel.transform, Icons.Kind.Sexton, new Color(0.95f, 0.4f, 0.3f), new Vector2(0f, 0.5f), new Vector2(12f, 0f), 50f);
            dangerText = UiKit.Text(panel.transform, "", 20, centre, new Vector2(30f, 0f), TextAnchor.MiddleCenter, new Vector2(570f, 70f), new Color(1f, 0.75f, 0.68f));
            dangerText.supportRichText = true;
            // An arrow that circles the crosshair, pointing toward him.
            dangerArrow = UiKit.Icon(root, Icons.Kind.Arrow, new Color(0.95f, 0.35f, 0.25f, 0.9f), centre, Vector2.zero, 44f);
            dangerPanel.SetActive(false);
        }

        // ---------- Tips ----------

        private void LoadTips()
        {
            int n = Campaign.Night;
            if (n == 1)
            {
                Add(Icons.Kind.Footsteps, "Move with W A S D and look with the mouse. Every step makes noise: the ring on the ground shows how far it carries.",
                    () => Vector3.Distance(player.transform.position, start) > 6f);
                Add(Icons.Kind.Crouch, "Press Ctrl (or C) to crouch. Creeping is nearly silent: only 2 m of noise. Shift sprints, but he hears that from 18 m.",
                    () => player.IsCrouching);
                Add(Icons.Kind.Lantern, "Your lantern is fully wound and lights the way. It dims slowly; hold F to crank it back up. Cranking is loud (12 m).",
                    () => false, 9f);
                Add(Icons.Kind.Camera, "Evidence glows: look for a column of golden light with a camera icon over it. The gold marker on screen leads you to the nearest one. Walk up to it and press E to photograph it.",
                    () => tookEvidence);
                Add(Icons.Kind.Sexton, "Slow knocking means he is walking his route. He is blind but hears everything. If the red warning appears, hide (E at a wardrobe) or stand still and hold Space.",
                    () => false, 12f);
                Add(Icons.Kind.Map, "Press Tab for your journal and the map. It draws itself as you walk and marks evidence you've passed.",
                    () => openedJournal);
                Add(Icons.Kind.Boat, "Once you've found tonight's evidence, follow the arrow under WHAT TO DO back to the boat and hold E to row out.",
                    () => Campaign.NightGoalMet(1), 10f);
            }
            else if (n == 2) Add(Icons.Kind.Eye, "New tonight: the Congregation, still figures that wail if your lantern stays on them (press L to lower it). Deep mud hides the Silt Hands; mash Space if they grab you.", () => false, 14f);
            else if (n == 3) Add(Icons.Kind.Breath, "New tonight: lower Millrow is flooded. Swim it holding Space, and watch your Breath bar. Lantern Moths eat your light; lower it (L) to scatter them.", () => false, 14f);
            else if (n == 4) Add(Icons.Kind.Sexton, "He guards the places you took evidence from. Expect him at sites you've already visited. Tonight you need the bell-tower photograph.", () => false, 14f);
            else Add(Icons.Kind.Bell, "Night 5: reach the radio room inside the dam (far north), hold E at the radio set, then survive 90 s. The chapel bell drowns every sound once a minute: move then.", () => false, 16f);
            tipShown = Time.time;
        }

        private void Add(Icons.Kind icon, string text, System.Func<bool> done, float min = 4f) =>
            tips.Add(new Tip { Icon = icon, Text = text, Done = done, MinSeconds = min });

        private void UpdateTips()
        {
            if (tipIndex < tips.Count)
            {
                Tip tip = tips[tipIndex];
                float shown = Time.time - tipShown;
                bool timedOnly = tip.Done() == false && tip.MinSeconds >= 9f && shown > tip.MinSeconds + 6f;
                if ((tip.Done() && shown > tip.MinSeconds) || timedOnly)
                {
                    tipIndex++;
                    tipShown = Time.time;
                    if (tipIndex < tips.Count) ProceduralSounds.PlayAt(ProceduralSounds.Rustle, player.transform.position, 0.4f);
                }
            }
            bool show = visible && tipIndex < tips.Count;
            tipPanel.SetActive(show);
            if (!show) return;
            tipIcon.sprite = Icons.Get(tips[tipIndex].Icon);
            tipText.text = tips[tipIndex].Text;
            tipCount.text = tips.Count > 1 ? $"{tipIndex + 1} / {tips.Count}" : "";
        }

        // ---------- What to do ----------

        private void UpdateSteps()
        {
            stepsPanel.SetActive(visible);
            if (!visible) return;
            int n = Campaign.Night;
            var b = new StringBuilder();
            Vector3? target = null;
            string targetName = "";
            if (n < 5)
            {
                bool met = Campaign.NightGoalMet(n);
                b.Append(Line(met, "1. Find tonight's evidence")).Append('\n');
                AppendNeed(b, EvidenceType.Photo, Story.PhotosNeeded(n), "photographs", "walk up, press E");
                AppendNeed(b, EvidenceType.Page, Story.PagesNeeded(n), "register pages", "walk up, press E");
                AppendNeed(b, EvidenceType.Tape, Story.TapesNeeded(n), "Emil's tapes", "walk up, press E");
                b.Append(Line(false, "2. Row out: hold E at the boat", !met));
                if (met) { target = director.BoatPosition; targetName = "the boat"; }
                else
                {
                    EvidenceSpot next = NearestNeeded(n);
                    nextEvidence = next;
                    if (next != null) { target = next.Position; targetName = $"{Kind(next)}: {next.Title}"; }
                }
            }
            else
            {
                bool started = director.Transmitting || director.Result == NightDirector.Outcome.Transmitted;
                b.Append(Line(started, "1. Reach the radio room in the dam (north)")).Append('\n');
                b.Append(Line(started, "2. Hold E at the radio set to transmit")).Append('\n');
                b.Append(Line(false, director.Transmitting ? $"3. Survive: {Mathf.CeilToInt(director.TransmitLeft)} s left" : "3. Survive 90 seconds", !started)).Append('\n');
                b.Append(Line(Campaign.SpillwayOpened, "Optional: open the spillway wheel", true));
                if (!started) { target = RadioRoom; targetName = "the radio room"; }
            }
            stepsText.text = b.ToString();

            directionArrow.enabled = target.HasValue;
            if (!target.HasValue) { directionText.text = director.Transmitting ? "Hold out. He's coming." : ""; return; }
            Vector3 to = target.Value - player.transform.position;
            to.y = 0f;
            Vector3 forward = player.View.transform.forward;
            forward.y = 0f;
            float angle = Vector3.SignedAngle(forward, to, Vector3.up);
            directionArrow.rectTransform.localEulerAngles = new Vector3(0f, 0f, -angle);
            float metres = to.magnitude;
            // Evidence distance is rounded, so the arrow guides without giving the spot away.
            directionText.text = metres < 3f ? $"{Capital(targetName)} is right here" : $"{Capital(targetName)}  ·  {Mathf.RoundToInt(metres)} m";
        }

        private static string Kind(EvidenceSpot e) => e.Type == EvidenceType.Photo ? "Photo" : e.Type == EvidenceType.Page ? "Page" : "Tape";

        // ---------- The waypoint: a marker on screen over the next evidence ----------

        private void BuildWaypoint(Transform root)
        {
            rootRect = (RectTransform)root;
            Vector2 centre = new Vector2(0.5f, 0.5f);
            waypointGlow = UiKit.Glow(root, UiKit.Fade(UiKit.Lantern, 0.35f), centre, Vector2.zero, 90f);
            waypointIcon = UiKit.Icon(root, Icons.Kind.Camera, UiKit.Lantern, centre, Vector2.zero, 44f);
            waypointText = UiKit.Text(root, "", 17, centre, Vector2.zero, TextAnchor.UpperCenter, new Vector2(420f, 70f), UiKit.Lantern);
            waypointText.supportRichText = true;
        }

        private void UpdateWaypoint()
        {
            bool show = visible && nextEvidence != null && Campaign.Night < 5 && !Campaign.NightGoalMet(Campaign.Night);
            waypointIcon.enabled = waypointGlow.enabled = waypointText.enabled = show;
            if (!show) return;
            EvidenceSpot e = nextEvidence;
            Vector3 world = e.Position + Vector3.up * 1.2f;
            Camera cam = player.View;
            Vector3 screen = cam.WorldToScreenPoint(world);
            // Off screen (or behind her): park it on the left or right edge, at eye height, and say which way to turn.
            Vector3 flat = e.Position - player.transform.position, forward = cam.transform.forward;
            flat.y = 0f;
            forward.y = 0f;
            float turn = Vector3.SignedAngle(forward, flat, Vector3.up);
            float margin = 110f;
            bool offScreen = screen.z < 0f || screen.x < margin || screen.x > Screen.width - margin;
            string side = "";
            if (offScreen)
            {
                screen = new Vector3(turn < 0f ? margin : Screen.width - margin, Screen.height * 0.5f, 0f);
                side = turn < 0f ? "  ◄ turn left" : "  turn right ►";
            }
            else screen.y = Mathf.Clamp(screen.y, margin * 1.8f, Screen.height - margin * 1.8f);
            Canvas canvas = rootRect.GetComponentInParent<Canvas>();
            Camera uiCam = canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera;
            RectTransformUtility.ScreenPointToLocalPointInRectangle(rootRect, screen, uiCam, out Vector2 local);
            Vector2 offset = rootRect.rect.center;
            Vector2 at = local - offset;
            // Keep the label (420 wide) fully on screen.
            float halfWidth = rootRect.rect.width * 0.5f - 220f;
            at.x = Mathf.Clamp(at.x, -halfWidth, halfWidth);

            waypointIcon.sprite = Icons.Get(e.Type == EvidenceType.Photo ? Icons.Kind.Camera : e.Type == EvidenceType.Page ? Icons.Kind.Page : Icons.Kind.Tape);
            waypointIcon.rectTransform.anchoredPosition = at;
            waypointGlow.rectTransform.anchoredPosition = at;
            waypointText.rectTransform.anchoredPosition = at + new Vector2(0f, -62f);

            float metres = Vector3.Distance(player.transform.position, e.Position);
            string where = "";
            foreach (BuildingDef b in TownBuilder.Buildings)
                if (b.Footprint.Contains(new Vector2(e.Position.x, e.Position.z))) { where = $"inside {b.Name}"; break; }
            string how = e.Type == EvidenceType.Photo
                ? (metres < PhotoSubject.SnapReach ? "press E to photograph it" : metres < 15f ? "walk into the light, then press E" : "")
                : (metres < 4f ? (e.InCabinet ? "hold E to force the cabinet" : "press E to pick it up") : "");
            string line2 = how != "" ? how : where;
            waypointText.text = $"{Kind(e).ToUpperInvariant()}  {Mathf.RoundToInt(metres)} m{side.Replace("◄", "<").Replace("►", ">")}" +
                                (line2 != "" ? $"\n<size=14><color=#{UiKit.Hex(UiKit.Paper)}>{line2}</color></size>" : "");
            float pulse = 0.7f + 0.3f * Mathf.Sin(Time.time * 3f);
            waypointGlow.color = UiKit.Fade(UiKit.Lantern, 0.3f * pulse);
        }

        private static string Capital(string s) => char.ToUpperInvariant(s[0]) + s.Substring(1);

        // The radio set in the radio room (TownBuilder: "Radio Set").
        private static readonly Vector3 RadioRoom = new Vector3(-3f, 0f, 95f);

        private static string Line(bool done, string text, bool dim = false)
        {
            string colour = done ? "#9AD27F" : dim ? "#" + UiKit.Hex(UiKit.Fade(UiKit.Paper, 0.45f)) : "#" + UiKit.Hex(UiKit.Paper);
            return $"<color={colour}>{(done ? "[x] " : "[ ] ")}{text}</color>";
        }

        private static void AppendNeed(StringBuilder b, EvidenceType type, int needed, string name, string how)
        {
            if (needed <= 0) return;
            int have = Mathf.Min(needed, Campaign.CountForNight(Campaign.Night, type));
            bool done = have >= needed;
            string colour = done ? "#9AD27F" : "#" + UiKit.Hex(UiKit.Lantern);
            b.Append($"    <color={colour}>{name} {have} / {needed}</color>");
            if (!done) b.Append($"  <color=#{UiKit.Hex(UiKit.Fade(UiKit.Paper, 0.55f))}>({how})</color>");
            b.Append('\n');
        }

        private EvidenceSpot NearestNeeded(int night)
        {
            EvidenceSpot best = null;
            float bestDistance = float.MaxValue;
            foreach (EvidenceSpot e in Story.Evidence)
            {
                if (e.Night > night || Campaign.Has(e.Id)) continue;
                if (!TownLayout.Open(night, e.Position) || e.Position.z < TownLayout.Shoreline(night)) continue;
                int needed = e.Type == EvidenceType.Photo ? Story.PhotosNeeded(night) : e.Type == EvidenceType.Page ? Story.PagesNeeded(night) : Story.TapesNeeded(night);
                if (Campaign.CountForNight(night, e.Type) >= needed) continue;
                float d = Vector3.Distance(player.transform.position, e.Position);
                if (d < bestDistance) { best = e; bestDistance = d; }
            }
            return best;
        }

        // ---------- He is near ----------

        private void UpdateDanger()
        {
            bool present = sexton != null && sexton.isActiveAndEnabled && !sexton.WashedAway && !player.IsDead;
            Vector3 to = present ? sexton.transform.position - player.transform.position : Vector3.zero;
            to.y = 0f;
            float distance = present ? to.magnitude : float.MaxValue;
            SextonController.State state = present ? sexton.CurrentState : SextonController.State.Patrol;
            bool hunting = state == SextonController.State.Hunt || state == SextonController.State.Kill;
            bool show = present && (distance < Near || (hunting && distance < Near * 1.6f));
            dangerPanel.SetActive(show);
            dangerArrow.enabled = show && distance > 2f;

            float level = !show ? 0f : distance < VeryClose ? 1f : distance < Close ? 0.65f : 0.3f;
            if (hunting && show) level = Mathf.Max(level, 0.7f);
            float pulse = 0.5f + 0.5f * Mathf.Sin(Time.time * Mathf.Lerp(3f, 11f, level));
            dangerEdges.color = new Color(0.75f, 0.12f, 0.06f, level * (0.35f + 0.35f * pulse));
            if (!show) { beatTimer = 0f; return; }

            Vector3 forward = player.View.transform.forward;
            forward.y = 0f;
            float angle = Vector3.SignedAngle(forward, to, Vector3.up);
            string where = Mathf.Abs(angle) < 40f ? "ahead of you" : Mathf.Abs(angle) > 140f ? "behind you" : angle > 0f ? "to your right" : "to your left";
            string advice = player.IsHiding ? "Stay hidden. Don't move." :
                distance < VeryClose ? "Hold your breath (Space). Don't move." :
                hunting ? "He's hunting you. Get out of sight and hide." :
                distance < Close ? "Crouch (Ctrl) or find a hiding spot." : "Move quietly.";
            string headline = hunting ? "HE IS HUNTING YOU" : distance < VeryClose ? "HE IS RIGHT THERE" : distance < Close ? "HE IS CLOSE" : "HE IS NEAR";
            dangerText.text = $"<size=26>{headline}</size>  ·  {Mathf.RoundToInt(distance)} m {where}\n<size=17>{advice}</size>";
            dangerPanel.GetComponent<Image>().color = new Color(0.08f + 0.2f * level * pulse, 0.02f, 0.01f, 0.75f);
            dangerIcon.color = Color.Lerp(new Color(0.95f, 0.55f, 0.4f), new Color(1f, 0.25f, 0.15f), pulse * level);

            float rad = (90f - angle) * Mathf.Deg2Rad;
            dangerArrow.rectTransform.anchoredPosition = new Vector2(Mathf.Cos(rad), Mathf.Sin(rad)) * 150f;
            dangerArrow.rectTransform.localEulerAngles = new Vector3(0f, 0f, -angle);

            // Heartbeat: slow at the edge of range, racing when he's on top of her.
            beatTimer -= Time.deltaTime;
            if (beatTimer <= 0f)
            {
                beatTimer = Mathf.Lerp(1.1f, 0.38f, level);
                heartbeat.volume = Mathf.Lerp(0.35f, 0.9f, level);
                heartbeat.Play();
            }
        }

        private void Update()
        {
            if (player == null) return;
            if (Input.GetKeyDown(KeyCode.H)) visible = !visible;
            bool playing = director.Result == NightDirector.Outcome.Playing && !director.Paused && !director.JournalOpen;
            if (!playing)
            {
                stepsPanel.SetActive(false);
                if (waypointIcon != null) waypointIcon.enabled = waypointGlow.enabled = waypointText.enabled = false;
                tipPanel.SetActive(false);
                dangerPanel.SetActive(false);
                dangerArrow.enabled = false;
                dangerEdges.color = Color.clear;
                return;
            }
            UpdateSteps();
            UpdateWaypoint();
            UpdateTips();
            UpdateDanger();
        }
    }
}
