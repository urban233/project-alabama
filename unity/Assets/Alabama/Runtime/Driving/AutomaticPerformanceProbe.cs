using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Alabama.Districts;
using UnityEngine;

namespace Alabama.Driving
{
    /// <summary>Explicit native qualification of real driving and automatic load/recovery decisions.</summary>
    public sealed class AutomaticPerformanceProbe : MonoBehaviour
    {
        [Serializable] private sealed class Sample
        {
            public string phase;
            public float seconds, frameMs, speed, scale;
            public int cap, tier;
            public bool supported;
        }
        [Serializable] private sealed class Report
        {
            public string gpu, cpu, method;
            public bool automaticActive, choseThirtyUnderLoad, returnedToSixty, supported, tuningUnchanged, passed;
            public int decisions;
            public Sample[] samples;
        }
        private bool controlledLoad;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Create()
        {
            if (!Application.isEditor && Environment.GetCommandLineArgs().Contains("-nfs-auto-review"))
                new GameObject("Automatic performance review").AddComponent<AutomaticPerformanceProbe>();
        }

        private void LateUpdate()
        {
            // Only this opt-in review simulates a weaker/busy CPU. Never installed during normal play.
            if (controlledLoad) System.Threading.Thread.Sleep(28);
        }

        private IEnumerator Start()
        {
            var args = Environment.GetCommandLineArgs(); int index = Array.IndexOf(args,"-nfs-stability-output");
            if (index < 0 || index+1 >= args.Length || !Path.IsPathRooted(args[index+1]))
                throw new ArgumentException("Automatic review requires an absolute output directory.");
            string output = args[index+1]; Directory.CreateDirectory(output);
            Application.runInBackground = true;
            var runtime = DistrictRuntime.Instance;
            while (runtime == null || !runtime.Ready)
            { yield return null; runtime = DistrictRuntime.Instance; }
            var automatic = FindFirstObjectByType<AutomaticPerformance>();
            if (automatic == null) throw new InvalidOperationException("Automatic performance did not start.");
            var car = runtime.Player; car.GetComponent<VehicleInput>().enabled = false;
            var guard = car.GetComponent<DistrictBoundaryGuard>();
            var wheels = car.GetComponentsInChildren<WheelCollider>();
            var roads = FindObjectsByType<MeshCollider>(FindObjectsSortMode.None).Where(c => c.transform.root.name == "RoadsPhysical").ToArray();
            var routes = new[] { new Vector3(542.82843f,-3.94928f,765.09351f),
                new Vector3(273.88864f,26.03149f,1120.71887f), new Vector3(-89.19291f,22.263f,-551.47f) };
            var headings = new[] { 310f,115f,80f };
            float fixedStep = Time.fixedDeltaTime, topSpeed = car.Tuning.MaximumSpeedMetresPerSecond, torque = car.Tuning.DriveTorque;
            var report = new Report { automaticActive = true, supported = true,
                gpu = SystemInfo.graphicsDeviceName, cpu = SystemInfo.processorType,
                method = "Real rendered driving: 20s baseline, 45s opt-in 28ms CPU load, 40s recovery. Load phase is a controller test, not a hardware benchmark." };
            var samples = new List<Sample>();
            double start = Time.realtimeSinceStartupAsDouble;
            int route = 0;
            while (Time.realtimeSinceStartupAsDouble-start < 105)
            {
                var direction = Quaternion.Euler(0,headings[route],0)*Vector3.forward;
                car.Body.position = routes[route]+Vector3.up*.24f; car.Body.rotation = Quaternion.LookRotation(direction);
                car.Body.linearVelocity = Vector3.zero; car.Body.angularVelocity = Vector3.zero;
                car.SetCommand(default); guard.ResetHistory(); Physics.SyncTransforms(); runtime.Chase.SnapToTarget();
                car.enabled = false;
                foreach (var wheel in wheels) { wheel.motorTorque = 0; wheel.brakeTorque = 10000; }
                for (float settle = 0; settle < .25f; settle += Time.fixedDeltaTime) yield return new WaitForFixedUpdate();
                foreach (var wheel in wheels) wheel.brakeTorque = 0;
                car.enabled = true;
                var ahead = routes[route]+direction*2;
                foreach (var road in roads)
                    if (road.Raycast(new Ray(ahead+Vector3.up*3,Vector3.down),out var hit,6)) { ahead = hit.point; break; }
                car.Body.linearVelocity = (ahead-routes[route]).normalized*35;
                car.SetCommand(new VehicleCommand(.4f,0,0,false));
                double driveStart = Time.realtimeSinceStartupAsDouble;
                while (Time.realtimeSinceStartupAsDouble-driveStart < 2 && Time.realtimeSinceStartupAsDouble-start < 105)
                {
                    yield return new WaitForEndOfFrame();
                    float seconds = (float)(Time.realtimeSinceStartupAsDouble-start);
                    controlledLoad = seconds >= 20 && seconds < 65;
                    var sample = new Sample { phase = seconds < 20 ? "baseline" : controlledLoad ? "controlled-load" : "recovery",
                        seconds = seconds, frameMs = Time.unscaledDeltaTime*1000, speed = car.SpeedMetresPerSecond,
                        cap = automatic.TargetFrameRate, tier = automatic.QualityTier, scale = automatic.RenderScale,
                        supported = guard.ContainsFootprint(car.Body.position,car.Body.rotation,runtime.OwnsCollision) };
                    samples.Add(sample); report.supported &= sample.supported;
                    report.choseThirtyUnderLoad |= controlledLoad && sample.cap == 30;
                    report.returnedToSixty |= seconds > 65 && sample.cap == 60 && report.choseThirtyUnderLoad;
                    yield return null;
                }
                route = (route+1)%routes.Length;
            }
            controlledLoad = false; car.SetCommand(default);
            report.decisions = automatic.Decisions; report.samples = samples.ToArray();
            report.tuningUnchanged = Time.fixedDeltaTime == fixedStep && car.Tuning.MaximumSpeedMetresPerSecond == topSpeed && car.Tuning.DriveTorque == torque;
            report.passed = report.automaticActive && report.choseThirtyUnderLoad && report.returnedToSixty && report.supported && report.tuningUnchanged;
            File.WriteAllText(Path.Combine(output,"automatic-results.json"),JsonUtility.ToJson(report,true));
            Application.Quit(report.passed ? 0 : 1);
        }
    }
}
