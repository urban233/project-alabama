using System;
using System.IO;
using System.Linq;
using Alabama.Driving;
using UnityEngine;

namespace Alabama.MicroSlice
{
    // Optional player-build probe. Ordinary play never enables automatic steering.
    public sealed class MicroSliceDriveProbe : MonoBehaviour
    {
        [SerializeField] private ArcadeCarController car;
        [SerializeField] private RouteProgress route;
        [SerializeField] private RouteProgress shortcutRoute;
        [SerializeField] private MicroSliceSession session;
        private bool active;
        private float started;
        private float maximumOffset;
        private float airborneTime;
        private float maximumAirborneTime;
        private float stoppedTime;
        private float nextDiagnostic;
        private int physicsSteps;
        private int readyFrame;
        private WheelCollider[] wheels;
        public void Configure(ArcadeCarController vehicle, RouteProgress progress, RouteProgress alternate, MicroSliceSession owner)
        { car = vehicle; route = progress; shortcutRoute = alternate; session = owner; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void EnableBackgroundProbe()
        {
            var args = Environment.GetCommandLineArgs();
            if (args.Contains("-microSliceProbe") || args.Contains("-microSliceShortcutProbe")) Application.runInBackground = true;
        }

        private void Start()
        {
            var args = Environment.GetCommandLineArgs();
            bool shortcut = args.Contains("-microSliceShortcutProbe");
            active = shortcut || args.Contains("-microSliceProbe");
            if (!active) return;
            car.GetComponent<VehicleInput>().enabled = false;
            wheels = car.GetComponentsInChildren<WheelCollider>();
            if (shortcut)
            {
                route.gameObject.SetActive(false);
                shortcutRoute.gameObject.SetActive(true);
                route = shortcutRoute;
                float beforeStart = route.LengthMetres - 8;
                car.Body.position = route.PositionAt(beforeStart) + Vector3.up * .4f;
                car.Body.rotation = Quaternion.LookRotation(route.DirectionAt(beforeStart),Vector3.up);
                car.Body.linearVelocity = Vector3.zero;
                car.Body.angularVelocity = Vector3.zero;
                Physics.SyncTransforms();
                route.ResetProgress();
            }
            // Let the route tracker observe the spawn before collecting measurements.
            readyFrame = Time.frameCount + 2;
            started = -1;
        }
        private void FixedUpdate()
        {
            if (!active || Time.frameCount < readyFrame) return;
            if (started < 0) started = Time.time;
            car.SetCommand(RouteFollower.Command(car, route));
            maximumOffset = Mathf.Max(maximumOffset, route.DistanceFromRoadMetres);
            int grounded = wheels.Count(w => w.isGrounded);
            airborneTime = grounded == 0 ? airborneTime + Time.fixedDeltaTime : 0;
            maximumAirborneTime = Mathf.Max(maximumAirborneTime, airborneTime);
            physicsSteps++;
            stoppedTime = car.SpeedMetresPerSecond < .5f ? stoppedTime + Time.fixedDeltaTime : 0;
            if (Time.time - started >= nextDiagnostic)
            {
                nextDiagnostic += 15;
                Debug.Log($"Micro-slice probe: t={Time.time-started:0.0}, gate={session.Laps.NextCheckpoint}, route={route.DistanceMetres:0.0}, position={car.Body.position}, speed={car.SpeedMetresPerSecond:0.0}");
            }
            if (session.Laps.CompletedLaps < 1 && Time.time - started < 180 && stoppedTime < 15) return;
            bool passed = session.Laps.CompletedLaps >= 1 && maximumOffset < 12 && maximumAirborneTime < 3;
            var report = new Report {
                passed = passed, seconds = Time.time - started, lapSeconds = session.Laps.LastLapSeconds,
                routeMetres = route.LengthMetres, maximumOffsetMetres = maximumOffset,
                maximumAirborneSeconds = maximumAirborneTime, physicsSteps = physicsSteps,
                nextCheckpoint = session.Laps.NextCheckpoint, position = car.Body.position, routeDistanceMetres = route.DistanceMetres
            };
            var args = Environment.GetCommandLineArgs();
            int outputIndex = Array.IndexOf(args, "-microSliceProbeOutput");
            string output = outputIndex >= 0 && outputIndex + 1 < args.Length ? args[outputIndex + 1] : Path.Combine(Application.persistentDataPath, "micro-slice-probe.json");
            Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(output)));
            File.WriteAllText(output, JsonUtility.ToJson(report, true));
            Debug.Log("Micro-slice driving probe: " + JsonUtility.ToJson(report));
            active = false;
            Application.Quit(passed ? 0 : 1);
        }
        [Serializable] private sealed class Report
        {
            public bool passed;
            public float seconds, lapSeconds, routeMetres, maximumOffsetMetres, maximumAirborneSeconds;
            public int physicsSteps;
            public int nextCheckpoint;
            public Vector3 position;
            public float routeDistanceMetres;
        }
    }
}
