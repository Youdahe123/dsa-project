using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Hushwater
{
    [RequireComponent(typeof(CharacterController))]
    // Nell: first-person movement, breath, swimming, hiding, tools, hold-to-interact, knockdowns, and her death animation.
    // The survey camera (SurveyCamera) and her crank lantern (NellLantern) are separate components on the same object.
    public sealed class PlayerController : MonoBehaviour
    {
        // One tool slot: a kind of tool and how many of it she carries.
        public sealed class ToolStack
        {
            public Pickup.Kind Kind;
            public int Count;
        }

        private const float PickupReach = 2f;
        // Design doc: smelling salts give +25 Vitality (max 100) and refill Breath.
        private const float SaltsHeal = 25f;
        // Design doc: oilcloth wraps give "silent steps (2 m) for 25 s".
        private const float WrapSeconds = 25f;
        // Design doc: flooded passages, "when it's empty you lose 10 Vitality a second".
        private const float DrowningDamage = 10f;
        // Design doc: Silt Hands "grip for 4 s, damage every second". Each tap of E shortens it by this much (ours).
        private const float GripSeconds = 4f;
        private const float GripTap = 0.4f;

        public static PlayerController Instance { get; private set; }

        [Header("Movement")]
        [SerializeField] private float walkSpeed = 3.2f;
        [SerializeField] private float sprintSpeed = 6f;
        [SerializeField] private float crouchSpeed = 1.5f;
        [SerializeField] private float swimSpeed = 2f;
        [SerializeField] private float mudSlowdown = 0.75f;
        [SerializeField] private float gravity = -20f;
        [SerializeField] private float mouseSensitivity = 2.2f;
        [Header("Breath (design doc: 30 s of total silence; diver's lungs make it 40 s)")]
        [SerializeField] private float breathRecovery = 5f;

        private readonly List<ToolStack> tools = new List<ToolStack>();
        private CharacterController controller;
        private Camera playerCamera;
        private AudioSource stepSource;
        private float maxBreath = 30f;
        private float verticalVelocity;
        private float pitch;
        private float breath;
        private float stepTimer;
        private float gaspCooldown;
        private float drownTick;
        private bool crouching;
        private bool sprinting;
        private bool hiding;
        private HideSpot nearbyHideSpot;
        private Interactable holding;
        private float gripTime;
        private float gripTick;

        public float Health { get; private set; } = 100f;
        public float MaxBreath => maxBreath;
        public float BreathNormalized => breath / maxBreath;
        public float BreathSeconds => breath;
        public bool IsHiding => hiding;
        public bool IsCrouching => crouching;
        public bool IsSprinting => sprinting;
        public bool InMud { get; private set; }
        public bool Underwater { get; private set; }
        public bool HoldingBreath { get; private set; }
        public bool IsDead => Health <= 0f;
        public bool IsKnockedDown { get; private set; }
        public bool IsGripped => gripTime > 0f;
        // Up a rope ladder, on a roof he can't reach.
        public bool OnRoof => transform.position.y > 2.5f;
        // Set by the NightDirector while paused or after the night ends.
        public bool InputLocked { get; set; }
        // Set by the survey camera while it is raised: slower movement, no tools.
        public bool CameraRaised { get; set; }
        public float WrapTimeLeft { get; private set; }
        public int SelectedSlot { get; private set; }
        public int SlotCount => Campaign.Owns(Upgrade.ToolSlot) ? 4 : 3;
        public IReadOnlyList<ToolStack> Tools => tools;
        public static readonly Pickup.Kind[] ToolSlots =
        {
            Pickup.Kind.Bottle, Pickup.Kind.Handbell, Pickup.Kind.Salts, Pickup.Kind.Wraps
        };
        public int ToolCount(Pickup.Kind kind)
        {
            ToolStack stack = tools.Find(s => s.Kind == kind);
            return stack == null ? 0 : stack.Count;
        }
        public int Evidence => Campaign.FoundTonight.Count;
        public static int EvidenceNeeded => Story.PhotosNeeded(Campaign.Night) +
            Story.PagesNeeded(Campaign.Night) + Story.TapesNeeded(Campaign.Night);
        public bool NearHideSpot => nearbyHideSpot != null;
        public HideSpot NearbyHideSpot => nearbyHideSpot;
        public HideSpot CurrentHideSpot { get; private set; }
        public Pickup NearbyPickup { get; private set; }
        // A photo scene close enough to photograph with E.
        public PhotoSubject NearbyPhoto { get; private set; }
        // The hold-to-use object in reach (lantern post, barred door, crate, cabinet...) and how far the hold has got.
        public Interactable FocusedInteractable { get; private set; }
        public float HoldProgress { get; private set; }
        public LanternPost Checkpoint { get; private set; }
        public Camera View => playerCamera;
        // Counters for the end-of-night screen.
        public int Knockdowns { get; private set; }
        public int Gasps { get; private set; }
        public int FoundTonight { get; private set; }
        // 1 right after a hit, fading to 0; the HUD tints the screen red with it.
        public float HurtFlash { get; private set; }
        // 0 to 1 while the death animation fades the screen out.
        public float DeathFade { get; private set; }
        public string DeathCause { get; private set; } = "";

        // Short messages for the HUD ("Lantern post lit", "Pockets full").
        public event Action<string> Notified;
        // Where a piece of evidence was taken from, so the Sexton can start guarding it.
        public event Action<Vector3> EvidenceTaken;

        // Caches components, fills the breath bar and locks the mouse for first-person look.
        private void Awake()
        {
            Instance = this;
            controller = GetComponent<CharacterController>();
            controller.stepOffset = 0.4f;
            playerCamera = GetComponentInChildren<Camera>();
            stepSource = gameObject.AddComponent<AudioSource>();
            maxBreath = Campaign.Owns(Upgrade.Lungs) ? 40f : 30f;
            breath = maxBreath;
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        // Main input loop. Death, knockdowns, pausing and the end of the night stop all input.
        private void Update()
        {
            HurtFlash = Mathf.MoveTowards(HurtFlash, 0f, Time.deltaTime * 1.5f);
            WrapTimeLeft = Mathf.Max(0f, WrapTimeLeft - Time.deltaTime);
            if (IsDead || IsKnockedDown || InputLocked)
            {
                HoldingBreath = false;
                return;
            }

            UpdateWater();
            UpdateBreath();
            Campaign.Reveal(transform.position, 6f);

            if (hiding)
            {
                if (GameInput.Down(KeyCode.E)) LeaveHideSpot();
                return;
            }

            Look();
            if (IsGripped)
            {
                UpdateGrip();
                return;
            }
            Move();
            SelectTool();

            // E picks up whatever is closest first, otherwise hides; hold-to-use objects come last.
            NearbyPickup = Pickup.Nearest(transform.position, PickupReach);
            NearbyPhoto = Underwater ? null : PhotoSubject.Near(transform.position, PhotoSubject.SnapReach);
            if (GameInput.Down(KeyCode.E))
            {
                if (NearbyPickup != null) Collect(NearbyPickup);
                else if (NearbyPhoto != null) GetComponent<SurveyCamera>().Snap(NearbyPhoto);
                else if (nearbyHideSpot != null && !Underwater) EnterHideSpot();
            }
            UpdateHold();
            if (GameInput.Down(KeyCode.Q) && !CameraRaised) UseTool();
        }

        // Mouse look: yaw turns the body, pitch tilts only the camera.
        private void Look()
        {
            if (Cursor.lockState != CursorLockMode.Locked) return;
            Vector2 look = GameInput.Look * GameSettings.LookSensitivity;
            transform.Rotate(Vector3.up * look.x * mouseSensitivity);
            pitch = Mathf.Clamp(pitch - look.y * mouseSensitivity, -85f, 85f);
            playerCamera.transform.localRotation = Quaternion.Euler(pitch, 0f, 0f);
        }

        // Walking, crouching, sprinting and swimming, plus the footstep noise each one makes.
        private void Move()
        {
            // Crouch is a toggle, as in the design doc's controls table.
            if (GameInput.Down(KeyCode.LeftControl) || Input.GetKeyDown(KeyCode.C)) crouching = !crouching;
            // Design doc: "No sprinting while wrapped." The camera raised is a slow walk too.
            sprinting = GameInput.Held(KeyCode.LeftShift) && GameInput.Move.y > 0.1f && WrapTimeLeft <= 0f && !Underwater && !CameraRaised;
            if (sprinting) crouching = false;

            controller.height = crouching ? 1.1f : 1.8f;
            controller.center = new Vector3(0f, crouching ? -0.35f : 0f, 0f);
            Vector3 eye = new Vector3(0f, crouching ? 0.1f : 0.75f, 0f);
            playerCamera.transform.localPosition = Vector3.Lerp(playerCamera.transform.localPosition, eye, Time.deltaTime * 10f);

            InMud = MudTracks.InMud(transform.position) && !OnRoof;
            Vector2 input = GameInput.Move;
            float speed = Underwater ? swimSpeed : crouching ? crouchSpeed : sprinting ? sprintSpeed : walkSpeed;
            if (InMud) speed *= mudSlowdown;
            if (CameraRaised) speed = Mathf.Min(speed, crouchSpeed);
            Vector3 movement = transform.TransformDirection(new Vector3(input.x, 0f, input.y)) * speed;
            if (controller.isGrounded && verticalVelocity < 0f) verticalVelocity = -2f;
            verticalVelocity += gravity * Time.deltaTime;
            movement.y = verticalVelocity;
            controller.Move(movement * Time.deltaTime);

            // Every footstep sends out a noise pulse; louder movement reaches farther. Swimming is silent.
            if (input.sqrMagnitude > 0.01f && controller.isGrounded && !Underwater)
            {
                stepTimer -= Time.deltaTime;
                if (stepTimer <= 0f) Footstep();
            }
            else stepTimer = 0f;
        }

        // One footstep: picks the noise radius from the doc table and, in mud, leaves a groove.
        private void Footstep()
        {
            float radius;
            NoiseKind kind;
            bool wrapped = WrapTimeLeft > 0f;
            if (crouching || wrapped) { radius = NoisePulse.Creep; kind = NoiseKind.Creep; }
            else if (sprinting) { radius = NoisePulse.Sprint; kind = NoiseKind.Sprint; }
            else if (InMud) { radius = NoisePulse.Mud; kind = NoiseKind.Mud; }
            else { radius = Campaign.Owns(Upgrade.Boots) ? NoisePulse.WalkFelt : NoisePulse.Walk; kind = NoiseKind.Walk; }

            NoisePulse.Emit(transform.position, radius, kind);
            float volume = crouching || wrapped ? 0.15f : sprinting ? 0.8f : 0.4f;
            stepSource.PlayOneShot(InMud ? ProceduralSounds.MudStep : ProceduralSounds.Step, volume);
            // Design doc: wraps "leave no mud grooves".
            if (InMud && !wrapped) MudTracks.Add(transform.position, transform.rotation);
            if (InMud && !crouching && !wrapped) SiltHands.Stepped(this);
            stepTimer = sprinting ? 0.3f : crouching ? 0.6f : 0.45f;
        }

        // Space holds breath, which drains the breath bar. Running out forces a gasp. Underwater it drains on its own.
        private void UpdateBreath()
        {
            gaspCooldown -= Time.deltaTime;
            if (Underwater)
            {
                HoldingBreath = true;
                breath = Mathf.Max(0f, breath - Time.deltaTime);
                if (breath > 0f) return;
                drownTick -= Time.deltaTime;
                if (drownTick > 0f) return;
                drownTick = 1f;
                Hurt(DrowningDamage, "The water kept you.");
                return;
            }
            HoldingBreath = GameInput.Held(KeyCode.Space) && gaspCooldown <= 0f;
            if (HoldingBreath)
            {
                breath = Mathf.Max(0f, breath - Time.deltaTime);
                if (breath <= 0f) Gasp();
            }
            else breath = Mathf.Min(maxBreath, breath + breathRecovery * Time.deltaTime);
        }

        // Out of air: a loud gasp the Sexton can hear from far away.
        private void Gasp()
        {
            NoisePulse.Emit(transform.position, NoisePulse.Gasp, NoiseKind.Gasp);
            stepSource.PlayOneShot(ProceduralSounds.GaspSound, 1f);
            HoldingBreath = false;
            gaspCooldown = 2f;
            Gasps++;
        }

        // Ducking into or out of a flooded passage.
        private void UpdateWater()
        {
            bool under = WaterVolume.Contains(playerCamera.transform.position);
            if (under == Underwater) return;
            Underwater = under;
            stepSource.PlayOneShot(ProceduralSounds.Splash, 0.7f);
            if (under)
            {
                crouching = false;
                Notify("Underwater. You're holding your breath; find air before it runs out.");
                return;
            }
            NoisePulse.Emit(transform.position, NoisePulse.Splash, NoiseKind.Splash);
            // Surfacing with empty lungs: a gasp he can hear.
            if (breath < maxBreath * 0.15f) Gasp();
        }

        // An eel bite: "costs 5 s of Breath, not Vitality".
        public void LoseBreath(float seconds)
        {
            breath = Mathf.Max(0f, breath - seconds);
            HurtFlash = Mathf.Max(HurtFlash, 0.4f);
        }

        // Hurts Nell outside of the Sexton's attacks (drowning, falls, silt hands). Can kill.
        public void Hurt(float amount, string cause)
        {
            if (IsDead) return;
            Health = Mathf.Max(0f, Health - amount);
            HurtFlash = 1f;
            if (Health <= 0f) Die(null, cause);
        }

        // ---------- Silt Hands ----------

        // Arms rise from the mud and hold her for 4 s, hurting her every second.
        public void Grip()
        {
            if (IsGripped || hiding || IsDead) return;
            gripTime = GripSeconds;
            gripTick = 0f;
            crouching = false;
            stepSource.PlayOneShot(ProceduralSounds.Squelch, 1f);
            Notify("Hands in the mud have your ankles. Tap E to break free (each pull makes noise).");
        }

        // Held fast: damage each second; tapping E pulls free sooner but each pull is noise.
        private void UpdateGrip()
        {
            gripTime -= Time.deltaTime;
            gripTick -= Time.deltaTime;
            if (gripTick <= 0f)
            {
                gripTick = 1f;
                Hurt(GameSettings.SiltGripDamage, "The silt held you under.");
            }
            if (GameInput.Down(KeyCode.E))
            {
                gripTime -= GripTap;
                NoisePulse.Emit(transform.position, NoisePulse.Struggle, NoiseKind.Struggle);
                stepSource.PlayOneShot(ProceduralSounds.Squelch, 0.7f);
            }
            if (gripTime <= 0f) gripTime = 0f;
        }

        // ---------- Hold-to-use objects ----------

        // Fills the progress bar while the right key is held near a lantern post, door, crate, cabinet, ladder or the boat.
        private void UpdateHold()
        {
            Interactable focus = Interactable.Nearest(transform.position);
            // E is already used for pickups and hiding, so those win when they are in reach.
            if (focus != null && focus.Key == KeyCode.E && (NearbyPickup != null || NearbyPhoto != null || nearbyHideSpot != null)) focus = null;
            FocusedInteractable = focus;

            if (focus == null || !GameInput.Held(focus.Key))
            {
                holding = null;
                HoldProgress = 0f;
                return;
            }
            if (focus != holding)
            {
                holding = focus;
                HoldProgress = 0f;
            }
            HoldProgress += Time.deltaTime / focus.HoldSeconds;
            focus.Holding(this, HoldProgress);
            if (HoldProgress < 1f) return;
            focus.Complete(this);
            holding = null;
            HoldProgress = 0f;
        }

        // Called by a lantern post once it is lit.
        public void SetCheckpoint(LanternPost post)
        {
            Checkpoint = post;
            Notify("Lantern post lit. If he catches you, you'll wake here.");
        }

        // Sends a short message to the HUD.
        public void Notify(string message) => Notified?.Invoke(message);

        // ---------- Tools ----------

        // Keys 1-4 or the scroll wheel pick a tool slot.
        private void SelectTool()
        {
            int slots = SlotCount;
            for (int i = 0; i < slots; i++)
                if (Input.GetKeyDown(KeyCode.Alpha1 + i)) SelectedSlot = i;
            int step = GameInput.ToolStep;
            if (CameraRaised) return; // the wheel is free while the camera is up
            if (step != 0) SelectedSlot = (SelectedSlot + slots + step) % slots;
        }

        // The stack in a slot, or null if the slot is empty.
        public ToolStack SlotStack(int slot) => slot < tools.Count ? tools[slot] : null;

        // Puts tools in her pockets: the same kind stacks, otherwise it needs an empty slot. Returns how many fitted.
        public int GiveTool(Pickup.Kind kind, int count)
        {
            int given = 0;
            ToolStack stack = tools.Find(s => s.Kind == kind);
            if (stack == null)
            {
                if (tools.Count >= SlotCount) return 0;
                stack = new ToolStack { Kind = kind };
                tools.Add(stack);
            }
            while (given < count && stack.Count < Pickup.StackLimit(kind))
            {
                stack.Count++;
                given++;
            }
            return given;
        }

        // Anything picked up: tools go in a slot, flash bulbs go with the camera, salvage goes in the boat bag.
        public bool Take(Pickup.Kind kind)
        {
            if (Pickup.IsSalvage(kind))
            {
                Campaign.AddSalvage(kind);
                Notify($"Salvage: {Pickup.Name(kind)}. Spend it on upgrades at the boat.");
                return true;
            }
            if (kind == Pickup.Kind.FlashBulbs)
            {
                // Design doc: "Flash Bulbs: 3 flash photos with no charging whine."
                Campaign.FlashBulbs += 3;
                Notify("Flash bulbs: 3 flash photos with no charging whine.");
                return true;
            }
            if (GiveTool(kind, 1) == 0) return false;
            Notify($"Picked up {Pickup.Name(kind)}.");
            return true;
        }

        // Q uses the selected tool: throw a bottle or handbell, sniff salts, wrap her boots, or refill the lantern.
        private void UseTool()
        {
            ToolStack stack = SlotStack(SelectedSlot);
            if (stack == null || stack.Count <= 0)
            {
                Notify("Nothing in that slot.");
                return;
            }
            switch (stack.Kind)
            {
                case Pickup.Kind.Salts:
                    if (Health >= 100f && breath >= maxBreath)
                    {
                        Notify("You don't need the salts yet.");
                        return;
                    }
                    Health = Mathf.Min(100f, Health + SaltsHeal);
                    breath = maxBreath;
                    NoisePulse.Emit(transform.position, NoisePulse.Salts, NoiseKind.Salts);
                    stepSource.PlayOneShot(ProceduralSounds.Sniff, 0.8f);
                    break;
                case Pickup.Kind.Wraps:
                    WrapTimeLeft = WrapSeconds;
                    Notify("Oilcloth wrapped round your boots: silent steps for 25 s. No sprinting.");
                    break;
                case Pickup.Kind.LampOil:
                    NellLantern lantern = GetComponent<NellLantern>();
                    if (lantern == null || lantern.Full)
                    {
                        Notify("The lantern is already full.");
                        return;
                    }
                    lantern.Refill();
                    Notify("Lantern refilled. No cranking needed.");
                    break;
                default:
                    Vector3 origin = playerCamera.transform.position + playerCamera.transform.forward * 0.8f;
                    Throwable.Throw(origin, playerCamera.transform.forward, stack.Kind == Pickup.Kind.Handbell);
                    break;
            }
            stack.Count--;
            if (stack.Count <= 0) tools.Remove(stack);
        }

        // ---------- Hiding, pickups, evidence ----------

        // Hiding turns off movement and parks the camera inside the wardrobe (or under the pew, or the jetty).
        private void EnterHideSpot()
        {
            hiding = true;
            CurrentHideSpot = nearbyHideSpot;
            CurrentHideSpot.MarkUsed();
            controller.enabled = false;
            transform.position = CurrentHideSpot.transform.position;
            playerCamera.transform.localPosition = new Vector3(0f, CurrentHideSpot.Noun == "wardrobe" ? 0.65f : -0.4f, 0f);
        }

        // Puts Nell back in the world at the hide spot's exit point.
        private void LeaveHideSpot()
        {
            hiding = false;
            transform.position = CurrentHideSpot != null ? CurrentHideSpot.ExitPoint.position : transform.position + transform.forward;
            controller.enabled = true;
            CurrentHideSpot = null;
        }

        // Adds the pickup to Nell's inventory and removes it from the world.
        private void Collect(Pickup pickup)
        {
            if (pickup.Type == Pickup.Kind.Evidence) CollectEvidence(pickup.Evidence, pickup.transform.position);
            else if (!Take(pickup.Type))
            {
                Notify("Your pockets are full.");
                return; // leave it where it is
            }
            Destroy(pickup.gameObject);
            NearbyPickup = null;
        }

        // A register page, a tape, or a photograph that counted. Kept only if she rows out with it.
        public void CollectEvidence(EvidenceSpot spot, Vector3 where)
        {
            if (spot == null || Campaign.Has(spot.Id)) return;
            Campaign.FoundTonight.Add(spot.Id);
            FoundTonight++;
            Campaign.Guard(new Vector3(where.x, 0f, where.z));
            string what = spot.Type == EvidenceType.Photo ? "Photographed" : spot.Type == EvidenceType.Page ? "Found" : "Found";
            Notify($"{what}: {spot.Title}.");
            EvidenceTaken?.Invoke(where);
        }

        // ---------- Ladders and falls ----------

        // Up or down a rope ladder.
        public void Climb(Vector3 to)
        {
            controller.enabled = false;
            transform.position = to;
            controller.enabled = true;
            verticalVelocity = 0f;
            stepSource.PlayOneShot(ProceduralSounds.Creak, 0.3f);
        }

        // Rotten boards gave way: fall damage and a moment on the floor.
        public void FallThrough(float damage)
        {
            if (IsDead || IsKnockedDown) return;
            Hurt(damage, "The floor gave way.");
            if (IsDead) return;
            Notify("The boards gave way under you. He heard that.");
            StopAllCoroutines();
            StartCoroutine(Stagger());
        }

        // Down in the dark for a second, then back up.
        private IEnumerator Stagger()
        {
            IsKnockedDown = true;
            sprinting = false;
            Quaternion upright = Quaternion.Euler(pitch, 0f, 0f);
            for (float time = 0f; time < 1.4f; time += Time.deltaTime)
            {
                float down = Mathf.Sin(Mathf.Clamp01(time / 1.4f) * Mathf.PI);
                playerCamera.transform.localPosition = new Vector3(0f, Mathf.Lerp(0.75f, -0.6f, down), 0f);
                playerCamera.transform.localRotation = Quaternion.Slerp(upright, Quaternion.Euler(10f, 0f, 25f), down);
                yield return null;
            }
            playerCamera.transform.localRotation = upright;
            IsKnockedDown = false;
        }

        // ---------- Damage, knockdowns, death ----------

        // A non-lethal hit (the Sexton's swipe). Lethal attacks go through Die instead.
        public void TakeDamage(float amount)
        {
            Health = Mathf.Max(0f, Health - amount);
            HurtFlash = 1f;
        }

        // A full grab that doesn't kill: lifted, thrown into the mud, then back on her feet. Called by the Sexton.
        public void KnockDown(float damage, SextonBody attacker)
        {
            if (IsDead) return;
            Health = Mathf.Max(1f, Health - damage);
            HurtFlash = 1f;
            Knockdowns++;
            gripTime = 0f;
            StopAllCoroutines();
            StartCoroutine(KnockdownAnimation(attacker));
        }

        // 0 to 0.5 s: lifted toward his grip. 0.5 to 0.8 s: thrown away from him. Lies stunned, then gets up at 2.6 s.
        private IEnumerator KnockdownAnimation(SextonBody attacker)
        {
            IsKnockedDown = true;
            if (hiding) LeaveHideSpot();
            controller.enabled = false;
            stepSource.PlayOneShot(ProceduralSounds.Knock, 1f);

            Vector3 start = transform.position;
            Vector3 away = transform.position - attacker.transform.position;
            away.y = 0f;
            away = away.sqrMagnitude > 0.01f ? away.normalized : -transform.forward;
            Vector3 landing = start + away * 1.8f;
            // Don't throw her through a wall.
            if (Physics.Linecast(start, landing, out RaycastHit hit, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
                landing = hit.point - away * 0.4f;
            landing.y = start.y;

            Quaternion upright = Quaternion.Euler(pitch, 0f, 0f);
            for (float time = 0f; time < 2.6f; time += Time.deltaTime)
            {
                float lift = Mathf.Sin(Mathf.Clamp01(time / 0.8f) * Mathf.PI);
                float thrown = Mathf.SmoothStep(0f, 1f, (time - 0.5f) / 0.3f);
                float rise = Mathf.SmoothStep(0f, 1f, (time - 2f) / 0.6f);
                transform.position = Vector3.Lerp(start, landing, thrown) + Vector3.up * lift * 0.5f;
                float floor = Mathf.Lerp(0.75f, -0.55f, thrown * (1f - rise));
                playerCamera.transform.localPosition = new Vector3(0f, floor, 0f);
                playerCamera.transform.localRotation = Quaternion.Slerp(upright, Quaternion.Euler(-20f, 0f, 70f), thrown * (1f - rise));
                yield return null;
            }

            playerCamera.transform.localRotation = upright;
            controller.enabled = true;
            IsKnockedDown = false;
        }

        // Called by the Sexton when he grabs Nell (killer set), or by drowning, falls and the silt (killer null).
        public void Die(SextonBody killer, string cause = "The Sexton found you.")
        {
            if (IsDead && DeathFade > 0f) return;
            StopAllCoroutines();
            Health = 0f;
            HoldingBreath = false;
            HurtFlash = 1f;
            IsKnockedDown = false;
            gripTime = 0f;
            DeathCause = cause;
            StartCoroutine(KillAnimation(killer));
        }

        // Nell is pulled out of hiding, turned to face the Sexton, lifted toward his hands and shaken, then the screen goes dark.
        // Without a killer she just sinks and the screen fades.
        private IEnumerator KillAnimation(SextonBody killer)
        {
            if (hiding) LeaveHideSpot();
            controller.enabled = false;
            stepSource.PlayOneShot(ProceduralSounds.Sting, 1f);

            Vector3 start = transform.position;
            Vector3 eye = playerCamera.transform.localPosition;
            for (float time = 0f; time < 2.4f; time += Time.deltaTime)
            {
                if (killer != null)
                {
                    // 0 to 0.8 s: dragged up to his grip. 0.8 to 1.8 s: shaken. Then the fade finishes.
                    float lift = Mathf.SmoothStep(0f, 1f, time / 0.8f);
                    float shake = time > 0.8f && time < 1.8f ? 0.06f : 0.01f;
                    transform.position = Vector3.Lerp(start, killer.GripPoint - eye, lift) + UnityEngine.Random.insideUnitSphere * shake;
                    Vector3 toHead = killer.Head.position - playerCamera.transform.position;
                    Quaternion look = Quaternion.LookRotation(toHead) * Quaternion.Euler(0f, 0f, Mathf.Sin(time * 25f) * shake * 60f);
                    playerCamera.transform.rotation = Quaternion.Slerp(playerCamera.transform.rotation, look, Time.deltaTime * 8f);
                }
                else
                {
                    playerCamera.transform.localPosition = Vector3.Lerp(eye, new Vector3(0f, -0.7f, 0f), time / 2.4f);
                    playerCamera.transform.localRotation = Quaternion.Slerp(playerCamera.transform.localRotation, Quaternion.Euler(30f, 0f, 40f), Time.deltaTime * 2f);
                }
                DeathFade = Mathf.Clamp01((time - 1.5f) / 0.9f);
                yield return null;
            }
            DeathFade = 1f;
        }

        // Restarts Nell at a lantern post with full Vitality and Breath. Evidence and tools are kept.
        public void Respawn(Vector3 position, Quaternion facing)
        {
            StopAllCoroutines();
            Health = 100f;
            breath = maxBreath;
            DeathFade = HurtFlash = HoldProgress = 0f;
            gaspCooldown = gripTime = 0f;
            IsKnockedDown = hiding = crouching = false;
            Underwater = false;
            CurrentHideSpot = null;
            holding = null;
            controller.enabled = false;
            transform.SetPositionAndRotation(position, Quaternion.Euler(0f, facing.eulerAngles.y, 0f));
            controller.enabled = true;
            pitch = 0f;
            playerCamera.transform.localPosition = new Vector3(0f, 0.75f, 0f);
            playerCamera.transform.localRotation = Quaternion.identity;
        }

        // ---------- Triggers ----------

        // Hide spot triggers tell the player when one is in reach.
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
