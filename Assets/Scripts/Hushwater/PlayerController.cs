using System.Collections;
using UnityEngine;

namespace Hushwater
{
    [RequireComponent(typeof(CharacterController))]
    // Nell: first-person movement, breath, hiding, pickups, bottle throwing, and her death animation.
    public sealed class PlayerController : MonoBehaviour
    {
        public const int EvidenceNeeded = 3;
        private const int MaxBottles = 5;
        private const float PickupReach = 2f;

        [Header("Movement")]
        [SerializeField] private float walkSpeed = 3.2f;
        [SerializeField] private float sprintSpeed = 6f;
        [SerializeField] private float crouchSpeed = 1.5f;
        [SerializeField] private float gravity = -20f;
        [SerializeField] private float mouseSensitivity = 2.2f;
        [Header("Breath")]
        [SerializeField] private float maxBreath = 30f;
        [SerializeField] private float breathRecovery = 5f;

        private CharacterController controller;
        private Camera playerCamera;
        private AudioSource stepSource;
        private Vector3 exitPoint;
        private float verticalVelocity;
        private float pitch;
        private float breath;
        private float stepTimer;
        private float gaspCooldown;
        private bool crouching;
        private bool hiding;
        private HideSpot nearbyHideSpot;
        private HideSpot currentHideSpot;

        public float Health { get; private set; } = 100f;
        public float BreathNormalized => breath / maxBreath;
        public bool IsHiding => hiding;
        public bool IsCrouching => crouching;
        public bool HoldingBreath { get; private set; }
        public bool IsDead => Health <= 0f;
        public int Bottles { get; private set; } = 3;
        public int Evidence { get; private set; }
        public bool Escaped { get; private set; }
        public bool NearHideSpot => nearbyHideSpot != null;
        public Pickup NearbyPickup { get; private set; }
        // 1 right after a hit, fading to 0; the HUD tints the screen red with it.
        public float HurtFlash { get; private set; }
        // 0 to 1 while the death animation fades the screen out.
        public float DeathFade { get; private set; }

        // Caches components, fills the breath bar and locks the mouse for first-person look.
        private void Awake()
        {
            controller = GetComponent<CharacterController>();
            controller.stepOffset = 0.4f;
            playerCamera = GetComponentInChildren<Camera>();
            stepSource = gameObject.AddComponent<AudioSource>();
            breath = maxBreath;
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }

        // Where Nell has to bring the evidence to finish the night.
        public void SetExit(Vector3 point) => exitPoint = point;

        // Main input loop. Death and escape stop all input; the kill animation runs on its own.
        private void Update()
        {
            HurtFlash = Mathf.MoveTowards(HurtFlash, 0f, Time.deltaTime * 1.5f);
            if (IsDead || Escaped) return;

            if (Input.GetKeyDown(KeyCode.Escape))
            {
                Cursor.lockState = CursorLockMode.None;
                Cursor.visible = true;
            }
            if (Input.GetMouseButtonDown(0) && Cursor.lockState != CursorLockMode.Locked)
            {
                Cursor.lockState = CursorLockMode.Locked;
                Cursor.visible = false;
            }

            UpdateBreath();

            if (hiding)
            {
                if (Input.GetKeyDown(KeyCode.E)) LeaveWardrobe();
                return;
            }

            Look();
            Move();

            // E picks up whatever is closest first, otherwise hides.
            NearbyPickup = Pickup.Nearest(transform.position, PickupReach);
            if (Input.GetKeyDown(KeyCode.E))
            {
                if (NearbyPickup != null) Collect(NearbyPickup);
                else if (nearbyHideSpot != null) EnterWardrobe();
            }
            if (Input.GetKeyDown(KeyCode.Q) && Bottles > 0) ThrowBottle();

            if (Evidence >= EvidenceNeeded && Vector3.Distance(transform.position, exitPoint) < 2.5f) Escaped = true;
        }

        // Mouse look: yaw turns the body, pitch tilts only the camera.
        private void Look()
        {
            if (Cursor.lockState != CursorLockMode.Locked) return;
            transform.Rotate(Vector3.up * Input.GetAxis("Mouse X") * mouseSensitivity);
            pitch = Mathf.Clamp(pitch - Input.GetAxis("Mouse Y") * mouseSensitivity, -85f, 85f);
            playerCamera.transform.localRotation = Quaternion.Euler(pitch, 0f, 0f);
        }

        // Walking, crouching and sprinting, plus the footstep noise each one makes.
        private void Move()
        {
            // Crouch is a toggle, as in the design doc's controls table.
            if (Input.GetKeyDown(KeyCode.LeftControl) || Input.GetKeyDown(KeyCode.C)) crouching = !crouching;
            bool sprinting = Input.GetKey(KeyCode.LeftShift) && Input.GetAxisRaw("Vertical") > 0.1f;
            if (sprinting) crouching = false;

            controller.height = crouching ? 1.1f : 1.8f;
            controller.center = new Vector3(0f, crouching ? -0.35f : 0f, 0f);
            Vector3 eye = new Vector3(0f, crouching ? 0.1f : 0.75f, 0f);
            playerCamera.transform.localPosition = Vector3.Lerp(playerCamera.transform.localPosition, eye, Time.deltaTime * 10f);

            Vector2 input = Vector2.ClampMagnitude(new Vector2(Input.GetAxisRaw("Horizontal"), Input.GetAxisRaw("Vertical")), 1f);
            float speed = crouching ? crouchSpeed : sprinting ? sprintSpeed : walkSpeed;
            Vector3 movement = transform.TransformDirection(new Vector3(input.x, 0f, input.y)) * speed;
            if (controller.isGrounded && verticalVelocity < 0f) verticalVelocity = -2f;
            verticalVelocity += gravity * Time.deltaTime;
            movement.y = verticalVelocity;
            controller.Move(movement * Time.deltaTime);

            // Every footstep sends out a noise pulse; louder movement reaches farther.
            if (input.sqrMagnitude > 0.01f && controller.isGrounded)
            {
                stepTimer -= Time.deltaTime;
                if (stepTimer <= 0f)
                {
                    float radius = crouching ? NoisePulse.Creep : sprinting ? NoisePulse.Sprint : NoisePulse.Walk;
                    NoisePulse.Emit(transform.position, radius);
                    stepSource.PlayOneShot(ProceduralSounds.Step, crouching ? 0.15f : sprinting ? 0.8f : 0.4f);
                    stepTimer = sprinting ? 0.3f : crouching ? 0.6f : 0.45f;
                }
            }
            else stepTimer = 0f;
        }

