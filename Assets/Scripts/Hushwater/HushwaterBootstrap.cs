using UnityEngine;

namespace Hushwater
{
    // Press Play in any scene: the director builds Millrow, the menus and the HUD at runtime.
    public sealed class HushwaterBootstrap : MonoBehaviour
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void StartGame()
        {
            HushwaterBootstrap existing = FindAnyObjectByType<HushwaterBootstrap>();
            GameObject host = existing != null ? existing.gameObject : new GameObject("Hushwater Runtime", typeof(HushwaterBootstrap));
            if (host.GetComponent<NightDirector>() == null) host.AddComponent<NightDirector>();
        }
    }
}
