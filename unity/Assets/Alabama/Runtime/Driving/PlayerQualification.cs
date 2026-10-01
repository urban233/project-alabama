using System;
using System.Collections.Generic;
using System.IO;
using Unity.Profiling;
using UnityEngine;
using UnityEngine.Profiling;

namespace Alabama.Driving
{
    /// <summary>Opt-in, graphics-enabled player measurement. Never runs during ordinary play.</summary>
    public sealed class PlayerQualification : MonoBehaviour
    {
        [Serializable]
        private sealed class Result
        {
            public string mode;
            public string utc;
            public string unityVersion;
            public string cpu;
            public string gpu;
            public string graphicsApi;
            public int systemMemoryMb;
            public int width;
            public int height;
            public int targetFrameRate;
            public int vSyncCount;
            public float routeLengthMetres;
            public int completedLaps;
            public float elapsedWallSeconds;
            public float measuredLapSeconds;
            public int measuredFrames;
            public float meanFps;
            public float frameP50Ms;
            public float frameP95Ms;
            public float frameP99Ms;
            public float frameMaxMs;
            public float cpuMainP95Ms;
            public float cpuRenderP95Ms;
            public float gpuP95Ms;
            public int gpuTimingSamples;
            public int slowestQuarter;
            public float slowestQuarterP95Ms;
            public float maximumRoadOffsetMetres;
            public float minimumCarHeightMetres;
            public float maximumCarHeightMetres;
            public long allocatedMemoryStartBytes;
            public long allocatedMemoryAfterWarmupBytes;
            public long allocatedMemoryEndBytes;
            public long allocatedMemoryPeakBytes;
            public long residentMemoryStartBytes;
            public long residentMemoryAfterWarmupBytes;
            public long residentMemoryEndBytes;
            public long residentMemoryPeakBytes;
            public int errors;
            public string status;
        }

        private readonly List<float> frameMs = new List<float>(10000);
        private readonly List<float> mainMs = new List<float>(10000);
        private readonly List<float> renderMs = new List<float>(10000);
        private readonly List<float> gpuMs = new List<float>(10000);
        private readonly List<float>[] quarterMs =
        {
            new List<float>(), new List<float>(), new List<float>(), new List<float>()
        };
        private readonly FrameTiming[] frameTiming = new FrameTiming[1];
        private ProfilerRecorder residentMemory;
        private ArcadeCarController car;
        private RouteProgress route;
        private Result result;
        private string outputPath;
        private double beginning;
        private double lastHeartbeat;
        private double firstLapElapsed;
        private float nextMemorySample;
        private float durationSeconds;
        private bool reliability;
        private bool finished;
        private bool warmupCompleted;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void CreateFromCommandLine()
        {
            string[] args = Environment.GetCommandLineArgs();
            if (Array.IndexOf(args, "-alabama-benchmark") < 0 &&
                Array.IndexOf(args, "-alabama-reliability") < 0) return;
            new GameObject("Player qualification").AddComponent<PlayerQualification>();
        }

