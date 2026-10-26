using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

namespace Hushwater
{
    public interface IInteractable
    {
        string Prompt(PlayerController player);
        // 0 for a single press of E, otherwise how long E must be held.
        float HoldSeconds { get; }
        void Interact(PlayerController player);
    }

    // Marks colliders that only fence the player in; interaction rays pass through them.
    public sealed class InvisibleWall : MonoBehaviour { }

    public sealed class MudSurface : MonoBehaviour
    {
        public bool Deep;
    }

    // "Mud remembers": Nell's footprints, which the Sexton follows by touch.
    public static class MudTrail
    {
        private struct Print { public Vector3 Position; public float Time; }
        private static readonly List<Print> Prints = new List<Print>();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        public static void Clear() => Prints.Clear();

        public static void Add(Vector3 position)
        {
            Prints.Add(new Print { Position = position, Time = Time.time });
            if (Prints.Count > 240) Prints.RemoveAt(0);
        }

        // Finds the newest print near `from` that is younger than maxAge, ahead of `after` in time.
        public static bool Follow(Vector3 from, float radius, float maxAge, ref float after, out Vector3 next)
        {
            next = from;
            bool found = false;
            for (int i = Prints.Count - 1; i >= 0; i--)
            {
                Print p = Prints[i];
                if (Time.time - p.Time > maxAge || p.Time <= after) break;
                if ((p.Position - from).sqrMagnitude > radius * radius) continue;
                // Jump to the newest print within reach, then keep following forward.
                next = p.Position;
                after = p.Time;
                found = true;
                break;
            }
            return found;
        }
    }

    public sealed class EvidenceSite : MonoBehaviour
    {
        public string Title;
        public string Note;
        public bool Dark;
        public bool Required = true;
        public float Range = 7f;
        public Vector3 FocusOffset = Vector3.up;

        public bool Found { get; private set; }
        public Vector3 FocusPoint => transform.position + FocusOffset;
        public void MarkFound() => Found = true;
    }

    public sealed class LanternPost : MonoBehaviour, IInteractable
    {
        private Light glow;
        private Renderer flame;
        public bool Lit { get; private set; }
        public Transform RespawnPoint { get; private set; }

        public float HoldSeconds => Lit ? 0f : 2f;

        public void Setup(Light light, Renderer flameRenderer, Transform respawn)
        {
            glow = light;
            flame = flameRenderer;
            RespawnPoint = respawn;
            glow.enabled = false;
            if (flame != null) flame.enabled = false;
        }

        public string Prompt(PlayerController player) => Lit ? "Lantern post — lit (checkpoint)" : "Hold E — crank the lantern post alight (12 m of noise)";

        public void Interact(PlayerController player)
        {
            if (Lit) return;
            Lit = true;
            glow.enabled = true;
            if (flame != null) flame.enabled = true;
            NoisePulse.Emit(transform.position, NoisePulse.LanternCrank, NoiseKind.Lantern);
            NightDirector.Instance.SetCheckpoint(this);
        }

        private void Update()
        {
            if (Lit) glow.intensity = 1.6f + Mathf.PerlinNoise(Time.time * 7f, transform.position.x) * 0.5f;
        }
    }

    public enum PickupKind { Handbell, Bottle, Salts }

    public sealed class Pickup : MonoBehaviour, IInteractable
    {
        public PickupKind Kind;
        public float HoldSeconds => 0f;

        public string Prompt(PlayerController player) => Kind switch
        {
            PickupKind.Handbell => "E — take the handbell",
            PickupKind.Bottle => "E — take the bottle",
            _ => "E — take the smelling salts"
        };

        public void Interact(PlayerController player)
        {
            if (!player.AddTool(Kind)) return;
            SoundFx.PlayAt(ProceduralAudio.UiTick, transform.position, 0.5f, 6f, 0.6f);
            Destroy(gameObject);
        }
    }

    public sealed class BoatExit : MonoBehaviour, IInteractable
    {
        public float HoldSeconds => 0f;

        public string Prompt(PlayerController player)
        {
            NightDirector d = NightDirector.Instance;
            return d.RequiredFound >= d.RequiredEvidence
                ? "E — row out before the dawn bell"
                : $"The boat. You need {d.RequiredEvidence} photographs ({d.RequiredFound} so far)";
        }

        public void Interact(PlayerController player) => NightDirector.Instance.TryLeave();
    }

    // "Barred Doors": nailed shut on the flood night. Forcing one is the loudest thing you can do.
    public sealed class BarredDoor : MonoBehaviour, IInteractable
    {
        public Transform Leaf;
        public GameObject Bars;
        private bool open;

        public float HoldSeconds => open ? 0f : 1.6f;

        public string Prompt(PlayerController player) => open ? "" : "Hold E — force the barred door (25 m: the loudest thing you can do)";

        public void Interact(PlayerController player)
        {
            if (open) return;
            open = true;
            NoisePulse.Emit(transform.position, NoisePulse.ForceDoor, NoiseKind.Door);
            SoundFx.PlayAt(ProceduralAudio.DoorForce, transform.position, 1f, 40f);
            if (Bars != null) Destroy(Bars);
            foreach (NavMeshObstacle obstacle in GetComponentsInChildren<NavMeshObstacle>()) obstacle.enabled = false;
            StartCoroutine(Swing());
        }

        private IEnumerator Swing()
        {
            Quaternion start = Leaf.localRotation;
            Quaternion end = start * Quaternion.Euler(0f, -105f, 0f);
            for (float t = 0f; t < 1f; t += Time.deltaTime * 1.6f)
            {
                Leaf.localRotation = Quaternion.Slerp(start, end, 1f - (1f - t) * (1f - t));
                yield return null;
            }
            Leaf.localRotation = end;
        }
    }
}
