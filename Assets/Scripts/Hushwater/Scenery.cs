using System;
using System.Collections.Generic;
using UnityEngine;
using Random = UnityEngine.Random;

namespace Hushwater
{
    // Everything that makes the valley feel like a place rather than a level: the sky, the hills around the reservoir,
    // crows, mist, dead trees, telegraph wires, wreckage. None of it is in the doc's rules; it serves "The town is the
    // witness" and "walking through a held breath". Built with a fixed seed so the town looks the same every night.
    public static class Scenery
    {
        private const int Seed = 1953;
        private static Material bark, wire, ridge, wreck, plank;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => bark = wire = ridge = wreck = plank = null;

        private static void Materials()
        {
            if (bark != null) return;
            bark = HushwaterAssets.Surface("weathered_plank_siding", new Color(0.2f, 0.19f, 0.17f), 0.5f, 0.1f);
            wreck = HushwaterAssets.Surface("weathered_plank_siding", new Color(0.3f, 0.27f, 0.22f), 1f, 0.3f);
            plank = HushwaterAssets.Surface("weathered_plank_siding", new Color(0.38f, 0.34f, 0.28f), 1f, 0.2f);
            wire = new Material(Shader.Find("Sprites/Default")) { color = new Color(0.02f, 0.025f, 0.03f, 1f) };
            ridge = new Material(Shader.Find("Sprites/Default")) { color = Color.white, renderQueue = 2450 };
        }

        // ---------- The whole valley around tonight's town ----------

        public static void BuildTown(int night)
        {
            Materials();
            Random.State saved = Random.state;
            Random.InitState(Seed);
            Physics.SyncTransforms();

            Sky(new Vector3(0f, 0f, 42f));
            Ridges(new Vector3(0f, 0f, 42f), 175f);
            TelegraphLine(night, -15.6f);
            TelegraphLine(night, 15.6f);
            DeadTrees(night);
            Debris(night);
            Crows(night);

            Random.state = saved;
        }

        // A moon, its halo, and a sky of faint stars. Sprites ignore fog, so they stay visible above it.
        public static void Sky(Vector3 centre)
        {
            Materials();
            Vector3 moonDir = Quaternion.Euler(-28f, 150f, 0f) * Vector3.forward;
            Billboard(centre + moonDir * 420f, 34f, new Color(0.86f, 0.88f, 0.82f, 1f), 2410, "Moon", true);
            Billboard(centre + moonDir * 421f, 120f, new Color(0.55f, 0.65f, 0.7f, 0.09f), 2405, "Moon Halo", false);

            GameObject stars = new GameObject("Stars");
            stars.transform.position = centre;
            ParticleSystem ps = stars.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            ParticleSystem.MainModule main = ps.main;
            main.loop = false;
            main.playOnAwake = false;
            main.maxParticles = 700;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            ParticleSystem.EmissionModule emission = ps.emission;
            emission.enabled = false;
            ParticleSystemRenderer renderer = stars.GetComponent<ParticleSystemRenderer>();
            renderer.sharedMaterial = new Material(Fx.Particle) { renderQueue = 2400 };
            var emit = new ParticleSystem.EmitParams();
            for (int i = 0; i < 650; i++)
            {
                Vector3 dir = Random.onUnitSphere;
                dir.y = Mathf.Abs(dir.y) * 0.9f + 0.06f;
                emit.position = centre + dir.normalized * 460f;
                emit.startSize = Random.Range(0.9f, 2.6f);
                emit.startLifetime = 1e6f;
                emit.startColor = new Color(0.85f, 0.9f, 1f, Random.Range(0.15f, 0.75f));
                ps.Emit(emit, 1);
            }
        }

        // The valley walls: a ring of dark hills standing against the sky.
        public static void Ridges(Vector3 centre, float radius)
        {
            Materials();
            const int segments = 96;
            var verts = new List<Vector3>();
            var colors = new List<Color>();
            var tris = new List<int>();
            // Vertex colours skip the gamma-to-linear conversion material colours get, so convert them here.
            Color top = new Color(0.022f, 0.03f, 0.034f, 1f).linear;
            Color foot = new Color(0.016f, 0.02f, 0.022f, 1f).linear;
            for (int i = 0; i <= segments; i++)
            {
                float a = i / (float)segments * Mathf.PI * 2f;
                float h = 28f + Mathf.PerlinNoise(a * 1.7f, 3.1f) * 46f + Mathf.PerlinNoise(a * 6f, 9.2f) * 12f;
                // The dam end (north) sits in a notch; the lake end (south) opens out lower.
                h *= Mathf.Lerp(0.55f, 1f, Mathf.Abs(Mathf.Cos(a * 0.5f)));
                Vector3 dir = new Vector3(Mathf.Sin(a), 0f, Mathf.Cos(a));
                verts.Add(centre + dir * radius + Vector3.down * 20f);
                verts.Add(centre + dir * radius + Vector3.up * h);
                colors.Add(foot);
                colors.Add(top);
                if (i == segments) continue;
                int b = i * 2;
                tris.AddRange(new[] { b, b + 1, b + 2, b + 1, b + 3, b + 2 });
                tris.AddRange(new[] { b, b + 2, b + 1, b + 1, b + 2, b + 3 });
            }
            var mesh = new Mesh { name = "Valley Ridges" };
            mesh.SetVertices(verts);
            mesh.SetColors(colors);
            mesh.SetTriangles(tris, 0);
            mesh.RecalculateBounds();
            GameObject hills = new GameObject("Valley Ridges");
            hills.AddComponent<MeshFilter>().sharedMesh = mesh;
            hills.AddComponent<MeshRenderer>().sharedMaterial = ridge;
            HorizonGlow(centre);
        }

