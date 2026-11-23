using System.Collections.Generic;
using UnityEngine;

namespace Hushwater
{
    // The Sexton's 2.4 m frame (design doc: "the Sexton must feel impossibly tall in doorways built for people"),
    // built in code and animated in code until the Blender rig exists. A drowned bell-ringer: gaunt, hunched,
    // in a waterlogged coat that hangs in strips, his eyes bound with rags, a boat-hook in one hand and the
    // chapel bell's iron clapper chained at his belt. Water drips off him as he walks.
    public sealed class SextonBody : MonoBehaviour
    {
        // What his arms are doing, picked by SextonController each frame.
        public enum Pose { Walk, Hunt, Sweep, Feel, Ring }

        private sealed class Limb
        {
            public Transform Upper;
            public Transform Lower;
            public readonly List<Transform> Fingers = new List<Transform>();
        }

        // Long legs: he stands 2.4 m upright and stoops to about 2.1 m.
        private const float HipHeight = 1.32f;

        private Transform hips;
        private Transform spineLow;
        private Transform spineHigh;
        private Transform neck;
        private Transform head;
        private Limb leftArm;
        private Limb rightArm;
        private Limb leftLeg;
        private Limb rightLeg;
        private Transform hook;
        private Transform clapper;
        private readonly List<Transform> coatStrips = new List<Transform>();
        private readonly List<float> stripSeeds = new List<float>();

        private float phase;
        private float stride;
        private float swipe;
        private float grab;
        private float smash;
        private bool grabbing;
        private float sweep;
        private float feel;
        private float ring;
        private float hunt;
        // The last sound he heard, in his own space, and how fresh it is: his head turns an ear toward it.
        private Vector3 heardAt;
        private float heardFresh;

        // Where a grabbed player's eyes are held: lifted up to his face, just in front of his hands.
        public Vector3 GripPoint => transform.position + transform.forward * 1.25f + Vector3.up * 1.85f;
        public Transform Head => head;
        // The tip of his hook, which he drags through the mud to feel for Nell's grooves.
        public Vector3 HookTip => transform.position + transform.forward * 1.1f + transform.right * 0.4f;

