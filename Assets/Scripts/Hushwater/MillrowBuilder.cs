using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Rendering;

namespace Hushwater
{
    public sealed class Level
    {
        public Transform Root;
        public Vector3 PlayerSpawn;
        public float PlayerSpawnYaw;
        public Vector3 SextonSpawn;
        public Vector3[] PatrolPoints;
        public readonly List<EvidenceSite> Evidence = new List<EvidenceSite>();
        public readonly List<LanternPost> Posts = new List<LanternPost>();
        public BoatExit Boat;
        public NavMeshDataInstance NavMesh;
        public ReflectionProbe Probe;
        public Light Moon;
    }

    // Builds Night 1: Millrow, the first district the drought gave back.
    public static class MillrowBuilder
    {
        private const int InteractLayer = 2; // Ignore Raycast: triggers, pickups and doors stay out of the NavMesh bake.
        private const float WaterLevel = -0.55f;

        private static Transform root;
        private static Level level;
        private static System.Random random;

        private static Material Mud => HushwaterAssets.Surface("brown_mud_02", new Color(0.58f, 0.55f, 0.48f), 0.24f, 1.3f);
        private static Material Siding => HushwaterAssets.Surface("weathered_plank_siding", new Color(0.6f, 0.62f, 0.6f), 0.22f);
        private static Material Plaster => HushwaterAssets.Surface("worn_mossy_plasterwall", new Color(0.66f, 0.68f, 0.64f), 0.18f);
        private static Material Wallpaper => HushwaterAssets.Surface("decrepit_wallpaper", new Color(0.55f, 0.56f, 0.52f), 0.15f);
        private static Material Brick => HushwaterAssets.Surface("church_bricks_03", new Color(0.62f, 0.63f, 0.62f), 0.2f);
        private static Material Slate => HushwaterAssets.Surface("roof_slates_02", new Color(0.42f, 0.44f, 0.45f), 0.12f);
        private static Material Planks => HushwaterAssets.Surface("wood_planks_dirt", new Color(0.6f, 0.58f, 0.52f), 0.3f);
        private static Material DarkWood => HushwaterAssets.Surface("dark_wooden_planks", new Color(0.55f, 0.52f, 0.48f), 0.3f);
        private static Material Rust => HushwaterAssets.Surface("rusty_metal_02", new Color(0.8f, 0.75f, 0.7f), 0.35f);
        private static Material SiltStain => HushwaterAssets.Surface("brown_mud_02", new Color(0.36f, 0.34f, 0.3f), 0.55f);

        public static float GroundHeight(float x, float z)
        {
            float h = (Mathf.PerlinNoise(x * 0.07f + 3.1f, z * 0.07f + 7.4f) - 0.5f) * 0.45f;
            h += (Mathf.PerlinNoise(x * 0.31f + 11f, z * 0.31f) - 0.5f) * 0.08f;
            if (z < -24f) h -= (-24f - z) * 0.12f;
            float bowlX = Mathf.Max(0f, Mathf.Abs(x) - 30f);
            float bowlZ = Mathf.Max(0f, z - 60f);
            h += bowlX * bowlX * 0.035f + bowlZ * bowlZ * 0.035f;
            return h;
        }

        public static Level Build(Transform parent)
        {
            root = parent;
            level = new Level { Root = parent };
            random = new System.Random(1953);

            BuildAtmosphere();
            BuildGround();
            BuildJettyAndBoat();
            BuildHouses();
            BuildChapel();
            BuildGraveyard();
            BuildStreetDressing();
            BuildLanternPosts();
            BuildBounds();
            BakeNavMesh();

            level.PlayerSpawn = new Vector3(0f, 0.4f, -40.5f);
            level.PlayerSpawnYaw = 0f;
            level.SextonSpawn = new Vector3(0f, 0.3f, 48f);
            level.PatrolPoints = new[]
            {
                new Vector3(0f, 0f, 46f), new Vector3(0f, 0f, 31f), new Vector3(-5.5f, 0f, 22f), new Vector3(5f, 0f, 10f),
                new Vector3(-5f, 0f, 4f), new Vector3(0f, 0f, -5f), new Vector3(5f, 0f, -10f), new Vector3(-1f, 0f, 14f),
                new Vector3(14f, 0f, 41f), new Vector3(4f, 0f, 36f),
            };
            return level;
        }

        // ---------- atmosphere ----------

        private static void BuildAtmosphere()
        {
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.ExponentialSquared;
            RenderSettings.fogColor = new Color(0.11f, 0.14f, 0.155f);
            RenderSettings.fogDensity = 0.026f;
            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(0.13f, 0.16f, 0.19f);
            RenderSettings.ambientEquatorColor = new Color(0.08f, 0.095f, 0.1f);
            RenderSettings.ambientGroundColor = new Color(0.035f, 0.033f, 0.03f);

            Texture sky = Resources.Load<Texture>("Hushwater/Sky/NightSky");
            if (sky != null)
            {
                Material skybox = new Material(HushwaterAssets.Shader("Skybox/Panoramic"));
                skybox.SetTexture("_MainTex", sky);
                skybox.SetFloat("_Exposure", 0.3f);
                RenderSettings.skybox = skybox;
            }

            GameObject moonObject = new GameObject("Moon");
            moonObject.transform.SetParent(root, false);
            moonObject.transform.rotation = Quaternion.Euler(32f, -38f, 0f);
            Light moon = moonObject.AddComponent<Light>();
            moon.type = LightType.Directional;
            moon.color = new Color(0.62f, 0.72f, 0.88f);
            moon.intensity = 0.55f;
            moon.shadows = LightShadows.Soft;
            moon.shadowStrength = 0.85f;
            level.Moon = moon;

            GameObject probeObject = new GameObject("Reflections");
            probeObject.transform.SetParent(root, false);
            probeObject.transform.position = new Vector3(0f, 3f, 0f);
            ReflectionProbe probe = probeObject.AddComponent<ReflectionProbe>();
            probe.mode = ReflectionProbeMode.Realtime;
            probe.refreshMode = ReflectionProbeRefreshMode.ViaScripting;
            probe.timeSlicingMode = ReflectionProbeTimeSlicingMode.NoTimeSlicing;
            probe.size = new Vector3(200f, 60f, 240f);
            probe.intensity = 0.3f;
            probe.resolution = 128;
            probe.clearFlags = ReflectionProbeClearFlags.Skybox;
            level.Probe = probe;

            AudioReverbZone reverb = probeObject.AddComponent<AudioReverbZone>();
            reverb.reverbPreset = AudioReverbPreset.Alley;
            reverb.minDistance = 200f;
            reverb.maxDistance = 400f;

            BuildMist();
        }