        // Space holds breath, which drains the breath bar. Running out forces a gasp.
        private void UpdateBreath()
        {
            gaspCooldown -= Time.deltaTime;
            HoldingBreath = Input.GetKey(KeyCode.Space) && gaspCooldown <= 0f;
            if (HoldingBreath)
            {
                breath = Mathf.Max(0f, breath - Time.deltaTime);
                if (breath <= 0f)
                {
                    // Out of air: a loud gasp the Sexton can hear from far away.
                    NoisePulse.Emit(transform.position, NoisePulse.Gasp);
                    HoldingBreath = false;
                    gaspCooldown = 2f;
                }
            }
            else breath = Mathf.Min(maxBreath, breath + breathRecovery * Time.deltaTime);
        }

        // Hiding turns off movement and parks the camera inside the wardrobe.
        private void EnterWardrobe()
        {
            hiding = true;
            currentHideSpot = nearbyHideSpot;
            controller.enabled = false;
            transform.position = currentHideSpot.transform.position;
            playerCamera.transform.localPosition = new Vector3(0f, 0.65f, 0f);
        }

        // Puts Nell back in the world at the wardrobe's exit point.
        private void LeaveWardrobe()
        {
            hiding = false;
            transform.position = currentHideSpot != null ? currentHideSpot.ExitPoint.position : transform.position + transform.forward;
            controller.enabled = true;
            currentHideSpot = null;
        }

        // Adds the pickup to Nell's inventory and removes it from the world.
        private void Collect(Pickup pickup)
        {
            if (pickup.Type == Pickup.Kind.Evidence) Evidence++;
            else if (Bottles < MaxBottles) Bottles++;
            else return; // pockets full, leave the bottle where it is
            Destroy(pickup.gameObject);
            NearbyPickup = null;
        }

        // A non-lethal hit (the Sexton's swipe). Lethal attacks go through Die instead.
        public void TakeDamage(float amount)
        {
            Health = Mathf.Max(0f, Health - amount);
            HurtFlash = 1f;
        }

        // Called by the Sexton when he grabs Nell. Plays the kill animation, then the HUD shows game over.
        public void Die(SextonBody killer)
        {
            if (IsDead) return;
            Health = 0f;
            HoldingBreath = false;
            HurtFlash = 1f;
            StartCoroutine(KillAnimation(killer));
        }

        // Nell is pulled out of hiding, turned to face the Sexton, lifted toward his hands and shaken, then the screen goes dark.
        private IEnumerator KillAnimation(SextonBody killer)
        {
            if (hiding) LeaveWardrobe();
            controller.enabled = false;
            stepSource.PlayOneShot(ProceduralSounds.Sting, 1f);

            Vector3 start = transform.position;
            Vector3 eye = playerCamera.transform.localPosition;
            for (float time = 0f; time < 2.4f; time += Time.deltaTime)
            {
                // 0 to 0.8 s: dragged up to his grip. 0.8 to 1.8 s: shaken. Then the fade finishes.
                float lift = Mathf.SmoothStep(0f, 1f, time / 0.8f);
                float shake = time > 0.8f && time < 1.8f ? 0.06f : 0.01f;
                transform.position = Vector3.Lerp(start, killer.GripPoint - eye, lift) + Random.insideUnitSphere * shake;

                Vector3 toHead = killer.Head.position - playerCamera.transform.position;
                Quaternion look = Quaternion.LookRotation(toHead) * Quaternion.Euler(0f, 0f, Mathf.Sin(time * 25f) * shake * 60f);
                playerCamera.transform.rotation = Quaternion.Slerp(playerCamera.transform.rotation, look, Time.deltaTime * 8f);

                DeathFade = Mathf.Clamp01((time - 1.5f) / 0.9f);
                yield return null;
            }
            DeathFade = 1f;
        }

        // Throws from just in front of the camera, in the direction Nell is looking.
        private void ThrowBottle()
        {
            Bottles--;
            ThrowableBottle.Throw(playerCamera.transform.position + playerCamera.transform.forward * 0.8f, playerCamera.transform.forward);
        }

        // Wardrobe triggers tell the player when a hide spot is in reach.
        private void OnTriggerEnter(Collider other)
        {
            HideSpot spot = other.GetComponentInParent<HideSpot>();
            if (spot != null) nearbyHideSpot = spot;
        }

        // Forget the hide spot once Nell walks out of its trigger.
        private void OnTriggerExit(Collider other)
        {
            if (other.GetComponentInParent<HideSpot>() == nearbyHideSpot) nearbyHideSpot = null;
        }
    }
}
