#if UNITY_EDITOR
using System.Collections;
using System.IO;
using Alabama.Driving;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Alabama.Tests
{
    public sealed class NfsWorldDrivingTests
    {
        // Allow cold private-map loading while retaining every driving assertion.
        [Timeout(1200000)]
        [UnityTest]
        public IEnumerator ConvertedDistrictSupportsWheelContactDrivingAndRecovery()
        {
            const string district = "Assets/Alabama/Art/Maps/NfsWorld/Scenes/NfsWorldPrototype.unity";
            const string sample = "Assets/Alabama/Art/Maps/NfsWorld/Scenes/NfsWorldSample.unity";
            string path = File.Exists(district) ? district : sample;
            return CheckDriving(path);
        }

        // Allow cold private-map loading while retaining every driving assertion.
        [Timeout(1200000)]
        [UnityTest]
        public IEnumerator ConvertedVisualStudySupportsDrivingAndRecovery()
        {
            return CheckDriving("Assets/Alabama/Art/Maps/NfsWorld/Scenes/NfsWorldStylePreview.unity");
        }

        // Allow cold private-map loading while retaining every driving assertion.
        [Timeout(1200000)]
        [UnityTest]
        public IEnumerator ConvertedArtStudySupportsDrivingAndRecovery()
        {
            return CheckDriving("Assets/Alabama/Art/Maps/NfsWorld/Scenes/NfsWorldArtPass.unity");
        }

        private IEnumerator CheckDriving(string path)
        {
            if (!File.Exists(path)) Assert.Ignore("Local NFS World prototype assets are absent.");
            var priorRendering = Object.FindFirstObjectByType<NfsWorldRenderSettings>();
            if (priorRendering != null) priorRendering.Restore();
            var previousPipeline = QualitySettings.renderPipeline;
            int previousAntialiasing = QualitySettings.antiAliasing;
            yield return EditorSceneManager.LoadSceneInPlayMode(path, new LoadSceneParameters(LoadSceneMode.Single));
            var rendering = Object.FindFirstObjectByType<NfsWorldRenderSettings>();
            if (rendering != null)
            {
                var mapPipeline = QualitySettings.renderPipeline;
                Assert.That(mapPipeline, Is.Not.SameAs(previousPipeline), "The map must use its local pipeline copy.");
                rendering.enabled = false;
                Assert.That(QualitySettings.renderPipeline, Is.SameAs(previousPipeline), "Scene render settings must restore the original pipeline.");
                Assert.That(QualitySettings.antiAliasing, Is.EqualTo(previousAntialiasing));
                rendering.enabled = true;
                Assert.That(QualitySettings.renderPipeline, Is.SameAs(mapPipeline));
            }
            var car = Object.FindFirstObjectByType<ArcadeCarController>();
            Assert.That(car, Is.Not.Null);
            car.GetComponent<VehicleInput>().enabled = false;
            var body = car.Body;
            var spawn = body.position;
            for (int i = 0; i < 120; i++) yield return new WaitForFixedUpdate();
            int grounded = 0;
            foreach (var wheel in car.GetComponentsInChildren<WheelCollider>())
                if (wheel.GetGroundHit(out var hit) && hit.collider is MeshCollider) grounded++;
            Assert.That(grounded, Is.EqualTo(4), "All wheels must settle on imported collision.");
            Assert.That(body.position.y, Is.InRange(spawn.y - .7f, spawn.y + .7f));
            var start = body.position;
            car.SetCommand(new VehicleCommand(1, 0, 0, false));
            for (int i = 0; i < 240; i++)
            {
                yield return new WaitForFixedUpdate();
                Assert.That(float.IsFinite(body.position.y), Is.True);
                Assert.That(body.position.y, Is.GreaterThan(spawn.y - 2), "The car fell through imported road collision.");
            }
            Assert.That(Vector3.Distance(body.position, start), Is.GreaterThan(4), "The existing car must drive on the converted surface.");
            Assert.That(car.SignedForwardSpeed, Is.GreaterThan(4));
            float movingSpeed = car.SignedForwardSpeed;
            car.SetCommand(new VehicleCommand(0, 1, 0, false));
            for (int i = 0; i < 120; i++) yield return new WaitForFixedUpdate();
            Assert.That(car.SignedForwardSpeed, Is.LessThan(movingSpeed - 2));
            car.ResetToSpawn();
            Assert.That(Vector3.Distance(body.position, spawn), Is.LessThan(.05f));
            Assert.That(body.linearVelocity.magnitude, Is.LessThan(.05f));
            if (!path.EndsWith("NfsWorldSample.unity"))
            {
                // These two elevated positions were resolved on the source road meshes
                // by the rendered baseline benchmark, not invented ground planes.
                var elevated = new[] { new Vector3(273.88864f, 26.03149f, 1120.7189f),
                                       new Vector3(-89.19291f, 22.263f, -551.47f) };
                var roadColliders = Object.FindObjectsByType<MeshCollider>(FindObjectsSortMode.None);
                foreach (var point in elevated)
                {
                    bool found = false;
                    foreach (var road in roadColliders)
                        if (road.transform.root.name == "RoadsPhysical" &&
                            road.Raycast(new Ray(point + Vector3.up * 2, Vector3.down), out var hit, 4) && hit.normal.y > .5f)
                        { found = true; break; }
                    Assert.That(found, Is.True, "Verified elevated source road is missing.");
                    body.position = point + Vector3.up * .24f;
                    body.linearVelocity = Vector3.zero;
                    body.angularVelocity = Vector3.zero;
                    car.SetCommand(new VehicleCommand(0, 1, 0, true));
                    Physics.SyncTransforms();
                    for (int i = 0; i < 120; i++) yield return new WaitForFixedUpdate();
                    int contacts = 0;
                    foreach (var wheel in car.GetComponentsInChildren<WheelCollider>())
                        if (wheel.GetGroundHit(out var hit) && hit.collider.transform.root.name == "RoadsPhysical") contacts++;
                    Assert.That(contacts, Is.EqualTo(4), "All wheels must contact the elevated imported road.");
                    Assert.That(body.position.y, Is.InRange(point.y - .7f, point.y + .7f));
                }
                car.ResetToSpawn();
            }
            var culling = Object.FindFirstObjectByType<NfsWorldDistanceCulling>();
            if (culling != null)
            {
                var camera = Camera.main;
                var pose = camera.transform.position;
                var colliders = Object.FindObjectsByType<MeshCollider>(FindObjectsSortMode.None);
                camera.transform.position = Vector3.one * 100000;
                culling.Refresh();
                int hidden = 0;
                foreach (var renderer in Object.FindObjectsByType<MeshRenderer>(FindObjectsSortMode.None))
                    if (renderer.forceRenderingOff) hidden++;
                Assert.That(hidden, Is.GreaterThan(0), "Source distance limits must hide far scenery.");
                foreach (var collider in colliders)
                    Assert.That(collider.enabled && collider.gameObject.activeInHierarchy, Is.True,
                        "Visual distance culling must retain all driving collision.");
                camera.transform.position = pose;
                culling.enabled = false;
                foreach (var renderer in Object.FindObjectsByType<MeshRenderer>(FindObjectsSortMode.None))
                    Assert.That(renderer.forceRenderingOff, Is.False, "Disabling culling must restore renderers.");
                culling.enabled = true;
            }
            body.position = new Vector3(0, -100, 0);
            yield return new WaitForFixedUpdate();
            yield return new WaitForFixedUpdate();
            Assert.That(body.position.y, Is.GreaterThan(-2), "Map recovery must reset falls outside the imported district.");
            Debug.Log($"NFS World driving proof: {grounded} wheels grounded, {movingSpeed * 3.6f:0.0} km/h after two seconds, reset/recovery passed.");
        }
    }
}
#endif