        private void Start()
        {
            string[] args = Environment.GetCommandLineArgs();
            reliability = Array.IndexOf(args, "-alabama-reliability") >= 0;
            outputPath = ArgumentValue(args, "-alabama-output");
            if (string.IsNullOrEmpty(outputPath) || !Path.IsPathRooted(outputPath))
                throw new ArgumentException("-alabama-output needs an absolute JSON file path.");
            durationSeconds = reliability && float.TryParse(ArgumentValue(args, "-alabama-duration-seconds"),
                System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out float parsed)
                ? parsed : 1800;
            car = FindFirstObjectByType<ArcadeCarController>();
            route = FindFirstObjectByType<RouteProgress>();
            if (car == null || route == null) throw new InvalidOperationException("Qualification requires the district loop.");
            car.GetComponent<VehicleInput>().enabled = false;
            QualitySettings.vSyncCount = 0;
            int.TryParse(ArgumentValue(args, "-alabama-cap"), out int requestedCap);
            Application.targetFrameRate = reliability ? 60 : requestedCap > 0 ? requestedCap : -1;
            Application.runInBackground = true;
            Screen.SetResolution(1920, 1080, FullScreenMode.Windowed);
            residentMemory = ProfilerRecorder.StartNew(ProfilerCategory.Memory, "System Used Memory");
            Application.logMessageReceived += OnLog;
            beginning = Time.realtimeSinceStartupAsDouble;
            lastHeartbeat = 0;
            result = new Result
            {
                mode = reliability ? "reliability" : "benchmark",
                utc = DateTime.UtcNow.ToString("O"),
                unityVersion = Application.unityVersion,
                cpu = SystemInfo.processorType,
                gpu = SystemInfo.graphicsDeviceName,
                graphicsApi = SystemInfo.graphicsDeviceType.ToString(),
                systemMemoryMb = SystemInfo.systemMemorySize,
                targetFrameRate = Application.targetFrameRate,
                vSyncCount = QualitySettings.vSyncCount,
                routeLengthMetres = route.LengthMetres,
                minimumCarHeightMetres = float.PositiveInfinity,
                maximumCarHeightMetres = float.NegativeInfinity,
                allocatedMemoryStartBytes = Profiler.GetTotalAllocatedMemoryLong(),
                residentMemoryStartBytes = ResidentBytes()
            };
            result.allocatedMemoryPeakBytes = result.allocatedMemoryStartBytes;
            result.residentMemoryPeakBytes = result.residentMemoryStartBytes;
            Debug.Log($"Alabama {result.mode} started; output={outputPath}");
        }

        private void Update()
        {
            if (finished || result == null) return;
            car.SetCommand(RouteFollower.Command(car, route));
            double elapsed = Time.realtimeSinceStartupAsDouble - beginning;
            if (!warmupCompleted && route.CompletedLaps >= 1)
            {
                warmupCompleted = true;
                firstLapElapsed = elapsed;
                result.allocatedMemoryAfterWarmupBytes = Profiler.GetTotalAllocatedMemoryLong();
                result.residentMemoryAfterWarmupBytes = ResidentBytes();
            }
            bool measuring = reliability || route.CompletedLaps >= 1;
            if (measuring)
            {
                float ms = Time.unscaledDeltaTime * 1000;
                if (ms > 0 && ms < 1000)
                {
                    frameMs.Add(ms);
                    int quarter = Mathf.Clamp(Mathf.FloorToInt(route.Fraction * 4), 0, 3);
                    quarterMs[quarter].Add(ms);
                }
                if (FrameTimingManager.GetLatestTimings(1, frameTiming) > 0)
                {
                    AddPositive(mainMs, frameTiming[0].cpuMainThreadFrameTime);
                    AddPositive(renderMs, frameTiming[0].cpuRenderThreadFrameTime);
                    AddPositive(gpuMs, frameTiming[0].gpuFrameTime);
                }
                result.maximumRoadOffsetMetres = Mathf.Max(result.maximumRoadOffsetMetres, route.DistanceFromRoadMetres);
                result.minimumCarHeightMetres = Mathf.Min(result.minimumCarHeightMetres, car.Body.position.y);
                result.maximumCarHeightMetres = Mathf.Max(result.maximumCarHeightMetres, car.Body.position.y);
            }
            if (elapsed >= nextMemorySample)
            {
                result.allocatedMemoryPeakBytes = Math.Max(result.allocatedMemoryPeakBytes, Profiler.GetTotalAllocatedMemoryLong());
                result.residentMemoryPeakBytes = Math.Max(result.residentMemoryPeakBytes, ResidentBytes());
                nextMemorySample += 5;
            }
            if (reliability && elapsed - lastHeartbeat >= 60)
            {
                Debug.Log($"Alabama reliability progress: {elapsed:0} s, laps={route.CompletedLaps}, " +
                    $"roadOffset={result.maximumRoadOffsetMetres:0.0} m, errors={result.errors}");
                lastHeartbeat = elapsed;
            }
            if (reliability ? elapsed >= durationSeconds : route.CompletedLaps >= 2 || elapsed >= 180)
                Finish(elapsed);
        }

        private void LateUpdate()
        {
            if (!finished) FrameTimingManager.CaptureFrameTimings();
        }