        public static SextonBody Build(Transform root)
        {
            // Waterlogged wool: dark and matte, with a little wet sheen at the hem.
            Material coat = HushwaterAssets.Surface("worn_mossy_plasterwall", new Color(0.075f, 0.075f, 0.07f), 2f, 0.12f);
            Material coatEdge = HushwaterAssets.Surface("brown_mud_02", new Color(0.13f, 0.12f, 0.1f), 2f, 0.22f);
            // Drowned skin: grey-green, wrinkled, slick.
            Material skin = HushwaterAssets.Surface("worn_mossy_plasterwall", new Color(0.3f, 0.33f, 0.31f), 3f, 0.5f);
            Material rag = HushwaterAssets.Surface("worn_mossy_plasterwall", new Color(0.42f, 0.39f, 0.32f), 1f, 0.1f);
            Material iron = HushwaterAssets.Surface("rusty_metal_02", new Color(0.38f, 0.33f, 0.3f), 1f, 0.35f);
            Material wood = HushwaterAssets.Surface("weathered_plank_siding", new Color(0.25f, 0.21f, 0.17f), 1f);
            Material hollow = new Material(Shader.Find("Standard")) { color = new Color(0.01f, 0.01f, 0.01f) };
            Material brass = new Material(Shader.Find("Standard")) { color = new Color(0.42f, 0.34f, 0.17f) };
            brass.SetFloat("_Metallic", 0.8f);

            GameObject visual = new GameObject("Sexton Body");
            visual.transform.SetParent(root, false);
            SextonBody body = visual.AddComponent<SextonBody>();
            Transform v = visual.transform;

            // ---------- Legs: long and thin, bare grey shins showing under the coat ----------
            body.hips = Pivot(v, "Hips", new Vector3(0f, HipHeight, 0f));
            body.leftLeg = Leg(body.hips, -1f, coat, skin);
            body.rightLeg = Leg(body.hips, 1f, coat, skin);

            // ---------- Spine: two bends, so he hunches like a question mark ----------
            Part(PrimitiveType.Sphere, body.hips, "Hip Drape", new Vector3(0f, 0.05f, 0f), new Vector3(0.5f, 0.34f, 0.4f), coat);
            body.spineLow = Pivot(body.hips, "Spine Low", new Vector3(0f, 0.12f, 0f));
            Part(PrimitiveType.Capsule, body.spineLow, "Waist", new Vector3(0f, 0.22f, 0f), new Vector3(0.42f, 0.3f, 0.3f), coat);
            Part(PrimitiveType.Cylinder, body.spineLow, "Belt", new Vector3(0f, 0.05f, 0f), new Vector3(0.44f, 0.04f, 0.32f), iron);
            body.spineHigh = Pivot(body.spineLow, "Spine High", new Vector3(0f, 0.42f, 0f));
            Part(PrimitiveType.Capsule, body.spineHigh, "Chest", new Vector3(0f, 0.2f, 0.02f), new Vector3(0.5f, 0.32f, 0.34f), coat);
            // A sodden shoulder cape, sagging.
            Part(PrimitiveType.Sphere, body.spineHigh, "Cape", new Vector3(0f, 0.36f, -0.04f), new Vector3(0.66f, 0.22f, 0.44f), coatEdge);

            // ---------- The coat skirt: strips that hang from the belt and swing ----------
            for (int i = 0; i < 11; i++)
            {
                float a = i / 11f * Mathf.PI * 2f;
                Vector3 at = new Vector3(Mathf.Sin(a) * 0.24f, 0.08f, Mathf.Cos(a) * 0.18f);
                Transform strip = Pivot(body.spineLow, "Coat Strip", at);
                strip.localRotation = Quaternion.Euler(0f, a * Mathf.Rad2Deg, 0f);
                float length = Random.Range(1.1f, 1.35f);
                Part(PrimitiveType.Cube, strip, "Cloth", new Vector3(0f, -length * 0.5f, 0.02f), new Vector3(0.17f, length, 0.025f), i % 3 == 0 ? coatEdge : coat);
                body.coatStrips.Add(strip);
                body.stripSeeds.Add(Random.Range(0f, 10f));
            }

            // ---------- Neck and head: a long neck, a deep hood, rags bound over the eyes ----------
            body.neck = Pivot(body.spineHigh, "Neck", new Vector3(0f, 0.46f, 0.08f));
            Part(PrimitiveType.Capsule, body.neck, "Neck Skin", new Vector3(0f, 0.08f, 0f), new Vector3(0.1f, 0.12f, 0.1f), skin);
            body.head = Pivot(body.neck, "Head", new Vector3(0f, 0.2f, 0.04f));
            // A long, narrow skull with sunken cheeks and a jaw that hangs open.
            Part(PrimitiveType.Sphere, body.head, "Skull", new Vector3(0f, 0.05f, 0f), new Vector3(0.17f, 0.27f, 0.21f), skin);
            Part(PrimitiveType.Sphere, body.head, "Cheekbones", new Vector3(0f, 0.02f, 0.05f), new Vector3(0.18f, 0.06f, 0.14f), skin);
            Part(PrimitiveType.Sphere, body.head, "Jaw", new Vector3(0f, -0.13f, 0.035f), new Vector3(0.12f, 0.15f, 0.13f), skin);
            Part(PrimitiveType.Cube, body.head, "Mouth", new Vector3(0f, -0.11f, 0.075f), new Vector3(0.045f, 0.08f, 0.03f), hollow);
            foreach (float tilt in new[] { 8f, -6f })
            {
                Transform band = Part(PrimitiveType.Cylinder, body.head, "Rag", new Vector3(0f, 0.06f + tilt * 0.002f, 0f), new Vector3(0.215f, 0.022f, 0.245f), rag).transform;
                band.localRotation = Quaternion.Euler(tilt, 0f, tilt * 0.5f);
            }
            Part(PrimitiveType.Cube, body.head, "Rag Tail", new Vector3(0.04f, -0.05f, -0.13f), new Vector3(0.04f, 0.2f, 0.01f), rag).transform.localRotation = Quaternion.Euler(10f, 0f, 12f);
            // The hood: a deep cowl drawn forward, so the face sits back in shadow.
            Part(PrimitiveType.Sphere, body.head, "Hood", new Vector3(0f, 0.07f, -0.07f), new Vector3(0.31f, 0.4f, 0.34f), coat);
            // The cowl's rim, drawn forward past the brow.
            Part(PrimitiveType.Sphere, body.head, "Hood Rim", new Vector3(0f, 0.12f, 0.06f), new Vector3(0.3f, 0.2f, 0.2f), coat);

            // ---------- Arms: too long, jointed at the elbow, long grey fingers ----------
            body.leftArm = Arm(body.spineHigh, -1f, coat, skin);
            body.rightArm = Arm(body.spineHigh, 1f, coat, skin);

            // The boat-hook, gripped in the right hand: a long ash pole and an iron hook.
            // Held near its top, so the long shaft and the iron hook trail down and ahead of him.
            body.hook = Pivot(body.rightArm.Lower, "Boat Hook", new Vector3(0f, -0.6f, 0.04f));
            Part(PrimitiveType.Cylinder, body.hook, "Pole", new Vector3(0f, -0.7f, 0f), new Vector3(0.04f, 0.9f, 0.04f), wood);
            Transform hookHead = Pivot(body.hook, "Hook Head", new Vector3(0f, -1.6f, 0f));
            Part(PrimitiveType.Cylinder, hookHead, "Spike", new Vector3(0f, -0.14f, 0f), new Vector3(0.03f, 0.14f, 0.03f), iron);
            for (int i = 0; i < 5; i++)
            {
                float a = 20f + i * 32f;
                Vector3 at = new Vector3(0f, -Mathf.Sin(a * Mathf.Deg2Rad) * 0.1f + 0.02f, Mathf.Cos(a * Mathf.Deg2Rad) * 0.1f + 0.08f);
                Part(PrimitiveType.Cylinder, hookHead, "Hook Curve", at, new Vector3(0.025f, 0.035f, 0.025f), iron).transform.localRotation = Quaternion.Euler(a, 0f, 0f);
            }

            // The chapel bell's iron clapper, chained at his hip. It knocks against him as he walks.
            Transform chainTop = Pivot(body.spineLow, "Chain", new Vector3(-0.24f, 0.04f, 0.08f));
            for (int i = 0; i < 4; i++)
                Part(PrimitiveType.Cylinder, chainTop, "Link", new Vector3(0f, -0.05f - i * 0.06f, 0f), new Vector3(0.035f, 0.025f, 0.012f), iron)
                    .transform.localRotation = Quaternion.Euler(90f, i % 2 == 0 ? 0f : 90f, 0f);
            body.clapper = Pivot(chainTop, "Clapper", new Vector3(0f, -0.3f, 0f));
            Part(PrimitiveType.Capsule, body.clapper, "Iron Clapper", new Vector3(0f, -0.14f, 0f), new Vector3(0.1f, 0.16f, 0.1f), iron);
            // The brass handbell he tolls with, in his left hand.
            Part(PrimitiveType.Cylinder, body.leftArm.Lower, "Handbell", new Vector3(0f, -0.62f, 0.04f), new Vector3(0.13f, 0.08f, 0.13f), brass);

            // A torn cowl hanging over the shoulders and down the back.
            Part(PrimitiveType.Cube, body.spineHigh, "Cowl Back", new Vector3(0f, 0.18f, -0.2f), new Vector3(0.5f, 0.55f, 0.04f), coatEdge).transform.localRotation = Quaternion.Euler(-12f, 0f, 0f);
            // Bare ribs where the coat has rotted open.
            for (int i = 0; i < 3; i++)
                Part(PrimitiveType.Capsule, body.spineHigh, "Rib", new Vector3(0f, 0.08f + i * 0.08f, 0.16f), new Vector3(0.03f, 0.13f, 0.03f), skin)
                    .transform.localRotation = Quaternion.Euler(0f, 0f, 90f);

            body.Drips(visual.transform);

            // Only the NavMeshAgent should collide; the body is visual.
            foreach (Collider c in visual.GetComponentsInChildren<Collider>()) DestroyImmediate(c);
            return body;
        }

