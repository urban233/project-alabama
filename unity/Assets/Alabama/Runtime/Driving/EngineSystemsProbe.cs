using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Alabama.Districts;
using UnityEngine;

namespace Alabama.Driving
{
    /// <summary>Opt-in native measurements; no captures or synthetic frame time during sampling.</summary>
    public sealed class EngineSystemsProbe : MonoBehaviour
    {
        [Serializable] private sealed class RouteResult
        {
            public int pass, route, frames, missingVisibleFrames, unsupportedFrames, residentCells, loadedCells;
            public float prepareSeconds, p95FrameMs, maximumFrameMs, distance, resetDistance;
            public bool resetOnRoad;
        }
        [Serializable] private sealed class Report
        {
            public string gpu, cpu, method, failure;
            public double engineReadySeconds, corePreloadSeconds, districtReadySeconds;
            public double initialSceneSeconds, initialScenerySeconds;
            public int totalCells, failedCells, loadedCells;
            public float maximumCellLoadSeconds;
            public bool tuningUnchanged, passed;
            public RouteResult[] routes;
        }
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Create()
        {
            if (!Application.isEditor && Environment.GetCommandLineArgs().Contains("-systems-review"))
                new GameObject("Native engine systems review").AddComponent<EngineSystemsProbe>();
        }
        private IEnumerator Start()
        {
            var args = Environment.GetCommandLineArgs(); int index = Array.IndexOf(args,"-systems-output");
            if (index < 0 || index+1 >= args.Length || !Path.IsPathRooted(args[index+1]))
                throw new ArgumentException("Systems review needs an absolute -systems-output directory.");
            string output = args[index+1]; Directory.CreateDirectory(output);
            Application.runInBackground = true;
            var report = new Report { gpu = SystemInfo.graphicsDeviceName, cpu = SystemInfo.processorType,
                method = "Warm-cache native 1920x1080; real frame time including frame pacing; six first/repeat 3-second physical drives at an initial 35m/s; no capture overhead in frame samples." };
            var runtime = DistrictRuntime.Instance; float deadline = Time.realtimeSinceStartup+180;
            while (runtime == null || !runtime.Ready)
            {
                if (runtime != null && runtime.LastFailure != null || Time.realtimeSinceStartup > deadline)
                {
                    report.failure = runtime == null ? "Runtime did not start." : runtime.LastFailure ?? "Startup timed out.";
                    Write(output,report); Application.Quit(1); yield break;
                }
                yield return null; runtime = DistrictRuntime.Instance;
            }
            yield return null; // Bootstrap publishes its timings after district readiness.
            report.engineReadySeconds = GameStartup.ReadySeconds;
            report.corePreloadSeconds = GameStartup.PreloadSeconds;
            report.districtReadySeconds = runtime.InitialContentSeconds;
            report.initialSceneSeconds = runtime.InitialSceneSeconds;
            report.initialScenerySeconds = runtime.InitialVisualsSeconds;
            var car = runtime.Player; var guard = car.GetComponent<DistrictBoundaryGuard>();
            car.GetComponent<VehicleInput>().enabled = false;
            var streamers = FindObjectsByType<DistrictVisualStreamer>(FindObjectsSortMode.None);
            var roadCollision = FindObjectsByType<MeshCollider>(FindObjectsSortMode.None)
                .Where(c => c.transform.root.name == "RoadsPhysical").ToArray();
            float torque = car.Tuning.DriveTorque, speedLimit = car.Tuning.MaximumSpeedMetresPerSecond, fixedStep = Time.fixedDeltaTime;
            var origins = new[] { new Vector3(542.82843f,-3.94928f,765.09351f),
                new Vector3(273.88864f,26.03149f,1120.71887f), new Vector3(-89.19291f,22.263f,-551.47f) };
            var headings = new[] { 310f,115f,80f }; var results = new List<RouteResult>();
            Screen.SetResolution(1920,1080,FullScreenMode.Windowed);
            for (int pass = 0; pass < 2; pass++) for (int route = 0; route < origins.Length; route++)
            {
                car.SetCommand(default); car.Body.linearVelocity = car.Body.angularVelocity = Vector3.zero;
                car.Body.isKinematic = true; car.Body.position = origins[route]+Vector3.up*.24f;
                car.Body.rotation = Quaternion.Euler(0,headings[route],0); Physics.SyncTransforms(); runtime.Chase.SnapToTarget();
                float prepare = Time.realtimeSinceStartup;
                yield return runtime.PrepareVisuals(car.Body.position);
                var result = new RouteResult { pass = pass,route = route,prepareSeconds = Time.realtimeSinceStartup-prepare };
                car.Body.isKinematic = false; guard.ResetHistory();
                for (int frame = 0; frame < 12; frame++) yield return new WaitForFixedUpdate();
                var start = car.Body.position; var direction = car.transform.forward;
                var ahead = start+direction*2;
                foreach (var collider in roadCollision)
                    if (collider.Raycast(new Ray(ahead+Vector3.up*4,Vector3.down),out var hit,8))
                    { ahead.y = hit.point.y+.24f; break; }
                car.Body.linearVelocity = (ahead-start).normalized*35;
                car.SetCommand(new VehicleCommand(.25f,0,0,false));
                float began = Time.realtimeSinceStartup; var frames = new List<float>();
                while (Time.realtimeSinceStartup-began < 3)
                {
                    yield return null;
                    frames.Add(Time.unscaledDeltaTime*1000);
                    if (streamers.Any(s => s.MissingVisibleCells(runtime.Chase.transform.position) != 0)) result.missingVisibleFrames++;
                    if (!guard.ContainsFootprint(car.Body.position,car.Body.rotation,
                        scene => runtime.LoadedDistricts.Any(d => d.gameObject.scene == scene))) result.unsupportedFrames++;
                }
                frames.Sort(); result.frames = frames.Count;
                result.p95FrameMs = frames[Mathf.Clamp(Mathf.CeilToInt(frames.Count*.95f)-1,0,frames.Count-1)];
                result.maximumFrameMs = frames[frames.Count-1]; result.distance = Vector3.Distance(start,car.Body.position);
                result.residentCells = streamers.Sum(s => s.ResidentCount); result.loadedCells = streamers.Sum(s => s.LoadCount);
                var before = car.Body.position; car.SetCommand(default);
                result.resetOnRoad = runtime.ResetToNearestRoad() && runtime.IsOnDesignatedRoad(car.Body.position);
                result.resetDistance = Vector3.Distance(before,car.Body.position); results.Add(result);
                while (!runtime.Ready && runtime.LastFailure == null) yield return null;
                Debug.Log($"Systems route {pass}/{route}: p95={result.p95FrameMs:F2}ms, missing={result.missingVisibleFrames}, support failures={result.unsupportedFrames}, road reset={result.resetOnRoad}");
            }
            report.routes = results.ToArray(); report.totalCells = streamers.Sum(s => s.CellCount);
            report.failedCells = streamers.Sum(s => s.FailedCount); report.loadedCells = streamers.Sum(s => s.LoadCount);
            report.maximumCellLoadSeconds = streamers.Length == 0 ? 0 : streamers.Max(s => s.MaximumLoadSeconds);
            report.tuningUnchanged = torque == car.Tuning.DriveTorque && speedLimit == car.Tuning.MaximumSpeedMetresPerSecond && fixedStep == Time.fixedDeltaTime;
            report.passed = report.failedCells == 0 && report.tuningUnchanged && runtime.HasDesignatedRoads &&
                results.All(r => r.resetOnRoad && r.unsupportedFrames == 0 && r.missingVisibleFrames == 0 && r.distance > 80);
            Write(output,report); Application.Quit(report.passed ? 0 : 1);
        }
        private static void Write(string output, Report report)
            => File.WriteAllText(Path.Combine(output,"systems-results.json"),JsonUtility.ToJson(report,true));
    }
}
