using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

namespace Hushwater
{
    // One building in town. The layout is fixed (design doc: "Fixed landmarks to steer by"); the journal map draws it too.
    public sealed class BuildingDef
    {
        public string Name;
        public Vector2 Centre;
        public Vector2 Size;
        // Which wall the doorway is in: 'N', 'S', 'E' or 'W'.
        public char Door;
        public float Height;
        // Set for doors nailed shut on the flood night.
        public string BarredId;

        public BuildingDef(string name, float x, float z, float w, float d, char door, float height, string barredId = null)
        {
            Name = name;
            Centre = new Vector2(x, z);
            Size = new Vector2(w, d);
            Door = door;
            Height = height;
            BarredId = barredId;
        }

        public Rect Footprint => new Rect(Centre - Size * 0.5f, Size);
    }

    // Builds the town for one night: three districts, the lake at tonight's level, evidence, tools, threats,
    // lantern posts, ladders, and the NavMesh. Placement numbers are ours; the doc gives the districts and landmarks.
    public sealed class TownBuilder
    {
        public static readonly BuildingDef[] Buildings =
        {
            // Millrow
            new BuildingDef("Holloway house", -9f, -6f, 6f, 6f, 'E', 3f),
            new BuildingDef("Petrov house", 9f, -9f, 6f, 6f, 'W', 3f),
            new BuildingDef("The Mill", -9f, 4f, 6f, 8f, 'E', 6f),
            new BuildingDef("Dunne house", 9f, 2f, 6f, 6f, 'W', 3f),
            new BuildingDef("Lisle house", -9f, 14f, 6f, 6f, 'E', 3f, "door:lisle"),
            new BuildingDef("Bramley house", 10f, 17f, 6f, 6f, 'W', 3f),
            // Chapel Hill
            new BuildingDef("Ashdown house", -9f, 30f, 6f, 6f, 'E', 3f),
            new BuildingDef("Crypt", 11f, 30f, 4f, 4f, 'W', 2.5f),
            new BuildingDef("Chapel", -9f, 44f, 10f, 12f, 'E', 5f),
            new BuildingDef("Bell tower", 8f, 49f, 4f, 4f, 'W', 12f, "door:tower"),
            new BuildingDef("School", -9f, 56f, 10f, 8f, 'E', 3.5f),
            new BuildingDef("Chapel House", 10f, 57f, 6f, 6f, 'W', 3f),
            // The Dam Works
            new BuildingDef("Lock-keeper's house", -9f, 70f, 8f, 10f, 'E', 3f),
            new BuildingDef("Pump house", 9f, 68f, 8f, 8f, 'W', 3.5f),
            new BuildingDef("Radio room", -6f, 92f, 10f, 8f, 'S', 4f),
        };

        // The graveyard wall, the spillway channel and the flooded culvert, for the map.
        public static readonly Rect Graveyard = new Rect(4f, 27f, 10f, 16f);
        public static readonly Rect SpillwayChannel = new Rect(-15f, 79f, 30f, 4f);
        public static readonly Rect Culvert = new Rect(9f, 86f, 4f, 12f);

        // Patrol stops along each district's streets. Evidence sites are added to these.
        private static readonly Vector3[] MillrowPatrol =
        {
            new Vector3(0f, 0f, -10f), new Vector3(-4.5f, 0f, -2f), new Vector3(-4.5f, 0f, 8f), new Vector3(-4.5f, 0f, 14f),
            new Vector3(0f, 0f, 20f), new Vector3(5f, 0f, 21f), new Vector3(5f, 0f, 12f), new Vector3(4.5f, 0f, 2f)
        };
        private static readonly Vector3[] ChapelPatrol =
        {
            new Vector3(0f, 0f, 28f), new Vector3(-2f, 0f, 36f), new Vector3(-2f, 0f, 44f), new Vector3(3f, 0f, 46f),
            new Vector3(-2f, 0f, 52f), new Vector3(0f, 0f, 59f), new Vector3(3.5f, 0f, 40f), new Vector3(3.5f, 0f, 33f)
        };
        private static readonly Vector3[] DamPatrol =
        {
            new Vector3(0f, 0f, 66f), new Vector3(-3f, 0f, 70f), new Vector3(3f, 0f, 68f), new Vector3(0f, 0f, 76f),
            new Vector3(0f, 0f, 86f), new Vector3(-6f, 0f, 86f), new Vector3(4f, 0f, 90f), new Vector3(3f, 0f, 97f)
        };

        // Lantern posts per district, in the order they are used (Undertow gets only the first of each).
        private static readonly Vector3[][] LanternSpots =
        {
            new[] { new Vector3(-4f, 0f, 4f), new Vector3(2f, 0f, 18f), new Vector3(2.5f, 0f, -11.5f), new Vector3(3.5f, 0f, -1f) },
            new[] { new Vector3(-2.5f, 0f, 28f), new Vector3(2.5f, 0f, 45f), new Vector3(-2.5f, 0f, 53f), new Vector3(3f, 0f, 60f) },
            new[] { new Vector3(-2f, 0f, 66f), new Vector3(-1.5f, 0f, 75.5f), new Vector3(-1f, 0f, 86f), new Vector3(3f, 0f, 95f) },
        };

        private readonly int night;
        private Material ground, siding, roof, iron, mud, plaster, stone, wood, water, paper, dark;

        public readonly List<Vector3> PatrolPoints = new List<Vector3>();
        public Vector3 NellStart { get; private set; }
        public Vector3 SextonStart { get; private set; }
        public Vector3 Boat { get; private set; }
        public SpillwayWheel Spillway { get; private set; }

        public TownBuilder(int night)
        {
            this.night = night;
        }

        // Everything, in order: geometry, then the NavMesh, then the things that need it.
        public void Build()
        {
            LoadMaterials();
            BuildGroundAndEdges();
            BuildLake();
            foreach (BuildingDef b in Buildings) BuildBuilding(b);
            BuildMillrow();
            BuildChapelHill();
            BuildDamWorks();
            BuildEvidence();
            BuildLanternPostsAndLadders();
            BuildLighting();

            Boat = TownLayout.BoatPosition(night);
            BuildBoat(Boat);
            NellStart = Boat + new Vector3(0f, 1f, 1.6f);
            SextonStart = night == 1 ? new Vector3(0f, 0f, 18f) : night == 2 ? new Vector3(0f, 0f, 55f) : new Vector3(3f, 0f, 92f);
            BuildPatrolRoute();
            BakeNavMesh();
        }

