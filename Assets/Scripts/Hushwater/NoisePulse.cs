using System;
using UnityEngine;

namespace Hushwater
{
    // One sound in the world: where it happened and how far away it can be heard.
    public readonly struct NoiseEvent
    {
        public readonly Vector3 Position;
        public readonly float Radius;
        // Lures (thrown bottles) send the Sexton straight to investigate, skipping the 2-and-3 sound rule.
        public readonly bool IsLure;

        public NoiseEvent(Vector3 position, float radius, bool isLure)
        {
            Position = position;
            Radius = radius;
            IsLure = isLure;
        }
    }

    // Global noise bus: anything can Emit a sound, and listeners (the Sexton) subscribe to Emitted.
    public static class NoisePulse
    {
        // Noise ranges in metres, from the design doc.
        public const float Creep = 2f;
        public const float Walk = 6f;
        public const float Sprint = 18f;
        public const float Gasp = 30f;

        public static event Action<NoiseEvent> Emitted;

        // Broadcasts a sound to every listener. Each listener decides if it was close enough to hear it.
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
