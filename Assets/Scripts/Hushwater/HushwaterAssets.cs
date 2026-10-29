using System.Collections.Generic;
using UnityEngine;

namespace Hushwater
{
    // Loads the Poly Haven (CC0) textures and models under Resources/Hushwater.
    // Anything missing falls back to a flat colour or a grey box, so the prototype still runs.
    public static class HushwaterAssets
    {
        private const string Root = "Hushwater/";
        private static readonly Dictionary<string, Material> Materials = new Dictionary<string, Material>();

        // Everything the reservoir touched is greyed toward silt.
        private static readonly Color SiltTint = new Color(0.78f, 0.76f, 0.68f);

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => Materials.Clear();

        // A tiling surface from one of the texture sets (e.g. "brown_mud_02" loads _diff and _nor_gl).
        public static Material Surface(string texture, Color tint, float tiling = 1f, float smoothness = 0.2f)
        {
            string key = $"{texture}|{tint}|{tiling}";
            if (Materials.TryGetValue(key, out Material cached)) return cached;

            Material material = new Material(Shader.Find("Standard")) { name = texture, color = tint };
            material.SetFloat("_Glossiness", smoothness);
            Texture2D albedo = Resources.Load<Texture2D>(Root + "Textures/" + texture + "_diff");
            if (albedo != null) material.mainTexture = albedo;
            Texture2D normal = Resources.Load<Texture2D>(Root + "Textures/" + texture + "_nor_gl");
            if (normal != null)
            {
                material.SetTexture("_BumpMap", normal);
                material.EnableKeyword("_NORMALMAP");
            }
            material.mainTextureScale = Vector2.one * tiling;
            Materials[key] = material;
            return material;
        }

        // Spawns a model scaled to targetHeight and standing on position, with a box collider when solid.
        public static GameObject Model(string name, Vector3 position, float yaw, float targetHeight, bool solid = true)
        {
            GameObject prefab = Resources.Load<GameObject>(Root + "Models/" + name + "/" + name);

            // Wrap the model so its import transform (Blender axis rotation, unit scale) is kept.
            GameObject instance = new GameObject(name);
            instance.transform.rotation = Quaternion.Euler(0f, yaw, 0f);
            if (prefab != null) Object.Instantiate(prefab, instance.transform, false);
            else
            {
                Debug.LogWarning($"Hushwater: model '{name}' missing, using a placeholder box.");
                GameObject box = GameObject.CreatePrimitive(PrimitiveType.Cube);
                box.transform.SetParent(instance.transform, false);
                Object.Destroy(box.GetComponent<Collider>());
            }

            Bounds local = LocalBounds(instance);
            float scale = local.size.y > 0.0001f ? targetHeight / local.size.y : 1f;
            instance.transform.localScale = Vector3.one * scale;
            Vector3 feet = new Vector3(local.center.x, local.min.y, local.center.z) * scale;
            instance.transform.position = position - instance.transform.rotation * feet;

            foreach (Renderer renderer in instance.GetComponentsInChildren<Renderer>())
                foreach (Material material in renderer.materials)
                    if (material.HasProperty("_Color")) material.color *= SiltTint;

            if (solid)
            {
                BoxCollider collider = instance.AddComponent<BoxCollider>();
                collider.center = local.center;
                collider.size = local.size;
            }
            return instance;
        }

        // Bounds of every mesh under root, in root's local space (before scaling).
        private static Bounds LocalBounds(GameObject root)
        {
            bool any = false;
            Bounds bounds = new Bounds();
            foreach (MeshFilter filter in root.GetComponentsInChildren<MeshFilter>())
            {
                if (filter.sharedMesh == null) continue;
                Bounds mesh = filter.sharedMesh.bounds;
                for (int i = 0; i < 8; i++)
                {
                    Vector3 corner = mesh.center + Vector3.Scale(mesh.extents, new Vector3((i & 1) == 0 ? -1 : 1, (i & 2) == 0 ? -1 : 1, (i & 4) == 0 ? -1 : 1));
                    Vector3 point = root.transform.InverseTransformPoint(filter.transform.TransformPoint(corner));
                    if (!any) { bounds = new Bounds(point, Vector3.zero); any = true; }
                    else bounds.Encapsulate(point);
                }
            }
            return any ? bounds : new Bounds(Vector3.up * 0.5f, Vector3.one);
        }
    }
}