        private bool Open(Vector3 p) => TownLayout.Open(night, p);
        private bool Dry(Vector3 p) => Open(p) && p.z >= TownLayout.Shoreline(night);

        // ---------- Materials ----------

        private void LoadMaterials()
        {
            ground = HushwaterAssets.Surface("brown_mud_02", new Color(0.55f, 0.52f, 0.47f), 30f);
            siding = HushwaterAssets.Surface("weathered_plank_siding", new Color(0.55f, 0.55f, 0.52f), 2f);
            roof = HushwaterAssets.Surface("roof_slates_02", new Color(0.5f, 0.5f, 0.5f), 3f);
            iron = HushwaterAssets.Surface("rusty_metal_02", new Color(0.6f, 0.55f, 0.5f));
            mud = HushwaterAssets.Surface("brown_mud_02", new Color(0.22f, 0.2f, 0.16f), 2f, 0.75f);
            plaster = HushwaterAssets.Surface("worn_mossy_plasterwall", new Color(0.6f, 0.6f, 0.56f), 2f);
            stone = HushwaterAssets.Surface("worn_mossy_plasterwall", new Color(0.4f, 0.41f, 0.4f), 4f);
            wood = HushwaterAssets.Surface("weathered_plank_siding", new Color(0.35f, 0.3f, 0.25f), 1f);
            paper = new Material(Shader.Find("Standard")) { color = new Color(0.85f, 0.82f, 0.72f) };
            dark = new Material(Shader.Find("Standard")) { color = new Color(0.06f, 0.06f, 0.06f) };
            water = Transparent(new Color(UiKit.Lake.r * 0.6f, UiKit.Lake.g * 0.6f, UiKit.Lake.b * 0.6f, 0.72f));
        }

        // A Standard-shader material switched to alpha blending, for the lake.
        public static Material Transparent(Color color)
        {
            Material m = new Material(Shader.Find("Standard")) { color = color };
            m.SetFloat("_Mode", 3f);
            m.SetOverrideTag("RenderType", "Transparent");
            m.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
            m.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            m.SetInt("_ZWrite", 0);
            m.DisableKeyword("_ALPHATEST_ON");
            m.EnableKeyword("_ALPHABLEND_ON");
            m.renderQueue = 3000;
            m.SetFloat("_Glossiness", 0.9f);
            return m;
        }

        // ---------- Ground, edges, the lake ----------

        private void BuildGroundAndEdges()
        {
            float length = TownLayout.DamWall - TownLayout.MillrowStart;
            float midZ = (TownLayout.DamWall + TownLayout.MillrowStart) * 0.5f;
            Cube("Ground", new Vector3(0f, -0.25f, midZ), new Vector3(30f, 0.5f, length), ground);
            Wall("West Edge", new Vector3(-15f, 2f, midZ), new Vector3(0.5f, 4f, length), stone);
            Wall("East Edge", new Vector3(15f, 2f, midZ), new Vector3(0.5f, 4f, length), stone);
            // A low quay wall: the lake is visible beyond it.
            Cube("Quay", new Vector3(0f, 0.5f, TownLayout.MillrowStart), new Vector3(30f, 1f, 0.5f), stone);

            // The dam: a landmark you can see from everywhere.
            Wall("The Dam", new Vector3(0f, 7f, TownLayout.DamWall + 1f), new Vector3(34f, 14f, 2f), stone);

            // Districts that aren't open yet are blocked by rubble.
            float north = TownLayout.NorthLimit(night);
            if (north < TownLayout.DamWall) Rubble(north);

            // Past the shoreline the town is under the lake. On Night 3 the flooded street can be swum.
            float shore = TownLayout.Shoreline(night);
            if (shore > TownLayout.MillrowStart && !TownLayout.FloodSwimmable(night))
                Invisible("Lake Edge", new Vector3(0f, 2f, shore - 0.3f), new Vector3(30f, 6f, 0.4f));
        }

        // A tumble of beams and stone across the street, plus an invisible wall so the gap can't be climbed.
        private void Rubble(float z)
        {
            for (int i = 0; i < 9; i++)
            {
                float x = -13f + i * 3.25f;
                GameObject block = Cube("Rubble", new Vector3(x, 1.2f, z + Random.Range(-0.4f, 0.4f)),
                    new Vector3(3.4f, 2.4f + Random.Range(0f, 1.2f), 1.4f), i % 2 == 0 ? stone : wood);
                block.transform.rotation = Quaternion.Euler(Random.Range(-8f, 8f), Random.Range(-20f, 20f), Random.Range(-6f, 6f));
            }
            Invisible("District Closed", new Vector3(0f, 3f, z), new Vector3(30f, 6f, 1.8f));
        }

        // The reservoir: a sheet of water south of the shoreline at tonight's level.
        private void BuildLake()
        {
            float shore = TownLayout.Shoreline(night);
            float level = TownLayout.LakeLevel(night);
            float south = -90f;
            GameObject lake = GameObject.CreatePrimitive(PrimitiveType.Cube);
            lake.name = "The Lake";
            Object.DestroyImmediate(lake.GetComponent<Collider>());
            float bottom = -1.5f;
            lake.transform.position = new Vector3(0f, (level + bottom) * 0.5f, (shore + south) * 0.5f);
            lake.transform.localScale = new Vector3(200f, level - bottom, shore - south);
            lake.GetComponent<Renderer>().sharedMaterial = water;
            lake.layer = 2;

            if (TownLayout.FloodSwimmable(night))
            {
                WaterVolume.Add(new Bounds(new Vector3(0f, (level - 1f) * 0.5f, (shore + TownLayout.MillrowStart) * 0.5f),
                    new Vector3(30f, level + 1f, shore - TownLayout.MillrowStart)));
                // Design doc: "Eels of the Cellars. Found in flooded rooms."
                Eel.Build(new Vector3(0f, 0.9f, -12f), new Vector3(0f, 0.9f, -6f));
            }
        }

        // ---------- Buildings ----------

