using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Hushwater
{
    public sealed class HudController : MonoBehaviour
    {
        private const string Controls = "WASD move | Shift sprint | Ctrl/C crouch | Space hold breath | E hide | Q throw bottle";

        private PlayerController player;
        private SextonController sexton;
        private Text status;
        private Text prompt;
        private Text gameOver;
        private RectTransform healthFill;
        private RectTransform breathFill;

        public void Initialize(PlayerController target, SextonController enemy)
        {
            player = target;
            sexton = enemy;
            Canvas canvas = new GameObject("HUD Canvas").AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            CanvasScaler scaler = canvas.gameObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);

            MakeText(canvas.transform, "HEALTH", 16, new Vector2(0f, 1f), new Vector2(30f, -24f), TextAnchor.UpperLeft);
            healthFill = MakeBar(canvas.transform, new Vector2(30f, -50f), new Color(0.65f, 0.15f, 0.12f));
            MakeText(canvas.transform, "BREATH", 16, new Vector2(0f, 1f), new Vector2(30f, -72f), TextAnchor.UpperLeft);
            breathFill = MakeBar(canvas.transform, new Vector2(30f, -98f), new Color(0.25f, 0.6f, 0.65f));
            status = MakeText(canvas.transform, Controls, 18, new Vector2(0f, 0f), new Vector2(30f, 24f), TextAnchor.LowerLeft);
            prompt = MakeText(canvas.transform, "", 24, new Vector2(0.5f, 0.5f), new Vector2(0f, -80f), TextAnchor.MiddleCenter);
            gameOver = MakeText(canvas.transform, "", 48, new Vector2(0.5f, 0.5f), Vector2.zero, TextAnchor.MiddleCenter);
        }

        private void Update()
        {
            if (player == null) return;
            healthFill.anchorMax = new Vector2(player.Health / 100f, 1f);
            breathFill.anchorMax = new Vector2(player.BreathNormalized, 1f);

            string mode = player.IsHiding ? "HIDDEN" : player.IsCrouching ? "CROUCHING" : "";
            status.text = $"{Controls}\nBottles: {player.Bottles}   {mode}   Sexton: {sexton.StateName}";
            prompt.text = player.IsHiding ? "E to leave the wardrobe" : player.NearHideSpot ? "E to hide in the wardrobe" : "";

            if (player.IsDead)
            {
                gameOver.text = "The Sexton found you.\nPress R to try again";
                Cursor.lockState = CursorLockMode.None;
                Cursor.visible = true;
                if (Input.GetKeyDown(KeyCode.R)) SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
            }
        }

        private static Text MakeText(Transform parent, string value, int size, Vector2 anchor, Vector2 position, TextAnchor alignment)
        {
            Text text = new GameObject("Text").AddComponent<Text>();
            text.transform.SetParent(parent, false);
            text.text = value;
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.fontSize = size;
            text.alignment = alignment;
            RectTransform rect = text.rectTransform;
            rect.anchorMin = rect.anchorMax = rect.pivot = anchor;
            rect.anchoredPosition = position;
            rect.sizeDelta = new Vector2(1400f, 120f);
            return text;
        }

        private static RectTransform MakeBar(Transform parent, Vector2 position, Color color)
        {
            Image background = new GameObject("Bar").AddComponent<Image>();
            background.transform.SetParent(parent, false);
            background.color = new Color(0f, 0f, 0f, 0.6f);
            RectTransform rect = background.rectTransform;
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0f, 1f);
            rect.anchoredPosition = position;
            rect.sizeDelta = new Vector2(260f, 16f);

            Image fill = new GameObject("Fill").AddComponent<Image>();
            fill.transform.SetParent(background.transform, false);
            fill.color = color;
            fill.rectTransform.anchorMin = Vector2.zero;
            fill.rectTransform.anchorMax = Vector2.one;
            fill.rectTransform.offsetMin = fill.rectTransform.offsetMax = Vector2.zero;
            return fill.rectTransform;
        }
    }
}