        private static void BuildMist()
        {
            GameObject mistObject = new GameObject("Ground Mist");
            mistObject.transform.SetParent(root, false);
            mistObject.transform.position = new Vector3(0f, 0.3f, 5f);
            ParticleSystem mist = mistObject.AddComponent<ParticleSystem>();
            mist.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            ParticleSystem.MainModule main = mist.main;
            main.loop = true;
            main.duration = 20f;
            main.startLifetime = 22f;
            main.startSpeed = 0.12f;
            main.startSize = new ParticleSystem.MinMaxCurve(5f, 10f);
            main.startColor = new Color(0.55f, 0.62f, 0.66f, 0.07f);
            main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
            main.maxParticles = 600;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.prewarm = true;
            ParticleSystem.EmissionModule emission = mist.emission;
            emission.rateOverTime = 26f;
            ParticleSystem.ShapeModule shape = mist.shape;
            shape.shapeType = ParticleSystemShapeType.Box;
            shape.scale = new Vector3(70f, 0.6f, 100f);
            ParticleSystem.ColorOverLifetimeModule colour = mist.colorOverLifetime;
            colour.enabled = true;
            Gradient fade = new Gradient();
            fade.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.3f), new GradientAlphaKey(1f, 0.7f), new GradientAlphaKey(0f, 1f) });
            colour.color = fade;
            ParticleSystemRenderer renderer = mistObject.GetComponent<ParticleSystemRenderer>();
            Material material = new Material(HushwaterAssets.Shader("Legacy Shaders/Particles/Alpha Blended"));
            material.mainTexture = SoftCircle();
            material.SetColor("_TintColor", new Color(0.5f, 0.5f, 0.5f, 0.5f));
            renderer.sharedMaterial = material;
            renderer.renderMode = ParticleSystemRenderMode.Billboard;
            mist.Play();
        }

        public static Texture2D SoftCircle()
        {
            const int size = 64;
            Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float d = Vector2.Distance(new Vector2(x, y), new Vector2(size / 2f, size / 2f)) / (size / 2f);
                float a = Mathf.Clamp01(1f - d);
                texture.SetPixel(x, y, new Color(1f, 1f, 1f, a * a * (0.7f + 0.3f * Mathf.PerlinNoise(x * 0.15f, y * 0.15f))));
            }
            texture.Apply();
            return texture;
        }

        // ---------- ground and water ----------

        private static void BuildGround()
        {
            const float minX = -70f, maxX = 70f, minZ = -64f, maxZ = 90f, step = 1.25f;
            int nx = Mathf.RoundToInt((maxX - minX) / step) + 1;
            int nz = Mathf.RoundToInt((maxZ - minZ) / step) + 1;
            var vertices = new Vector3[nx * nz];
            var uvs = new Vector2[nx * nz];
            for (int z = 0; z < nz; z++)
            for (int x = 0; x < nx; x++)
            {
                float wx = minX + x * step, wz = minZ + z * step;
                vertices[z * nx + x] = new Vector3(wx, GroundHeight(wx, wz), wz);
                uvs[z * nx + x] = new Vector2(wx, wz) / 3.5f;
            }
            var triangles = new int[(nx - 1) * (nz - 1) * 6];
            int t = 0;
            for (int z = 0; z < nz - 1; z++)
            for (int x = 0; x < nx - 1; x++)
            {
                int i = z * nx + x;
                triangles[t++] = i; triangles[t++] = i + nx; triangles[t++] = i + 1;
                triangles[t++] = i + 1; triangles[t++] = i + nx; triangles[t++] = i + nx + 1;
            }
            Mesh mesh = new Mesh { name = "Reservoir Bed", indexFormat = IndexFormat.UInt32 };
            mesh.vertices = vertices;
            mesh.uv = uvs;
            mesh.triangles = triangles;
            mesh.RecalculateNormals();
            mesh.RecalculateTangents();
            mesh.RecalculateBounds();
            GameObject ground = MeshKit.Solid("Reservoir Bed (mud)", root, mesh, Mud, Vector3.zero, Quaternion.identity, false);
            ground.AddComponent<MeshCollider>().sharedMesh = mesh;
            ground.AddComponent<MudSurface>();

            Material water = new Material(HushwaterAssets.Shader("Hushwater/Water"));
            water.SetTexture("_NormalTex", HushwaterAssets.Texture("brown_mud_02_nor_gl"));
            GameObject lake = GameObject.CreatePrimitive(PrimitiveType.Plane);
            lake.name = "Lake";
            HushwaterAssets.Discard(lake.GetComponent<Collider>());
            lake.transform.SetParent(root, false);
            lake.transform.position = new Vector3(0f, WaterLevel, -90f);
            lake.transform.localScale = new Vector3(30f, 1f, 13f);
            lake.GetComponent<Renderer>().sharedMaterial = water;
            lake.GetComponent<Renderer>().shadowCastingMode = ShadowCastingMode.Off;

            // Deep mud pools: wetter, darker and louder underfoot.
            AddMudPool(new Vector3(-3f, 0f, -2f), new Vector3(4.2f, 1.5f, 3.4f), 12f);
            AddMudPool(new Vector3(3.2f, 0f, 16f), new Vector3(3.6f, 1.5f, 4.6f), -20f);
            AddMudPool(new Vector3(15f, 0f, 45f), new Vector3(3.2f, 1.5f, 3.2f), 30f);
        }

        private static void AddMudPool(Vector3 centre, Vector3 size, float yaw)
        {
            Material wet = HushwaterAssets.Surface("brown_mud_02", new Color(0.16f, 0.15f, 0.13f), 0.5f, 0.5f);
            float y = GroundHeight(centre.x, centre.z);
            for (int i = 0; i < 3; i++)
            {
                GameObject disc = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                disc.name = "Mud pool";
                HushwaterAssets.Discard(disc.GetComponent<Collider>());
                disc.transform.SetParent(root, false);
                Vector3 offset = Quaternion.Euler(0f, yaw + i * 120f, 0f) * Vector3.forward * (i == 0 ? 0f : size.x * 0.22f);
                disc.transform.position = new Vector3(centre.x + offset.x, y + 0.02f + i * 0.004f, centre.z + offset.z);
                disc.transform.rotation = Quaternion.Euler(0f, yaw + i * 50f, 0f);
                disc.transform.localScale = new Vector3(size.x * (i == 0 ? 0.9f : 0.6f), 0.02f, size.z * (i == 0 ? 0.9f : 0.55f));
                disc.GetComponent<Renderer>().sharedMaterial = wet;
            }
            GameObject pool = new GameObject("Deep mud (trigger)");
            pool.transform.SetParent(root, false);
            pool.transform.position = new Vector3(centre.x, GroundHeight(centre.x, centre.z) + 0.5f, centre.z);
            pool.layer = InteractLayer;
            BoxCollider box = pool.AddComponent<BoxCollider>();
            box.isTrigger = true;
            box.size = size;
            pool.AddComponent<MudSurface>().Deep = true;
        }

        // ---------- jetty and boat ----------

        private static void BuildJettyAndBoat()
        {
            Transform jetty = Group("Jetty");
            const float deck = 0.35f;
            AddBox("Jetty deck", new Vector3(0f, deck - 0.06f, -34.5f), new Vector3(2.2f, 0.12f, 21f), Planks, 2f, 0f, true, jetty);
            for (float z = -25f; z >= -44f; z -= 2.6f)
            {
                foreach (float x in new[] { -1.05f, 1.05f })
                    AddBox("Jetty post", new Vector3(x, deck - 1.6f, z), new Vector3(0.18f, 3.4f, 0.18f), DarkWood, 1f, (float)random.NextDouble() * 8f, true, jetty);
            }
            AddBox("Mooring post", new Vector3(1.05f, deck + 0.4f, -43.6f), new Vector3(0.2f, 0.9f, 0.2f), DarkWood, 1f, 0f, true, jetty);
            Invisible(new Vector3(-1.2f, 1f, -36f), new Vector3(0.1f, 2f, 18f));
            Invisible(new Vector3(1.2f, 1f, -37f), new Vector3(0.1f, 2f, 14f));
            Invisible(new Vector3(0f, 1f, -45.2f), new Vector3(2.6f, 2f, 0.1f));

            // A plank rowboat tied off the end of the jetty: the only safe place.
            Transform boat = Group("Rowboat");
            boat.position = new Vector3(2.15f, WaterLevel + 0.05f, -42f);
            boat.rotation = Quaternion.Euler(0f, 4f, 0f);
            Material hull = HushwaterAssets.Surface("weathered_plank_siding", new Color(0.42f, 0.4f, 0.36f), 0.35f);
            LocalBox(boat, "Keel", new Vector3(0f, 0.05f, 0f), new Vector3(0.9f, 0.1f, 3.2f), hull, 0f);
            for (int side = -1; side <= 1; side += 2)
            {
                LocalBox(boat, "Strake", new Vector3(side * 0.52f, 0.28f, 0f), new Vector3(0.06f, 0.45f, 3.0f), hull, 0f, side * 14f);
                LocalBox(boat, "Bow plank", new Vector3(side * 0.27f, 0.28f, 1.78f), new Vector3(0.06f, 0.45f, 0.75f), hull, side * -42f, side * 10f);
                LocalBox(boat, "Stern", new Vector3(side * 0.27f, 0.28f, -1.55f), new Vector3(0.06f, 0.45f, 0.5f), hull, side * 25f, side * 10f);
            }
            LocalBox(boat, "Transom", new Vector3(0f, 0.28f, -1.72f), new Vector3(0.75f, 0.45f, 0.06f), hull, 0f);
            LocalBox(boat, "Seat", new Vector3(0f, 0.38f, 0.2f), new Vector3(1.0f, 0.05f, 0.3f), DarkWood, 0f);
            LocalBox(boat, "Seat", new Vector3(0f, 0.38f, -1.1f), new Vector3(0.8f, 0.05f, 0.28f), DarkWood, 0f);
            LocalBox(boat, "Oar", new Vector3(0.75f, 0.45f, 0.1f), new Vector3(0.05f, 0.05f, 2.4f), DarkWood, 12f, 0f);
            LocalBox(boat, "Oar", new Vector3(-0.75f, 0.45f, 0.1f), new Vector3(0.05f, 0.05f, 2.4f), DarkWood, -12f, 0f);
            GameObject lamp = HushwaterAssets.Model("vintage_oil_lamp", boat, boat.TransformPoint(new Vector3(0f, 0.41f, -1.1f)), 30f, 0.35f, false);
            Light boatLight = AddLight(lamp.transform, lamp.transform.position + Vector3.up * 0.2f, new Color(1f, 0.72f, 0.38f), 0.9f, 5f, false);
            boatLight.gameObject.AddComponent<Flicker>();
            HushwaterAssets.Model("vintage_radio_transceiver", boat, boat.TransformPoint(new Vector3(0f, 0.12f, 1.2f)), 180f, 0.3f, false);

            BoxCollider boatCollider = boat.gameObject.AddComponent<BoxCollider>();
            boatCollider.center = new Vector3(0f, 0.5f, 0f);
            boatCollider.size = new Vector3(1.2f, 1f, 3.8f);
            boat.gameObject.layer = InteractLayer;
            level.Boat = boat.gameObject.AddComponent<BoatExit>();
            boat.gameObject.AddComponent<Bob>();
        }

        // ---------- houses ----------

        private static void BuildHouses()
        {
            House(new Vector3(-10f, 0f, -14f), -90f, Plaster, Wallpaper, "Varga house", false, false, h =>
            {
                Prop("dining_table", h, new Vector3(-2f, 0f, 1.8f), 10f, 0.76f);
                Prop("painted_wooden_chair_01", h, new Vector3(-1.2f, 0f, 1f), 160f, 0.95f);
                Prop("old_bed_frame", h, new Vector3(2f, 0f, 1.6f), 90f, 1.1f);
                Prop("wooden_bucket_01", h, new Vector3(-3.2f, 0f, -2.6f), 0f, 0.35f);
                AddPickup(PickupKind.Handbell, h.TransformPoint(new Vector3(-2f, 0.78f, 1.8f)));
            });
            House(new Vector3(10f, 0f, -10f), 90f, Siding, Plaster, "Hollis house", true, false, h =>
            {
                Prop("rubber_boots", h, new Vector3(-0.8f, 0f, -2.75f), 0f, 0.36f);
                Prop("vintage_suitcase", h, new Vector3(1.1f, 0f, -2.6f), 10f, 0.42f);
                Prop("painted_wooden_chair_01", h, new Vector3(2.4f, 0f, 1.6f), -20f, 0.95f);
                Evidence(h, new Vector3(0f, 0f, -2.6f), "Shoes by the Door",
                    "Two pairs of boots, still at the door. A packed suitcase, never carried out. The register says the Hollis family left on the 14th.",
                    false, new Vector3(0f, 0.3f, 0f));
            });
            House(new Vector3(-10f, 0f, 4f), -90f, Siding, Wallpaper, "Abbot house", true, false, h =>
            {
                Prop("dining_table", h, new Vector3(0.4f, 0f, 1f), 0f, 0.76f);
                Prop("painted_wooden_chair_01", h, new Vector3(-0.6f, 0f, 1f), 90f, 0.95f);
                Prop("painted_wooden_chair_01", h, new Vector3(1.4f, 0f, 1f), -90f, 0.95f);
                Prop("painted_wooden_chair_01", h, new Vector3(0.4f, 0f, 2.1f), 180f, 0.95f);
                Prop("tea_set_01", h, new Vector3(0.4f, 0.77f, 1f), 30f, 0.11f, false);
                Prop("vintage_oil_lamp", h, new Vector3(1.0f, 0.77f, 0.7f), 0f, 0.35f, false);
                Prop("Barrel_01", h, new Vector3(-3.2f, 0f, 2.6f), 0f, 0.9f);
                Evidence(h, new Vector3(0.4f, 0.77f, 1f), "The Set Table",
                    "Supper laid for four. Cups still on their saucers under the silt. Nobody packed. Nobody left.",
                    true, new Vector3(0f, 0.15f, 0f));
            });
            House(new Vector3(10f, 0f, 10f), 90f, Plaster, Wallpaper, "Crane house (barred)", false, true, h =>
            {
                Prop("vintage_grandfather_clock_01", h, new Vector3(2.6f, 0f, 2.9f), 180f, 2.1f);
                Prop("painted_wooden_chair_01", h, new Vector3(-1.5f, 0f, 1.2f), 30f, 0.95f);
                Evidence(h, new Vector3(2.6f, 1.6f, 2.7f), "The Stopped Clock",
                    "Abel Crane's clock stopped at 2:40. The floodgates opened at 2:10. Somebody barred this door from outside.",
                    true, Vector3.zero, false);
            });
            House(new Vector3(-10f, 0f, 22f), -90f, Siding, Plaster, "Ruin", false, false, h =>
            {
                Prop("old_bed_frame", h, new Vector3(1.5f, 0f, 1.5f), 70f, 1.1f);
                Prop("wooden_crate_01", h, new Vector3(-2.5f, 0f, 2f), 20f, 0.6f);
                AddPickup(PickupKind.Salts, h.TransformPoint(new Vector3(-2.5f, 0.62f, 2f)));
            }, true);
            House(new Vector3(10f, 0f, 26f), 90f, Siding, Wallpaper, "Mercer house", true, false, h =>
            {
                Prop("wooden_crate_01", h, new Vector3(-2.6f, 0f, 2.2f), 10f, 0.6f);
                Prop("wooden_crate_01", h, new Vector3(-2.5f, 0.6f, 2.3f), 35f, 0.55f);
                Prop("dining_table", h, new Vector3(1f, 0f, 1.5f), 80f, 0.76f);
                Prop("wine_bottles_01", h, new Vector3(1f, 0.77f, 1.5f), 80f, 0.3f, false);
                AddPickup(PickupKind.Bottle, h.TransformPoint(new Vector3(0.6f, 0.78f, 1.0f)));
                AddPickup(PickupKind.Bottle, h.TransformPoint(new Vector3(-2.2f, 0.05f, -2.4f)));
            });
        }

        private delegate void Furnish(Transform house);

        private static void House(Vector3 position, float yaw, Material outside, Material inside, string name, bool wardrobe, bool barred, Furnish furnish, bool ruin = false)
        {
            const float w = 8f, d = 7f, height = 3.1f, wall = 0.22f, floor = 0.15f;
            Transform h = Group(name);
            float baseY = GroundHeight(position.x, position.z);
            h.SetPositionAndRotation(new Vector3(position.x, baseY + floor, position.z), Quaternion.Euler(0f, yaw, 0f));

            LocalBox(h, "Foundation", new Vector3(0f, -0.5f, 0f), new Vector3(w + 0.3f, 1f, d + 0.3f), Brick, 0f);
            LocalBox(h, "Floor", new Vector3(0f, 0.02f, 0f), new Vector3(w - 0.2f, 0.06f, d - 0.2f), Planks, 0f);
            // Front step down to the street.
            LocalBox(h, "Step", new Vector3(0f, -0.12f, -d / 2f - 0.4f), new Vector3(1.6f, 0.3f, 0.6f), Brick, 0f);

            float wallHeight = ruin ? 1.9f : height;
            var door = new Opening(0f, 1.3f, 0f, 2.25f);
            var windowA = new Opening(-2.4f, 1.0f, 1.0f, 2.1f);
            var windowB = new Opening(2.4f, 1.0f, 1.0f, 2.1f);
            var sideWindow = new Opening(0.5f, 1.1f, 1.0f, 2.1f);

            Wall(h, outside, inside, new Vector3(0f, 0f, -d / 2f), 0f, w, wallHeight, wall, door, windowA, windowB);
            Wall(h, outside, inside, new Vector3(0f, 0f, d / 2f), 180f, w, ruin ? 1.2f : height, wall, new Opening(1.8f, 1.0f, 1.0f, 2.1f));
            Wall(h, outside, inside, new Vector3(-w / 2f, 0f, 0f), 90f, d - wall * 2f, wallHeight, wall, sideWindow);
            Wall(h, outside, inside, new Vector3(w / 2f, 0f, 0f), -90f, d - wall * 2f, ruin ? 2.4f : height, wall, sideWindow);

            if (!ruin) Roof(h, w, d, height, name.GetHashCode());
            else
            {
                Prop("dead_tree_trunk", h, new Vector3(-1.5f, 0f, -0.5f), 40f, -5f);
                Prop("rock_moss_set_01", h, new Vector3(2.5f, 0f, -2.5f), 0f, 0.5f);
            }

            Transform doorPivot = new GameObject("Door pivot").transform;
            doorPivot.SetParent(h, false);
            doorPivot.localPosition = new Vector3(0.65f, 0f, -d / 2f);
            if (barred)
            {
                doorPivot.gameObject.layer = InteractLayer;
                GameObject leaf = LocalBox(doorPivot, "Barred door", new Vector3(-0.65f, 1.12f, 0f), new Vector3(1.28f, 2.24f, 0.08f), DarkWood, 0f);
                leaf.layer = InteractLayer;
                NavMeshObstacle obstacle = leaf.AddComponent<NavMeshObstacle>();
                obstacle.carving = true;
                obstacle.size = new Vector3(1.4f, 2.2f, 0.4f);
                Transform bars = new GameObject("Bars").transform;
                bars.SetParent(h, false);
                GameObject a = LocalBox(bars, "Bar", new Vector3(0f, 1.3f, -d / 2f - 0.08f), new Vector3(1.7f, 0.14f, 0.05f), Planks, 0f, 0f, 24f);
                GameObject b = LocalBox(bars, "Bar", new Vector3(0f, 0.8f, -d / 2f - 0.08f), new Vector3(1.7f, 0.14f, 0.05f), Planks, 0f, 0f, -18f);
                a.layer = b.layer = InteractLayer;
                BarredDoor barredDoor = leaf.AddComponent<BarredDoor>();
                barredDoor.Leaf = doorPivot;
                barredDoor.Bars = bars.gameObject;
            }
            else if (!ruin)
            {
                GameObject leaf = LocalBox(doorPivot, "Door", new Vector3(-0.65f, 1.12f, 0f), new Vector3(1.28f, 2.24f, 0.06f), DarkWood, 0f);
                HushwaterAssets.Discard(leaf.GetComponent<Collider>());
                doorPivot.localRotation = Quaternion.Euler(0f, -100f, 0f);
            }

            if (wardrobe) Wardrobe(h, new Vector3(-w / 2f + 0.55f, 0f, 2.2f), 90f, "GothicCabinet_01");

            // The silt line: everything below 1 m is stained darker.
            Wall(h, SiltStain, null, new Vector3(0f, 0f, -d / 2f - 0.025f), 0f, w + 0.05f, 0.9f, 0.04f, door);

            furnish(h);
        }

        private readonly struct Opening
        {
            public readonly float Centre, Width, Bottom, Top;
            public Opening(float centre, float width, float bottom, float top) { Centre = centre; Width = width; Bottom = bottom; Top = top; }
        }

        // A wall along local X centred on `centre`, with rectangular holes for doors and windows.
        private static void Wall(Transform parent, Material outside, Material inside, Vector3 centre, float yaw, float length, float height, float thickness, params Opening[] openings)
        {
            Transform w = new GameObject("Wall").transform;
            w.SetParent(parent, false);
            w.localPosition = centre;
            w.localRotation = Quaternion.Euler(0f, yaw, 0f);
            var sorted = new List<Opening>(openings);
            sorted.Sort((a, b) => a.Centre.CompareTo(b.Centre));
            float cursor = -length / 2f;
            void Piece(float x0, float x1, float y0, float y1)
            {
                if (x1 - x0 < 0.01f || y1 - y0 < 0.01f) return;
                float cx = (x0 + x1) / 2f, cy = (y0 + y1) / 2f;
                if (inside == null)
                {
                    LocalBox(w, "Wall piece", new Vector3(cx, cy, 0f), new Vector3(x1 - x0, y1 - y0, thickness), outside, 0f);
                    return;
                }
                // Outer skin and inner skin, so interiors get wallpaper or plaster.
                LocalBox(w, "Wall outer", new Vector3(cx, cy, -thickness * 0.25f), new Vector3(x1 - x0, y1 - y0, thickness * 0.5f), outside, 0f);
                LocalBox(w, "Wall inner", new Vector3(cx, cy, thickness * 0.25f), new Vector3(x1 - x0, y1 - y0, thickness * 0.5f), inside, 0f);
            }
            foreach (Opening o in sorted)
            {
                float left = o.Centre - o.Width / 2f, right = o.Centre + o.Width / 2f;
                Piece(cursor, left, 0f, height);
                Piece(left, right, 0f, Mathf.Min(o.Bottom, height));
                Piece(left, right, Mathf.Min(o.Top, height), height);
                cursor = right;
            }
            Piece(cursor, length / 2f, 0f, height);
        }

        private static void Roof(Transform h, float w, float d, float height, int seed)
        {
            const float rise = 1.7f, overhang = 0.35f;
            float half = d / 2f + overhang;
            float slope = Mathf.Sqrt(half * half + rise * rise);
            float angle = Mathf.Atan2(rise, half) * Mathf.Rad2Deg;
            bool holed = (seed & 1) == 0;
            for (int side = -1; side <= 1; side += 2)
            {
                float z = side * half / 2f;
                float y = height + rise / 2f;
                if (holed && side == 1)
                {
                    // A collapsed section lets the moon in.
                    LocalBox(h, "Roof", new Vector3(-w / 2f + 1.6f, y, z), new Vector3(3.6f, 0.12f, slope), Slate, side * angle, 0f, 0f, 0f, true);
                    LocalBox(h, "Roof", new Vector3(w / 2f - 1.2f, y, z), new Vector3(2.8f, 0.12f, slope), Slate, side * angle, 0f, 0f, 0f, true);
                }
                else LocalBox(h, "Roof", new Vector3(0f, y, z), new Vector3(w + overhang * 2f, 0.12f, slope), Slate, side * angle, 0f, 0f, 0f, true);
            }
            // Gable ends, stepped from boards.
            for (int end = -1; end <= 1; end += 2)
            {
                for (int i = 0; i < 6; i++)
                {
                    float t = i / 6f;
                    float span = d * (1f - t) - 0.1f;
                    LocalBox(h, "Gable", new Vector3(end * (w / 2f - 0.11f), height + rise * (t + 1f / 12f), 0f), new Vector3(0.2f, rise / 6f + 0.01f, span), Siding, 0f);
                }
            }
        }

        private static void Wardrobe(Transform parent, Vector3 local, float yaw, string model)
        {
            Vector3 world = parent.TransformPoint(local);
            float worldYaw = parent.eulerAngles.y + yaw;
            GameObject cabinet = HushwaterAssets.Model(model, parent, world, worldYaw, 2.05f);
            HideSpot spot = cabinet.AddComponent<HideSpot>();
            Transform exit = new GameObject("Exit").transform;
            exit.SetParent(parent, false);
            exit.position = world + Quaternion.Euler(0f, worldYaw, 0f) * Vector3.forward * 1.1f + Vector3.up * 0.9f;
            Transform view = new GameObject("Inside").transform;
            view.SetParent(parent, false);
            view.position = world + Vector3.up * 1.45f;
            view.rotation = Quaternion.Euler(0f, worldYaw, 0f);
            spot.Setup(exit, view);
            spot.Label = model == "GothicCabinet_01" ? "wardrobe" : "cabinet";
        }

        // ---------- chapel and bell tower ----------

        private static void BuildChapel()
        {
            const float w = 11f, d = 18f, height = 6.2f, wall = 0.5f;
            Transform c = Group("Chapel of St. Brendan");
            float baseY = GroundHeight(0f, 44f) + 0.2f;
            c.SetPositionAndRotation(new Vector3(0f, baseY, 44f), Quaternion.identity);
            LocalBox(c, "Plinth", new Vector3(0f, -0.6f, 0f), new Vector3(w + 0.6f, 1.2f, d + 0.6f), Brick, 0f);
            LocalBox(c, "Stone floor", new Vector3(0f, 0.02f, 0f), new Vector3(w - 0.4f, 0.06f, d - 0.4f), HushwaterAssets.Surface("church_bricks_03", new Color(0.45f, 0.45f, 0.44f), 0.45f), 0f);
            LocalBox(c, "Steps", new Vector3(0f, -0.15f, -d / 2f - 0.6f), new Vector3(3.4f, 0.4f, 1.2f), Brick, 0f);

            var tall = new[] { new Opening(-5f, 1.2f, 1.9f, 4.9f), new Opening(0f, 1.2f, 1.9f, 4.9f), new Opening(5f, 1.2f, 1.9f, 4.9f) };
            Wall(c, Brick, null, new Vector3(0f, 0f, -d / 2f), 0f, w, height, wall, new Opening(0f, 2.4f, 0f, 3.5f), new Opening(-3.4f, 0.9f, 2.4f, 4.6f), new Opening(3.4f, 0.9f, 2.4f, 4.6f));
            Wall(c, Brick, null, new Vector3(0f, 0f, d / 2f), 180f, w, height, wall, new Opening(0f, 1.6f, 2.2f, 5.2f));
            Wall(c, Brick, null, new Vector3(-w / 2f, 0f, 0f), 90f, d - wall * 2f, height, wall, tall);
            Wall(c, Brick, null, new Vector3(w / 2f, 0f, 0f), -90f, d - wall * 2f, height, wall, tall);
            Wall(c, SiltStain, null, new Vector3(0f, 0f, -d / 2f - 0.27f), 0f, w + 0.05f, 1.2f, 0.04f, new Opening(0f, 2.4f, 0f, 3.5f));

            // Roof ridge runs north-south.
            const float rise = 3f;
            float half = w / 2f + 0.4f;
            float slope = Mathf.Sqrt(half * half + rise * rise);
            float angle = Mathf.Atan2(rise, half) * Mathf.Rad2Deg;
            for (int side = -1; side <= 1; side += 2)
                LocalBox(c, "Chapel roof", new Vector3(side * half / 2f, height + rise / 2f, 0f), new Vector3(slope, 0.16f, d + 0.8f), Slate, 0f, 0f, -side * angle);
            for (int end = -1; end <= 1; end += 2)
                for (int i = 0; i < 8; i++)
                {
                    float t = i / 8f;
                    LocalBox(c, "Gable", new Vector3(0f, height + rise * (t + 1f / 16f), end * (d / 2f - 0.25f)), new Vector3(w * (1f - t) - 0.2f, rise / 8f + 0.01f, wall), Brick, 0f);
                }

            // Pews: names carved into the wood.
            for (int row = 0; row < 5; row++)
            for (int side = -1; side <= 1; side += 2)
            {
                Vector3 local = new Vector3(side * 2.4f, 0f, -4.5f + row * 2.1f);
                GameObject pew = HushwaterAssets.Model("painted_wooden_bench", c, c.TransformPoint(local), 180f + (float)(random.NextDouble() - 0.5) * 8f, 0.95f);
                pew.transform.localScale = new Vector3(pew.transform.localScale.x * 1.7f, pew.transform.localScale.y, pew.transform.localScale.z);
            }
            Evidence(c, new Vector3(-2.4f, 0.5f, 3.9f), "Names in the Pew",
                "Forty-one names cut into the back of the pew with a knife, under a date: the flood night. The last one is still unfinished.",
                true, new Vector3(0f, 0.2f, 0f));

            LocalBox(c, "Altar", new Vector3(0f, 0.5f, 7.2f), new Vector3(2.6f, 1f, 1f), Brick, 0f);
            for (int side = -1; side <= 1; side += 2)
            {
                GameObject lamp = HushwaterAssets.Model("vintage_oil_lamp", c, c.TransformPoint(new Vector3(side * 1f, 1f, 7.2f)), 0f, 0.4f, false);
                AddLight(c, lamp.transform.position + Vector3.up * 0.3f, new Color(1f, 0.6f, 0.3f), 0.7f, 6f, false).gameObject.AddComponent<Flicker>();
            }
            Wardrobe(c, new Vector3(4.5f, 0f, 6.8f), -90f, "painted_wooden_cabinet");

            BuildBellTower(c.TransformPoint(new Vector3(-w / 2f - 2.1f, 0f, -d / 2f + 2.2f)));
        }

        private static void BuildBellTower(Vector3 basePosition)
        {
            Transform tower = Group("Bell Tower");
            tower.position = basePosition;
            const float size = 3.8f, shaft = 11f;
            AddBox("Tower shaft", basePosition + Vector3.up * (shaft / 2f - 0.6f), new Vector3(size, shaft, size), Brick, 3f, 0f, true, tower);
            AddBox("Belfry floor", basePosition + Vector3.up * (shaft - 0.5f), new Vector3(size + 0.3f, 0.3f, size + 0.3f), Brick, 3f, 0f, true, tower);
            for (int i = 0; i < 4; i++)
            {
                Vector3 corner = new Vector3((i & 1) == 0 ? -1 : 1, 0f, (i & 2) == 0 ? -1 : 1) * (size / 2f - 0.25f);
                AddBox("Belfry pier", basePosition + corner + Vector3.up * (shaft + 1.1f), new Vector3(0.5f, 3f, 0.5f), Brick, 3f, 0f, true, tower);
            }
            AddBox("Belfry cap", basePosition + Vector3.up * (shaft + 2.7f), new Vector3(size + 0.4f, 0.35f, size + 0.4f), Brick, 3f, 0f, true, tower);
            Mesh spire = MeshKit.Lathe(new[] { new Vector2(size * 0.75f, 0f), new Vector2(0.05f, 4.2f) }, 4);
            MeshKit.Solid("Spire", tower, spire, Slate, basePosition + Vector3.up * (shaft + 2.85f), Quaternion.Euler(0f, 45f, 0f), false);

            Mesh bell = MeshKit.Lathe(new[]
            {
                new Vector2(0.78f, 0f), new Vector2(0.74f, 0.08f), new Vector2(0.6f, 0.3f), new Vector2(0.5f, 0.7f),
                new Vector2(0.47f, 1.0f), new Vector2(0.38f, 1.15f), new Vector2(0.0f, 1.2f)
            }, 24);
            Material bronze = HushwaterAssets.Surface("rusty_metal_02", new Color(0.35f, 0.42f, 0.36f), 0.5f);
            GameObject bellObject = MeshKit.Solid("Chapel bell", tower, bell, bronze, basePosition + Vector3.up * (shaft + 0.6f), Quaternion.identity, false);
            bellObject.AddComponent<Bob>().Swing = true;
        }

        // ---------- graveyard ----------

        private static void BuildGraveyard()
        {
            Transform g = Group("Graveyard");
            Material stone = HushwaterAssets.Surface("church_bricks_03", new Color(0.55f, 0.56f, 0.55f), 0.3f);
            for (int i = 0; i < 22; i++)
            {
                float x = 10.5f + (i % 5) * 2.4f + (float)(random.NextDouble() - 0.5);
                float z = 35f + (i / 5) * 3.6f + (float)(random.NextDouble() - 0.5);
                float y = GroundHeight(x, z);
                bool cross = i % 4 == 0;
                GameObject marker = AddBox(cross ? "Cross" : "Headstone", new Vector3(x, y + 0.35f, z), cross ? new Vector3(0.14f, 1.2f, 0.14f) : new Vector3(0.62f, 0.95f, 0.14f),
                    stone, 1f, (float)(random.NextDouble() - 0.5) * 20f, true, g);
                marker.transform.rotation *= Quaternion.Euler((float)(random.NextDouble() - 0.5) * 18f, 0f, (float)(random.NextDouble() - 0.5) * 14f);
                if (cross) LocalBox(marker.transform, "Arm", new Vector3(0f, 0.22f, 0f), new Vector3(4f, 0.1f, 1f), stone, 0f);
            }
            for (float z = 33f; z <= 53f; z += 1.1f)
            {
                if (z > 41f && z < 44f) continue;
                AddBox("Fence post", new Vector3(8.6f, GroundHeight(8.6f, z) + 0.6f, z), new Vector3(0.05f, 1.3f, 0.05f), Rust, 1f, 0f, false, g)
                    .transform.rotation *= Quaternion.Euler((float)(random.NextDouble() - 0.5) * 12f, 0f, (float)(random.NextDouble() - 0.5) * 12f);
            }
            AddBox("Fence rail", new Vector3(8.6f, GroundHeight(8.6f, 37f) + 1.1f, 37f), new Vector3(0.04f, 0.04f, 8f), Rust, 1f, 0f, false, g);
            AddBox("Fence rail", new Vector3(8.6f, GroundHeight(8.6f, 48.5f) + 1.1f, 48.5f), new Vector3(0.04f, 0.04f, 9f), Rust, 1f, 0f, false, g);
            HushwaterAssets.Model("dead_tree_trunk", g, new Vector3(21f, GroundHeight(21f, 38f), 38f), 120f, -6f);
            HushwaterAssets.Model("dead_tree_trunk", g, new Vector3(13f, GroundHeight(13f, 52f), 52f), 10f, -5f);
        }

        // ---------- street ----------

        private static void BuildStreetDressing()
        {
            Transform s = Group("Millrow Lane");
            void P(string model, float x, float z, float yaw, float height, bool solid = true) =>
                HushwaterAssets.Model(model, s, new Vector3(x, GroundHeight(x, z) - 0.03f, z), yaw, height, solid);

            P("dead_tree_trunk", -6f, -20f, 30f, -6f);
            P("dead_tree_trunk", 5.8f, 1f, 200f, -5f);
            P("dead_tree_trunk", -5.5f, 31f, 15f, -6.5f);
            P("root_cluster_01", 4.8f, -18f, 0f, 0.9f);
            P("root_cluster_01", -4.2f, 12f, 140f, 0.8f);
            P("boulder_01", 6.2f, 19.5f, 20f, 1.3f);
            P("boulder_01", -14f, -4f, 90f, 1.6f);
            P("rock_moss_set_01", -3.6f, -9f, 0f, 0.45f);
            P("rock_moss_set_01", 3.2f, 33f, 70f, 0.5f);
            P("Barrel_01", 5.4f, -6f, 10f, 0.9f);
            P("Barrel_01", 6.1f, -5.2f, 60f, 0.9f);
            P("wooden_crate_01", -5.3f, -12f, 25f, 0.65f);
            P("wooden_crate_01", 5.6f, 23f, -15f, 0.65f);
            P("rusted_wheel_rim_01", -2.4f, 7f, 0f, 0.25f, false);
            P("rusted_wheel_rim_01", 2f, -16f, 70f, 0.25f, false);
            P("can_rusted", 1.2f, 3f, 0f, 0.12f, false);
            P("can_rusted", -1.8f, 25f, 40f, 0.12f, false);
            P("wooden_bucket_01", 4.6f, 8f, 0f, 0.35f);
            P("painted_wooden_chair_01", -3f, 18f, 60f, 0.95f);
            P("street_lamp_01", 4.6f, -23f, -90f, 3.8f);
            P("street_lamp_01", -4.7f, 13f, 90f, 3.8f);

            // A telegraph pole that came down in the flood.
            GameObject pole = AddBox("Fallen telegraph pole", new Vector3(-1.5f, GroundHeight(-1.5f, 28f) + 0.15f, 28f), new Vector3(0.24f, 0.24f, 9f), DarkWood, 1.5f, 68f, true, s);
            pole.transform.rotation *= Quaternion.Euler(4f, 0f, 0f);

            AddPickup(PickupKind.Bottle, new Vector3(2.4f, GroundHeight(2.4f, -27f) + 0.05f, -27f));
            AddPickup(PickupKind.Bottle, new Vector3(-6.4f, GroundHeight(-6.4f, 9f) + 0.05f, 9f));
        }

        private static void BuildLanternPosts()
        {
            var spots = new List<Vector3> { new Vector3(2.6f, 0f, 5.5f), new Vector3(2.4f, 0f, -22f), new Vector3(3.4f, 0f, 33f), new Vector3(14f, 0f, 39f) };
            int count = Mathf.Clamp(GameSettings.LanternPosts, 1, spots.Count);
            Transform posts = Group("Lantern Posts");
            for (int i = 0; i < count; i++)
            {
                Vector3 p = spots[i];
                p.y = GroundHeight(p.x, p.z);
                Transform post = new GameObject("Lantern Post " + (i + 1)).transform;
                post.SetParent(posts, false);
                post.position = p;
                LocalBox(post, "Post", new Vector3(0f, 1.15f, 0f), new Vector3(0.16f, 2.3f, 0.16f), DarkWood, 0f);
                LocalBox(post, "Arm", new Vector3(0f, 2.2f, 0.3f), new Vector3(0.08f, 0.08f, 0.7f), DarkWood, 0f);
                LocalBox(post, "Crank box", new Vector3(0.12f, 1.0f, 0f), new Vector3(0.12f, 0.25f, 0.18f), Rust, 0f);
                GameObject lantern = HushwaterAssets.Model("Lantern_01", post, post.TransformPoint(new Vector3(0f, 1.75f, 0.6f)), 0f, 0.42f, false);
                GameObject flame = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                HushwaterAssets.Discard(flame.GetComponent<Collider>());
                flame.transform.SetParent(post, false);
                flame.transform.position = lantern.transform.position + Vector3.up * 0.16f;
                flame.transform.localScale = Vector3.one * 0.07f;
                flame.GetComponent<Renderer>().sharedMaterial = HushwaterAssets.Emissive(Color.black, new Color(4f, 2.4f, 0.9f));
                Light light = AddLight(post, flame.transform.position, new Color(1f, 0.74f, 0.4f), 1.6f, 11f, true);
                Transform respawn = new GameObject("Respawn").transform;
                respawn.SetParent(post, false);
                respawn.localPosition = new Vector3(-1.2f, 1f, 0f);
                BoxCollider use = post.gameObject.AddComponent<BoxCollider>();
                use.center = new Vector3(0f, 1.2f, 0.2f);
                use.size = new Vector3(0.6f, 2.4f, 1f);
                LanternPost lp = post.gameObject.AddComponent<LanternPost>();
                lp.Setup(light, flame.GetComponent<Renderer>(), respawn);
                level.Posts.Add(lp);
            }
        }

        private static void BuildBounds()
        {
            Invisible(new Vector3(-31f, 3f, -29.5f), new Vector3(60f, 6f, 0.3f));
            Invisible(new Vector3(31.2f, 3f, -29.5f), new Vector3(60f, 6f, 0.3f));
            Invisible(new Vector3(-30f, 3f, 15f), new Vector3(0.3f, 8f, 100f));
            Invisible(new Vector3(30f, 3f, 15f), new Vector3(0.3f, 8f, 100f));
            Invisible(new Vector3(0f, 3f, 60f), new Vector3(62f, 8f, 0.3f));
        }

        private static void BakeNavMesh()
        {
            var sources = new List<NavMeshBuildSource>();
            var bounds = new Bounds(new Vector3(0f, 2f, 10f), new Vector3(70f, 24f, 110f));
            NavMeshBuilder.CollectSources(bounds, ~(1 << InteractLayer), NavMeshCollectGeometry.PhysicsColliders, 0, new List<NavMeshBuildMarkup>(), sources);
            NavMeshBuildSettings settings = NavMesh.GetSettingsByID(0);
            settings.agentRadius = 0.4f;
            settings.agentHeight = 2.0f;
            settings.agentClimb = 0.45f;
            settings.agentSlope = 40f;
            NavMeshData data = NavMeshBuilder.BuildNavMeshData(settings, sources, bounds, Vector3.zero, Quaternion.identity);
            level.NavMesh = NavMesh.AddNavMeshData(data);
        }

        // ---------- helpers ----------

        private static Transform Group(string name)
        {
            Transform t = new GameObject(name).transform;
            t.SetParent(root, false);
            return t;
        }

        private static GameObject AddBox(string name, Vector3 position, Vector3 size, Material material, float textureMetres, float yaw, bool collider, Transform parent = null)
        {
            GameObject go = MeshKit.Solid(name, parent != null ? parent : root, MeshKit.Box(size, textureMetres), material, position, Quaternion.Euler(0f, yaw, 0f), false);
            if (collider) go.AddComponent<BoxCollider>();
            return go;
        }

        private static GameObject LocalBox(Transform parent, string name, Vector3 local, Vector3 size, Material material, float pitch, float yaw = 0f, float roll = 0f, float textureMetres = 0f, bool collider = true)
        {
            if (textureMetres <= 0f) textureMetres = material == Brick ? 2.5f : material == Slate ? 2f : 2.2f;
            GameObject go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = local;
            go.transform.localRotation = Quaternion.Euler(pitch, yaw, roll);
            go.AddComponent<MeshFilter>().sharedMesh = MeshKit.Box(size, textureMetres);
            go.AddComponent<MeshRenderer>().sharedMaterial = material;
            if (collider) go.AddComponent<BoxCollider>();
            return go;
        }

        private static GameObject Prop(string model, Transform house, Vector3 local, float yaw, float height, bool solid = true) =>
            HushwaterAssets.Model(model, house, house.TransformPoint(local), house.eulerAngles.y + yaw, height, solid);

        private static void Evidence(Transform parent, Vector3 local, string title, string note, bool dark, Vector3 focus, bool required = true)
        {
            GameObject go = new GameObject("Evidence: " + title);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = local;
            EvidenceSite site = go.AddComponent<EvidenceSite>();
            site.Title = title;
            site.Note = note;
            site.Dark = dark;
            site.Required = required;
            site.FocusOffset = focus;
            level.Evidence.Add(site);
        }

        private static void AddPickup(PickupKind kind, Vector3 position)
        {
            GameObject item = Throwable.BuildVisual(kind, root, position);
            item.layer = InteractLayer;
            Collider collider = item.GetComponent<Collider>();
            collider.isTrigger = true;
            // A larger trigger so the item is easy to look at.
            if (collider is CapsuleCollider capsule) capsule.radius = 0.15f;
            if (collider is SphereCollider sphere) sphere.radius = 0.18f;
            if (collider is BoxCollider box) box.size = new Vector3(0.3f, 0.3f, 0.3f);
            item.AddComponent<Pickup>().Kind = kind;
        }

        private static Light AddLight(Transform parent, Vector3 position, Color colour, float intensity, float range, bool shadows)
        {
            GameObject go = new GameObject("Light");
            go.transform.SetParent(parent, false);
            go.transform.position = position;
            Light light = go.AddComponent<Light>();
            light.type = LightType.Point;
            light.color = colour;
            light.intensity = intensity;
            light.range = range;
            light.shadows = shadows ? LightShadows.Soft : LightShadows.None;
            return light;
        }

        private static void Invisible(Vector3 position, Vector3 size)
        {
            GameObject go = new GameObject("Invisible wall");
            go.transform.SetParent(root, false);
            go.transform.position = position;
            go.AddComponent<BoxCollider>().size = size;
            go.AddComponent<InvisibleWall>();
        }
    }

    public sealed class Flicker : MonoBehaviour
    {
        private Light target;
        private float baseIntensity;
        private float seed;

        private void Awake()
        {
            target = GetComponent<Light>();
            baseIntensity = target.intensity;
            seed = Random.value * 100f;
        }

        private void Update() => target.intensity = baseIntensity * (0.75f + 0.5f * Mathf.PerlinNoise(Time.time * 6f, seed));
    }

    // Gentle motion for the moored boat and the hanging bell.
    public sealed class Bob : MonoBehaviour
    {
        public bool Swing;
        private Vector3 origin;
        private Quaternion rotation;

        private void Awake()
        {
            origin = transform.position;
            rotation = transform.rotation;
        }

        private void Update()
        {
            float t = Time.time;
            if (Swing) transform.rotation = rotation * Quaternion.Euler(Mathf.Sin(t * 0.7f) * 2f, 0f, Mathf.Sin(t * 0.5f) * 1.5f);
            else
            {
                transform.position = origin + Vector3.up * Mathf.Sin(t * 0.9f) * 0.04f;
                transform.rotation = rotation * Quaternion.Euler(Mathf.Sin(t * 0.7f) * 1.5f, 0f, Mathf.Sin(t * 1.1f) * 2.2f);
            }
        }
    }
}
