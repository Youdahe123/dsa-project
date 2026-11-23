using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.UI;

namespace Hushwater
{
    // The settings screen from the design doc: difficulty, volume, the accessibility toggles (sound ripples, captions),
    // brightness. Look sensitivity is ours. Shared by the main menu, the pause menu and the boat.
    public sealed class SettingsPanel
    {
        private readonly GameObject root;
        private readonly List<Action> refreshers = new List<Action>();
        private readonly Button first;
        private readonly Text note;
        private readonly Action closed;

        public bool Visible => root.activeSelf;

        public SettingsPanel(Transform parent, Action onClosed)
        {
            closed = onClosed;
            Image back = UiKit.Overlay(parent, UiKit.Fade(UiKit.Night, 0.96f));
            back.raycastTarget = true;
            root = back.gameObject;
            Transform p = root.transform;
            Vector2 centre = new Vector2(0.5f, 0.5f);
            UiKit.Text(p, "SETTINGS", 44, centre, new Vector2(0f, 405f), TextAnchor.MiddleCenter, new Vector2(800f, 60f), UiKit.Paper);

            first = Cycle(p, 330f, () => $"Difficulty:  {GameSettings.Name(GameSettings.Selected)}",
                () => GameSettings.Selected = (Difficulty)(((int)GameSettings.Selected + 1) % 3));
            Stepper(p, 268f, () => $"Master volume  {Mathf.RoundToInt(GameSettings.MasterVolume * 100f)}%",
                d => GameSettings.MasterVolume = Mathf.Clamp01(GameSettings.MasterVolume + d * 0.1f));
            Stepper(p, 206f, () => $"Brightness  {Mathf.RoundToInt(GameSettings.Brightness * 100f)}%",
                d => GameSettings.Brightness = Mathf.Clamp(GameSettings.Brightness + d * 0.1f, 0.5f, 2f));
            Stepper(p, 144f, () => $"Look sensitivity  {GameSettings.LookSensitivity:0.0}×",
                d => GameSettings.LookSensitivity = Mathf.Clamp(GameSettings.LookSensitivity + d * 0.1f, 0.3f, 3f));
            Cycle(p, 82f, () => $"Sound ripples:  {(GameSettings.SoundRipples ? "On" : "Off")}", () => GameSettings.SoundRipples = !GameSettings.SoundRipples);
            Cycle(p, 20f, () => $"Captions:  {(GameSettings.Captions ? "On" : "Off")}", () => GameSettings.Captions = !GameSettings.Captions);
            Cycle(p, -42f, () => $"Microphone:  {MicName()}", NextMic);
            Stepper(p, -104f, () => $"Mic sensitivity  {GameSettings.MicSensitivity:0.0}×",
                d => GameSettings.MicSensitivity = Mathf.Clamp(GameSettings.MicSensitivity + d * 0.1f, 0.3f, 3f));
            Cycle(p, -166f, () => $"Sexton on minimap:  {(GameSettings.SextonOnMinimap ? "On" : "Off")}", () => GameSettings.SextonOnMinimap = !GameSettings.SextonOnMinimap);
            UiKit.Button(p, "Back", new Vector2(0f, -240f), new Vector2(420f, 56f), () => Show(false));
            note = UiKit.Text(p, "", 17, centre, new Vector2(0f, -320f), TextAnchor.UpperCenter, new Vector2(900f, 80f), UiKit.Lantern);
            root.SetActive(false);
        }

        public void Show(bool show)
        {
            root.SetActive(show);
            if (show)
            {
                Refresh();
                UiKit.Select(first);
            }
            else
            {
                GameSettings.SavePrefs();
                closed?.Invoke();
            }
        }

        private void Refresh()
        {
            foreach (Action refresh in refreshers) refresh();
            note.text = GameSettings.Blurb(GameSettings.Selected) +
                        (GameSettings.MicEnabled ? "\nMicrophone: talking, breathing and bumps near the mic are noise he can hear. Changes start next night." : "") +
                        (NightDirector.Instance != null && GameSettings.Selected != GameSettings.Active ? "\nThe new difficulty starts with the next night." : "");
        }

        // Off, then the system default, then each named input (AirPods, a headset, the built-in mic...).
        private static string MicName()
        {
            if (!GameSettings.MicEnabled) return "Off";
            return string.IsNullOrEmpty(GameSettings.MicDevice) ? "System default" : GameSettings.MicDevice;
        }

        private static void NextMic()
        {
            string[] devices = Microphone.devices;
            if (!GameSettings.MicEnabled) { GameSettings.MicEnabled = true; GameSettings.MicDevice = ""; return; }
            int index = string.IsNullOrEmpty(GameSettings.MicDevice) ? -1 : Array.IndexOf(devices, GameSettings.MicDevice);
            if (index + 1 < devices.Length) GameSettings.MicDevice = devices[index + 1];
            else { GameSettings.MicEnabled = false; GameSettings.MicDevice = ""; }
        }

        private Button Cycle(Transform p, float y, Func<string> label, Action onClick)
        {
            Button button = UiKit.Button(p, "", new Vector2(0f, y), new Vector2(520f, 56f), () =>
            {
                onClick();
                GameSettings.SavePrefs();
                Refresh();
            });
            refreshers.Add(() => UiKit.Label(button, label()));
            return button;
        }

