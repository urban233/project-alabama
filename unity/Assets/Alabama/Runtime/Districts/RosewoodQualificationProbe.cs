using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Alabama.Driving;
using Unity.Profiling;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

namespace Alabama.Districts
{
    /// <summary>Opt-in real-road proof; independent opposing route legs use one persistent car.</summary>
    public sealed class RosewoodQualificationProbe : MonoBehaviour
    {
        [SerializeField] private TextAsset route;
        [SerializeField] private string signature;
        [Serializable] private sealed class Route { public Leg[] legs; public Vector3 seamCentre; public Vector3 seamOutward; public string downtownConnection; public string rosewoodConnection; }
        [Serializable] private sealed class Leg { public string name; public Vector3[] points; }
        [Serializable] private sealed class LegResult { public string name; public float distanceMetres; public float durationSeconds; public int frames; public int supportedFrames; public int seamCrossings; public float maximumLateralError; public float maximumRoadHeightError; }
        [Serializable] private sealed class Report
        {
            public bool passed; public string failure; public string signature; public int seamCrossings;
            public int frames; public int cameraRenders; public int gpuSamples; public int independentLegSpawns;
            public string graphicsDevice; public string processor; public int systemMemoryMegabytes; public int graphicsMemoryMegabytes; public float startupSeconds; public int width; public int height; public float renderScale;
            public int internalWidth; public int internalHeight; public string upscaling; public int frameCap;
            public float meanFps; public float p50Milliseconds; public float p95Milliseconds; public float p99Milliseconds;
            public float maximumMilliseconds; public int framesOver40Milliseconds; public double gpuP95Milliseconds;
            public double cpuP95Milliseconds; public long peakAllocatedMemoryBytes; public long peakDrawCalls;
            public long peakRenderedTriangles; public bool renderingCountersAvailable;
            public float durationSeconds; public float distanceMetres; public LegResult[] legs; public string method;
        }
        private readonly List<float> frameTimes = new List<float>();
        private readonly List<double> gpuTimes = new List<double>(), cpuTimes = new List<double>();
        private int renders;
        private string failure;
        public void Configure(TextAsset data, string identity) { route = data; signature = identity; }
        private void CountRender(ScriptableRenderContext context, Camera camera) { if (camera == Camera.main) renders++; }
        private static string DirectoryPath => Path.GetFullPath(Path.Combine(Application.dataPath, "../../../artifacts/NfsWorld/Rosewood"));

