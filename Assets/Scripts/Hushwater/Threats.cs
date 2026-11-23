using System.Collections.Generic;
using UnityEngine;

namespace Hushwater
{
    // Design doc, "Threats of the Town": "None of these can be killed, and few can kill. Their job is to make
    // noise for the Sexton or pin you in place while he comes."

    // The Congregation: silt-covered residents, frozen where the water found them.
    // "Never move while you watch them. Turn toward any light within 8 m. Lit for 3 s: a wail heard 30 m away.
    // Some are elsewhere when you come back." The handbell "can wake the Congregation" too.
    public sealed class Congregant : MonoBehaviour
    {
        private const float LightRange = 8f;
        private const float LitToWail = 3f;
        // Ours: after a wail they are quiet for a while, and how often an unwatched one may shift.
        private const float WailCooldown = 12f;

        private static readonly List<Congregant> All = new List<Congregant>();
        private static Material silt;

        private Vector3 home;
        private float litTime;
        private float cooldown;
        private float shiftTimer;
        private AudioSource voice;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            All.Clear();
            silt = null;
        }

        public static Congregant Build(Vector3 position, float yaw)
        {
            if (silt == null) silt = HushwaterAssets.Surface("brown_mud_02", new Color(0.42f, 0.4f, 0.33f), 1f, 0.5f);
            GameObject root = new GameObject("Congregant");
            root.transform.SetPositionAndRotation(position, Quaternion.Euler(0f, yaw, 0f));
            Transform t = root.transform;
            float height = Random.Range(0.85f, 1.05f);
            Part(t, PrimitiveType.Capsule, new Vector3(0f, 0.85f * height, 0f), new Vector3(0.5f, 0.85f * height, 0.38f), true);
            Part(t, PrimitiveType.Sphere, new Vector3(0f, 1.78f * height, 0.04f), new Vector3(0.26f, 0.3f, 0.27f), false);
            // Arms frozen mid-gesture: reaching, covering a face, holding a child that isn't there.
            Part(t, PrimitiveType.Capsule, new Vector3(-0.3f, 1.3f * height, 0.25f), new Vector3(0.11f, 0.32f, 0.11f), false)
                .transform.localRotation = Quaternion.Euler(Random.Range(40f, 100f), 0f, 10f);
            Part(t, PrimitiveType.Capsule, new Vector3(0.3f, 1.2f * height, 0.12f), new Vector3(0.11f, 0.32f, 0.11f), false)
                .transform.localRotation = Quaternion.Euler(Random.Range(-20f, 70f), 0f, -10f);
            Congregant c = root.AddComponent<Congregant>();
            c.home = position;
            c.shiftTimer = Random.Range(25f, 50f);
            return c;
        }

        private static GameObject Part(Transform parent, PrimitiveType type, Vector3 local, Vector3 scale, bool solid)
        {
            GameObject part = GameObject.CreatePrimitive(type);
            if (!solid) Object.DestroyImmediate(part.GetComponent<Collider>());
            part.transform.SetParent(parent, false);
            part.transform.localPosition = local;
            part.transform.localScale = scale;
            part.GetComponent<Renderer>().sharedMaterial = silt;
            return part;
        }

        private void OnEnable()
        {
            All.Add(this);
            SurveyCamera.Flashed += OnFlash;
            NoisePulse.Emitted += OnNoise;
            voice = gameObject.AddComponent<AudioSource>();
            voice.spatialBlend = 1f;
            voice.maxDistance = 40f;
            voice.rolloffMode = AudioRolloffMode.Linear;
        }

        private void OnDisable()
        {
            All.Remove(this);
            SurveyCamera.Flashed -= OnFlash;
            NoisePulse.Emitted -= OnNoise;
        }

        private void Update()
        {
            PlayerController player = PlayerController.Instance;
            if (player == null) return;
            cooldown -= Time.deltaTime;
            bool watched = Watched(player);
            NellLantern lantern = player.GetComponent<NellLantern>();
            float distance = Vector3.Distance(transform.position, player.transform.position);
            bool lit = lantern != null && lantern.Lit && distance < LightRange && !player.IsHiding;

            if (lit)
            {
                litTime += Time.deltaTime;
                if (!watched) Face(player.transform.position); // they only move when you aren't looking
                if (litTime >= LitToWail) Wail();
            }
            else litTime = Mathf.Max(0f, litTime - Time.deltaTime * 0.5f);

            // "Some are elsewhere when you come back."
            shiftTimer -= Time.deltaTime;
            if (shiftTimer <= 0f)
            {
                shiftTimer = Random.Range(25f, 50f);
                if (!watched && distance > 12f) Shift();
            }
        }

