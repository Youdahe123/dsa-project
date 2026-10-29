using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

namespace Hushwater
{
    // Builds the street, player, Sexton and HUD at runtime.
    public sealed class HushwaterBootstrap : MonoBehaviour
    {
        // Nell starts here, and has to come back here with the evidence.
        private static readonly Vector3 SouthGate = new Vector3(0f, 1f, -13f);

        private readonly Vector3[] patrolPoints =
        {
            new Vector3(0f, 0f, 18f), new Vector3(-5f, 0f, 8f), new Vector3(5f, 0f, 0f),
            new Vector3(0f, 0f, -10f), new Vector3(-5f, 0f, -2f), new Vector3(5f, 0f, 12f)
        };

        private Material ground;
        private Material siding;
        private Material roof;
        private Material iron;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void StartGame()
        {
            if (FindAnyObjectByType<HushwaterBootstrap>() == null)
                new GameObject("Hushwater Runtime").AddComponent<HushwaterBootstrap>();
        }

        // Build order matters: geometry first, then the NavMesh bake, then the agents that walk on it.
        private void Start()
        {
            LoadMaterials();
            BuildStreet();
            BuildProps();
            BakeNavMesh();
            PlayerController player = BuildPlayer();
            SextonController sexton = BuildSexton(player);
            gameObject.AddComponent<HudController>().Initialize(player, sexton);
        }

        // Textured materials shared by the street geometry.
        private void LoadMaterials()
        {
            ground = HushwaterAssets.Surface("brown_mud_02", new Color(0.55f, 0.52f, 0.47f), 8f);
            siding = HushwaterAssets.Surface("weathered_plank_siding", new Color(0.55f, 0.55f, 0.52f), 2f);
            roof = HushwaterAssets.Surface("roof_slates_02", new Color(0.5f, 0.5f, 0.5f), 3f);
            iron = HushwaterAssets.Surface("rusty_metal_02", new Color(0.6f, 0.55f, 0.5f));
        }

        // Ground, boundary walls, houses, the gate, and night lighting.
        private void BuildStreet()
        {
            MakeCube("Ground", new Vector3(0f, -0.25f, 4f), new Vector3(30f, 0.5f, 40f), ground);
            MakeCube("West Edge", new Vector3(-15f, 2f, 4f), new Vector3(0.5f, 4f, 40f), siding);
            MakeCube("East Edge", new Vector3(15f, 2f, 4f), new Vector3(0.5f, 4f, 40f), siding);
            MakeCube("South Edge", new Vector3(0f, 2f, -16f), new Vector3(30f, 4f, 0.5f), siding);
            MakeCube("North Edge", new Vector3(0f, 2f, 24f), new Vector3(30f, 4f, 0.5f), siding);

            BuildHouse(new Vector3(-9f, 0f, -6f), true);
            BuildHouse(new Vector3(9f, 0f, 2f), false);
            BuildHouse(new Vector3(-9f, 0f, 14f), false);

            // Two iron posts mark the south gate, the way out.
            MakeCube("Gate Post", new Vector3(-2f, 1.5f, -15.5f), new Vector3(0.3f, 3f, 0.3f), iron);
            MakeCube("Gate Post", new Vector3(2f, 1.5f, -15.5f), new Vector3(0.3f, 3f, 0.3f), iron);

            Light moon = new GameObject("Moonlight").AddComponent<Light>();
            moon.type = LightType.Directional;
            moon.transform.rotation = Quaternion.Euler(35f, -30f, 0f);
            moon.color = new Color(0.6f, 0.7f, 0.85f);
            moon.intensity = 0.35f;
            moon.shadows = LightShadows.Soft;
            RenderSettings.ambientLight = new Color(0.08f, 0.1f, 0.12f);
            RenderSettings.fog = true;
            RenderSettings.fogColor = new Color(0.07f, 0.09f, 0.1f);
            RenderSettings.fogDensity = 0.05f;
        }

