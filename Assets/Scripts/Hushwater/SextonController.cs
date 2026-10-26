using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

namespace Hushwater
{
    // Abel Crane. Blind, cannot be killed, hears everything. Behaviour follows the design doc's state chart.
    public sealed class SextonController : MonoBehaviour
    {
        public enum State { Patrol, Listen, Investigate, Hunt, Search, Toll, Grabbing }

        private const float PatrolSpeed = 1.35f;
        private const float InvestigateSpeed = 2.1f;
        private const float HuntSpeed = 4.9f;
        private const float SwipeReach = 2f;
        private const float GrabReach = 1.05f;
        private const float SearchRadius = 10f;
        private const float DeafSeconds = 30f;

        private NavMeshAgent agent;
        private PlayerController player;
        private SextonBody body;
        private Vector3[] patrol;
        private int patrolIndex;
        private readonly List<float> heard = new List<float>();
        private readonly Queue<HideSpot> searchQueue = new Queue<HideSpot>();
        private Vector3 lastHeard;
        private float lastHeardTime;
        private float stateTimer;
        private float knockTimer;
        private float deafTimer;
        private float attackCooldown;
        private float trackAfter;
        private float trackTimer;
        private float trackCaptionCooldown;
        private float knockCaptionTimer;
        private float heartTimer;
        private float stepDistance;
        private bool huntedThisCycle;
        private bool feeling;
        private int tollsLeft;
        private AudioSource scrape;
        private AudioSource organ;

        public State Current { get; private set; }
        public bool Deaf => deafTimer > 0f;
        public string StateName => Current.ToString().ToUpperInvariant();

        public void Setup(PlayerController target, Vector3 spawn, Vector3[] patrolPoints)
        {
            player = target;
            patrol = patrolPoints;
            for (int i = 0; i < patrol.Length; i++) patrol[i] = Snap(patrol[i]);
            transform.position = Snap(spawn);
            body = SextonBody.Build(transform);

            CapsuleCollider capsule = gameObject.AddComponent<CapsuleCollider>();
            capsule.height = 2.4f;
            capsule.radius = 0.38f;
            capsule.center = Vector3.up * 1.2f;

            agent = gameObject.AddComponent<NavMeshAgent>();
            agent.radius = 0.4f;
            agent.height = 2.4f;
            agent.speed = PatrolSpeed;
            agent.acceleration = 14f;
            agent.angularSpeed = 260f;
            agent.stoppingDistance = 0.3f;
            agent.autoBraking = true;
            agent.Warp(transform.position);

            scrape = SoundFx.Loop(gameObject, ProceduralAudio.Scrape, 0f, 22f);
            organ = SoundFx.Loop(gameObject, ProceduralAudio.Organ, 0f, 10f, false);
            NoisePulse.Emitted += Hear;
            Enter(State.Patrol);
            agent.SetDestination(patrol[0]);
        }

        private void OnDestroy() => NoisePulse.Emitted -= Hear;

        private static Vector3 Snap(Vector3 p) => NavMesh.SamplePosition(p, out NavMeshHit hit, 6f, NavMesh.AllAreas) ? hit.position : p;

        private void Update()
        {
            if (player == null || agent == null || !agent.isOnNavMesh) return;
            float dt = Time.deltaTime;
            deafTimer -= dt;
            attackCooldown -= dt;
            stateTimer -= dt;
            trackCaptionCooldown -= dt;

            switch (Current)
            {
                case State.Patrol: UpdatePatrol(dt); break;
                case State.Listen: UpdateListen(); break;
                case State.Investigate: UpdateInvestigate(); break;
                case State.Hunt: UpdateHunt(); break;
                case State.Search: UpdateSearch(); break;
                case State.Toll: UpdateToll(); break;
                case State.Grabbing:
                    if (stateTimer <= 0f) BeginToll();
                    break;
            }

            if (Current != State.Hunt && Current != State.Grabbing && Current != State.Toll) CheckBump();
            UpdateSound(dt);
            body.Animate(agent.velocity.magnitude, Current, feeling, dt);
        }

        // ---------- states ----------