        private void Finish(double elapsed)
        {
            finished = true;
            car.SetCommand(default);
            string status = !Application.isBatchMode && Screen.width == 1920 && Screen.height == 1080 &&
                gpuMs.Count > 0 && result.errors == 0 && result.maximumRoadOffsetMetres < 14 &&
                result.minimumCarHeightMetres > -.3f && result.maximumCarHeightMetres < 1.8f &&
                (reliability ? route.CompletedLaps >= Mathf.FloorToInt(durationSeconds / 90f) : route.CompletedLaps >= 2)
                ? "passed" : "failed";
            Write(status, elapsed);
            Debug.Log($"Alabama {result.mode} {status}: {result.measuredFrames} frames, p95={result.frameP95Ms:0.00} ms, errors={result.errors}");
            Application.Quit(status == "passed" ? 0 : 1);
        }

        private void Write(string status, double elapsed)
        {
            result.status = status;
            result.elapsedWallSeconds = (float)elapsed;
            result.completedLaps = route.CompletedLaps;
            result.measuredLapSeconds = !reliability && route.CompletedLaps >= 2
                ? (float)(elapsed - firstLapElapsed) : 0;
            result.width = Screen.width;
            result.height = Screen.height;
            result.measuredFrames = frameMs.Count;
            result.meanFps = frameMs.Count > 0 ? frameMs.Count * 1000f / Sum(frameMs) : 0;
            result.frameP50Ms = Percentile(frameMs, .5f);
            result.frameP95Ms = Percentile(frameMs, .95f);
            result.frameP99Ms = Percentile(frameMs, .99f);
            result.frameMaxMs = Percentile(frameMs, 1);
            result.cpuMainP95Ms = Percentile(mainMs, .95f);
            result.cpuRenderP95Ms = Percentile(renderMs, .95f);
            result.gpuP95Ms = Percentile(gpuMs, .95f);
            result.gpuTimingSamples = gpuMs.Count;
            result.slowestQuarter = 0;
            result.slowestQuarterP95Ms = 0;
            for (int i = 0; i < quarterMs.Length; i++)
            {
                float p95 = Percentile(quarterMs[i], .95f);
                if (p95 > result.slowestQuarterP95Ms)
                {
                    result.slowestQuarter = i + 1;
                    result.slowestQuarterP95Ms = p95;
                }
            }
            result.allocatedMemoryEndBytes = Profiler.GetTotalAllocatedMemoryLong();
            result.residentMemoryEndBytes = ResidentBytes();
            result.allocatedMemoryPeakBytes = Math.Max(result.allocatedMemoryPeakBytes, result.allocatedMemoryEndBytes);
            result.residentMemoryPeakBytes = Math.Max(result.residentMemoryPeakBytes, result.residentMemoryEndBytes);
            Directory.CreateDirectory(Path.GetDirectoryName(outputPath));
            File.WriteAllText(outputPath, JsonUtility.ToJson(result, true));
        }

        private static string ArgumentValue(string[] args, string name)
        {
            int index = Array.IndexOf(args, name);
            return index >= 0 && index + 1 < args.Length ? args[index + 1] : null;
        }

        private static void AddPositive(List<float> values, double value)
        {
            if (value > 0 && value < 1000) values.Add((float)value);
        }

        private long ResidentBytes() => residentMemory.Valid ? residentMemory.CurrentValue : 0;

        private static float Sum(List<float> values)
        {
            float sum = 0;
            foreach (float value in values) sum += value;
            return sum;
        }

        private static float Percentile(List<float> values, float fraction)
        {
            if (values.Count == 0) return 0;
            var sorted = values.ToArray();
            Array.Sort(sorted);
            return sorted[Mathf.Clamp(Mathf.CeilToInt(fraction * sorted.Length) - 1, 0, sorted.Length - 1)];
        }

        private void OnLog(string condition, string stackTrace, LogType type)
        {
            if (result != null &&
                (type == LogType.Error || type == LogType.Exception || type == LogType.Assert)) result.errors++;
        }

        private void OnDestroy()
        {
            Application.logMessageReceived -= OnLog;
            if (residentMemory.Valid) residentMemory.Dispose();
        }
    }
}
