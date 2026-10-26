using System;
using System.Collections.Generic;
using UnityEngine;

namespace Hushwater
{
    [RequireComponent(typeof(CharacterController))]
    public sealed class PlayerController : MonoBehaviour
    {
        private const float StandHeight = 1.75f;
        private const float CrouchHeight = 1.1f;
        private const float StandEye = 0.72f;
        private const float CrouchEye = 0.12f;
        private const float MaxBreath = 30f;

        [Header("Movement")]
        [SerializeField] private float walkSpeed = 3.1f;
        [SerializeField] private float sprintSpeed = 5.9f;
        [SerializeField] private float creepSpeed = 1.45f;
        [SerializeField] private float gravity = -20f;

        private CharacterController controller;
        private Camera view;
        private float verticalVelocity;
        private float pitch;
        private float yaw;
        private float breath = MaxBreath;
        private float gaspCooldown;
        private float stepDistance;
        private float stepPhase;
        private int stepFoot;
        private float printDistance;
        private float breathingTimer;
        private float pantTimer;
        private float knockdownTimer;
        private float shake;
        private float eyeHeight = StandEye;
        private int deepMudContacts;
        private HideSpot hideSpot;
        private float hideYaw;
        private IInteractable target;
        private IInteractable heldTarget;
        private float holdTime;
        private AudioSource crankLoop;
        private readonly Dictionary<PickupKind, int> tools = new Dictionary<PickupKind, int>
        {
            { PickupKind.Handbell, 0 }, { PickupKind.Bottle, 1 }, { PickupKind.Salts, 0 }
        };

        public static event Action<float, NoiseKind> MadeNoise;

        public bool InputEnabled { get; set; }
        public float Vitality { get; private set; } = 100f;
        public float BreathNormalized => breath / MaxBreath;
        public bool HoldingBreath { get; private set; }
        public bool IsHiding => hideSpot != null;
        public HideSpot CurrentHideSpot => hideSpot;
        public bool IsCrouching { get; private set; }
        public bool IsSprinting { get; private set; }
        public bool KnockedDown => knockdownTimer > 0f;
        public bool CanAct => InputEnabled && !IsHiding && !KnockedDown;
        public Camera View => view;
        public string Prompt { get; private set; }
        public float HoldProgress { get; private set; }
        public PickupKind SelectedTool { get; private set; } = PickupKind.Bottle;
        public int ToolCount(PickupKind kind) => tools[kind];
        public static int ToolCapacity(PickupKind kind) => kind == PickupKind.Bottle ? 3 : 2;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => MadeNoise = null;

        private void Awake()
        {
            controller = GetComponent<CharacterController>();
            controller.height = StandHeight;
            controller.radius = 0.32f;
            controller.stepOffset = 0.45f;
            controller.slopeLimit = 50f;
            view = GetComponentInChildren<Camera>();
            yaw = transform.eulerAngles.y;
        }

        public void Teleport(Vector3 feet, float facing)
        {
            if (IsHiding) LeaveHideSpot();
            controller.enabled = false;
            transform.SetPositionAndRotation(feet + Vector3.up * (StandHeight / 2f + 0.05f), Quaternion.Euler(0f, facing, 0f));
            yaw = facing;
            pitch = 0f;
            verticalVelocity = 0f;
            controller.enabled = true;
        }

        public void ResetVitals()
        {
            Vitality = 100f;
            breath = MaxBreath;
            knockdownTimer = 0f;
        }

        private void Update()
        {
            float dt = Time.deltaTime;
            gaspCooldown -= dt;
            knockdownTimer -= dt;
            shake = Mathf.MoveTowards(shake, 0f, dt * 2f);

            if (InputEnabled && !KnockedDown) Look();
            UpdateBreath(dt);

            if (IsHiding)
            {
                Prompt = "E — leave the " + hideSpot.Label + (HoldingBreath ? "" : "   ·   hold Space to hold your breath");
                HoldProgress = 0f;
                if (InputEnabled && Input.GetKeyDown(KeyCode.E)) LeaveHideSpot();
                ApplyCamera(dt, Vector2.zero);
                return;
            }

            Vector2 input = CanAct ? Vector2.ClampMagnitude(new Vector2(Input.GetAxisRaw("Horizontal"), Input.GetAxisRaw("Vertical")), 1f) : Vector2.zero;
            UpdateCrouch();
            Move(input, dt);
            if (CanAct)
            {
                UpdateInteraction(dt);
                UpdateTools();
            }
            else
            {
                Prompt = null;
                HoldProgress = 0f;
                StopCrank();
            }
            UpdateBreathingNoise(dt);
            ApplyCamera(dt, input);
        }

