using System.Collections.Generic;
using UnityEngine;

namespace Hushwater
{
    // Something Nell can pick up with E: a tool, salvage for boat upgrades, flash bulbs, or evidence (a register page or a tape).
    // Photographs are not pickups: they are taken with the survey camera (see SurveyCamera / PhotoSubject).
    public sealed class Pickup : MonoBehaviour
    {
        // Tools (take a slot): Bottle, Handbell, Salts, Wraps, LampOil.
        // FlashBulbs go with the camera. Brass, Wire, Lens are salvage. Evidence is a page or tape.
        public enum Kind { Bottle, Handbell, Salts, Wraps, LampOil, FlashBulbs, Brass, Wire, Lens, Evidence }

        private static readonly List<Pickup> All = new List<Pickup>();

        public Kind Type { get; private set; }
        public string Label { get; private set; }
        // Set for evidence pickups.
        public EvidenceSpot Evidence { get; private set; }

        public static bool IsTool(Kind kind) => kind <= Kind.LampOil;
        public static bool IsSalvage(Kind kind) => kind == Kind.Brass || kind == Kind.Wire || kind == Kind.Lens;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => All.Clear();

        // Turns any object into a pickup.
        public static Pickup Create(GameObject target, Kind type, string label, EvidenceSpot evidence = null)
        {
            Pickup pickup = target.AddComponent<Pickup>();
            pickup.Type = type;
            pickup.Label = label;
            pickup.Evidence = evidence;
            return pickup;
        }

        // Pickups register themselves so the player can search them without physics triggers.
        private void OnEnable() => All.Add(this);
        private void OnDisable() => All.Remove(this);

        // A slow spin so small pickups catch the lantern light. Pages and tapes lie still.
        private void Update()
        {
            if (Type == Kind.Evidence) return;
            transform.Rotate(Vector3.up, 40f * Time.deltaTime, Space.World);
        }

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

        // Player-facing name of a kind.
        public static string Name(Kind kind)
        {
            switch (kind)
            {
                case Kind.Bottle: return "bottle";
                case Kind.Handbell: return "handbell";
                case Kind.Salts: return "smelling salts";
                case Kind.Wraps: return "oilcloth wraps";
                case Kind.LampOil: return "lamp oil";
                case Kind.FlashBulbs: return "flash bulbs";
                case Kind.Brass: return "brass fitting";
                case Kind.Wire: return "copper wire";
                case Kind.Lens: return "lens glass";
                default: return "evidence";
            }
        }

        // Short label for a tool slot.
        public static string SlotName(Kind kind)
        {
            switch (kind)
            {
                case Kind.Bottle: return "BOTTLE";
                case Kind.Handbell: return "HANDBELL";
                case Kind.Salts: return "SALTS";
                case Kind.Wraps: return "WRAPS";
                case Kind.LampOil: return "LAMP OIL";
                default: return "";
            }
        }

        // How many of a tool fit in one slot. Design doc: carry 2 handbells, 2 wraps; lamp oil uses up a slot.
        // Bottle and salts limits are ours.
        public static int StackLimit(Kind kind)
        {
            switch (kind)
            {
                case Kind.Bottle: return 5;
                case Kind.Handbell: return 2;
                case Kind.Salts: return 2;
                case Kind.Wraps: return 2;
                default: return 1;
            }
        }
    }
}
