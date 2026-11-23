using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

namespace Hushwater
{
    // Anything the Sexton can batter his way through when it stands between him and a sound: nailed doors,
    // and the radio room door on Night 5.
    public interface IBreachable
    {
        bool Broken { get; }
        // Where he stands to batter it.
        Vector3 Approach { get; }
        Vector3 Position { get; }
        // How far from his goal the door can be and still count as "in the way".
        float BlockRange { get; }
        // One blow. Returns true once he is through.
        bool Hit();
    }

    public static class Breachables
    {
        public static readonly List<IBreachable> All = new List<IBreachable>();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => All.Clear();

        // The closest unbroken door whose block range covers the point, or null.
        public static IBreachable Near(Vector3 point)
        {
            IBreachable best = null;
            float bestDistance = float.MaxValue;
            foreach (IBreachable door in All)
            {
                if (door.Broken) continue;
                Vector3 d = door.Position - point;
                d.y = 0f;
                if (d.magnitude <= door.BlockRange && d.magnitude < bestDistance)
                {
                    best = door;
                    bestDistance = d.magnitude;
                }
            }
            return best;
        }
    }

    // A warped cabinet holding a register page. Design doc: "Prying registers from warped cabinets" is loud;
    // "the parish register sits in a cabinet that must be forced open (25 m of noise)".
    public sealed class RegisterCabinet : Interactable
    {
        private EvidenceSpot page;
        private bool opened;
        private float creakTimer;

        public override KeyCode Key => KeyCode.E;
        public override float HoldSeconds => 2.5f;
        public override string Prompt => "Hold E to force the cabinet (25 m, very loud)";
        public override bool Available => !opened;
        public override float Reach => 1.8f;

        public static RegisterCabinet Build(EvidenceSpot spot, Material wood)
        {
            GameObject cabinet = HushwaterAssets.Model("GothicCabinet_01", spot.Position, Random.Range(-10f, 10f) + 90f, 1.3f);
            cabinet.name = "Warped Cabinet";
            RegisterCabinet c = cabinet.AddComponent<RegisterCabinet>();
            c.page = spot;
            return c;
        }

        // Warped wood creaks while she pries at it.
        public override void Holding(PlayerController player, float progress)
        {
            creakTimer -= Time.deltaTime;
            if (creakTimer > 0f) return;
            creakTimer = 0.8f;
            ProceduralSounds.PlayAt(ProceduralSounds.Creak, transform.position, 0.6f);
        }

        public override void Complete(PlayerController player)
        {
            opened = true;
            NoisePulse.Emit(transform.position, NoisePulse.Door, NoiseKind.Cabinet);
            ProceduralSounds.PlayAt(ProceduralSounds.Crash, transform.position, 0.8f);
            player.CollectEvidence(page, transform.position);
        }
    }

    // Design doc upgrade: "Rope ladder: a permanent escape he can't climb." Bought at the boat, one per spot.
    public sealed class RopeLadder : Interactable
    {
        private Vector3 foot;
        private Vector3 roof;

        public override KeyCode Key => KeyCode.E;
        public override float HoldSeconds => 0.7f;
        public override float Reach => 2.4f;
        public override bool Available => true;
        public override string Prompt => OnRoof(PlayerController.Instance) ? "Hold E to climb down the rope ladder" : "Hold E to climb the rope ladder (he can't follow)";

        public static RopeLadder Build(Vector3 foot, Vector3 roofTop, Material rope)
        {
            GameObject root = new GameObject("Rope Ladder");
            root.layer = 2;
            root.transform.position = new Vector3(foot.x, 2.6f, foot.z);
            for (int i = 0; i < 2; i++)
            {
                GameObject side = GameObject.CreatePrimitive(PrimitiveType.Cube);
                Object.DestroyImmediate(side.GetComponent<Collider>());
                side.transform.SetParent(root.transform, false);
                side.transform.position = new Vector3(foot.x, 1.6f, foot.z + (i == 0 ? -0.25f : 0.25f));
                side.transform.localScale = new Vector3(0.04f, 3.2f, 0.04f);
                side.GetComponent<Renderer>().sharedMaterial = rope;
            }
            for (int r = 0; r < 9; r++)
            {
                GameObject rung = GameObject.CreatePrimitive(PrimitiveType.Cube);
                Object.DestroyImmediate(rung.GetComponent<Collider>());
                rung.transform.SetParent(root.transform, false);
                rung.transform.position = new Vector3(foot.x, 0.3f + r * 0.35f, foot.z);
                rung.transform.localScale = new Vector3(0.05f, 0.04f, 0.5f);
                rung.GetComponent<Renderer>().sharedMaterial = rope;
            }
            RopeLadder ladder = root.AddComponent<RopeLadder>();
            ladder.foot = foot + Vector3.up;
            ladder.roof = roofTop + Vector3.up;
            return ladder;
        }

