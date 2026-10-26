using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

namespace Hushwater
{
    // Builds the grey-box street, player, Sexton and HUD at runtime.
    public sealed class HushwaterBootstrap : MonoBehaviour
    {
        private static readonly Color GroundColor = new Color(0.22f, 0.21f, 0.19f);
        private static readonly Color WallColor = new Color(0.32f, 0.35f, 0.37f);
        private static readonly Color PropColor = new Color(0.14f, 0.12f, 0.1f);

        private readonly Vector3[] patrolPoints =
        {
            new Vector3(0f, 0f, 18f), new Vector3(-5f, 0f, 8f), new Vector3(5f, 0f, 0f),
            new Vector3(0f, 0f, -10f), new Vector3(-5f, 0f, -2f), new Vector3(5f, 0f, 12f)
        };

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void StartGame()
        {
            if (FindAnyObjectByType<HushwaterBootstrap>() == null)
                new GameObject("Hushwater Runtime").AddComponent<HushwaterBootstrap>();
        }

        private void Start()
        {
            BuildStreet();
            BakeNavMesh();
            PlayerController player = BuildPlayer();
            SextonController sexton = BuildSexton(player);
            gameObject.AddComponent<HudController>().Initialize(player, sexton);
        }

        private void BuildStreet()
        {
            MakeCube("Ground", new Vector3(0f, -0.25f, 4f), new Vector3(30f, 0.5f, 40f), GroundColor);
            MakeCube("West Edge", new Vector3(-15f, 2f, 4f), new Vector3(0.5f, 4f, 40f), WallColor);
            MakeCube("East Edge", new Vector3(15f, 2f, 4f), new Vector3(0.5f, 4f, 40f), WallColor);
            MakeCube("South Edge", new Vector3(0f, 2f, -16f), new Vector3(30f, 4f, 0.5f), WallColor);
            MakeCube("North Edge", new Vector3(0f, 2f, 24f), new Vector3(30f, 4f, 0.5f), WallColor);

            BuildHouse(new Vector3(-9f, 0f, -6f), true);
            BuildHouse(new Vector3(9f, 0f, 2f), false);
            BuildHouse(new Vector3(-9f, 0f, 14f), false);
            MakeCube("Crate", new Vector3(3f, 0.4f, -4f), new Vector3(0.8f, 0.8f, 0.8f), PropColor);
            MakeCube("Crate", new Vector3(-3f, 0.4f, 6f), new Vector3(0.8f, 0.8f, 0.8f), PropColor);
            MakeCube("Cart", new Vector3(4f, 0.5f, 16f), new Vector3(1.4f, 1f, 2.4f), PropColor);

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
            MakeCube("House Back", new Vector3(centre.x - facing * 3f, 1.5f, centre.z), new Vector3(0.3f, 3f, 6f), WallColor);
            MakeCube("House Side", new Vector3(centre.x, 1.5f, centre.z - 3f), new Vector3(6f, 3f, 0.3f), WallColor);
            MakeCube("House Side", new Vector3(centre.x, 1.5f, centre.z + 3f), new Vector3(6f, 3f, 0.3f), WallColor);
            MakeCube("House Front", new Vector3(doorSide, 1.5f, centre.z - 2f), new Vector3(0.3f, 3f, 2f), WallColor);
            MakeCube("House Front", new Vector3(doorSide, 1.5f, centre.z + 2f), new Vector3(0.3f, 3f, 2f), WallColor);
            MakeCube("Lintel", new Vector3(doorSide, 2.6f, centre.z), new Vector3(0.3f, 0.8f, 2f), WallColor);
            MakeCube("Roof", new Vector3(centre.x, 3.1f, centre.z), new Vector3(6.4f, 0.2f, 6.4f), PropColor);
            if (withWardrobe) BuildWardrobe(new Vector3(centre.x - facing * 2.3f, 1f, centre.z + 1.5f), facing);
        }

        private void BuildWardrobe(Vector3 position, float facing)
        {
            GameObject wardrobe = new GameObject("Wardrobe (E to hide)");
            wardrobe.transform.position = position;
            MakeCube("Wardrobe Body", position, new Vector3(0.9f, 2f, 1.4f), PropColor).transform.SetParent(wardrobe.transform);
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

        private static PlayerController BuildPlayer()
        {
            GameObject playerObject = new GameObject("Nell");
            playerObject.transform.position = new Vector3(0f, 1f, -13f);
            CharacterController controller = playerObject.AddComponent<CharacterController>();
            controller.height = 1.8f;
            controller.radius = 0.35f;
            GameObject cameraObject = new GameObject("Player Camera");
            cameraObject.transform.SetParent(playerObject.transform, false);
            cameraObject.transform.localPosition = new Vector3(0f, 0.75f, 0f);
            Camera camera = cameraObject.AddComponent<Camera>();
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = RenderSettings.fogColor;
            cameraObject.AddComponent<AudioListener>();
            Light lantern = cameraObject.AddComponent<Light>();
            lantern.type = LightType.Point;
            lantern.color = new Color(1f, 0.75f, 0.45f);
            lantern.range = 7f;
            lantern.intensity = 1.2f;
            return playerObject.AddComponent<PlayerController>();
        }

        private SextonController BuildSexton(PlayerController player)
        {
            // Tall placeholder body (2.4 m) on a root that the NavMeshAgent moves along the ground.
            GameObject sexton = new GameObject("The Sexton (blind)");
            sexton.transform.position = new Vector3(0f, 0f, 20f);
            GameObject body = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            body.transform.SetParent(sexton.transform, false);
            body.transform.localPosition = new Vector3(0f, 1.2f, 0f);
            body.transform.localScale = new Vector3(0.8f, 1.2f, 0.8f);
            SextonController controller = sexton.AddComponent<SextonController>();
            controller.Setup(player, patrolPoints);
            return controller;
        }

        private static GameObject MakeCube(string name, Vector3 position, Vector3 scale, Color color)
        {
            GameObject cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
            cube.name = name;
            cube.transform.position = position;
            cube.transform.localScale = scale;
            cube.GetComponent<Renderer>().material.color = color;
            return cube;
        }
    }
}
