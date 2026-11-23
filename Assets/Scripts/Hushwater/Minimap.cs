using UnityEngine;
using UnityEngine.UI;

namespace Hushwater
{
    // Top-right minimap (ours, not the doc's: the doc never shows him). The explored map turns around Nell so
    // the way she faces is always up. It shows the boat, lit lantern posts and, if Settings allow, the Sexton
    // as a red dot in real time. When he is beyond its 40 m edge, the dot sits on the rim pointing his way.
    public sealed class Minimap : MonoBehaviour
    {
        private const float Size = 240f;
        private const float Range = 30f;                    // metres from the centre to the edge
        private const float Scale = (Size * 0.5f - 8f) / Range; // UI units per metre
        private const int PixelsPerMetre = 1;

        private PlayerController player;
        private SextonController sexton;
        private NightDirector director;
        private GameObject panel;
        private RectTransform world;
        private RawImage mapImage;
        private Texture2D texture;
        private Image sextonDot, sextonRing, boatIcon;
        private Text sextonLabel, footer;
        private RectTransform north;
        private float redrawTimer;

        public void Build(Transform root, PlayerController target, SextonController enemy, NightDirector night)
        {
            player = target;
            sexton = enemy;
            director = night;
            Vector2 topRight = new Vector2(1f, 1f), centre = new Vector2(0.5f, 0.5f);

            Image frame = UiKit.Panel(root, topRight, new Vector2(-30f, -110f), new Vector2(Size, Size + 30f));
            panel = frame.gameObject;
            Image window = UiKit.Box(frame.transform, new Color(0.06f, 0.06f, 0.055f, 1f), new Vector2(0.5f, 1f), Vector2.zero, new Vector2(Size, Size));
            window.gameObject.AddComponent<Mask>().showMaskGraphic = true;

            // Everything in "world" turns with Nell; the map image sits inside it in metres * Scale.
            world = new GameObject("World", typeof(RectTransform)).GetComponent<RectTransform>();
            world.SetParent(window.transform, false);
            world.anchorMin = world.anchorMax = world.pivot = centre;
            world.sizeDelta = Vector2.zero;
            mapImage = new GameObject("Map", typeof(RectTransform), typeof(RawImage)).GetComponent<RawImage>();
            mapImage.transform.SetParent(world, false);
            mapImage.raycastTarget = false;
            mapImage.color = new Color(1f, 1f, 1f, 0.9f);
            RectTransform mr = mapImage.rectTransform;
            mr.anchorMin = mr.anchorMax = mr.pivot = centre;
            mr.sizeDelta = new Vector2(Campaign.MapMaxX - Campaign.MapMinX, Campaign.MapMaxZ - Campaign.MapMinZ) * Scale;

            // Range rings at 10 and 20 m: his warning starts at about 24 m.
            for (int i = 1; i <= 2; i++)
            {
                Image ring = UiKit.Box(window.transform, new Color(0.9f, 0.85f, 0.7f, 0.18f), centre, Vector2.zero, Vector2.one * (i * 20f * Scale));
                ring.sprite = Ring();
            }
            boatIcon = UiKit.Icon(window.transform, Icons.Kind.Boat, new Color(0.95f, 0.45f, 0.35f), centre, Vector2.zero, 24f);
            sextonRing = UiKit.Glow(window.transform, new Color(1f, 0.2f, 0.1f, 0.6f), centre, Vector2.zero, 40f);
            sextonDot = UiKit.Icon(window.transform, Icons.Kind.Sexton, new Color(1f, 0.3f, 0.2f), centre, Vector2.zero, 26f);
            sextonLabel = UiKit.Text(window.transform, "", 13, centre, Vector2.zero, TextAnchor.MiddleCenter, new Vector2(80f, 18f), new Color(1f, 0.6f, 0.5f));
            UiKit.Icon(window.transform, Icons.Kind.Arrow, UiKit.Lantern, centre, Vector2.zero, 22f);
            UiKit.Frame(window.transform, UiKit.Fade(UiKit.Lantern, 0.6f), 2f);
            north = UiKit.Text(window.transform, "N", 14, centre, Vector2.zero, TextAnchor.MiddleCenter, new Vector2(20f, 20f), UiKit.Paper).rectTransform;
            footer = UiKit.Text(frame.transform, "", 13, new Vector2(0.5f, 0f), new Vector2(0f, 4f), TextAnchor.MiddleCenter, new Vector2(Size - 10f, 22f), UiKit.Fade(UiKit.Paper, 0.75f));
            footer.supportRichText = true;

            int w = Mathf.RoundToInt((Campaign.MapMaxX - Campaign.MapMinX) * PixelsPerMetre);
            int h = Mathf.RoundToInt((Campaign.MapMaxZ - Campaign.MapMinZ) * PixelsPerMetre);
            texture = new Texture2D(w, h, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp };
            mapImage.texture = texture;
            Redraw();
        }