        private void Look()
        {
            if (Cursor.lockState != CursorLockMode.Locked) return;
            float sensitivity = GameSettings.MouseSensitivity;
            yaw += Input.GetAxis("Mouse X") * sensitivity;
            pitch = Mathf.Clamp(pitch - Input.GetAxis("Mouse Y") * sensitivity, -85f, 85f);
            if (IsHiding)
            {
                yaw = hideYaw + Mathf.Clamp(Mathf.DeltaAngle(hideYaw, yaw), -45f, 45f);
                pitch = Mathf.Clamp(pitch, -30f, 30f);
            }
            transform.rotation = Quaternion.Euler(0f, yaw, 0f);
        }

        private void UpdateCrouch()
        {
            if (CanAct && (Input.GetKeyDown(KeyCode.LeftControl) || Input.GetKeyDown(KeyCode.C)))
            {
                if (IsCrouching)
                {
                    // Only stand if there is headroom.
                    Vector3 top = transform.position + Vector3.up * (CrouchHeight / 2f - 0.325f);
                    if (!Physics.SphereCast(top, 0.3f, Vector3.up, out _, StandHeight - CrouchHeight, ~0, QueryTriggerInteraction.Ignore))
                        IsCrouching = false;
                }
                else IsCrouching = true;
            }
            float h = IsCrouching ? CrouchHeight : StandHeight;
            controller.height = h;
            controller.center = new Vector3(0f, -(StandHeight - h) / 2f, 0f);
        }

        private void Move(Vector2 input, float dt)
        {
            IsSprinting = CanAct && Input.GetKey(KeyCode.LeftShift) && input.y > 0.1f && !IsCrouching;
            if (IsSprinting && IsCrouching) IsCrouching = false;
            bool deep = deepMudContacts > 0;
            float speed = IsCrouching ? creepSpeed : IsSprinting ? sprintSpeed : walkSpeed;
            if (deep) speed *= 0.65f;
            Vector3 move = transform.TransformDirection(new Vector3(input.x, 0f, input.y)) * speed;
            if (controller.isGrounded && verticalVelocity < 0f) verticalVelocity = -2f;
            verticalVelocity += gravity * dt;
            move.y = verticalVelocity;
            Vector3 before = transform.position;
            controller.Move(move * dt);
            Vector3 delta = transform.position - before;
            delta.y = 0f;
            float travelled = delta.magnitude;

            if (!controller.isGrounded || travelled < 0.0001f) return;
            float stride = IsCrouching ? 0.55f : IsSprinting ? 1.15f : 0.8f;
            stepDistance += travelled;
            stepPhase += travelled / stride * Mathf.PI;
            printDistance += travelled;
            if (stepDistance >= stride)
            {
                stepDistance = 0f;
                Footstep(deep);
            }
            if (printDistance >= 0.75f)
            {
                printDistance = 0f;
                LeavePrint();
            }
        }

        private void Footstep(bool deep)
        {
            float radius = IsCrouching ? NoisePulse.Creep : IsSprinting ? NoisePulse.Sprint : deep ? NoisePulse.DeepMud : NoisePulse.Walk;
            Vector3 feet = transform.position + Vector3.down * (controller.height / 2f);
            bool mud = OnMud(out _);
            float volume = IsCrouching ? 0.12f : IsSprinting ? 0.7f : 0.35f;
            SoundFx.PlayAt(ProceduralAudio.Footstep(stepFoot % 3, mud || deep), feet, volume, 12f, UnityEngine.Random.Range(0.9f, 1.1f));
            stepFoot++;
            Emit(feet, radius, NoiseKind.Footstep);
            if (IsSprinting) pantTimer = 4f;
        }

        private bool OnMud(out RaycastHit hit)
        {
            return Physics.Raycast(transform.position, Vector3.down, out hit, 2.2f, ~(1 << 2), QueryTriggerInteraction.Ignore)
                   && hit.collider.GetComponent<MudSurface>() != null;
        }

        private void LeavePrint()
        {
            if (!OnMud(out RaycastHit hit)) return;
            Vector3 side = transform.right * ((stepFoot & 1) == 0 ? 0.12f : -0.12f);
            Vector3 point = hit.point + side;
            MudTrail.Add(point);
            Footprints.Place(point + hit.normal * 0.012f, Quaternion.LookRotation(transform.forward, hit.normal));
        }

        private void UpdateBreath(float dt)
        {
            HoldingBreath = InputEnabled && !KnockedDown && Input.GetKey(KeyCode.Space) && gaspCooldown <= 0f;
            if (HoldingBreath)
            {
                breath = Mathf.Max(0f, breath - dt);
                if (breath <= 0f)
                {
                    // Let it run out and you gasp.
                    HoldingBreath = false;
                    gaspCooldown = 2.5f;
                    SoundFx.Play2D(ProceduralAudio.Gasp, 0.9f);
                    Emit(transform.position, NoisePulse.Gasp, NoiseKind.Gasp);
                    NightDirector.Instance?.Caption("[You gasp for air]", 2.5f);
                }
            }
            else breath = Mathf.Min(MaxBreath, breath + dt * 5f);
        }

