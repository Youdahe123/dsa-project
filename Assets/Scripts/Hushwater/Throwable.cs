using UnityEngine;

namespace Hushwater
{
    // Something Nell throws with Q. A bottle shatters on first impact; a handbell lands and keeps ringing.
    // Either way the Sexton walks to where it lands, which clears her path.
    public sealed class Throwable : MonoBehaviour
    {
        // Design doc: "Handbell. Throw it: rings for 6 s where it lands."
        private const float RingSeconds = 6f;
        private const float RingInterval = 1f;

        private bool isBell;
        private bool landed;
        private float ringTime;
        private float ringTimer;

        // Spawns the thrown object with a physics body and launches it forward with a slight upward arc.
        public static void Throw(Vector3 position, Vector3 direction, bool handbell)
        {
            GameObject item = GameObject.CreatePrimitive(handbell ? PrimitiveType.Sphere : PrimitiveType.Cylinder);
            item.name = handbell ? "Thrown Handbell" : "Thrown Bottle";
            item.transform.position = position;
            item.transform.localScale = handbell ? Vector3.one * 0.16f : new Vector3(0.12f, 0.15f, 0.12f);
            item.GetComponent<Renderer>().material.color = handbell ? new Color(0.7f, 0.55f, 0.25f) : new Color(0.1f, 0.35f, 0.2f);
            Rigidbody body = item.AddComponent<Rigidbody>();
            body.mass = handbell ? 0.5f : 0.25f;
            body.linearVelocity = direction.normalized * 12f + Vector3.up * 3f;
            item.AddComponent<Throwable>().isBell = handbell;
        }

        // Clean up throws that never hit anything.
        private void Start() => Destroy(gameObject, isBell ? 14f : 8f);

        // A landed handbell keeps sending lure pulses until it stops ringing.
        private void Update()
        {
            if (!landed || !isBell || ringTime <= 0f) return;
            ringTime -= Time.deltaTime;
            ringTimer -= Time.deltaTime;
            if (ringTimer > 0f) return;
            ringTimer = RingInterval;
            Ring();
        }

        // Only the first impact counts, so a bouncing bottle makes one noise.
        private void OnCollisionEnter(Collision collision)
        {
            if (landed) return;
            landed = true;
            if (isBell)
            {
                ringTime = RingSeconds;
                ringTimer = RingInterval;
                Ring();
                return;
            }
            NoisePulse.Emit(transform.position, NoisePulse.Lure, NoiseKind.Bottle);
            ProceduralSounds.PlayAt(ProceduralSounds.Shatter, transform.position);
            Destroy(gameObject, 0.2f);
        }

        // One ring of the handbell: a sound plus a lure pulse.
        private void Ring()
        {
            NoisePulse.Emit(transform.position, NoisePulse.Lure, NoiseKind.Handbell);
            ProceduralSounds.PlayAt(ProceduralSounds.Handbell, transform.position, 0.8f);
        }
    }
}
