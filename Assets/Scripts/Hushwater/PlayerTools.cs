using System.Collections;
using UnityEngine;

namespace Hushwater
{
    // "Shoot": the survey camera. 12 exposures a night; the flash gives a clear shot at the cost of noise.
    public sealed class SurveyCamera : MonoBehaviour
    {
        private const float NormalFov = 62f;
        private const float ZoomFov = 40f;

        private PlayerController player;
        private Camera view;
        private Transform viewmodel;
        private Vector3 restPosition = new Vector3(0.32f, -0.34f, 0.55f);
        private Vector3 raisedPosition = new Vector3(0f, -0.07f, 0.24f);
        private Light flashLight;
        private bool busy;

        public int Exposures { get; private set; } = 12;
        public bool FlashOn { get; private set; } = true;
        public bool Raised { get; private set; }
        public float WhineProgress { get; private set; }
        public EvidenceSite InFrame { get; private set; }

        public void Setup(PlayerController owner)
        {
            player = owner;
            view = owner.View;
            viewmodel = new GameObject("Survey camera (held)").transform;
            viewmodel.SetParent(view.transform, false);
            viewmodel.localPosition = restPosition;
            GameObject model = HushwaterAssets.Model("Camera_01", viewmodel, viewmodel.position, 0f, 0.11f, false, 0.45f);
            model.transform.SetParent(viewmodel, true);
            model.transform.localRotation = Quaternion.Euler(0f, 180f, 0f);
            model.transform.localPosition = Vector3.zero;
            foreach (Renderer r in model.GetComponentsInChildren<Renderer>()) r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

            flashLight = new GameObject("Flash").AddComponent<Light>();
            flashLight.transform.SetParent(view.transform, false);
            flashLight.type = LightType.Spot;
            flashLight.spotAngle = 85f;
            flashLight.range = 16f;
            flashLight.intensity = 0f;
            flashLight.color = new Color(0.92f, 0.96f, 1f);
            flashLight.enabled = false;
        }

        public void ResetNight() => Exposures = 12;

        private void Update()
        {
            bool canUse = player.CanAct;
            Raised = canUse && Input.GetMouseButton(1);
            view.fieldOfView = Mathf.Lerp(view.fieldOfView, Raised ? ZoomFov : NormalFov, Time.deltaTime * 9f);
            viewmodel.localPosition = Vector3.Lerp(viewmodel.localPosition, Raised ? raisedPosition : restPosition, Time.deltaTime * 12f);
            viewmodel.gameObject.SetActive(!player.IsHiding);
            InFrame = Raised ? FindEvidence() : null;
            if (!canUse) return;
            if (Input.GetKeyDown(KeyCode.R))
            {
                FlashOn = !FlashOn;
                SoundFx.Play2D(ProceduralAudio.UiTick, 0.4f, FlashOn ? 1.2f : 0.8f);
            }
            if (Raised && Input.GetMouseButtonDown(0) && !busy)
            {
                if (Exposures <= 0)
                {
                    NightDirector.Instance.Caption("No exposures left tonight", 2f);
                    return;
                }
                StartCoroutine(TakePhoto());
            }
        }

        private EvidenceSite FindEvidence()
        {
            EvidenceSite best = null;
            float bestScore = float.MaxValue;
            foreach (EvidenceSite site in NightDirector.Instance.Evidence)
            {
                if (site == null || site.Found) continue;
                Vector3 focus = site.FocusPoint;
                float distance = Vector3.Distance(view.transform.position, focus);
                if (distance > site.Range) continue;
                Vector3 vp = view.WorldToViewportPoint(focus);
                if (vp.z <= 0f || vp.x < 0.22f || vp.x > 0.78f || vp.y < 0.18f || vp.y > 0.82f) continue;
                if (Physics.Linecast(view.transform.position, focus, out RaycastHit hit, ~(1 << 2), QueryTriggerInteraction.Ignore)
                    && hit.distance < distance - 0.6f) continue;
                float score = Vector2.Distance(new Vector2(vp.x, vp.y), new Vector2(0.5f, 0.5f)) + distance * 0.02f;
                if (score < bestScore) { bestScore = score; best = site; }
            }
            return best;
        }

        private IEnumerator TakePhoto()
        {
            busy = true;
            bool flash = FlashOn;
            if (flash)
            {
                // The flash whines as it charges.
                AudioSource whine = SoundFx.PlayAt(ProceduralAudio.FlashWhine, view.transform.position, 0.35f, 10f);
                whine.transform.SetParent(view.transform, true);
                for (float t = 0f; t < 1f; t += Time.deltaTime / 1.1f)
                {
                    WhineProgress = t;
                    if (!Raised) { WhineProgress = 0f; busy = false; Destroy(whine.gameObject); yield break; }
                    yield return null;
                }
                WhineProgress = 0f;
            }

            EvidenceSite site = FindEvidence();
            Exposures--;
            SoundFx.Play2D(ProceduralAudio.Shutter, 0.8f);
            if (flash)
            {
                SoundFx.Play2D(ProceduralAudio.FlashPop, 0.5f);
                flashLight.enabled = true;
                flashLight.intensity = 6f;
                player.EmitNoise(view.transform.position, NoisePulse.Flash, NoiseKind.Flash);
            }
            else player.EmitNoise(view.transform.position, NoisePulse.PhotoNoFlash, NoiseKind.Photo);

            RenderTexture photo = Capture();
            bool blurry = site != null && site.Dark && !flash;
            NightDirector.Instance.OnPhotograph(site, photo, blurry, flash);

            if (flash)
            {
                ScreenGrade grade = view.GetComponent<ScreenGrade>();
                if (grade != null) grade.Flash = 1.1f;
                yield return new WaitForSeconds(0.08f);
                flashLight.intensity = 0f;
                flashLight.enabled = false;
            }
            yield return new WaitForSeconds(0.6f);
            busy = false;
        }