        private static Limb Leg(Transform hips, float side, Material coat, Material skin)
        {
            var leg = new Limb();
            leg.Upper = Pivot(hips, side < 0f ? "Left Thigh" : "Right Thigh", new Vector3(side * 0.13f, 0f, 0f));
            Part(PrimitiveType.Capsule, leg.Upper, "Thigh", new Vector3(0f, -0.33f, 0f), new Vector3(0.14f, 0.35f, 0.14f), coat);
            leg.Lower = Pivot(leg.Upper, "Knee", new Vector3(0f, -0.66f, 0f));
            Part(PrimitiveType.Capsule, leg.Lower, "Shin", new Vector3(0f, -0.3f, 0f), new Vector3(0.08f, 0.33f, 0.08f), skin);
            Part(PrimitiveType.Cube, leg.Lower, "Foot", new Vector3(0f, -0.63f, 0.08f), new Vector3(0.09f, 0.04f, 0.3f), skin);
            return leg;
        }

        private static Limb Arm(Transform chest, float side, Material coat, Material skin)
        {
            var arm = new Limb();
            arm.Upper = Pivot(chest, side < 0f ? "Left Arm" : "Right Arm", new Vector3(side * 0.3f, 0.34f, 0f));
            Part(PrimitiveType.Capsule, arm.Upper, "Sleeve", new Vector3(0f, -0.27f, 0f), new Vector3(0.12f, 0.3f, 0.12f), coat);
            // The sleeve is torn off below the elbow.
            Part(PrimitiveType.Cube, arm.Upper, "Sleeve Rag", new Vector3(0f, -0.56f, 0.03f), new Vector3(0.1f, 0.16f, 0.015f), coat).transform.localRotation = Quaternion.Euler(8f, 0f, 0f);
            arm.Lower = Pivot(arm.Upper, "Elbow", new Vector3(0f, -0.56f, 0f));
            Part(PrimitiveType.Capsule, arm.Lower, "Forearm", new Vector3(0f, -0.25f, 0f), new Vector3(0.07f, 0.28f, 0.07f), skin);
            Part(PrimitiveType.Cube, arm.Lower, "Palm", new Vector3(0f, -0.56f, 0.01f), new Vector3(0.09f, 0.11f, 0.04f), skin);
            for (int i = 0; i < 4; i++)
            {
                Transform finger = Pivot(arm.Lower, "Finger", new Vector3(-0.033f + i * 0.022f, -0.61f, 0.015f));
                Part(PrimitiveType.Capsule, finger, "Digit", new Vector3(0f, -0.09f, 0f), new Vector3(0.016f, 0.1f, 0.016f), skin);
                arm.Fingers.Add(finger);
            }
            return arm;
        }

