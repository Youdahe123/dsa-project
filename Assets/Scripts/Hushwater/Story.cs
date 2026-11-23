using System.Collections.Generic;
using UnityEngine;

namespace Hushwater
{
    // The three kinds of evidence in the design doc's "Evidence Kit": photographs, parish register pages and Emil's tapes.
    public enum EvidenceType { Photo, Page, Tape }

    // One piece of evidence placed in the town: which night it appears on, where, and what it says.
    public sealed class EvidenceSpot
    {
        public string Id;
        public EvidenceType Type;
        public int Night;
        public Vector3 Position;
        // Photos only: the room is dark, so a shot without the flash may come out too blurry to count.
        public bool Dark;
        // Pages only: locked in a warped cabinet that has to be forced open (25 m, like a door).
        public bool InCabinet;
        // Photos: what the picture shows. Pages: the household listed. Tapes: the transcript.
        public string Text;
        // A short title for the journal.
        public string Title;
    }

    // All the written content: evidence placement and text, Emil's tapes, and the four endings.
    // Placement numbers are ours (the doc gives the per-night evidence targets, not positions).
    public static class Story
    {
        // Design doc, "The Five Nights": what each night opens and what evidence it needs.
        public static readonly string[] NightDistricts =
        {
            "", "Millrow", "Millrow, Chapel Hill", "Millrow, Chapel Hill, The Dam Works", "Chapel Hill, The Dam Works", "The Dam Works"
        };

        public static readonly string[] NightPressure =
        {
            "",
            "Learn to move quietly. He is heard, never seen.",
            "He patrols. Your first chase.",
            "Lower Millrow floods. Lantern Moths appear.",
            "His routes follow the evidence you took.",
            "He hunts nonstop. The bell tolls every 60 s."
        };

        // The evidence target for each night: photos, pages and tapes that must be in hand to open the next night.
        public static int PhotosNeeded(int night) => night == 1 ? 3 : night == 2 ? 2 : night == 4 ? 1 : 0;
        public static int PagesNeeded(int night) => night == 2 ? 2 : night == 3 ? 3 : 0;
        public static int TapesNeeded(int night) => night == 3 ? 1 : 0;

        // Design doc: "18 register pages to find" and "6 of Emil's tapes". The endings need 15+ pages and all 6 tapes.
        public const int TotalPages = 18;
        public const int TotalTapes = 6;
        public const int RollCallPages = 15;

        // Declared before Evidence: static fields initialise in order, and Evidence reads these.
        // Households from the parish register. Each page lists one street's families who were marked as staying.
        private static readonly string[] Households =
        {
            "Mill Lane: the Holloways, the Petrovs, the Dunnes. 13 souls.",
            "Mill Lane: the Abernathys, the Okafors, the widow Lisle. 11 souls.",
            "Weir Street: the Kowalskis, the Bramleys. 12 souls.",
            "Weir Street: the Sandovals, the Teagues, the Marsh children. 14 souls.",
            "Chapel Row: the Fennimores, the Hartes. 10 souls.",
            "Chapel Row: the Lindqvists, the Moores, Mr. Ochs the cobbler. 12 souls.",
            "School House Lane: the Baptistes, the Carvers. 11 souls.",
            "School House Lane: Miss Ida Penn and her boarders. 9 souls.",
            "Lock Road: the Varga cousins, the Reillys. 13 souls.",
            "Lock Road: the Quinns, the Ashdowns. 12 souls.",
            "Tannery Row: the Nakamuras, the Whitlocks. 14 souls.",
            "Tannery Row: the Greers, the Delacroix. 11 souls.",
            "Spillway Cottages: the Brandts, the Oyelarans. 12 souls.",
            "Spillway Cottages: the Haldanes, old Mrs. Crane. 10 souls.",
            "Upper Millrow: the Sorensens, the Mbekis. 13 souls.",
            "Upper Millrow: the Castellanos, the Pryors. 12 souls.",
            "The Mill dormitory: twenty-two millhands, unnamed. 15 souls listed.",
            "Chapel House: Abel Crane, sexton. 1 soul. The last line on the last page."
        };