        // In Nell's view, close enough to make out, and nothing in the way.
        private bool Watched(PlayerController player)
        {
            Camera view = player.View;
            if (view == null || player.IsHiding) return false;
            Vector3 chest = transform.position + Vector3.up * 1.3f;
            Vector3 to = chest - view.transform.position;
            if (to.magnitude > 25f || Vector3.Angle(view.transform.forward, to) > view.fieldOfView * 0.75f) return false;
            return !Physics.Raycast(view.transform.position, to.normalized, out RaycastHit hit, to.magnitude - 0.5f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore)
                   || hit.collider.GetComponentInParent<Congregant>() == this;
        }

        private void OnFlash(Vector3 from)
        {
            if (Vector3.Distance(from, transform.position) > LightRange) return;
            litTime = LitToWail; // a flash in the face is plenty
            Face(from);
            Wail();
        }

        private void OnNoise(NoiseEvent noise)
        {
            if (noise.Kind == NoiseKind.Handbell && Vector3.Distance(noise.Position, transform.position) < LightRange) Wail();
        }

        // "Lit for 3 s: a wail heard 30 m away."
        private void Wail()
        {
            if (cooldown > 0f) return;
            cooldown = WailCooldown;
            litTime = 0f;
            voice.PlayOneShot(ProceduralSounds.Wail, 1f);
            NoisePulse.Emit(transform.position, NoisePulse.Wail, NoiseKind.Wail);
        }

        private void Face(Vector3 point)
        {
            Vector3 d = point - transform.position;
            d.y = 0f;
            if (d.sqrMagnitude > 0.01f) transform.rotation = Quaternion.LookRotation(d);
        }