        // Four walls with a doorway, a roof, and (for barred houses) a nailed door.
        private void BuildBuilding(BuildingDef b)
        {
            Vector3 c = new Vector3(b.Centre.x, 0f, b.Centre.y);
            if (!NearOpen(c, b.Size)) return;
            float hx = b.Size.x * 0.5f, hz = b.Size.y * 0.5f, h = b.Height;
            Material walls = b.Name == "Chapel" || b.Name == "Bell tower" || b.Name == "Crypt" || b.Name == "Radio room" || b.Name == "Pump house" ? stone : siding;
            if (b.Name == "School" || b.Name == "Lock-keeper's house") walls = plaster;

            WallLine(b.Name, new Vector3(c.x - hx, 0f, c.z), b.Size.y, true, h, b.Door == 'W', walls);
            WallLine(b.Name, new Vector3(c.x + hx, 0f, c.z), b.Size.y, true, h, b.Door == 'E', walls);
            WallLine(b.Name, new Vector3(c.x, 0f, c.z - hz), b.Size.x, false, h, b.Door == 'S', walls);
            WallLine(b.Name, new Vector3(c.x, 0f, c.z + hz), b.Size.x, false, h, b.Door == 'N', walls);
            Cube(b.Name + " Roof", new Vector3(c.x, h + 0.1f, c.z), new Vector3(b.Size.x + 0.4f, 0.2f, b.Size.y + 0.4f), roof);

            if (!string.IsNullOrEmpty(b.BarredId))
            {
                Vector3 door = DoorPoint(b);
                bool alongX = b.Door == 'E' || b.Door == 'W';
                float outward = b.Door == 'E' ? 1f : b.Door == 'W' ? -1f : b.Door == 'N' ? 2f : -2f;
                BarredDoor.Build(door + Vector3.up * 1.1f, alongX ? new Vector3(0.2f, 2.2f, 2f) : new Vector3(2f, 2.2f, 0.2f), siding, iron, outward, b.BarredId);
            }
        }

        // The middle of a building's doorway, at ground level.
        public static Vector3 DoorPoint(BuildingDef b)
        {
            Vector3 c = new Vector3(b.Centre.x, 0f, b.Centre.y);
            switch (b.Door)
            {
                case 'E': return c + Vector3.right * b.Size.x * 0.5f;
                case 'W': return c + Vector3.left * b.Size.x * 0.5f;
                case 'N': return c + Vector3.forward * b.Size.y * 0.5f;
                default: return c + Vector3.back * b.Size.y * 0.5f;
            }
        }

        private static BuildingDef Find(string name) => System.Array.Find(Buildings, b => b.Name == name);

        // One wall, with a 2 m doorway (and a lintel over it) in the middle if it has the door.
        private void WallLine(string name, Vector3 centre, float length, bool alongZ, float height, bool door, Material material)
        {
            const float t = 0.3f;
            Vector3 axis = alongZ ? Vector3.forward : Vector3.right;
            Vector3 Size(float len, float hgt) => alongZ ? new Vector3(t, hgt, len) : new Vector3(len, hgt, t);
            if (!door)
            {
                Wall(name + " Wall", centre + Vector3.up * height * 0.5f, Size(length + t, height), material);
                return;
            }
            float side = (length - 2f) * 0.5f;
            Wall(name + " Wall", centre - axis * (1f + side * 0.5f) + Vector3.up * height * 0.5f, Size(side + t * 0.5f, height), material);
            Wall(name + " Wall", centre + axis * (1f + side * 0.5f) + Vector3.up * height * 0.5f, Size(side + t * 0.5f, height), material);
            float lintel = height - 2.2f;
            if (lintel > 0.05f) Wall(name + " Lintel", centre + Vector3.up * (2.2f + lintel * 0.5f), Size(2f, lintel), material);
        }

        // True if any part of a footprint is in tonight's town (closed districts aren't built at all).
        private bool NearOpen(Vector3 c, Vector2 size)
        {
            float zMin = c.z - size.y * 0.5f, zMax = c.z + size.y * 0.5f;
            if (zMin > TownLayout.NorthLimit(night) + 4f) return false;
            return true;
        }

        // ---------- Millrow ----------

        private void BuildMillrow()
        {
            if (!NearOpen(new Vector3(0f, 0f, 4f), new Vector2(30f, 40f))) return;
            bool flooded = night >= 3;

            Wardrobe(new Vector3(-11.3f, 1f, -6f), Vector3.right, "hide:holloway");
            Wardrobe(new Vector3(11.3f, 1f, -10.5f), Vector3.left, "hide:petrov");
            Wardrobe(new Vector3(8.5f, 1f, 4.3f), Vector3.back, "hide:dunne");
            Wardrobe(new Vector3(-8f, 1f, 16.3f), Vector3.back, "hide:lisle");
            Wardrobe(new Vector3(12.3f, 1f, 15.5f), Vector3.left, "hide:bramley");

            // The Mill: tall, with a wheel on its west wall, and rotten boards inside.
            GameObject wheel = Cube("Mill Wheel", new Vector3(-12.6f, 2.6f, 4f), new Vector3(0.4f, 5f, 5f), wood);
            Object.DestroyImmediate(wheel.GetComponent<Collider>());
            wheel.transform.rotation = Quaternion.Euler(30f, 0f, 0f);
            RottenFloor.Build(new Vector3(-9.5f, 0f, 2.5f), new Vector2(2.6f, 2.6f), wood);

            if (!flooded) Jetty(new Vector3(6f, 0f, -14.4f));

            Mud(new Vector3(0f, 0f, 5f), 2.8f, false);
            Mud(new Vector3(-4.3f, 0f, 13.5f), 2.2f, true);
            Mud(new Vector3(3.6f, 0f, 2f), 2f, false);
            Mud(new Vector3(-1.5f, 0f, -9f), 2f, true);
            Mud(new Vector3(2.5f, 0f, 21.5f), 1.8f, false);

            Cache(new Vector3(3f, 0f, -4f), 20f);
            Cache(new Vector3(-10.5f, 0f, 6.5f), -15f);
            Cache(new Vector3(13f, 0f, 10f), 5f);
            HushwaterAssets.Model("Barrel_01", new Vector3(4f, 0f, 16f), 0f, 0.9f);
            HushwaterAssets.Model("Barrel_01", new Vector3(4.7f, 0f, 16.8f), 60f, 0.9f);
            StreetLamp(new Vector3(-4.5f, 0f, -10f), 90f);
            StreetLamp(new Vector3(4.5f, 0f, 9f), -90f);

            Item(new Vector3(5f, 0f, -8f), Pickup.Kind.Bottle);
            Item(new Vector3(-6f, 0f, 9.5f), Pickup.Kind.Bottle);
            Item(new Vector3(-10f, 0f, -8f), Pickup.Kind.Bottle);
            Item(new Vector3(-1f, 0f, 21.5f), Pickup.Kind.Handbell);
            Item(new Vector3(10.5f, 0f, -2f), Pickup.Kind.Handbell);
            Item(new Vector3(-10.5f, 0f, -3.5f), Pickup.Kind.Salts);
            Item(new Vector3(7.5f, 0f, 4.2f), Pickup.Kind.Salts);
            Item(new Vector3(11f, 0f, 19f), Pickup.Kind.Wraps);
            Item(new Vector3(-7f, 0f, 1f), Pickup.Kind.LampOil);
            Item(new Vector3(9f, 0f, -11f), Pickup.Kind.Brass);
            Item(new Vector3(-11f, 0f, 7f), Pickup.Kind.Brass);
            Item(new Vector3(12f, 0f, 16f), Pickup.Kind.Wire);

            // The Congregation (from Night 2): residents frozen where the water found them.
            if (night >= 2)
            {
                Congregation(new Vector3(8.5f, 0f, -8f), 270f);
                Congregation(new Vector3(-8f, 0f, 5.5f), 90f);
            }
            // Lantern Moths (Night 3+) nest in drowned attics.
            if (night >= 3) Moths(new Vector3(-9f, 4.6f, 6f));
        }

