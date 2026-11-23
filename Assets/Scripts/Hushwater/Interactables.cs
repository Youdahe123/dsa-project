using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

namespace Hushwater
{
    // Anything Nell interacts with by holding a key: lantern posts, barred doors, supply caches.
    // The player finds the nearest one in reach, fills a progress bar while the key is held, then calls Complete.
    public abstract class Interactable : MonoBehaviour
    {
        private static readonly List<Interactable> All = new List<Interactable>();

        // How close Nell has to stand.
        public virtual float Reach => 2.2f;
        // Which key to hold.
        public abstract KeyCode Key { get; }
        // Seconds the key has to be held.
        public abstract float HoldSeconds { get; }
        // The HUD prompt, e.g. "Hold F to crank the lantern post".
        public abstract string Prompt { get; }
        // False once it has been used up (a lit post, a broken door, an emptied cache).
        public abstract bool Available { get; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => All.Clear();

        // Interactables register themselves so the player can find them without physics triggers.
        protected virtual void OnEnable() => All.Add(this);
        protected virtual void OnDisable() => All.Remove(this);

        // Called every frame the key is held, with the progress so far (0..1). Used for the noise of the work.
        public virtual void Holding(PlayerController player, float progress) { }

        // Called once when the hold finishes.
        public abstract void Complete(PlayerController player);

        // The closest available interactable within its reach of the point, or null.
        public static Interactable Nearest(Vector3 point)
        {
            Interactable best = null;
            float bestDistance = float.MaxValue;
            foreach (Interactable item in All)
            {
                if (!item.Available) continue;
                float distance = Vector3.Distance(point, item.transform.position);
                if (distance < item.Reach && distance < bestDistance)
                {
                    best = item;
                    bestDistance = distance;
                }
            }
            return best;
        }
    }

    // A checkpoint. Crank it alight (12 m of noise) and Nell restarts here if he catches her.
    public sealed class LanternPost : Interactable
    {
        private Light lamp;
        private Renderer glass;
        private float crankTimer;

        public bool Lit { get; private set; }
        // Where Nell stands when she restarts here.
        public Vector3 RespawnPoint => transform.position + transform.forward * 1.2f + Vector3.up;

        public override KeyCode Key => KeyCode.F;
        public override float HoldSeconds => 3f;
        public override string Prompt => "Hold F to crank the lantern post";
        public override bool Available => !Lit;

        // Builds the post: an iron pole, a glass lamp box and an unlit light.
        public static LanternPost Build(Vector3 position, Material iron)
        {
            GameObject root = new GameObject("Lantern Post");
            root.transform.position = position;
            GameObject pole = GameObject.CreatePrimitive(PrimitiveType.Cube);
            pole.transform.SetParent(root.transform, false);
            pole.transform.localPosition = new Vector3(0f, 1f, 0f);
            pole.transform.localScale = new Vector3(0.15f, 2f, 0.15f);
            pole.GetComponent<Renderer>().sharedMaterial = iron;

            GameObject box = GameObject.CreatePrimitive(PrimitiveType.Cube);
            Object.DestroyImmediate(box.GetComponent<Collider>());
            box.transform.SetParent(root.transform, false);
            box.transform.localPosition = new Vector3(0f, 2.15f, 0f);
            box.transform.localScale = new Vector3(0.3f, 0.35f, 0.3f);

            LanternPost post = root.AddComponent<LanternPost>();
            post.glass = box.GetComponent<Renderer>();
            post.glass.material.color = new Color(0.15f, 0.14f, 0.12f);
            post.lamp = new GameObject("Post Light").AddComponent<Light>();
            post.lamp.transform.SetParent(root.transform, false);
            post.lamp.transform.localPosition = new Vector3(0f, 2.15f, 0f);
            post.lamp.type = LightType.Point;
            post.lamp.color = new Color(0.94f, 0.76f, 0.42f); // lantern amber, #F0C36A
            post.lamp.range = 8f;
            post.lamp.intensity = 0f;
            return post;
        }

        // Each half-turn of the crank is a 12 m noise.
        public override void Holding(PlayerController player, float progress)
        {
            crankTimer -= Time.deltaTime;
            if (crankTimer > 0f) return;
            crankTimer = 0.5f;
            NoisePulse.Emit(transform.position, Campaign.Owns(Upgrade.Crank) ? NoisePulse.CrankGeared : NoisePulse.Crank, NoiseKind.Crank);
            ProceduralSounds.PlayAt(ProceduralSounds.Crank, transform.position, 0.7f);
            lamp.intensity = progress * 0.5f; // flickers up as she cranks
        }

        // Lights the post and makes it Nell's restart point.
        public override void Complete(PlayerController player)
        {
            Lit = true;
            lamp.intensity = 1.4f;
            glass.material.color = new Color(1f, 0.85f, 0.5f);
            glass.material.EnableKeyword("_EMISSION");
            glass.material.SetColor("_EmissionColor", new Color(0.94f, 0.76f, 0.42f));
            player.SetCheckpoint(this);
        }
    }

    // A door nailed shut on the flood night. Forcing it is the loudest thing Nell can do (25 m).
    // Better: lure the Sexton into smashing it for her.
    public sealed class BarredDoor : Interactable, IBreachable
    {
        private readonly List<GameObject> planks = new List<GameObject>();
        private string id;

        public bool Broken { get; private set; }
        // A point on the street side of the door, where the Sexton stands to smash it.
        public Vector3 Approach { get; private set; }
        public Vector3 Position => transform.position;
        public float BlockRange => 6f;