        private static bool OnRoof(PlayerController player) => player != null && player.transform.position.y > 2.5f;

        public override void Complete(PlayerController player) => player.Climb(OnRoof(player) ? foot : roof);
    }

    // The rowboat. Design doc: "Win a night: reach the boat before the dawn bell with at least one new piece of evidence."
    public sealed class BoatLanding : Interactable
    {
        public override KeyCode Key => KeyCode.E;
        public override float HoldSeconds => 1.2f;
        public override float Reach => 2.8f;
        // Only offered once there is something new to take home.
        public override bool Available => NightDirector.Instance != null && NightDirector.Instance.NewEvidenceTonight;
        public override string Prompt => NightDirector.Instance != null ? NightDirector.Instance.BoatPrompt : "";

        public override void Complete(PlayerController player)
        {
            if (NightDirector.Instance != null) NightDirector.Instance.TryRowOut();
        }
    }

    // The radio room door on Night 5. Nell can shut it; the Sexton batters it. Standing against it from the inside braces it.
    // Design doc: "Hold the door, lure him with bells, or open the spillway to wash him away."
    public sealed class RadioDoor : Interactable, IBreachable
    {
        private const int BlowsToBreak = 4; // ours
        private NavMeshObstacle obstacle;
        private Collider slabCollider;
        private Transform slab;
        private Vector3 closedPosition;
        private int blows;
        private float shake;

        public bool Closed { get; private set; }
        public bool Broken { get; private set; }
        public Vector3 Approach { get; private set; }
        public Vector3 Position => transform.position;
        public float BlockRange => 10f;
        // Nell is leaning on it from inside the radio room.
        public bool Braced
        {
            get
            {
                PlayerController player = PlayerController.Instance;
                if (!Closed || player == null || player.IsDead || player.IsKnockedDown) return false;
                Vector3 p = player.transform.position;
                return p.z > closedPosition.z && Mathf.Abs(p.x - closedPosition.x) < 1.4f && p.z - closedPosition.z < 1.4f;
            }
        }

        public override KeyCode Key => KeyCode.E;
        public override float HoldSeconds => 0.3f;
        public override float Reach => 2f;
        public override bool Available => !Broken;
        public override string Prompt => Closed ? "E  open the door      (stand against it from inside to hold it shut)" : "E  shut the radio room door";

        public static RadioDoor Build(Vector3 position, Material wood)
        {
            GameObject root = new GameObject("Radio Room Door");
            root.transform.position = position;
            GameObject door = GameObject.CreatePrimitive(PrimitiveType.Cube);
            door.name = "Door Slab";
            door.layer = 2;
            door.transform.SetParent(root.transform, false);
            door.transform.localScale = new Vector3(2f, 2.2f, 0.15f);
            door.transform.localPosition = new Vector3(0f, 1.1f, 0f);
            door.GetComponent<Renderer>().sharedMaterial = wood;
            RadioDoor d = root.AddComponent<RadioDoor>();
            d.slab = door.transform;
            d.slabCollider = door.GetComponent<Collider>();
            d.closedPosition = position;
            d.Approach = new Vector3(position.x, 0f, position.z - 1f);
            d.obstacle = door.AddComponent<NavMeshObstacle>();
            d.obstacle.shape = NavMeshObstacleShape.Box;
            d.obstacle.size = Vector3.one;
            d.obstacle.carving = true;
            d.SetClosed(false);
            return d;
        }

        private void Update()
        {
            if (shake <= 0f) return;
            shake -= Time.deltaTime;
            slab.localPosition = new Vector3(Random.Range(-0.03f, 0.03f), 1.1f, Random.Range(-0.03f, 0.03f));
            if (shake <= 0f) slab.localPosition = new Vector3(0f, 1.1f, 0f);
        }

        private void SetClosed(bool closed)
        {
            Closed = closed;
            slabCollider.enabled = closed;
            obstacle.enabled = closed;
            slab.localRotation = closed ? Quaternion.identity : Quaternion.Euler(0f, 90f, 0f);
            slab.localPosition = closed ? new Vector3(0f, 1.1f, 0f) : new Vector3(-1f, 1.1f, 1f);
        }

        public override void Complete(PlayerController player)
        {
            SetClosed(!Closed);
            ProceduralSounds.PlayAt(ProceduralSounds.Creak, transform.position, 0.5f);
        }

