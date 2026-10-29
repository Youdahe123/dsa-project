using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

namespace Hushwater
{
    // The blind Sexton: reacts only to noise pulses that reach him.
    public sealed class SextonController : MonoBehaviour
    {
        private enum State { Patrol, Investigate, Hunt, Search, Kill }

        [SerializeField] private float walkSpeed = 1.6f;
        [SerializeField] private float investigateSpeed = 2.4f;
        [SerializeField] private float huntSpeed = 4.6f;
        [Header("Attacks (design doc: swipe at 2 m, grab at 1 m)")]
        [SerializeField] private float swipeRange = 2f;
        [SerializeField] private float grabRange = 1f;
        [SerializeField] private float swipeDamage = 35f;
        [SerializeField] private float swipeCooldown = 1.5f;
        // How close he has to be to hear Nell breathing. Holding breath hides it.
        [SerializeField] private float breathHearing = 3f;

        private Vector3[] patrolPoints;
        private readonly List<float> recentSounds = new List<float>();
        private NavMeshAgent agent;
        private PlayerController player;
        private SextonBody body;
        private AudioSource knockSource;
        private State state;
        private int patrolIndex;
        private float searchTime;
        private float swipeTimer;
        private float knockTimer;
        private float lastHeardTime;

        public string StateName => state.ToString().ToUpperInvariant();

        public void Setup(PlayerController target, Vector3[] points)
        {
            player = target;
            patrolPoints = points;
            body = SextonBody.Build(transform);
            agent = gameObject.AddComponent<NavMeshAgent>();
            agent.radius = 0.4f;
            agent.height = 2.4f;
            agent.angularSpeed = 240f;
            agent.acceleration = 12f;
            agent.stoppingDistance = 0.3f;
            knockSource = gameObject.AddComponent<AudioSource>();
            knockSource.spatialBlend = 1f;
            knockSource.maxDistance = 40f;
            knockSource.rolloffMode = AudioRolloffMode.Linear;
            NoisePulse.Emitted += HearNoise;
            SetState(State.Patrol);
            agent.SetDestination(patrolPoints[0]);
        }

        private void OnDestroy() => NoisePulse.Emitted -= HearNoise;

        private void Update()
        {
            if (player == null || agent == null) return;
            swipeTimer -= Time.deltaTime;

            switch (state)
            {
                case State.Patrol:
                    if (Arrived()) agent.SetDestination(patrolPoints[++patrolIndex % patrolPoints.Length]);
                    break;
                case State.Investigate:
                    if (Arrived()) StartSearch(4f);
                    break;
                case State.Hunt:
                    // Up close he can hear Nell breathing and closes in on her directly.
                    if (CanHearBreathing()) agent.SetDestination(player.transform.position);
                    TryAttack();
                    if (state == State.Hunt && Arrived() && Time.time - lastHeardTime > 1.5f) StartSearch(6f);
                    break;
                case State.Search:
                    searchTime -= Time.deltaTime;
                    transform.Rotate(Vector3.up, 60f * Time.deltaTime);
                    // Feels around nearby: a hidden player is only safe while holding their breath.
                    if (player.IsHiding && !player.HoldingBreath && PlayerDistance() < 2.5f) Grab();
                    else if (CanHearBreathing()) GoTo(State.Hunt, player.transform.position);
                    else if (searchTime <= 0f)
                    {
                        SetState(State.Patrol);
                        agent.SetDestination(patrolPoints[patrolIndex % patrolPoints.Length]);
                    }
                    break;
                case State.Kill:
                    FacePlayer();
                    break;
            }

            Knock();
            body.Animate(agent.velocity.magnitude, state == State.Hunt, Time.deltaTime);
        }

        private void HearNoise(NoiseEvent noise)
        {
            if (state == State.Kill) return;
            if (Vector3.Distance(transform.position, noise.Position) > noise.Radius) return;
            lastHeardTime = Time.time;

            if (noise.IsLure)
            {
                recentSounds.Clear();
                GoTo(State.Investigate, noise.Position);
                return;
            }
            if (state == State.Hunt)
            {
                agent.SetDestination(noise.Position);
                return;
            }

            // Doc rule: 2 sounds in 5 seconds -> investigate, 3 -> hunt.
            recentSounds.Add(Time.time);
            recentSounds.RemoveAll(t => Time.time - t > 5f);
            if (recentSounds.Count >= 3) GoTo(State.Hunt, noise.Position);
            else if (recentSounds.Count == 2) GoTo(State.Investigate, noise.Position);
        }

        private void GoTo(State next, Vector3 point)
        {
            SetState(next);
            agent.SetDestination(point);
        }

        private void SetState(State next)
        {
            state = next;
            agent.isStopped = next == State.Search || next == State.Kill;
            agent.speed = next == State.Hunt ? huntSpeed : next == State.Investigate ? investigateSpeed : walkSpeed;
        }

        private void StartSearch(float seconds)
        {
            recentSounds.Clear();
            searchTime = seconds;
            SetState(State.Search);
        }

        private bool CanHearBreathing() =>
            !player.IsDead && !player.IsHiding && !player.HoldingBreath && PlayerDistance() < breathHearing;

        // Grab at 1 m kills outright. At 2 m he swipes with the hook; a swipe that would finish Nell becomes a grab.
        private void TryAttack()
        {
            if (player.IsHiding || player.IsDead) return;
            float distance = PlayerDistance();
            if (distance <= grabRange) { Grab(); return; }
            if (distance > swipeRange || swipeTimer > 0f) return;

            swipeTimer = swipeCooldown;
            if (player.Health <= swipeDamage) { Grab(); return; }
            body.Swipe();
            knockSource.PlayOneShot(ProceduralSounds.Knock, 1f);
            player.TakeDamage(swipeDamage);
        }

        // Starts the kill: he stops, raises both hands and the player plays their death animation.
        private void Grab()
        {
            SetState(State.Kill);
            agent.velocity = Vector3.zero;
            body.Grab();
            player.Die(body);
        }

        private void FacePlayer()
        {
            Vector3 d = player.transform.position - transform.position;
            d.y = 0f;
            if (d.sqrMagnitude > 0.01f)
                transform.rotation = Quaternion.RotateTowards(transform.rotation, Quaternion.LookRotation(d), 360f * Time.deltaTime);
        }

        private float PlayerDistance()
        {
            Vector3 d = player.transform.position - transform.position;
            d.y = 0f;
            return d.magnitude;
        }

        private bool Arrived() => !agent.pathPending && agent.remainingDistance < 0.6f;

        // Placeholder knocking: slow while patrolling, fast while hunting.
        private void Knock()
        {
            knockTimer -= Time.deltaTime;
            if (knockTimer > 0f) return;
            knockTimer = state == State.Hunt ? 0.35f : state == State.Investigate ? 0.8f : 1.3f;
            knockSource.PlayOneShot(ProceduralSounds.Knock, state == State.Hunt ? 1f : 0.6f);
        }
    }
}
