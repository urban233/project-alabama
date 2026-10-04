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
            public double cpuP95Milliseconds;
            public double cpuMainThreadP95Milliseconds;
            public double cpuRenderThreadP95Milliseconds;
            public double presentWaitP95Milliseconds;
            public int gpuSamples;
            public long drawCalls;
            public long renderedTriangles;
            public long setPassCalls;
            public bool driving;
            public float distanceMetres;
            public int supportedFrames;
            public int framesOver40Milliseconds;
            public float maximumMilliseconds;
        }
        [Serializable] private sealed class Report
        {
            public string graphicsDevice;
            public string graphicsApi;
            public string processor;
            public int width;
            public int height;
            public float renderScale;
            public int internalWidth;
            public int internalHeight;
            public string upscaling;
            public int cameraRenders;
            public int meshLodTargets, staticBatchLodTargets, reducedLodTargets;
            public bool diffuseFillConfigured, diffuseFillActive;
            public long allocatedMemoryBytes;
            public View[] views;
            public string method;
            public bool renderingCountersAvailable;
            public string scene;
            public bool diagnosticNoShadows;
            public bool diagnosticHardShadows;
            public int targetFrameRate;
        }

        private int cameraRenders;
        private IEnumerator Start()
        {
            if (Application.isEditor || !Environment.GetCommandLineArgs().Contains("-nfs-benchmark")) yield break;
            var runtime = Alabama.Districts.DistrictRuntime.Instance;
            if (runtime != null)
                while (!runtime.Ready)
                {
                    if (runtime.LastFailure != null) throw new InvalidOperationException(runtime.LastFailure);
                    yield return null;
                }
            var arguments = Environment.GetCommandLineArgs();
            bool driving = arguments.Contains("-nfs-driving-benchmark");
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
            int capArgument = Array.IndexOf(arguments, "-nfs-benchmark-cap");
            if (capArgument >= 0 && capArgument + 1 < arguments.Length && int.TryParse(arguments[capArgument + 1], out int cap) && cap > 0)
                Application.targetFrameRate = cap;
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
            var probes = new List<Vector3> { spawn, new Vector3(450, 0, 800), new Vector3(-550, 0, 550),
                                 new Vector3(100, 0, 1400), new Vector3(0, 0, -600) };
            var routes = new[] { new Vector3(542.82843f, -3.94928f, 765.09351f),
                new Vector3(273.88864f, 26.03149f, 1120.71887f), new Vector3(-89.19291f, 22.263f, -551.47f) };
            var headings = new[] { 310f, 115f, 80f };
            if (driving) for (int repeat = 0; repeat < 3; repeat++) probes.AddRange(routes);
            var wheels = car.GetComponentsInChildren<WheelCollider>();
            var colliders = FindObjectsByType<MeshCollider>(FindObjectsSortMode.None)
                .Where(c => c.transform.root.name == "RoadsPhysical").ToArray();
            var timings = new FrameTiming[1];
            using var drawCalls = ProfilerRecorder.StartNew(ProfilerCategory.Render, "Draw Calls Count", 1);
            using var triangles = ProfilerRecorder.StartNew(ProfilerCategory.Render, "Triangles Count", 1);
            using var setPass = ProfilerRecorder.StartNew(ProfilerCategory.Render, "SetPass Calls Count", 1);
            for (int index = 0; index < probes.Count; index++)
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
                bool moving = index >= 5;
                var direction = moving ? Quaternion.Euler(0, headings[(index - 5) % 3], 0) * Vector3.forward : Vector3.forward;
                if (moving) point = probes[index];
                car.Body.position = point + Vector3.up * .24f;
                if (moving) car.Body.rotation = Quaternion.LookRotation(direction);
                car.Body.linearVelocity = Vector3.zero;
                car.Body.angularVelocity = Vector3.zero;
                car.SetCommand(new VehicleCommand(0, 0, 0, true));
                Physics.SyncTransforms();
                chase.SnapToTarget();
                for (int warmup = 0; warmup < 120; warmup++) yield return null;
                if (moving)
                {
                    if (!Road(point + direction * 2, out var ahead)) throw new InvalidOperationException("Benchmark road grade is absent.");
                    car.SetCommand(new VehicleCommand(.4f, 0, 0, false));
                    car.Body.linearVelocity = (ahead - point).normalized * 35;
                }
                float began = Time.time;
                int supported = 0;
                var times = new List<float>();
                var gpu = new List<double>();
                var cpu = new List<double>();
                var cpuMain = new List<double>(); var cpuRender = new List<double>(); var present = new List<double>();
                for (int frame = 0; moving ? Time.time - began < 2 : frame < 360; frame++)
                {
                    yield return null;
                    times.Add(Time.unscaledDeltaTime * 1000);
                    if (moving && wheels.Count(w => w.GetGroundHit(out var ground) && ground.collider.transform.root.name == "RoadsPhysical") >= 3) supported++;
                    FrameTimingManager.CaptureFrameTimings();
                    if (FrameTimingManager.GetLatestTimings(1, timings) > 0)
                    {
                        if (timings[0].gpuFrameTime > 0) gpu.Add(timings[0].gpuFrameTime);
                        if (timings[0].cpuFrameTime > 0) cpu.Add(timings[0].cpuFrameTime);
                        cpuMain.Add(timings[0].cpuMainThreadFrameTime);
                        cpuRender.Add(timings[0].cpuRenderThreadFrameTime);
                        present.Add(timings[0].cpuMainThreadPresentWaitTime);
                    }
                }
                times.Sort(); gpu.Sort(); cpu.Sort();
                cpuMain.Sort(); cpuRender.Sort(); present.Sort();
                float travelled = moving ? Vector3.Dot(car.Body.position - point, direction) : 0;
                if (moving && (travelled < 45 || supported < times.Count * .9f))
                    throw new InvalidOperationException($"Benchmark traversal failed: {travelled} metres, {supported}/{times.Count} supported frames.");
                views.Add(new View
                {
                    name = moving ? "drive-" + (index - 5) : "district-view-" + index, position = point, frames = times.Count,
                    driving = moving, distanceMetres = travelled, supportedFrames = supported,
                    framesOver40Milliseconds = times.Count(t => t > 40), maximumMilliseconds = times.Last(),
                    p50Milliseconds = Percentile(times, .5f), p95Milliseconds = Percentile(times, .95f),
                    p99Milliseconds = Percentile(times, .99f), meanFps = 1000 / times.Average(),
                    gpuP95Milliseconds = gpu.Count == 0 ? 0 : gpu[(int)((gpu.Count - 1) * .95)], gpuSamples = gpu.Count,
                    cpuP95Milliseconds = cpu.Count == 0 ? 0 : cpu[(int)((cpu.Count - 1) * .95)],
                    cpuMainThreadP95Milliseconds = cpuMain.Count == 0 ? 0 : cpuMain[(int)((cpuMain.Count - 1) * .95)],
                    cpuRenderThreadP95Milliseconds = cpuRender.Count == 0 ? 0 : cpuRender[(int)((cpuRender.Count - 1) * .95)],
                    presentWaitP95Milliseconds = present.Count == 0 ? 0 : present[(int)((present.Count - 1) * .95)],
                    drawCalls = drawCalls.Valid ? drawCalls.LastValue : -1,
                    renderedTriangles = triangles.Valid ? triangles.LastValue : -1,
                    setPassCalls = setPass.Valid ? setPass.LastValue : -1
                });
                Debug.Log($"NFS World view {index}: {views.Last().meanFps:0.0} FPS; {gpu.Count} GPU samples.");
            }
            RenderPipelineManager.endCameraRendering -= CountRender;
            var meshLods = FindObjectsByType<NfsWorldMeshLods>(FindObjectsSortMode.None);
            var renderSettings = FindFirstObjectByType<NfsWorldRenderSettings>();
            var report = new Report
            {
                graphicsDevice = SystemInfo.graphicsDeviceName, processor = SystemInfo.processorType,
                graphicsApi = SystemInfo.graphicsDeviceType.ToString(),
                width = Screen.width, height = Screen.height, cameraRenders = cameraRenders,
                meshLodTargets = meshLods.Sum(lod => lod.TargetCount),
                staticBatchLodTargets = meshLods.Sum(lod => lod.StaticBatchTargetCount),
                reducedLodTargets = meshLods.Sum(lod => lod.ReducedTargetCount),
                diffuseFillConfigured = renderSettings != null && renderSettings.DiffuseFillConfigured,
                diffuseFillActive = renderSettings != null && renderSettings.DiffuseFillActive,
                allocatedMemoryBytes = UnityEngine.Profiling.Profiler.GetTotalAllocatedMemoryLong(), views = views.ToArray(),
                renderingCountersAvailable = drawCalls.Valid && triangles.Valid && setPass.Valid,
                scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name,
                diagnosticNoShadows = diagnosticNoShadows, diagnosticHardShadows = diagnosticHardShadows,
                targetFrameRate = Application.targetFrameRate,
                method = "Rendered standalone player; five stationary road viewpoints, 120 warmup and 360 measured frames each." +
                    (driving ? " Nine 2-second high-speed traversals (three clear source-road corridors, repeated three times), initial 126 km/h along road grade. This does not certify every district street." : " Not a route-driving benchmark.")
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
            if (runtime != null) filename = "player-runtime-benchmark.json";
            if (arguments.Contains("-nfs-art-direction-benchmark")) filename = "player-art-direction-benchmark.json";
            if (driving) filename = filename.Replace(".json", "-driving.json");
            if (Application.targetFrameRate > 0) filename = filename.Replace(".json", "-cap" + Application.targetFrameRate + ".json");
            File.WriteAllText(Path.Combine(directory, filename), JsonUtility.ToJson(report, true));
            Debug.Log($"NFS World rendered benchmark finished: {cameraRenders} camera renders, {views.Sum(v => v.gpuSamples)} GPU samples.");
            Application.Quit(cameraRenders >= 2400 && views.All(v => v.gpuSamples > 0) &&
                report.staticBatchLodTargets == 0 &&
                (!arguments.Contains("-nfs-art-direction-benchmark") ||
                 (report.meshLodTargets > 0 && report.reducedLodTargets > 0 &&
                  report.diffuseFillConfigured && report.diffuseFillActive)) ? 0 : 1);
        }

        private void CountRender(ScriptableRenderContext context, Camera camera) { if (camera == Camera.main) cameraRenders++; }
        private static bool Road(Vector3 probe, out Vector3 point)
        {
            var hits = Physics.RaycastAll(probe + Vector3.up * 5, Vector3.down, 10)
                .Where(h => h.collider.transform.root.name == "RoadsPhysical" && h.normal.y > .5f)
                .OrderBy(h => Mathf.Abs(h.point.y - probe.y)).ToArray();
            point = hits.Length == 0 ? probe : hits[0].point;
            return hits.Length > 0 && Mathf.Abs(point.y - probe.y) < 3;
        }
        private static float Percentile(List<float> values, float fraction) => values[Mathf.FloorToInt((values.Count - 1) * fraction)];
    }
}