        private void Stepper(Transform p, float y, Func<string> label, Action<int> step)
        {
            Text text = UiKit.Text(p, "", 24, new Vector2(0.5f, 0.5f), new Vector2(0f, y), TextAnchor.MiddleCenter, new Vector2(400f, 56f), UiKit.Paper);
            UiKit.Button(p, "−", new Vector2(-230f, y), new Vector2(56f, 56f), () => { step(-1); GameSettings.SavePrefs(); Refresh(); });
            UiKit.Button(p, "+", new Vector2(230f, y), new Vector2(56f, 56f), () => { step(1); GameSettings.SavePrefs(); Refresh(); });
            refreshers.Add(() => text.text = label());
        }
    }

    // Design doc: "The journal holds every photo, register page and tape transcript found so far, plus a hand-drawn map
    // that fills in as Nell explores. The map never shows the Sexton."
    public sealed class JournalView
    {
        private const int PixelsPerMetre = 4;
        private readonly GameObject root;
        private readonly Text heading;
        private readonly Text content;
        private readonly RawImage map;
        private readonly GameObject mapGroup;
        private readonly Transform markers;
        private Text mapNote;
        // Where the map sits on the 1920x1080 page, and how big. The town is 54 m wide and 116 m long.
        private const float MapH = 700f;
        private static readonly float MapW = MapH * (Campaign.MapMaxX - Campaign.MapMinX) / (Campaign.MapMaxZ - Campaign.MapMinZ);
        private const float MapX = -420f, MapY = -60f;
        private readonly Button[] tabs = new Button[4];
        private readonly Action closed;
        private Texture2D texture;
        private int tab;

        public bool Visible => root.activeSelf;

        public JournalView(Transform parent, Action onClosed)
        {
            closed = onClosed;
            Image back = UiKit.Overlay(parent, new Color(0.08f, 0.075f, 0.065f, 0.97f));
            back.raycastTarget = true;
            root = back.gameObject;
            Transform p = root.transform;
            Vector2 centre = new Vector2(0.5f, 0.5f);
            UiKit.Text(p, "NELL VARGA · SURVEY JOURNAL", 30, centre, new Vector2(0f, 480f), TextAnchor.MiddleCenter, new Vector2(1200f, 44f), UiKit.Paper);

            string[] names = { "Photographs", "Register", "Emil's tapes", "Map" };
            for (int i = 0; i < 4; i++)
            {
                int index = i;
                tabs[i] = UiKit.Button(p, names[i], new Vector2(-540f + i * 300f, 410f), new Vector2(270f, 50f), () => SetTab(index));
            }
            heading = UiKit.Text(p, "", 22, centre, new Vector2(0f, 345f), TextAnchor.MiddleCenter, new Vector2(1400f, 34f), UiKit.Lantern);
            content = UiKit.Text(p, "", 18, new Vector2(0.5f, 1f), new Vector2(0f, -250f), TextAnchor.UpperLeft, new Vector2(1400f, 760f), UiKit.Paper);
            content.lineSpacing = 1.15f;
            content.supportRichText = true;

            mapGroup = new GameObject("Map", typeof(RectTransform));
            mapGroup.transform.SetParent(p, false);
            UiKit.Stretch((RectTransform)mapGroup.transform);
            map = new GameObject("Map Image").AddComponent<RawImage>();
            map.transform.SetParent(mapGroup.transform, false);
            map.rectTransform.anchorMin = map.rectTransform.anchorMax = centre;
            map.rectTransform.anchoredPosition = new Vector2(MapX, MapY);
            map.rectTransform.sizeDelta = new Vector2(MapW, MapH);
            UiKit.Frame(map.transform, UiKit.Fade(UiKit.Lantern, 0.4f), 2f);
            // Icons drawn over the paper: rebuilt every time the map is opened.
            markers = new GameObject("Map Markers", typeof(RectTransform)).transform;
            markers.SetParent(map.transform, false);
            UiKit.Stretch((RectTransform)markers);
            UiKit.Text(mapGroup.transform, "NORTH", 20, centre, new Vector2(MapX, MapY + MapH * 0.5f + 18f), TextAnchor.MiddleCenter, new Vector2(100f, 30f), UiKit.Paper);
            float Y(float z) => MapY - MapH * 0.5f + MapH * (z - Campaign.MapMinZ) / (Campaign.MapMaxZ - Campaign.MapMinZ);
            float labelX = MapX + MapW * 0.5f + 16f;
            UiKit.Text(mapGroup.transform, "— MILLROW", 18, centre, new Vector2(labelX + 110f, Y(4f)), TextAnchor.MiddleLeft, new Vector2(220f, 30f), UiKit.Lantern);
            UiKit.Text(mapGroup.transform, "— CHAPEL HILL", 18, centre, new Vector2(labelX + 110f, Y(43f)), TextAnchor.MiddleLeft, new Vector2(220f, 30f), UiKit.Lantern);
            UiKit.Text(mapGroup.transform, "— THE DAM WORKS", 18, centre, new Vector2(labelX + 110f, Y(81f)), TextAnchor.MiddleLeft, new Vector2(220f, 30f), UiKit.Lantern);
            UiKit.Text(mapGroup.transform, "— the boat & the lake", 16, centre, new Vector2(labelX + 110f, Y(-14f)), TextAnchor.MiddleLeft, new Vector2(220f, 30f), UiKit.Fade(UiKit.Paper, 0.8f));
            BuildLegend(mapGroup.transform);
            UiKit.Button(p, "Close  (Tab / Esc)", new Vector2(0f, -490f), new Vector2(360f, 50f), () => closed?.Invoke());
            root.SetActive(false);
        }

        public void Show(bool show)
        {
            root.SetActive(show);
            if (!show) return;
            SetTab(tab);
            UiKit.Select(tabs[tab]);
        }

