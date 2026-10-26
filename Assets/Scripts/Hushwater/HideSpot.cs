using UnityEngine;

namespace Hushwater
{
    public sealed class HideSpot : MonoBehaviour
    {
        [SerializeField] private Transform exitPoint;

        public Transform ExitPoint => exitPoint != null ? exitPoint : transform;

        public void SetExitPoint(Transform point) => exitPoint = point;
    }
}
