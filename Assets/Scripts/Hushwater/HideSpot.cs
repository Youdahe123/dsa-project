using System.Collections.Generic;
using UnityEngine;

namespace Hushwater
{
    public sealed class HideSpot : MonoBehaviour, IInteractable
    {
        public static readonly List<HideSpot> All = new List<HideSpot>();

        [SerializeField] private Transform exitPoint;
        [SerializeField] private Transform viewPoint;
        public string Label = "wardrobe";

        public Transform ExitPoint => exitPoint != null ? exitPoint : transform;
        public Transform ViewPoint => viewPoint != null ? viewPoint : transform;
        public float HoldSeconds => 0f;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => All.Clear();

        public void Setup(Transform exit, Transform view)
        {
            exitPoint = exit;
            viewPoint = view;
        }

        private void OnEnable() => All.Add(this);
        private void OnDisable() => All.Remove(this);

        public string Prompt(PlayerController player) => $"E — hide in the {Label}";

        public void Interact(PlayerController player) => player.EnterHideSpot(this);
    }
}
