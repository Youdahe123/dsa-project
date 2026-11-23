using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

namespace Hushwater
{
    // Sounds the Sexton makes. The HUD turns them into directional captions; Nell never sees him on a map.
    public enum SextonSound { SlowKnock, FastKnock, Scrape, Hymn, Bell, Smash, Feeling }

    // The blind Sexton: reacts only to noise pulses that reach him, and to grooves in the mud he can feel with his hook.
    public sealed class SextonController : MonoBehaviour
    {
        // Patrol: walks between evidence sites. Investigate: walks to a sound, then sweeps his hook and listens.
        // Hunt: runs at the latest sound. Search: lost Nell nearby, feels inside hiding spots within 10 m.
        // Toll: Nell got out of reach, so he rings his bell for 30 s and hears nothing. Recover: just knocked Nell down.
        // Kill: holding Nell, game over.
        public enum State { Patrol, Investigate, Hunt, Search, Toll, Recover, Kill }

        [SerializeField] private float walkSpeed = 1.6f;
        [SerializeField] private float investigateSpeed = 2.4f;
        [SerializeField] private float huntSpeed = 4.6f;
        [Header("Attacks (design doc: swipe at 2 m, grab at 1 m; damage comes from GameSettings)")]
        [SerializeField] private float swipeRange = 2f;
        [SerializeField] private float grabRange = 1f;
        [SerializeField] private float swipeCooldown = 1.5f;
        // After a knockdown he can't grab again for this long, so Nell has a real chance to get away. Not in the doc.
        [SerializeField] private float grabCooldownAfterKnockdown = 6f;
        [Header("Senses")]
        // How close he has to be to hear Nell breathing. Holding breath hides it.
        [SerializeField] private float breathHearing = 3f;
        // Each wall between a sound and him multiplies its reach by this. The doc says hearing "checks distance and walls"
        // but gives no number, so this is a starting value.
        [SerializeField] private float wallMuffle = 0.5f;
        // Design doc: "Search: feels inside hiding spots within 10 m".
        [SerializeField] private float searchRange = 10f;
        // Design doc: "Toll: you got out of reach. His bell drowns all sound for 30 s." Out of reach is our number.
        [SerializeField] private float tollDistance = 20f;
        [SerializeField] private float tollSeconds = 30f;

        private readonly List<Vector3> route = new List<Vector3>();
        private readonly List<float> recentSounds = new List<float>();
        private List<HideSpot> searchQueue = new List<HideSpot>();
        private NavMeshAgent agent;
        private PlayerController player;
        private SextonBody body;
        private AudioSource voice;
        private State state;
        private Vector3 goal;
        private int patrolIndex;
        private float lastHeardTime;
        private float swipeTimer;
        private float grabCooldown;
        private float knockTimer;
        private float scrapeTimer = 6f;
        private float repathTimer;
        // The barred door (or the radio room door) he is walking to so he can smash it, if any.
        private IBreachable breaching;
        private float blowTimer;
        // Night 5: "He hunts nonstop." Every so often he comes looking where Nell is.
        private float relentlessTimer = 12f;
        // Patrol: turning toward a single sound.
        private Vector3 listenPoint;
        private float listenTimer;
        // Investigate: sweeping the hook at the spot, and the newest groove he has followed so far.
        private bool sweeping;
        private float sweepTime;
        private float trackTime;
        // Search: the hide spot being checked and how long he has been feeling inside it.
        private HideSpot checking;
        private bool feeling;
        private float feelTime;
        private float lingerTime;
        // Toll and Recover timers.
        private float tollTime;
        private float bellTimer;
        private float recoverTime;

        public State CurrentState => state;
        public string StateName => state.ToString().ToUpperInvariant();
        public NavMeshAgent Agent => agent;
        // How many times he has started hunting Nell this night (for the end screen).
        public int Hunts { get; private set; }
        // Night 5 sets this.
        public bool Relentless { get; set; }
        // The spillway swept him away.
        public bool WashedAway { get; private set; }

