using System;
using System.Collections.Generic;
using UnityEngine;

namespace Hushwater
{
    // The town's fixed geography. It runs south to north, away from the lake: Millrow (z -16..24),
    // Chapel Hill (24..62), then the Dam Works (62..100) under the dam wall. All numbers here are ours.
    public static class TownLayout
    {
        public const float MillrowStart = -16f, ChapelHillStart = 24f, DamWorksStart = 62f, DamWall = 100f;
        // The main road, the houses either side, then a back lane and a second row of buildings each side.
        public const float HalfWidth = 27f;

        // Rope ladder spots: the foot of the ladder against a wall, and the middle of the roof it leads to.
        public static readonly Vector3[] LadderSpots =
        {
            new Vector3(5.6f, 0f, 4.2f),
            new Vector3(-5.6f, 0f, 28.2f),
            new Vector3(-4.6f, 0f, 67.2f),
            new Vector3(-5.6f, 0f, -8.2f)
        };
        public static readonly Vector3[] LadderRoofs =
        {
            new Vector3(9f, 3.2f, 2f),
            new Vector3(-9f, 3.2f, 30f),
            new Vector3(-9f, 3.2f, 70f),
            new Vector3(-9f, 3.2f, -6f)
        };

        // Design doc: "The lake climbs a metre every night, drowning streets you relied on and forcing new routes."
        // Everything south of the shoreline is under the lake. Night 3: "Lower Millrow floods".
        public static float Shoreline(int night)
        {
            switch (night)
            {
                case 3: return -4f;
                case 4: return ChapelHillStart;
                case 5: return DamWorksStart;
                default: return MillrowStart;
            }
        }

        // The lake's surface, rising every night. Night 1 and 2 it sits below the town.
        public static float LakeLevel(int night) => night <= 2 ? -0.3f + 0.1f * night : 1.6f + 0.2f * night;

        // Night 3's flooded streets can be swum on held breath; later nights the old town is too deep.
        public static bool FloodSwimmable(int night) => night == 3;

        // How far north Nell can go: "OPEN Millrow" / "Millrow, Chapel Hill" / then everything.
        public static float NorthLimit(int night) => night == 1 ? ChapelHillStart : night == 2 ? DamWorksStart : DamWall;

        // The boat waits at the water's edge in the middle of the road.
        public static Vector3 BoatPosition(int night) => new Vector3(0f, 0f, Shoreline(night) + 1.4f);

        // True if a point is in the part of town that is open and above water tonight.
        public static bool Open(int night, Vector3 point)
        {
            if (point.z > NorthLimit(night)) return false;
            if (point.z >= Shoreline(night)) return true;
            return FloodSwimmable(night) && point.z > MillrowStart;
        }

        public static string District(float z) => z < ChapelHillStart ? "Millrow" : z < DamWorksStart ? "Chapel Hill" : "The Dam Works";
    }

    // Boat upgrades from the design doc's "Upgrades at the Boat" table.
    public enum Upgrade { Boots, Crank, Shutter, Lungs, ToolSlot, Ladder }

    // The whole five-night run: which night it is, the evidence banked so far, salvage, upgrades, tools,
    // what the Sexton has learned about Nell's hiding spots, and the map she has drawn.
    // Static so it survives the scene reload between nights; saved to PlayerPrefs at the boat (design doc: "Save here").
    public static class Campaign
    {
        // Which screen the bootstrap should build after the next scene load.
        public enum Mode { Menu, Night, Boat, Ending }

        [Serializable]
        private sealed class SaveData
        {
            public int night = 1;
            public List<string> evidence = new List<string>();
            public List<string> tapesPlayed = new List<string>();
            public int brass;
            public int wire;
            public int lens;
            public List<int> upgrades = new List<int>();
            public int ladders;
            public List<int> toolKinds = new List<int>();
            public List<int> toolCounts = new List<int>();
            public int flashBulbs;
            public List<string> brokenDoors = new List<string>();
            public List<string> hideIds = new List<string>();
            public List<int> hideCounts = new List<int>();
            public List<Vector3> guardedSites = new List<Vector3>();
            public string map = "";
            public int ending;
            public bool spillwayOpened;
        }

