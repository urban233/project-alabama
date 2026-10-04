using System;
using System.Collections;
using System.IO;
using System.Linq;
using Alabama.Districts;
using UnityEngine;

namespace Alabama.Driving
{
    /// <summary>Opt-in native screenshots of reviewed signs and roadside panes.</summary>
    public sealed class NfsWorldMapReview : MonoBehaviour
    {
        [Serializable] public sealed class View { public string label; public Vector3 position; public Vector3 target; }
        [Serializable] public sealed class Plan { public View[] views; }
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Create()
        {
            if (!Application.isEditor && Environment.GetCommandLineArgs().Contains("-nfs-map-review"))
                new GameObject("Map readability review").AddComponent<NfsWorldMapReview>();
        }
        private IEnumerator Start()
        {
            var args = Environment.GetCommandLineArgs();
            string Argument(string key) => args[Array.IndexOf(args, key) + 1];
            string output = Argument("-nfs-stability-output");
            var plan = JsonUtility.FromJson<Plan>(File.ReadAllText(Argument("-nfs-map-review")));
            while (DistrictRuntime.Instance == null || !DistrictRuntime.Instance.Ready) yield return null;
            Application.runInBackground = true; Application.targetFrameRate = 30;
            Screen.SetResolution(1920, 1080, FullScreenMode.Windowed);
            Time.captureDeltaTime = 1f / 30;
            var car = DistrictRuntime.Instance.Player;
            car.GetComponent<VehicleInput>().enabled = false; car.enabled = false; car.Body.isKinematic = true;
            var camera = Camera.main; camera.GetComponent<ChaseCamera>().enabled = false;
            foreach (var view in plan.views)
            {
                string directory = Path.Combine(output, view.label); Directory.CreateDirectory(directory);
                camera.transform.position = view.position; camera.transform.LookAt(view.target);
                foreach (var culling in FindObjectsByType<NfsWorldDistanceCulling>(FindObjectsSortMode.None)) culling.Refresh();
                FindFirstObjectByType<NfsWorldMeshLods>()?.Refresh(camera.transform.position);
                for (int frame = -12; frame < 12; frame++)
                {
                    if (frame >= 0) camera.transform.position = view.position + camera.transform.right * frame * .03f;
                    yield return new WaitForEndOfFrame();
                    if (frame >= 0)
                    {
                        var texture = ScreenCapture.CaptureScreenshotAsTexture();
                        File.WriteAllBytes(Path.Combine(directory, frame.ToString("D2") + ".png"), texture.EncodeToPNG());
                        Destroy(texture);
                    }
                    yield return null;
                }
            }
            File.WriteAllText(Path.Combine(output, "complete.json"), JsonUtility.ToJson(plan, true));
            Application.Quit();
        }
    }
}