        public bool Hit()
        {
            if (!Closed || Broken) return true;
            shake = 0.4f;
            ProceduralSounds.PlayAt(ProceduralSounds.Crash, transform.position, 0.6f);
            if (Braced)
            {
                PlayerController.Instance.Notify("You hold the door. It shudders against your shoulder.");
                return false;
            }
            blows++;
            if (blows < BlowsToBreak) return false;
            Broken = true;
            SetClosed(false);
            slab.localRotation = Quaternion.Euler(80f, 20f, 0f);
            slab.localPosition = new Vector3(0f, 0.2f, 1.2f);
            return true;
        }

        protected override void OnEnable()
        {
            base.OnEnable();
            Breachables.All.Add(this);
        }

        protected override void OnDisable()
        {
            base.OnDisable();
            Breachables.All.Remove(this);
        }
    }

    // The dam's radio. Design doc: "The Radio Room: load your evidence and hold out 90 s while it transmits."
    public sealed class Transmitter : Interactable
    {
        private static readonly List<Transmitter> Sets = new List<Transmitter>();

        public bool Started { get; private set; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => Sets.Clear();

        // The generator stalled (Nell went down mid-transmission): it has to be started again.
        public static void ResetAll()
        {
            foreach (Transmitter t in Sets) t.Started = false;
        }

        protected override void OnEnable()
        {
            base.OnEnable();
            Sets.Add(this);
        }

        protected override void OnDisable()
        {
            base.OnDisable();
            Sets.Remove(this);
        }

        public override KeyCode Key => KeyCode.E;
        public override float HoldSeconds => 2f;
        public override float Reach => 2f;
        public override bool Available => !Started && Campaign.Night == 5;
        public override string Prompt => "Hold E to load the evidence and start transmitting (the generator is loud)";

        public override void Complete(PlayerController player)
        {
            Started = true;
            if (NightDirector.Instance != null) NightDirector.Instance.StartTransmission(transform.position);
        }
    }

    // The spillway wheel. Design doc, Night 5: "open the spillway to wash him away, and flood your own route home."
    public sealed class SpillwayWheel : Interactable
    {
        private float creakTimer;
        private GameObject channelWater;

        public bool Opened { get; private set; }

        public override KeyCode Key => KeyCode.E;
        public override float HoldSeconds => 4f;
        public override float Reach => 2f;
        public override bool Available => !Opened && Campaign.Night == 5;
        public override string Prompt => "Hold E to open the spillway (25 m; washes him away and floods the road home)";

        public static SpillwayWheel Build(Vector3 position, Material iron, GameObject channelWater)
        {
            GameObject root = new GameObject("Spillway Wheel");
            root.transform.position = position;
            GameObject post = GameObject.CreatePrimitive(PrimitiveType.Cube);
            post.transform.SetParent(root.transform, false);
            post.transform.localPosition = new Vector3(0f, 0.5f, 0f);
            post.transform.localScale = new Vector3(0.3f, 1f, 0.3f);
            post.GetComponent<Renderer>().sharedMaterial = iron;
            GameObject wheel = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            Object.DestroyImmediate(wheel.GetComponent<Collider>());
            wheel.transform.SetParent(root.transform, false);
            wheel.transform.localPosition = new Vector3(0f, 1.1f, 0f);
            wheel.transform.localRotation = Quaternion.Euler(0f, 0f, 90f);
            wheel.transform.localScale = new Vector3(0.9f, 0.05f, 0.9f);
            wheel.GetComponent<Renderer>().sharedMaterial = iron;
            SpillwayWheel w = root.AddComponent<SpillwayWheel>();
            w.channelWater = channelWater;
            return w;
        }

        public override void Holding(PlayerController player, float progress)
        {
            creakTimer -= Time.deltaTime;
            if (creakTimer > 0f) return;
            creakTimer = 0.7f;
            ProceduralSounds.PlayAt(ProceduralSounds.Creak, transform.position, 0.8f);
            NoisePulse.Emit(transform.position, NoisePulse.Struggle, NoiseKind.Spillway);
        }

        public override void Complete(PlayerController player)
        {
            Opened = true;
            Campaign.SpillwayOpened = true;
            NoisePulse.Emit(transform.position, NoisePulse.Spillway, NoiseKind.Spillway);
            ProceduralSounds.PlayAt(ProceduralSounds.Roar, transform.position, 1f);
            if (channelWater != null) channelWater.SetActive(true);
            if (NightDirector.Instance != null) NightDirector.Instance.SpillwayOpened();
        }
    }