        // The doc's town runs south to north: Millrow, Chapel Hill, then the Dam Works. 2 m map cells.
        public const float MapMinX = -TownLayout.HalfWidth, MapMaxX = TownLayout.HalfWidth, MapMinZ = -16f, MapMaxZ = 100f, MapCell = 2f;
        public static readonly int MapWidth = Mathf.CeilToInt((MapMaxX - MapMinX) / MapCell);
        public static readonly int MapHeight = Mathf.CeilToInt((MapMaxZ - MapMinZ) / MapCell);

        private const string SaveKey = "Hushwater.Save";
        private static SaveData data = new SaveData();
        private static bool[] map;

        public static Mode Next = Mode.Menu;

        // Evidence, salvage and tools picked up during the current night. Banked only when Nell rows out.
        public static readonly List<string> FoundTonight = new List<string>();
        public static int BrassTonight, WireTonight, LensTonight;
        // The last night's result, shown on the boat screen.
        public static string BoatMessage = "";
        public static string LastEndingText = "";
        // What was banked the last time Nell rowed out, for the boat screen ("develop photos").
        public static readonly List<string> LastBanked = new List<string>();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            data = new SaveData();
            map = null;
            Next = Mode.Menu;
            FoundTonight.Clear();
            LastBanked.Clear();
            BrassTonight = WireTonight = LensTonight = 0;
        }

        public static int Night => data.night;
        public static bool HasSave => PlayerPrefs.HasKey(SaveKey);
        public static int Brass => data.brass;
        public static int Wire => data.wire;
        public static int Lens => data.lens;
        public static int Ladders => data.ladders;
        public static int FlashBulbs { get => data.flashBulbs; set => data.flashBulbs = Mathf.Max(0, value); }
        public static bool SpillwayOpened { get => data.spillwayOpened; set => data.spillwayOpened = value; }
        public static Story.Ending Ending => (Story.Ending)data.ending;

        // ---------- Starting, saving, loading ----------

        // New Game: Night 1 with the starting kit (three bottles), nothing found.
        public static void NewGame()
        {
            data = new SaveData();
            FoundTonight.Clear();
            LastBanked.Clear();
            BrassTonight = WireTonight = LensTonight = 0;
            data.toolKinds.Add((int)Pickup.Kind.Bottle);
            data.toolCounts.Add(3);
            map = new bool[MapWidth * MapHeight];
            Save();
        }

        // Continue: reads the save made at the boat.
        public static bool Load()
        {
            if (!HasSave) return false;
            try
            {
                data = JsonUtility.FromJson<SaveData>(PlayerPrefs.GetString(SaveKey)) ?? new SaveData();
            }
            catch (Exception)
            {
                data = new SaveData();
                return false;
            }
            map = null;
            return true;
        }

        // Writes the whole run to PlayerPrefs as JSON. Only called at the boat, so dying rolls back to here.
        public static void Save()
        {
            data.map = EncodeMap();
            PlayerPrefs.SetString(SaveKey, JsonUtility.ToJson(data));
            PlayerPrefs.Save();
        }

        // ---------- Evidence ----------

        // Two tiers: FoundTonight is at risk until she rows out; data.evidence is banked for good.
        // True if the piece is banked or was found tonight.
        public static bool Has(string id) => data.evidence.Contains(id) || FoundTonight.Contains(id);
        public static bool Banked(string id) => data.evidence.Contains(id);

        // Every banked or found-tonight piece of one type.
        public static int Count(EvidenceType type)
        {
            int n = 0;
            foreach (EvidenceSpot spot in Story.Evidence)
                if (spot.Type == type && Has(spot.Id)) n++;
            return n;
        }

        // How much of a night's own evidence has been found (banked on earlier tries, or tonight).
        public static int CountForNight(int night, EvidenceType type)
        {
            int n = 0;
            foreach (EvidenceSpot spot in Story.ForNight(night))
                if (spot.Type == type && Has(spot.Id)) n++;
            return n;
        }

        // Design doc: "Nights 1-4 need their listed evidence to continue."
        public static bool NightGoalMet(int night) =>
            CountForNight(night, EvidenceType.Photo) >= Story.PhotosNeeded(night) &&
            CountForNight(night, EvidenceType.Page) >= Story.PagesNeeded(night) &&
            CountForNight(night, EvidenceType.Tape) >= Story.TapesNeeded(night);