        // The night sky is lighter at the horizon than overhead, so the hills stand out as silhouettes.
        private static void HorizonGlow(Vector3 centre)
        {
            const int segments = 48;
            var verts = new List<Vector3>();
            var colors = new List<Color>();
            var tris = new List<int>();
            Color horizon = new Color(0.1f, 0.12f, 0.13f, 1f).linear;
            // The top matches the camera's clear colour (the doc's Night), so there's no seam overhead.
            Color zenith = UiKit.Night.linear;
            for (int i = 0; i <= segments; i++)
            {
                float a = i / (float)segments * Mathf.PI * 2f;
                Vector3 dir = new Vector3(Mathf.Sin(a), 0f, Mathf.Cos(a));
                verts.Add(centre + dir * 470f + Vector3.down * 40f);
                verts.Add(centre + dir * 470f + Vector3.up * 30f);
                verts.Add(centre + dir * 470f + Vector3.up * 230f);
                colors.Add(horizon);
                colors.Add(horizon);
                colors.Add(zenith);
                if (i == segments) continue;
                int b = i * 3;
                for (int k = 0; k < 2; k++)
                {
                    int v = b + k;
                    tris.AddRange(new[] { v, v + 1, v + 3, v + 1, v + 4, v + 3 });
                    tris.AddRange(new[] { v, v + 3, v + 1, v + 1, v + 3, v + 4 });
                }
            }
            var mesh = new Mesh { name = "Horizon Glow" };
            mesh.SetVertices(verts);
            mesh.SetColors(colors);
            mesh.SetTriangles(tris, 0);
            mesh.RecalculateBounds();
            GameObject glow = new GameObject("Horizon Glow");
            glow.AddComponent<MeshFilter>().sharedMesh = mesh;
            glow.AddComponent<MeshRenderer>().sharedMaterial = new Material(ridge) { renderQueue = 2390 };
        }

        private static void Billboard(Vector3 at, float size, Color color, int queue, string name, bool solidDisc)
        {
            GameObject quad = GameObject.CreatePrimitive(PrimitiveType.Quad);
            quad.name = name;
            UnityEngine.Object.DestroyImmediate(quad.GetComponent<Collider>());
            quad.transform.position = at;
            quad.transform.localScale = Vector3.one * size;
            Material m = new Material(solidDisc ? MoonMaterial() : Fx.Particle) { color = color, renderQueue = queue };
            quad.GetComponent<Renderer>().sharedMaterial = m;
            quad.AddComponent<FaceCamera>();
        }

