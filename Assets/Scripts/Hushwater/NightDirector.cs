using System;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Hushwater
{
    // Runs one night: the dawn bell timer, rowing out, losing the night, restarting at a lantern post, pausing,
    // and Night 5's transmission, chapel bell and spillway.
    public sealed class NightDirector : MonoBehaviour
    {
        public enum Outcome { Playing, Escaped, Caught, Dawn, Transmitted }

        // Design doc: "The Radio Room: load your evidence and hold out 90 s while it transmits."
        public const float TransmitSeconds = 90f;
        // Design doc, Night 5: "The bell tolls every 60 s." How long each toll drowns sound is ours.
        private const float BellEvery = 60f;
        private const float BellDrowns = 8f;
        private static readonly Vector3 ChapelBell = new Vector3(8f, 11f, 49f);

        private PlayerController player;
        private SextonController sexton;
        private Vector3 boat;
        private Vector3 generator;
        private float generatorPulse;
        private float bellTimer = BellEvery;
        private AudioSource generatorHum;

        public static NightDirector Instance { get; private set; }

        public Outcome Result { get; private set; } = Outcome.Playing;
        public bool Paused { get; private set; }
        public bool JournalOpen { get; private set; }
        // Seconds until the dawn bell. Design doc: 24 / 18 / 14 minutes by difficulty.
        public float TimeLeft { get; private set; }
        public float Elapsed { get; private set; }
        public bool Transmitting { get; private set; }
        public float TransmitLeft { get; private set; } = TransmitSeconds;
        public Story.Ending Ending { get; private set; }
        public bool CanRetryFromPost => Result == Outcome.Caught && player.Checkpoint != null;
        public Vector3 BoatPosition => boat;
        public bool NewEvidenceTonight => Campaign.FoundTonight.Count > 0;

        // Design doc: "Win a night: reach the boat before the dawn bell with at least one new piece of evidence."
        public string BoatPrompt => NewEvidenceTonight
            ? (Campaign.NightGoalMet(Campaign.Night) || Campaign.Night == 5 ? "Hold E to row out" : "Hold E to row out (tonight's evidence isn't all found yet)")
            : "Find at least one new piece of evidence before you row out";

        public event Action<bool> PauseChanged;
        public event Action<bool> JournalChanged;
        // Night 5's chapel bell, for the HUD captions.
        public event Action BellTolled;

        private void Awake() => Instance = this;

        public void Initialize(PlayerController target, SextonController enemy, Vector3 boatPosition)
        {
            player = target;
            sexton = enemy;
            boat = boatPosition;
            TimeLeft = GameSettings.DawnSeconds;
            // He guards the truth: every piece Nell takes becomes a stop on his patrol.
            player.EvidenceTaken += sexton.GuardSite;
            if (Campaign.Night == 5) sexton.Relentless = true;
        }

        // ---------- Called by the world ----------

        public void TryRowOut()
        {
            if (Result != Outcome.Playing || player == null || player.IsDead) return;
            if (!NewEvidenceTonight)
            {
                player.Notify("Not with nothing to show for it. Find at least one new piece of evidence.");
                return;
            }
            Campaign.BankNight(player);
            End(Outcome.Escaped);
        }

        // The generator starts: 90 s of transmission, and noise he can follow all the way to the radio room.
        public void StartTransmission(Vector3 position)
        {
            if (Transmitting) return;
            Transmitting = true;
            TransmitLeft = TransmitSeconds;
            generator = position;
            generatorPulse = 0f;
            generatorHum = new GameObject("Generator Hum").AddComponent<AudioSource>();
            generatorHum.transform.position = position;
            generatorHum.clip = ProceduralSounds.Hum;
            generatorHum.loop = true;
            generatorHum.spatialBlend = 1f;
            generatorHum.maxDistance = 30f;
            generatorHum.Play();
            player.Notify("The generator coughs into life. Hold out for 90 seconds. He will hear it.");
        }

        public void SpillwayOpened()
        {
            Rect ch = TownBuilder.SpillwayChannel;
            WaterVolume.Add(new Bounds(new Vector3(ch.center.x, 0.6f, ch.center.y), new Vector3(ch.width, 3.2f, ch.height)));
            if (sexton != null) sexton.WashAway();
            player.Notify("The spillway roars open. The bell goes tumbling downstream, and the road home is under water.");
        }

        // ---------- The night ----------

        private void Update()
        {
            if (player == null) return;
            if (Result == Outcome.Playing)
            {
                if (GameInput.Down(KeyCode.Escape))
                {
                    if (JournalOpen) SetJournal(false);
                    else SetPaused(!Paused);
                }
                else if (GameInput.Down(KeyCode.Tab) && !Paused) SetJournal(!JournalOpen);
                if (Paused || JournalOpen) return;

                Elapsed += Time.deltaTime;
                TimeLeft = Mathf.Max(0f, TimeLeft - Time.deltaTime);
                if (Campaign.Night == 5) TollChapelBell();
                if (Transmitting) UpdateTransmission();
                if (Result != Outcome.Playing) return;

                if (player.DeathFade >= 1f) End(Outcome.Caught);
                else if (TimeLeft <= 0f)
                {
                    ProceduralSounds.PlayAt(ProceduralSounds.Bell, player.transform.position, 1f);
                    End(Outcome.Dawn);
                }
                return;
            }

            // Night over.
            if (CanRetryFromPost && GameInput.Down(KeyCode.R)) RetryFromPost();
            else if (GameInput.Down(KeyCode.E) || Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter)) Continue();
        }

        // "The bell tolls every 60 s", drowning every sound for a few seconds.
        private void TollChapelBell()
        {
            bellTimer -= Time.deltaTime;
            if (bellTimer > 0f) return;
            bellTimer = BellEvery;
            NoisePulse.DrownedUntil = Time.time + BellDrowns;
            ProceduralSounds.PlayAt(ProceduralSounds.Bell, player.transform.position + (ChapelBell - player.transform.position).normalized * 6f, 1f);
            BellTolled?.Invoke();
        }

        private void UpdateTransmission()
        {
            TransmitLeft -= Time.deltaTime;
            generatorPulse -= Time.deltaTime;
            if (generatorPulse <= 0f)
            {
                generatorPulse = 2f;
                NoisePulse.Emit(generator, NoisePulse.Generator, NoiseKind.Generator);
            }
            if (TransmitLeft > 0f) return;
            Transmitting = false;
            if (generatorHum != null) Destroy(generatorHum.gameObject);
            int pages = Campaign.Count(EvidenceType.Page);
            int tapes = Campaign.Count(EvidenceType.Tape);
            Ending = Story.EndingFor(pages, tapes, Campaign.SpillwayOpened);
            Campaign.Finish(Ending, player);
            End(Outcome.Transmitted);
        }

        // Stops play and frees the mouse for the end card.
        private void End(Outcome outcome)
        {
            Result = outcome;
            player.InputLocked = true;
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
            if (outcome == Outcome.Dawn && Campaign.Night == 5)
            {
                // Design doc ending "Silence: the transmission fails. The rains come."
                Ending = Story.Ending.Silence;
                Campaign.Finish(Ending, player);
            }
        }

        // Leaves the night for the boat, or the ending.
        private void Continue()
        {
            switch (Result)
            {
                case Outcome.Escaped:
                    Campaign.Next = Campaign.Mode.Boat;
                    break;
                case Outcome.Transmitted:
                    Campaign.Next = Campaign.Mode.Ending;
                    break;
                case Outcome.Dawn when Campaign.Night == 5:
                    Campaign.Next = Campaign.Mode.Ending;
                    break;
                default:
                    // Design doc: "Lose a night: Vitality hits 0, or he lands a full grab." What she found goes back into the silt.
                    Campaign.LoseNight();
                    Campaign.BoatMessage = Result == Outcome.Dawn
                        ? $"The dawn bell rang before you reached the boat. Night {Campaign.Night} is lost; row back in tonight."
                        : $"He found you. You came to in the boat, drifting. Night {Campaign.Night} is lost; row back in tonight.";
                    Campaign.Next = Campaign.Mode.Boat;
                    break;
            }
            Load();
        }

        // Puts Nell back at her lit lantern post and sends the Sexton to the far end of his route.
        private void RetryFromPost()
        {
            LanternPost post = player.Checkpoint;
            player.Respawn(post.RespawnPoint, post.transform.rotation);
            if (!sexton.WashedAway) sexton.ResetTo(post.transform.position);
            if (Transmitting)
            {
                // The generator stalled while she was down.
                Transmitting = false;
                if (generatorHum != null) Destroy(generatorHum.gameObject);
                Transmitter.ResetAll();
            }
            player.InputLocked = false;
            Result = Outcome.Playing;
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }

        // Starts tonight over from the boat (nothing found tonight is kept).
        public void Restart()
        {
            Campaign.LoseNight();
            Campaign.Next = Campaign.Mode.Night;
            Load();
        }

        public void MainMenu()
        {
            Campaign.LoseNight();
            Campaign.Next = Campaign.Mode.Menu;
            Load();
        }

        public static void Load()
        {
            Time.timeScale = 1f;
            AudioListener.pause = false;
            int index = SceneManager.GetActiveScene().buildIndex;
            SceneManager.LoadScene(index >= 0 ? index : 0);
        }

        // Esc pauses: the game freezes (the Sexton stops mid-step), audio pauses and the mouse is freed for the menu.
        public void SetPaused(bool paused)
        {
            if (Result != Outcome.Playing) return;
            Paused = paused;
            Freeze(paused || JournalOpen);
            PauseChanged?.Invoke(paused);
        }

        // Design doc: "Journal & map: Tab." It pauses the night too.
        public void SetJournal(bool open)
        {
            if (Result != Outcome.Playing) return;
            if (open && Paused)
            {
                Paused = false;
                PauseChanged?.Invoke(false);
            }
            JournalOpen = open;
            Freeze(open || Paused);
            JournalChanged?.Invoke(open);
        }

        private void Freeze(bool frozen)
        {
            Time.timeScale = frozen ? 0f : 1f;
            AudioListener.pause = frozen;
            player.InputLocked = frozen;
            Cursor.lockState = frozen ? CursorLockMode.None : CursorLockMode.Locked;
            Cursor.visible = frozen;
        }

        public static void Quit()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
            Time.timeScale = 1f;
            AudioListener.pause = false;
        }
    }
}