        private void SetTab(int index)
        {
            tab = index;
            bool showMap = tab == 3;
            mapGroup.SetActive(showMap);
            content.gameObject.SetActive(!showMap);
            for (int i = 0; i < tabs.Length; i++) tabs[i].GetComponent<Image>().color = i == tab ? UiKit.Fade(UiKit.Lantern, 0.45f) : Color.white;
            switch (tab)
            {
                case 0: Photographs(); break;
                case 1: Register(); break;
                case 2: Tapes(); break;
                default: DrawMap(); break;
            }
        }

        private void Photographs()
        {
            var b = new StringBuilder();
            int found = 0, total = 0;
            foreach (EvidenceSpot spot in Story.Evidence)
            {
                if (spot.Type != EvidenceType.Photo) continue;
                total++;
                if (!Campaign.Has(spot.Id)) continue;
                found++;
                string pending = Campaign.Banked(spot.Id) ? "" : "  <i>(still on the film: row out to keep it)</i>";
                b.Append($"<b>{spot.Title}</b>{pending}\n{spot.Text}\n\n");
            }
            heading.text = $"PHOTOGRAPHS  {found} / {total}";
            content.text = found == 0 ? "No photographs yet. Raise the camera (right mouse) and frame what the water left behind." : b.ToString();
        }

        private void Register()
        {
            var b = new StringBuilder();
            int found = 0;
            foreach (EvidenceSpot spot in Story.Evidence)
            {
                if (spot.Type != EvidenceType.Page) continue;
                if (Campaign.Has(spot.Id))
                {
                    found++;
                    b.Append($"<b>{spot.Title}.</b>  {spot.Text}\n");
                }
                else b.Append($"<color=#{UiKit.Hex(UiKit.Fade(UiKit.Paper, 0.35f))}>{spot.Title}.  missing</color>\n");
            }
            heading.text = $"PARISH REGISTER  {found} / {Story.TotalPages} pages   ·   {Story.RollCallPages} are needed to read every name";
            content.text = b.ToString();
        }

        private void Tapes()
        {
            var b = new StringBuilder();
            int found = 0;
            foreach (EvidenceSpot spot in Story.Evidence)
            {
                if (spot.Type != EvidenceType.Tape) continue;
                if (!Campaign.Has(spot.Id)) continue;
                found++;
                b.Append(Campaign.TapePlayed(spot.Id)
                    ? $"<b>{spot.Title}</b>\n\"{spot.Text}\"\n\n"
                    : $"<b>{spot.Title}</b>\n<i>Not played yet. Play it at the boat.</i>\n\n");
            }
            heading.text = $"EMIL'S TAPES  {found} / {Story.TotalTapes}";
            content.text = found == 0 ? "Emil hid his tapes around the town. Find them and play them at the boat." : b.ToString();
        }

        // Paper where she has walked, ink outlines for buildings she has seen, the lake at tonight's level.
        private void DrawMap()
        {
            int w = Mathf.RoundToInt((Campaign.MapMaxX - Campaign.MapMinX) * PixelsPerMetre);
            int h = Mathf.RoundToInt((Campaign.MapMaxZ - Campaign.MapMinZ) * PixelsPerMetre);
            if (texture == null)
            {
                texture = new Texture2D(w, h, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp };
                map.texture = texture;
            }
            int night = Campaign.Night;
            float shore = TownLayout.Shoreline(night);
            Color unknown = new Color(0.12f, 0.115f, 0.1f, 1f);
            Color paperColor = new Color(0.78f, 0.74f, 0.64f);
            Color ink = new Color(0.2f, 0.17f, 0.13f);
            Color wall = new Color(0.55f, 0.5f, 0.42f);
            Color lake = UiKit.Lake;
            var pixels = new Color[w * h];
            for (int py = 0; py < h; py++)
            {
                float z = Campaign.MapMinZ + (py + 0.5f) / PixelsPerMetre;
                int cz = Mathf.FloorToInt((z - Campaign.MapMinZ) / Campaign.MapCell);
                for (int px = 0; px < w; px++)
                {
                    float x = Campaign.MapMinX + (px + 0.5f) / PixelsPerMetre;
                    int cx = Mathf.FloorToInt((x - Campaign.MapMinX) / Campaign.MapCell);
                    bool seen = Campaign.Revealed(cx, cz);
                    Color c = z < shore ? lake : seen ? paperColor : unknown;
                    // Districts closed tonight: hatched in rust, behind the rubble.
                    if (z > TownLayout.NorthLimit(night))
                        c = ((px + py) / 4) % 2 == 0 ? new Color(0.24f, 0.12f, 0.09f) : new Color(0.16f, 0.1f, 0.08f);
                    if (seen || z < shore)
                    {
                        Vector2 point = new Vector2(x, z);
                        foreach (BuildingDef b in TownBuilder.Buildings)
                        {
                            Rect r = b.Footprint;
                            if (!r.Contains(point)) continue;
                            bool edge = x - r.xMin < 0.4f || r.xMax - x < 0.4f || z - r.yMin < 0.4f || r.yMax - z < 0.4f;
                            c = z < shore ? Color.Lerp(lake, ink, edge ? 0.6f : 0.2f) : edge ? ink : wall;
                        }
                        if (seen && z >= shore)
                        {
                            if (Outline(TownBuilder.Graveyard, point)) c = ink;
                            if (TownBuilder.SpillwayChannel.Contains(point)) c = Color.Lerp(c, lake, 0.5f);
                            if (Outline(TownBuilder.Culvert, point)) c = ink;
                        }
                    }
                    pixels[py * w + px] = c;
                }
            }
            texture.SetPixels(pixels);
            texture.Apply();
            PlaceMarkers(night);
            heading.text = $"THE TOWN  ·  Night {night}  ·  the lake reaches {(shore <= TownLayout.MillrowStart ? "the quay" : TownLayout.District(shore))}";
        }

