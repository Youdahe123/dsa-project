using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

namespace Hushwater
{
    // Runs a night: menu, intro, the dawn bell timer, evidence, checkpoints and the end screens.
    public sealed class NightDirector : MonoBehaviour
    {
        public enum Phase { Menu, Intro, Playing, Paused, Journal, Ended }

        public sealed class Photo
        {
            public Texture Image;
            public string Title;
            public bool Blurry;
        }

        public static NightDirector Instance { get; private set; }

        private Level level;
        private Camera menuCamera;
        private HudController hud;
        private MenuController menus;
        private LanternPost checkpoint;
        private AudioSource wind;
        private float menuOrbit;
        private float nightStart;
        private int timesSpotted;
        private bool enoughAnnounced;
        private bool played;
        private Difficulty builtDifficulty;

        public Phase Current { get; private set; }
        public Transform WorldRoot { get; private set; }
        public PlayerController Player { get; private set; }
        public SurveyCamera Survey { get; private set; }
        public PlayerLantern Lantern { get; private set; }
        public SextonController Sexton { get; private set; }
        public List<EvidenceSite> Evidence => level != null ? level.Evidence : new List<EvidenceSite>();
        public readonly List<Photo> Photos = new List<Photo>();
        public float TimeLeft { get; private set; }
        public float DeafTimeLeft { get; private set; }
        public int RequiredEvidence => 3;
        public int RequiredFound => Evidence.FindAll(e => e.Required && e.Found).Count;
        public int TotalFound => Evidence.FindAll(e => e.Found).Count;

        private void Awake()
        {
            Instance = this;
            QualitySettings.shadowDistance = 45f;
            QualitySettings.pixelLightCount = 8;
            QualitySettings.anisotropicFiltering = AnisotropicFiltering.Enable;
            QualitySettings.shadowCascades = 2;
            AudioListener.volume = GameSettings.MasterVolume;
            foreach (Camera stray in FindObjectsByType<Camera>()) stray.gameObject.SetActive(false);
            foreach (Light stray in FindObjectsByType<Light>()) stray.gameObject.SetActive(false);
        }

        private void Start()
        {
            hud = gameObject.AddComponent<HudController>();
            hud.Build(this);
            menus = gameObject.AddComponent<MenuController>();
            menus.Build(this);
            BuildWorld();
            ShowMenu();
            hud.SetFade(1f);
            hud.Fade(0f, 0.6f);
        }

        // ---------- world ----------

        private void BuildWorld()
        {
            if (WorldRoot != null)
            {
                level.NavMesh.Remove();
                DestroyImmediate(WorldRoot.gameObject);
            }
            MudTrail.Clear();
            Photos.Clear();
            WorldRoot = new GameObject("Millrow (Night 1)").transform;
            level = MillrowBuilder.Build(WorldRoot);
            builtDifficulty = GameSettings.Difficulty;

            menuCamera = MakeCamera("Menu camera", WorldRoot);
            menuCamera.gameObject.AddComponent<AudioListener>();

            wind = SoundFx.Loop(WorldRoot.gameObject, ProceduralAudio.Wind, 0.22f, 10f, false);
            GameObject shore = new GameObject("Lake lapping");
            shore.transform.SetParent(WorldRoot, false);
            shore.transform.position = new Vector3(0f, -0.4f, -32f);
            SoundFx.Loop(shore, ProceduralAudio.Lapping, 0.6f, 40f);
            StartCoroutine(Drips());
            StartCoroutine(RenderReflections());
        }

        private IEnumerator RenderReflections()
        {
            yield return null;
            if (level?.Probe != null) level.Probe.RenderProbe();
        }