        public static List<EvidenceSpot> BankedOfType(EvidenceType type)
        {
            var list = new List<EvidenceSpot>();
            foreach (EvidenceSpot spot in Story.Evidence)
                if (spot.Type == type && Has(spot.Id)) list.Add(spot);
            return list;
        }

        // Tapes are played at the boat; only played ones show their transcript in the journal.
        public static bool TapePlayed(string id) => data.tapesPlayed.Contains(id);

        // ---------- The end of a night ----------

        // Nell rowed out: bank the night's finds, salvage and tools, and move on if the night's target is met.
        public static bool BankNight(PlayerController player)
        {
            LastBanked.Clear();
            LastBanked.AddRange(FoundTonight);
            foreach (string id in FoundTonight)
                if (!data.evidence.Contains(id)) data.evidence.Add(id);
            data.brass += BrassTonight;
            data.wire += WireTonight;
            data.lens += LensTonight;
            StoreTools(player);
            bool advance = NightGoalMet(data.night) && data.night < 5;
            int finished = data.night;
            if (advance) data.night++;
            FoundTonight.Clear();
            BrassTonight = WireTonight = LensTonight = 0;
            Save();
            BoatMessage = advance
                ? $"Night {finished} done. Night {data.night} opens: {Story.NightDistricts[data.night]}. {Story.NightPressure[data.night]}"
                : finished == 5
                    ? "The proof is safe in the boat, but it has to go out over the radio. Row back in and transmit."
                    : $"You haven't got everything Night {finished} needs. Row back in and finish it.";
            return advance;
        }

        // The night was lost: whatever was found tonight goes back into the silt.
        public static void LoseNight()
        {
            FoundTonight.Clear();
            BrassTonight = WireTonight = LensTonight = 0;
            Load();
        }

        // Plays every new tape at the boat and returns them.
        public static List<EvidenceSpot> PlayNewTapes()
        {
            var fresh = new List<EvidenceSpot>();
            foreach (EvidenceSpot spot in Story.Evidence)
                if (spot.Type == EvidenceType.Tape && data.evidence.Contains(spot.Id) && !data.tapesPlayed.Contains(spot.Id))
                {
                    data.tapesPlayed.Add(spot.Id);
                    fresh.Add(spot);
                }
            Save();
            return fresh;
        }

        // Night 5's transmission finished (or failed): pick the ending.
        public static void Finish(Story.Ending ending, PlayerController player)
        {
            foreach (string id in FoundTonight)
                if (!data.evidence.Contains(id)) data.evidence.Add(id);
            FoundTonight.Clear();
            data.ending = (int)ending;
            PlayerPrefs.DeleteKey(SaveKey); // the run is over; Continue goes away
            PlayerPrefs.Save();
        }

        // ---------- Salvage and upgrades ----------

        public static void AddSalvage(Pickup.Kind kind)
        {
            if (kind == Pickup.Kind.Brass) BrassTonight++;
            else if (kind == Pickup.Kind.Wire) WireTonight++;
            else if (kind == Pickup.Kind.Lens) LensTonight++;
        }

        public static bool Owns(Upgrade upgrade) => data.upgrades.Contains((int)upgrade);

        // Design doc costs: boots 4 brass; crank 3 brass 2 wire; shutter 2 lens 2 wire; lungs 5 brass;
        // tool slot 3 wire 3 brass; rope ladder 2 wire each.
        public static void Cost(Upgrade upgrade, out int brass, out int wire, out int lens)
        {
            brass = wire = lens = 0;
            switch (upgrade)
            {
                case Upgrade.Boots: brass = 4; break;
                case Upgrade.Crank: brass = 3; wire = 2; break;
                case Upgrade.Shutter: lens = 2; wire = 2; break;
                case Upgrade.Lungs: brass = 5; break;
                case Upgrade.ToolSlot: wire = 3; brass = 3; break;
                case Upgrade.Ladder: wire = 2; break;
            }
        }