        // The legend beside the map: what each mark means, and how the map fills in.
        private void BuildLegend(Transform parent)
        {
            Vector2 centre = new Vector2(0.5f, 0.5f);
            UiKit.Panel(parent, centre, new Vector2(390f, -60f), new Vector2(700f, 700f));
            UiKit.Text(parent, "How the map works", 30, centre, new Vector2(390f, 250f), TextAnchor.MiddleCenter, new Vector2(640f, 44f), UiKit.Lantern);
            UiKit.Text(parent,
                "Nell draws the map as she walks. Everything within about 8 m of where you've been is filled in on paper; " +
                "dark ground is still unexplored. North is up: the boat waits at the lake in the south, the dam is at the top. " +
                "It's saved each time you reach the boat.",
                17, centre, new Vector2(390f, 165f), TextAnchor.UpperLeft, new Vector2(620f, 120f), UiKit.Paper);
            var rows = new (Icons.Kind icon, Color color, string text)[]
            {
                (Icons.Kind.Arrow, UiKit.Lantern, "You, pointing the way you face"),
                (Icons.Kind.Boat, new Color(0.95f, 0.55f, 0.4f), "The boat: hold E there to row out"),
                (Icons.Kind.LanternPost, UiKit.Lantern, "Lantern post you've lit (you wake here)"),
                (Icons.Kind.LanternPost, new Color(0.6f, 0.58f, 0.52f), "Lantern post, not lit yet"),
                (Icons.Kind.Wardrobe, new Color(0.7f, 0.82f, 0.8f), "Hiding spot: wardrobe, pew or jetty"),
                (Icons.Kind.Eye, new Color(1f, 0.93f, 0.6f), "Evidence glint you've passed but not taken"),
                (Icons.Kind.Check, new Color(0.6f, 0.85f, 0.5f), "Evidence you've collected"),
            };
            for (int i = 0; i < rows.Length; i++)
            {
                float y = 70f - i * 52f;
                UiKit.Icon(parent, rows[i].icon, rows[i].color, centre, new Vector2(90f, y), 40f);
                UiKit.Text(parent, rows[i].text, 18, centre, new Vector2(400f, y), TextAnchor.MiddleLeft, new Vector2(560f, 40f), UiKit.Paper);
            }
            UiKit.Box(parent, UiKit.Lake, centre, new Vector2(90f, 70f - 7 * 52f), new Vector2(36f, 24f));
            UiKit.Text(parent, "The lake (it rises every night)", 18, centre, new Vector2(400f, 70f - 7 * 52f), TextAnchor.MiddleLeft, new Vector2(560f, 40f), UiKit.Paper);
            UiKit.Box(parent, new Color(0.24f, 0.12f, 0.09f), centre, new Vector2(90f, 70f - 8 * 52f), new Vector2(36f, 24f));
            UiKit.Text(parent, "Closed tonight (rubble blocks the way)", 18, centre, new Vector2(400f, 70f - 8 * 52f), TextAnchor.MiddleLeft, new Vector2(560f, 40f), UiKit.Paper);
            mapNote = UiKit.Text(parent, "", 17, centre, new Vector2(390f, -385f), TextAnchor.MiddleCenter, new Vector2(640f, 40f), UiKit.Fade(UiKit.Rust, 1f));
            mapNote.color = Color.Lerp(UiKit.Rust, Color.white, 0.35f);
        }

        // World (x, z) to a point on the map image.
        private static Vector2 MapPoint(Vector3 world) => new Vector2(
            ((world.x - Campaign.MapMinX) / (Campaign.MapMaxX - Campaign.MapMinX) - 0.5f) * MapW,
            ((world.z - Campaign.MapMinZ) / (Campaign.MapMaxZ - Campaign.MapMinZ) - 0.5f) * MapH);

        private bool Seen(Vector3 world) => Campaign.Revealed(
            Mathf.FloorToInt((world.x - Campaign.MapMinX) / Campaign.MapCell), Mathf.FloorToInt((world.z - Campaign.MapMinZ) / Campaign.MapCell));

        private void Marker(Icons.Kind kind, Color color, Vector3 world, float size, float rotation = 0f)
        {
            Image icon = UiKit.Icon(markers, kind, color, new Vector2(0.5f, 0.5f), MapPoint(world), size);
            icon.rectTransform.localEulerAngles = new Vector3(0f, 0f, rotation);
            Shadow shadow = icon.gameObject.AddComponent<Shadow>();
            shadow.effectColor = new Color(0f, 0f, 0f, 0.9f);
            shadow.effectDistance = new Vector2(1.5f, -1.5f);
        }