        private IEnumerator Drips()
        {
            while (true)
            {
                yield return new WaitForSeconds(Random.Range(1.5f, 5f));
                Transform listener = Player != null ? Player.transform : menuCamera != null ? menuCamera.transform : null;
                if (listener == null) continue;
                Vector3 p = listener.position + new Vector3(Random.Range(-9f, 9f), Random.Range(0f, 2.5f), Random.Range(-9f, 9f));
                SoundFx.PlayAt(ProceduralAudio.Drip(Random.Range(0, 3)), p, 0.35f, 14f, Random.Range(0.9f, 1.1f));
            }
        }

        public static Camera MakeCamera(string name, Transform parent)
        {
            GameObject go = new GameObject(name);
            go.transform.SetParent(parent, false);
            Camera cam = go.AddComponent<Camera>();
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = RenderSettings.fogColor;
            cam.nearClipPlane = 0.03f;
            cam.farClipPlane = 140f;
            cam.fieldOfView = 62f;
            cam.allowHDR = true;
            cam.allowMSAA = true;
            go.AddComponent<ScreenGrade>();
            return cam;
        }

        // ---------- flow ----------

        private void ShowMenu()
        {
            Current = Phase.Menu;
            menuCamera.gameObject.SetActive(true);
            hud.SetGameplayVisible(false);
            menus.ShowMain();
            SetCursor(false);
        }

        public void NewGame()
        {
            StartCoroutine(BeginNight());
        }

        private IEnumerator BeginNight()
        {
            hud.Fade(1f, 2.5f);
            yield return new WaitForSecondsRealtime(0.45f);
            menus.HideAll();
            if (played || builtDifficulty != GameSettings.Difficulty) BuildWorld();
            played = true;
            if (Player != null) Destroy(Player.gameObject);
            if (Sexton != null) Destroy(Sexton.gameObject);

            SpawnPlayer();
            SpawnSexton();
            menuCamera.gameObject.SetActive(false);
            checkpoint = null;
            timesSpotted = 0;
            enoughAnnounced = false;
            TimeLeft = GameSettings.NightSeconds;
            nightStart = Time.time;
            DeafTimeLeft = 0f;

            Current = Phase.Intro;
            hud.SetGameplayVisible(true);
            hud.ShowTitle("NIGHT ONE  ·  " + GameSettings.DifficultyName.ToUpperInvariant(), "Millrow",
                "Photograph three pieces of evidence that the people of Hushwater never left.\nGet back to the boat before the dawn bell.\n\nHe cannot see you. He can hear everything. Walk softly.", 6.5f);
            hud.Fade(0f, 0.35f);
            yield return new WaitForSeconds(4.5f);
            if (Current != Phase.Intro) yield break;
            Current = Phase.Playing;
            Player.InputEnabled = true;
            SetCursor(true);
            Caption("Raise the survey camera with the right mouse button.", 5f);
        }

        private void SpawnPlayer()
        {
            GameObject nell = new GameObject("Nell Varga");
            nell.transform.SetParent(WorldRoot, false);
            nell.AddComponent<CharacterController>();
            Camera view = MakeCamera("Player camera", nell.transform);
            view.gameObject.AddComponent<AudioListener>();
            Player = nell.AddComponent<PlayerController>();
            Player.Teleport(level.PlayerSpawn, level.PlayerSpawnYaw);
            Survey = nell.AddComponent<SurveyCamera>();
            Survey.Setup(Player);
            Lantern = nell.AddComponent<PlayerLantern>();
            Lantern.Setup(Player);
        }

        private void SpawnSexton()
        {
            GameObject abel = new GameObject("The Sexton (Abel Crane)");
            abel.transform.SetParent(WorldRoot, false);
            Sexton = abel.AddComponent<SextonController>();
            Sexton.Setup(Player, level.SextonSpawn, (Vector3[])level.PatrolPoints.Clone());
        }