        private RenderTexture Capture()
        {
            RenderTexture rt = new RenderTexture(512, 384, 24, RenderTextureFormat.ARGB32) { name = "Photograph" };
            bool hadViewmodel = viewmodel.gameObject.activeSelf;
            viewmodel.gameObject.SetActive(false);
            PlayerLantern lantern = GetComponent<PlayerLantern>();
            lantern?.SetViewmodelVisible(false);
            float fov = view.fieldOfView;
            view.fieldOfView = ZoomFov;
            view.targetTexture = rt;
            view.Render();
            view.targetTexture = null;
            view.fieldOfView = fov;
            viewmodel.gameObject.SetActive(hadViewmodel);
            lantern?.SetViewmodelVisible(true);
            return rt;
        }
    }

    // Nell's oil lantern. Cranking it (F) refills the charge but carries 12 m.
    public sealed class PlayerLantern : MonoBehaviour
    {
        private PlayerController player;
        private Transform viewmodel;
        private Light glow;
        private Renderer flame;
        private float noiseTimer;
        private AudioSource crank;

        public float Charge { get; private set; } = 85f;
        public bool Cranking { get; private set; }

        public void Setup(PlayerController owner)
        {
            player = owner;
            viewmodel = new GameObject("Lantern (held)").transform;
            viewmodel.SetParent(owner.View.transform, false);
            viewmodel.localPosition = new Vector3(-0.36f, -0.42f, 0.62f);
            GameObject lamp = HushwaterAssets.Model("vintage_oil_lamp", viewmodel, viewmodel.position, 0f, 0.3f, false, 0.4f);
            lamp.transform.SetParent(viewmodel, true);
            lamp.transform.localRotation = Quaternion.identity;
            foreach (Renderer r in lamp.GetComponentsInChildren<Renderer>()) r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

            GameObject flameObject = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            Destroy(flameObject.GetComponent<Collider>());
            flameObject.transform.SetParent(viewmodel, false);
            flameObject.transform.localPosition = new Vector3(0f, 0.15f, 0f);
            flameObject.transform.localScale = new Vector3(0.025f, 0.045f, 0.025f);
            flame = flameObject.GetComponent<Renderer>();
            flame.sharedMaterial = HushwaterAssets.Emissive(Color.black, new Color(5f, 2.8f, 1f));
            flame.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

            glow = new GameObject("Lantern light").AddComponent<Light>();
            glow.transform.SetParent(viewmodel, false);
            glow.transform.localPosition = new Vector3(0.1f, 0.35f, 0.15f);
            glow.type = LightType.Point;
            glow.color = new Color(1f, 0.7f, 0.38f);
            glow.shadows = LightShadows.Soft;
            glow.shadowNearPlane = 0.3f;
            crank = SoundFx.Loop(gameObject, ProceduralAudio.Crank, 0.45f, 15f, false);
            crank.Stop();
        }

        public void SetViewmodelVisible(bool visible)
        {
            foreach (Renderer r in viewmodel.GetComponentsInChildren<Renderer>()) r.enabled = visible && (r != flame || Charge > 0f);
        }

        public void ResetNight() => Charge = 85f;

        private void Update()
        {
            float dt = Time.deltaTime;
            Cranking = player.CanAct && Input.GetKey(KeyCode.F) && Charge < 100f;
            if (Cranking)
            {
                Charge = Mathf.Min(100f, Charge + dt * 20f);
                noiseTimer -= dt;
                if (noiseTimer <= 0f)
                {
                    noiseTimer = 1f;
                    player.EmitNoise(transform.position, NoisePulse.LanternCrank, NoiseKind.Lantern);
                }
                if (!crank.isPlaying) crank.Play();
            }
            else
            {
                noiseTimer = 0f;
                if (crank.isPlaying) crank.Stop();
                Charge = Mathf.Max(0f, Charge - dt * 0.28f);
            }

            float level = Charge / 100f;
            float flicker = 0.85f + 0.3f * Mathf.PerlinNoise(Time.time * 8f, 0.5f);
            glow.enabled = Charge > 0f;
            glow.intensity = Mathf.Lerp(0.25f, 1.35f, level) * flicker;
            glow.range = Mathf.Lerp(4f, 10f, level);
            flame.enabled = Charge > 0f && !player.IsHiding;
            viewmodel.gameObject.SetActive(!player.IsHiding);

            // Sway with movement; the lamp lifts while cranking.
            float sway = Mathf.Sin(Time.time * 1.7f) * 0.008f;
            Vector3 target = new Vector3(-0.36f, Cranking ? -0.32f : -0.42f, 0.62f) + new Vector3(sway, Mathf.Abs(sway), 0f);
            viewmodel.localPosition = Vector3.Lerp(viewmodel.localPosition, target, dt * 8f);
            viewmodel.localRotation = Quaternion.Euler(0f, 0f, Cranking ? Mathf.Sin(Time.time * 20f) * 3f : 0f);
        }
    }
}