        // ---------- Chapel Hill ----------

        private void BuildChapelHill()
        {
            if (night < 2) return;

            Wardrobe(new Vector3(-11.3f, 1f, 28.5f), Vector3.right, "hide:ashdown");
            Wardrobe(new Vector3(-8f, 1f, 52.7f), Vector3.forward, "hide:school");
            Wardrobe(new Vector3(12.3f, 1f, 55.5f), Vector3.left, "hide:chapelhouse");

            // Chapel: two banks of pews either side of the aisle, an altar at the west end. Pews are hiding spots.
            foreach (float z in new[] { 40f, 48f })
                Pew(new Vector3(-9f, 0f, z), "hide:pew" + z);
            Pew(new Vector3(-9f, 0f, 42f), null);
            Pew(new Vector3(-9f, 0f, 46f), null);
            Cube("Altar", new Vector3(-12.8f, 0.5f, 44f), new Vector3(1f, 1f, 2.4f), stone);

            // The bell tower: the bell at the top is the landmark of the whole valley.
            GameObject bell = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            bell.name = "Chapel Bell";
            Object.DestroyImmediate(bell.GetComponent<Collider>());
            bell.transform.position = new Vector3(8f, 11f, 49f);
            bell.transform.localScale = new Vector3(1.4f, 0.8f, 1.4f);
            bell.GetComponent<Renderer>().sharedMaterial = iron;

            // The graveyard: a low wall, headstones, and deep mud where the Silt Hands wait.
            Rect g = Graveyard;
            Cube("Graveyard Wall", new Vector3(g.xMin, 0.45f, g.center.y + 2f), new Vector3(0.3f, 0.9f, g.height - 4f), stone);
            Cube("Graveyard Wall", new Vector3(g.center.x, 0.45f, g.yMax), new Vector3(g.width, 0.9f, 0.3f), stone);
            Cube("Graveyard Wall", new Vector3(g.center.x + 1.5f, 0.45f, g.yMin), new Vector3(g.width - 3f, 0.9f, 0.3f), stone);
            for (int i = 0; i < 14; i++)
            {
                Vector3 p = new Vector3(5.5f + (i % 4) * 2.2f, 0.5f, 34f + (i / 4) * 2.4f);
                if (Vector3.Distance(p, new Vector3(8f, 0.5f, 36f)) < 2.6f) continue;
                GameObject stoneSlab = Cube("Headstone", p, new Vector3(0.7f, 1f, 0.15f), stone);
                stoneSlab.transform.rotation = Quaternion.Euler(Random.Range(-10f, 10f), Random.Range(-15f, 15f), Random.Range(-8f, 8f));
            }
            Mud(new Vector3(8f, 0f, 36f), 2.5f, true);
            Mud(new Vector3(-1.5f, 0f, 40f), 2.2f, false);
            Mud(new Vector3(2f, 0f, 55f), 2f, false);

            // The school: rows of desks, rotten boards by the stove.
            for (int r = 0; r < 3; r++)
                for (int col = 0; col < 2; col++)
                    Cube("Desk", new Vector3(-11f + col * 3f, 0.4f, 54f + r * 1.6f), new Vector3(1.2f, 0.8f, 0.5f), wood);
            RottenFloor.Build(new Vector3(-5.6f, 0f, 59f), new Vector2(1.4f, 1.4f), wood);

            Cache(new Vector3(3f, 0f, 31f), 10f);
            Cache(new Vector3(-3f, 0f, 48f), -20f);
            Cache(new Vector3(12f, 0f, 58.5f), 35f);
            StreetLamp(new Vector3(-3.2f, 0f, 36f), 90f);
            StreetLamp(new Vector3(3.2f, 0f, 52f), -90f);

            Item(new Vector3(-2f, 0f, 30f), Pickup.Kind.Bottle);
            Item(new Vector3(5f, 0f, 44f), Pickup.Kind.Bottle);
            Item(new Vector3(-12.5f, 0f, 41f), Pickup.Kind.Handbell);
            Item(new Vector3(-6f, 0f, 60f), Pickup.Kind.Salts);
            Item(new Vector3(9f, 0f, 59f), Pickup.Kind.Wraps);
            Item(new Vector3(10f, 0f, 41f), Pickup.Kind.LampOil);
            Item(new Vector3(-7f, 0f, 32f), Pickup.Kind.Brass);
            Item(new Vector3(12f, 0f, 29f), Pickup.Kind.Wire);
            Item(new Vector3(-12.5f, 0f, 59f), Pickup.Kind.Lens);
            if (night >= 3) Item(new Vector3(11f, 0f, 55f), Pickup.Kind.FlashBulbs);

            Congregation(new Vector3(-6f, 0f, 41.5f), 270f);
            Congregation(new Vector3(-11f, 0f, 47f), 90f);
            Congregation(new Vector3(6.5f, 0f, 33f), 0f);
            Congregation(new Vector3(13f, 0f, 37.5f), 180f);
            Congregation(new Vector3(-1f, 0f, 56f), 200f);
            if (night >= 3)
            {
                Moths(new Vector3(-9f, 4f, 44f));
                Moths(new Vector3(10f, 2.5f, 57f));
            }
        }