    // Design doc obstacle: "Rotten Floors. Waterlogged boards give way under a sprint. You drop into the cellar,
    // take fall damage and make a crash he can hear."
    public sealed class RottenFloor : MonoBehaviour
    {
        private Vector2 half;
        private bool broken;

        public static void Build(Vector3 centre, Vector2 size, Material boards)
        {
            GameObject patch = GameObject.CreatePrimitive(PrimitiveType.Cube);
            patch.name = "Rotten Boards";
            Object.DestroyImmediate(patch.GetComponent<Collider>());
            patch.transform.position = new Vector3(centre.x, 0.03f, centre.z);
            patch.transform.localScale = new Vector3(size.x, 0.05f, size.y);
            patch.GetComponent<Renderer>().sharedMaterial = boards;
            RottenFloor floor = patch.AddComponent<RottenFloor>();
            floor.half = size * 0.5f;
        }

        private void Update()
        {
            PlayerController player = PlayerController.Instance;
            if (broken || player == null || !player.IsSprinting) return;
            Vector3 d = player.transform.position - transform.position;
            if (Mathf.Abs(d.x) > half.x || Mathf.Abs(d.z) > half.y || d.y > 1.5f) return;
            broken = true;
            NoisePulse.Emit(transform.position, NoisePulse.Fall, NoiseKind.Fall);
            ProceduralSounds.PlayAt(ProceduralSounds.Crash, transform.position, 1f);
            player.FallThrough(GameSettings.HazardDamage);
            GetComponent<Renderer>().material.color = new Color(0.02f, 0.02f, 0.02f);
        }
    }

    // Design doc obstacle: "Flooded Passages. Cellars and dam tunnels are underwater. You swim on held breath,
    // and when it's empty you lose 10 Vitality a second."
    public sealed class WaterVolume : MonoBehaviour
    {
        private static readonly List<Bounds> Volumes = new List<Bounds>();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => Volumes.Clear();

        public static void Add(Bounds bounds) => Volumes.Add(bounds);

        public static void Clear() => Volumes.Clear();

        // True if the point (Nell's eyes) is underwater.
        public static bool Contains(Vector3 point)
        {
            foreach (Bounds b in Volumes)
                if (b.Contains(point)) return true;
            return false;
        }
    }

    // Something left behind that Nell can read: a letter, a calendar, a notice. Not evidence (it isn't banked or
    // counted); it tells the town's story to anyone who goes looking. Reading is silent and can be repeated.
    public sealed class Readable : Interactable
    {
        private string title;
        private string body;

        // The HUD shows the paper while it's open.
        public static event System.Action<string, string> Opened;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetEvents() => Opened = null;

        public override float Reach => 1.8f;
        public override KeyCode Key => KeyCode.E;
        public override float HoldSeconds => 0.25f;
        public override string Prompt => $"Hold E to read the {title.ToLowerInvariant()}";
        public override bool Available => true;

        public static Readable Build(Vector3 at, float yaw, string title, string body, bool onWall)
        {
            GameObject paper = GameObject.CreatePrimitive(onWall ? PrimitiveType.Quad : PrimitiveType.Cube);
            paper.name = "Readable: " + title;
            Object.DestroyImmediate(paper.GetComponent<Collider>());
            paper.transform.SetPositionAndRotation(at, Quaternion.Euler(0f, yaw, 0f));
            paper.transform.localScale = onWall ? new Vector3(0.45f, 0.6f, 1f) : new Vector3(0.24f, 0.01f, 0.32f);
            Material m = new Material(Shader.Find("Standard")) { color = new Color(0.72f, 0.68f, 0.58f) };
            m.SetFloat("_Glossiness", 0.05f);
            paper.GetComponent<Renderer>().sharedMaterial = m;
            // A faint warm glint so loose papers can be found in the dark, as evidence can.
            Light glint = new GameObject("Paper Glint").AddComponent<Light>();
            glint.transform.SetParent(paper.transform, false);
            glint.transform.position = at + Vector3.up * 0.4f + paper.transform.forward * (onWall ? -0.3f : 0f);
            glint.type = LightType.Point;
            glint.range = 1.4f;
            glint.intensity = 0.5f;
            glint.color = new Color(0.9f, 0.85f, 0.7f);
            Readable r = paper.AddComponent<Readable>();
            r.title = title;
            r.body = body;
            return r;
        }

