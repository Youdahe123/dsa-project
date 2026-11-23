using System;
using UnityEngine;

namespace Hushwater
{
    // What made a sound. Used for captions, the HUD ripple and the debug view.
    public enum NoiseKind { Creep, Walk, Mud, Sprint, Gasp, Crank, Door, Rummage, Salts, Bottle, Handbell, Photo, Flash, Whine, Wail, Hiss, Struggle, Fall, Generator, Spillway, Cabinet, Splash }

    // One sound in the world: where it happened, how far away it can be heard, and what it was.
    public readonly struct NoiseEvent
    {
        public readonly Vector3 Position;
        public readonly float Radius;
        public readonly NoiseKind Kind;

        // Lures (thrown bottles and handbells) send the Sexton straight to investigate, skipping the 2-and-3 sound rule.
        public bool IsLure => Kind == NoiseKind.Bottle || Kind == NoiseKind.Handbell;

        // Packs one sound into an event.
        public NoiseEvent(Vector3 position, float radius, NoiseKind kind)
        {
            Position = position;
            Radius = radius;
            Kind = kind;
        }
    }

    // Global noise bus: anything can Emit a sound, and listeners (the Sexton, the HUD) subscribe to Emitted.
    public static class NoisePulse
    {
        // Noise ranges in metres, from the design doc's "How far your noise carries" table.
        public const float Creep = 2f;
        public const float Walk = 6f;
        public const float Mud = 10f;
        public const float Crank = 12f;
        public const float Sprint = 18f;
        public const float Door = 25f;
        public const float Gasp = 30f;
        // Smelling salts: "a sharp sniff: 5 m of noise" (Pickups page).
        public const float Salts = 5f;
        // Not in the doc: how far a bottle or handbell carries. Tune in playtests.
        public const float Lure = 22f;
        // Not in the doc: rummaging through a supply cache, treated as loud as walking.
        public const float Rummage = 6f;
        // Design doc: "Photo, no flash 4 m", "Photo with flash 20 m".
        public const float Photo = 4f;
        public const float Flash = 20f;
        // Not in the doc: the flash's charging whine, quieter than the flash itself.
        public const float Whine = 8f;
        // Upgrades: felt-lined boots (walking 6 -> 4 m), geared crank (cranking 12 -> 7 m).
        public const float WalkFelt = 4f;
        public const float CrankGeared = 7f;
        // Congregation: "Lit for 3 s: a wail heard 30 m away". Lantern Moths: "Their hiss reaches the Sexton at 10 m".
        public const float Wail = 30f;
        public const float Hiss = 10f;
        // Not in the doc: each tap to break a Silt Hands grip, a crash through a rotten floor, the Night 5 generator,
        // the spillway wheel and surfacing from a flooded passage.
        public const float Struggle = 6f;
        public const float Fall = 20f;
        public const float Generator = 20f;
        public const float Spillway = 25f;
        public const float Splash = 8f;

        // While a bell tolls, every sound is drowned out (design doc: "His bell drowns all sound for 30 s").
        // Night 5's chapel bell sets this too.
        public static float DrownedUntil;
        public static bool Drowned => Time.time < DrownedUntil;

        public static event Action<NoiseEvent> Emitted;

        // Broadcasts a sound to every listener. Each listener decides if it was close enough to hear it.
        public static void Emit(Vector3 position, float radius, NoiseKind kind)
        {
            if (radius <= 0f) return;
            Emitted?.Invoke(new NoiseEvent(position, radius, kind));
        }

        // Short name for the HUD ripple and the debug view.
        public static string Label(NoiseKind kind)
        {
            switch (kind)
            {
                case NoiseKind.Creep: return "creeping";
                case NoiseKind.Walk: return "walking";
                case NoiseKind.Mud: return "deep mud";
                case NoiseKind.Sprint: return "sprinting";
                case NoiseKind.Gasp: return "gasp";
                case NoiseKind.Crank: return "cranking";
                case NoiseKind.Door: return "forcing a door";
                case NoiseKind.Rummage: return "rummaging";
                case NoiseKind.Salts: return "sniff";
                case NoiseKind.Bottle: return "bottle";
                case NoiseKind.Handbell: return "handbell";
                case NoiseKind.Photo: return "camera shutter";
                case NoiseKind.Flash: return "flash";
                case NoiseKind.Whine: return "flash charging";
                case NoiseKind.Wail: return "a wail";
                case NoiseKind.Hiss: return "moth hiss";
                case NoiseKind.Struggle: return "struggling";
                case NoiseKind.Fall: return "falling through the floor";
                case NoiseKind.Generator: return "generator";
                case NoiseKind.Spillway: return "spillway wheel";
                case NoiseKind.Cabinet: return "forcing a cabinet";
                default: return "splashing";
            }
        }

        // Clear the event between play sessions, in case domain reload is ever turned off in Enter Play Mode settings.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            Emitted = null;
            DrownedUntil = 0f;
        }
    }
}