        // ---------- The Dam Works ----------

        private void BuildDamWorks()
        {
            if (night < 3) return;

            Wardrobe(new Vector3(-12.3f, 1f, 66.4f), Vector3.right, "hide:lockkeeper");
            Wardrobe(new Vector3(12.3f, 1f, 70.5f), Vector3.left, "hide:pumphouse");
            RottenFloor.Build(new Vector3(-9f, 0f, 70f), new Vector2(2.2f, 2.2f), wood);
            Cube("Pump", new Vector3(10f, 0.9f, 67.5f), new Vector3(2f, 1.8f, 1.4f), iron);

            // The spillway channel crosses the road between the boat and the radio room.
            Rect ch = SpillwayChannel;
            GameObject bed = Cube("Spillway Channel", new Vector3(ch.center.x, 0.01f, ch.center.y), new Vector3(ch.width, 0.02f, ch.height), stone);
            Object.DestroyImmediate(bed.GetComponent<Collider>());
            GameObject channelWater = GameObject.CreatePrimitive(PrimitiveType.Cube);
            channelWater.name = "Spillway Flood";
            Object.DestroyImmediate(channelWater.GetComponent<Collider>());
            channelWater.transform.position = new Vector3(ch.center.x, 1f, ch.center.y);
            channelWater.transform.localScale = new Vector3(ch.width, 2.2f, ch.height);
            channelWater.GetComponent<Renderer>().sharedMaterial = water;
            channelWater.SetActive(false);
            Spillway = SpillwayWheel.Build(new Vector3(4f, 0f, 77.5f), iron, channelWater);

            // The flooded culvert: a stone tunnel under water, Emil's tape at the far end.
            Rect cu = Culvert;
            Wall("Culvert Wall", new Vector3(cu.xMin, 1.25f, cu.center.y), new Vector3(0.3f, 2.5f, cu.height), stone);
            Wall("Culvert Wall", new Vector3(cu.xMax, 1.25f, cu.center.y), new Vector3(0.3f, 2.5f, cu.height), stone);
            Wall("Culvert End", new Vector3(cu.center.x, 1.25f, cu.yMax), new Vector3(cu.width, 2.5f, 0.3f), stone);
            Cube("Culvert Roof", new Vector3(cu.center.x, 2.35f, cu.center.y), new Vector3(cu.width + 0.3f, 0.3f, cu.height), stone);
            GameObject culvertWater = GameObject.CreatePrimitive(PrimitiveType.Cube);
            culvertWater.name = "Culvert Water";
            Object.DestroyImmediate(culvertWater.GetComponent<Collider>());
            culvertWater.transform.position = new Vector3(cu.center.x, 1.05f, cu.center.y + 0.3f);
            culvertWater.transform.localScale = new Vector3(cu.width - 0.3f, 2.1f, cu.height - 0.6f);
            culvertWater.GetComponent<Renderer>().sharedMaterial = water;
            culvertWater.layer = 2;
            WaterVolume.Add(new Bounds(culvertWater.transform.position + Vector3.down * 0.5f, culvertWater.transform.localScale + Vector3.up));
            Eel.Build(new Vector3(cu.center.x, 0.9f, cu.yMin + 2f), new Vector3(cu.center.x, 0.9f, cu.yMax - 1.5f));

            // The radio room inside the dam: Night 5's finish line.
            Cube("Generator", new Vector3(-2.2f, 0.6f, 90f), new Vector3(1.4f, 1.2f, 1f), iron);
            Cube("Radio Bench", new Vector3(-3f, 0.45f, 95f), new Vector3(2.4f, 0.9f, 0.8f), wood);
            GameObject radio = Cube("Radio Set", new Vector3(-3f, 1.2f, 95.2f), new Vector3(1.2f, 0.6f, 0.5f), dark);
            radio.AddComponent<Transmitter>();
            RadioDoor.Build(DoorPoint(Find("Radio room")), siding);

            Mud(new Vector3(0f, 0f, 72f), 2.5f, false);
            Mud(new Vector3(4f, 0f, 87f), 2f, false);
            Mud(new Vector3(2f, 0f, 93f), 2.2f, false);
            Cache(new Vector3(3f, 0f, 64.5f), 0f);
            Cache(new Vector3(-6.5f, 0f, 77.5f), 25f);
            Cache(new Vector3(6f, 0f, 97f), -10f);
            StreetLamp(new Vector3(3.2f, 0f, 72f), -90f);
            StreetLamp(new Vector3(-3.2f, 0f, 85f), 90f);

            Item(new Vector3(-2f, 0f, 64f), Pickup.Kind.Bottle);
            Item(new Vector3(6f, 0f, 92f), Pickup.Kind.Handbell);
            Item(new Vector3(-4f, 0f, 76f), Pickup.Kind.Handbell);
            Item(new Vector3(-7f, 0f, 67f), Pickup.Kind.Salts);
            Item(new Vector3(7f, 0f, 65f), Pickup.Kind.LampOil);
            Item(new Vector3(-12f, 0f, 87f), Pickup.Kind.Wraps);
            Item(new Vector3(11f, 0f, 71f), Pickup.Kind.Brass);
            Item(new Vector3(-10f, 0f, 90.5f), Pickup.Kind.Wire);
            Item(new Vector3(12.5f, 0f, 84f), Pickup.Kind.Lens);
            Item(new Vector3(-12f, 0f, 76.5f), Pickup.Kind.FlashBulbs);

            Congregation(new Vector3(-3f, 0f, 73f), 90f);
            Congregation(new Vector3(6f, 0f, 85f), 180f);
            Congregation(new Vector3(-12f, 0f, 83f), 60f);
            Moths(new Vector3(9f, 3f, 68f));
        }