        private void Update()
        {
            float dt = Time.deltaTime;
            switch (Current)
            {
                case Phase.Menu:
                    menuOrbit += Time.unscaledDeltaTime * 0.025f;
                    // A slow drift up Millrow Lane toward the chapel.
                    Vector3 pos = new Vector3(Mathf.Sin(menuOrbit) * 2.2f + 1f, 2.4f + Mathf.Sin(menuOrbit * 1.7f) * 0.3f, -12f + Mathf.Sin(menuOrbit * 0.6f) * 6f);
                    menuCamera.transform.SetPositionAndRotation(pos, Quaternion.LookRotation(new Vector3(-3f, 6f, 42f) - pos));
                    break;
                case Phase.Playing:
                    TimeLeft -= dt;
                    DeafTimeLeft = Sexton != null && Sexton.Deaf ? DeafTimeLeft - dt : 0f;
                    if (TimeLeft <= 0f) DawnBell();
                    else if (Input.GetKeyDown(KeyCode.Escape)) Pause();
                    else if (Input.GetKeyDown(KeyCode.Tab)) OpenJournal();
                    if (Cursor.lockState != CursorLockMode.Locked && Input.GetMouseButtonDown(0)) SetCursor(true);
                    break;
                case Phase.Paused:
                    if (Input.GetKeyDown(KeyCode.Escape))
                    {
                        if (menus.SettingsOpen) menus.CloseSettings();
                        else Resume();
                    }
                    break;
                case Phase.Journal:
                    if (Input.GetKeyDown(KeyCode.Escape) || Input.GetKeyDown(KeyCode.Tab)) Resume();
                    break;
            }

            if (Player != null)
            {
                ScreenGrade grade = Player.View.GetComponent<ScreenGrade>();
                grade.Breath = Mathf.MoveTowards(grade.Breath, Player.HoldingBreath ? 1f : 0f, Time.unscaledDeltaTime * 2f);
            }
            if (wind != null) wind.volume = Mathf.MoveTowards(wind.volume, DeafTimeLeft > 0f ? 0.06f : 0.22f, Time.unscaledDeltaTime * 0.3f);
        }

        public void Pause()
        {
            Current = Phase.Paused;
            Time.timeScale = 0f;
            AudioListener.pause = true;
            Player.InputEnabled = false;
            menus.ShowPause();
            SetCursor(false);
        }

        public void OpenJournal()
        {
            Current = Phase.Journal;
            Time.timeScale = 0f;
            AudioListener.pause = true;
            Player.InputEnabled = false;
            menus.ShowJournal();
            SetCursor(false);
        }

        public void Resume()
        {
            Current = Phase.Playing;
            Time.timeScale = 1f;
            AudioListener.pause = false;
            menus.HideAll();
            Player.InputEnabled = true;
            SetCursor(true);
        }

        public void ReturnToMenu()
        {
            Time.timeScale = 1f;
            AudioListener.pause = false;
            StopAllCoroutines();
            if (Player != null) Destroy(Player.gameObject);
            if (Sexton != null) Destroy(Sexton.gameObject);
            Player = null;
            Sexton = null;
            BuildWorld();
            played = false;
            ShowMenu();
            hud.SetFade(1f);
            hud.Fade(0f, 1f);
        }

        private static void SetCursor(bool locked)
        {
            Cursor.lockState = locked ? CursorLockMode.Locked : CursorLockMode.None;
            Cursor.visible = !locked;
        }

        // ---------- events from the world ----------

        public void Caption(string text, float seconds, bool ambient = false)
        {
            if (hud == null) return;
            bool soundCaption = text.StartsWith("[");
            if (soundCaption && !GameSettings.Captions) return;
            if (ambient && Current != Phase.Playing) return;
            hud.Caption(text, seconds);
        }