        // Names on the buildings she has seen, then the icons. Never the Sexton: "The map never shows the Sexton."
        private void PlaceMarkers(int night)
        {
            for (int i = markers.childCount - 1; i >= 0; i--) UnityEngine.Object.Destroy(markers.GetChild(i).gameObject);
            Color ink = new Color(0.18f, 0.14f, 0.1f);
            foreach (BuildingDef b in TownBuilder.Buildings)
            {
                Vector3 c = new Vector3(b.Centre.x, 0f, b.Centre.y);
                if (!Seen(c) || c.z < TownLayout.Shoreline(night) || c.z > TownLayout.NorthLimit(night)) continue;
                Text label = UiKit.Text(markers, b.Name, 11, new Vector2(0.5f, 0.5f), MapPoint(c), TextAnchor.MiddleCenter,
                    new Vector2(b.Size.x / (Campaign.MapMaxX - Campaign.MapMinX) * MapW + 10f, 40f), ink);
                label.GetComponent<Shadow>().enabled = false;
            }
            bool inTown = PlayerController.Instance != null;
            if (inTown)
            {
                foreach (HideSpot spot in UnityEngine.Object.FindObjectsByType<HideSpot>())
                    if (Seen(spot.transform.position)) Marker(Icons.Kind.Wardrobe, new Color(0.7f, 0.82f, 0.8f), spot.transform.position, 14f);
                foreach (LanternPost post in UnityEngine.Object.FindObjectsByType<LanternPost>())
                    if (Seen(post.transform.position)) Marker(Icons.Kind.LanternPost, post.Lit ? UiKit.Lantern : new Color(0.6f, 0.58f, 0.52f), post.transform.position, 18f);
            }
            int glints = 0;
            foreach (EvidenceSpot e in Story.Evidence)
            {
                // Anything from tonight or earlier that's still above water and in an open district.
                if (e.Night > night || !TownLayout.Open(night, e.Position) || e.Position.z < TownLayout.Shoreline(night)) continue;
                if (Campaign.Has(e.Id)) Marker(Icons.Kind.Check, new Color(0.6f, 0.85f, 0.5f), e.Position, 16f);
                else if (inTown && Seen(e.Position)) { Marker(Icons.Kind.Eye, new Color(1f, 0.93f, 0.6f), e.Position, 16f); glints++; }
            }
            Marker(Icons.Kind.Boat, new Color(0.95f, 0.55f, 0.4f), TownLayout.BoatPosition(night), 26f);
            if (inTown)
            {
                Transform nell = PlayerController.Instance.transform;
                Marker(Icons.Kind.Arrow, UiKit.Lantern, nell.position, 22f, -nell.eulerAngles.y);
            }
            mapNote.text = glints > 0 ? $"{glints} evidence glint{(glints == 1 ? "" : "s")} you've walked past are marked. Go back for them." :
                inTown ? "He is never shown on the map. Listen for him." : "At the boat. The map shows tonight's town as you left it.";
        }

        private static bool Outline(Rect r, Vector2 p) =>
            r.Contains(p) && (p.x - r.xMin < 0.3f || r.xMax - p.x < 0.3f || p.y - r.yMin < 0.3f || r.yMax - p.y < 0.3f);

        private static void Mark(Color[] pixels, int w, int h, Vector3 world, Color color, int radius)
        {
            int cx = Mathf.RoundToInt((world.x - Campaign.MapMinX) * PixelsPerMetre);
            int cy = Mathf.RoundToInt((world.z - Campaign.MapMinZ) * PixelsPerMetre);
            for (int y = cy - radius; y <= cy + radius; y++)
                for (int x = cx - radius; x <= cx + radius; x++)
                    if (x >= 0 && y >= 0 && x < w && y < h) pixels[y * w + x] = color;
        }
    }

    // Design doc: "Main menu: Continue, New Game, Settings, Quit."
    public sealed class MenuScreen : MonoBehaviour
    {
        private SettingsPanel settings;
        private Instructions howTo;
        private Button newGame;
        private Button firstButton;
        private bool confirmNew;

        private void Start()
        {
            Scenery.Vista(Scenery.VistaKind.Menu);
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
            Transform c = UiKit.Canvas("Main Menu");
            UiKit.Overlay(c, UiKit.Fade(UiKit.Night, 0.25f));
            UiKit.Vignette(c, 0.95f);
            Vector2 centre = new Vector2(0.5f, 0.5f);
            UiKit.Text(c, "Hushwater", 120, centre, new Vector2(0f, 370f), TextAnchor.MiddleCenter, new Vector2(1200f, 150f), UiKit.Paper);
            UiKit.Text(c, "The water took the town. It did not take him.", 24, centre, new Vector2(0f, 285f), TextAnchor.MiddleCenter, new Vector2(1200f, 40f), UiKit.Fade(UiKit.Lantern, 0.85f));

            // Left: the menu.
            float x = -560f, y = 140f;
            if (Campaign.HasSave)
            {
                firstButton = UiKit.Button(c, "Continue", new Vector2(x, y), new Vector2(420f, 60f), Continue);
                y -= 76f;
            }
            newGame = UiKit.Button(c, "New Game", new Vector2(x, y), new Vector2(420f, 60f), NewGame);
            if (firstButton == null) firstButton = newGame;
            UiKit.Button(c, "How to Play", new Vector2(x, y - 76f), new Vector2(420f, 60f), () => howTo.Show(() => UiKit.Select(firstButton)));
            UiKit.Button(c, "Settings", new Vector2(x, y - 152f), new Vector2(420f, 60f), () => settings.Show(true));
            UiKit.Button(c, "Quit", new Vector2(x, y - 228f), new Vector2(420f, 60f), NightDirector.Quit);
            UiKit.Text(c, Campaign.HasSave ? "" : "New here? Start a New Game: it begins with a short guide.", 17, centre, new Vector2(x, y - 300f),
                TextAnchor.MiddleCenter, new Vector2(460f, 50f), UiKit.Fade(UiKit.Paper, 0.7f));

            // Right: the two things that keep Nell alive.
            Hero(c, new Vector2(150f, -20f), Icons.Kind.Lantern, "YOUR LANTERN",
                "Your light in the drowned town. It starts fully wound and dims slowly: hold F to crank it. Light lantern posts to mark where you'll wake if he catches you.");
            Hero(c, new Vector2(590f, -20f), Icons.Kind.Boat, "YOUR BOAT",
                "The only safe place. Every night you row in from here and come back with the evidence to row out (hold E). Save, develop photos, play tapes, buy upgrades.");

            UiKit.Text(c, "Wear headphones. Every sound you make has a radius, and he hears all of them.", 18, new Vector2(0.5f, 0f),
                new Vector2(0f, 50f), TextAnchor.MiddleCenter, new Vector2(1200f, 30f), UiKit.Fade(UiKit.Paper, 0.6f));
            settings = new SettingsPanel(c, () => UiKit.Select(firstButton));
            howTo = new Instructions(c);
            UiKit.Select(firstButton);
        }