        // Water running off him: a thin stream of drops falling from the coat.
        private void Drips(Transform parent)
        {
            GameObject drip = new GameObject("Drips");
            drip.transform.SetParent(parent, false);
            drip.transform.localPosition = new Vector3(0f, 1.2f, 0f);
            ParticleSystem ps = drip.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            ParticleSystem.MainModule main = ps.main;
            main.startLifetime = 0.9f;
            main.startSpeed = 0f;
            main.startSize = 0.025f;
            main.gravityModifier = 1f;
            main.startColor = new Color(0.7f, 0.8f, 0.85f, 0.6f);
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.maxParticles = 80;
            ParticleSystem.EmissionModule emission = ps.emission;
            emission.rateOverTime = 22f;
            ParticleSystem.ShapeModule shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Box;
            shape.scale = new Vector3(0.5f, 0.9f, 0.4f);
            drip.GetComponent<ParticleSystemRenderer>().sharedMaterial = Fx.Particle;
            ps.Play();
        }

        private static Transform Pivot(Transform parent, string name, Vector3 local)
        {
            Transform pivot = new GameObject(name).transform;
            pivot.SetParent(parent, false);
            pivot.localPosition = local;
            return pivot;
        }

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

        // Both arms come down hard on a door.
        public void Smash() => smash = 1f;

        // Raises both arms toward Nell and holds them there.
        public void Grab() => grabbing = true;

        // Lets go after a knockdown.
        public void Release() => grabbing = false;

        // A sound reached him: he turns an ear toward it before he moves.
        public void Heard(Vector3 worldPoint)
        {
            heardAt = transform.InverseTransformPoint(worldPoint);
            heardFresh = 1f;
        }