        // Fired whenever he makes a sound Nell could hear: the HUD shows it as a caption.
        public event Action<SextonSound, Vector3> MadeSound;
        // Fired for every noise pulse he checks: whether he heard it, how many walls were in the way, and its reach after
        // difficulty and walls. Used by the debug view.
        public event Action<NoiseEvent, bool, int, float> Listened;

        // Called by the bootstrap: builds the body, adds the NavMeshAgent and starts listening.
        public void Setup(PlayerController target, Vector3[] points)
        {
            player = target;
            route.AddRange(points);
            body = SextonBody.Build(transform);
            agent = gameObject.AddComponent<NavMeshAgent>();
            agent.radius = 0.4f;
            agent.height = 2.4f;
            agent.angularSpeed = 240f;
            agent.acceleration = 12f;
            agent.stoppingDistance = 0.3f;
            voice = gameObject.AddComponent<AudioSource>();
            voice.spatialBlend = 1f;
            voice.maxDistance = 45f;
            voice.rolloffMode = AudioRolloffMode.Linear;
            NoisePulse.Emitted += HearNoise;
            ResumePatrol();
        }

        // Unsubscribe, or the static event would keep calling a destroyed Sexton.
        private void OnDestroy() => NoisePulse.Emitted -= HearNoise;

        // Runs the current state each frame, then makes his sounds and animates the body.
        private void Update()
        {
            if (player == null || agent == null || WashedAway) return;
            float dt = Time.deltaTime;
            swipeTimer -= dt;
            grabCooldown -= dt;

            if (!HandleBarredDoor()) RunState(dt);
            MakeSounds(dt);
            body.Animate(agent.velocity.magnitude, CurrentPose(), dt);
        }

        // One frame of whichever state he is in.
        private void RunState(float dt)
        {
            switch (state)
            {
                case State.Patrol: UpdatePatrol(dt); break;
                case State.Investigate: UpdateInvestigate(dt); break;
                case State.Hunt: UpdateHunt(); break;
                case State.Search: UpdateSearch(dt); break;
                case State.Toll: UpdateToll(dt); break;
                case State.Recover: UpdateRecover(dt); break;
                case State.Kill: Face(player.transform.position); break;
            }
        }

        // ---------- Hearing ----------

        // Runs for every noise in the world. Walls and difficulty change how far a sound carries to him.
        private void HearNoise(NoiseEvent noise)
        {
            if (state == State.Kill || state == State.Recover || WashedAway) return;
            // Night 5's chapel bell drowns every sound while it tolls.
            if (NoisePulse.Drowned)
            {
                Listened?.Invoke(noise, false, 0, 0f);
                return;
            }
            int walls = WallsBetween(noise.Position);
            float reach = noise.Radius * GameSettings.HearingMultiplier * Mathf.Pow(wallMuffle, walls);
            // During the toll his own bell drowns out everything.
            bool heard = state != State.Toll && FlatDistance(noise.Position) <= reach;
            Listened?.Invoke(noise, heard, walls, reach);
            if (!heard) return;
            lastHeardTime = Time.time;
            body.Heard(noise.Position);

            if (noise.IsLure)
            {
                recentSounds.Clear();
                StartInvestigate(noise.Position);
                return;
            }
            if (state == State.Hunt)
            {
                MoveTo(noise.Position);
                return;
            }

            // Doc rule: inside the radius he turns toward you. 2 sounds in 5 seconds -> investigate, 3 -> hunt.
            recentSounds.Add(Time.time);
            recentSounds.RemoveAll(t => Time.time - t > 5f);
            if (recentSounds.Count >= 3) StartHunt(noise.Position);
            else if (recentSounds.Count == 2) StartInvestigate(noise.Position);
            else if (state == State.Patrol) TurnToward(noise.Position);
        }