        // A small house with a doorway facing the street; the first one has the wardrobe.
        private void BuildHouse(Vector3 centre, bool withWardrobe)
        {
            float facing = centre.x < 0f ? 1f : -1f;
            float doorSide = centre.x + facing * 3f;
            MakeCube("House Back", new Vector3(centre.x - facing * 3f, 1.5f, centre.z), new Vector3(0.3f, 3f, 6f), siding);
            MakeCube("House Side", new Vector3(centre.x, 1.5f, centre.z - 3f), new Vector3(6f, 3f, 0.3f), siding);
            MakeCube("House Side", new Vector3(centre.x, 1.5f, centre.z + 3f), new Vector3(6f, 3f, 0.3f), siding);
            MakeCube("House Front", new Vector3(doorSide, 1.5f, centre.z - 2f), new Vector3(0.3f, 3f, 2f), siding);
            MakeCube("House Front", new Vector3(doorSide, 1.5f, centre.z + 2f), new Vector3(0.3f, 3f, 2f), siding);
            MakeCube("Lintel", new Vector3(doorSide, 2.6f, centre.z), new Vector3(0.3f, 0.8f, 2f), siding);
            MakeCube("Roof", new Vector3(centre.x, 3.1f, centre.z), new Vector3(6.4f, 0.2f, 6.4f), roof);
            if (withWardrobe) BuildWardrobe(new Vector3(centre.x - facing * 2.3f, 1f, centre.z + 1.5f), facing);
        }

        // Places the wardrobe model plus an invisible trigger that makes it a HideSpot.
        private void BuildWardrobe(Vector3 position, float facing)
        {
            // The cabinet model faces the doorway; the hide spot itself is an invisible trigger around it.
            HushwaterAssets.Model("GothicCabinet_01", new Vector3(position.x, 0f, position.z), facing * 90f, 2.05f);
            GameObject wardrobe = new GameObject("Wardrobe (E to hide)");
            wardrobe.transform.position = position;
            wardrobe.layer = 2; // keep the trigger out of the NavMesh bake
            BoxCollider trigger = wardrobe.AddComponent<BoxCollider>();
            trigger.isTrigger = true;
            trigger.size = new Vector3(3f, 2f, 2.4f);
            HideSpot spot = wardrobe.AddComponent<HideSpot>();
            Transform exit = new GameObject("Exit").transform;
            exit.SetParent(wardrobe.transform);
            exit.position = position + new Vector3(facing * 1.3f, 0f, 0f);
            spot.SetExitPoint(exit);
        }

        // Poly Haven props, street lamps, and the things Nell can pick up.
        private void BuildProps()
        {
            HushwaterAssets.Model("wooden_crate_01", new Vector3(3f, 0f, -4f), 20f, 0.8f);
            HushwaterAssets.Model("wooden_crate_01", new Vector3(-3f, 0f, 6f), -15f, 0.8f);
            HushwaterAssets.Model("Barrel_01", new Vector3(4f, 0f, 16f), 0f, 0.9f);
            HushwaterAssets.Model("Barrel_01", new Vector3(4.7f, 0f, 16.8f), 60f, 0.9f);
            BuildLamp(new Vector3(-4.5f, 0f, -10f), 90f);
            BuildLamp(new Vector3(4.5f, 0f, 9f), -90f);

            // Three photographs of the drowned town: the evidence Nell came for.
            BuildEvidence(new Vector3(11f, 0f, 1f));
            BuildEvidence(new Vector3(-11f, 0f, 15.5f));
            BuildEvidence(new Vector3(6f, 0f, 20.5f));

            BuildBottle(new Vector3(5f, 0f, -8f));
            BuildBottle(new Vector3(-6f, 0f, 9.5f));
            BuildBottle(new Vector3(-10f, 0f, -8f));
        }

        // A street lamp model with a warm point light near the top.
        private static void BuildLamp(Vector3 position, float yaw)
        {
            HushwaterAssets.Model("street_lamp_01", position, yaw, 3.8f);
            Light light = new GameObject("Lamp Light").AddComponent<Light>();
            light.transform.position = position + Vector3.up * 3.5f;
            light.type = LightType.Point;
            light.color = new Color(1f, 0.7f, 0.4f);
            light.range = 9f;
            light.intensity = 1.1f;
        }