        // Design doc: "6 of Emil's tapes, played at the boat." Emil Varga is Nell's late grandfather.
        private static readonly string[] Tapes =
        {
            "Nell. If you've found this, the lake is low again. I'm sorry I never said it to your face. " +
            "I worked for the Power Authority in '53. I was nineteen. They told us the town was empty. It wasn't.",

            "The trucks came on the Monday. Half of them left empty. The mill families wouldn't go without their wages, " +
            "and the Authority wouldn't pay until the gates were shut. So they stayed. And on the Tuesday we were told to open the gates anyway.",

            "Abel Crane kept the chapel. Blind since the war. He knew every soul in town by the sound of their step. " +
            "When the sirens didn't come, he went up the tower to ring the bell himself. To wake them. To get them up the hill.",

            "Somebody nailed the tower door shut from the outside. I won't say it was me. " +
            "I'll say I held the nails for a man in an Authority coat, and I didn't ask why. A bell brings newspapers.",

            "He rang it all night. The water came in under the door and he kept ringing. We heard it from the dam until morning. " +
            "Then nothing. And every dry summer since, I swear I hear it again.",

            "The register is the proof, Nell. Two hundred and twelve names. Read them out. All of them. " +
            "Maybe then he can stop ringing. Maybe then I can stop hearing it."
        };

        public static readonly List<EvidenceSpot> Evidence = new List<EvidenceSpot>
        {
            // ---------- Night 1: Millrow, three photographs ----------
            Photo("photo:table", 1, new Vector3(11f, 0f, 1f), false, "The dinner table",
                "A dinner table set for six. The plates were never cleared."),
            Photo("photo:shoes", 1, new Vector3(-11f, 0f, 15.5f), true, "Shoes by the door",
                "Shoes lined up by the door, children's sizes too. Nobody packed."),
            Photo("photo:bicycle", 1, new Vector3(6f, 0f, 20.5f), false, "The bicycle",
                "A child's bicycle still chained to the porch rail."),
            Tape(1, 1, new Vector3(-10.5f, 0f, -4.5f)),

            // ---------- Night 2: Millrow and Chapel Hill, two photographs and two pages ----------
            Photo("photo:pews", 2, new Vector3(-9f, 0f, 42f), true, "Names in the pews",
                "Names carved into the pews. Dozens of them, all dated the week of the flood."),
            Photo("photo:slate", 2, new Vector3(-13.4f, 0f, 56f), true, "The school slate",
                "The schoolroom slate: \"Tuesday. 31 present.\" Tuesday was the flood."),
            Page(1, 2, new Vector3(-12.6f, 0f, 48.5f), true),
            Page(2, 2, new Vector3(-7f, 0f, 58.5f), false),
            Page(3, 2, new Vector3(12f, 0f, 40f), false),
            Page(4, 2, new Vector3(-11f, 0f, -8f), false),
            Tape(2, 2, new Vector3(11.8f, 0f, 30.5f)),

            // ---------- Night 3: all three districts, three pages and a tape ----------
            Page(5, 3, new Vector3(-12.4f, 0f, 74f), true),
            Page(6, 3, new Vector3(-3.5f, 0f, 90f), false),
            Page(7, 3, new Vector3(-12f, 0f, 53.2f), false),
            Page(8, 3, new Vector3(-7.5f, 0f, 47.5f), false),
            Page(9, 3, new Vector3(-11f, 0f, 12.5f), false),
            Page(10, 3, new Vector3(11f, 0f, 4f), false),
            Tape(3, 3, new Vector3(11f, 0f, 97f)),   // at the end of the flooded culvert
            Tape(4, 3, new Vector3(0f, 0f, 22f)),

            // ---------- Night 4: Chapel Hill and the Dam Works, the bell-tower photo ----------
            Photo("photo:tower", 4, new Vector3(6.6f, 0f, 49f), true, "The tower door",
                "The bell-tower door, from the inside. It was barred from the outside."),
            Page(11, 4, new Vector3(-11.2f, 0f, 31.5f), true),
            Page(12, 4, new Vector3(-10f, 0f, 58.8f), false),
            Page(13, 4, new Vector3(6f, 0f, 29f), false),
            Page(14, 4, new Vector3(-7.5f, 0f, 73.5f), false),
            Page(15, 4, new Vector3(7f, 0f, 70.5f), false),
            Tape(5, 4, new Vector3(9f, 0f, 50.2f)),

            // ---------- Night 5: the Dam Works, transmit ----------
            Page(16, 5, new Vector3(-11.5f, 0f, 72.5f), false),
            Page(17, 5, new Vector3(11.5f, 0f, 65.5f), false),
            Page(18, 5, new Vector3(-9.8f, 0f, 94.6f), true),
            Tape(6, 5, new Vector3(-11.5f, 0f, 68f)),
        };

