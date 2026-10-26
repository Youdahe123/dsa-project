using System.Collections;
using UnityEngine;

namespace Hushwater
{
    // Thrown lures: "He walks to where it lands, which clears your path."
    public sealed class Throwable : MonoBehaviour
    {
        private const float BottleRadius = 22f;
        private const float HandbellRadius = 26f;
        private PickupKind kind;
        private bool landed;

        public static void Throw(PickupKind kind, Vector3 position, Vector3 direction)
        {
            GameObject item = BuildVisual(kind, null, position);
            item.name = "Thrown " + kind;
            Rigidbody body = item.AddComponent<Rigidbody>();
            body.mass = 0.3f;
            body.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            body.linearVelocity = direction.normalized * 13f + Vector3.up * 2.5f;
            body.angularVelocity = Random.insideUnitSphere * 8f;
            item.AddComponent<Throwable>().kind = kind;
        }

        public static GameObject BuildVisual(PickupKind kind, Transform parent, Vector3 position)
        {
            GameObject root = new GameObject(kind.ToString());
            root.transform.SetParent(parent, false);
            root.transform.position = position;
            switch (kind)
            {
                case PickupKind.Bottle:
                {
                    Material glass = HushwaterAssets.Plain(new Color(0.08f, 0.2f, 0.12f), 0.92f);
                    Part(PrimitiveType.Cylinder, root.transform, new Vector3(0f, 0.11f, 0f), new Vector3(0.08f, 0.11f, 0.08f), glass);
                    Part(PrimitiveType.Cylinder, root.transform, new Vector3(0f, 0.27f, 0f), new Vector3(0.03f, 0.06f, 0.03f), glass);
                    root.AddComponent<CapsuleCollider>().height = 0.32f;
                    root.GetComponent<CapsuleCollider>().radius = 0.045f;
                    root.GetComponent<CapsuleCollider>().center = Vector3.up * 0.16f;
                    break;
                }
                case PickupKind.Handbell:
                {
                    Material brass = HushwaterAssets.Plain(new Color(0.45f, 0.33f, 0.16f), 0.55f, 0.85f);
                    Mesh bell = MeshKit.Lathe(new[] { new Vector2(0.075f, 0f), new Vector2(0.065f, 0.02f), new Vector2(0.05f, 0.07f), new Vector2(0.04f, 0.11f), new Vector2(0.0f, 0.12f) }, 16);
                    MeshKit.Solid("Bell", root.transform, bell, brass, position, Quaternion.identity, false);
                    Part(PrimitiveType.Cylinder, root.transform, new Vector3(0f, 0.18f, 0f), new Vector3(0.025f, 0.07f, 0.025f), HushwaterAssets.Surface("dark_wooden_planks", new Color(0.5f, 0.4f, 0.3f), 0.3f));
                    SphereCollider sphere = root.AddComponent<SphereCollider>();
                    sphere.radius = 0.08f;
                    sphere.center = Vector3.up * 0.08f;
                    break;
                }
                default:
                {
                    Material vial = HushwaterAssets.Plain(new Color(0.55f, 0.6f, 0.62f), 0.9f);
                    Part(PrimitiveType.Cylinder, root.transform, new Vector3(0f, 0.06f, 0f), new Vector3(0.05f, 0.06f, 0.05f), vial);
                    Part(PrimitiveType.Cylinder, root.transform, new Vector3(0f, 0.13f, 0f), new Vector3(0.035f, 0.015f, 0.035f), HushwaterAssets.Plain(new Color(0.2f, 0.12f, 0.08f), 0.2f));
                    root.AddComponent<BoxCollider>().size = new Vector3(0.12f, 0.16f, 0.12f);
                    root.GetComponent<BoxCollider>().center = Vector3.up * 0.08f;
                    break;
                }
            }
            return root;
        }

        private static void Part(PrimitiveType type, Transform parent, Vector3 localPosition, Vector3 scale, Material material)
        {
            GameObject part = GameObject.CreatePrimitive(type);
            HushwaterAssets.Discard(part.GetComponent<Collider>());
            part.transform.SetParent(parent, false);
            part.transform.localPosition = localPosition;
            part.transform.localScale = scale;
            part.GetComponent<Renderer>().sharedMaterial = material;
        }

        private void Start() => Destroy(gameObject, 12f);

        private void OnCollisionEnter(Collision collision)
        {
            if (landed) return;
            landed = true;
            if (kind == PickupKind.Bottle)
            {
                SoundFx.PlayAt(ProceduralAudio.Smash, transform.position, 1f, 35f);
                NoisePulse.Emit(transform.position, BottleRadius, NoiseKind.Lure);
                NightDirector.Instance?.Caption("[Glass shatters in the dark]", 2.5f);
                Destroy(gameObject, 0.05f);
            }
            else
            {
                StartCoroutine(Ring());
            }
        }

        // The handbell rings for 6 s where it lands.
        private IEnumerator Ring()
        {
            NightDirector.Instance?.Caption("[A handbell rings, away from you]", 3f);
            for (int i = 0; i < 4; i++)
            {
                SoundFx.PlayAt(ProceduralAudio.Handbell, transform.position, 0.9f, 40f, 1f + Random.Range(-0.03f, 0.03f));
                NoisePulse.Emit(transform.position, HandbellRadius, NoiseKind.Lure);
                yield return new WaitForSeconds(1.5f);
            }
        }
    }
}
