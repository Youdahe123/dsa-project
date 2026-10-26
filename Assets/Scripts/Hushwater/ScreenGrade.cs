using UnityEngine;

namespace Hushwater
{
    // Full-screen grade for the built-in pipeline: tonemapping, palette, vignette, grain and hurt/flash pulses.
    [ExecuteAlways]
    [RequireComponent(typeof(Camera))]
    public sealed class ScreenGrade : MonoBehaviour
    {
        public float Damage;
        public float Breath;
        public float Flash;
        private Material material;

        private void OnRenderImage(RenderTexture source, RenderTexture destination)
        {
            if (material == null)
            {
                Shader shader = Shader.Find("Hidden/Hushwater/Grade");
                if (shader == null) { Graphics.Blit(source, destination); return; }
                material = new Material(shader) { hideFlags = HideFlags.HideAndDontSave };
            }
            material.SetFloat("_Exposure", 1.7f * GameSettings.Brightness);
            material.SetFloat("_Vignette", 0.55f);
            material.SetFloat("_Grain", 0.016f);
            material.SetFloat("_Bloom", 0.55f);
            material.SetFloat("_Damage", Damage);
            material.SetFloat("_Breath", Breath);
            material.SetFloat("_Flash", Flash);
            material.SetFloat("_Seed", Time.realtimeSinceStartup % 100f);
            Graphics.Blit(source, destination, material);
        }

        private void Update()
        {
            if (!Application.isPlaying) return;
            Damage = Mathf.MoveTowards(Damage, 0f, Time.unscaledDeltaTime * 0.8f);
            Flash = Mathf.MoveTowards(Flash, 0f, Time.unscaledDeltaTime * 4f);
        }

        private void OnDestroy()
        {
            if (material != null) DestroyImmediate(material);
        }
    }
}