        // A step or two from home, somewhere clear.
        private void Shift()
        {
            for (int i = 0; i < 6; i++)
            {
                Vector2 offset = Random.insideUnitCircle * 3f;
                Vector3 to = home + new Vector3(offset.x, 0f, offset.y);
                if (Physics.CheckSphere(to + Vector3.up, 0.45f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore)) continue;
                transform.SetPositionAndRotation(to, Quaternion.Euler(0f, Random.Range(0f, 360f), 0f));
                return;
            }
        }
    }

    // Silt Hands: "Arms that rise from deep mud in Millrow and the graveyard. Hidden until you step into their pool.
    // Grip for 4 s, damage every second. Tap Interact to break free (each tap is noise). Creeping through never wakes them."
    public static class SiltHands
    {
        // Ours: a pool that has gripped once rests for a while.
        private const float Rest = 25f;
        private static readonly List<Vector4> Pools = new List<Vector4>();
        private static readonly List<float> Ready = new List<float>();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        public static void Clear()
        {
            Pools.Clear();
            Ready.Clear();
        }

        public static void AddPool(Vector3 centre, float radius)
        {
            Pools.Add(new Vector4(centre.x, 0f, centre.z, radius));
            Ready.Add(0f);
        }

        // Called on every footstep in deep mud that isn't a creep.
        public static void Stepped(PlayerController player)
        {
            Vector3 p = player.transform.position;
            for (int i = 0; i < Pools.Count; i++)
            {
                Vector4 pool = Pools[i];
                float dx = p.x - pool.x, dz = p.z - pool.z;
                if (dx * dx + dz * dz > pool.w * pool.w || Time.time < Ready[i]) continue;
                Ready[i] = Time.time + Rest;
                player.Grip();
                Arms(new Vector3(p.x, 0f, p.z));
                return;
            }
        }

        // Two silt-grey arms rising round her ankles, sinking again after the grip.
        private static void Arms(Vector3 at)
        {
            Material silt = HushwaterAssets.Surface("brown_mud_02", new Color(0.3f, 0.28f, 0.22f), 1f, 0.7f);
            GameObject root = new GameObject("Silt Hands");
            root.transform.position = at;
            for (int i = 0; i < 2; i++)
            {
                GameObject arm = GameObject.CreatePrimitive(PrimitiveType.Capsule);
                Object.DestroyImmediate(arm.GetComponent<Collider>());
                arm.transform.SetParent(root.transform, false);
                arm.transform.localPosition = new Vector3(i == 0 ? -0.35f : 0.35f, 0.3f, 0.2f);
                arm.transform.localRotation = Quaternion.Euler(-20f, 0f, i == 0 ? 25f : -25f);
                arm.transform.localScale = new Vector3(0.12f, 0.4f, 0.12f);
                arm.GetComponent<Renderer>().sharedMaterial = silt;
            }
            root.AddComponent<Sinker>();
        }

        private sealed class Sinker : MonoBehaviour
        {
            private float age;

            private void Update()
            {
                age += Time.deltaTime;
                if (age > 4f) transform.position += Vector3.down * Time.deltaTime * 0.6f;
                if (age > 6f) Destroy(gameObject);
            }
        }
    }

    // Lantern Moths (Night 3+): "Pale swarms that nest in drowned attics. Drawn to your lantern from 15 m.
    // Drain 1 lantern charge every 5 s. Their hiss reaches the Sexton at 10 m. Turn your light to zero to scatter them."
    public sealed class MothSwarm : MonoBehaviour
    {
        private const float DrawRange = 15f;
        private const float FeedRange = 2f;
        private const float DrainEvery = 5f;
        // Ours: how often the feeding swarm hisses.
        private const float HissEvery = 3f;

        private readonly List<Transform> motes = new List<Transform>();
        private Vector3 nest;
        private float drainTimer = DrainEvery;
        private float hissTimer;
        private AudioSource voice;

        public bool Feeding { get; private set; }

        public static MothSwarm Build(Vector3 nest)
        {
            GameObject root = new GameObject("Lantern Moths");
            root.transform.position = nest;
            MothSwarm swarm = root.AddComponent<MothSwarm>();
            swarm.nest = nest;
            Material pale = new Material(Shader.Find("Standard")) { color = new Color(0.9f, 0.88f, 0.8f) };
            pale.EnableKeyword("_EMISSION");
            pale.SetColor("_EmissionColor", new Color(0.25f, 0.24f, 0.2f));
            for (int i = 0; i < 18; i++)
            {
                GameObject mote = GameObject.CreatePrimitive(PrimitiveType.Quad);
                Object.DestroyImmediate(mote.GetComponent<Collider>());
                mote.transform.SetParent(root.transform, false);
                mote.transform.localScale = Vector3.one * 0.05f;
                mote.GetComponent<Renderer>().sharedMaterial = pale;
                swarm.motes.Add(mote.transform);
            }
            swarm.voice = root.AddComponent<AudioSource>();
            swarm.voice.spatialBlend = 1f;
            swarm.voice.maxDistance = 20f;
            return swarm;
        }

        private void Update()
        {
            PlayerController player = PlayerController.Instance;
            NellLantern lantern = player != null ? player.GetComponent<NellLantern>() : null;
            Vector3 target = nest;
            bool drawn = lantern != null && lantern.Lit && !player.IsHiding &&
                         Vector3.Distance(player.View.transform.position, nest) < DrawRange;
            if (drawn) target = player.View.transform.position + player.View.transform.forward * 0.6f;
            float speed = drawn ? 2.2f : 4f; // they scatter faster than they gather
            transform.position = Vector3.MoveTowards(transform.position, target, speed * Time.deltaTime);

            Feeding = drawn && Vector3.Distance(transform.position, target) < FeedRange;
            if (Feeding)
            {
                drainTimer -= Time.deltaTime;
                if (drainTimer <= 0f)
                {
                    drainTimer = DrainEvery;
                    lantern.Drain(1f);
                }
                hissTimer -= Time.deltaTime;
                if (hissTimer <= 0f)
                {
                    hissTimer = HissEvery;
                    voice.PlayOneShot(ProceduralSounds.Hiss, 0.8f);
                    NoisePulse.Emit(transform.position, NoisePulse.Hiss, NoiseKind.Hiss);
                }
            }
            else drainTimer = DrainEvery;

            // A loose, fluttering cloud.
            float spread = Feeding ? 0.5f : 0.8f;
            for (int i = 0; i < motes.Count; i++)
            {
                float t = Time.time * (2f + i * 0.13f) + i;
                motes[i].localPosition = new Vector3(Mathf.Sin(t) * spread, Mathf.Sin(t * 1.7f) * spread * 0.5f, Mathf.Cos(t * 0.9f) * spread);
                if (Camera.main != null) motes[i].rotation = Camera.main.transform.rotation;
            }
        }
    }

    // Eels of the Cellars: "Only underwater. A bite costs 5 s of Breath, not Vitality. They can't turn tight: hug the walls.
    // Never alert the Sexton."
    public sealed class Eel : MonoBehaviour
    {
        private const float BiteBreath = 5f;
        private const float Speed = 2.6f;
        private const float TurnRate = 70f; // degrees a second: wide, slow turns
        private const float BiteRange = 0.8f;
        // They keep to the middle of the channel. Ours: how far from the centreline they swim.
        private const float LaneHalfWidth = 0.6f;

        private Vector3 a, b;
        private Vector3 heading;
        private float biteCooldown;
        private bool toB = true;

        public static Eel Build(Vector3 from, Vector3 to)
        {
            Material skin = new Material(Shader.Find("Standard")) { color = new Color(0.12f, 0.14f, 0.1f) };
            skin.SetFloat("_Glossiness", 0.8f);
            GameObject root = new GameObject("Eel");
            root.transform.position = from;
            for (int i = 0; i < 5; i++)
            {
                GameObject seg = GameObject.CreatePrimitive(PrimitiveType.Capsule);
                Object.DestroyImmediate(seg.GetComponent<Collider>());
                seg.transform.SetParent(root.transform, false);
                seg.transform.localPosition = new Vector3(0f, 0f, -i * 0.28f);
                seg.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
                float w = 0.13f - i * 0.018f;
                seg.transform.localScale = new Vector3(w, 0.18f, w);
                seg.GetComponent<Renderer>().sharedMaterial = skin;
            }
            Eel eel = root.AddComponent<Eel>();
            eel.a = from;
            eel.b = to;
            eel.heading = (to - from).normalized;
            return eel;
        }

        private void Update()
        {
            PlayerController player = PlayerController.Instance;
            biteCooldown -= Time.deltaTime;
            Vector3 lane = (b - a).normalized;
            Vector3 target = toB ? b : a;
            bool hunting = player != null && player.Underwater && Vector3.Distance(player.transform.position, transform.position) < 6f;
            if (hunting)
            {
                // Chase, but only along the middle of the channel: Nell is safe against the walls.
                Vector3 p = player.transform.position;
                Vector3 along = a + lane * Vector3.Dot(p - a, lane);
                Vector3 side = p - along;
                side.y = 0f;
                target = along + Vector3.ClampMagnitude(side, LaneHalfWidth);
                target.y = a.y;
            }
            else if (Vector3.Distance(transform.position, target) < 0.8f) toB = !toB;

            Vector3 want = (target - transform.position);
            want.y = 0f;
            if (want.sqrMagnitude > 0.001f)
                heading = Vector3.RotateTowards(heading, want.normalized, TurnRate * Mathf.Deg2Rad * Time.deltaTime, 0f);
            transform.position += heading * Speed * (hunting ? 1f : 0.6f) * Time.deltaTime;
            // Stay in the water between the ends.
            Vector3 rel = transform.position - a;
            float t = Mathf.Clamp(Vector3.Dot(rel, lane), 0f, Vector3.Distance(a, b));
            Vector3 onLine = a + lane * t;
            Vector3 off = transform.position - onLine;
            off.y = 0f;
            transform.position = onLine + Vector3.ClampMagnitude(off, LaneHalfWidth + 0.2f);
            if (heading.sqrMagnitude > 0.001f) transform.rotation = Quaternion.LookRotation(heading);

            if (hunting && biteCooldown <= 0f && Vector3.Distance(transform.position, player.transform.position + Vector3.down * 0.1f) < BiteRange + 0.4f)
            {
                biteCooldown = 3f;
                player.LoseBreath(BiteBreath);
                player.Notify("An eel bites. Keep to the walls: they can't turn tight.");
            }
        }
    }
}