        public override void Complete(PlayerController player)
        {
            ProceduralSounds.PlayAt(ProceduralSounds.Rustle, transform.position, 0.7f);
            Opened?.Invoke(title, body);
        }
    }
}

namespace Hushwater
{
    // Makes evidence easy to spot (ours): a soft golden column of light you can see down the street, a floating icon
    // of what it is (camera, page or tape), and a warm light on the spot. It all goes once the evidence is taken.
    public sealed class EvidenceBeacon : MonoBehaviour
    {
        private EvidenceSpot spot;
        private Transform icon, shaft;
        private SpriteRenderer iconRenderer;
        private float seed;

        public static void Build(EvidenceSpot spot)
        {
            var root = new GameObject("Beacon: " + spot.Title);
            root.transform.position = new Vector3(spot.Position.x, spot.Position.y, spot.Position.z);
            EvidenceBeacon beacon = root.AddComponent<EvidenceBeacon>();
            beacon.spot = spot;
            beacon.seed = UnityEngine.Random.value * 10f;

            GameObject column = GameObject.CreatePrimitive(PrimitiveType.Quad);
            UnityEngine.Object.DestroyImmediate(column.GetComponent<Collider>());
            column.name = "Shaft";
            column.transform.SetParent(root.transform, false);
            column.transform.localPosition = Vector3.up * 3.2f;
            column.transform.localScale = new Vector3(1.1f, 6.5f, 1f);
            column.GetComponent<Renderer>().sharedMaterial = ShaftMaterial;
            beacon.shaft = column.transform;

            var iconObject = new GameObject("Icon");
            iconObject.transform.SetParent(root.transform, false);
            iconObject.transform.localPosition = Vector3.up * 2.1f;
            iconObject.transform.localScale = Vector3.one * 0.42f;
            beacon.iconRenderer = iconObject.AddComponent<SpriteRenderer>();
            beacon.iconRenderer.sprite = Icons.Get(spot.Type == EvidenceType.Photo ? Icons.Kind.Camera : spot.Type == EvidenceType.Page ? Icons.Kind.Page : Icons.Kind.Tape);
            beacon.iconRenderer.color = new Color(1f, 0.82f, 0.45f, 0.95f);
            beacon.icon = iconObject.transform;

            Light light = new GameObject("Evidence Light").AddComponent<Light>();
            light.transform.SetParent(root.transform, false);
            light.transform.localPosition = Vector3.up * 1.3f;
            light.color = new Color(1f, 0.8f, 0.45f);
            light.range = 4.5f;
            light.intensity = 1.6f;
            light.shadows = LightShadows.None;
        }

        private static Material shaftMaterial;
        private static Material ShaftMaterial
        {
            get
            {
                if (shaftMaterial != null) return shaftMaterial;
                // A soft vertical gradient: bright at the ground, fading upward and at the sides.
                const int w = 32, h = 128;
                var tex = new Texture2D(w, h, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
                var px = new Color[w * h];
                for (int y = 0; y < h; y++)
                    for (int x = 0; x < w; x++)
                    {
                        float across = 1f - Mathf.Abs((x + 0.5f) / w * 2f - 1f);
                        float up = 1f - (float)y / h;
                        px[y * w + x] = new Color(1f, 1f, 1f, across * across * up * up);
                    }
                tex.SetPixels(px);
                tex.Apply();
                shaftMaterial = new Material(Shader.Find("Legacy Shaders/Particles/Additive") ?? Shader.Find("Sprites/Default"))
                {
                    mainTexture = tex,
                    renderQueue = 3100
                };
                shaftMaterial.SetColor("_TintColor", new Color(0.55f, 0.42f, 0.18f, 0.5f));
                shaftMaterial.color = new Color(1f, 0.8f, 0.4f, 0.35f);
                return shaftMaterial;
            }
        }

        private void LateUpdate()
        {
            if (Campaign.Has(spot.Id)) { Destroy(gameObject); return; }
            Camera cam = Camera.main;
            if (cam == null) return;
            // Both turn to face the camera around the vertical axis only.
            Vector3 to = cam.transform.position - transform.position;
            to.y = 0f;
            if (to.sqrMagnitude > 0.01f)
            {
                Quaternion face = Quaternion.LookRotation(-to);
                shaft.rotation = face;
                icon.rotation = face;
            }
            float t = Time.time + seed;
            icon.localPosition = Vector3.up * (2.1f + Mathf.Sin(t * 2f) * 0.12f);
            // Fade the shaft when she's standing in it, so it doesn't blind her.
            float near = Mathf.Clamp01((to.magnitude - 1.5f) / 4f);
            shaft.localScale = new Vector3(1.1f * (0.9f + 0.1f * Mathf.Sin(t * 3f)), 6.5f, 1f);
            shaft.gameObject.SetActive(near > 0.05f);
        }
    }
}
