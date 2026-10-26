using UnityEngine;

namespace Hushwater
{
    [RequireComponent(typeof(CharacterController))]
    public sealed class PlayerController : MonoBehaviour
    {
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
        public bool NearHideSpot => nearbyHideSpot != null;

        private void Awake()
        {
            controller = GetComponent<CharacterController>();
            controller.stepOffset = 0.4f;
            playerCamera = GetComponentInChildren<Camera>();
            breath = maxBreath;
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }

        private void Update()
        {
            if (IsDead) return;

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
            if (nearbyHideSpot != null && Input.GetKeyDown(KeyCode.E)) EnterWardrobe();
            if (Input.GetKeyDown(KeyCode.Q) && Bottles > 0) ThrowBottle();
        }

        private void Look()
        {
            if (Cursor.lockState != CursorLockMode.Locked) return;
            transform.Rotate(Vector3.up * Input.GetAxis("Mouse X") * mouseSensitivity);
            pitch = Mathf.Clamp(pitch - Input.GetAxis("Mouse Y") * mouseSensitivity, -85f, 85f);
            playerCamera.transform.localRotation = Quaternion.Euler(pitch, 0f, 0f);
        }

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
                    stepTimer = sprinting ? 0.3f : crouching ? 0.6f : 0.45f;
                }
            }
            else stepTimer = 0f;
        }

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

        private void EnterWardrobe()
        {
            hiding = true;
            currentHideSpot = nearbyHideSpot;
            controller.enabled = false;
            transform.position = currentHideSpot.transform.position;
            playerCamera.transform.localPosition = new Vector3(0f, 0.65f, 0f);
        }

        private void LeaveWardrobe()
        {
            hiding = false;
            transform.position = currentHideSpot != null ? currentHideSpot.ExitPoint.position : transform.position + transform.forward;
            controller.enabled = true;
            currentHideSpot = null;
        }

        public void TakeDamage(float amount) => Health = Mathf.Max(0f, Health - amount);

        private void ThrowBottle()
        {
            Bottles--;
            ThrowableBottle.Throw(playerCamera.transform.position + playerCamera.transform.forward * 0.8f, playerCamera.transform.forward);
        }

        private void OnTriggerEnter(Collider other)
        {
            HideSpot spot = other.GetComponentInParent<HideSpot>();
            if (spot != null) nearbyHideSpot = spot;
        }

        private void OnTriggerExit(Collider other)
        {
            if (other.GetComponentInParent<HideSpot>() == nearbyHideSpot) nearbyHideSpot = null;
        }
    }
}
