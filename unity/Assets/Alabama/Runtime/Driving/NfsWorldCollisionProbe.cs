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
            string Argument(string key) => args[Array.IndexOf(args, key)+1];
            var plan = JsonUtility.FromJson<Plan>(File.ReadAllText(Argument("-nfs-collision-probe")));
            string output = Argument("-nfs-stability-output"); Directory.CreateDirectory(output);
            var runtime = DistrictRuntime.Instance; var car = runtime.Player;
            var guard = car.GetComponent<DistrictBoundaryGuard>();
            car.GetComponent<VehicleInput>().enabled = false; car.SetCommand(default);
            Application.runInBackground = true; Application.targetFrameRate = 60;
            Screen.SetResolution(1920, 1080, FullScreenMode.Windowed);
            var results = new System.Collections.Generic.List<Result>();
            foreach (var route in plan.routes)
            {
                car.Body.position = route.position; car.Body.rotation = Quaternion.LookRotation(route.reverse ? -route.direction : route.direction);
                car.Body.linearVelocity = Vector3.zero; car.Body.angularVelocity = Vector3.zero;
                guard.ResetHistory(); Physics.SyncTransforms(); runtime.Chase.SnapToTarget();
                for (int i = 0; i < 12; i++) yield return new WaitForFixedUpdate();
                var start = car.Body.position;
                contacts = 0; probeActive = true; int blocked = guard.BlockedMoves;
                var result = new Result { label = route.label, supported = true };
                car.Body.linearVelocity = route.direction * 25;
                for (int i = 0; i < 120; i++)
                {
                    yield return new WaitForFixedUpdate();
                    float progress = Vector3.Dot(car.Body.position - start, route.direction);
                    result.maximumProgress = Mathf.Max(result.maximumProgress, progress);
                    bool supported = guard.ContainsFootprint(car.Body.position, car.Body.rotation,
                        scene => runtime.LoadedDistricts.Any(d => d.gameObject.scene == scene));
                    if (result.supported && !supported) result.firstUnsupportedPosition = car.Body.position;
                    result.supported &= supported;
                }
                probeActive = false; result.contacts = contacts; result.blockedMoves = guard.BlockedMoves-blocked;
                result.finalProgress = Vector3.Dot(car.Body.position-start, route.direction);
                result.finalPosition = car.Body.position;
                result.passed = result.supported && result.maximumProgress > .5f &&
                    result.maximumProgress < route.maximumProgress &&
                    (route.boundary ? result.blockedMoves > 0 : result.contacts > 0);
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
