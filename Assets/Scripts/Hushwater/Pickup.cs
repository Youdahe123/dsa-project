using System.Collections.Generic;
using UnityEngine;

namespace Hushwater
{
    // Something Nell can pick up with E: a spare bottle to throw, or a piece of evidence.
    public sealed class Pickup : MonoBehaviour
    {
        public enum Kind { Bottle, Evidence }

        private static readonly List<Pickup> All = new List<Pickup>();

        public Kind Type { get; private set; }
        public string Label { get; private set; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => All.Clear();

        public static Pickup Create(GameObject target, Kind type, string label)
        {
            Pickup pickup = target.AddComponent<Pickup>();
            pickup.Type = type;
            pickup.Label = label;
            return pickup;
        }

        private void OnEnable() => All.Add(this);
        private void OnDisable() => All.Remove(this);

        // Closest pickup within reach of the player, or null.
        public static Pickup Nearest(Vector3 position, float reach)
        {
            Pickup best = null;
            float bestDistance = reach;
            foreach (Pickup pickup in All)
            {
                float distance = Vector3.Distance(position, pickup.transform.position);
                if (distance < bestDistance)
                {
                    best = pickup;
                    bestDistance = distance;
                }
            }
            return best;
        }
    }
}
