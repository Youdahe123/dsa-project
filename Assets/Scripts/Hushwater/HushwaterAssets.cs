using System.Collections.Generic;
using UnityEngine;

namespace Hushwater
{
    // Loads the Poly Haven (CC0) models and textures under Resources/Hushwater.
    public static class HushwaterAssets
    {
        private const string Root = "Hushwater/";
        private static readonly Dictionary<string, Material> Materials = new Dictionary<string, Material>();
        private static readonly Dictionary<string, GameObject> Models = new Dictionary<string, GameObject>();
        private static Font titleFont;
        private static Font bodyFont;

        // Everything the lake touched is greyed toward silt.
        public static readonly Color SiltTint = new Color(0.78f, 0.76f, 0.68f);

        public static Font TitleFont => titleFont != null ? titleFont : titleFont = LoadFont("Fonts/IMFellEnglishSC");
        public static Font BodyFont => bodyFont != null ? bodyFont : bodyFont = LoadFont("Fonts/SpecialElite");

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            Materials.Clear();
            Models.Clear();
        }

        private static Font LoadFont(string path)
        {
            Font font = Resources.Load<Font>(Root + path);
            return font != null ? font : Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        }

        // Destroy that also works when the builder runs in edit mode (screenshots, tools).
        public static void Discard(Object target)
        {
            if (target == null) return;
            if (Application.isPlaying) Object.Destroy(target);
            else Object.DestroyImmediate(target);
        }

        public static Texture2D Texture(string file) => Resources.Load<Texture2D>(Root + "Textures/" + file);

        public static Shader Shader(string name)
        {
            Shader shader = UnityEngine.Shader.Find(name);
            return shader != null ? shader : UnityEngine.Shader.Find("Standard");
        }

        // A tiling PBR surface from one of the downloaded texture sets.
        public static Material Surface(string texture, Color tint, float smoothness = 0.2f, float normalStrength = 1f)
        {
            string key = $"{texture}|{tint}|{smoothness}|{normalStrength}";
            if (Materials.TryGetValue(key, out Material cached)) return cached;
            Material material = new Material(Shader("Standard")) { name = texture };
            material.color = tint;
            material.SetFloat("_Glossiness", smoothness);
            material.SetFloat("_Metallic", 0f);
            Texture2D albedo = Texture(texture + "_diff");
            if (albedo != null) material.mainTexture = albedo;
            Texture2D normal = Texture(texture + "_nor_gl");
            if (normal != null)
            {
                material.SetTexture("_BumpMap", normal);
                material.SetFloat("_BumpScale", normalStrength);
                material.EnableKeyword("_NORMALMAP");
            }
            material.enableInstancing = true;
            Materials[key] = material;
            return material;
        }

        public static Material Plain(Color color, float smoothness = 0.2f, float metallic = 0f)
        {
            string key = $"plain|{color}|{smoothness}|{metallic}";
            if (Materials.TryGetValue(key, out Material cached)) return cached;
            Material material = new Material(Shader("Standard")) { name = "Plain" };
            material.color = color;
            material.SetFloat("_Glossiness", smoothness);
            material.SetFloat("_Metallic", metallic);
            Materials[key] = material;
            return material;
        }

        public static Material Emissive(Color color, Color emission)
        {
            Material material = new Material(Shader("Standard")) { name = "Emissive" };
            material.color = color;
            material.EnableKeyword("_EMISSION");
            material.SetColor("_EmissionColor", emission);
            material.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
            return material;
        }

        // Spawns a model scaled to targetHeight (0 keeps its own size; negative fits the longest side), standing on position.
        public static GameObject Model(string name, Transform parent, Vector3 position, float yaw, float targetHeight, bool solid = true, float wetness = 0.32f)
        {
            if (!Models.TryGetValue(name, out GameObject prefab))
            {
                prefab = Resources.Load<GameObject>(Root + "Models/" + name + "/" + name);
                Models[name] = prefab;
            }

            // Wrap the model so its import transform (Blender axis rotation, unit scale) is preserved.
            GameObject instance = new GameObject(name);
            instance.transform.SetParent(parent, false);
            instance.transform.rotation = Quaternion.Euler(0f, yaw, 0f);
            if (prefab != null) Object.Instantiate(prefab, instance.transform, false).name = name + " mesh";
            else
            {
                Debug.LogWarning($"Hushwater: model '{name}' missing, using a placeholder box.");
                GameObject box = GameObject.CreatePrimitive(PrimitiveType.Cube);
                box.transform.SetParent(instance.transform, false);
                Discard(box.GetComponent<Collider>());
            }

            Transform t = instance.transform;
            Bounds local = LocalBounds(instance);
            float longest = Mathf.Max(local.size.x, Mathf.Max(local.size.y, local.size.z));
            float scale = targetHeight > 0f && local.size.y > 0.0001f ? targetHeight / local.size.y
                : targetHeight < 0f && longest > 0.0001f ? -targetHeight / longest : 1f;
            t.localScale = Vector3.one * scale;
            Vector3 anchor = new Vector3(local.center.x, local.min.y, local.center.z) * scale;
            t.position = position - t.rotation * anchor;

            foreach (Renderer renderer in instance.GetComponentsInChildren<Renderer>())
            {
                Material[] materials = renderer.materials;
                foreach (Material material in materials)
                {
                    if (material.HasProperty("_Color")) material.color *= SiltTint;
                    if (material.HasProperty("_Glossiness")) material.SetFloat("_Glossiness", wetness);
                    if (material.HasProperty("_GlossMapScale")) material.SetFloat("_GlossMapScale", wetness);
                }
                renderer.materials = materials;
            }

            if (solid)
            {
                BoxCollider box = instance.AddComponent<BoxCollider>();
                box.center = local.center;
                box.size = local.size;
            }
            return instance;
        }