        private void Enter(State next)
        {
            State previous = Current;
            Current = next;
            feeling = false;
            agent.isStopped = next == State.Listen || next == State.Toll || next == State.Grabbing;
            agent.speed = next == State.Hunt ? HuntSpeed : next == State.Investigate || next == State.Search ? InvestigateSpeed : PatrolSpeed;
            if (previous == next) return;
            NightDirector d = NightDirector.Instance;
            switch (next)
            {
                case State.Listen: d.Caption("[The knocking stops. He's listening]", 2.5f); break;
                case State.Investigate: d.Caption($"[Slow knocks turn toward the sound · {Direction()}]", 3f); break;
                case State.Hunt:
                    huntedThisCycle = true;
                    d.Caption($"[Fast knocks — he's hunting you · {Direction()}]", 3f);
                    d.OnSpotted();
                    SoundFx.Play2D(ProceduralAudio.Sting, 0.55f);
                    break;
                case State.Search: d.Caption("[A hook scrapes stone. He's searching]", 3f); break;
            }
        }

        private void UpdatePatrol(float dt)
        {
            if (!agent.pathPending && agent.remainingDistance < 0.7f)
            {
                patrolIndex = (patrolIndex + 1) % patrol.Length;
                agent.SetDestination(patrol[patrolIndex]);
            }
            // Mud remembers: he drags his hook through the silt and follows Nell's prints.
            trackTimer -= dt;
            if (trackTimer > 0f) return;
            trackTimer = 0.6f;
            if (MudTrail.Follow(transform.position, 3.2f, 45f, ref trackAfter, out Vector3 next))
            {
                agent.SetDestination(Snap(next));
                if (trackCaptionCooldown <= 0f)
                {
                    trackCaptionCooldown = 25f;
                    NightDirector.Instance.Caption("[A hook drags through the silt — he's found your tracks]", 3.5f);
                }
            }
        }

        private void UpdateListen()
        {
            TurnToward(lastHeard);
            if (stateTimer <= 0f) ResumePatrol();
        }

        private void UpdateInvestigate()
        {
            if (stateTimer > 0f) { TurnToward(lastHeard); return; }
            if (feeling)
            {
                ResumePatrol();
                return;
            }
            TryAttack();
            if (!agent.pathPending && agent.remainingDistance < 0.8f)
            {
                // Sweeps his hook, listens.
                feeling = true;
                agent.isStopped = true;
                stateTimer = 3f;
            }
        }

        private void UpdateHunt()
        {
            TryAttack();
            if (Current != State.Hunt) return;
            if (!agent.pathPending && agent.remainingDistance < 0.9f && Time.time - lastHeardTime > 1.5f) BeginSearch();
        }

        private void BeginSearch()
        {
            searchQueue.Clear();
            var spots = new List<HideSpot>(HideSpot.All);
            spots.RemoveAll(s => Vector3.Distance(s.transform.position, lastHeard) > SearchRadius);
            spots.Sort((a, b) => Vector3.Distance(a.transform.position, lastHeard).CompareTo(Vector3.Distance(b.transform.position, lastHeard)));
            foreach (HideSpot s in spots) searchQueue.Enqueue(s);
            Enter(State.Search);
            stateTimer = 6f;
            NextSearchTarget();
        }

        private HideSpot searching;

        private void NextSearchTarget()
        {
            feeling = false;
            agent.isStopped = false;
            if (searchQueue.Count > 0)
            {
                searching = searchQueue.Dequeue();
                agent.SetDestination(Snap(searching.ExitPoint.position));
            }
            else
            {
                searching = null;
                Vector2 r = Random.insideUnitCircle * 4f;
                agent.SetDestination(Snap(lastHeard + new Vector3(r.x, 0f, r.y)));
            }
        }

        private void UpdateSearch()
        {
            if (searching != null)
            {
                if (!feeling && !agent.pathPending && agent.remainingDistance < 0.9f)
                {
                    feeling = true;
                    agent.isStopped = true;
                    stateTimer = 2.4f;
                    TurnToward(searching.transform.position);
                }
                if (feeling)
                {
                    TurnToward(searching.transform.position);
                    if (stateTimer <= 0f)
                    {
                        // Feels inside the hiding spot. Holding your breath is the only defence.
                        if (player.CurrentHideSpot == searching)
                        {
                            if (player.HoldingBreath) NightDirector.Instance.Caption("[The hook slides past your face... and away]", 3f);
                            else { Grab(); return; }
                        }
                        stateTimer = 6f;
                        NextSearchTarget();
                    }
                }
                return;
            }
            TryAttack();
            if (stateTimer <= 0f)
            {
                if (huntedThisCycle) BeginToll();
                else ResumePatrol();
            }
            else if (!agent.pathPending && agent.remainingDistance < 0.8f) NextSearchTarget();
        }