        // A tall card: a big glowing icon, a heading and a few lines.
        private static void Hero(Transform c, Vector2 at, Icons.Kind icon, string title, string body)
        {
            Image panel = UiKit.Panel(c, new Vector2(0.5f, 0.5f), at, new Vector2(400f, 470f));
            Transform p = panel.transform;
            // Top-anchored, so each position is the element's top edge.
            Image glow = UiKit.Glow(p, UiKit.Fade(UiKit.Lantern, 0.3f), new Vector2(0.5f, 1f), new Vector2(0f, -15f), 230f);
            glow.gameObject.AddComponent<GlowPulse>();
            UiKit.Icon(p, icon, UiKit.Lantern, new Vector2(0.5f, 1f), new Vector2(0f, -45f), 170f);
            UiKit.Text(p, title, 30, new Vector2(0.5f, 1f), new Vector2(0f, -258f), TextAnchor.MiddleCenter, new Vector2(360f, 44f), UiKit.Lantern);
            UiKit.Text(p, body, 19, new Vector2(0.5f, 1f), new Vector2(0f, -312f), TextAnchor.UpperCenter, new Vector2(360f, 150f), UiKit.Paper).lineSpacing = 1.12f;
        }

        private void Update() => howTo?.Update();

        private void Continue()
        {
            if (!Campaign.Load()) return;
            Campaign.BoatMessage = $"Back in the boat at the water's edge. Night {Campaign.Night}: {Story.NightDistricts[Campaign.Night]}.";
            Campaign.Next = Campaign.Mode.Boat;
            NightDirector.Load();
        }

        private void NewGame()
        {
            if (Campaign.HasSave && !confirmNew)
            {
                confirmNew = true;
                UiKit.Label(newGame, "New Game: overwrite your save?");
                return;
            }
            Campaign.NewGame();
            // A new player sees the guide first; "Row out to Hushwater" on the last page goes to the boat.
            howTo.Show(() =>
            {
                Campaign.BoatMessage =
                    "1953: the Colter Valley Power Authority flooded Hushwater for a reservoir. The record says everyone was evacuated.\n" +
                    "Now a drought has drained the lake for the first time in 34 years, and your grandfather's tapes say the record is a lie.\n" +
                    "Five nights before the rains refill it. Prove 212 people were drowned in their homes, and get the proof out.\n" +
                    "Something down there still rings the chapel bell. It cannot see you. It can hear everything. Walk softly.";
                Campaign.Next = Campaign.Mode.Boat;
                NightDirector.Load();
            }, "Row out to Hushwater");
        }
    }

    // Design doc: "The Boat. Your only safe place. Save here, develop photos, play Emil's tapes and buy upgrades between nights."
    public sealed class BoatScreen : MonoBehaviour
    {
        private static readonly (Upgrade upgrade, string name, string effect)[] Upgrades =
        {
            (Upgrade.Boots, "Felt-lined boots", "Walking noise 6 m → 4 m"),
            (Upgrade.Crank, "Geared lantern crank", "Cranking noise 12 m → 7 m"),
            (Upgrade.Shutter, "Quiet shutter", "Flash whine 2 s → 1 s"),
            (Upgrade.Lungs, "Diver's lungs", "Breath 30 s → 40 s"),
            (Upgrade.ToolSlot, "Extra tool slot", "3 slots → 4"),
            (Upgrade.Ladder, "Rope ladder", "A permanent escape he can't climb"),
        };

        private readonly List<(Button button, Upgrade upgrade)> shop = new List<(Button, Upgrade)>();
        private Text salvage;
        private Text shopNote;
        private SettingsPanel settings;
        private JournalView journal;
        private Button rowIn;
        private Instructions howTo;

