using System;
using UnityEngine;

namespace Hushwater
{
    public enum NoiseKind { Footstep, Breathing, Photo, Flash, Lantern, Door, Lure, Gasp, Interact }

    public readonly struct NoiseEvent
    {
        public readonly Vector3 Position;
        public readonly float Radius;
        public readonly NoiseKind Kind;

        public NoiseEvent(Vector3 position, float radius, NoiseKind kind)
        {
            Position = position;
            Radius = radius;
            Kind = kind;
        }

        // 0..1 against the loudest sound in the doc's table (a gasp, 30 m).
        public float Loudness => Mathf.Clamp01(Radius / 30f);
    }

    public static class NoisePulse
    {
        // Doc noise table, in metres.
        public const float Creep = 2f;
        public const float PhotoNoFlash = 4f;
        public const float Walk = 6f;
        public const float DeepMud = 10f;
        public const float LanternCrank = 12f;
        public const float Sprint = 18f;
        public const float Flash = 20f;
        public const float ForceDoor = 25f;
        public const float Gasp = 30f;

        public static event Action<NoiseEvent> Emitted;

        public static void Emit(Vector3 position, float radius, NoiseKind kind)
        {
            if (radius <= 0f) return;
            Emitted?.Invoke(new NoiseEvent(position, radius, kind));
        }

        // Domain reload is off in this project, so static events must be cleared by hand.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => Emitted = null;
    }
}