        // ---------- Evidence ----------

        // Every unbanked piece placed on this night or earlier that sits in tonight's open town.
        // Design doc: "Anything extra you find changes the ending."
        private void BuildEvidence()
        {
            foreach (EvidenceSpot spot in Story.Evidence)
            {
                if (spot.Night > night || Campaign.Banked(spot.Id) || !Open(spot.Position)) continue;
                switch (spot.Type)
                {
                    case EvidenceType.Photo: PhotoScene(spot); break;
                    case EvidenceType.Page:
                        if (spot.InCabinet) RegisterCabinet.Build(spot, wood);
                        else EvidencePickup(spot, new Vector3(0.3f, 0.01f, 0.4f), paper, "register page");
                        break;
                    default: EvidencePickup(spot, new Vector3(0.32f, 0.14f, 0.22f), dark, "tape recorder"); break;
                }
                PatrolPoints.Add(new Vector3(spot.Position.x, 0f, spot.Position.z));
            }
        }

        // A page or a tape on the floor, with a faint glint so it can be found in the dark.
        private void EvidencePickup(EvidenceSpot spot, Vector3 size, Material material, string label)
        {
            GameObject item = GameObject.CreatePrimitive(PrimitiveType.Cube);
            item.name = spot.Title;
            Object.DestroyImmediate(item.GetComponent<Collider>());
            item.transform.position = spot.Position + Vector3.up * (size.y * 0.5f + 0.02f);
            item.transform.rotation = Quaternion.Euler(0f, Random.Range(0f, 360f), 0f);
            item.transform.localScale = size;
            item.GetComponent<Renderer>().sharedMaterial = material;
            Glint(item.transform, spot.Position + Vector3.up * 0.5f, 0.6f);
            Pickup.Create(item, Pickup.Kind.Evidence, label, spot);
        }

        // The scene a photograph shows, built from primitives. Dark scenes get no glint: they need the flash.
        private void PhotoScene(EvidenceSpot spot)
        {
            GameObject root = new GameObject("Photo: " + spot.Title);
            root.transform.position = spot.Position;
            Transform t = root.transform;
            switch (spot.Id)
            {
                case "photo:table":
                    Part(t, new Vector3(0f, 0.75f, 0f), new Vector3(0.9f, 0.06f, 1.8f), wood);
                    foreach (Vector3 leg in new[] { new Vector3(-0.4f, 0f, -0.8f), new Vector3(0.4f, 0f, -0.8f), new Vector3(-0.4f, 0f, 0.8f), new Vector3(0.4f, 0f, 0.8f) })
                        Part(t, leg + Vector3.up * 0.37f, new Vector3(0.06f, 0.74f, 0.06f), wood);
                    for (int i = 0; i < 6; i++)
                        Part(t, new Vector3(i < 3 ? -0.28f : 0.28f, 0.8f, -0.55f + (i % 3) * 0.55f), new Vector3(0.24f, 0.02f, 0.24f), paper, PrimitiveType.Cylinder);
                    break;
                case "photo:shoes":
                    for (int i = 0; i < 6; i++)
                        Part(t, new Vector3(0f, 0.06f, -0.9f + i * 0.36f), i % 3 == 2 ? new Vector3(0.14f, 0.1f, 0.2f) : new Vector3(0.26f, 0.12f, 0.12f), dark);
                    break;
                case "photo:bicycle":
                    Part(t, new Vector3(0f, 0.35f, -0.45f), new Vector3(0.7f, 0.02f, 0.7f), iron, PrimitiveType.Cylinder).transform.localRotation = Quaternion.Euler(0f, 0f, 90f);
                    Part(t, new Vector3(0f, 0.35f, 0.45f), new Vector3(0.7f, 0.02f, 0.7f), iron, PrimitiveType.Cylinder).transform.localRotation = Quaternion.Euler(0f, 0f, 90f);
                    Part(t, new Vector3(0f, 0.6f, 0f), new Vector3(0.05f, 0.05f, 0.9f), iron);
                    Part(t, new Vector3(-0.5f, 0.5f, 0f), new Vector3(0.08f, 1f, 2.4f), wood); // the porch rail
                    break;
                case "photo:pews":
                    Part(t, new Vector3(0f, 0.7f, 0.42f), new Vector3(2.2f, 0.45f, 0.05f), paper); // names carved into the back
                    break;
                case "photo:slate":
                    Part(t, new Vector3(-0.35f, 1.4f, 0f), new Vector3(0.05f, 1f, 1.8f), dark);
                    break;
                case "photo:tower":
                    Part(t, new Vector3(-0.45f, 1.1f, 0f), new Vector3(0.04f, 1.6f, 1.6f), paper); // nail points through the boards
                    break;
            }
            if (!spot.Dark) Glint(t, spot.Position + Vector3.up * 1.2f, 0.7f);
            PhotoSubject.Attach(root, spot);
        }

        // ---------- Lantern posts, ladders, the boat ----------

        private void BuildLanternPostsAndLadders()
        {
            int perDistrict = GameSettings.LanternPosts;
            foreach (Vector3[] district in LanternSpots)
                for (int i = 0; i < Mathf.Min(perDistrict, district.Length); i++)
                    if (Dry(district[i])) LanternPost.Build(district[i], iron);

            // Rope ladders bought at the boat, one per spot.
            Material rope = new Material(Shader.Find("Standard")) { color = new Color(0.45f, 0.38f, 0.25f) };
            for (int i = 0; i < Campaign.Ladders && i < TownLayout.LadderSpots.Length; i++)
                if (Dry(TownLayout.LadderSpots[i])) RopeLadder.Build(TownLayout.LadderSpots[i], TownLayout.LadderRoofs[i], rope);
        }

