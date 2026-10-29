using UnityEngine;

namespace Hushwater
{
    // The Sexton's 2.4 m frame, built from primitives and animated in code (no Animator needed).
    public sealed class SextonBody : MonoBehaviour
    {
        private Transform torso;
        private Transform head;
        private Transform leftArm;
        private Transform rightArm;
        private float phase;
        private float swipe;
        private float grab;
        private bool grabbing;

        // Where a grabbed player's eyes are held: lifted up to his face, just in front of his hands.
        public Vector3 GripPoint => transform.position + transform.forward * 1.25f + Vector3.up * 1.85f;
        public Transform Head => head;

        // Builds the whole body as children of root (the NavMeshAgent object) and returns its animator.
        public static SextonBody Build(Transform root)
        {
            Material cloth = HushwaterAssets.Surface("worn_mossy_plasterwall", new Color(0.12f, 0.11f, 0.1f));
            Material skin = new Material(Shader.Find("Standard")) { color = new Color(0.45f, 0.47f, 0.42f) };
            Material iron = HushwaterAssets.Surface("rusty_metal_02", new Color(0.35f, 0.32f, 0.3f), 1f, 0.35f);
            Material blindfold = new Material(Shader.Find("Standard")) { color = new Color(0.04f, 0.035f, 0.03f) };

            GameObject visual = new GameObject("Sexton Body");
            visual.transform.SetParent(root, false);
            SextonBody body = visual.AddComponent<SextonBody>();

            // Long coat down to the ground, then a torso pivot at the hips for leaning.
            Part(PrimitiveType.Cylinder, visual.transform, "Coat Hem", new Vector3(0f, 0.55f, 0f), new Vector3(0.75f, 0.55f, 0.6f), cloth);
            body.torso = Pivot(visual.transform, "Torso", new Vector3(0f, 1.05f, 0f));
            Part(PrimitiveType.Capsule, body.torso, "Coat", new Vector3(0f, 0.45f, 0f), new Vector3(0.62f, 0.55f, 0.45f), cloth);

            // Hooded head with a blindfold: he has no eyes to see Nell, only ears.
            body.head = Pivot(body.torso, "Head", new Vector3(0f, 1.15f, 0.08f));
            Part(PrimitiveType.Sphere, body.head, "Skull", Vector3.zero, new Vector3(0.24f, 0.32f, 0.26f), skin);
            Part(PrimitiveType.Sphere, body.head, "Hood", new Vector3(0f, 0.05f, -0.08f), new Vector3(0.31f, 0.37f, 0.3f), cloth);
            Part(PrimitiveType.Cube, body.head, "Blindfold", new Vector3(0f, 0.01f, 0.1f), new Vector3(0.27f, 0.08f, 0.1f), blindfold);

            body.leftArm = Arm(body.torso, -1f, cloth, skin);
            body.rightArm = Arm(body.torso, 1f, cloth, skin);

            // The hook he swipes with, held in the right hand.
            Transform hook = Part(PrimitiveType.Cylinder, body.rightArm, "Hook Staff", new Vector3(0f, -1.1f, 0.25f), new Vector3(0.05f, 0.45f, 0.05f), iron).transform;
            hook.localRotation = Quaternion.Euler(30f, 0f, 0f);

            // Only the NavMeshAgent should collide; the body is visual.
            foreach (Collider c in visual.GetComponentsInChildren<Collider>()) Destroy(c);
            return body;
        }

        // A shoulder pivot with a sleeve and a hand; rotating the pivot swings the whole arm.
        private static Transform Arm(Transform torso, float side, Material cloth, Material skin)
        {
            Transform shoulder = Pivot(torso, side < 0f ? "Left Arm" : "Right Arm", new Vector3(side * 0.36f, 0.85f, 0f));
            Part(PrimitiveType.Capsule, shoulder, "Sleeve", new Vector3(0f, -0.35f, 0f), new Vector3(0.14f, 0.38f, 0.14f), cloth);
            Part(PrimitiveType.Capsule, shoulder, "Hand", new Vector3(0f, -0.85f, 0f), new Vector3(0.09f, 0.18f, 0.09f), skin);
            return shoulder;
        }

        // An empty transform used as a joint to rotate parts around.
        private static Transform Pivot(Transform parent, string name, Vector3 local)
        {
            Transform pivot = new GameObject(name).transform;
            pivot.SetParent(parent, false);
            pivot.localPosition = local;
            return pivot;
        }

        // One visible piece of the body: a primitive with a shared material.
        private static GameObject Part(PrimitiveType type, Transform parent, string name, Vector3 local, Vector3 scale, Material material)
        {
            GameObject part = GameObject.CreatePrimitive(type);
            part.name = name;
            part.transform.SetParent(parent, false);
            part.transform.localPosition = local;
            part.transform.localScale = scale;
            part.GetComponent<Renderer>().sharedMaterial = material;
            return part;
        }

        // Starts a one-off hook swipe with the right arm; it fades back over about 0.4 s.
        public void Swipe() => swipe = 1f;

        // Raises both arms toward Nell and holds them there for the kill.
        public void Grab() => grabbing = true;

        // Called every frame by SextonController with his current speed.
        public void Animate(float speed, bool hunting, float dt)
        {
            phase += dt * (1.5f + speed * 1.6f);
            swipe = Mathf.MoveTowards(swipe, 0f, dt * 2.5f);
            grab = Mathf.MoveTowards(grab, grabbing ? 1f : 0f, dt * 4f);

            // Hunched forward when hunting, bobbing with each stride.
            float lean = Mathf.Lerp(hunting ? 25f : 10f, 35f, grab);
            float bob = Mathf.Abs(Mathf.Sin(phase)) * 0.05f * Mathf.Clamp01(speed);
            torso.localPosition = new Vector3(0f, 1.05f + bob, 0f);
            torso.localRotation = Quaternion.Euler(lean, 0f, Mathf.Sin(phase) * 3f);

            // Blind: the head cocks side to side, listening. During a grab it lifts to face Nell.
            head.localRotation = Quaternion.Euler(Mathf.Lerp(-lean * 0.6f, -lean - 10f, grab), Mathf.Sin(Time.time * 0.7f) * 25f * (1f - grab), 15f + Mathf.Sin(Time.time) * 8f);

            // Arms swing while walking; a swipe whips the right arm forward; a grab raises both.
            float swing = Mathf.Sin(phase) * 20f * Mathf.Clamp01(speed / 2f);
            float swipeAngle = -Mathf.Sin(swipe * Mathf.PI) * 110f;
            leftArm.localRotation = Quaternion.Euler(Mathf.Lerp(-swing, -125f, grab), 0f, -6f);
            rightArm.localRotation = Quaternion.Euler(Mathf.Lerp(swing - 20f + swipeAngle, -125f, grab), 0f, 6f);
        }
    }
}