        // Looks up a piece of evidence by id, or null.
        public static EvidenceSpot Find(string id) => Evidence.Find(e => e.Id == id);

        // Every piece of evidence placed on a night.
        public static IEnumerable<EvidenceSpot> ForNight(int night)
        {
            foreach (EvidenceSpot spot in Evidence)
                if (spot.Night == night) yield return spot;
        }

        private static EvidenceSpot Photo(string id, int night, Vector3 position, bool dark, string title, string text) =>
            new EvidenceSpot { Id = id, Type = EvidenceType.Photo, Night = night, Position = position, Dark = dark, Title = title, Text = text };

        private static EvidenceSpot Page(int number, int night, Vector3 position, bool cabinet) =>
            new EvidenceSpot
            {
                Id = $"page:{number:00}", Type = EvidenceType.Page, Night = night, Position = position, InCabinet = cabinet,
                Title = $"Register page {number}", Text = "Marked STAYED. " + Households[number - 1]
            };

        private static EvidenceSpot Tape(int number, int night, Vector3 position) =>
            new EvidenceSpot
            {
                Id = $"tape:{number}", Type = EvidenceType.Tape, Night = night, Position = position,
                Title = $"Emil's tape {number}", Text = Tapes[number - 1]
            };

        // ---------- Endings (design doc: "Four Endings") ----------

        public enum Ending { None, Record, RollCall, BroughtIn, Silence }

        // Which ending the evidence and the spillway choice earn once the transmission finishes.
        public static Ending EndingFor(int pages, int tapes, bool spillwayOpened)
        {
            bool everything = pages >= RollCallPages && tapes >= TotalTapes;
            if (!everything) return Ending.Record;
            return spillwayOpened ? Ending.RollCall : Ending.BroughtIn;
        }

        public static string EndingTitle(Ending ending)
        {
            switch (ending)
            {
                case Ending.RollCall: return "The Roll Call";
                case Ending.BroughtIn: return "Brought In";
                case Ending.Silence: return "Silence";
                default: return "The Record";
            }
        }

        public static string EndingText(Ending ending, bool spillwayOpened)
        {
            switch (ending)
            {
                case Ending.RollCall:
                    return "Every name is read on the radio, all two hundred and twelve.\nSomewhere below the dam, the bell finally stops.";
                case Ending.BroughtIn:
                    return "Every name goes out over the air. Then Nell climbs the tower and rings the bell herself.\n" +
                           "He lays down his hook and walks into the water with the Congregation.";
                case Ending.Silence:
                    return "The transmission fails. The rains come.\nThe town, and the proof, go back under.";
                default:
                    return spillwayOpened
                        ? "The story airs, and the Authority denies it.\nHe is swept downstream, still ringing."
                        : "The story airs, and the Authority denies it.\nBehind the dam, the bell keeps ringing.";
            }
        }
    }
}