        // The rowboat at the water's edge, with a lantern to steer back to. Hold E beside it to row out.
        private void BuildBoat(Vector3 at)
        {
            GameObject boat = new GameObject("Boat");
            boat.transform.position = at;
            Transform t = boat.transform;
            Part(t, new Vector3(0f, 0.12f, 0f), new Vector3(2.8f, 0.12f, 1f), siding);
            Part(t, new Vector3(0f, 0.3f, 0.5f), new Vector3(2.8f, 0.4f, 0.08f), siding);
            Part(t, new Vector3(0f, 0.3f, -0.5f), new Vector3(2.8f, 0.4f, 0.08f), siding);
            Part(t, new Vector3(1.4f, 0.3f, 0f), new Vector3(0.08f, 0.4f, 1f), siding);
            Part(t, new Vector3(-1.4f, 0.3f, 0f), new Vector3(0.08f, 0.4f, 1f), siding);
            Part(t, new Vector3(0f, 0.35f, 0f), new Vector3(0.3f, 0.06f, 1f), siding);
            Light lamp = new GameObject("Boat Lantern").AddComponent<Light>();
            lamp.transform.SetParent(t, false);
            lamp.transform.localPosition = new Vector3(1.1f, 0.8f, 0f);
            lamp.type = LightType.Point;
            lamp.color = UiKit.Lantern;
            lamp.range = 6f;
            lamp.intensity = 1f;
            boat.AddComponent<BoatLanding>();
        }

        // A low plank jetty by the boat. Design doc: "Slip into wardrobes, under pews or beneath the jetty."
        private void Jetty(Vector3 at)
        {
            Cube("Jetty", at + new Vector3(0f, 0.75f, 0f), new Vector3(3.2f, 0.12f, 1.6f), wood);
            foreach (float x in new[] { -1.4f, 1.4f })
                Cube("Jetty Post", at + new Vector3(x, 0.35f, 0f), new Vector3(0.15f, 0.7f, 0.15f), wood);
            HideTrigger(at + Vector3.up, new Vector3(3.2f, 2f, 3f), at + new Vector3(0f, 1f, 1.6f), "jetty", "hide:jetty");
        }

        private void BuildLighting()
        {
            Light moon = new GameObject("Moonlight").AddComponent<Light>();
            moon.type = LightType.Directional;
            moon.transform.rotation = Quaternion.Euler(35f, -30f, 0f);
            moon.color = new Color(0.6f, 0.7f, 0.85f);
            moon.intensity = 0.35f;
            moon.shadows = LightShadows.Soft;
            RenderSettings.ambientLight = new Color(0.08f, 0.1f, 0.12f) * GameSettings.Brightness;
            RenderSettings.fog = true;
            RenderSettings.fogColor = UiKit.Night;
            RenderSettings.fogDensity = 0.05f;
        }

        // ---------- The Sexton's route and the NavMesh ----------

        // District streets plus evidence sites, plus (Night 4 on) every place Nell has taken evidence from.
        // Design doc, Night 4: "His routes follow the evidence you took."
        private void BuildPatrolRoute()
        {
            foreach (Vector3[] district in new[] { MillrowPatrol, ChapelPatrol, DamPatrol })
                foreach (Vector3 p in district)
                    if (Dry(p)) PatrolPoints.Add(p);
            if (night >= 4)
                foreach (Vector3 site in Campaign.GuardedSites)
                    if (Dry(site)) PatrolPoints.Add(site);
            PatrolPoints.RemoveAll(p => !Dry(p));

            // Walk them as a loop around the middle of the open town.
            Vector3 middle = Vector3.zero;
            foreach (Vector3 p in PatrolPoints) middle += p;
            middle /= Mathf.Max(1, PatrolPoints.Count);
            PatrolPoints.Sort((a, b) => Mathf.Atan2(a.z - middle.z, a.x - middle.x).CompareTo(Mathf.Atan2(b.z - middle.z, b.x - middle.x)));
        }

        // Runtime bake over tonight's dry, open streets only, so the Sexton never wades into the lake.
        private void BakeNavMesh()
        {
            float south = Mathf.Max(TownLayout.Shoreline(night), TownLayout.MillrowStart) + 0.3f;
            float north = TownLayout.NorthLimit(night) - 1f;
            var bounds = new Bounds(new Vector3(0f, 1f, (south + north) * 0.5f), new Vector3(32f, 8f, north - south));
            // Everything was built this frame: push the new transforms into physics before reading colliders.
            Physics.SyncTransforms();
            var sources = new List<NavMeshBuildSource>();
            NavMeshBuilder.CollectSources(bounds, ~(1 << 2), NavMeshCollectGeometry.PhysicsColliders, 0, new List<NavMeshBuildMarkup>(), sources);
            NavMeshBuildSettings settings = NavMesh.GetSettingsByID(0);
            settings.agentRadius = 0.4f;
            // Bake around the bounds' own centre: bounds that don't contain the data's origin come out empty
            // (that hit Nights 4 and 5, which start north of z = 0).
            NavMeshData data = NavMeshBuilder.BuildNavMeshData(settings, sources, new Bounds(Vector3.zero, bounds.size), bounds.center, Quaternion.identity);
            NavMesh.RemoveAllNavMeshData();
            NavMesh.AddNavMeshData(data);
        }

        // ---------- Small builders ----------

        private void Wardrobe(Vector3 position, Vector3 exitDirection, string id)
        {
            if (!Open(position)) return;
            float yaw = Mathf.Atan2(exitDirection.x, exitDirection.z) * Mathf.Rad2Deg;
            HushwaterAssets.Model("GothicCabinet_01", new Vector3(position.x, 0f, position.z), yaw, 2.05f);
            bool alongX = Mathf.Abs(exitDirection.x) > 0.5f;
            HideTrigger(position, alongX ? new Vector3(3f, 2f, 2.4f) : new Vector3(2.4f, 2f, 3f), position + exitDirection * 1.3f, "wardrobe", id);
        }

        private void Pew(Vector3 position, string hideId)
        {
            Cube("Pew", position + new Vector3(0f, 0.25f, 0f), new Vector3(5f, 0.5f, 0.6f), wood);
            Cube("Pew Back", position + new Vector3(0f, 0.7f, 0.3f), new Vector3(5f, 0.6f, 0.08f), wood);
            if (hideId != null)
                HideTrigger(position + Vector3.up, new Vector3(5f, 2f, 1.8f), position + new Vector3(2.9f, 1f, -0.8f), "pew", hideId);
        }