        // Nell's breathing carries a metre or two; panting after a sprint carries further.
        private void UpdateBreathingNoise(float dt)
        {
            pantTimer -= dt;
            breathingTimer -= dt;
            if (breathingTimer > 0f) return;
            breathingTimer = 1.5f;
            if (HoldingBreath) return;
            Emit(transform.position, pantTimer > 0f ? 3.5f : 1.2f, NoiseKind.Breathing);
        }

        private void Emit(Vector3 position, float radius, NoiseKind kind)
        {
            NoisePulse.Emit(position, radius, kind);
            if (kind != NoiseKind.Breathing) MadeNoise?.Invoke(radius, kind);
        }

        public void EmitNoise(Vector3 position, float radius, NoiseKind kind) => Emit(position, radius, kind);

        private void UpdateInteraction(float dt)
        {
            target = FindTarget();
            string prompt = target?.Prompt(this);
            Prompt = string.IsNullOrEmpty(prompt) ? null : prompt;
            if (target == null || target.HoldSeconds <= 0f)
            {
                HoldProgress = 0f;
                holdTime = 0f;
                StopCrank();
                if (target != null && Input.GetKeyDown(KeyCode.E)) target.Interact(this);
                return;
            }
            if (Input.GetKey(KeyCode.E))
            {
                if (heldTarget != target) holdTime = 0f;
                heldTarget = target;
                holdTime += dt;
                HoldProgress = Mathf.Clamp01(holdTime / target.HoldSeconds);
                if (target is LanternPost) StartCrank();
                if (holdTime >= target.HoldSeconds)
                {
                    target.Interact(this);
                    holdTime = 0f;
                    HoldProgress = 0f;
                    StopCrank();
                }
            }
            else
            {
                holdTime = 0f;
                HoldProgress = 0f;
                StopCrank();
            }
        }