        public static BarredDoor Near(Vector3 point, float range)
        {
            return Breachables.Near(point) as BarredDoor;
        }

        public override KeyCode Key => KeyCode.E;
        public override float HoldSeconds => 2.5f;
        public override string Prompt => "Hold E to force the barred door (25 m, very loud)";
        public override bool Available => !Broken;
        public override float Reach => 1.8f;

        // Builds the door slab (facing along x) and two planks nailed across its street side.
        // A carving NavMeshObstacle blocks the Sexton until it breaks.
        public static BarredDoor Build(Vector3 position, Vector3 size, Material wood, Material plankMaterial, float outward, string id = "")
        {
            // A door broken on an earlier night stays broken.
            if (Campaign.DoorBroken(id)) return null;
            GameObject slab = GameObject.CreatePrimitive(PrimitiveType.Cube);
            slab.name = "Barred Door";
            slab.layer = 2; // kept out of the NavMesh bake; the obstacle below carves the hole instead
            slab.transform.position = position;
            slab.transform.localScale = size;
            slab.GetComponent<Renderer>().sharedMaterial = wood;
            NavMeshObstacle obstacle = slab.AddComponent<NavMeshObstacle>();
            obstacle.shape = NavMeshObstacleShape.Box; // the default capsule would carve a 2 m circle around the doorway
            obstacle.size = Vector3.one;
            obstacle.carving = true;
            BarredDoor door = slab.AddComponent<BarredDoor>();
            door.id = id;
            // outward is +-1 along x for doors in east/west walls; doors in north/south walls pass +-2 to face along z.
            bool alongZ = Mathf.Abs(outward) > 1.5f;
            float side = Mathf.Sign(outward);
            door.Approach = alongZ
                ? new Vector3(position.x, 0f, position.z + side * 0.9f)
                : new Vector3(position.x + side * 0.9f, 0f, position.z);

            // Separate objects rather than children, so the slab's non-uniform scale doesn't skew them.
            foreach (float tilt in new[] { 35f, -35f })
            {
                GameObject plank = GameObject.CreatePrimitive(PrimitiveType.Cube);
                plank.name = "Plank";
                Object.DestroyImmediate(plank.GetComponent<Collider>());
                plank.transform.position = alongZ
                    ? position + new Vector3(0f, 0f, side * (size.z * 0.5f + 0.05f))
                    : position + new Vector3(side * (size.x * 0.5f + 0.05f), 0f, 0f);
                plank.transform.rotation = alongZ ? Quaternion.Euler(0f, 0f, tilt) : Quaternion.Euler(tilt, 0f, 0f);
                plank.transform.localScale = alongZ ? new Vector3(size.x * 1.15f, 0.22f, 0.08f) : new Vector3(0.08f, 0.22f, size.z * 1.15f);
                plank.GetComponent<Renderer>().sharedMaterial = plankMaterial;
                door.planks.Add(plank);
            }
            return door;
        }

        // Nell forced it open: a 25 m crash the Sexton can hear across the whole street.
        public override void Complete(PlayerController player) => Break(true);

        // Breaks the door. Called by Nell (loud) or by the Sexton smashing through (he doesn't hear his own crash).
        public void Break(bool byNell)
        {
            if (Broken) return;
            Broken = true;
            Campaign.BreakDoor(id);
            if (byNell) NoisePulse.Emit(transform.position, NoisePulse.Door, NoiseKind.Door);
            ProceduralSounds.PlayAt(ProceduralSounds.Crash, transform.position, 1f);
            foreach (GameObject plank in planks) Destroy(plank);
            Destroy(gameObject);
        }

        // One blow from the Sexton is enough for a nailed door.
        public bool Hit()
        {
            Break(false);
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

    // A crate Nell can rummage through. Design doc: "Supply Caches (chance a search finds one)" 60/40/20%.
    public sealed class SupplyCache : Interactable
    {
        private bool searched;
        private float rummageTimer;

        public override KeyCode Key => KeyCode.E;
        public override float HoldSeconds => 1.5f;
        public override string Prompt => "Hold E to search the crate";
        public override bool Available => !searched;
        public override float Reach => 1.8f;

        // Adds a cache to a crate model already in the street.
        public static SupplyCache Attach(GameObject crate) => crate.AddComponent<SupplyCache>();

        // Rummaging makes a little noise, about as loud as walking.
        public override void Holding(PlayerController player, float progress)
        {
            rummageTimer -= Time.deltaTime;
            if (rummageTimer > 0f) return;
            rummageTimer = 0.75f;
            NoisePulse.Emit(transform.position, NoisePulse.Rummage, NoiseKind.Rummage);
        }

        // Rolls the difficulty's chance; a find is a random tool.
        public override void Complete(PlayerController player)
        {
            searched = true;
            if (Random.value > GameSettings.CacheChance)
            {
                player.Notify("The crate is empty.");
                return;
            }
            // Design doc: flash bulbs are "rare before Night 3". The loot table itself is ours.
            var loot = new List<Pickup.Kind> { Pickup.Kind.Bottle, Pickup.Kind.Handbell, Pickup.Kind.Salts, Pickup.Kind.Wraps,
                Pickup.Kind.LampOil, Pickup.Kind.Brass, Pickup.Kind.Brass, Pickup.Kind.Wire, Pickup.Kind.Lens };
            if (Campaign.Night >= 3) loot.Add(Pickup.Kind.FlashBulbs);
            Pickup.Kind found = loot[Random.Range(0, loot.Count)];
            if (!player.Take(found)) player.Notify("Found something, but your pockets are full.");
        }
    }
}
