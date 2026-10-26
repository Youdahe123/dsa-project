using System;
using UnityEngine;

namespace Hushwater
{
    public readonly struct NoiseEvent
    {
        public readonly Vector3 Position;
        public readonly float Radius;
        public readonly bool IsLure;

        public NoiseEvent(Vector3 position, float radius, bool isLure)
        {
            Position = position;
            Radius = radius;
            IsLure = isLure;
        }
    }

    public static class NoisePulse
    {
        // Noise ranges in metres, from the design doc.
        public const float Creep = 2f;
        public const float Walk = 6f;
        public const float Sprint = 18f;
        public const float Gasp = 30f;

        public static event Action<NoiseEvent> Emitted;

        public static void Emit(Vector3 position, float radius, bool isLure = false)
        {
            if (radius <= 0f) return;
            Emitted?.Invoke(new NoiseEvent(position, radius, isLure));
        }

        // Domain reload is off in this project, so clear the event between play sessions.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => Emitted = null;
    }
}
