using UnityEngine;
using UnityEngine.UI;

namespace Hushwater
{
    // F3 toggles the design doc's "Noise Debug View": a ring on the ground for every noise pulse
    // (rust if the Sexton heard it, grey if not), his NavMesh path, and his current state.
    // This is a developer tool, so unlike the HUD it is allowed to show where he is.
    public sealed class NoiseDebugView : MonoBehaviour
    {
        private const int RingCount = 12;
        private const int Segments = 48;

        private SextonController sexton;
        private Text readout;
        private LineRenderer path;
        private readonly LineRenderer[] rings = new LineRenderer[RingCount];
        private readonly float[] ringAge = new float[RingCount];
        private int nextRing;
        private bool visible;
        private string lastNoise = "none yet";

        // Creates the line renderers and the text readout, hidden until F3 is pressed.
        public void Initialize(SextonController enemy, Transform canvas)
        {
            sexton = enemy;
            Material lines = new Material(Shader.Find("Sprites/Default"));
            for (int i = 0; i < RingCount; i++)
            {
                rings[i] = MakeLine("Noise Ring", lines, Segments + 1);
                ringAge[i] = 99f;
            }
            path = MakeLine("Sexton Path", lines, 0);
            path.startColor = path.endColor = UiKit.Lake;
            readout = UiKit.Text(canvas, "", 16, new Vector2(0f, 1f), new Vector2(36f, -150f), TextAnchor.UpperLeft, new Vector2(620f, 120f), UiKit.Lantern);
            sexton.Listened += OnListened;
            SetVisible(false);
        }

        // Stop listening when the scene unloads.
        private void OnDestroy()
        {
            if (sexton != null) sexton.Listened -= OnListened;
        }

        // F3 toggles; while visible, rings fade and the path and readout follow him.
        private void Update()
        {
            if (Input.GetKeyDown(KeyCode.F3)) SetVisible(!visible);
            if (!visible || sexton == null) return;
            if (sexton.WashedAway || !sexton.Agent.enabled)
            {
                path.positionCount = 0;
                readout.text = "SEXTON: swept away by the spillway";
                return;
            }

            for (int i = 0; i < RingCount; i++)
            {
                ringAge[i] += Time.deltaTime;
                float alpha = Mathf.Clamp01(1f - ringAge[i] / 2.5f);
                Color color = rings[i].startColor;
                rings[i].startColor = rings[i].endColor = new Color(color.r, color.g, color.b, alpha);
            }

            Vector3[] corners = sexton.Agent.hasPath ? sexton.Agent.path.corners : new Vector3[0];
            path.positionCount = corners.Length;
            for (int i = 0; i < corners.Length; i++) path.SetPosition(i, corners[i] + Vector3.up * 0.1f);

            readout.text = $"SEXTON: {sexton.StateName}   hearing ×{GameSettings.HearingMultiplier:0.##}   path {sexton.Agent.pathStatus}\n" +
                           $"Last pulse: {lastNoise}";
        }

        // Draws a ring at the noise's effective reach (radius × hearing × wall muffling).
        private void OnListened(NoiseEvent noise, bool heard, int walls, float reach)
        {
            if (!visible) return;
            LineRenderer ring = rings[nextRing];
            ringAge[nextRing] = 0f;
            nextRing = (nextRing + 1) % RingCount;
            ring.startColor = ring.endColor = heard ? UiKit.Rust : new Color(0.6f, 0.6f, 0.6f);
            for (int s = 0; s <= Segments; s++)
            {
                float angle = s * Mathf.PI * 2f / Segments;
                ring.SetPosition(s, noise.Position + new Vector3(Mathf.Cos(angle) * reach, 0.1f - noise.Position.y, Mathf.Sin(angle) * reach));
            }
            string wallText = walls == 0 ? "" : walls == 1 ? ", 1 wall" : $", {walls} walls";
            lastNoise = $"{NoisePulse.Label(noise.Kind)} · {noise.Radius:0} m{wallText} → {(heard ? "HEARD" : "not heard")}";
        }

        // Shows or hides everything at once.
        private void SetVisible(bool show)
        {
            visible = show;
            readout.enabled = show;
            path.enabled = show;
            foreach (LineRenderer ring in rings) ring.enabled = show;
        }

        // A thin world-space line renderer.
        private LineRenderer MakeLine(string name, Material material, int points)
        {
            LineRenderer line = new GameObject(name).AddComponent<LineRenderer>();
            line.transform.SetParent(transform, false);
            line.sharedMaterial = material;
            line.widthMultiplier = 0.06f;
            line.positionCount = points;
            line.useWorldSpace = true;
            line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            return line;
        }
    }
}
