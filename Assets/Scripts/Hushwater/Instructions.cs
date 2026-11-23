using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Hushwater
{
    // "How to Play": five illustrated pages, shown before a new game and from the menu, boat and pause menu.
    // The story, the steps of a night, what to look for, how to stay alive, and the controls.
    public sealed class Instructions
    {
        private readonly GameObject root;
        private readonly List<GameObject> pages = new List<GameObject>();
        private readonly List<Image> dots = new List<Image>();
        private readonly Button back, next;
        private readonly Text pageTitle;
        private string finishLabel;
        private Action done;
        private int page;

        private static readonly string[] Titles = { "The story", "Each night, step by step", "What to look for", "Staying alive", "Controls" };

        public bool Visible => root.activeSelf;

        public Instructions(Transform parent)
        {
            finishLabel = "Done";
            Image backdrop = UiKit.Overlay(parent, new Color(0.03f, 0.04f, 0.045f, 0.97f));
            backdrop.raycastTarget = true;
            root = backdrop.gameObject;
            UiKit.Vignette(root.transform, 0.8f);
            Transform r = root.transform;
            Vector2 centre = new Vector2(0.5f, 0.5f);
            UiKit.Text(r, "HOW TO PLAY", 22, centre, new Vector2(0f, 480f), TextAnchor.MiddleCenter, new Vector2(800f, 30f), UiKit.Fade(UiKit.Lantern, 0.8f));
            pageTitle = UiKit.Text(r, "", 54, centre, new Vector2(0f, 425f), TextAnchor.MiddleCenter, new Vector2(1400f, 70f), UiKit.Paper);

            pages.Add(StoryPage(r));
            pages.Add(StepsPage(r));
            pages.Add(LookForPage(r));
            pages.Add(SurvivePage(r));
            pages.Add(ControlsPage(r));

            for (int i = 0; i < pages.Count; i++)
                dots.Add(UiKit.Box(r, Color.white, centre, new Vector2((i - (pages.Count - 1) * 0.5f) * 28f, -430f), new Vector2(14f, 14f)));
            back = UiKit.Button(r, "Back", new Vector2(-330f, -480f), new Vector2(240f, 56f), () => Go(page - 1));
            next = UiKit.Button(r, "Next", new Vector2(330f, -480f), new Vector2(240f, 56f), () => { if (page < pages.Count - 1) Go(page + 1); else Close(); });
            UiKit.Button(r, "Skip", new Vector2(0f, -480f), new Vector2(160f, 46f), Close).GetComponentInChildren<Text>().fontSize = 18;
            root.SetActive(false);
        }

        public void Show(Action onDone, string finish = "Done")
        {
            done = onDone;
            finishLabel = finish;
            root.SetActive(true);
            root.transform.SetAsLastSibling();
            Go(0);
        }

        // Closes without running the callback (e.g. the game was unpaused underneath it).
        public void Hide() => root.SetActive(false);

        private void Close()
        {
            root.SetActive(false);
            done?.Invoke();
        }

        private void Go(int index)
        {
            page = Mathf.Clamp(index, 0, pages.Count - 1);
            for (int i = 0; i < pages.Count; i++)
            {
                pages[i].SetActive(i == page);
                dots[i].color = i == page ? UiKit.Lantern : UiKit.Fade(UiKit.Paper, 0.3f);
            }
            pageTitle.text = Titles[page];
            back.interactable = page > 0;
            UiKit.Label(next, page == pages.Count - 1 ? finishLabel : "Next");
            UiKit.Select(next);
        }

        // Arrow keys / bumpers flip pages; Esc closes.
        public void Update()
        {
            if (!Visible) return;
            if (Input.GetKeyDown(KeyCode.RightArrow) || Input.GetKeyDown(KeyCode.JoystickButton5)) Go(page + 1);
            if (Input.GetKeyDown(KeyCode.LeftArrow) || Input.GetKeyDown(KeyCode.JoystickButton4)) Go(page - 1);
            if (Input.GetKeyDown(KeyCode.Escape)) Close();
        }

        // ---------- Pages ----------

        private static GameObject Page(Transform parent)
        {
            var page = new GameObject("Page", typeof(RectTransform));
            page.transform.SetParent(parent, false);
            UiKit.Stretch((RectTransform)page.transform);
            return page;
        }

        // An illustrated card: a big icon, a title and a few lines.
        private static void Card(Transform parent, Vector2 at, Vector2 size, Icons.Kind icon, Color tint, string title, string body, string badge = null)
        {
            Vector2 centre = new Vector2(0.5f, 0.5f);
            Image panel = UiKit.Panel(parent, centre, at, size);
            Transform c = panel.transform;
            // (Top-anchored children are placed by their top edge.)
            float iconSize = Mathf.Min(110f, size.y * 0.3f);
            UiKit.Box(c, UiKit.Fade(tint, 0.12f), new Vector2(0.5f, 1f), new Vector2(0f, -18f), new Vector2(iconSize + 26f, iconSize + 26f));
            UiKit.Icon(c, icon, tint, new Vector2(0.5f, 1f), new Vector2(0f, -31f), iconSize);
            if (badge != null)
            {
                Image disc = UiKit.Box(c, UiKit.Lantern, new Vector2(0f, 1f), new Vector2(30f, -30f), new Vector2(40f, 40f));
                Text num = UiKit.Text(disc.transform, badge, 24, centre, Vector2.zero, TextAnchor.MiddleCenter, new Vector2(40f, 40f), UiKit.Night);
                num.GetComponent<Shadow>().enabled = false;
            }
            float titleTop = -(iconSize + 26f + 18f + 14f);
            UiKit.Text(c, title, 24, new Vector2(0.5f, 1f), new Vector2(0f, titleTop), TextAnchor.MiddleCenter, new Vector2(size.x - 24f, 36f), UiKit.Lantern);
            float bodyTop = titleTop - 46f;
            Text text = UiKit.Text(c, body, 18, new Vector2(0.5f, 1f), new Vector2(0f, bodyTop),
                TextAnchor.UpperCenter, new Vector2(size.x - 36f, size.y + bodyTop - 14f), UiKit.Paper);
            text.lineSpacing = 1.1f;
        }

        private static GameObject StoryPage(Transform parent)
        {
            GameObject page = Page(parent);
            Transform p = page.transform;
            Vector2 centre = new Vector2(0.5f, 0.5f);
            UiKit.Panel(p, centre, new Vector2(-170f, -10f), new Vector2(1080f, 700f));
            UiKit.Text(p,
                "<color=#F0C36A>1953.</color> The Colter Valley Power Authority flooded the town of Hushwater to make a reservoir. " +
                "The official record says everyone was evacuated.\n\n" +
                "<color=#F0C36A>Now.</color> A drought has drained the lake for the first time in 34 years. You are <color=#F0C36A>Nell Varga</color>, " +
                "a surveyor. Your grandfather Emil was the town's last radio operator, and his tapes say the record is a lie: " +
                "<color=#F0C36A>212 people were drowned in their homes</color>.\n\n" +
                "You have <color=#F0C36A>five nights</color> before the rains refill the valley. Row in each night from your boat, " +
                "gather proof (photographs, pages of the parish register, Emil's hidden tapes) and on the last night send it all " +
                "out over the radio in the dam.\n\n" +
                "Something still walks the drowned streets: <color=#E07A5F>the Sexton</color>, the old bell-ringer. " +
                "He is blind. <color=#E07A5F>He hears everything.</color>",
                21, centre, new Vector2(-170f, -10f), TextAnchor.MiddleLeft, new Vector2(980f, 640f), UiKit.Paper).supportRichText = true;
            Card(p, new Vector2(590f, 175f), new Vector2(340f, 330f), Icons.Kind.Boat, UiKit.Lantern, "Your boat", "Your only safe place. Every night starts and ends here.");
            Card(p, new Vector2(590f, -180f), new Vector2(340f, 330f), Icons.Kind.Sexton, new Color(0.9f, 0.4f, 0.3f), "The Sexton", "Blind, 2.4 m tall, armed with a boat-hook. Never let him hear you.");
            return page;
        }

        private static GameObject StepsPage(Transform parent)
        {
            GameObject page = Page(parent);
            Transform p = page.transform;
            Vector2 size = new Vector2(330f, 520f);
            var steps = new (Icons.Kind, string, string)[]
            {
                (Icons.Kind.Boat, "Row in", "Choose upgrades at the boat, then press Row in. You start at the water's edge."),
                (Icons.Kind.Lantern, "Light your way", "Your lantern starts fully wound. Hold F to crank it when it dims. Light lantern posts as checkpoints."),
                (Icons.Kind.Camera, "Find evidence", "Evidence glows in a golden column of light. A gold marker on screen leads you to the nearest piece."),
                (Icons.Kind.Sound, "Stay unheard", "Walk softly, crouch, hide. A red warning tells you when he's close and which way."),
                (Icons.Kind.Boat, "Row out", "With tonight's evidence, return to the boat and hold E. Then upgrade, save and go again."),
            };
            for (int i = 0; i < steps.Length; i++)
                Card(p, new Vector2((i - 2) * 350f, 20f), size, steps[i].Item1, i == 3 ? new Color(0.9f, 0.45f, 0.35f) : UiKit.Lantern, steps[i].Item2, steps[i].Item3, (i + 1).ToString());
            UiKit.Text(p, "Night 5 is different: reach the radio room in the dam, hold E at the radio set, and survive 90 seconds while it transmits.",
                19, new Vector2(0.5f, 0.5f), new Vector2(0f, -330f), TextAnchor.MiddleCenter, new Vector2(1500f, 60f), UiKit.Lantern);
            return page;
        }

        private static GameObject LookForPage(Transform parent)
        {
            GameObject page = Page(parent);
            Transform p = page.transform;
            Color gold = UiKit.Lantern, pale = new Color(0.75f, 0.85f, 0.82f);
            var items = new (Icons.Kind, Color, string, string)[]
            {
                (Icons.Kind.Camera, gold, "Photo scenes", "A golden column of light with a camera icon. Walk up to it and press E to photograph it."),
                (Icons.Kind.Page, gold, "Register pages", "Torn pages naming the families who stayed. Glinting paper; walk up and press E. Some are in locked cabinets."),
                (Icons.Kind.Tape, gold, "Emil's tapes", "Reel tapes he hid around town. Press E to take; they play at the boat."),
                (Icons.Kind.Note, pale, "Notes & letters", "Hold E to read. They tell what happened; they don't count as evidence."),
                (Icons.Kind.LanternPost, gold, "Lantern posts", "Hold F to light one. If he catches you, you wake at the last one you lit."),
                (Icons.Kind.Wardrobe, pale, "Hiding spots", "Wardrobes, pews, under the jetty. Press E. Don't use the same one too often: he learns."),
                (Icons.Kind.Bottle, pale, "Tools", "Bottles and handbells to throw (Q) as lures, smelling salts, boot wraps, lamp oil."),
                (Icons.Kind.Salvage, pale, "Salvage", "Brass, copper wire and lens glass in crates. Spend it on upgrades at the boat."),
            };
            for (int i = 0; i < items.Length; i++)
            {
                int col = i % 4, row = i / 4;
                Card(p, new Vector2((col - 1.5f) * 420f, 165f - row * 400f), new Vector2(400f, 380f), items[i].Item1, items[i].Item2, items[i].Item3, items[i].Item4);
            }
            return page;
        }

        private static GameObject SurvivePage(Transform parent)
        {
            GameObject page = Page(parent);
            Transform p = page.transform;
            Color red = new Color(0.9f, 0.4f, 0.3f), gold = UiKit.Lantern;
            var items = new (Icons.Kind, Color, string, string)[]
            {
                (Icons.Kind.Sound, red, "Every sound carries", "Creep 2 m · walk 6 m · mud 10 m · crank 12 m · sprint 18 m · flash 20 m · door 25 m · gasp 30 m. Two sounds close together and he investigates; three and he hunts."),
                (Icons.Kind.Danger, red, "Heed the warning", "Within about 24 m a red banner shows how far and which way, and your heartbeat races. The minimap (top right) shows him as a red dot."),
                (Icons.Kind.Crouch, gold, "Crouch & creep", "Ctrl or C. Slow, but almost silent. Avoid deep mud and loose boards."),
                (Icons.Kind.Wardrobe, gold, "Hide", "Get into a wardrobe or under a pew and wait. If he searches it, hold your breath."),
                (Icons.Kind.Breath, gold, "Hold your breath", "Hold Space when he's right beside you. Your real microphone counts too: talking or breathing hard is noise he hears (see the MIC bar)."),
                (Icons.Kind.Eye, red, "Other dangers", "The Congregation wails if lit too long. Silt Hands grab in deep mud (mash Space). Moths eat your light. Eels in flooded rooms."),
            };
            for (int i = 0; i < items.Length; i++)
            {
                int col = i % 3, row = i / 3;
                Card(p, new Vector2((col - 1f) * 520f, 165f - row * 400f), new Vector2(500f, 380f), items[i].Item1, items[i].Item2, items[i].Item3, items[i].Item4);
            }
            return page;
        }

        private static GameObject ControlsPage(Transform parent)
        {
            GameObject page = Page(parent);
            Transform p = page.transform;
            Vector2 centre = new Vector2(0.5f, 0.5f);
            UiKit.Panel(p, centre, new Vector2(0f, -10f), new Vector2(1500f, 720f));
            string[,] rows =
            {
                { "Move / look", "W A S D + mouse", "Left stick / right stick" },
                { "Crouch (quiet)", "Ctrl or C", "B" },
                { "Sprint (loud)", "Shift", "L3" },
                { "Hold breath", "Space", "RB" },
                { "Take evidence (photos too), use, hide, read, row out", "E (hold)", "A" },
                { "Crank lantern / light a post", "F (hold)", "Y" },
                { "Lantern up / down", "L", "R3" },
                { "Raise camera / take photo", "Right mouse / left mouse", "LT / RT" },
                { "Flash on / off", "R", "X" },
                { "Choose tool / throw or use it", "1-4 or wheel / Q", "D-pad / LB" },
                { "Journal & map", "Tab", "View" },
                { "Show / hide the guide", "H", "" },
                { "Pause, settings, controls", "Esc", "Menu" },
            };
            UiKit.Text(p, "ACTION", 18, centre, new Vector2(-450f, 300f), TextAnchor.MiddleLeft, new Vector2(500f, 30f), UiKit.Lantern);
            UiKit.Text(p, "KEYBOARD & MOUSE", 18, centre, new Vector2(150f, 300f), TextAnchor.MiddleLeft, new Vector2(400f, 30f), UiKit.Lantern);
            UiKit.Text(p, "GAMEPAD", 18, centre, new Vector2(560f, 300f), TextAnchor.MiddleLeft, new Vector2(300f, 30f), UiKit.Lantern);
            for (int i = 0; i < rows.GetLength(0); i++)
            {
                float y = 255f - i * 44f;
                if (i % 2 == 0) UiKit.Box(p, new Color(1f, 1f, 1f, 0.035f), centre, new Vector2(0f, y), new Vector2(1440f, 42f));
                UiKit.Text(p, rows[i, 0], 19, centre, new Vector2(-450f, y), TextAnchor.MiddleLeft, new Vector2(500f, 40f), UiKit.Paper);
                UiKit.Text(p, rows[i, 1], 19, centre, new Vector2(150f, y), TextAnchor.MiddleLeft, new Vector2(400f, 40f), UiKit.Paper);
                UiKit.Text(p, rows[i, 2], 19, centre, new Vector2(560f, y), TextAnchor.MiddleLeft, new Vector2(300f, 40f), UiKit.Fade(UiKit.Paper, 0.75f));
            }
            return page;
        }
    }
}
