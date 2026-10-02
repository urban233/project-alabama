using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Alabama.Bootstrap;
using Alabama.Driving;
using UnityEngine;
using UnityEngine.Rendering;

namespace Alabama.Districts
{
    /// <summary>Opt-in Windows-player lifetime verification, separate from steady-view benchmarks.</summary>
    public sealed class DistrictLifetimeProbe : MonoBehaviour
    {
        private const string Root = "Assets/Alabama/Art/Maps/NfsWorld/Runtime/";
        [Serializable] private sealed class Operation
        {
            public string name;
            public float wallMilliseconds;
            public float meanFrameMilliseconds;
            public float p95Milliseconds;
            public float maximumFrameMilliseconds;
            public int frames;
            public long allocatedMemoryBytes;
        }
        [Serializable] private sealed class Report
        {
            public bool passed;
            public string[] failures;
            public string graphicsDevice;
            public string processor;
            public int width, height, targetFrameRate, cameraRenders;
            public int registrations, unregistrations;
            public int initialGameObjects, finalGameObjects, initialColliders, finalColliders;
            public long initialAllocatedBytes, finalAllocatedBytes;
            public Operation[] operations;
            public string method = "Rendered Windows player; six remote fixture cycles, two real Downtown unload/reload cycles with explicit recovery to retained synthetic support. No real neighbouring district or seamless connection is claimed.";
        }
        private readonly List<string> failures = new List<string>();
        private readonly List<Operation> operations = new List<Operation>();
        private readonly List<float> frameTimes = new List<float>();
        private bool measuring;
        private double lastFrameSample;
        private int renders;
        private Report report;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Install()
        {
            if (!Application.isEditor && Environment.GetCommandLineArgs().Contains("-district-lifetime-probe"))
                new GameObject("Optional district lifetime probe").AddComponent<DistrictLifetimeProbe>();
        }

        private IEnumerator Start()
        {
            Application.runInBackground = true;
            Application.logMessageReceived += Log;
            RenderPipelineManager.endCameraRendering += Render;
            var runtime = DistrictRuntime.Instance;
            float deadline = Time.realtimeSinceStartup + 120;
            while (runtime != null && !runtime.Ready && runtime.LastFailure == null && Time.realtimeSinceStartup < deadline) yield return null;
            if (runtime == null || !runtime.Ready) { Check(false, "Runtime did not become ready."); Finish(); yield break; }
            var car = runtime.Player; var camera = runtime.Chase;
            var input = car.GetComponent<VehicleInput>();
            var pipeline = QualitySettings.renderPipeline;
            var fog = RenderSettings.fogColor; float density = RenderSettings.fogDensity;
            var sun = RenderSettings.sun; float intensity = sun.intensity; var shadows = sun.shadows;
            int cap = Application.targetFrameRate;
            for (int frame = 0; frame < 120; frame++) yield return null;
            report = new Report { graphicsDevice = SystemInfo.graphicsDeviceName, processor = SystemInfo.processorType,
                width = Screen.width, height = Screen.height, targetFrameRate = cap,
                initialGameObjects = FindObjectsByType<GameObject>(FindObjectsSortMode.None).Length,
                initialColliders = FindObjectsByType<Collider>(FindObjectsSortMode.None).Length,
                initialAllocatedBytes = UnityEngine.Profiling.Profiler.GetTotalAllocatedMemoryLong() };
            runtime.DistrictRegistered += _ => report.registrations++;
            runtime.DistrictUnregistered += _ => report.unregistrations++;
            for (int cycle = 0; cycle < 6; cycle++)
            {
                yield return Measure("fixture-load-" + cycle, runtime.LoadDistrict(Root + "FixtureB.unity"));
                Check(runtime.LastFailure == null && runtime.DistrictCount == 2, "Fixture load failed: " + runtime.LastFailure);
                var b = runtime.LoadedDistricts.FirstOrDefault(d => d.Id == "fixture-b");
                var stale = b == null ? Array.Empty<NfsWorldDistanceCulling>() : b.Culling;
                yield return Measure("fixture-unload-" + cycle, runtime.UnloadDistrict("fixture-b"));
                Check(runtime.LastFailure == null && runtime.DistrictCount == 1 && stale.All(c => c == null), "Fixture lifetime cleanup failed.");
            }
            yield return runtime.UnloadDistrict("downtown");
            Check(runtime.LastFailure != null && runtime.DistrictCount == 1, "Last-district unload must be rejected.");
            for (int cycle = 0; cycle < 2; cycle++)
            {
                yield return Measure("recovery-fixture-load-" + cycle, runtime.LoadDistrict(Root + "FixtureB.unity"));
                yield return runtime.UnloadDistrict("downtown");
                Check(runtime.LastFailure != null && runtime.DistrictCount == 2, "Supporting unload must require explicit recovery.");
                var old = runtime.LoadedDistricts.FirstOrDefault(d => d.Id == "downtown");
                var stale = old == null ? Array.Empty<NfsWorldDistanceCulling>() : old.Culling;
                Time.timeScale = 0;
                yield return Measure("downtown-unload-controlled-" + cycle, runtime.UnloadDistrict("downtown", "fixture-b"));
                Check(Time.timeScale == 0 && runtime.LastFailure == null && runtime.DistrictCount == 1 && stale.All(c => c == null), "Controlled unload/pause/cleanup failed.");
                Time.timeScale = 1;
                yield return Measure("downtown-reload-" + cycle, runtime.LoadDistrict(Root + "DowntownContent.unity"));
                Check(runtime.LastFailure == null && runtime.DistrictCount == 2, "Downtown reload failed.");
                yield return Measure("fixture-unload-controlled-" + cycle, runtime.UnloadDistrict("fixture-b", "downtown"));
                Check(runtime.LastFailure == null && runtime.DistrictCount == 1, "Return to real Downtown failed.");
                for (int step = 0; step < 120; step++) yield return new WaitForFixedUpdate();
                var downtown = runtime.LoadedDistricts.FirstOrDefault(d => d.Id == "downtown");
                Check(downtown != null && car.GetComponentsInChildren<WheelCollider>().Count(w => w.GetGroundHit(out var h) &&
                    h.collider.gameObject.scene == downtown.gameObject.scene) == 4, "Real Downtown tyre support failed.");
                Check(downtown != null && downtown.Connections.All(c => !c.seamVerified && c.closure.activeInHierarchy), "Unverified exits must remain closed.");
            }
            Check(FindObjectsByType<ArcadeCarController>(FindObjectsSortMode.None).SequenceEqual(new[] { car }) &&
                FindObjectsByType<ChaseCamera>(FindObjectsSortMode.None).SequenceEqual(new[] { camera }) &&
                FindObjectsByType<VehicleInput>(FindObjectsSortMode.None).SequenceEqual(new[] { input }) && input.enabled &&
                FindObjectsByType<DemoBootstrap>(FindObjectsSortMode.None).Length == 1 &&
                FindObjectsByType<NfsWorldRenderSettings>(FindObjectsSortMode.None).Length == 1, "Persistent owner identity changed.");
            Check(QualitySettings.renderPipeline == pipeline && RenderSettings.fogColor == fog && RenderSettings.fogDensity == density &&
                RenderSettings.sun == sun && sun.intensity == intensity && sun.shadows == shadows && Application.targetFrameRate == cap &&
                UnityEngine.SceneManagement.SceneManager.GetActiveScene() == runtime.gameObject.scene, "Shared rendering changed during district lifetime.");
            car.Body.position = new Vector3(0, -100, 0);
            yield return new WaitForFixedUpdate(); yield return new WaitForFixedUpdate();
            Check(car.Body.position.y > -2, "Real Downtown fall recovery failed after reload.");
            for (int frame = 0; frame < 120; frame++) yield return null;
            report.finalGameObjects = FindObjectsByType<GameObject>(FindObjectsSortMode.None).Length;
            report.finalColliders = FindObjectsByType<Collider>(FindObjectsSortMode.None).Length;
            report.finalAllocatedBytes = UnityEngine.Profiling.Profiler.GetTotalAllocatedMemoryLong();
            Check(report.initialGameObjects == report.finalGameObjects && report.initialColliders == report.finalColliders,
                "Repeated operations accumulated objects or colliders.");
            Check(report.registrations == report.unregistrations, "Lifetime hooks did not balance.");
            ScreenCapture.CaptureScreenshot(Path.Combine(OutputDirectory(), "runtime-player-after-reload.png"));
            for (int frame = 0; frame < 5; frame++) yield return null;
            Check(renders > 240, "The player did not render camera frames.");
            Finish();
        }