        // A hard-edged disc with faint maria, for the moon.
        private static Material MoonMaterial()
        {
            const int size = 128;
            Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(size / 2f, size / 2f)) / (size / 2f);
                    float alpha = Mathf.Clamp01((1f - d) * 30f);
                    float maria = 0.82f + 0.18f * Mathf.PerlinNoise(x * 0.06f, y * 0.06f) - 0.1f * Mathf.PerlinNoise(x * 0.15f + 5f, y * 0.15f);
                    tex.SetPixel(x, y, new Color(maria, maria, maria * 0.97f, alpha));
                }
            tex.Apply();
            return new Material(Shader.Find("Sprites/Default")) { mainTexture = tex };
        }

        // ---------- Telegraph poles along the back lanes, wires sagging between them ----------

        private static void TelegraphLine(int night, float x)
        {
            Vector3? last = null;
            for (float z = -12f; z < 98f; z += 12f)
            {
                Vector3 foot = new Vector3(x, 0f, z);
                if (!TownLayout.Open(night, foot) || z < TownLayout.Shoreline(night) || (z > 78f && z < 84f))
                {
                    last = null;
                    continue;
                }
                float lean = Random.Range(-6f, 6f);
                GameObject pole = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                pole.name = "Telegraph Pole";
                pole.transform.position = foot + Vector3.up * 3.5f;
                pole.transform.localScale = new Vector3(0.22f, 3.5f, 0.22f);
                pole.transform.rotation = Quaternion.Euler(lean, 0f, Random.Range(-4f, 4f));
                pole.GetComponent<Renderer>().sharedMaterial = bark;
                GameObject arm = GameObject.CreatePrimitive(PrimitiveType.Cube);
                UnityEngine.Object.DestroyImmediate(arm.GetComponent<Collider>());
                arm.transform.SetParent(pole.transform, false);
                arm.transform.localPosition = new Vector3(0f, 0.9f, 0f);
                arm.transform.localScale = new Vector3(7f, 0.03f, 0.5f);
                arm.GetComponent<Renderer>().sharedMaterial = bark;
                Vector3 top = pole.transform.TransformPoint(new Vector3(0f, 0.9f, 0f));
                if (last.HasValue)
                    foreach (float side in new[] { -0.6f, 0.6f })
                        Wire(last.Value + Vector3.right * side, top + Vector3.right * side);
                last = top;
            }
        }

        private static void Wire(Vector3 a, Vector3 b)
        {
            GameObject w = new GameObject("Wire");
            LineRenderer line = w.AddComponent<LineRenderer>();
            line.sharedMaterial = wire;
            line.widthMultiplier = 0.025f;
            line.positionCount = 10;
            for (int i = 0; i < 10; i++)
            {
                float t = i / 9f;
                Vector3 p = Vector3.Lerp(a, b, t) + Vector3.down * Mathf.Sin(t * Mathf.PI) * 0.7f;
                line.SetPosition(i, p);
            }
        }

        // ---------- Dead trees in the back lanes and the orchard ----------

        private static readonly Vector3[] TreeSpots =
        {
            new Vector3(-15.5f, 0f, -14f), new Vector3(16f, 0f, -3f), new Vector3(-16f, 0f, 20f), new Vector3(26f, 0f, 22f),
            new Vector3(-25f, 0f, -1f), new Vector3(15.5f, 0f, 40f), new Vector3(-25.5f, 0f, 26f), new Vector3(25.5f, 0f, 61f),
            new Vector3(-15.8f, 0f, 77f), new Vector3(16f, 0f, 86f), new Vector3(-25.5f, 0f, 84f), new Vector3(25f, 0f, 98f),
            // The parsonage orchard.
            new Vector3(-24f, 0f, 43f), new Vector3(-20f, 0f, 42.5f), new Vector3(-24.5f, 0f, 47f), new Vector3(-19.5f, 0f, 47.5f),
            new Vector3(-24f, 0f, 51.5f), new Vector3(-20.5f, 0f, 52f),
        };

        private static void DeadTrees(int night)
        {
            foreach (Vector3 spot in TreeSpots)
            {
                if (!TownLayout.Open(night, spot) || spot.z < TownLayout.Shoreline(night)) continue;
                DeadTree(spot, Random.Range(3.5f, 6.5f));
            }
        }

        public static void DeadTree(Vector3 at, float height)
        {
            Materials();
            GameObject root = new GameObject("Dead Tree");
            root.transform.position = at;
            Vector3 lean = Quaternion.Euler(Random.Range(-8f, 8f), 0f, Random.Range(-8f, 8f)) * Vector3.up;
            Vector3 top = Branch(root.transform, at, lean, height * 0.55f, 0.16f + height * 0.02f, true);
            for (int i = 0; i < 3; i++) Grow(root.transform, top, height * 0.35f, 0.1f, 2);
        }

        private static void Grow(Transform root, Vector3 from, float length, float thickness, int depth)
        {
            Vector3 dir = (Vector3.up * Random.Range(0.6f, 1.3f) + Random.insideUnitSphere).normalized;
            if (dir.y < 0.15f) dir.y = 0.15f;
            Vector3 end = Branch(root, from, dir.normalized, length, thickness, false);
            if (depth <= 0) return;
            int children = Random.Range(1, 4);
            for (int i = 0; i < children; i++) Grow(root, end, length * 0.6f, thickness * 0.6f, depth - 1);
        }

        private static Vector3 Branch(Transform root, Vector3 from, Vector3 dir, float length, float thickness, bool solid)
        {
            GameObject b = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            b.name = "Branch";
            if (!solid) UnityEngine.Object.DestroyImmediate(b.GetComponent<Collider>());
            b.transform.SetParent(root, true);
            b.transform.position = from + dir * length * 0.5f;
            b.transform.rotation = Quaternion.FromToRotation(Vector3.up, dir);
            b.transform.localScale = new Vector3(thickness, length * 0.5f, thickness);
            b.GetComponent<Renderer>().sharedMaterial = bark;
            return from + dir * length;
        }

        // ---------- Wreckage the water left: planks, a fallen fence, upturned boats ----------

        private static void Debris(int night)
        {
            for (int i = 0; i < 70; i++)
            {
                Vector3 p = new Vector3(Random.Range(-26f, 26f), 0f, Random.Range(-15f, 99f));
                if (!TownLayout.Open(night, p) || p.z < TownLayout.Shoreline(night)) continue;
                if (Mathf.Abs(p.x) < 3.2f) continue; // keep the main road clear
                if (Physics.CheckBox(p + Vector3.up, new Vector3(0.8f, 0.9f, 0.8f), Quaternion.identity, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore)) continue;
                GameObject piece = GameObject.CreatePrimitive(PrimitiveType.Cube);
                piece.name = "Debris";
                UnityEngine.Object.DestroyImmediate(piece.GetComponent<Collider>());
                bool beam = Random.value < 0.3f;
                piece.transform.localScale = beam ? new Vector3(0.18f, 0.18f, Random.Range(2f, 3.5f)) : new Vector3(Random.Range(0.15f, 0.3f), 0.03f, Random.Range(0.8f, 2.2f));
                piece.transform.position = p + Vector3.up * (beam ? 0.12f : 0.03f);
                piece.transform.rotation = Quaternion.Euler(Random.Range(-6f, 6f), Random.Range(0f, 360f), beam ? Random.Range(-10f, 10f) : 0f);
                piece.GetComponent<Renderer>().sharedMaterial = plank;
            }

            // Rowboats the flood left, upturned and half sunk in the silt.
            Vector3[] boats = { new Vector3(-17f, 0f, -12f), new Vector3(16f, 0f, 24f), new Vector3(-16.5f, 0f, 64f), new Vector3(24f, 0f, 81.2f) };
            foreach (Vector3 b in boats)
                if (TownLayout.Open(night, b) && b.z >= TownLayout.Shoreline(night)) Wreck(b, Random.Range(0f, 360f));

            // A fence the water flattened.
            for (int i = 0; i < 8; i++)
            {
                Vector3 p = new Vector3(-26f + i * 1.1f, 0.06f, 23f);
                if (!TownLayout.Open(night, p)) break;
                GameObject post = GameObject.CreatePrimitive(PrimitiveType.Cube);
                UnityEngine.Object.DestroyImmediate(post.GetComponent<Collider>());
                post.transform.position = p;
                post.transform.localScale = new Vector3(0.1f, 0.08f, 1.2f);
                post.transform.rotation = Quaternion.Euler(0f, Random.Range(-20f, 20f), 80f);
                post.GetComponent<Renderer>().sharedMaterial = plank;
            }
        }

        public static void Wreck(Vector3 at, float yaw)
        {
            Materials();
            GameObject boat = new GameObject("Wrecked Boat");
            boat.transform.SetPositionAndRotation(at, Quaternion.Euler(Random.Range(-8f, 8f), yaw, 175f));
            void Board(Vector3 local, Vector3 scale)
            {
                GameObject board = GameObject.CreatePrimitive(PrimitiveType.Cube);
                UnityEngine.Object.DestroyImmediate(board.GetComponent<Collider>());
                board.transform.SetParent(boat.transform, false);
                board.transform.localPosition = local;
                board.transform.localScale = scale;
                board.GetComponent<Renderer>().sharedMaterial = wreck;
            }
            Board(new Vector3(0f, -0.35f, 0f), new Vector3(3f, 0.08f, 1.1f));
            Board(new Vector3(0f, -0.15f, 0.55f), new Vector3(3f, 0.45f, 0.06f));
            Board(new Vector3(0f, -0.15f, -0.55f), new Vector3(3f, 0.45f, 0.06f));
            Board(new Vector3(1.5f, -0.15f, 0f), new Vector3(0.06f, 0.45f, 1.1f));
            boat.AddComponent<BoxCollider>().size = new Vector3(3f, 0.6f, 1.2f);
            boat.transform.position += Vector3.up * 0.25f;
        }

        // ---------- Crows: on the roofs, on the wires, circling the tower ----------

        private static void Crows(int night)
        {
            var perches = new List<Vector3>();
            foreach (BuildingDef b in TownBuilder.Buildings)
            {
                Vector3 c = new Vector3(b.Centre.x, 0f, b.Centre.y);
                if (!TownLayout.Open(night, c) || c.z < TownLayout.Shoreline(night)) continue;
                if (Random.value < 0.45f) continue;
                Vector3 roof = c + new Vector3(Random.Range(-b.Size.x, b.Size.x) * 0.35f, b.Height + 0.22f, Random.Range(-b.Size.y, b.Size.y) * 0.35f);
                perches.Add(roof);
                if (Random.value < 0.5f) perches.Add(roof + new Vector3(Random.Range(-0.8f, 0.8f), 0f, Random.Range(-0.8f, 0.8f)));
            }
            foreach (Vector3 p in perches) Crow.Build(p, false);
            // A few always circle the bell tower, Night 2 onward, where they can be seen from the street.
            if (night >= 2 && night <= 4)
                for (int i = 0; i < 6; i++) Crow.Build(new Vector3(8f, 16f + i, 49f), true);
            else
                for (int i = 0; i < 5; i++) Crow.Build(new Vector3(0f, 18f + i, night == 1 ? 10f : 88f), true);
        }

        // ---------- Backdrops for the menu, the boat and the ending ----------

        public enum VistaKind { Menu, Boat, Dawn, Drowned }

        // The drowned town seen from the lake at night: a silhouette of roofs, the chapel tower, the dam.
        private static Transform Part(Transform parent, PrimitiveType type, Vector3 local, Vector3 scale, Material material)
        {
            GameObject g = GameObject.CreatePrimitive(type);
            UnityEngine.Object.DestroyImmediate(g.GetComponent<Collider>());
            g.transform.SetParent(parent, false);
            g.transform.localPosition = local;
            g.transform.localScale = scale;
            g.GetComponent<Renderer>().sharedMaterial = material;
            return g.transform;
        }

        public static Camera Vista(VistaKind kind)
        {
            Materials();
            Random.State saved = Random.state;
            Random.InitState(Seed);
            bool dawn = kind == VistaKind.Dawn;
            Color fog = dawn ? new Color(0.32f, 0.33f, 0.36f) : UiKit.Night;
            RenderSettings.fog = true;
            RenderSettings.fogColor = fog;
            RenderSettings.fogDensity = dawn ? 0.018f : 0.022f;
            RenderSettings.reflectionIntensity = 0.12f;
            // Flat ambient: in the default Skybox mode the colour below is ignored and Unity's bright default sky lights everything.
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
            RenderSettings.ambientLight = dawn ? new Color(0.25f, 0.24f, 0.26f) : new Color(0.12f, 0.14f, 0.17f) * GameSettings.Brightness;

            Light moon = new GameObject("Moonlight").AddComponent<Light>();
            moon.type = LightType.Directional;
            moon.transform.rotation = Quaternion.Euler(dawn ? 8f : 30f, dawn ? 100f : -30f, 0f);
            moon.color = dawn ? new Color(0.95f, 0.75f, 0.6f) : new Color(0.6f, 0.7f, 0.85f);
            moon.intensity = dawn ? 0.8f : 0.45f;

            float level = kind == VistaKind.Drowned ? 6f : 0f;
            GameObject lake = GameObject.CreatePrimitive(PrimitiveType.Plane);
            lake.name = "Lake";
            UnityEngine.Object.DestroyImmediate(lake.GetComponent<Collider>());
            lake.transform.position = new Vector3(0f, level, 60f);
            lake.transform.localScale = new Vector3(60f, 1f, 60f);
            Material water = new Material(Shader.Find("Standard")) { color = new Color(0.06f, 0.13f, 0.14f) };
            water.SetFloat("_Glossiness", 0.95f);
            lake.GetComponent<Renderer>().sharedMaterial = water;
            lake.AddComponent<LakeSwell>();

            Material wall = HushwaterAssets.Surface("weathered_plank_siding", new Color(0.3f, 0.3f, 0.29f), 2f);
            Material stone = HushwaterAssets.Surface("worn_mossy_plasterwall", new Color(0.32f, 0.33f, 0.32f), 3f);
            // The exposed town on the far shore: houses rising out of the silt, the chapel, its tower.
            for (int i = 0; i < 14; i++)
            {
                float x = -40f + i * 6.2f + Random.Range(-1.5f, 1.5f);
                float z = 70f + Random.Range(-6f, 18f);
                float h = Random.Range(3f, 6f);
                GameObject house = GameObject.CreatePrimitive(PrimitiveType.Cube);
                house.transform.position = new Vector3(x, h * 0.5f - 0.5f, z);
                house.transform.localScale = new Vector3(Random.Range(4f, 7f), h, Random.Range(4f, 7f));
                house.transform.rotation = Quaternion.Euler(0f, Random.Range(-10f, 10f), Random.Range(-3f, 3f));
                house.GetComponent<Renderer>().sharedMaterial = wall;
                GameObject roof = GameObject.CreatePrimitive(PrimitiveType.Cube);
                roof.transform.position = house.transform.position + Vector3.up * (h * 0.5f + 0.6f);
                roof.transform.localScale = new Vector3(house.transform.localScale.x * 0.75f, 1.6f, house.transform.localScale.z * 1.05f);
                roof.transform.rotation = house.transform.rotation * Quaternion.Euler(0f, 0f, 45f);
                roof.GetComponent<Renderer>().sharedMaterial = stone;
            }
            GameObject tower = GameObject.CreatePrimitive(PrimitiveType.Cube);
            tower.transform.position = new Vector3(6f, 7f, 82f);
            tower.transform.localScale = new Vector3(4f, 16f, 4f);
            tower.GetComponent<Renderer>().sharedMaterial = stone;
            GameObject spire = GameObject.CreatePrimitive(PrimitiveType.Cube);
            spire.transform.position = new Vector3(6f, 16.5f, 82f);
            spire.transform.localScale = new Vector3(2.8f, 2.8f, 2.8f);
            spire.transform.rotation = Quaternion.Euler(45f, 45f, 0f);
            spire.GetComponent<Renderer>().sharedMaterial = stone;
            GameObject dam = GameObject.CreatePrimitive(PrimitiveType.Cube);
            dam.transform.position = new Vector3(0f, 9f, 135f);
            dam.transform.localScale = new Vector3(120f, 22f, 4f);
            dam.GetComponent<Renderer>().sharedMaterial = stone;
            DeadTree(new Vector3(-18f, 0f, 64f), 6f);
            DeadTree(new Vector3(22f, 0f, 68f), 5f);
            DeadTree(new Vector3(-9f, 0f, 74f), 4.5f);

            if (!dawn) Sky(new Vector3(0f, 0f, 60f));
            Ridges(new Vector3(0f, 0f, 70f), 190f);
            for (int i = 0; i < 7; i++) Crow.Build(new Vector3(6f, 22f + i, 82f), true);

            // Where we watch from: the boat, rocking.
            Camera cam = UiKit.BackdropCamera();
            cam.tag = "MainCamera";
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = fog;
            cam.farClipPlane = 1200f;
            cam.transform.position = new Vector3(kind == VistaKind.Boat ? -2f : 0f, level + 1.3f, kind == VistaKind.Boat ? 22f : 10f);
            cam.transform.rotation = Quaternion.Euler(-3f, kind == VistaKind.Menu ? 8f : 0f, 0f);
            cam.gameObject.AddComponent<BoatRock>();

            if (kind == VistaKind.Boat || kind == VistaKind.Menu)
            {
                // The bow of Nell's boat in the foreground with its lantern.
                GameObject bow = new GameObject("Bow");
                bow.transform.SetParent(cam.transform, false);
                bow.transform.localPosition = new Vector3(0.6f, -1.05f, 1.6f);
                Material hull = HushwaterAssets.Surface("weathered_plank_siding", new Color(0.32f, 0.27f, 0.21f), 1f);
                foreach (float side in new[] { -0.45f, 0.45f })
                {
                    GameObject gunwale = GameObject.CreatePrimitive(PrimitiveType.Cube);
                    gunwale.transform.SetParent(bow.transform, false);
                    gunwale.transform.localPosition = new Vector3(side, 0f, 0.4f);
                    gunwale.transform.localRotation = Quaternion.Euler(0f, -side * 22f, 0f);
                    gunwale.transform.localScale = new Vector3(0.07f, 0.28f, 1.8f);
                    gunwale.GetComponent<Renderer>().sharedMaterial = hull;
                }
                // Nell's lantern hanging from a pole at the bow: brass frame, glowing glass, a halo in the mist.
                Part(bow.transform, PrimitiveType.Cylinder, new Vector3(-0.6f, 0.05f, 1.2f), new Vector3(0.035f, 0.3f, 0.035f), hull);
                Part(bow.transform, PrimitiveType.Cylinder, new Vector3(-0.6f, 0.36f, 1.31f), new Vector3(0.025f, 0.11f, 0.025f), hull).localRotation = Quaternion.Euler(90f, 0f, 0f);
                Material brass = new Material(Shader.Find("Standard")) { color = new Color(0.45f, 0.35f, 0.18f) };
                brass.SetFloat("_Metallic", 0.7f);
                Material glass = new Material(Shader.Find("Standard")) { color = new Color(1f, 0.85f, 0.55f) };
                glass.EnableKeyword("_EMISSION");
                glass.SetColor("_EmissionColor", UiKit.Lantern * 3f);
                Vector3 hang = new Vector3(-0.6f, 0.2f, 1.42f);
                Part(bow.transform, PrimitiveType.Cylinder, hang + Vector3.up * 0.17f, new Vector3(0.2f, 0.025f, 0.2f), brass);
                Part(bow.transform, PrimitiveType.Cylinder, hang, new Vector3(0.15f, 0.14f, 0.15f), glass);
                Part(bow.transform, PrimitiveType.Cylinder, hang - Vector3.up * 0.16f, new Vector3(0.22f, 0.025f, 0.22f), brass);
                GameObject halo = GameObject.CreatePrimitive(PrimitiveType.Quad);
                UnityEngine.Object.DestroyImmediate(halo.GetComponent<Collider>());
                halo.transform.SetParent(bow.transform, false);
                halo.transform.localPosition = hang;
                halo.transform.localScale = Vector3.one * 0.9f;
                halo.GetComponent<Renderer>().sharedMaterial = new Material(Fx.Particle) { color = new Color(1f, 0.78f, 0.42f, 0.45f), renderQueue = 3200 };
                halo.AddComponent<FaceCamera>();
                Light lantern = new GameObject("Boat Lantern").AddComponent<Light>();
                lantern.transform.SetParent(bow.transform, false);
                lantern.transform.localPosition = hang;
                lantern.type = LightType.Point;
                lantern.color = UiKit.Lantern;
                lantern.range = 8f;
                lantern.intensity = 1.6f;
                lantern.gameObject.AddComponent<Flicker>();
            }
            Random.state = saved;
            return cam;
        }
    }

    // Cloth that stirs in a breath of wind.
    public sealed class Sway : MonoBehaviour
    {
        private Quaternion rest;
        private float seed;

        private void Start()
        {
            rest = transform.rotation;
            seed = transform.position.x * 1.7f + transform.position.z;
        }

        private void Update() =>
            transform.rotation = rest * Quaternion.Euler(Mathf.Sin(Time.time * 0.9f + seed) * 6f + Mathf.PerlinNoise(Time.time * 0.4f, seed) * 8f, 0f, 0f);
    }

    // A UI glow that breathes slowly, like a flame.
    public sealed class GlowPulse : MonoBehaviour
    {
        private UnityEngine.UI.Image image;
        private Color baseColor;

        private void Start()
        {
            image = GetComponent<UnityEngine.UI.Image>();
            baseColor = image.color;
        }

        private void Update()
        {
            float f = 0.8f + 0.2f * Mathf.PerlinNoise(Time.unscaledTime * 1.5f, transform.position.x);
            image.color = new Color(baseColor.r, baseColor.g, baseColor.b, baseColor.a * f);
            transform.localScale = Vector3.one * (0.95f + 0.08f * f);
        }
    }

    // Keeps a quad turned to the camera (the moon).
    public sealed class FaceCamera : MonoBehaviour
    {
        private void LateUpdate()
        {
            Camera cam = Camera.main != null ? Camera.main : Camera.current;
            if (cam != null) transform.rotation = cam.transform.rotation;
        }
    }

    // The camera rides the swell, as if sitting in the boat.
    public sealed class BoatRock : MonoBehaviour
    {
        private Vector3 start;
        private Quaternion rest;

        private void Start()
        {
            start = transform.position;
            rest = transform.rotation;
        }

        private void Update()
        {
            float t = Time.unscaledTime;
            transform.position = start + new Vector3(Mathf.Sin(t * 0.13f) * 1.2f, Mathf.Sin(t * 0.7f) * 0.08f, Mathf.Sin(t * 0.09f) * 0.8f);
            transform.rotation = rest * Quaternion.Euler(Mathf.Sin(t * 0.6f) * 1.2f, Mathf.Sin(t * 0.05f) * 6f, Mathf.Sin(t * 0.45f) * 1.8f);
        }
    }

    // The lake's surface heaves slowly.
    public sealed class LakeSwell : MonoBehaviour
    {
        private float baseY;

        private void Start() => baseY = transform.position.y;

        private void Update() =>
            transform.position = new Vector3(transform.position.x, baseY + Mathf.Sin(Time.unscaledTime * 0.5f) * 0.04f, transform.position.z);
    }

    // A lantern flame's unsteady light.
    public sealed class Flicker : MonoBehaviour
    {
        private Light lamp;
        private float baseIntensity;

        private void Start()
        {
            lamp = GetComponent<Light>();
            baseIntensity = lamp.intensity;
        }

        private void Update() =>
            lamp.intensity = baseIntensity * (0.85f + 0.15f * Mathf.PerlinNoise(Time.unscaledTime * 6f, 0f));
    }

    // Low mist drifting over the silt around Nell.
    public sealed class GroundMist : MonoBehaviour
    {
        private void Start()
        {
            ParticleSystem ps = gameObject.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            ParticleSystem.MainModule main = ps.main;
            main.startLifetime = 16f;
            main.startSpeed = 0.15f;
            main.startSize = new ParticleSystem.MinMaxCurve(5f, 9f);
            main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
            main.startColor = new Color(0.3f, 0.35f, 0.38f, 0.035f);
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.maxParticles = 140;
            main.prewarm = true;
            ParticleSystem.EmissionModule emission = ps.emission;
            emission.rateOverTime = 8f;
            ParticleSystem.ShapeModule shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Box;
            shape.scale = new Vector3(36f, 0.5f, 36f);
            shape.position = new Vector3(0f, -0.9f, 0f);
            ParticleSystem.ColorOverLifetimeModule fade = ps.colorOverLifetime;
            fade.enabled = true;
            var gradient = new Gradient();
            gradient.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.3f), new GradientAlphaKey(1f, 0.7f), new GradientAlphaKey(0f, 1f) });
            fade.color = gradient;
            // Lying flat, so it pools over the ground instead of hanging in front of Nell's face.
            ParticleSystemRenderer renderer = GetComponent<ParticleSystemRenderer>();
            renderer.renderMode = ParticleSystemRenderMode.HorizontalBillboard;
            renderer.sharedMaterial = new Material(Fx.Particle) { renderQueue = 3100 };
            ps.Play();
        }
    }

    // A crow. Perched ones burst into the air when Nell comes close, when something loud happens nearby,
    // or when the Sexton walks under them, so a scattering flock tells you where he is. Some just circle.
    public sealed class Crow : MonoBehaviour
    {
        private enum Mode { Perched, Fleeing, Circling, Returning }

        private static readonly List<Crow> All = new List<Crow>();
        private static Material feathers;
        private static float lastCaw;
        private static float nextSearch;
        private static Transform sexton;

        // Where a flock took off, for the HUD captions.
        public static event Action<Vector3> Scattered;

        private Mode mode;
        private bool forever;
        private Vector3 perch;
        private Quaternion perchFacing;
        private Vector3 circleCentre;
        private float circleRadius, circleSpeed, angle, timer;
        private Transform wingL, wingR, head;
        private float flapPhase;
        private Vector3 velocity;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            All.Clear();
            feathers = null;
            Scattered = null;
            lastCaw = 0f;
            nextSearch = 0f;
            sexton = null;
        }

        public static Crow Build(Vector3 at, bool circling)
        {
            if (feathers == null)
            {
                feathers = new Material(Shader.Find("Standard")) { color = new Color(0.03f, 0.03f, 0.035f) };
                feathers.SetFloat("_Glossiness", 0.6f);
            }
            GameObject root = new GameObject("Crow");
            root.transform.SetPositionAndRotation(at, Quaternion.Euler(0f, Random.Range(0f, 360f), 0f));
            Crow crow = root.AddComponent<Crow>();
            Transform t = root.transform;
            Part(t, PrimitiveType.Capsule, new Vector3(0f, 0.12f, 0f), new Vector3(0.11f, 0.13f, 0.11f), Quaternion.Euler(70f, 0f, 0f));
            crow.head = Part(t, PrimitiveType.Sphere, new Vector3(0f, 0.2f, 0.11f), Vector3.one * 0.08f, Quaternion.identity);
            Part(crow.head, PrimitiveType.Cube, new Vector3(0f, -0.05f, 0.9f), new Vector3(0.3f, 0.3f, 0.9f), Quaternion.identity);
            Part(t, PrimitiveType.Cube, new Vector3(0f, 0.09f, -0.17f), new Vector3(0.08f, 0.015f, 0.14f), Quaternion.Euler(-15f, 0f, 0f));
            crow.wingL = Pivot(t, new Vector3(-0.05f, 0.15f, 0f));
            crow.wingR = Pivot(t, new Vector3(0.05f, 0.15f, 0f));
            Part(crow.wingL, PrimitiveType.Cube, new Vector3(-0.16f, 0f, -0.02f), new Vector3(0.32f, 0.012f, 0.13f), Quaternion.identity);
            Part(crow.wingR, PrimitiveType.Cube, new Vector3(0.16f, 0f, -0.02f), new Vector3(0.32f, 0.012f, 0.13f), Quaternion.identity);
            crow.perch = at;
            crow.perchFacing = root.transform.rotation;
            crow.forever = circling;
            crow.circleCentre = at;
            crow.circleRadius = Random.Range(6f, 12f);
            crow.circleSpeed = Random.Range(0.25f, 0.45f) * (Random.value < 0.5f ? -1f : 1f);
            crow.angle = Random.Range(0f, Mathf.PI * 2f);
            crow.mode = circling ? Mode.Circling : Mode.Perched;
            return crow;
        }

        private static Transform Pivot(Transform parent, Vector3 local)
        {
            Transform p = new GameObject("Wing").transform;
            p.SetParent(parent, false);
            p.localPosition = local;
            return p;
        }

        private static Transform Part(Transform parent, PrimitiveType type, Vector3 local, Vector3 scale, Quaternion rotation)
        {
            GameObject g = GameObject.CreatePrimitive(type);
            UnityEngine.Object.DestroyImmediate(g.GetComponent<Collider>());
            g.transform.SetParent(parent, false);
            g.transform.localPosition = local;
            g.transform.localRotation = rotation;
            g.transform.localScale = scale;
            g.GetComponent<Renderer>().sharedMaterial = feathers;
            return g.transform;
        }

        private void OnEnable()
        {
            All.Add(this);
            NoisePulse.Emitted += OnNoise;
        }

        private void OnDisable()
        {
            All.Remove(this);
            NoisePulse.Emitted -= OnNoise;
        }

        // Anything loud close by sends them up.
        private void OnNoise(NoiseEvent noise)
        {
            if (mode == Mode.Perched && noise.Radius >= 10f && Vector3.Distance(noise.Position, perch) < noise.Radius * 0.6f) TakeOff(true);
        }

        private void Update()
        {
            float dt = Time.deltaTime;
            switch (mode)
            {
                case Mode.Perched: Perched(dt); break;
                case Mode.Fleeing: Fleeing(dt); break;
                case Mode.Circling: Circling(dt); break;
                case Mode.Returning: Returning(dt); break;
            }
        }

        private void Perched(float dt)
        {
            wingL.localRotation = Quaternion.Euler(0f, -60f, -10f);
            wingR.localRotation = Quaternion.Euler(0f, 60f, 10f);
            // Little twitches of the head.
            head.localRotation = Quaternion.Euler(Mathf.Sin(Time.time * 1.3f + perch.x) * 15f, Mathf.Round(Mathf.Sin(Time.time * 0.4f + perch.z) * 2f) * 35f, 0f);
            PlayerController player = PlayerController.Instance;
            if (player != null)
            {
                float close = player.IsCrouching ? 3f : player.IsSprinting ? 9f : 5.5f;
                if (Vector3.Distance(player.transform.position, perch) < close) { TakeOff(true); return; }
            }
            if (sexton == null && Time.time > nextSearch)
            {
                nextSearch = Time.time + 1f;
                SextonController s = FindAnyObjectByType<SextonController>();
                if (s != null) sexton = s.transform;
            }
            if (sexton != null && sexton.gameObject.activeInHierarchy && Vector3.Distance(sexton.position, perch) < 7f) TakeOff(true);
        }

        // Up and away from the perch, then into a wide circle overhead.
        private void TakeOff(bool spread)
        {
            if (mode != Mode.Perched) return;
            mode = Mode.Fleeing;
            timer = Random.Range(1.5f, 2.5f);
            Vector3 away = Random.insideUnitSphere;
            away.y = 0f;
            velocity = (away.normalized + Vector3.up * 1.4f).normalized * 6f;
            circleCentre = perch + Vector3.up * Random.Range(12f, 18f);
            if (Time.time - lastCaw > 1.2f)
            {
                lastCaw = Time.time;
                ProceduralSounds.PlayAt(ProceduralSounds.Flap, perch, 0.8f);
                ProceduralSounds.PlayAt(ProceduralSounds.Caw, perch + Vector3.up, 0.9f);
                Scattered?.Invoke(perch);
            }
            if (!spread) return;
            foreach (Crow other in All.ToArray())
                if (other != this && Vector3.Distance(other.perch, perch) < 6f) other.TakeOff(false);
        }

        private void Fleeing(float dt)
        {
            Flap(dt, 22f);
            transform.position += velocity * dt;
            Face(velocity);
            timer -= dt;
            if (timer > 0f) return;
            mode = Mode.Circling;
            timer = Random.Range(18f, 30f);
            angle = Mathf.Atan2(transform.position.z - circleCentre.z, transform.position.x - circleCentre.x);
        }

        private void Circling(float dt)
        {
            Flap(dt, Mathf.Sin(Time.time * 0.7f + angle) > 0.3f ? 14f : 0f); // flap, then glide
            angle += circleSpeed * dt;
            Vector3 target = circleCentre + new Vector3(Mathf.Cos(angle) * circleRadius, Mathf.Sin(angle * 2f) * 1.2f, Mathf.Sin(angle) * circleRadius);
            Vector3 step = target - transform.position;
            transform.position = Vector3.MoveTowards(transform.position, target, 7f * dt);
            if (step.sqrMagnitude > 0.001f) Face(step);
            if (forever) return;
            timer -= dt;
            if (timer <= 0f) mode = Mode.Returning;
        }

        private void Returning(float dt)
        {
            Flap(dt, 16f);
            Vector3 step = perch - transform.position;
            transform.position = Vector3.MoveTowards(transform.position, perch, 5f * dt);
            if (step.sqrMagnitude > 0.01f) Face(step);
            if (step.magnitude > 0.1f) return;
            transform.rotation = perchFacing;
            mode = Mode.Perched;
        }

        private void Flap(float dt, float rate)
        {
            flapPhase += dt * rate;
            float a = rate > 0f ? Mathf.Sin(flapPhase) * 45f : 8f;
            wingL.localRotation = Quaternion.Euler(0f, 0f, a);
            wingR.localRotation = Quaternion.Euler(0f, 0f, -a);
        }

        private void Face(Vector3 dir)
        {
            dir.y *= 0.4f;
            if (dir.sqrMagnitude > 0.0001f) transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(dir), 0.15f);
        }
    }
}