        // A photograph on the floor that counts toward the evidence goal.
        private static void BuildEvidence(Vector3 position)
        {
            GameObject photo = HushwaterAssets.Model("hanging_picture_frame_01", position, Random.Range(0f, 360f), 0.45f, false);
            // A faint glint so the photographs can be found in the dark.
            Light glint = new GameObject("Glint").AddComponent<Light>();
            glint.transform.SetParent(photo.transform, false);
            glint.transform.position = position + Vector3.up * 0.6f;
            glint.color = new Color(0.8f, 0.85f, 1f);
            glint.range = 1.8f;
            glint.intensity = 0.8f;
            Pickup.Create(photo, Pickup.Kind.Evidence, "photograph");
        }

        // A spare bottle lying on the ground that Nell can pick up and throw later.
        private static void BuildBottle(Vector3 position)
        {
            GameObject bottle = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            bottle.name = "Bottle";
            bottle.transform.position = position + Vector3.up * 0.15f;
            bottle.transform.localScale = new Vector3(0.12f, 0.15f, 0.12f);
            bottle.GetComponent<Renderer>().material.color = new Color(0.1f, 0.35f, 0.2f);
            Destroy(bottle.GetComponent<Collider>());
            Pickup.Create(bottle, Pickup.Kind.Bottle, "bottle");
        }

        // Runtime NavMesh bake so the Sexton can path around the houses (A* on the NavMesh).
        private static void BakeNavMesh()
        {
            var sources = new List<NavMeshBuildSource>();
            var bounds = new Bounds(new Vector3(0f, 1f, 4f), new Vector3(32f, 8f, 42f));
            NavMeshBuilder.CollectSources(bounds, ~(1 << 2), NavMeshCollectGeometry.PhysicsColliders, 0, new List<NavMeshBuildMarkup>(), sources);
            NavMeshBuildSettings settings = NavMesh.GetSettingsByID(0);
            settings.agentRadius = 0.4f;
            NavMeshData data = NavMeshBuilder.BuildNavMeshData(settings, sources, bounds, Vector3.zero, Quaternion.identity);
            NavMesh.RemoveAllNavMeshData();
            NavMesh.AddNavMeshData(data);
        }

        // Nell: a CharacterController with a camera, audio listener and lantern light at eye height.
        private static PlayerController BuildPlayer()
        {
            GameObject playerObject = new GameObject("Nell");
            playerObject.transform.position = SouthGate;
            CharacterController controller = playerObject.AddComponent<CharacterController>();
            controller.height = 1.8f;
            controller.radius = 0.35f;
            GameObject cameraObject = new GameObject("Player Camera");
            cameraObject.transform.SetParent(playerObject.transform, false);
            cameraObject.transform.localPosition = new Vector3(0f, 0.75f, 0f);
            Camera camera = cameraObject.AddComponent<Camera>();
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = RenderSettings.fogColor;
            camera.nearClipPlane = 0.05f;
            cameraObject.AddComponent<AudioListener>();
            Light lantern = cameraObject.AddComponent<Light>();
            lantern.type = LightType.Point;
            lantern.color = new Color(1f, 0.75f, 0.45f);
            lantern.range = 7f;
            lantern.intensity = 1.2f;
            PlayerController player = playerObject.AddComponent<PlayerController>();
            player.SetExit(SouthGate);
            return player;
        }

        // Creates the Sexton at the north end of the street and gives him his patrol route.
        private SextonController BuildSexton(PlayerController player)
        {
            // The body is built by SextonController.Setup; this root is what the NavMeshAgent moves.
            GameObject sexton = new GameObject("The Sexton (blind)");
            sexton.transform.position = new Vector3(0f, 0f, 20f);
            SextonController controller = sexton.AddComponent<SextonController>();
            controller.Setup(player, patrolPoints);
            return controller;
        }

        // A box primitive with a shared material, used for all the walls and floors.
        private static GameObject MakeCube(string name, Vector3 position, Vector3 scale, Material material)
        {
            GameObject cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
            cube.name = name;
            cube.transform.position = position;
            cube.transform.localScale = scale;
            cube.GetComponent<Renderer>().sharedMaterial = material;
            return cube;
        }
    }
}