        // "You got out of reach. His bell drowns all sound for 30 s."
        private void BeginToll()
        {
            Enter(State.Toll);
            huntedThisCycle = false;
            heard.Clear();
            tollsLeft = 3;
            stateTimer = 1.8f;
            SoundFx.PlayAt(ProceduralAudio.Hum, transform.position + Vector3.up * 2f, 0.7f, 18f);
            NightDirector.Instance.Caption("[He hums one bar of a funeral hymn]", 2.5f);
        }

        private void UpdateToll()
        {
            if (stateTimer > 0f) return;
            if (tollsLeft > 0)
            {
                tollsLeft--;
                stateTimer = 2.4f;
                SoundFx.Play2D(ProceduralAudio.Toll, 0.55f);
                SoundFx.PlayAt(ProceduralAudio.Toll, transform.position + Vector3.up * 2f, 1f, 80f);
                body.Strike();
                if (tollsLeft == 2)
                {
                    deafTimer = DeafSeconds;
                    NightDirector.Instance.Caption("[The bell tolls. All sound is drowned for 30 s — move now]", 5f);
                    NightDirector.Instance.OnToll(DeafSeconds);
                }
                return;
            }
            ResumePatrol();
        }

        private void ResumePatrol()
        {
            heard.Clear();
            Enter(State.Patrol);
            agent.isStopped = false;
            agent.SetDestination(patrol[patrolIndex]);
        }

        // ---------- hearing ----------

        private void Hear(NoiseEvent noise)
        {
            if (Deaf || Current == State.Toll || Current == State.Grabbing) return;
            float distance = Vector3.Distance(transform.position + Vector3.up, noise.Position);
            float reach = noise.Radius * GameSettings.HearingMultiplier * Occlusion(noise.Position);
            if (distance > reach) return;

            lastHeard = Snap(noise.Position);
            lastHeardTime = Time.time;

            if (noise.Kind == NoiseKind.Lure)
            {
                // He walks to where it lands.
                heard.Clear();
                Enter(State.Investigate);
                agent.isStopped = false;
                agent.SetDestination(lastHeard);
                return;
            }

            if (Current == State.Hunt)
            {
                agent.SetDestination(lastHeard);
                return;
            }

            heard.Add(Time.time);
            heard.RemoveAll(t => Time.time - t > 5f);
            if (heard.Count >= 3)
            {
                Enter(State.Hunt);
                agent.isStopped = false;
                agent.SetDestination(lastHeard);
            }
            else if (heard.Count == 2)
            {
                Enter(State.Investigate);
                agent.isStopped = false;
                agent.SetDestination(lastHeard);
                stateTimer = 0.4f;
            }
            else if (Current == State.Patrol)
            {
                // Inside the radius he turns toward you.
                Enter(State.Listen);
                stateTimer = 1.6f;
            }
        }

        // The doc's Hearing checks walls: every surface between him and the sound muffles it.
        private float Occlusion(Vector3 source)
        {
            Vector3 ear = transform.position + Vector3.up * 2f;
            Vector3 from = source + Vector3.up * 0.4f;
            Vector3 dir = ear - from;
            RaycastHit[] hits = Physics.RaycastAll(from, dir.normalized, dir.magnitude, ~(1 << 2), QueryTriggerInteraction.Ignore);
            int walls = 0;
            foreach (RaycastHit hit in hits)
            {
                if (hit.collider.transform.IsChildOf(transform) || hit.collider.transform.IsChildOf(player.transform)) continue;
                if (hit.collider.GetComponent<MudSurface>() != null || hit.collider.GetComponent<InvisibleWall>() != null) continue;
                walls++;
            }
            return Mathf.Pow(0.72f, Mathf.Min(walls, 4));
        }

        // ---------- attacks ----------

        private float PlayerDistance()
        {
            Vector3 d = player.transform.position - transform.position;
            d.y = 0f;
            return d.magnitude;
        }

        private void CheckBump()
        {
            if (player.IsHiding || Current == State.Search && feeling) return;
            if (PlayerDistance() < 1.3f)
            {
                lastHeard = player.transform.position;
                lastHeardTime = Time.time;
                Enter(State.Hunt);
                agent.isStopped = false;
            }
        }

