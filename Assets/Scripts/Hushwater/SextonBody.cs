using UnityEngine;

namespace Hushwater
{
    // The Sexton's 2.4 m frame, built from lathed cloth and limbs and animated procedurally.
    public sealed class SextonBody : MonoBehaviour
    {
        private Transform torso;
        private Transform head;
        private Transform leftShoulder;
        private Transform rightShoulder;
        private Transform rightElbow;
        private Transform staff;
        private Transform clapper;
        private Transform skirt;
        private float phase;
        private float strike;
        private float swipe;
        private float lean;

        public Vector3 ClapperPosition => clapper != null ? clapper.position : transform.position + Vector3.up;

        public static SextonBody Build(Transform root)
        {
            Material cloth = HushwaterAssets.Surface("worn_mossy_plasterwall", new Color(0.1f, 0.095f, 0.085f), 0.32f, 1.6f);
            Material skin = HushwaterAssets.Surface("brown_mud_02", new Color(0.5f, 0.52f, 0.47f), 0.45f, 1.4f);
            Material iron = HushwaterAssets.Surface("rusty_metal_02", new Color(0.32f, 0.3f, 0.29f), 0.35f);
            Material blind = HushwaterAssets.Plain(new Color(0.05f, 0.045f, 0.04f), 0.3f);

            GameObject visual = new GameObject("Sexton body");
            visual.transform.SetParent(root, false);
            SextonBody body = visual.AddComponent<SextonBody>();

            // Long coat, its hem torn and dragging.
            body.skirt = Part("Coat skirt", visual.transform, MeshKit.Lathe(new[]
            {
                new Vector2(0.5f, 0.02f), new Vector2(0.45f, 0.3f), new Vector2(0.37f, 0.75f), new Vector2(0.3f, 1.12f)
            }, 22, 0.14f, 5), cloth, Vector3.zero, new Vector3(1.1f, 1f, 0.85f)).transform;

            body.torso = new GameObject("Torso").transform;
            body.torso.SetParent(visual.transform, false);
            body.torso.localPosition = new Vector3(0f, 1.08f, 0f);
            Part("Coat body", body.torso, MeshKit.Lathe(new[]
            {
                new Vector2(0.3f, -0.08f), new Vector2(0.27f, 0.3f), new Vector2(0.31f, 0.68f), new Vector2(0.33f, 0.78f),
                new Vector2(0.18f, 0.9f), new Vector2(0.07f, 0.95f)
            }, 20, 0.03f, 9), cloth, Vector3.zero, new Vector3(1.2f, 1f, 0.75f));

            body.head = new GameObject("Head").transform;
            body.head.SetParent(body.torso, false);
            body.head.localPosition = new Vector3(0f, 1.02f, 0.16f);
            Primitive(PrimitiveType.Sphere, body.head, "Skull", Vector3.zero, new Vector3(0.22f, 0.3f, 0.25f), skin);
            Primitive(PrimitiveType.Sphere, body.head, "Hood", new Vector3(0f, 0.03f, -0.05f), new Vector3(0.3f, 0.36f, 0.31f), cloth);
            Primitive(PrimitiveType.Cube, body.head, "Blindfold", new Vector3(0f, 0.03f, 0.1f), new Vector3(0.24f, 0.06f, 0.08f), blind);
            Primitive(PrimitiveType.Cube, body.head, "Jaw", new Vector3(0f, -0.1f, 0.08f), new Vector3(0.13f, 0.07f, 0.1f), skin);
            Primitive(PrimitiveType.Cylinder, body.torso, "Neck", new Vector3(0f, 0.92f, 0.09f), new Vector3(0.09f, 0.08f, 0.09f), skin)
                .transform.localRotation = Quaternion.Euler(25f, 0f, 0f);

            body.leftShoulder = Arm(body.torso, -1, cloth, skin, out _);
            body.rightShoulder = Arm(body.torso, 1, cloth, skin, out body.rightElbow);

            // The hook staff, held low so the hook drags through the mud ahead of him.
            body.staff = new GameObject("Hook staff").transform;
            body.staff.SetParent(body.rightElbow, false);
            body.staff.localPosition = new Vector3(0f, -0.52f, 0f);
            body.staff.localRotation = Quaternion.Euler(-35f, 0f, 0f);
            Primitive(PrimitiveType.Cylinder, body.staff, "Shaft", new Vector3(0f, -0.6f, 0f), new Vector3(0.045f, 1.25f, 0.045f), iron);
            for (int i = 0; i < 8; i++)
            {
                float a = i / 7f * Mathf.PI * 1.15f;
                Vector3 p = new Vector3(0f, -1.85f - Mathf.Sin(a) * 0.17f, 0.17f - Mathf.Cos(a) * 0.17f);
                Primitive(PrimitiveType.Cylinder, body.staff, "Hook", p, new Vector3(0.035f, 0.035f, 0.035f), iron).transform.localRotation = Quaternion.Euler(a * Mathf.Rad2Deg, 0f, 0f);
            }

            // The iron clapper, carried on a chain in the left hand.
            Transform leftHand = body.leftShoulder.Find("Elbow");
            body.clapper = new GameObject("Clapper").transform;
            body.clapper.SetParent(leftHand, false);
            body.clapper.localPosition = new Vector3(0f, -0.56f, 0f);
            Primitive(PrimitiveType.Cylinder, body.clapper, "Chain", new Vector3(0f, -0.14f, 0f), new Vector3(0.015f, 0.14f, 0.015f), iron);
            Part("Clapper weight", body.clapper, MeshKit.Lathe(new[]
            {
                new Vector2(0.0f, -0.42f), new Vector2(0.07f, -0.39f), new Vector2(0.09f, -0.33f), new Vector2(0.05f, -0.26f), new Vector2(0.02f, -0.28f)
            }, 12), iron, Vector3.zero, Vector3.one);

            foreach (Collider c in visual.GetComponentsInChildren<Collider>()) HushwaterAssets.Discard(c);
            return body;
        }

