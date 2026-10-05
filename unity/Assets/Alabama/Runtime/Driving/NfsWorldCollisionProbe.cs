using System;
using System.Collections;
using System.IO;
using System.Linq;
using Alabama.Districts;
using UnityEngine;

namespace Alabama.Driving
{
    /// <summary>Explicit native collision checks using the real car and loaded map.</summary>
    public sealed class NfsWorldCollisionProbe : MonoBehaviour
    {
        [Serializable] public sealed class Route
        {
            public string label;
            public Vector3 position, direction;
            public float maximumProgress;
            public bool boundary, reverse;
        }
        [Serializable] public sealed class Plan { public Route[] routes; }
        [Serializable] private sealed class Result
        {
            public string label;
            public float maximumProgress, finalProgress;
            public int blockedMoves, contacts;
            public bool supported, passed;
            public Vector3 finalPosition, firstUnsupportedPosition;
            public Vector3 firstUnsupportedRotation, firstUnsupportedVelocity;
            public string unsupportedGround;
            public float escapeProgress, escapeDrivenMetres, escapeTurnDegrees, localResetDistance;
            public float escapeDisplacement;
            public float maximumRecoveryStep;
            public int localRecoveries, recoveryPoses;
            public string wheelContacts;
            public bool escaped;
            public int airborneFrames;
            public bool resetOnDesignatedRoad;
        }
        [Serializable] private sealed class Report { public Result[] results; public bool passed; }
        private int contacts;
        private bool probeActive;
        private void OnCollisionEnter(Collision collision)
        {
            if (probeActive && collision.contacts.Any(c => Mathf.Abs(c.normal.y) < .75f)) contacts++;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Create()
        {
            if (!Application.isEditor && Environment.GetCommandLineArgs().Contains("-nfs-collision-probe"))
                new GameObject("Native scenery collision review").AddComponent<NfsWorldCollisionProbe>();
        }
        private IEnumerator Start()
        {
            // The bootstrap starts the routine on the car so it receives physical contact callbacks.
            if (GetComponent<ArcadeCarController>() == null)
            {
                while (DistrictRuntime.Instance == null || !DistrictRuntime.Instance.Ready) yield return null;
                DistrictRuntime.Instance.Player.gameObject.AddComponent<NfsWorldCollisionProbe>();
                Destroy(gameObject); yield break;
            }
            var args = Environment.GetCommandLineArgs();
            bool checkRecovery = args.Contains("-nfs-collision-recovery");
            string Argument(string key) => args[Array.IndexOf(args, key)+1];
            var plan = JsonUtility.FromJson<Plan>(File.ReadAllText(Argument("-nfs-collision-probe")));
            string output = Argument("-nfs-stability-output"); Directory.CreateDirectory(output);
            var runtime = DistrictRuntime.Instance; var car = runtime.Player;
            var guard = car.GetComponent<DistrictBoundaryGuard>();
            var wheels = car.GetComponentsInChildren<WheelCollider>();
            car.GetComponent<VehicleInput>().enabled = false; car.SetCommand(default);
            Application.runInBackground = true; Application.targetFrameRate = 60;
            Screen.SetResolution(1920, 1080, FullScreenMode.Windowed);
            var results = new System.Collections.Generic.List<Result>();
            foreach (var route in plan.routes)
            {
                while (!runtime.Ready && runtime.LastFailure == null) yield return null;
                if (runtime.LastFailure != null) throw new InvalidOperationException(runtime.LastFailure);
                // Preloading must not settle/drive the car before the fixed collision setup.
                car.Body.isKinematic = true;
                car.Body.position = route.position; car.Body.rotation = Quaternion.LookRotation(route.reverse ? -route.direction : route.direction);
                car.Body.linearVelocity = Vector3.zero; car.Body.angularVelocity = Vector3.zero;
                guard.ResetHistory(); Physics.SyncTransforms(); runtime.Chase.SnapToTarget();
                yield return runtime.PrepareVisuals(route.position);
                car.Body.isKinematic = false;
                // Remove wheel RPM/suspension state left by the previous escape drive.
                car.enabled = false;
                foreach (var wheel in wheels) { wheel.motorTorque = 0; wheel.brakeTorque = 10000; }
                for (float elapsed = 0; elapsed < .5f; elapsed += Time.fixedDeltaTime)
                    yield return new WaitForFixedUpdate();
                foreach (var wheel in wheels) wheel.brakeTorque = 0;
                car.enabled = true;
                var start = car.Body.position;
                contacts = 0; probeActive = true; int blocked = guard.BlockedMoves;
                var result = new Result { label = route.label, supported = true };
                car.Body.linearVelocity = route.direction * 25;
                for (int i = 0; i < 120; i++)
                {
                    yield return new WaitForFixedUpdate();
                    float progress = Vector3.Dot(car.Body.position - start, route.direction);
                    result.maximumProgress = Mathf.Max(result.maximumProgress, progress);
                    bool airborne = !wheels.Any(w => w.GetGroundHit(out _));
                    if (airborne) result.airborneFrames++;
                    bool supported = guard.ContainsFootprint(car.Body.position, car.Body.rotation,
                        scene => runtime.LoadedDistricts.Any(d => d.gameObject.scene == scene),
                        maximumDrop: airborne ? 12 : 3);
                    if (result.supported && !supported)
                    {
                        result.firstUnsupportedPosition = car.Body.position;
                        result.firstUnsupportedRotation = car.Body.rotation.eulerAngles;
                        result.firstUnsupportedVelocity = car.Body.linearVelocity;
                        var ground = new System.Text.StringBuilder();
                        var yaw = Quaternion.Euler(0,car.Body.rotation.eulerAngles.y,0);
                        foreach (float x in new[] { -.95f,.95f }) foreach (float z in new[] { -2.225f,2.225f })
                        {
                            var point = car.Body.position+yaw*new Vector3(x,0,z);
                            ground.AppendLine($"Corner {point}");
                            foreach (var hit in Physics.RaycastAll(point+Vector3.up*3,Vector3.down,15))
                                ground.AppendLine($"{hit.collider.name}: {hit.point}, normal {hit.normal}, coverage {hit.collider.GetComponentInParent<DistrictGroundCoverage>() != null}");
                        }
                        result.unsupportedGround = ground.ToString();
                    }
                    result.supported &= supported;
                }
                probeActive = false; result.contacts = contacts; result.blockedMoves = guard.BlockedMoves-blocked;
                result.finalProgress = Vector3.Dot(car.Body.position-start, route.direction);
                result.finalPosition = car.Body.position;
                result.passed = result.supported && result.maximumProgress > .5f &&
                    (route.boundary || result.maximumProgress < route.maximumProgress) &&
                    (route.boundary ? result.blockedMoves > 0 : result.contacts > 0);
                if (checkRecovery)
                {
                    var crash = car.Body.position; var heading = car.Body.rotation;
                    var previous = crash; int recovered = guard.LocalRecoveries;
                    for (float elapsed = 0; elapsed < 5; elapsed += Time.fixedDeltaTime)
                    {
                        // Back away first, then steer. Reapply input after an automatic unwedging.
                        // Impacts can spin the car: choose the gear that moves away from the approach.
                        bool forwardAway = Vector3.Dot(car.transform.forward,route.direction) < 0;
                        car.SetCommand(new VehicleCommand(forwardAway ? 1 : 0, forwardAway ? 0 : 1,
                            elapsed < 1 ? 0 : .3f, false));
                        yield return new WaitForFixedUpdate();
                        float travelled = Vector3.Distance(previous, car.Body.position);
                        if (travelled < 1) result.escapeDrivenMetres += travelled; // Exclude recovery teleports.
                        else result.maximumRecoveryStep = Mathf.Max(result.maximumRecoveryStep, travelled);
                        previous = car.Body.position;
                    }
                    car.SetCommand(default);
                    result.escapeProgress = Vector3.Dot(crash-car.Body.position, route.direction);
                    result.escapeDisplacement = Vector3.Distance(crash,car.Body.position);
                    result.escapeTurnDegrees = Quaternion.Angle(heading, car.Body.rotation);
                    result.localRecoveries = guard.LocalRecoveries-recovered;
                    result.recoveryPoses = guard.RecoveryPoseCount;
                    result.wheelContacts = string.Join(";", car.GetComponentsInChildren<WheelCollider>().Select(w =>
                        w.GetGroundHit(out var hit) ? hit.collider.name : "airborne"));
                    result.escaped = result.escapeDisplacement > 2 && result.escapeDrivenMetres > 2 &&
                        result.maximumRecoveryStep < 25 &&
                        guard.ContainsFootprint(car.Body.position, car.Body.rotation,
                            scene => runtime.LoadedDistricts.Any(d => d.gameObject.scene == scene));
                    var beforeReset = car.Body.position;
                    bool reset = runtime.ResetToNearestRoad();
                    result.localResetDistance = Vector3.Distance(beforeReset, car.Body.position);
                    result.resetOnDesignatedRoad = runtime.IsOnDesignatedRoad(car.Body.position);
                    result.escaped &= reset && (runtime.HasDesignatedRoads ? result.resetOnDesignatedRoad : result.localResetDistance < 10);
                    result.passed &= result.escaped;
                }
                results.Add(result);
                yield return new WaitForEndOfFrame();
                var capture = ScreenCapture.CaptureScreenshotAsTexture();
                File.WriteAllBytes(Path.Combine(output, route.label + ".png"), capture.EncodeToPNG()); Destroy(capture);
            }
            var report = new Report { results = results.ToArray(), passed = results.All(r => r.passed) };
            File.WriteAllText(Path.Combine(output, "results.json"), JsonUtility.ToJson(report, true));
            Application.Quit(report.passed ? 0 : 1);
        }
    }
}