        private IEnumerator Start()
        {
            var args = Environment.GetCommandLineArgs();
            bool longRoute = args.Contains("-nfs-rosewood-route");
            if (Application.isEditor || (!longRoute && !args.Contains("-nfs-rosewood-seam"))) yield break;
            Directory.CreateDirectory(DirectoryPath);
            Application.logMessageReceived += LogFailure;
            var runtime = DistrictRuntime.Instance;
            float deadline = Time.realtimeSinceStartup + 300;
            while (!runtime.Ready && Time.realtimeSinceStartup < deadline && failure == null)
            { if (runtime.LastFailure != null) failure = runtime.LastFailure; yield return null; }
            var report = new Report { signature = signature, graphicsDevice = SystemInfo.graphicsDeviceName, processor = SystemInfo.processorType,
                systemMemoryMegabytes = SystemInfo.systemMemorySize, graphicsMemoryMegabytes = SystemInfo.graphicsMemorySize, startupSeconds = Time.realtimeSinceStartup,
                independentLegSpawns = 2, method = "Two independently spawned opposing source-lane legs, real WheelCollider driving with the same persistent car/input/camera. No position or velocity writes during either leg or seam crossing. Warm-up and initial loading excluded from frame statistics." };
            if (!runtime.Ready || failure != null) { report.failure = failure ?? "Runtime readiness timed out."; Write(report, longRoute); yield break; }
            if (runtime.DistrictCount != 2) { report.failure = "Both real districts must be registered."; Write(report, longRoute); yield break; }
            var data = JsonUtility.FromJson<Route>(route.text);
            var car = runtime.Player; var body = car.Body; var input = car.GetComponent<VehicleInput>();
            var camera = Camera.main; var chase = runtime.Chase;
            var pipeline = QualitySettings.renderPipeline;
            var fog = RenderSettings.fogColor; float density = RenderSettings.fogDensity;
            var owners = runtime.LoadedDistricts.ToArray();
            // Trial opening is in the disposable player's scene instances; acceptance saves
            // only these two closures after this report passes. Other walls stay active.
            foreach (var district in owners)
                foreach (var connection in district.Connections)
                    if (connection.id == data.downtownConnection || connection.id == data.rosewoodConnection)
                        connection.closure.SetActive(false);
            input.enabled = false; Application.runInBackground = true; QualitySettings.vSyncCount = 0;
            int cap = 30; int capIndex = Array.IndexOf(args, "-nfs-rosewood-cap");
            if (capIndex >= 0 && capIndex + 1 < args.Length && int.TryParse(args[capIndex + 1], out int requested)) cap = requested > 0 ? requested : -1;
            Application.targetFrameRate = cap; Screen.SetResolution(1920, 1080, FullScreenMode.Windowed);
            RenderPipelineManager.endCameraRendering += CountRender;
            var wheels = car.GetComponentsInChildren<WheelCollider>();
            var results = new List<LegResult>();
            using var drawCalls = ProfilerRecorder.StartNew(ProfilerCategory.Render, "Draw Calls Count", 1);
            using var triangles = ProfilerRecorder.StartNew(ProfilerCategory.Render, "Triangles Count", 1);
            report.renderingCountersAvailable = drawCalls.Valid && triangles.Valid;
            using var memory = ProfilerRecorder.StartNew(ProfilerCategory.Memory, "Total Used Memory", 1);
            foreach (var sourceLeg in data.legs)
            {
                var points = sourceLeg.points;
                if (!longRoute)
                {
                    int nearest = Enumerable.Range(0, points.Length).OrderBy(i => (points[i] - data.seamCentre).sqrMagnitude).First();
                    points = points.Skip(Mathf.Max(0, nearest - 5)).Take(11).ToArray();
                }
                var result = new LegResult { name = sourceLeg.name };
                var direction = points[1] - points[0]; direction.y = 0;
                if (!Road(points[0], out var ground, out _)) { failure = "Initial source lane has no road: " + points[0]; break; }
                body.isKinematic = true; body.position = ground + Vector3.up * .24f; body.rotation = Quaternion.LookRotation(direction);
                Physics.SyncTransforms(); body.isKinematic = false; body.linearVelocity = Vector3.zero; body.angularVelocity = Vector3.zero;
                car.SetCommand(new VehicleCommand(0, 1, 0, true)); chase.SnapToTarget();
                float settle = Time.realtimeSinceStartup + 2;
                while (Time.realtimeSinceStartup < settle) yield return null;
                sampling = true;
                var previous = body.position; float start = Time.realtimeSinceStartup; int segment = 0;
                float previousSide = Vector3.Dot(body.position - data.seamCentre, data.seamOutward);
                var contactOwners = new HashSet<string>(); bool captured = false;
                var capturedViews = new HashSet<int>();
                while (segment < points.Length - 1 && failure == null)
                {
                    var position = body.position;
                    while (segment < points.Length - 2 && Projection(position, points[segment], points[segment + 1]) >= 1) segment++;
                    if (segment == points.Length - 2 && Vector3.Distance(position, points[points.Length - 1]) < 5) break;
                    var nearest = Closest(position, points[segment], points[segment + 1]);
                    float lateral = new Vector2(position.x - nearest.x, position.z - nearest.z).magnitude;
                    result.maximumLateralError = Mathf.Max(result.maximumLateralError, lateral);
                    if (lateral > 5 || Vector3.Dot(car.transform.up, Vector3.up) < .5f) { failure = "Route departure/rollover: " + sourceLeg.name + " at segment " + segment; break; }
                    var aim = LookAhead(points, segment, nearest, 9);
                    var local = car.transform.InverseTransformPoint(aim);
                    float steerAngle = Mathf.Atan2(2 * 2.7f * local.x, Mathf.Max(1, local.x * local.x + local.z * local.z)) * Mathf.Rad2Deg;
                    float availableSteer = Mathf.Lerp(car.Tuning.LowSpeedSteerDegrees, car.Tuning.HighSpeedSteerDegrees,
                        Mathf.Clamp01(Mathf.Abs(car.SignedForwardSpeed) / car.Tuning.HighSpeedSteerAtMetresPerSecond));
                    float targetSpeed = Mathf.Lerp(10, 6, Mathf.Clamp01(Mathf.Abs(steerAngle) / 25));
                    float speed = car.SignedForwardSpeed;
                    car.SetCommand(new VehicleCommand(speed < targetSpeed ? .35f : 0, speed > targetSpeed + .5f ? .25f : 0,
                        Mathf.Clamp(steerAngle / availableSteer, -1, 1), false));
                    yield return new WaitForFixedUpdate();
                    if (runtime.Player != car || runtime.Chase != chase || car.GetComponent<VehicleInput>() != input ||
                        Camera.main != camera || QualitySettings.renderPipeline != pipeline || RenderSettings.fogColor != fog ||
                        Mathf.Abs(RenderSettings.fogDensity - density) > 1e-7f || SceneManager.GetActiveScene() != runtime.gameObject.scene)
                    { failure = "Persistent gameplay/render ownership changed across the route."; break; }
                    int supported = 0;
                    foreach (var wheel in wheels)
                        if (wheel.GetGroundHit(out var hit) && hit.collider.transform.root.name == "RoadsPhysical")
                        {
                            supported++; contactOwners.Add(hit.collider.gameObject.scene == owners.Single(d => d.Id == "downtown").gameObject.scene ? "downtown" : "rosewood");
                            if (Road(hit.point, out _, out int tyreOwners) && tyreOwners > 1) failure = "Duplicate district collision at a tyre contact.";
                        }
                    if (supported >= 3) result.supportedFrames++;
                    result.frames++;
                    report.peakAllocatedMemoryBytes = Math.Max(report.peakAllocatedMemoryBytes, Math.Max(memory.LastValue, UnityEngine.Profiling.Profiler.GetTotalAllocatedMemoryLong()));
                    report.peakDrawCalls = Math.Max(report.peakDrawCalls, drawCalls.LastValue);
                    report.peakRenderedTriangles = Math.Max(report.peakRenderedTriangles, triangles.LastValue);
                    if (!Road(body.position, out ground, out int sceneOwners)) { failure = "Road missing under moving car."; break; }
                    float heightError = Mathf.Abs(body.position.y - (ground.y + .24f));
                    result.maximumRoadHeightError = Mathf.Max(result.maximumRoadHeightError, heightError);
                    if (heightError > 1.2f || sceneOwners > 1) { failure = "Fall or duplicate district collision under moving car."; break; }
                    float side = Vector3.Dot(body.position - data.seamCentre, data.seamOutward);
                    if (side * previousSide < 0) result.seamCrossings++;
                    previousSide = side;
                    result.distanceMetres += Vector3.Distance(previous, body.position); previous = body.position;
                    if (!captured && Mathf.Abs(side) < 12)
                    { ScreenCapture.CaptureScreenshot(Path.Combine(DirectoryPath, sourceLeg.name + (longRoute ? "-route" : "-seam") + ".png")); captured = true; }
                    if (longRoute)
                        foreach (int percentage in new[] { 25, 50, 75 })
                            if (segment >= (points.Length - 1) * percentage / 100 && capturedViews.Add(percentage))
                                ScreenCapture.CaptureScreenshot(Path.Combine(DirectoryPath, sourceLeg.name + "-" + percentage + ".png"));
                    if (Time.realtimeSinceStartup - start > (longRoute ? 400 : 90)) { failure = "Driving leg timeout."; break; }
                }
                result.durationSeconds = Time.realtimeSinceStartup - start;
                sampling = false;
                car.SetCommand(new VehicleCommand(0, 1, 0, true));
                if (result.seamCrossings != 1 || contactOwners.Count != 2 || result.frames == 0 || result.supportedFrames < result.frames * .94f)
                    failure = failure ?? "Seam crossing or wheel support was not proven in " + result.name;
                results.Add(result);
                report.seamCrossings += result.seamCrossings; report.distanceMetres += result.distanceMetres;
                report.durationSeconds += result.durationSeconds; report.frames += result.frames;
                if (failure != null) break;
            }
            RenderPipelineManager.endCameraRendering -= CountRender;
            report.legs = results.ToArray(); report.cameraRenders = renders;
            report.width = Screen.width; report.height = Screen.height; report.frameCap = Application.targetFrameRate;
            if (GraphicsSettings.currentRenderPipeline is UniversalRenderPipelineAsset urp)
            { report.renderScale = urp.renderScale; report.internalWidth = Mathf.RoundToInt(Screen.width * urp.renderScale); report.internalHeight = Mathf.RoundToInt(Screen.height * urp.renderScale); report.upscaling = urp.upscalingFilter.ToString(); }
            report.failure = failure;
            report.passed = failure == null && report.seamCrossings == 2 && renders > 120 && report.width == 1920 && report.height == 1080 && gpuTimes.Count > 0 &&
                Mathf.Abs(report.renderScale - .75f) < .001f && report.upscaling == "FSR";
            Write(report, longRoute);
        }