        // Counts the walls on a straight line between a sound and him, at chest height so crates and barrels don't count.
        private int WallsBetween(Vector3 source)
        {
            Vector3 from = new Vector3(source.x, 1.2f, source.z);
            Vector3 to = new Vector3(transform.position.x, 1.2f, transform.position.z);
            Vector3 direction = to - from;
            if (direction.sqrMagnitude < 0.01f) return 0;
            int walls = 0;
            foreach (RaycastHit hit in Physics.RaycastAll(from, direction.normalized, direction.magnitude, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
                if (hit.collider.GetComponent<Occluder>() != null) walls++;
            return walls;
        }

        // Breathing is only audible up close, and not while hidden or holding breath.
        private bool CanHearBreathing() =>
            !player.IsDead && !player.IsHiding && !player.HoldingBreath && PlayerDistance() < breathHearing;

        // ---------- States ----------

        // Walks the route. A single sound makes him stop and turn his head toward it for a moment.
        private void UpdatePatrol(float dt)
        {
            if (CanHearBreathing()) { StartHunt(player.transform.position); return; }
            if (listenTimer > 0f)
            {
                listenTimer -= dt;
                Face(listenPoint);
                if (listenTimer <= 0f) agent.isStopped = false;
                return;
            }
            if (Relentless)
            {
                relentlessTimer -= dt;
                if (relentlessTimer <= 0f)
                {
                    relentlessTimer = UnityEngine.Random.Range(10f, 16f);
                    Vector2 off = UnityEngine.Random.insideUnitCircle * 6f;
                    StartInvestigate(player.transform.position + new Vector3(off.x, 0f, off.y));
                    return;
                }
            }
            if (Arrived())
            {
                patrolIndex = (patrolIndex + 1) % route.Count;
                MoveTo(route[patrolIndex]);
            }
        }

        // Walks to the sound, then sweeps his hook through the mud. Grooves he touches lead him on along Nell's trail.
        private void UpdateInvestigate(float dt)
        {
            if (CanHearBreathing()) { StartHunt(player.transform.position); return; }
            if (!sweeping)
            {
                if (Arrived()) BeginSweep();
                return;
            }
            sweepTime -= dt;
            transform.Rotate(Vector3.up, 45f * dt);
            if (FollowTrack()) return;
            if (sweepTime <= 0f)
            {
                recentSounds.Clear();
                ResumePatrol();
            }
        }

        // Runs at the latest sound. When the trail goes cold he searches nearby, or tolls if Nell got far away.
        private void UpdateHunt()
        {
            // Up close he can hear Nell breathing and closes in on her directly. That counts as hearing her,
            // so the trail doesn't "go cold" while she is standing right next to him.
            if (CanHearBreathing())
            {
                lastHeardTime = Time.time;
                MoveTo(player.transform.position);
            }
            TryAttack();
            if (state != State.Hunt) return;
            if (Arrived() && Time.time - lastHeardTime > 1.5f)
            {
                if (PlayerDistance() > tollDistance) StartToll();
                else StartSearch();
            }
        }

        // Visits each hiding spot within 10 m (her favourite first) and feels inside.
        // A hidden player is only safe while holding her breath.
        private void UpdateSearch(float dt)
        {
            if (CanHearBreathing()) { StartHunt(player.transform.position); return; }
            if (checking != null)
            {
                if (!feeling)
                {
                    if (!Arrived()) return;
                    feeling = true;
                    feelTime = 2.5f;
                    agent.isStopped = true;
                    Raise(SextonSound.Feeling);
                    return;
                }
                Face(checking.transform.position);
                feelTime -= dt;
                if (player.CurrentHideSpot == checking && !player.HoldingBreath && grabCooldown <= 0f) { Grab(); return; }
                if (feelTime <= 0f) NextHideSpot();
                return;
            }

            // Nothing left to check: stand, listen, and drag the hook around his feet.
            transform.Rotate(Vector3.up, 60f * dt);
            if (FollowTrack()) return;
            lingerTime -= dt;
            if (lingerTime <= 0f) GiveUp();
        }

        // Stands ringing his bell. He is deaf to everything until it stops.
        private void UpdateToll(float dt)
        {
            tollTime -= dt;
            bellTimer -= dt;
            if (bellTimer <= 0f)
            {
                bellTimer = 2.5f;
                voice.PlayOneShot(ProceduralSounds.Bell, 1f);
                Raise(SextonSound.Bell);
            }
            if (tollTime <= 0f) GiveUp();
        }

        // Just knocked Nell down: drops her, stands a moment, then starts feeling around for her.
        private void UpdateRecover(float dt)
        {
            recoverTime -= dt;
            if (recoverTime < 1.6f) body.Release();
            if (recoverTime <= 0f) StartSearch();
        }

        // ---------- State changes ----------

        // Changes state and sets his speed; he stands still while tolling, recovering or killing.
        private void SetState(State next)
        {
            if (next == State.Hunt && state != State.Hunt) Hunts++;
            state = next;
            agent.isStopped = next == State.Toll || next == State.Kill || next == State.Recover;
            agent.speed = next == State.Hunt ? huntSpeed : next == State.Investigate ? investigateSpeed : walkSpeed;
            sweeping = false;
            feeling = false;
            listenTimer = 0f;
        }

        // Back to his route, at the nearest patrol point.
        private void ResumePatrol()
        {
            SetState(State.Patrol);
            trackTime = 0f;
            patrolIndex = NearestRoutePoint();
            MoveTo(route[patrolIndex]);
        }

        // Stops and turns toward a single sound without leaving his route.
        private void TurnToward(Vector3 point)
        {
            listenPoint = point;
            listenTimer = 1.5f;
            agent.isStopped = true;
        }

        // Walks to a sound to check it out.
        private void StartInvestigate(Vector3 point)
        {
            SetState(State.Investigate);
            MoveTo(point);
        }

        // Runs at a sound.
        private void StartHunt(Vector3 point)
        {
            SetState(State.Hunt);
            MoveTo(point);
        }

        // Stops and sweeps his hook through the mud for a few seconds.
        private void BeginSweep()
        {
            sweeping = true;
            sweepTime = 3.5f;
            agent.isStopped = true;
        }

        // Lists the hiding spots within 10 m and starts checking them one by one.
        private void StartSearch()
        {
            recentSounds.Clear();
            SetState(State.Search);
            searchQueue = HideSpot.Within(transform.position, searchRange);
            lingerTime = 4f;
            NextHideSpot();
        }

        // Moves on to the next hiding spot, or stands still if none are left.
        private void NextHideSpot()
        {
            feeling = false;
            if (searchQueue.Count == 0)
            {
                checking = null;
                agent.isStopped = true;
                return;
            }
            checking = searchQueue[0];
            searchQueue.RemoveAt(0);
            agent.isStopped = false;
            MoveTo(checking.ExitPoint.position);
        }

        // Rings his bell for 30 s; nothing reaches him until it stops.
        private void StartToll()
        {
            recentSounds.Clear();
            SetState(State.Toll);
            tollTime = tollSeconds;
            bellTimer = 0f;
        }

        // He has lost her: one bar of the funeral hymn, then back to patrol.
        private void GiveUp()
        {
            voice.PlayOneShot(ProceduralSounds.Hymn, 0.8f);
            Raise(SextonSound.Hymn);
            ResumePatrol();
        }

        // If his hook touches a groove newer than the last one he followed, he walks to it. Returns true if he found one.
        private bool FollowTrack()
        {
            if (!MudTracks.NewestNear(body.HookTip, 2.5f, trackTime, out MudTracks.Footprint print)) return false;
            trackTime = print.Time;
            SetState(State.Investigate);
            MoveTo(print.Position);
            return true;
        }

        // ---------- Attacks ----------

        // Grab at 1 m. At 2 m he swipes with the hook; a swipe that would finish Nell becomes a grab.
        private void TryAttack()
        {
            // Design doc: his weakness is ladders. On a roof she is out of reach.
            if (player.IsHiding || player.IsDead || player.IsKnockedDown || player.OnRoof) return;
            float distance = PlayerDistance();
            if (distance <= grabRange) { if (grabCooldown <= 0f) Grab(); return; }
            if (distance > swipeRange || swipeTimer > 0f) return;

            swipeTimer = swipeCooldown;
            float damage = GameSettings.SwipeDamage;
            if (player.Health <= damage && grabCooldown <= 0f) { Grab(); return; }
            body.Swipe();
            voice.PlayOneShot(ProceduralSounds.Knock, 1f);
            player.TakeDamage(Mathf.Min(damage, player.Health - 1f));
        }

        // A full grab. Design doc: knocked down for 60 / 80 Vitality on Still Water / Standard; on Undertow the night is lost.
        private void Grab()
        {
            agent.velocity = Vector3.zero;
            body.Grab();
            float damage = GameSettings.GrabDamage;
            if (damage >= player.Health)
            {
                SetState(State.Kill);
                player.Die(body);
                return;
            }
            SetState(State.Recover);
            recoverTime = 2.5f;
            grabCooldown = grabCooldownAfterKnockdown;
            player.KnockDown(damage, body);
        }

        // If a barred door stands between him and the sound he is chasing, he walks to its street side and smashes it.
        // Design doc: "He can be used... bait him into smashing doors Nell can't force herself."
        // Returns true while he is busy with a door, so the normal state logic waits.
        private bool HandleBarredDoor()
        {
            if (repathTimer > 0f)
            {
                // The NavMesh hole left by the door takes a moment to update, then he re-plans to his goal.
                repathTimer -= Time.deltaTime;
                if (repathTimer <= 0f) MoveTo(goal);
                return true;
            }
            bool chasing = state == State.Investigate || state == State.Hunt || state == State.Search;
            if (breaching != null)
            {
                if (breaching.Broken || !chasing) { breaching = null; return false; }
                // At the door, or as close as the NavMesh lets him get to it.
                bool atDoor = FlatDistance(breaching.Approach) < 1.3f || (Arrived() && FlatDistance(breaching.Position) < 2.5f);
                if (!atDoor) return true;
                agent.isStopped = true;
                Face(breaching.Position);
                // A nailed door goes in one blow; the radio room door takes several, and none while Nell braces it.
                blowTimer -= Time.deltaTime;
                if (blowTimer > 0f) return true;
                blowTimer = 1.2f;
                body.Smash();
                Raise(SextonSound.Smash);
                if (!breaching.Hit()) return true;
                breaching = null;
                repathTimer = 0.4f;
                return true;
            }
            if (!chasing || agent.pathPending || agent.pathStatus == NavMeshPathStatus.PathComplete) return false;
            // His path is cut short. If a door is near where he wants to go, that door is the reason.
            IBreachable door = Breachables.Near(goal);
            if (door == null) return false;
            breaching = door;
            blowTimer = 0.4f;
            agent.isStopped = false;
            agent.SetDestination(door.Approach);
            return true;
        }

        // ---------- Called by other systems ----------

        // Design doc: "His patrols link the places that mattered to him" and "His routes follow the evidence you took."
        // When Nell takes evidence, he adds that spot to his route.
        public void GuardSite(Vector3 site)
        {
            site.y = 0f;
            foreach (Vector3 point in route)
                if (Vector3.Distance(point, site) < 3f) return;
            route.Insert((patrolIndex + 1) % (route.Count + 1), site);
        }

        // The spillway: "open the spillway to wash him away". He is carried off down the channel, still ringing.
        public void WashAway()
        {
            if (WashedAway) return;
            WashedAway = true;
            body.Release();
            voice.PlayOneShot(ProceduralSounds.Bell, 1f);
            Raise(SextonSound.Bell);
            agent.enabled = false;
            gameObject.AddComponent<SweptAway>();
        }

        // Sends him back to patrol from a far point when Nell restarts at a lantern post.
        public void ResetTo(Vector3 farFrom)
        {
            body.Release();
            Vector3 farthest = route[0];
            foreach (Vector3 point in route)
                if (FlatDistance(point, farFrom) > FlatDistance(farthest, farFrom)) farthest = point;
            agent.Warp(farthest);
            recentSounds.Clear();
            grabCooldown = 0f;
            ResumePatrol();
        }

        // ---------- Sounds and helpers ----------

        // Knocking (slow on patrol, fast on the hunt) and the occasional scrape of his hook on stone.
        private void MakeSounds(float dt)
        {
            if (state == State.Toll || state == State.Kill) return;
            knockTimer -= dt;
            if (knockTimer <= 0f)
            {
                bool fast = state == State.Hunt;
                knockTimer = fast ? 0.35f : state == State.Investigate ? 0.8f : 1.3f;
                voice.PlayOneShot(ProceduralSounds.Knock, fast ? 1f : 0.6f);
                Raise(fast ? SextonSound.FastKnock : SextonSound.SlowKnock);
            }
            if (agent.velocity.sqrMagnitude < 0.5f) return;
            scrapeTimer -= dt;
            if (scrapeTimer > 0f) return;
            scrapeTimer = UnityEngine.Random.Range(5f, 9f);
            voice.PlayOneShot(ProceduralSounds.Scrape, 0.7f);
            Raise(SextonSound.Scrape);
        }

        // Tells the HUD about a sound he made.
        private void Raise(SextonSound sound) => MadeSound?.Invoke(sound, transform.position);

        // Which pose the body should hold for the current state.
        private SextonBody.Pose CurrentPose()
        {
            switch (state)
            {
                case State.Hunt: return SextonBody.Pose.Hunt;
                case State.Toll: return SextonBody.Pose.Ring;
                case State.Investigate: return sweeping ? SextonBody.Pose.Sweep : SextonBody.Pose.Walk;
                case State.Search: return feeling ? SextonBody.Pose.Feel : checking == null ? SextonBody.Pose.Sweep : SextonBody.Pose.Walk;
                default: return SextonBody.Pose.Walk;
            }
        }

        // Remembers where he is going (so he can re-plan after smashing a door) and sets the agent's destination.
        private void MoveTo(Vector3 point)
        {
            goal = point;
            breaching = null;
            agent.isStopped = false;
            agent.SetDestination(point);
        }

        // Turns in place to face a point.
        private void Face(Vector3 point)
        {
            Vector3 d = point - transform.position;
            d.y = 0f;
            if (d.sqrMagnitude > 0.01f)
                transform.rotation = Quaternion.RotateTowards(transform.rotation, Quaternion.LookRotation(d), 360f * Time.deltaTime);
        }

        // Index of the route point closest to him.
        private int NearestRoutePoint()
        {
            int best = 0;
            for (int i = 1; i < route.Count; i++)
                if (FlatDistance(route[i]) < FlatDistance(route[best])) best = i;
            return best;
        }

        // Flat distance to the player, ignoring height.
        private float PlayerDistance() => FlatDistance(player.transform.position);

        // Flat distance from him to a point.
        private float FlatDistance(Vector3 point) => FlatDistance(transform.position, point);

        // Flat distance between two points, ignoring height.
        private static float FlatDistance(Vector3 a, Vector3 b)
        {
            a.y = b.y = 0f;
            return Vector3.Distance(a, b);
        }

        // True once the agent has reached its current destination.
        private bool Arrived() => !agent.pathPending && agent.remainingDistance < 0.6f;
    }

    // Carries the Sexton's body off along the spillway channel and out of town.
    public sealed class SweptAway : MonoBehaviour
    {
        private float age;

        private void Update()
        {
            age += Time.deltaTime;
            transform.position += new Vector3(9f, -0.4f, 0f) * Time.deltaTime;
            transform.Rotate(0f, 0f, 40f * Time.deltaTime);
            if (age > 4f) gameObject.SetActive(false);
        }
    }

    // Marks a collider as a wall that muffles sound for the Sexton's hearing.
    public sealed class Occluder : MonoBehaviour { }
}
