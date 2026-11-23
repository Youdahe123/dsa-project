using UnityEngine;

namespace Hushwater
{
    // The only thing in the scene. On every load it builds whichever screen the campaign is on:
    // the main menu, a night in town, the boat between nights, or the ending.
    public sealed class HushwaterBootstrap : MonoBehaviour
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void StartGame()
        {
            if (FindAnyObjectByType<HushwaterBootstrap>() == null)
                new GameObject("Hushwater Runtime").AddComponent<HushwaterBootstrap>();
        }

        private void Start()
        {
            GameSettings.LoadPrefs();
            Time.timeScale = 1f;
            AudioListener.pause = false;
            switch (Campaign.Next)
            {
                case Campaign.Mode.Boat: gameObject.AddComponent<BoatScreen>(); break;
                case Campaign.Mode.Ending: gameObject.AddComponent<EndingScreen>(); break;
                case Campaign.Mode.Night: BuildNight(); break;
                default: gameObject.AddComponent<MenuScreen>(); break;
            }
        }

        // Build order matters: geometry first, then the NavMesh bake, then the agents that walk on it.
        private void BuildNight()
        {
            // A fresh night: lock in the difficulty and forget the last night's grooves, pools and hands.
            GameSettings.BeginNight();
            MudTracks.Clear();
            SiltHands.Clear();
            WaterVolume.Clear();
            Campaign.FoundTonight.Clear();
            NoisePulse.DrownedUntil = 0f;

            int night = Campaign.Night;
            TownBuilder town = new TownBuilder(night);
            town.Build();

            PlayerController player = BuildPlayer(town.NellStart);
            Campaign.RestoreTools(player);
            SextonController sexton = BuildSexton(player, town);

            NightDirector director = gameObject.AddComponent<NightDirector>();
            director.Initialize(player, sexton, town.Boat);
            HudController hud = gameObject.AddComponent<HudController>();
            hud.Initialize(player, sexton, director);
            gameObject.AddComponent<NoiseDebugView>().Initialize(sexton, hud.Root);
            gameObject.AddComponent<Ambience>();
        }

        // Nell: a CharacterController with a camera, audio listener and her crank lantern at eye height,
        // the survey camera, and the lantern's charge.
        private static PlayerController BuildPlayer(Vector3 start)
        {
            GameObject playerObject = new GameObject("Nell");
            playerObject.transform.SetPositionAndRotation(start, Quaternion.identity);
            CharacterController controller = playerObject.AddComponent<CharacterController>();
            controller.height = 1.8f;
            controller.radius = 0.35f;
            GameObject cameraObject = new GameObject("Player Camera");
            cameraObject.tag = "MainCamera";
            cameraObject.transform.SetParent(playerObject.transform, false);
            cameraObject.transform.localPosition = new Vector3(0f, 0.75f, 0f);
            Camera camera = cameraObject.AddComponent<Camera>();
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = RenderSettings.fogColor;
            camera.nearClipPlane = 0.05f;
            // Far enough for the moon, stars and the valley ridges (they ignore fog; the town doesn't).
            camera.farClipPlane = 600f;
            cameraObject.AddComponent<AudioListener>();
            // Nell's crank lantern: a warm glow all round her, bright enough to read the street by.
            Light lantern = cameraObject.AddComponent<Light>();
            lantern.type = LightType.Point;
            lantern.color = new Color(1f, 0.8f, 0.55f);
            lantern.range = 14f;
            lantern.intensity = 2.4f;
            lantern.shadows = LightShadows.None;
            // Plus a wide beam ahead, the way a lantern held up throws light where you look.
            Light beam = new GameObject("Lantern Beam").AddComponent<Light>();
            beam.transform.SetParent(cameraObject.transform, false);
            beam.transform.localPosition = new Vector3(0.25f, -0.2f, 0.1f);
            beam.type = LightType.Spot;
            beam.color = new Color(1f, 0.82f, 0.6f);
            beam.spotAngle = 75f;
            beam.range = 24f;
            beam.intensity = 2f;
            beam.gameObject.AddComponent<LanternBeam>();
            PlayerController player = playerObject.AddComponent<PlayerController>();
            playerObject.AddComponent<SurveyCamera>();
            playerObject.AddComponent<NellLantern>();
            new GameObject("Ground Mist", typeof(GroundMist)).transform.SetParent(playerObject.transform, false);
            return player;
        }

        private static SextonController BuildSexton(PlayerController player, TownBuilder town)
        {
            // The body is built by SextonController.Setup; this root is what the NavMeshAgent moves.
            GameObject sexton = new GameObject("The Sexton (blind)");
            sexton.transform.position = town.SextonStart;
            SextonController controller = sexton.AddComponent<SextonController>();
            controller.Setup(player, town.PatrolPoints.ToArray());
            return controller;
        }
    }

    // Design doc, "Sound": "No music while exploring. Only dripping water, wind in broken windows and the lap of the lake."
    // "Chases get a score: a single hymn on a waterlogged, out-of-tune organ."
    public sealed class Ambience : MonoBehaviour
    {
        private AudioSource wind;
        private AudioSource organ;
        private float dripTimer = 3f;
        private SextonController sexton;

        private void Start()
        {
            sexton = FindAnyObjectByType<SextonController>();
            wind = Loop(ProceduralSounds.Wind, 0.25f);
            organ = Loop(ProceduralSounds.Organ, 0f);
        }

        private AudioSource Loop(AudioClip clip, float volume)
        {
            AudioSource source = gameObject.AddComponent<AudioSource>();
            source.clip = clip;
            source.loop = true;
            source.volume = volume;
            source.Play();
            return source;
        }

        private void Update()
        {
            PlayerController player = PlayerController.Instance;
            if (player == null) return;
            bool chase = sexton != null && !sexton.WashedAway && sexton.CurrentState == SextonController.State.Hunt;
            organ.volume = Mathf.MoveTowards(organ.volume, chase ? 0.35f : 0f, Time.deltaTime * (chase ? 0.5f : 0.15f));
            wind.volume = player.Underwater ? 0.05f : 0.25f;
            dripTimer -= Time.deltaTime;
            if (dripTimer > 0f) return;
            dripTimer = Random.Range(2f, 6f);
            Vector2 offset = Random.insideUnitCircle * 6f;
            ProceduralSounds.PlayAt(ProceduralSounds.Drip, player.transform.position + new Vector3(offset.x, 2f, offset.y), 0.3f);
        }
    }
}
