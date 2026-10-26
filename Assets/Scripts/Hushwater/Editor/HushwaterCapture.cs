using System.IO;
using UnityEditor;
using UnityEngine;

namespace Hushwater.EditorTools
{
    // Builds Millrow in edit mode and renders reference shots. Used for headless checks:
    // Unity -batchmode -executeMethod Hushwater.EditorTools.HushwaterCapture.Run -captureDir <dir> -quit
    public static class HushwaterCapture
    {
        private struct Shot
        {
            public string Name;
            public Vector3 Position;
            public Vector3 Target;
            public float Fov;
            public bool Lantern;
        }

        [MenuItem("Hushwater/Capture Reference Shots")]
        public static void Run()
        {
            string dir = ArgValue("-captureDir") ?? Path.Combine(Application.dataPath, "../Captures");
            Directory.CreateDirectory(dir);

            foreach (GameObject prefab in Resources.LoadAll<GameObject>("Hushwater/Models"))
            {
                GameObject probe = HushwaterAssets.Model(prefab.name, null, Vector3.zero, 0f, 0f, false);
                Debug.Log($"HWSIZE {prefab.name} {HushwaterAssets.LocalBounds(probe).size.ToString("F3")}");
                Object.DestroyImmediate(probe);
            }
            Transform root = new GameObject("Capture World").transform;
            MillrowBuilder.Build(root);
            Transform sexton = new GameObject("Sexton").transform;
            sexton.SetParent(root, false);
            sexton.position = new Vector3(-1f, MillrowBuilder.GroundHeight(-1f, 9f), 9f);
            sexton.rotation = Quaternion.Euler(0f, 170f, 0f);
            SextonBody body = SextonBody.Build(sexton);
            body.Animate(1.2f, SextonController.State.Patrol, false, 0.3f);
            foreach (ReflectionProbe probe in root.GetComponentsInChildren<ReflectionProbe>()) probe.RenderProbe();
            foreach (Renderer r in root.GetComponentsInChildren<Renderer>())
            {
                if (r.bounds.size.magnitude > 14f && !(r is ParticleSystemRenderer))
                    Debug.Log($"HWBIG {r.transform.parent?.name}/{r.name} size={r.bounds.size} centre={r.bounds.center}");
            }

            Shot[] shots =
            {
                new Shot { Name = "01_jetty", Position = new Vector3(0.3f, 2.0f, -40f), Target = new Vector3(0f, 2.5f, 0f), Fov = 62f, Lantern = true },
                new Shot { Name = "02_street", Position = new Vector3(1.2f, 1.65f, -6f), Target = new Vector3(-1f, 1.8f, 12f), Fov = 62f, Lantern = true },
                new Shot { Name = "03_menu", Position = new Vector3(1f, 2.4f, -12f), Target = new Vector3(-3f, 6f, 42f), Fov = 62f },
                new Shot { Name = "04_set_table", Position = new Vector3(-7.2f, 1.75f, 3.2f), Target = new Vector3(-9.0f, 1.0f, 4.4f), Fov = 62f, Lantern = true },
                new Shot { Name = "05_chapel", Position = new Vector3(2f, 1.7f, 26f), Target = new Vector3(-2f, 5f, 44f), Fov = 62f, Lantern = true },
                new Shot { Name = "06_sexton", Position = new Vector3(0.5f, 1.6f, 3.5f), Target = new Vector3(-1f, 1.5f, 9f), Fov = 55f, Lantern = true },
                new Shot { Name = "07_overview", Position = new Vector3(24f, 22f, -30f), Target = new Vector3(0f, 0f, 15f), Fov = 55f },
                new Shot { Name = "08_pews", Position = new Vector3(1f, 1.7f, 37f), Target = new Vector3(-2f, 1f, 48f), Fov = 62f, Lantern = true },
            };

            foreach (Shot shot in shots)
            {
                Camera cam = NightDirector.MakeCamera("Capture camera", root);
                cam.transform.SetPositionAndRotation(shot.Position, Quaternion.LookRotation(shot.Target - shot.Position));
                cam.fieldOfView = shot.Fov;
                Light lantern = null;
                if (shot.Lantern)
                {
                    lantern = new GameObject("Lantern").AddComponent<Light>();
                    lantern.transform.SetParent(cam.transform, false);
                    lantern.transform.localPosition = new Vector3(-0.3f, -0.3f, 0.5f);
                    lantern.type = LightType.Point;
                    lantern.color = new Color(1f, 0.7f, 0.38f);
                    lantern.intensity = 1.25f;
                    lantern.range = 9.5f;
                    lantern.shadows = LightShadows.Soft;
                }
                RenderTexture rt = new RenderTexture(1600, 900, 24, RenderTextureFormat.ARGB32);
                cam.targetTexture = rt;
                cam.Render();
                RenderTexture.active = rt;
                Texture2D image = new Texture2D(1600, 900, TextureFormat.RGB24, false);
                image.ReadPixels(new Rect(0, 0, 1600, 900), 0, 0);
                image.Apply();
                RenderTexture.active = null;
                File.WriteAllBytes(Path.Combine(dir, shot.Name + ".png"), image.EncodeToPNG());
                cam.targetTexture = null;
                Object.DestroyImmediate(cam.gameObject);
                Object.DestroyImmediate(image);
                rt.Release();
            }
            Object.DestroyImmediate(root.gameObject);
            Debug.Log("Hushwater capture written to " + dir);
        }

        private static string ArgValue(string name)
        {
            string[] args = System.Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length - 1; i++)
                if (args[i] == name) return args[i + 1];
            return null;
        }
    }
}