        public void OnPhotograph(EvidenceSite site, Texture image, bool blurry, bool flash)
        {
            string title;
            string note;
            string stamp = null;
            if (site != null && !blurry)
            {
                site.MarkFound();
                title = site.Title;
                note = site.Note;
                Caption($"Evidence photographed: {site.Title}", 4f);
                if (!enoughAnnounced && RequiredFound >= RequiredEvidence)
                {
                    enoughAnnounced = true;
                    Caption("That's enough to prove it. Get back to the boat before the dawn bell.", 6f);
                }
            }
            else if (site != null)
            {
                title = "Too dark";
                note = "The shot is blurred past using. Dark rooms need the flash (R) — and the flash is loud.";
                stamp = "BLURRED";
            }
            else
            {
                title = "Nothing of note";
                note = "Frame the evidence in the viewfinder; the frame turns amber when something matters.";
                stamp = "NO EVIDENCE";
            }
            Photos.Insert(0, new Photo { Image = image, Title = title, Blurry = stamp != null });
            hud.ShowPhoto(image, title, note, stamp);
        }

        public void SetCheckpoint(LanternPost post)
        {
            checkpoint = post;
            Caption("Lantern post lit. If he catches you, you'll wake here.", 4f);
        }

        public void OnSpotted() => timesSpotted++;

        public void OnToll(float seconds) => DeafTimeLeft = seconds;

        public void TryLeave()
        {
            if (Current != Phase.Playing) return;
            if (RequiredFound < RequiredEvidence)
            {
                Caption($"Not yet. You need {RequiredEvidence} photographs of evidence ({RequiredFound} so far).", 3.5f);
                return;
            }
            StartCoroutine(EndNight(true, "Night One Survived",
                "Rowing home, the dawn bell rings behind you.\nYou were sure the tower was empty.", null, null));
        }

        public void PlayerDowned()
        {
            if (Current != Phase.Playing) return;
            string primary = checkpoint != null ? "Wake at the lantern post" : "Wake on the jetty";
            StartCoroutine(EndNight(false, "Night Lost", "The hook found you in the dark.", primary, RetryFromCheckpoint));
        }

        private void DawnBell()
        {
            StartCoroutine(EndNight(false, "The Dawn Bell", "The bell rang from the empty tower, and the light caught you in the open.", "Try the night again", NewGame));
        }

        private IEnumerator EndNight(bool won, string title, string line, string primary, UnityEngine.Events.UnityAction action)
        {
            Current = Phase.Ended;
            Player.InputEnabled = false;
            if (won || title == "The Dawn Bell") SoundFx.Play2D(ProceduralAudio.Toll, 0.8f);
            hud.Fade(1f, won ? 0.5f : 1.2f);
            yield return new WaitForSeconds(won ? 2.2f : 1.4f);
            float elapsed = Time.time - nightStart;
            string stats = $"Evidence  {RequiredFound}/{RequiredEvidence}" + (TotalFound > RequiredFound ? $"  (+{TotalFound - RequiredFound} extra)" : "") +
                           $"     Photographs  {Photos.Count}     Times hunted  {timesSpotted}     Time in town  {(int)(elapsed / 60f)}:{(int)(elapsed % 60f):00}";
            if (won) stats += "\n\nAnything extra you find changes the ending.";
            hud.SetGameplayVisible(false);
            menus.ShowEnd(won, title, line, stats, primary, action);
            SetCursor(false);
        }

        private void RetryFromCheckpoint()
        {
            menus.HideAll();
            Vector3 spot = checkpoint != null ? checkpoint.RespawnPoint.position : level.PlayerSpawn;
            if (NavMesh.SamplePosition(spot, out NavMeshHit hit, 3f, NavMesh.AllAreas)) spot = hit.position;
            Player.Teleport(spot, checkpoint != null ? checkpoint.transform.eulerAngles.y : level.PlayerSpawnYaw);
            Player.ResetVitals();
            if (Sexton != null) Destroy(Sexton.gameObject);
            SpawnSexton();
            TimeLeft = Mathf.Max(TimeLeft, 240f);
            hud.SetGameplayVisible(true);
            hud.Fade(0f, 0.5f);
            Current = Phase.Playing;
            Player.InputEnabled = true;
            SetCursor(true);
            Caption("You wake to the lap of the lake. The knocking is far away... for now.", 4f);
        }
    }
}
