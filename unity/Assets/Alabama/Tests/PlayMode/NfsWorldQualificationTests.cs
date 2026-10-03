#if UNITY_EDITOR
using System.Collections;
using System.IO;
using System.Linq;
using Alabama.Driving;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Alabama.Tests
{
    public sealed class NfsWorldQualificationTests
    {
        private const string Scene = "Assets/Alabama/Art/Maps/NfsWorld/Scenes/NfsWorldArtPass.unity";

        private static bool Road(Vector3 probe, out Vector3 point)
        {
            var hits = Physics.RaycastAll(probe + Vector3.up * 5, Vector3.down, 10)
                .Where(hit => hit.collider.transform.root.name == "RoadsPhysical" && hit.normal.y > .5f)
                .OrderBy(hit => Mathf.Abs(hit.point.y - probe.y)).ToArray();
            point = hits.Length == 0 ? probe : hits[0].point;
            return hits.Length > 0 && Mathf.Abs(point.y - probe.y) < 3;
        }

        private static bool Corridor(Vector3 origin, out Vector3 start, out Vector3 forward)
        {
            start = origin; forward = Vector3.forward;
            for (int angle = 0; angle < 360; angle += 5)
            {
                var direction = Quaternion.Euler(0, angle, 0) * Vector3.forward;
                var right = Vector3.Cross(Vector3.up, direction);
                var previous = origin;
                bool clear = Road(origin, out var ground);
                for (int distance = 5; clear && distance <= 120; distance += 5)
                {
                    var probe = origin + direction * distance; probe.y = previous.y;
                    clear = Road(probe, out var centre) && Road(centre + right * 1.2f, out var left) &&
                        Road(centre - right * 1.2f, out var other) && Mathf.Abs(left.y - centre.y) < .4f && Mathf.Abs(other.y - centre.y) < .4f;
                    previous = centre;
                }
                if (!clear) continue;
                var obstacles = Physics.BoxCastAll(ground + Vector3.up, new Vector3(1, .65f, 2.4f), direction,
                    Quaternion.LookRotation(direction), 100).Where(hit =>
                        hit.collider.transform.root.name != "RoadsPhysical" && hit.collider.transform.root.name != "E46 driver car");
                if (obstacles.Any()) continue;
                start = ground; forward = direction; return true;
            }
            return false;
        }

        // Allow cold private-map loading while retaining every driving assertion.
        [Timeout(1200000)]
        [UnityTest]
        public IEnumerator RepresentativeRoadsSupportHighSpeedWheelContact()
        {
            if (!File.Exists(Scene)) Assert.Ignore("Local converted map assets are absent.");
            yield return EditorSceneManager.LoadSceneInPlayMode(Scene, new LoadSceneParameters(LoadSceneMode.Single));
            var car = Object.FindFirstObjectByType<ArcadeCarController>();
            car.GetComponent<VehicleInput>().enabled = false;
            var wheels = car.GetComponentsInChildren<WheelCollider>();
            var regions = new[] { new Vector3(542.82843f, -3.94928f, 765.09351f),
                new Vector3(273.88864f, 26.03149f, 1120.71887f), new Vector3(-89.19291f, 22.263f, -551.47f) };
            foreach (var region in regions)
            {
                Assert.That(Corridor(region, out var start, out var direction), Is.True,
                    "No clear 120-metre source-road corridor was found at " + region);
                car.Body.position = start + Vector3.up * .24f;
                car.Body.rotation = Quaternion.LookRotation(direction);
                car.Body.linearVelocity = Vector3.zero; car.Body.angularVelocity = Vector3.zero;
                car.SetCommand(new VehicleCommand(0, 1, 0, true)); Physics.SyncTransforms();
                for (int step = 0; step < 120; step++) yield return new WaitForFixedUpdate();
                car.SetCommand(new VehicleCommand(.4f, 0, 0, false));
                Assert.That(Road(start + direction * 2, out var ahead), Is.True);
                // A horizontal impulse artificially launches the car off a
                // descending ramp. Start at 126 km/h along the actual grade.
                car.Body.linearVelocity = (ahead - start).normalized * 35;
                int supported = 0;
                int unsupportedRun = 0, longestUnsupportedRun = 0, absentRoad = 0;
                int steps = Mathf.CeilToInt(2 / Time.fixedDeltaTime);
                for (int step = 0; step < steps; step++)
                {
                    yield return new WaitForFixedUpdate();
                    int contacts = wheels.Count(wheel => wheel.GetGroundHit(out var hit) && hit.collider.transform.root.name == "RoadsPhysical");
                    if (contacts >= 3) { supported++; unsupportedRun = 0; }
                    else
                    {
                        if (unsupportedRun == 0) Debug.Log($"Wheel support interruption at {car.Body.position}, speed {car.SpeedMetresPerSecond * 3.6f:0.0} km/h.");
                        unsupportedRun++; longestUnsupportedRun = Mathf.Max(longestUnsupportedRun, unsupportedRun);
                    }
                    if (!Road(car.Body.position - Vector3.up * .24f, out _)) absentRoad++;
                    Assert.That(float.IsFinite(car.Body.position.y), Is.True);
                }
                Debug.Log($"High-speed diagnostics {region}, heading {direction}: supported {supported}/{steps}, longest unsupported {longestUnsupportedRun * Time.fixedDeltaTime:0.000}s, absent road {absentRoad}, displacement {Vector3.Dot(car.Body.position - start, direction):0.00}m.");
                Assert.That(Vector3.Dot(car.Body.position - start, direction), Is.GreaterThan(45), "High-speed traversal stopped unexpectedly.");
                // Source ramps can briefly launch the car at a crest. Require a
                // continuous road beneath it, bounded airtime and safe traversal.
                Assert.That(absentRoad, Is.Zero, "The car left the continuous source driving surface.");
                Assert.That(longestUnsupportedRun * Time.fixedDeltaTime, Is.LessThan(.5f), "The car failed to regain wheel support after a crest.");
                Assert.That(supported, Is.GreaterThanOrEqualTo(Mathf.CeilToInt(steps * .9f)), "High-speed wheels lost road support too often.");
                Debug.Log($"High-speed road proof at {region}: {supported}/{steps} supported steps at initial 126 km/h.");
            }
            car.ResetToSpawn();
        }

        // Allow cold private-map loading while retaining every driving assertion.
        [Timeout(1200000)]
        [UnityTest]
        public IEnumerator AllReviewedExitWallsStopThePrototypeCar()
        {
            if (!File.Exists(Scene)) Assert.Ignore("Local converted map assets are absent.");
            yield return EditorSceneManager.LoadSceneInPlayMode(Scene, new LoadSceneParameters(LoadSceneMode.Single));
            var root = GameObject.Find("Downtown exit closures");
            Assert.That(root, Is.Not.Null); Assert.That(root.transform.childCount, Is.EqualTo(6));
            var car = Object.FindFirstObjectByType<ArcadeCarController>();
            car.GetComponent<VehicleInput>().enabled = false;
            foreach (Transform wall in root.transform)
            {
                Assert.That(wall.GetComponents<BoxCollider>().Length, Is.GreaterThan(0));
                var forward = wall.forward;
                float width = wall.GetComponent<MeshFilter>().sharedMesh.bounds.size.x;
                bool found = false; Vector3 start = Vector3.zero;
                foreach (float fraction in new[] { -.25f, .25f, 0 })
                {
                    var probe = wall.position - forward * 10 + wall.right * (width * fraction);
                    if (Road(probe, out start) && Road(start + wall.right, out var left) && Road(start - wall.right, out var right) &&
                        Mathf.Abs(left.y - start.y) < .4f && Mathf.Abs(right.y - start.y) < .4f) { found = true; break; }
                }
                Assert.That(found, Is.True, "Exit approach has no retained driving surface: " + wall.name);
                car.Body.position = start + Vector3.up * .24f; car.Body.rotation = Quaternion.LookRotation(forward);
                car.Body.linearVelocity = Vector3.zero; car.Body.angularVelocity = Vector3.zero;
                car.SetCommand(new VehicleCommand(0, 1, 0, true)); Physics.SyncTransforms();
                for (int step = 0; step < 100; step++) yield return new WaitForFixedUpdate();
                car.SetCommand(new VehicleCommand(.5f, 0, 0, false)); car.Body.linearVelocity = forward * 30;
                for (int step = 0; step < 100; step++) yield return new WaitForFixedUpdate();
                Assert.That(Vector3.Dot(car.Body.position - wall.position, forward), Is.LessThan(.6f), "The prototype passed the exit wall: " + wall.name);
                Assert.That(car.Body.linearVelocity.magnitude, Is.LessThan(6), "The exit wall did not stop the prototype: " + wall.name);
                Debug.Log("Exit wall collision proof: " + wall.name);
            }
            car.ResetToSpawn();
        }
    }
}
#endif
