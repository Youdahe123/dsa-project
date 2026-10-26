using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

namespace Hushwater
{
    // The blind Sexton: reacts only to noise pulses that reach him.
    public sealed class SextonController : MonoBehaviour
    {
        private enum State { Patrol, Investigate, Hunt, Search }

        [SerializeField] private float walkSpeed = 1.6f;
        [SerializeField] private float investigateSpeed = 2.4f;
        [SerializeField] private float huntSpeed = 4.6f;
        [SerializeField] private float swipeRange = 2f;
        [SerializeField] private float swipeDamage = 35f;
        [SerializeField] private float swipeCooldown = 1.5f;

        private Vector3[] patrolPoints;
        private readonly List<float> recentSounds = new List<float>();
        private NavMeshAgent agent;
        private PlayerController player;
        private Renderer bodyRenderer;
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
            bodyRenderer = GetComponentInChildren<Renderer>();
            agent = gameObject.AddComponent<NavMeshAgent>();
            agent.radius = 0.4f;
            agent.height = 2.4f;
            agent.angularSpeed = 240f;
            agent.acceleration = 12f;
            knockSource = gameObject.AddComponent<AudioSource>();
            knockSource.clip = MakeKnock();
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
                    TrySwipe();
                    if (Arrived() && Time.time - lastHeardTime > 1.5f) StartSearch(6f);
                    break;
                case State.Search:
                    searchTime -= Time.deltaTime;
                    transform.Rotate(Vector3.up, 60f * Time.deltaTime);
                    // Feels around nearby: a hidden player is only safe while holding their breath.
                    if (player.IsHiding && !player.HoldingBreath && PlayerDistance() < 2.5f) Catch();
                    if (searchTime <= 0f)
                    {
                        SetState(State.Patrol);
                        agent.SetDestination(patrolPoints[patrolIndex % patrolPoints.Length]);
                    }
                    break;
            }

            Knock();
            if (bodyRenderer != null)
                bodyRenderer.material.color = state == State.Hunt ? new Color(0.35f, 0.04f, 0.04f) : new Color(0.08f, 0.06f, 0.06f);
        }

        private void HearNoise(NoiseEvent noise)
        {
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
            agent.isStopped = next == State.Search;
            agent.speed = next == State.Hunt ? huntSpeed : next == State.Investigate ? investigateSpeed : walkSpeed;
        }

        private void StartSearch(float seconds)
        {
            recentSounds.Clear();
            searchTime = seconds;
            SetState(State.Search);
        }

        private void TrySwipe()
        {
            if (player.IsHiding || swipeTimer > 0f || PlayerDistance() > swipeRange) return;
            swipeTimer = swipeCooldown;
            player.TakeDamage(swipeDamage);
        }

        private void Catch()
        {
            player.TakeDamage(player.Health);
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
            knockSource.PlayOneShot(knockSource.clip, state == State.Hunt ? 1f : 0.6f);
        }

        private static AudioClip MakeKnock()
        {
            const int rate = 44100;
            float[] data = new float[rate / 2];
            for (int i = 0; i < data.Length; i++)
            {
                float t = i / (float)rate;
                data[i] = (Mathf.Sin(2f * Mathf.PI * 180f * t) + 0.5f * Mathf.Sin(2f * Mathf.PI * 410f * t)) * Mathf.Exp(-t * 12f) * 0.6f;
            }
            AudioClip clip = AudioClip.Create("Knock", data.Length, 1, rate, false);
            clip.SetData(data, 0);
            return clip;
        }
    }
}