        public static Bounds LocalBounds(GameObject root)
        {
            Transform t = root.transform;
            bool any = false;
            Bounds bounds = new Bounds();
            foreach (MeshFilter filter in root.GetComponentsInChildren<MeshFilter>())
            {
                if (filter.sharedMesh == null) continue;
                Bounds mb = filter.sharedMesh.bounds;
                for (int i = 0; i < 8; i++)
                {
                    Vector3 corner = mb.center + Vector3.Scale(mb.extents, new Vector3((i & 1) == 0 ? -1 : 1, (i & 2) == 0 ? -1 : 1, (i & 4) == 0 ? -1 : 1));
                    Vector3 p = t.InverseTransformPoint(filter.transform.TransformPoint(corner));
                    if (!any) { bounds = new Bounds(p, Vector3.zero); any = true; }
                    else bounds.Encapsulate(p);
                }
            }
            if (!any) bounds = new Bounds(Vector3.up * 0.5f, Vector3.one);
            return bounds;
        }
    }

    // Small procedural meshes with world-scaled UVs, so textures keep their real size.
    public static class MeshKit
    {
        private static readonly Dictionary<string, Mesh> Boxes = new Dictionary<string, Mesh>();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => Boxes.Clear();

        public static Mesh Box(Vector3 size, float textureMetres)
        {
            string key = $"{size.x:F2}|{size.y:F2}|{size.z:F2}|{textureMetres:F2}";
            if (Boxes.TryGetValue(key, out Mesh cached)) return cached;
            Vector3 h = size * 0.5f;
            var vertices = new List<Vector3>();
            var normals = new List<Vector3>();
            var uvs = new List<Vector2>();
            var triangles = new List<int>();
            void Face(Vector3 normal, Vector3 right, Vector3 up, float width, float height)
            {
                int start = vertices.Count;
                Vector3 centre = Vector3.Scale(normal, h);
                Vector3 r = right * width * 0.5f;
                Vector3 u = up * height * 0.5f;
                vertices.Add(centre - r - u); vertices.Add(centre + r - u); vertices.Add(centre + r + u); vertices.Add(centre - r + u);
                for (int i = 0; i < 4; i++) normals.Add(normal);
                float w = width / textureMetres, ht = height / textureMetres;
                uvs.Add(new Vector2(0, 0)); uvs.Add(new Vector2(w, 0)); uvs.Add(new Vector2(w, ht)); uvs.Add(new Vector2(0, ht));
                triangles.AddRange(new[] { start, start + 2, start + 1, start, start + 3, start + 2 });
            }
            Face(Vector3.forward, Vector3.left, Vector3.up, size.x, size.y);
            Face(Vector3.back, Vector3.right, Vector3.up, size.x, size.y);
            Face(Vector3.right, Vector3.forward, Vector3.up, size.z, size.y);
            Face(Vector3.left, Vector3.back, Vector3.up, size.z, size.y);
            Face(Vector3.up, Vector3.right, Vector3.forward, size.x, size.z);
            Face(Vector3.down, Vector3.right, Vector3.back, size.x, size.z);
            Mesh mesh = new Mesh { name = "Box " + key };
            mesh.SetVertices(vertices);
            mesh.SetNormals(normals);
            mesh.SetUVs(0, uvs);
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateBounds();
            mesh.RecalculateTangents();
            Boxes[key] = mesh;
            return mesh;
        }

        // Revolves a (radius, height) profile around Y. Used for the bell, the clapper and the Sexton's coat.
        public static Mesh Lathe(Vector2[] profile, int segments, float jitter = 0f, int seed = 1)
        {
            var random = new System.Random(seed);
            var vertices = new List<Vector3>();
            var uvs = new List<Vector2>();
            var triangles = new List<int>();
            for (int s = 0; s <= segments; s++)
            {
                float angle = s / (float)segments * Mathf.PI * 2f;
                Vector3 dir = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle));
                for (int p = 0; p < profile.Length; p++)
                {
                    float wobble = s == segments ? 0f : (float)(random.NextDouble() - 0.5) * jitter * (1f - p / (float)profile.Length);
                    vertices.Add(dir * (profile[p].x + wobble) + Vector3.up * (profile[p].y + (p == 0 ? wobble * 2f : 0f)));
                    uvs.Add(new Vector2(s / (float)segments * 2f, profile[p].y));
                }
            }
            // Close the seam by copying the first ring's offsets onto the last.
            int ring = profile.Length;
            for (int p = 0; p < ring; p++) vertices[segments * ring + p] = vertices[p];
            for (int s = 0; s < segments; s++)
            {
                for (int p = 0; p < ring - 1; p++)
                {
                    int a = s * ring + p, b = (s + 1) * ring + p;
                    triangles.AddRange(new[] { a, a + 1, b, b, a + 1, b + 1 });
                }
            }
            Mesh mesh = new Mesh { name = "Lathe" };
            mesh.SetVertices(vertices);
            mesh.SetUVs(0, uvs);
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            mesh.RecalculateTangents();
            return mesh;
        }

        public static GameObject Solid(string name, Transform parent, Mesh mesh, Material material, Vector3 position, Quaternion rotation, bool collider = true)
        {
            GameObject go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.SetPositionAndRotation(position, rotation);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            go.AddComponent<MeshRenderer>().sharedMaterial = material;
            if (collider) go.AddComponent<BoxCollider>();
            return go;
        }
    }
}