        private void Start()
        {
            Scenery.Vista(Scenery.VistaKind.Boat);
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
            Campaign.Save();
            List<EvidenceSpot> newTapes = Campaign.PlayNewTapes();
            Ambience(newTapes.Count > 0);

            Transform c = UiKit.Canvas("Boat");
            UiKit.Overlay(c, UiKit.Fade(UiKit.Night, 0.3f));
            UiKit.Vignette(c, 0.9f);
            UiKit.Panel(c, new Vector2(0.5f, 0.5f), new Vector2(-480f, -105f), new Vector2(880f, 730f));
            UiKit.Panel(c, new Vector2(0.5f, 0.5f), new Vector2(375f, -105f), new Vector2(760f, 730f));
            Vector2 top = new Vector2(0.5f, 1f);
            Vector2 centre = new Vector2(0.5f, 0.5f);
            UiKit.Text(c, "THE BOAT", 48, top, new Vector2(0f, -40f), TextAnchor.UpperCenter, new Vector2(1200f, 60f), UiKit.Paper);
            UiKit.Text(c, $"Night {Campaign.Night} of 5  ·  {Story.NightDistricts[Campaign.Night]}  ·  {GameSettings.Name(GameSettings.Selected)}  ·  saved",
                20, top, new Vector2(0f, -104f), TextAnchor.UpperCenter, new Vector2(1400f, 30f), UiKit.Fade(UiKit.Paper, 0.7f));
            UiKit.Text(c, Campaign.BoatMessage, 20, top, new Vector2(0f, -146f), TextAnchor.UpperCenter, new Vector2(1500f, 110f), UiKit.Lantern);

            // Left: what she has, what she developed, what Emil said.
            var left = new StringBuilder();
            left.Append($"<b>EVIDENCE</b>\nPhotographs  {Campaign.Count(EvidenceType.Photo)} / 6\nRegister pages  {Campaign.Count(EvidenceType.Page)} / {Story.TotalPages}\n" +
                        $"Emil's tapes  {Campaign.Count(EvidenceType.Tape)} / {Story.TotalTapes}\n\n");
            int n = Campaign.Night;
            if (n < 5)
                left.Append($"<b>NIGHT {n} NEEDS</b>\n{Need(n, EvidenceType.Photo, Story.PhotosNeeded(n), "photographs")}" +
                            $"{Need(n, EvidenceType.Page, Story.PagesNeeded(n), "register pages")}{Need(n, EvidenceType.Tape, Story.TapesNeeded(n), "tapes")}\n");
            else left.Append("<b>NIGHT 5</b>\nGet to the radio room in the dam and transmit. Hold out 90 s.\n\n");
            var developed = new StringBuilder();
            foreach (string id in Campaign.LastBanked)
            {
                EvidenceSpot spot = Story.Find(id);
                if (spot != null && spot.Type == EvidenceType.Photo) developed.Append($"· {spot.Title}: {spot.Text}\n");
            }
            if (developed.Length > 0) left.Append("<b>DEVELOPED TONIGHT</b>\n").Append(developed).Append('\n');
            foreach (EvidenceSpot tape in newTapes) left.Append($"<b>{tape.Title.ToUpperInvariant()}</b> <i>(hiss, then Emil's voice)</i>\n\"{tape.Text}\"\n\n");
            // The typewriter face has no bold: headings are picked out in lantern amber instead.
            string leftBody = left.ToString().Replace("<b>", $"<color=#{UiKit.Hex(UiKit.Lantern)}>").Replace("</b>", "</color>");
            Text leftText = UiKit.Text(c, leftBody, 20, centre, new Vector2(-480f, -110f), TextAnchor.UpperLeft, new Vector2(820f, 690f), UiKit.Paper);
            leftText.supportRichText = true;

            // Middle-right: the upgrades bought with salvage.
            UiKit.Text(c, "UPGRADES", 22, centre, new Vector2(330f, 230f), TextAnchor.MiddleLeft, new Vector2(400f, 30f), UiKit.Lantern);
            salvage = UiKit.Text(c, "", 18, centre, new Vector2(330f, 200f), TextAnchor.MiddleLeft, new Vector2(500f, 26f), UiKit.Paper);
            for (int i = 0; i < Upgrades.Length; i++)
            {
                Upgrade u = Upgrades[i].upgrade;
                Button b = UiKit.Button(c, "", new Vector2(390f, 150f - i * 58f), new Vector2(620f, 50f), () => Buy(u));
                b.GetComponentInChildren<Text>().fontSize = 18;
                shop.Add((b, u));
            }
            shopNote = UiKit.Text(c, "", 16, centre, new Vector2(390f, -220f), TextAnchor.UpperCenter, new Vector2(620f, 40f), UiKit.Fade(UiKit.Paper, 0.7f));

            // Bottom: rowing in, journal, settings, menu.
            Vector2 bottom = new Vector2(0.5f, 0f);
            string rowLabel = n == 5 ? "Row in: Night 5, the Dam Works" : $"Row in: Night {n}";
            rowIn = UiKit.Button(c, rowLabel, new Vector2(390f, -300f), new Vector2(620f, 64f), RowIn);
            UiKit.Button(c, "Journal & Map", new Vector2(160f, -380f), new Vector2(300f, 50f), () => journal.Show(true));
            UiKit.Button(c, "Settings", new Vector2(470f, -380f), new Vector2(240f, 50f), () => settings.Show(true));
            UiKit.Button(c, "Main Menu", new Vector2(160f, -440f), new Vector2(300f, 50f), () => { Campaign.Next = Campaign.Mode.Menu; NightDirector.Load(); });
            UiKit.Button(c, "Quit", new Vector2(470f, -440f), new Vector2(240f, 50f), NightDirector.Quit);
            UiKit.Button(c, "How to Play", new Vector2(-480f, -430f), new Vector2(300f, 50f), () => howTo.Show(() => UiKit.Select(rowIn)));

            settings = new SettingsPanel(c, () => UiKit.Select(rowIn));
            howTo = new Instructions(c);
            journal = new JournalView(c, () => { journal.Show(false); UiKit.Select(rowIn); });
            RefreshShop();
            UiKit.Select(rowIn);
        }

        private void Update()
        {
            if (howTo.Visible)
            {
                howTo.Update();
                return;
            }
            if (GameInput.Down(KeyCode.Tab) || (journal.Visible && GameInput.Down(KeyCode.Escape)))
            {
                journal.Show(!journal.Visible);
                if (!journal.Visible) UiKit.Select(rowIn);
            }
        }

        private static string Need(int night, EvidenceType type, int needed, string what) =>
            needed <= 0 ? "" : $"{what}  {Mathf.Min(needed, Campaign.CountForNight(night, type))} / {needed}\n";