        private IEnumerator Measure(string name, IEnumerator operation)
        {
            frameTimes.Clear(); measuring = true; double start = Time.realtimeSinceStartupAsDouble;
            lastFrameSample = start;
            yield return operation;
            float wall = (float)((Time.realtimeSinceStartupAsDouble - start) * 1000);
            // Include the first rendered frames after completion to retain activation/teardown hitches.
            for (int frame = 0; frame < 5; frame++) yield return null;
            measuring = false; frameTimes.Sort();
            operations.Add(new Operation { name = name, wallMilliseconds = wall, frames = frameTimes.Count,
                meanFrameMilliseconds = frameTimes.Count == 0 ? 0 : frameTimes.Average(),
                p95Milliseconds = frameTimes.Count == 0 ? 0 : frameTimes[(int)((frameTimes.Count - 1) * .95f)],
                maximumFrameMilliseconds = frameTimes.Count == 0 ? 0 : frameTimes.Last(),
                allocatedMemoryBytes = UnityEngine.Profiling.Profiler.GetTotalAllocatedMemoryLong() });
        }
        private void Update()
        {
            if (!measuring) return;
            double now = Time.realtimeSinceStartupAsDouble;
            frameTimes.Add((float)((now - lastFrameSample) * 1000)); lastFrameSample = now;
        }
        private void Render(ScriptableRenderContext context, Camera camera) { if (camera == Camera.main) renders++; }
        private void Log(string message, string stack, LogType type)
        { if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert) failures.Add(message); }
        private void Check(bool valid, string message) { if (!valid) failures.Add(message); }
        private static string OutputDirectory()
        {
            var path = Path.GetFullPath(Path.Combine(Application.dataPath, "../../../artifacts/NfsWorld"));
            Directory.CreateDirectory(path); return path;
        }
        private void Finish()
        {
            report = report ?? new Report(); report.passed = failures.Count == 0; report.failures = failures.ToArray();
            report.operations = operations.ToArray(); report.cameraRenders = renders;
            File.WriteAllText(Path.Combine(OutputDirectory(), "player-runtime-lifetime.json"), JsonUtility.ToJson(report, true));
            Application.logMessageReceived -= Log; RenderPipelineManager.endCameraRendering -= Render;
            Application.Quit(report.passed ? 0 : 1);
        }
    }
}