        private bool sampling;
        private readonly FrameTiming[] latest = new FrameTiming[1];
        private void LateUpdate()
        {
            if (!sampling || renders == 0) return;
            frameTimes.Add(Time.unscaledDeltaTime * 1000);
            FrameTimingManager.CaptureFrameTimings();
            if (FrameTimingManager.GetLatestTimings(1, latest) > 0)
            { if (latest[0].gpuFrameTime > 0) gpuTimes.Add(latest[0].gpuFrameTime); if (latest[0].cpuFrameTime > 0) cpuTimes.Add(latest[0].cpuFrameTime); }
        }
        private void Write(Report report, bool longRoute)
        {
            if (frameTimes.Count > 0)
            {
                var ordered = frameTimes.OrderBy(v => v).ToArray();
                report.meanFps = 1000 / frameTimes.Average(); report.p50Milliseconds = ordered[(int)((ordered.Length - 1) * .5f)];
                report.p95Milliseconds = ordered[(int)((ordered.Length - 1) * .95f)]; report.p99Milliseconds = ordered[(int)((ordered.Length - 1) * .99f)];
                report.maximumMilliseconds = ordered.Last(); report.framesOver40Milliseconds = ordered.Count(v => v > 40);
            }
            report.gpuSamples = gpuTimes.Count;
            if (gpuTimes.Count > 0) report.gpuP95Milliseconds = gpuTimes.OrderBy(v => v).ElementAt((int)((gpuTimes.Count - 1) * .95));
            if (cpuTimes.Count > 0) report.cpuP95Milliseconds = cpuTimes.OrderBy(v => v).ElementAt((int)((cpuTimes.Count - 1) * .95));
            string name = longRoute ? "player-route" + (report.frameCap > 0 ? "-cap" + report.frameCap : "-uncapped") : "player-seam";
            File.WriteAllText(Path.Combine(DirectoryPath, name + ".json"), JsonUtility.ToJson(report, true));
            if (report.passed) Debug.Log("Rosewood real-road qualification passed."); else Debug.LogError("Rosewood qualification failed: " + report.failure);
            Application.logMessageReceived -= LogFailure;
            Application.Quit(report.passed ? 0 : 1);
        }
        private void LogFailure(string message, string stack, LogType type) { if (type == LogType.Exception || type == LogType.Error) failure = failure ?? message; }
        private static float Projection(Vector3 p, Vector3 a, Vector3 b) => Vector3.Dot(p - a, b - a) / (b - a).sqrMagnitude;
        private static Vector3 Closest(Vector3 p, Vector3 a, Vector3 b) => Vector3.Lerp(a, b, Mathf.Clamp01(Projection(p, a, b)));
        private static Vector3 LookAhead(Vector3[] points, int segment, Vector3 nearest, float distance)
        {
            var current = nearest;
            for (int i = segment + 1; i < points.Length; i++)
            { float length = Vector3.Distance(current, points[i]); if (length >= distance) return Vector3.Lerp(current, points[i], distance / length); distance -= length; current = points[i]; }
            return points.Last();
        }
        private static bool Road(Vector3 probe, out Vector3 point, out int owners)
        {
            var hits = Physics.RaycastAll(probe + Vector3.up * 2, Vector3.down, 4, ~0, QueryTriggerInteraction.Ignore)
                .Where(h => h.collider.transform.root.name == "RoadsPhysical" && h.normal.y > .5f)
                .OrderBy(h => Mathf.Abs(h.point.y - probe.y)).ToArray();
            owners = hits.Where(h => hits.Length > 0 && Mathf.Abs(h.point.y - hits[0].point.y) < .05f).Select(h => h.collider.gameObject.scene.handle).Distinct().Count();
            point = hits.Length > 0 ? hits[0].point : Vector3.zero;
            return hits.Length > 0;
        }
    }
}