        private static Sprite ring;
        private static Sprite Ring()
        {
            if (ring != null) return ring;
            const int n = 128;
            var tex = new Texture2D(n, n, TextureFormat.RGBA32, false);
            var px = new Color[n * n];
            for (int y = 0; y < n; y++)
                for (int x = 0; x < n; x++)
                {
                    float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(n * 0.5f, n * 0.5f));
                    px[y * n + x] = new Color(1f, 1f, 1f, Mathf.Clamp01(1.5f - Mathf.Abs(d - (n * 0.5f - 2f))));
                }
            tex.SetPixels(px);
            tex.Apply();
            return ring = Sprite.Create(tex, new Rect(0, 0, n, n), new Vector2(0.5f, 0.5f));
        }

        // The explored town at a metre a pixel: lake, paper where she's walked, buildings in ink.
        private void Redraw()
        {
            int w = texture.width, h = texture.height, night = Campaign.Night;
            float shore = TownLayout.Shoreline(night);
            Color unknown = new Color(0f, 0f, 0f, 0f), paper = new Color(0.55f, 0.52f, 0.45f), lake = UiKit.Lake;
            Color ink = new Color(0.18f, 0.15f, 0.12f), closed = new Color(0.22f, 0.11f, 0.08f);
            var pixels = new Color[w * h];
            for (int py = 0; py < h; py++)
            {
                float z = Campaign.MapMinZ + (py + 0.5f) / PixelsPerMetre;
                int cz = Mathf.FloorToInt((z - Campaign.MapMinZ) / Campaign.MapCell);
                for (int px = 0; px < w; px++)
                {
                    float x = Campaign.MapMinX + (px + 0.5f) / PixelsPerMetre;
                    bool seen = Campaign.Revealed(Mathf.FloorToInt((x - Campaign.MapMinX) / Campaign.MapCell), cz);
                    Color c = z < shore ? lake : z > TownLayout.NorthLimit(night) ? closed : seen ? paper : unknown;
                    if (seen && z >= shore)
                        foreach (BuildingDef b in TownBuilder.Buildings)
                            if (b.Footprint.Contains(new Vector2(x, z))) { c = ink; break; }
                    pixels[py * w + px] = c;
                }
            }
            texture.SetPixels(pixels);
            texture.Apply();
        }

        // Where a world point lands on the minimap, with Nell at the centre facing up.
        private Vector2 Local(Vector3 point, out float metres)
        {
            Vector3 to = point - player.transform.position;
            to.y = 0f;
            metres = to.magnitude;
            Vector3 forward = player.View.transform.forward;
            forward.y = 0f;
            float a = Vector3.SignedAngle(forward, to, Vector3.up) * Mathf.Deg2Rad;
            return new Vector2(Mathf.Sin(a), Mathf.Cos(a)) * metres * Scale;
        }

        private static Vector2 Clamp(Vector2 p, out bool clamped)
        {
            float edge = Size * 0.5f - 14f;
            clamped = p.magnitude > edge;
            return clamped ? p.normalized * edge : p;
        }