        private IInteractable FindTarget()
        {
            Ray ray = new Ray(view.transform.position, view.transform.forward);
            RaycastHit[] hits = Physics.RaycastAll(ray, 2.6f, ~0, QueryTriggerInteraction.Collide);
            Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));
            foreach (RaycastHit hit in hits)
            {
                if (hit.collider.transform.IsChildOf(transform)) continue;
                if (hit.collider.GetComponent<InvisibleWall>() != null) continue;
                IInteractable interactable = hit.collider.GetComponentInParent<IInteractable>();
                if (interactable != null) return interactable;
                if (!hit.collider.isTrigger) return null;
            }
            return null;
        }

        private void StartCrank()
        {
            if (crankLoop == null) crankLoop = SoundFx.Loop(gameObject, ProceduralAudio.Crank, 0.5f, 15f, false);
            if (!crankLoop.isPlaying) crankLoop.Play();
        }

        private void StopCrank()
        {
            if (crankLoop != null && crankLoop.isPlaying) crankLoop.Stop();
        }

        private void UpdateTools()
        {
            if (Input.GetKeyDown(KeyCode.Alpha1)) SelectedTool = PickupKind.Handbell;
            if (Input.GetKeyDown(KeyCode.Alpha2)) SelectedTool = PickupKind.Bottle;
            if (Input.GetKeyDown(KeyCode.Alpha3)) SelectedTool = PickupKind.Salts;
            if (!Input.GetKeyDown(KeyCode.Q) || tools[SelectedTool] <= 0) return;
            tools[SelectedTool]--;
            if (SelectedTool == PickupKind.Salts)
            {
                Vitality = Mathf.Min(100f, Vitality + 25f);
                breath = MaxBreath;
                Emit(transform.position, 5f, NoiseKind.Interact);
                NightDirector.Instance?.Caption("[A sharp sniff of smelling salts]", 2f);
                return;
            }
            Throwable.Throw(SelectedTool, view.transform.position + view.transform.forward * 0.6f, view.transform.forward);
            Emit(transform.position, 2f, NoiseKind.Interact);
        }

        public bool AddTool(PickupKind kind)
        {
            if (tools[kind] >= ToolCapacity(kind))
            {
                NightDirector.Instance?.Caption($"You can't carry more ({ToolCapacity(kind)} max)", 2f);
                return false;
            }
            tools[kind]++;
            SelectedTool = kind;
            return true;
        }

        public void EnterHideSpot(HideSpot spot)
        {
            hideSpot = spot;
            controller.enabled = false;
            IsCrouching = false;
            UpdateCrouch();
            transform.position = spot.ViewPoint.position - Vector3.up * StandEye;
            hideYaw = spot.ViewPoint.eulerAngles.y;
            yaw = hideYaw;
            pitch = 0f;
            transform.rotation = Quaternion.Euler(0f, yaw, 0f);
            Emit(transform.position, 1.5f, NoiseKind.Interact);
            SoundFx.PlayAt(ProceduralAudio.DoorForce, transform.position, 0.15f, 8f, 1.6f);
        }

        public void LeaveHideSpot()
        {
            if (hideSpot == null) return;
            transform.position = hideSpot.ExitPoint.position + Vector3.up * 0.1f;
            hideSpot = null;
            controller.enabled = true;
            Emit(transform.position, 1.5f, NoiseKind.Interact);
        }

        public void TakeDamage(float amount, bool grab)
        {
            Vitality = Mathf.Max(0f, Vitality - amount);
            shake = grab ? 1f : 0.6f;
            ScreenGrade grade = view.GetComponent<ScreenGrade>();
            if (grade != null) grade.Damage = grab ? 1.2f : 0.8f;
            SoundFx.Play2D(ProceduralAudio.Gasp, 0.7f, 0.8f);
            if (grab)
            {
                if (IsHiding) LeaveHideSpot();
                knockdownTimer = 1.8f;
                // Thrown back, away from him.
                controller.Move(-transform.forward * 1.2f + Vector3.up * 0.1f);
            }
            if (Vitality <= 0f) NightDirector.Instance?.PlayerDowned();
        }

        private void ApplyCamera(float dt, Vector2 input)
        {
            float targetEye = IsHiding ? StandEye : IsCrouching ? CrouchEye : StandEye;
            if (KnockedDown) targetEye = -0.5f;
            eyeHeight = Mathf.Lerp(eyeHeight, targetEye, dt * 8f);
            float moving = input.sqrMagnitude > 0.01f && controller.enabled && controller.isGrounded ? 1f : 0f;
            float bobAmount = (IsSprinting ? 0.06f : IsCrouching ? 0.02f : 0.035f) * moving;
            Vector3 bob = new Vector3(Mathf.Cos(stepPhase * 0.5f) * bobAmount * 0.6f, Mathf.Abs(Mathf.Sin(stepPhase * 0.5f)) * bobAmount, 0f);
            Vector3 jitter = UnityEngine.Random.insideUnitSphere * shake * 0.05f;
            view.transform.localPosition = new Vector3(0f, eyeHeight, 0f) + bob + jitter;
            float roll = KnockedDown ? 25f : -input.x * 1.2f;
            view.transform.localRotation = Quaternion.Euler(pitch, 0f, roll);
        }

        private void OnTriggerEnter(Collider other)
        {
            MudSurface mud = other.GetComponent<MudSurface>();
            if (mud != null && mud.Deep) deepMudContacts++;
        }

        private void OnTriggerExit(Collider other)
        {
            MudSurface mud = other.GetComponent<MudSurface>();
            if (mud != null && mud.Deep) deepMudContacts = Mathf.Max(0, deepMudContacts - 1);
        }
    }

    // Pooled footprints pressed into the silt.
    public static class Footprints
    {
        private static readonly Queue<GameObject> Pool = new Queue<GameObject>();
        private static Mesh mesh;
        private static Material material;
        private static Transform parent;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            Pool.Clear();
            parent = null;
        }

        public static void Place(Vector3 position, Quaternion rotation)
        {
            if (parent == null)
            {
                parent = new GameObject("Footprints").transform;
                if (NightDirector.Instance != null && NightDirector.Instance.WorldRoot != null) parent.SetParent(NightDirector.Instance.WorldRoot, true);
                Pool.Clear();
            }
            if (mesh == null)
            {
                mesh = new Mesh { name = "Footprint" };
                mesh.vertices = new[] { new Vector3(-0.055f, 0f, -0.13f), new Vector3(0.055f, 0f, -0.13f), new Vector3(0.065f, 0f, 0.13f), new Vector3(-0.065f, 0f, 0.13f) };
                mesh.uv = new[] { Vector2.zero, Vector2.right, Vector2.one, Vector2.up };
                mesh.triangles = new[] { 0, 2, 1, 0, 3, 2 };
                mesh.RecalculateNormals();
                material = HushwaterAssets.Plain(new Color(0.07f, 0.06f, 0.05f), 0.8f);
            }
            GameObject print;
            if (Pool.Count >= 160) print = Pool.Dequeue();
            else
            {
                print = new GameObject("Print");
                print.transform.SetParent(parent, false);
                print.AddComponent<MeshFilter>().sharedMesh = mesh;
                MeshRenderer renderer = print.AddComponent<MeshRenderer>();
                renderer.sharedMaterial = material;
                renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            }
            print.transform.SetPositionAndRotation(position, rotation);
            Pool.Enqueue(print);
        }
    }
}