        private void Buy(Upgrade upgrade)
        {
            shopNote.text = Campaign.Buy(upgrade) ? "Done. It's yours for the rest of the run." : "Not enough salvage. Search crates and the streets.";
            RefreshShop();
        }

        private void RefreshShop()
        {
            salvage.text = $"Brass fittings {Campaign.Brass}   ·   Copper wire {Campaign.Wire}   ·   Lens glass {Campaign.Lens}";
            foreach ((Button button, Upgrade upgrade) in shop)
            {
                var info = Array.Find(Upgrades, x => x.upgrade == upgrade);
                Campaign.Cost(upgrade, out int brass, out int wire, out int lens);
                var cost = new List<string>();
                if (brass > 0) cost.Add($"{brass} brass");
                if (wire > 0) cost.Add($"{wire} wire");
                if (lens > 0) cost.Add($"{lens} lens");
                bool owned = upgrade == Upgrade.Ladder ? Campaign.Ladders >= TownLayout.LadderSpots.Length : Campaign.Owns(upgrade);
                string state = owned ? "OWNED" : Campaign.CanBuy(upgrade) ? "BUY" : string.Join(", ", cost);
                string extra = upgrade == Upgrade.Ladder ? $" ({Campaign.Ladders}/{TownLayout.LadderSpots.Length} hung)" : "";
                UiKit.Label(button, $"{info.name}{extra}: {info.effect}    [{state}]");
                button.interactable = !owned;
            }
        }

        private void RowIn()
        {
            Campaign.Next = Campaign.Mode.Night;
            NightDirector.Load();
        }

        // Water lapping at the hull and, if there's a new tape, its hiss.
        private void Ambience(bool tape)
        {
            AudioSource wind = gameObject.AddComponent<AudioSource>();
            wind.clip = ProceduralSounds.Wind;
            wind.loop = true;
            wind.volume = 0.35f;
            wind.Play();
            if (!tape) return;
            AudioSource hiss = gameObject.AddComponent<AudioSource>();
            hiss.clip = ProceduralSounds.TapeHiss;
            hiss.loop = true;
            hiss.volume = 0.6f;
            hiss.Play();
        }
    }

    // Design doc, "Four Endings": The Record, The Roll Call, Brought In, Silence.
    public sealed class EndingScreen : MonoBehaviour
    {
        private void Start()
        {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
            Story.Ending ending = Campaign.Ending == Story.Ending.None ? Story.Ending.Record : Campaign.Ending;
            // The rains came: the town is under again. Otherwise it's the morning after the broadcast.
            Scenery.Vista(ending == Story.Ending.Silence ? Scenery.VistaKind.Drowned : Scenery.VistaKind.Dawn);
            Transform c = UiKit.Canvas("Ending");
            UiKit.Overlay(c, UiKit.Fade(UiKit.Night, 0.45f));
            UiKit.Vignette(c, 0.9f);
            UiKit.Panel(c, new Vector2(0.5f, 0.5f), new Vector2(0f, -150f), new Vector2(1380f, 480f));
            Vector2 centre = new Vector2(0.5f, 0.5f);
            UiKit.Text(c, Story.EndingTitle(ending), 72, centre, new Vector2(0f, 340f), TextAnchor.MiddleCenter, new Vector2(1400f, 100f), UiKit.Paper);
            UiKit.Text(c, Story.EndingText(ending, Campaign.SpillwayOpened), 26, centre, new Vector2(0f, 220f), TextAnchor.MiddleCenter, new Vector2(1400f, 120f), UiKit.Lantern);

            var names = new StringBuilder();
            if (ending == Story.Ending.RollCall || ending == Story.Ending.BroughtIn)
                foreach (EvidenceSpot page in Campaign.BankedOfType(EvidenceType.Page))
                    names.Append(page.Text.Replace("Marked STAYED. ", "")).Append('\n');
            else if (ending != Story.Ending.Silence)
                names.Append("The register pages you sent went out over the air.\nFind all 18 pages and all 6 of Emil's tapes and every name can be read.");
            else names.Append("Nothing reached the radio. The lake comes back up over the chapel roof.");
            UiKit.Text(c, names.ToString(), 17, centre, new Vector2(0f, -80f), TextAnchor.UpperCenter, new Vector2(1300f, 440f), UiKit.Fade(UiKit.Paper, 0.85f));
            UiKit.Text(c, $"Register pages {Campaign.Count(EvidenceType.Page)} / {Story.TotalPages}   ·   Emil's tapes {Campaign.Count(EvidenceType.Tape)} / {Story.TotalTapes}" +
                          $"   ·   Spillway {(Campaign.SpillwayOpened ? "opened" : "shut")}   ·   {GameSettings.Name(GameSettings.Active)}",
                18, centre, new Vector2(0f, -360f), TextAnchor.MiddleCenter, new Vector2(1400f, 30f), UiKit.Paper);
            Button menu = UiKit.Button(c, "Main Menu", new Vector2(-150f, -440f), new Vector2(260f, 56f), () => { Campaign.Next = Campaign.Mode.Menu; NightDirector.Load(); });
            UiKit.Button(c, "Quit", new Vector2(150f, -440f), new Vector2(260f, 56f), NightDirector.Quit);
            UiKit.Select(menu);
            AudioSource hymn = gameObject.AddComponent<AudioSource>();
            hymn.clip = ending == Story.Ending.Silence ? ProceduralSounds.Wind : ProceduralSounds.Organ;
            hymn.loop = true;
            hymn.volume = 0.4f;
            hymn.Play();
        }
    }
}