        private void TryAttack()
        {
            if (player.IsHiding || player.KnockedDown) return;
            float d = PlayerDistance();
            if (d <= GrabReach) { Grab(); return; }
            if (d <= SwipeReach && attackCooldown <= 0f)
            {
                attackCooldown = 1.5f;
                body.Swipe();
                SoundFx.PlayAt(ProceduralAudio.Scrape, transform.position + Vector3.up * 1.2f, 0.9f, 12f, 1.8f);
                player.TakeDamage(GameSettings.SwipeDamage, false);
                NightDirector.Instance.Caption("[The hook catches you]", 2f);
            }
        }

        private void Grab()
        {
            Enter(State.Grabbing);
            stateTimer = 2.6f;
            body.Swipe();
            SoundFx.Play2D(ProceduralAudio.Sting, 0.8f, 0.7f);
            NightDirector.Instance.Caption("[He has you]", 2.5f);
            player.TakeDamage(GameSettings.GrabDamage, true);
        }

        private void TurnToward(Vector3 point)
        {
            Vector3 d = point - transform.position;
            d.y = 0f;
            if (d.sqrMagnitude < 0.01f) return;
            transform.rotation = Quaternion.RotateTowards(transform.rotation, Quaternion.LookRotation(d), 180f * Time.deltaTime);
        }

        // ---------- sound ----------

        private void UpdateSound(float dt)
        {
            float speed = agent.velocity.magnitude;
            scrape.volume = Mathf.Lerp(scrape.volume, Mathf.Clamp01(speed / 3f) * 0.55f + (feeling ? 0.45f : 0f), dt * 4f);
            organ.volume = Mathf.MoveTowards(organ.volume, Current == State.Hunt ? 0.28f : 0f, dt * (Current == State.Hunt ? 0.5f : 0.12f));

            stepDistance += speed * dt;
            if (stepDistance > 1.3f)
            {
                stepDistance = 0f;
                SoundFx.PlayAt(ProceduralAudio.HeavyStep(Random.Range(0, 3)), transform.position, 0.55f, 22f, Random.Range(0.85f, 1f));
            }

            // The clapper is the heartbeat of the game: its speed tells you his mood.
            float interval = Current switch
            {
                State.Hunt => 0.34f,
                State.Investigate => 0.8f,
                State.Search => 1.0f,
                State.Listen => 99f,
                State.Toll => 99f,
                State.Grabbing => 99f,
                _ => 1.35f
            };
            knockTimer -= dt;
            if (knockTimer <= 0f)
            {
                knockTimer = interval;
                SoundFx.PlayAt(ProceduralAudio.Knock, body.ClapperPosition, Current == State.Hunt ? 1f : 0.65f, 48f, Random.Range(0.96f, 1.04f));
                body.Strike();
                knockCaptionTimer -= interval;
                float distance = PlayerDistance();
                if (knockCaptionTimer <= 0f && distance < 40f && (Current == State.Patrol || Current == State.Search))
                {
                    knockCaptionTimer = 7f;
                    NightDirector.Instance.Caption($"[Slow iron knocks · {Direction()}]", 2.5f, true);
                }
            }

            // Your own heartbeat when he is near.
            heartTimer -= dt;
            float near = PlayerDistance();
            if (near < 11f && heartTimer <= 0f)
            {
                heartTimer = Mathf.Lerp(0.42f, 1f, near / 11f);
                SoundFx.Play2D(ProceduralAudio.Heartbeat, Mathf.Lerp(0.6f, 0.15f, near / 11f));
            }
        }

        public string Direction()
        {
            Vector3 to = transform.position - player.transform.position;
            float distance = to.magnitude;
            to.y = 0f;
            float angle = Vector3.SignedAngle(player.transform.forward, to, Vector3.up);
            string side = Mathf.Abs(angle) < 30f ? "ahead" : Mathf.Abs(angle) > 150f ? "behind you" : angle < 0f ? (Mathf.Abs(angle) < 75f ? "ahead-left" : "to your left") : (Mathf.Abs(angle) < 75f ? "ahead-right" : "to your right");
            string range = distance < 8f ? "close" : distance < 20f ? "near" : "far";
            return $"{side}, {range}";
        }
    }
}
