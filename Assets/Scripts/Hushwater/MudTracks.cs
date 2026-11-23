using System.Collections.Generic;
using UnityEngine;

namespace Hushwater
{
    // Deep mud pools and the grooves Nell leaves in them.
    // Design doc: "Mud remembers. Nell leaves grooves in the silt. He drags his hook through the mud and follows them by touch."
    public static class MudTracks
    {
        // One groove: where it is and when Nell made it.
        public readonly struct Footprint
        {
            public readonly Vector3 Position;
            public readonly float Time;

            // Stores one footprint.
            public Footprint(Vector3 position, float time)
            {
                Position = position;
                Time = time;
            }
        }

        // Not in the doc: how long a groove stays readable before the silt settles. Tune in playtests.
        public const float Lifetime = 45f;
        private const int MaxPrints = 80;

        private static readonly List<Footprint> Prints = new List<Footprint>();
        private static readonly List<GameObject> Marks = new List<GameObject>();
        private static readonly List<Vector4> Pools = new List<Vector4>(); // xyz centre, w radius
        private static Material groove;

        // Clears pools and prints; called once per night (and between play sessions).
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        public static void Clear()
        {
            Prints.Clear();
            Marks.Clear();
            Pools.Clear();
            groove = null;
        }

        // Registers a circular pool of deep mud.
        public static void AddPool(Vector3 centre, float radius) => Pools.Add(new Vector4(centre.x, centre.y, centre.z, radius));

        // True if the point stands in deep mud (only x/z matter).
        public static bool InMud(Vector3 point)
        {
            foreach (Vector4 pool in Pools)
            {
                float dx = point.x - pool.x;
                float dz = point.z - pool.z;
                if (dx * dx + dz * dz < pool.w * pool.w) return true;
            }
            return false;
        }

        // Leaves a groove at Nell's feet, with a small dark mark the player can see too.
        public static void Add(Vector3 feet, Quaternion facing)
        {
            Prints.Add(new Footprint(feet, Time.time));
            if (groove == null) groove = new Material(Shader.Find("Standard")) { color = new Color(0.09f, 0.08f, 0.06f) };

            GameObject mark = GameObject.CreatePrimitive(PrimitiveType.Cube);
            mark.name = "Groove";
            Object.DestroyImmediate(mark.GetComponent<Collider>());
            mark.GetComponent<Renderer>().sharedMaterial = groove;
            mark.transform.SetPositionAndRotation(new Vector3(feet.x, 0.02f, feet.z), Quaternion.Euler(0f, facing.eulerAngles.y, 0f));
            mark.transform.localScale = new Vector3(0.13f, 0.01f, 0.3f);
            Object.Destroy(mark, Lifetime);
            Marks.Add(mark);

            // Keep the list bounded; the oldest grooves go first.
            if (Prints.Count > MaxPrints) Prints.RemoveAt(0);
            if (Marks.Count > MaxPrints)
            {
                if (Marks[0] != null) Object.Destroy(Marks[0]);
                Marks.RemoveAt(0);
            }
        }

        // The freshest groove within reach that is newer than 'after', so following prints always moves forward along the trail.
        public static bool NewestNear(Vector3 point, float reach, float after, out Footprint found)
        {
            found = default;
            bool any = false;
            foreach (Footprint print in Prints)
            {
                if (Time.time - print.Time > Lifetime || print.Time <= after) continue;
                Vector3 d = print.Position - point;
                d.y = 0f;
                if (d.sqrMagnitude > reach * reach) continue;
                if (!any || print.Time > found.Time)
                {
                    found = print;
                    any = true;
                }
            }
            return any;
        }
    }
}
