using UnityEngine;

namespace Hushwater
{
    // A place Nell can hide (the wardrobe). The player finds it through its trigger collider.
    public sealed class HideSpot : MonoBehaviour
    {
        // Where Nell steps out to when she leaves; falls back to the hide spot itself.
        [SerializeField] private Transform exitPoint;

        public Transform ExitPoint => exitPoint != null ? exitPoint : transform;

        // Set by the bootstrap when it builds the wardrobe.
        public void SetExitPoint(Transform point) => exitPoint = point;
    }
}
