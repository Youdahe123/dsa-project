using System.Collections.Generic;
using UnityEngine;

namespace Hushwater
{
    // A place Nell can hide: a wardrobe, under a pew, or beneath the jetty. The player finds it through its trigger collider.
    public sealed class HideSpot : MonoBehaviour
    {
        private static readonly List<HideSpot> All = new List<HideSpot>();

        // Where Nell steps out to when she leaves; falls back to the hide spot itself.
        [SerializeField] private Transform exitPoint;

        public Transform ExitPoint => exitPoint != null ? exitPoint : transform;
        // "wardrobe", "pew", "jetty": used in prompts ("E  hide under the pew").
        public string Noun { get; private set; } = "wardrobe";
        public string HidePhrase => Noun == "wardrobe" ? "hide in the wardrobe" : Noun == "jetty" ? "hide beneath the jetty" : "hide under the pew";
        // Stable name so the Sexton can remember it from night to night.
        public string Id { get; private set; } = "";
        // How many times Nell has hidden here, over the whole run.
        public int TimesUsed => Campaign.HideCount(Id);

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => All.Clear();

        // Hide spots register themselves so the Sexton can find the ones near him.
        private void OnEnable() => All.Add(this);
        private void OnDisable() => All.Remove(this);

        // Set by the town builder.
        public void Setup(Transform exit, string noun, string id)
        {
            exitPoint = exit;
            Noun = noun;
            Id = id;
        }

        public void SetExitPoint(Transform point) => exitPoint = point;

        // Called by the player each time she climbs in.
        public void MarkUsed() => Campaign.NoteHide(Id);

        // Every hide spot within range, nearest first.
        // Design doc: "Search: feels inside hiding spots within 10 m", and "After Night 3 he checks your favourite spot first."
        public static List<HideSpot> Within(Vector3 point, float range)
        {
            var found = new List<HideSpot>();
            foreach (HideSpot spot in All)
                if (Vector3.Distance(point, spot.ExitPoint.position) <= range) found.Add(spot);
            bool learned = Campaign.Night > 3;
            found.Sort((a, b) => learned && a.TimesUsed != b.TimesUsed
                ? b.TimesUsed.CompareTo(a.TimesUsed)
                : Vector3.Distance(point, a.ExitPoint.position).CompareTo(Vector3.Distance(point, b.ExitPoint.position)));
            return found;
        }
    }
}