        // Called every frame by SextonController with his current speed and pose.
        public void Animate(float speed, Pose pose, float dt)
        {
            float moving = Mathf.Clamp01(speed / 1.2f);
            stride = Mathf.MoveTowards(stride, moving, dt * 3f);
            phase += dt * (1.2f + speed * 1.5f);
            swipe = Mathf.MoveTowards(swipe, 0f, dt * 2.5f);
            smash = Mathf.MoveTowards(smash, 0f, dt * 2f);
            grab = Mathf.MoveTowards(grab, grabbing ? 1f : 0f, dt * 4f);
            sweep = Mathf.MoveTowards(sweep, pose == Pose.Sweep ? 1f : 0f, dt * 3f);
            feel = Mathf.MoveTowards(feel, pose == Pose.Feel ? 1f : 0f, dt * 3f);
            ring = Mathf.MoveTowards(ring, pose == Pose.Ring ? 1f : 0f, dt * 3f);
            hunt = Mathf.MoveTowards(hunt, pose == Pose.Hunt ? 1f : 0f, dt * 2f);
            heardFresh = Mathf.MoveTowards(heardFresh, 0f, dt * 0.35f);

            float s = Mathf.Sin(phase);
            float c = Mathf.Cos(phase);
            float breathe = Mathf.Sin(Time.time * 1.3f);

            // ---------- Legs: a long, dragging stride; knees bend on the swing ----------
            float reach = Mathf.Lerp(22f, 34f, hunt) * stride;
            float crouch = Mathf.Max(sweep * 0.8f, feel * 0.6f, grab * 0.4f);
            leftLeg.Upper.localRotation = Quaternion.Euler(-s * reach - crouch * 25f, 0f, -2f);
            rightLeg.Upper.localRotation = Quaternion.Euler(s * reach - crouch * 25f, 0f, 2f);
            leftLeg.Lower.localRotation = Quaternion.Euler(Mathf.Max(0f, c) * reach * 1.4f + crouch * 45f, 0f, 0f);
            rightLeg.Lower.localRotation = Quaternion.Euler(Mathf.Max(0f, -c) * reach * 1.4f + crouch * 45f, 0f, 0f);
            float bob = Mathf.Abs(s) * 0.06f * stride - crouch * 0.18f;
            hips.localPosition = new Vector3(0f, HipHeight + bob, 0f);
            hips.localRotation = Quaternion.Euler(0f, s * 6f * stride, 0f);

            // ---------- Spine: hunched, more so when hunting, bending low to sweep and feel ----------
            // Stooped like a question mark: deeper when hunting, deeper still when he bends to feel.
            float lean = Mathf.Lerp(38f, 52f, hunt) + Mathf.Max(grab * 8f, sweep * 20f, feel * 14f);
            spineLow.localRotation = Quaternion.Euler(lean * 0.45f + breathe * 1.5f, -s * 4f * stride, s * 2f * stride);
            spineHigh.localRotation = Quaternion.Euler(lean * 0.55f, 0f, 0f);

            // ---------- Head: blind, so it listens. It turns an ear to the last sound, then drifts ----------
            Vector3 toSound = heardAt;
            float earYaw = Mathf.Clamp(Mathf.Atan2(toSound.x, Mathf.Max(0.1f, toSound.z)) * Mathf.Rad2Deg, -70f, 70f) * heardFresh;
            float idleYaw = Mathf.Sin(Time.time * 0.6f) * 22f * (1f - heardFresh);
            float earRoll = Mathf.Sign(earYaw) * 22f * heardFresh + Mathf.Sin(Time.time * 0.9f) * 10f;
            // The neck bends back so the face points ahead, jutting forward from the hunch.
            float pitch = Mathf.Lerp(-lean * 0.85f, -lean - 10f, grab) + 12f;
            neck.localRotation = Quaternion.Euler(pitch * 0.4f, (earYaw + idleYaw) * 0.4f, 0f);
            head.localRotation = Quaternion.Euler(pitch * 0.6f, (earYaw + idleYaw) * 0.6f * (1f - grab), earRoll * (1f - grab));

            // ---------- Arms ----------
            float swing = s * 18f * stride;
            float swipeAngle = -Mathf.Sin(swipe * Mathf.PI) * 115f;
            float smashAngle = -Mathf.Sin(smash * Mathf.PI) * 150f;
            float sweepYaw = Mathf.Sin(Time.time * 2.2f) * 50f * sweep;
            float fumble = Mathf.Sin(Time.time * 9f) * 8f * feel;
            float shake = Mathf.Sin(Time.time * 14f) * 25f * ring;

            // Left: hangs and swings; reaches into wardrobes; lifts the handbell to toll.
            // Arm angles below are where the arm points in the world; subtracting the chest's lean
            // turns them into shoulder angles, so they hang straight down from a stooped body.
            float hang = lean - 12f;
            float leftPitch = Mathf.Lerp(swing + 6f, -75f + fumble, feel);
            leftPitch = Mathf.Lerp(leftPitch, -150f + shake, ring) + smashAngle;
            leftArm.Upper.localRotation = Quaternion.Euler(Mathf.Lerp(leftPitch, -110f, grab) - hang, 0f, -8f + 4f * (1f - stride));
            leftArm.Lower.localRotation = Quaternion.Euler(Mathf.Lerp(-18f - stride * 10f, -10f, Mathf.Max(feel, grab)), 0f, 0f);
            // Right: carries the hook low; swipes; drags it through the mud when sweeping.
            float rightPitch = Mathf.Lerp(-swing - 18f + swipeAngle, -35f, sweep) + smashAngle;
            rightArm.Upper.localRotation = Quaternion.Euler(Mathf.Lerp(rightPitch, -110f, grab) - hang, sweepYaw * (1f - grab), 8f);
            rightArm.Lower.localRotation = Quaternion.Euler(Mathf.Lerp(-35f, -5f, Mathf.Max(swipe, grab)), 0f, 0f);
            // The shaft trails ahead and down from his fist; sweeping drags the hook flat along the mud.
            hook.localRotation = Quaternion.Euler(Mathf.Lerp(-35f, -60f, sweep), 0f, 0f);

            // Long fingers curl and twitch; spread wide when grabbing or feeling.
            float spread = Mathf.Max(grab, feel);
            for (int i = 0; i < 4; i++)
            {
                float twitch = Mathf.Sin(Time.time * (3f + i) + i) * 8f;
                Quaternion curl = Quaternion.Euler(Mathf.Lerp(35f, 5f, spread) + twitch, 0f, (i - 1.5f) * 6f * spread);
                leftArm.Fingers[i].localRotation = curl;
                rightArm.Fingers[i].localRotation = Quaternion.Euler(70f, 0f, 0f); // closed round the hook
            }

            // ---------- Cloth and chain: lag behind the stride, swing with it ----------
            for (int i = 0; i < coatStrips.Count; i++)
            {
                float seed = stripSeeds[i];
                float flap = Mathf.Sin(phase * 1.0f + seed) * 9f * stride + Mathf.Sin(Time.time * 1.7f + seed) * 2f;
                // Undo the waist's lean so the cloth hangs straight down, then let it swing.
                // (The lean is undone in the waist's frame, before the strip's own yaw around the body.)
                coatStrips[i].localRotation = Quaternion.Euler(-lean * 0.45f, 0f, 0f) * Quaternion.Euler(-6f * stride + flap, i / (float)coatStrips.Count * 360f, 0f);
            }
            clapper.parent.localRotation = Quaternion.Euler(-c * 18f * stride - 8f * stride, 0f, s * 10f * stride);
        }
    }

    // Shared effects resources made in code: a soft round particle and its material.
    public static class Fx
    {
        private static Material particle;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => particle = null;

        // "Sprites/Default" ships with every build, so this works outside the editor too.
        public static Material Particle
        {
            get
            {
                if (particle != null) return particle;
                const int size = 32;
                Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
                for (int y = 0; y < size; y++)
                    for (int x = 0; x < size; x++)
                    {
                        float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(size / 2f, size / 2f)) / (size / 2f);
                        tex.SetPixel(x, y, new Color(1f, 1f, 1f, Mathf.Clamp01(1f - d * d)));
                    }
                tex.Apply();
                particle = new Material(Shader.Find("Sprites/Default")) { mainTexture = tex };
                return particle;
            }
        }
    }
}