        private void Update()
        {
            if (player == null) return;
            bool show = director.Result == NightDirector.Outcome.Playing && !director.Paused && !director.JournalOpen;
            panel.SetActive(show);
            if (!show) return;

            redrawTimer -= Time.deltaTime;
            if (redrawTimer <= 0f) { redrawTimer = 0.5f; Redraw(); }

            // Turn the map so her heading is up.
            float yaw = player.View.transform.eulerAngles.y;
            world.localEulerAngles = new Vector3(0f, 0f, yaw);
            Vector3 p = player.transform.position;
            Vector2 mapCentre = new Vector2((Campaign.MapMinX + Campaign.MapMaxX) * 0.5f, (Campaign.MapMinZ + Campaign.MapMaxZ) * 0.5f);
            mapImage.rectTransform.anchoredPosition = (mapCentre - new Vector2(p.x, p.z)) * Scale;
            // "N" rides the rim to show which way north is.
            float a = -yaw * Mathf.Deg2Rad;
            north.anchoredPosition = Clamp(new Vector2(Mathf.Sin(a), Mathf.Cos(a)) * Size, out _);

            boatIcon.rectTransform.anchoredPosition = Clamp(Local(director.BoatPosition, out _), out _);

            bool present = GameSettings.SextonOnMinimap && sexton != null && sexton.isActiveAndEnabled && !sexton.WashedAway;
            sextonDot.enabled = sextonRing.enabled = sextonLabel.enabled = present;
            if (present)
            {
                Vector2 at = Clamp(Local(sexton.transform.position, out float metres), out bool off);
                sextonDot.rectTransform.anchoredPosition = at;
                sextonRing.rectTransform.anchoredPosition = at;
                bool hunting = sexton.CurrentState == SextonController.State.Hunt || sexton.CurrentState == SextonController.State.Kill;
                float pulse = 0.5f + 0.5f * Mathf.Sin(Time.time * (hunting ? 10f : 3f));
                sextonRing.color = new Color(1f, 0.2f, 0.1f, (off ? 0.2f : 0.35f) + 0.35f * pulse);
                sextonLabel.rectTransform.anchoredPosition = at + new Vector2(0f, at.y > 60f ? -24f : 24f);
                sextonLabel.text = $"{Mathf.RoundToInt(metres)} m";
                footer.text = $"<color=#E8705A>He is {Mathf.RoundToInt(metres)} m away{(hunting ? ", hunting" : "")}</color>";
            }
            else footer.text = "Facing up  ·  30 m to the edge";
        }
    }

    // Bottom-left: how loud the real room is, as the noise radius he'd hear (from MicListener).
    public sealed class VoiceMeter : MonoBehaviour
    {
        private const float BarWidth = 260f;
        private Image fill, walkMark, sprintMark, inputFill, thresholdMark;
        private Text label, value;
        private MicListener mic;
        private NightDirector director;
        private GameObject panel;

        public void Build(Transform root, MicListener listener, NightDirector night)
        {
            mic = listener;
            director = night;
            Image back = UiKit.Panel(root, Vector2.zero, new Vector2(30f, 62f), new Vector2(BarWidth + 130f, 58f));
            panel = back.gameObject;
            Transform p = back.transform;
            UiKit.Icon(p, Icons.Kind.Sound, UiKit.Lantern, new Vector2(0f, 0.5f), new Vector2(10f, 0f), 34f);
            label = UiKit.Text(p, "MIC", 13, new Vector2(0f, 1f), new Vector2(54f, -4f), TextAnchor.MiddleLeft, new Vector2(BarWidth, 18f), UiKit.Fade(UiKit.Paper, 0.75f));
            label.horizontalOverflow = HorizontalWrapMode.Overflow;
            Image track = UiKit.Box(p, new Color(1f, 1f, 1f, 0.08f), new Vector2(0f, 0f), new Vector2(54f, 10f), new Vector2(BarWidth, 14f));
            // Thin top line: the raw microphone input, so you can see it react to your voice even when it's quiet.
            inputFill = UiKit.Box(track.transform, new Color(0.75f, 0.85f, 0.95f, 0.7f), new Vector2(0f, 1f), new Vector2(0f, 4f), new Vector2(0f, 4f));
            thresholdMark = UiKit.Box(track.transform, new Color(0.75f, 0.85f, 0.95f, 0.9f), new Vector2(0f, 1f), new Vector2(BarWidth * MicListener.Threshold, 6f), new Vector2(2f, 10f));
            // Main bar: how far that sound carries (0-30 m), with marks at walking (6 m) and sprinting (18 m) loudness.
            fill = UiKit.Box(track.transform, UiKit.Lantern, new Vector2(0f, 0.5f), Vector2.zero, new Vector2(0f, 14f));
            walkMark = UiKit.Box(track.transform, UiKit.Fade(UiKit.Paper, 0.6f), new Vector2(0f, 0.5f), new Vector2(BarWidth * NoisePulse.Walk / 30f, 0f), new Vector2(2f, 20f));
            sprintMark = UiKit.Box(track.transform, new Color(0.9f, 0.4f, 0.3f, 0.8f), new Vector2(0f, 0.5f), new Vector2(BarWidth * NoisePulse.Sprint / 30f, 0f), new Vector2(2f, 20f));
            value = UiKit.Text(p, "", 15, new Vector2(1f, 0f), new Vector2(-8f, 6f), TextAnchor.MiddleRight, new Vector2(70f, 22f), UiKit.Paper);
        }

        private void Update()
        {
            if (director == null) return;
            bool show = director.Result == NightDirector.Outcome.Playing && !director.Paused && !director.JournalOpen;
            panel.SetActive(show);
            if (!show) return;
            if (!mic.Active)
            {
                label.text = mic.Status.ToUpperInvariant();
                fill.rectTransform.sizeDelta = new Vector2(0f, 14f);
                value.text = "";
                return;
            }
            inputFill.rectTransform.sizeDelta = new Vector2(BarWidth * mic.Input, 4f);
            if (mic.Status.StartsWith("No sound"))
            {
                label.text = "MIC: NO SOUND. Allow Unity in macOS Privacy & Security > Microphone";
                fill.rectTransform.sizeDelta = new Vector2(0f, 14f);
                value.text = "";
                return;
            }
            float r = mic.Radius;
            string name = mic.Status.Length > 22 ? mic.Status.Substring(0, 21) + "…" : mic.Status;
            label.text = $"MIC  ·  {name}";
            fill.rectTransform.sizeDelta = new Vector2(BarWidth * mic.Level, 14f);
            fill.color = r < NoisePulse.Walk ? new Color(0.6f, 0.8f, 0.5f) : r < NoisePulse.Sprint ? UiKit.Lantern : new Color(0.95f, 0.35f, 0.25f);
            value.text = r < 1.5f ? "quiet" : $"{Mathf.RoundToInt(r)} m";
            value.color = fill.color;
        }
    }
}