        private static void HideTrigger(Vector3 position, Vector3 size, Vector3 exit, string noun, string id)
        {
            GameObject spotObject = new GameObject($"Hide ({noun})");
            spotObject.transform.position = position;
            spotObject.layer = 2;
            BoxCollider trigger = spotObject.AddComponent<BoxCollider>();
            trigger.isTrigger = true;
            trigger.size = size;
            Transform exitPoint = new GameObject("Exit").transform;
            exitPoint.SetParent(spotObject.transform);
            exitPoint.position = exit;
            spotObject.AddComponent<HideSpot>().Setup(exitPoint, noun, id);
        }

        private void Mud(Vector3 centre, float radius, bool siltHands)
        {
            if (!Dry(centre)) return;
            GameObject pool = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            pool.name = siltHands ? "Deep Mud (something moves)" : "Deep Mud";
            Object.DestroyImmediate(pool.GetComponent<Collider>());
            pool.transform.position = new Vector3(centre.x, 0.01f, centre.z);
            pool.transform.localScale = new Vector3(radius * 2f, 0.01f, radius * 2f);
            pool.GetComponent<Renderer>().sharedMaterial = mud;
            MudTracks.AddPool(centre, radius);
            // Design doc: Silt Hands rise "from deep mud in Millrow and the graveyard". From Night 2 on (ours).
            if (siltHands && night >= 2) SiltHands.AddPool(centre, radius);
        }

        private void Congregation(Vector3 position, float yaw)
        {
            if (Dry(position)) Congregant.Build(position, yaw);
        }

        private void Moths(Vector3 nest)
        {
            if (Dry(nest)) MothSwarm.Build(nest);
        }

        private void Cache(Vector3 position, float yaw)
        {
            if (!Dry(position)) return;
            SupplyCache.Attach(HushwaterAssets.Model("wooden_crate_01", position, yaw, 0.8f));
        }

        private void StreetLamp(Vector3 position, float yaw)
        {
            if (!Dry(position)) return;
            HushwaterAssets.Model("street_lamp_01", position, yaw, 3.8f);
            Light light = new GameObject("Lamp Light").AddComponent<Light>();
            light.transform.position = position + Vector3.up * 3.5f;
            light.type = LightType.Point;
            light.color = new Color(1f, 0.7f, 0.4f);
            light.range = 9f;
            light.intensity = 1.1f;
        }

        // A tool, salvage or flash bulbs lying in the street. Underwater ones are lost.
        private void Item(Vector3 position, Pickup.Kind kind)
        {
            if (!Dry(position)) return;
            PrimitiveType shape;
            Vector3 scale;
            Color color;
            switch (kind)
            {
                case Pickup.Kind.Bottle: shape = PrimitiveType.Cylinder; scale = new Vector3(0.12f, 0.15f, 0.12f); color = new Color(0.1f, 0.35f, 0.2f); break;
                case Pickup.Kind.Handbell: shape = PrimitiveType.Sphere; scale = Vector3.one * 0.18f; color = new Color(0.7f, 0.55f, 0.25f); break;
                case Pickup.Kind.Salts: shape = PrimitiveType.Cylinder; scale = new Vector3(0.08f, 0.1f, 0.08f); color = new Color(0.85f, 0.88f, 0.9f); break;
                case Pickup.Kind.Wraps: shape = PrimitiveType.Cube; scale = new Vector3(0.3f, 0.08f, 0.2f); color = new Color(0.3f, 0.28f, 0.18f); break;
                case Pickup.Kind.LampOil: shape = PrimitiveType.Cylinder; scale = new Vector3(0.14f, 0.16f, 0.14f); color = new Color(0.55f, 0.4f, 0.15f); break;
                case Pickup.Kind.FlashBulbs: shape = PrimitiveType.Sphere; scale = Vector3.one * 0.12f; color = new Color(0.9f, 0.95f, 1f); break;
                case Pickup.Kind.Brass: shape = PrimitiveType.Cylinder; scale = new Vector3(0.14f, 0.04f, 0.14f); color = new Color(0.75f, 0.6f, 0.25f); break;
                case Pickup.Kind.Wire: shape = PrimitiveType.Capsule; scale = new Vector3(0.1f, 0.08f, 0.1f); color = new Color(0.72f, 0.4f, 0.2f); break;
                default: shape = PrimitiveType.Cube; scale = new Vector3(0.12f, 0.12f, 0.02f); color = new Color(0.7f, 0.85f, 0.9f); break;
            }
            GameObject item = GameObject.CreatePrimitive(shape);
            item.name = Pickup.Name(kind);
            Object.DestroyImmediate(item.GetComponent<Collider>());
            item.transform.position = position + Vector3.up * (scale.y + 0.05f);
            item.transform.localScale = scale;
            item.GetComponent<Renderer>().material.color = color;
            Pickup.Create(item, kind, Pickup.Name(kind));
        }

        private static void Glint(Transform parent, Vector3 at, float intensity)
        {
            Light glint = new GameObject("Glint").AddComponent<Light>();
            glint.transform.SetParent(parent, true);
            glint.transform.position = at;
            glint.color = new Color(0.8f, 0.85f, 1f);
            glint.range = 1.8f;
            glint.intensity = intensity;
        }

        private GameObject Wall(string name, Vector3 position, Vector3 scale, Material material)
        {
            GameObject wall = Cube(name, position, scale, material);
            wall.AddComponent<Occluder>();
            return wall;
        }

        private static void Invisible(string name, Vector3 position, Vector3 scale)
        {
            GameObject wall = new GameObject(name);
            wall.transform.position = position;
            wall.AddComponent<BoxCollider>().size = scale;
        }

        private static GameObject Part(Transform parent, Vector3 local, Vector3 scale, Material material, PrimitiveType type = PrimitiveType.Cube)
        {
            GameObject part = GameObject.CreatePrimitive(type);
            Object.DestroyImmediate(part.GetComponent<Collider>());
            part.transform.SetParent(parent, false);
            part.transform.localPosition = local;
            part.transform.localScale = scale;
            part.GetComponent<Renderer>().sharedMaterial = material;
            return part;
        }

        private static GameObject Cube(string name, Vector3 position, Vector3 scale, Material material)
        {
            GameObject cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
            cube.name = name;
            cube.transform.position = position;
            cube.transform.localScale = scale;
            cube.GetComponent<Renderer>().sharedMaterial = material;
            return cube;
        }
    }
}
