using UnityEngine;

namespace Hushwater
{
    // A bottle thrown with Q. It shatters on first impact and lures the Sexton to that spot.
    public sealed class ThrowableBottle : MonoBehaviour
    {
        [SerializeField] private float noiseRadius = 22f;
        private bool landed;

        // Spawns a bottle with a physics body and launches it forward with a slight upward arc.
        public static void Throw(Vector3 position, Vector3 direction)
        {
            GameObject bottle = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            bottle.name = "Thrown Bottle";
            bottle.transform.position = position;
            bottle.transform.localScale = new Vector3(0.12f, 0.15f, 0.12f);
            bottle.GetComponent<Renderer>().material.color = new Color(0.1f, 0.35f, 0.2f);
            Rigidbody body = bottle.AddComponent<Rigidbody>();
            body.mass = 0.25f;
            body.linearVelocity = direction.normalized * 12f + Vector3.up * 3f;
            bottle.AddComponent<ThrowableBottle>();
        }

        // Clean up bottles that never hit anything.
        private void Start() => Destroy(gameObject, 8f);

        private void OnCollisionEnter(Collision collision)
        {
            if (landed) return;
            landed = true;
            // A lure: the Sexton walks straight to where it lands.
            NoisePulse.Emit(transform.position, noiseRadius, true);
            ProceduralSounds.PlayAt(ProceduralSounds.Shatter, transform.position);
            Destroy(gameObject, 0.2f);
        }
    }
}