        // Rope ladders can be bought once per ladder spot; everything else once.
        public static bool CanBuy(Upgrade upgrade)
        {
            if (upgrade == Upgrade.Ladder ? data.ladders >= TownLayout.LadderSpots.Length : Owns(upgrade)) return false;
            Cost(upgrade, out int brass, out int wire, out int lens);
            return data.brass >= brass && data.wire >= wire && data.lens >= lens;
        }

        public static bool Buy(Upgrade upgrade)
        {
            if (!CanBuy(upgrade)) return false;
            Cost(upgrade, out int brass, out int wire, out int lens);
            data.brass -= brass;
            data.wire -= wire;
            data.lens -= lens;
            if (upgrade == Upgrade.Ladder) data.ladders++;
            else data.upgrades.Add((int)upgrade);
            Save();
            return true;
        }

        // ---------- Tools carried between nights ----------

        public static void RestoreTools(PlayerController player)
        {
            for (int i = 0; i < data.toolKinds.Count && i < data.toolCounts.Count; i++)
                player.GiveTool((Pickup.Kind)data.toolKinds[i], data.toolCounts[i]);
        }

        private static void StoreTools(PlayerController player)
        {
            data.toolKinds.Clear();
            data.toolCounts.Clear();
            foreach (PlayerController.ToolStack stack in player.Tools)
            {
                if (stack.Count <= 0) continue;
                data.toolKinds.Add((int)stack.Kind);
                data.toolCounts.Add(stack.Count);
            }
        }

        // ---------- What the town remembers ----------

        public static bool DoorBroken(string id) => data.brokenDoors.Contains(id);

        public static void BreakDoor(string id)
        {
            if (!string.IsNullOrEmpty(id) && !data.brokenDoors.Contains(id)) data.brokenDoors.Add(id);
        }

        // Design doc: "a hunter who learns your hiding spots." Uses carry over from night to night.
        public static int HideCount(string id)
        {
            int i = data.hideIds.IndexOf(id);
            return i < 0 ? 0 : data.hideCounts[i];
        }

        public static void NoteHide(string id)
        {
            int i = data.hideIds.IndexOf(id);
            if (i < 0)
            {
                data.hideIds.Add(id);
                data.hideCounts.Add(1);
            }
            else data.hideCounts[i]++;
        }

        // Design doc, Night 4: "His routes follow the evidence you took."
        public static IReadOnlyList<Vector3> GuardedSites => data.guardedSites;

        // Records where evidence was taken so the Sexton's later routes pass there (skips spots within 3 m).
        public static void Guard(Vector3 site)
        {
            foreach (Vector3 known in data.guardedSites)
                if (Vector3.Distance(known, site) < 3f) return;
            data.guardedSites.Add(site);
        }

        // ---------- The hand-drawn map ----------

        public static bool Revealed(int x, int z)
        {
            EnsureMap();
            return x >= 0 && z >= 0 && x < MapWidth && z < MapHeight && map[z * MapWidth + x];
        }

        // Fills in the map around Nell as she explores. The map never shows the Sexton.
        public static void Reveal(Vector3 position, float radius)
        {
            EnsureMap();
            int cx = Mathf.FloorToInt((position.x - MapMinX) / MapCell);
            int cz = Mathf.FloorToInt((position.z - MapMinZ) / MapCell);
            int r = Mathf.CeilToInt(radius / MapCell);
            for (int z = cz - r; z <= cz + r; z++)
                for (int x = cx - r; x <= cx + r; x++)
                {
                    if (x < 0 || z < 0 || x >= MapWidth || z >= MapHeight) continue;
                    if ((x - cx) * (x - cx) + (z - cz) * (z - cz) > r * r) continue;
                    map[z * MapWidth + x] = true;
                }
        }

        private static void EnsureMap()
        {
            if (map != null) return;
            map = new bool[MapWidth * MapHeight];
            string saved = data.map ?? "";
            // A map drawn before the town was widened has a different row length: start it fresh.
            if (saved.Length != map.Length) saved = "";
            for (int i = 0; i < map.Length && i < saved.Length; i++) map[i] = saved[i] == '1';
        }

        private static string EncodeMap()
        {
            EnsureMap();
            var chars = new char[map.Length];
            for (int i = 0; i < map.Length; i++) chars[i] = map[i] ? '1' : '0';
            return new string(chars);
        }
    }
}
