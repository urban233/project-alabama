using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEngine.Rendering;
using Unity.Profiling;
using UnityEngine.Rendering.Universal;

namespace Alabama.Driving
{
    /// <summary>Opt-in rendered-player sampling of representative district viewpoints.</summary>
    public sealed class NfsWorldBenchmark : MonoBehaviour
    {
        [Serializable] private sealed class View
        {
            public string name;
            public Vector3 position;
            public int frames;
            public float p50Milliseconds;
            public float p95Milliseconds;
            public float p99Milliseconds;
            public float meanFps;
            public double gpuP95Milliseconds;
            public int gpuSamples;
            public long drawCalls;
            public long renderedTriangles;
            public long setPassCalls;
        }
        [Serializable] private sealed class Report
        {
            public string graphicsDevice;
            public string processor;
            public int width;
            public int height;
            public float renderScale;
            public int internalWidth;
            public int internalHeight;
            public string upscaling;
            public int cameraRenders;
            public long allocatedMemoryBytes;
            public View[] views;
            public string method;
            public bool renderingCountersAvailable;
            public string scene;
        }

        private int cameraRenders;
        private IEnumerator Start()
        {
            if (Application.isEditor || !Environment.GetCommandLineArgs().Contains("-nfs-benchmark")) yield break;
            var arguments = Environment.GetCommandLineArgs();
            bool diagnosticNoShadows = arguments.Contains("-nfs-diagnostic-no-shadows");
            bool diagnosticHardShadows = arguments.Contains("-nfs-diagnostic-hard-shadows");
            if (diagnosticNoShadows)
                foreach (var light in FindObjectsByType<Light>(FindObjectsSortMode.None)) light.shadows = LightShadows.None;
            else if (diagnosticHardShadows)
                foreach (var light in FindObjectsByType<Light>(FindObjectsSortMode.None)) light.shadows = LightShadows.Hard;
            int scaleArgument = Array.IndexOf(arguments, "-nfs-diagnostic-render-scale");
            if (scaleArgument >= 0 && scaleArgument + 1 < arguments.Length &&
                float.TryParse(arguments[scaleArgument + 1], System.Globalization.NumberStyles.Float,
                    System.Globalization.CultureInfo.InvariantCulture, out var scale) &&
                GraphicsSettings.currentRenderPipeline is UniversalRenderPipelineAsset diagnosticPipeline)
                diagnosticPipeline.renderScale = Mathf.Clamp(scale, .5f, 1);
            Application.runInBackground = true;
            Application.targetFrameRate = -1;
            QualitySettings.vSyncCount = 0;
            Screen.SetResolution(1920, 1080, FullScreenMode.Windowed);
            RenderPipelineManager.endCameraRendering += CountRender;
            var car = FindFirstObjectByType<ArcadeCarController>();
            car.GetComponent<VehicleInput>().enabled = false;
            var camera = Camera.main;
            var chase = camera.GetComponent<ChaseCamera>();
            var spawn = car.Body.position;
            var views = new List<View>();
            // Coordinates select separated district areas; raycasts resolve real imported road height.
            var probes = new[] { spawn, new Vector3(450, 0, 800), new Vector3(-550, 0, 550),
                                 new Vector3(100, 0, 1400), new Vector3(0, 0, -600) };
            var colliders = FindObjectsByType<MeshCollider>(FindObjectsSortMode.None)
                .Where(c => c.transform.root.name == "RoadsPhysical").ToArray();
            var timings = new FrameTiming[1];
            using var drawCalls = ProfilerRecorder.StartNew(ProfilerCategory.Render, "Draw Calls Count", 1);
            using var triangles = ProfilerRecorder.StartNew(ProfilerCategory.Render, "Triangles Count", 1);
            using var setPass = ProfilerRecorder.StartNew(ProfilerCategory.Render, "SetPass Calls Count", 1);
            for (int index = 0; index < probes.Length; index++)
            {
                var closest = colliders.OrderBy(c => (c.bounds.ClosestPoint(probes[index]) - probes[index]).sqrMagnitude).First();
                var point = closest.bounds.center;
                if (!closest.Raycast(new Ray(point + Vector3.up * 100, Vector3.down), out var hit, 250))
                {
                    // Fall back to a known triangle centre when a curved mesh's box centre is off-road.
                    var mesh = closest.sharedMesh;
                    var triangle = mesh.triangles;
                    var vertices = mesh.vertices;
                    point = closest.transform.TransformPoint((vertices[triangle[0]] + vertices[triangle[1]] + vertices[triangle[2]]) / 3);
                }
                else point = hit.point;
                if (index == 0) point = spawn - Vector3.up * .24f;
                car.Body.position = point + Vector3.up * .24f;
                car.Body.linearVelocity = Vector3.zero;
                car.Body.angularVelocity = Vector3.zero;
                car.SetCommand(new VehicleCommand(0, 0, 0, true));
                Physics.SyncTransforms();
                chase.SnapToTarget();
                for (int warmup = 0; warmup < 120; warmup++) yield return null;
                var times = new List<float>();
                var gpu = new List<double>();
                for (int frame = 0; frame < 360; frame++)
                {
                    yield return null;
                    times.Add(Time.unscaledDeltaTime * 1000);
                    FrameTimingManager.CaptureFrameTimings();
                    if (FrameTimingManager.GetLatestTimings(1, timings) > 0 && timings[0].gpuFrameTime > 0)
                        gpu.Add(timings[0].gpuFrameTime);
                }
                times.Sort(); gpu.Sort();
                views.Add(new View
                {
                    name = "district-view-" + index, position = point, frames = times.Count,
                    p50Milliseconds = Percentile(times, .5f), p95Milliseconds = Percentile(times, .95f),
                    p99Milliseconds = Percentile(times, .99f), meanFps = 1000 / times.Average(),
                    gpuP95Milliseconds = gpu.Count == 0 ? 0 : gpu[(int)((gpu.Count - 1) * .95)], gpuSamples = gpu.Count,
                    drawCalls = drawCalls.Valid ? drawCalls.LastValue : -1,
                    renderedTriangles = triangles.Valid ? triangles.LastValue : -1,
                    setPassCalls = setPass.Valid ? setPass.LastValue : -1
                });
                Debug.Log($"NFS World view {index}: {views.Last().meanFps:0.0} FPS; {gpu.Count} GPU samples.");
            }
            RenderPipelineManager.endCameraRendering -= CountRender;
            var report = new Report
            {
                graphicsDevice = SystemInfo.graphicsDeviceName, processor = SystemInfo.processorType,
                width = Screen.width, height = Screen.height, cameraRenders = cameraRenders,
                allocatedMemoryBytes = UnityEngine.Profiling.Profiler.GetTotalAllocatedMemoryLong(), views = views.ToArray(),
                renderingCountersAvailable = drawCalls.Valid && triangles.Valid && setPass.Valid,
                scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name,
                method = "Uncapped rendered standalone player; five stationary road viewpoints, 120 warmup and 360 measured frames each. Not a route-driving benchmark."
            };
            if (GraphicsSettings.currentRenderPipeline is UniversalRenderPipelineAsset pipeline)
            {
                report.renderScale = pipeline.renderScale;
                report.internalWidth = Mathf.RoundToInt(Screen.width * pipeline.renderScale);
                report.internalHeight = Mathf.RoundToInt(Screen.height * pipeline.renderScale);
                report.upscaling = pipeline.upscalingFilter.ToString();
            }
            var directory = Path.GetFullPath(Path.Combine(Application.dataPath, "../../../artifacts/NfsWorld"));
            Directory.CreateDirectory(directory);
            string filename = arguments.Contains("-nfs-art-benchmark") ? "player-art-benchmark.json" :
                arguments.Contains("-nfs-style-benchmark") ? "player-style-benchmark.json" : "player-view-benchmark.json";
            if (diagnosticNoShadows || diagnosticHardShadows || scaleArgument >= 0) filename = "player-art-diagnostic-benchmark.json";
            File.WriteAllText(Path.Combine(directory, filename), JsonUtility.ToJson(report, true));
            Debug.Log($"NFS World rendered benchmark finished: {cameraRenders} camera renders, {views.Sum(v => v.gpuSamples)} GPU samples.");
            Application.Quit(cameraRenders >= 2400 && views.All(v => v.gpuSamples > 0) ? 0 : 1);
        }

        private void CountRender(ScriptableRenderContext context, Camera camera) { if (camera == Camera.main) cameraRenders++; }
        private static float Percentile(List<float> values, float fraction) => values[Mathf.FloorToInt((values.Count - 1) * fraction)];
    }
}