        private static Transform Arm(Transform torso, int side, Material cloth, Material skin, out Transform elbow)
        {
            Transform shoulder = new GameObject(side < 0 ? "Left shoulder" : "Right shoulder").transform;
            shoulder.SetParent(torso, false);
            shoulder.localPosition = new Vector3(side * 0.38f, 0.74f, 0.02f);
            Primitive(PrimitiveType.Capsule, shoulder, "Sleeve", new Vector3(0f, -0.3f, 0f), new Vector3(0.14f, 0.32f, 0.14f), cloth);
            elbow = new GameObject("Elbow").transform;
            elbow.SetParent(shoulder, false);
            elbow.localPosition = new Vector3(0f, -0.6f, 0f);
            Primitive(PrimitiveType.Capsule, elbow, "Forearm", new Vector3(0f, -0.26f, 0f), new Vector3(0.085f, 0.28f, 0.085f), skin);
            for (int f = 0; f < 4; f++)
            {
                Transform finger = Primitive(PrimitiveType.Capsule, elbow, "Finger", new Vector3((f - 1.5f) * 0.022f, -0.63f, 0.01f), new Vector3(0.018f, 0.1f, 0.018f), skin).transform;
                finger.localRotation = Quaternion.Euler(12f + f * 3f, 0f, (f - 1.5f) * 6f);
            }
            return shoulder;
        }

        private static GameObject Part(string name, Transform parent, Mesh mesh, Material material, Vector3 local, Vector3 scale)
        {
            GameObject go = MeshKit.Solid(name, parent, mesh, material, Vector3.zero, Quaternion.identity, false);
            go.transform.localPosition = local;
            go.transform.localRotation = Quaternion.identity;
            go.transform.localScale = scale;
            return go;
        }

        private static GameObject Primitive(PrimitiveType type, Transform parent, string name, Vector3 local, Vector3 scale, Material material)
        {
            GameObject go = GameObject.CreatePrimitive(type);
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = local;
            go.transform.localScale = scale;
            go.GetComponent<Renderer>().sharedMaterial = material;
            return go;
        }

        public void Strike() => strike = 1f;
        public void Swipe() => swipe = 1f;

        public void Animate(float speed, SextonController.State state, bool feeling, float dt)
        {
            float pace = Mathf.Clamp01(speed / 4.9f);
            phase += dt * (1.5f + speed * 1.55f);
            strike = Mathf.MoveTowards(strike, 0f, dt * 5f);
            swipe = Mathf.MoveTowards(swipe, 0f, dt * 2.5f);
            bool hunting = state == SextonController.State.Hunt;
            lean = Mathf.Lerp(lean, hunting ? 26f : 10f, dt * 3f);

            float bob = Mathf.Abs(Mathf.Sin(phase)) * (0.03f + pace * 0.06f);
            torso.localPosition = new Vector3(0f, 1.08f + bob, 0f);
            torso.localRotation = Quaternion.Euler(lean, Mathf.Sin(phase) * 4f, Mathf.Sin(phase) * (2f + pace * 3f));
            skirt.localRotation = Quaternion.Euler(pace * 6f, 0f, Mathf.Sin(phase) * 2.5f);

            // Blind: the head cocks to listen.
            bool listening = state == SextonController.State.Listen || state == SextonController.State.Investigate || feeling;
            float tilt = listening ? 24f + Mathf.Sin(Time.time * 0.9f) * 8f : Mathf.Sin(Time.time * 0.4f) * 5f;
            head.localRotation = Quaternion.Euler(-lean * 0.5f + 10f, listening ? Mathf.Sin(Time.time * 0.7f) * 35f : 0f, tilt);

            float swing = Mathf.Sin(phase) * (8f + pace * 26f);
            leftShoulder.localRotation = Quaternion.Euler(-swing - strike * 25f, 0f, -6f);
            float reach = swipe > 0f ? -Mathf.Sin(swipe * Mathf.PI) * 100f : 0f;
            float sweep = feeling ? Mathf.Sin(Time.time * 2.4f) * 40f : 0f;
            rightShoulder.localRotation = Quaternion.Euler(-28f + swing * 0.4f + reach, sweep, 8f);
            rightElbow.localRotation = Quaternion.Euler(-12f, 0f, 0f);
            clapper.localRotation = Quaternion.Euler(Mathf.Sin(phase * 2f) * 12f + strike * 35f, 0f, 0f);
        }
    }
}
