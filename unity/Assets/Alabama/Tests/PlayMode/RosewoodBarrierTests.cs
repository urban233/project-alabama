#if UNITY_EDITOR
using System.Collections;
using System.IO;
using System.Linq;
using Alabama.Districts;
using Alabama.Driving;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Alabama.Tests
{
    public sealed class RosewoodBarrierTests
    {
        private DistrictRuntime runtime;

        private static bool Road(Scene scene, Vector3 probe, out Vector3 point)
        {
            var hits = Physics.RaycastAll(probe + Vector3.up * 3, Vector3.down, 6)
                .Where(h => h.collider.gameObject.scene == scene && h.collider.transform.root.name == "RoadsPhysical" && h.normal.y > .5f)
                .OrderBy(h => Mathf.Abs(h.point.y - probe.y)).ToArray();
            point = hits.Length > 0 ? hits[0].point : Vector3.zero;
            return hits.Length > 0;
        }

        [UnityTest, Timeout(2400000)]
        public IEnumerator RemainingRosewoodBarriersStopTheProductionCar()
        {
            const string content = "Assets/Alabama/Art/Maps/NfsWorld/Rosewood/RosewoodContent.unity";
            if (!File.Exists(content)) Assert.Ignore("Restore the private Rosewood pack first.");
            const string host = "Assets/Alabama/Art/Maps/NfsWorld/Runtime/DistrictRuntime.unity";
            yield return EditorSceneManager.LoadSceneInPlayMode(host, new LoadSceneParameters(LoadSceneMode.Single));
            runtime = DistrictRuntime.Instance;
            float deadline = Time.realtimeSinceStartup + 2400;
            while (!runtime.Ready && runtime.LastFailure == null && Time.realtimeSinceStartup < deadline) yield return null;
            Assert.That(runtime.Ready, Is.True, runtime.LastFailure);
            Assert.That(runtime.DistrictCount, Is.EqualTo(2));
            var rosewood = runtime.LoadedDistricts.Single(d => d.Id == "rosewood");
            var selected = rosewood.Connections.Single(c => c.id == "rosewood-exit-7");
            Assert.That(selected.seamVerified, Is.True, "Accept the actual two-way seam before release qualification.");
            Assert.That(selected.closure.activeSelf, Is.False);
            var retained = rosewood.Connections.Where(c => c.id != selected.id).ToArray();
            Assert.That(retained.Length, Is.EqualTo(8));
            var car = runtime.Player; var input = car.GetComponent<VehicleInput>(); input.enabled = false;
            var wheels = car.GetComponentsInChildren<WheelCollider>();
            foreach (var connection in retained)
            {
                var wall = connection.closure.transform;
                Assert.That(!connection.seamVerified && wall.gameObject.activeInHierarchy, Is.True, connection.id);
                Assert.That(wall.GetComponents<BoxCollider>().Length, Is.GreaterThan(0));
                var forward = connection.outward.normalized;
                float width = wall.GetComponent<MeshFilter>().sharedMesh.bounds.size.x;
                bool found = false; Vector3 start = Vector3.zero, ahead = Vector3.zero;
                foreach (float fraction in new[] { 0f, -.25f, .25f, -.125f, .125f, -.375f, .375f })
                {
                    var probe = wall.position - forward * 10 + wall.right * (width * fraction);
                    if (Road(rosewood.gameObject.scene, probe, out start) &&
                        Road(rosewood.gameObject.scene, start + forward * 2, out ahead) &&
                        Road(rosewood.gameObject.scene, start + wall.right * 1.2f, out var left) &&
                        Road(rosewood.gameObject.scene, start - wall.right * 1.2f, out var right) &&
                        Mathf.Abs(left.y - start.y) < .4f && Mathf.Abs(right.y - start.y) < .4f)
                    {
                        // A ray at the road centre does not prove a stable car
                        // placement on a bank/crest or beside source collision.
                        car.Body.position = start + Vector3.up * .24f; car.Body.rotation = Quaternion.LookRotation(forward);
                        car.Body.linearVelocity = Vector3.zero; car.Body.angularVelocity = Vector3.zero;
                        car.SetCommand(new VehicleCommand(0, 1, 0, true)); Physics.SyncTransforms();
                        for (int step = 0; step < 100; step++) yield return new WaitForFixedUpdate();
                        int contacts = wheels.Count(w => w.GetGroundHit(out var h) && h.collider.gameObject.scene == rosewood.gameObject.scene && h.collider.transform.root.name == "RoadsPhysical");
                        Debug.Log($"Rosewood barrier approach {connection.id}, fraction {fraction}: {contacts} supported wheels.");
                        if (contacts >= 3) { found = true; break; }
                    }
                }
                Assert.That(found, Is.True, "No supported source-road approach: " + connection.id);
                Assert.That(wheels.Count(w => w.GetGroundHit(out var h) && h.collider.gameObject.scene == rosewood.gameObject.scene && h.collider.transform.root.name == "RoadsPhysical"), Is.GreaterThanOrEqualTo(3), connection.id);
                car.SetCommand(new VehicleCommand(.5f, 0, 0, false));
                car.Body.linearVelocity = (ahead - start).normalized * 30;
                for (int step = 0; step < 100; step++) yield return new WaitForFixedUpdate();
                Assert.That(Vector3.Dot(car.Body.position - wall.position, forward), Is.LessThan(.6f), "Car passed " + connection.id);
                Assert.That(car.Body.linearVelocity.magnitude, Is.LessThan(6), "Car was not stopped by " + connection.id);
                Debug.Log("Rosewood retained barrier collision proof: " + connection.id);
            }
            Assert.That(runtime.Player, Is.SameAs(car));
            Assert.That(car.GetComponent<VehicleInput>(), Is.SameAs(input));
            Assert.That(SceneManager.GetActiveScene(), Is.EqualTo(runtime.gameObject.scene));
            car.SetCommand(new VehicleCommand(0, 1, 0, true)); car.ResetToSpawn(); input.enabled = true;
        }

        [UnityTearDown]
        public IEnumerator Cleanup()
        {
            SceneManager.SetActiveScene(SceneManager.CreateScene("Rosewood barrier cleanup"));
            if (runtime == null) yield break;
            foreach (var content in runtime.LoadedDistricts.ToArray()) yield return SceneManager.UnloadSceneAsync(content.gameObject.scene);
            yield return SceneManager.UnloadSceneAsync(runtime.gameObject.scene);
        }
    }
}
#endif
