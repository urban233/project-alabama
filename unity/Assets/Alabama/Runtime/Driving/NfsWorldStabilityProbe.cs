using System;
using System.Collections;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace Alabama.Driving
{
    /// <summary>Explicitly requested, deterministic native-player visual isolation. Never runs in normal play.</summary>
    public sealed class NfsWorldStabilityProbe : MonoBehaviour
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Create()
        {
            if (!Application.isEditor && Environment.GetCommandLineArgs().Contains("-nfs-stability-probe"))
                new GameObject("Visual stability probe").AddComponent<NfsWorldStabilityProbe>();
        }

        private IEnumerator Start()
        {
            var args = Environment.GetCommandLineArgs();
            int outputIndex = Array.IndexOf(args, "-nfs-stability-output");
            if (outputIndex < 0 || outputIndex + 1 >= args.Length || !Path.IsPathRooted(args[outputIndex + 1]))
                throw new ArgumentException("Stability capture needs an absolute -nfs-stability-output directory.");
            string output = args[outputIndex + 1];
            var runtime = Alabama.Districts.DistrictRuntime.Instance;
            while (runtime != null && !runtime.Ready)
            {
                if (runtime.LastFailure != null) throw new InvalidOperationException(runtime.LastFailure);
                yield return null;
            }
            Application.runInBackground = true;
            Application.targetFrameRate = 30;
            Screen.SetResolution(1920, 1080, FullScreenMode.Windowed);
            var car = FindFirstObjectByType<ArcadeCarController>();
            car.GetComponent<VehicleInput>().enabled = false;
            car.enabled = false;
            car.Body.isKinematic = true;
            var camera = Camera.main;
            camera.GetComponent<ChaseCamera>().enabled = false;
            var cameraOrigin = camera.transform.position;
            var carOrigin = car.transform.position;
            var rotation = camera.transform.rotation;
            var direction = car.transform.forward;
            var pipeline = (UniversalRenderPipelineAsset)QualitySettings.renderPipeline;
            var renderer = (UniversalRendererData)pipeline.rendererDataList[0];
            var priming = renderer.depthPrimingMode;
            var aoFeatures = renderer.rendererFeatures.Where(f => f.GetType().Name == "ScreenSpaceAmbientOcclusion").ToArray();
            var aoActive = aoFeatures.Select(f => f.isActive).ToArray();
            var sun = RenderSettings.sun;
            var shadows = sun.shadows;
            bool driving = args.Contains("-nfs-stability-driving");
            Time.captureDeltaTime = 1f / 30;
            var carRotation = car.transform.rotation;
            foreach (string variant in driving ? new[] { "current", "current-repeat", "no-shadows", "no-occlusion", "no-priming" } :
                new[] { "baseline", "no-ao", "no-shadows", "no-occlusion", "no-priming" })
            {
                camera.useOcclusionCulling = variant != "no-occlusion";
                sun.shadows = variant == "no-shadows" ? LightShadows.None : shadows;
                renderer.depthPrimingMode = variant == "no-priming" ? DepthPrimingMode.Disabled :
                    !driving ? DepthPrimingMode.Forced : priming;
                for (int index = 0; index < aoFeatures.Length; index++)
                    aoFeatures[index].SetActive(driving ? aoActive[index] : variant != "no-ao");
                renderer.SetDirty();
                string folder = Path.Combine(output, variant); Directory.CreateDirectory(folder);
                using var poses = new StreamWriter(Path.Combine(folder, "poses.csv"));
                poses.WriteLine("frame,carX,carY,carZ,cameraX,cameraY,cameraZ,speedMetresPerSecond,depthPriming,aoActive");
                car.transform.SetPositionAndRotation(carOrigin, carRotation);
                car.Body.isKinematic = !driving;
                car.enabled = driving;
                var chase = camera.GetComponent<ChaseCamera>(); chase.enabled = driving;
                if (driving)
                {
                    car.Body.linearVelocity = Vector3.zero; car.Body.angularVelocity = Vector3.zero;
                    car.SetCommand(new VehicleCommand(0, 0, 0, true));
                    Physics.SyncTransforms(); chase.SnapToTarget();
                }
                for (int frame = -30; frame < (driving ? 120 : 40); frame++)
                {
                    if (driving) car.SetCommand(new VehicleCommand(frame >= 8 ? .35f : 0, 0, 0, frame < 8));
                    else
                    {
                        var offset = direction * Mathf.Max(0, frame - 7) * .20f;
                        car.transform.position = carOrigin + offset;
                        camera.transform.SetPositionAndRotation(cameraOrigin + offset, rotation);
                    }
                    FindFirstObjectByType<NfsWorldDistanceCulling>()?.Refresh();
                    FindFirstObjectByType<NfsWorldMeshLods>()?.Refresh(camera.transform.position);
                    yield return new WaitForEndOfFrame();
                    if (frame >= 0)
                    {
                        var p = car.transform.position; var c = camera.transform.position;
                        poses.WriteLine(FormattableString.Invariant($"{frame},{p.x},{p.y},{p.z},{c.x},{c.y},{c.z},{car.SpeedMetresPerSecond},{renderer.depthPrimingMode},{aoFeatures.Any(f => f.isActive)}"));
                        var texture = ScreenCapture.CaptureScreenshotAsTexture();
                        File.WriteAllBytes(Path.Combine(folder, frame.ToString("D3") + ".png"), texture.EncodeToPNG());
                        Destroy(texture);
                    }
                    yield return null;
                }
            }
            File.WriteAllText(Path.Combine(output, "complete.txt"), driving ?
                "Five native variants; current and current-repeat use the built renderer settings. Physics acceleration from rest and real chase camera; fixed simulation step. Not a performance benchmark." :
                "Five native variants; eight stationary frames then 6.4 metres of scripted camera/car motion. Not a physics or performance benchmark.");
            Application.Quit();
        }
    }
}
