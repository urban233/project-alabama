using System;
using System.IO;
using System.Linq;
using Alabama.Driving;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Alabama.Editor
{
    /// <summary>Unsaved review views in the authored combined district and its lighting.</summary>
    public static class SuppliedCarCapture
    {
        private static int frames;
        private static double deadline;
        private static NfsWorldRenderSettings rendering;

        public static void Run()
        {
            SuppliedCarSetup.Verify();
            EditorSceneManager.OpenScene(NfsWorldDistrictRuntimeSetup.RuntimeScene);
            EditorSceneManager.OpenScene(NfsWorldDistrictRuntimeSetup.DowntownScene, OpenSceneMode.Additive);
            EditorSceneManager.OpenScene(NfsWorldRosewoodSetup.ScenePath, OpenSceneMode.Additive);
            rendering = UnityEngine.Object.FindFirstObjectByType<NfsWorldRenderSettings>();
            if (rendering != null) rendering.Apply();
            frames = 0;
            deadline = EditorApplication.timeSinceStartup + 180;
            EditorApplication.update += Capture;
        }

        private static void Capture()
        {
            if (EditorApplication.timeSinceStartup > deadline) { Finish(new TimeoutException("Car capture timed out.")); return; }
            if (++frames < 30 || EditorApplication.isCompiling || EditorApplication.isUpdating) return;
            EditorApplication.update -= Capture;
            try
            {
                string output = Path.GetFullPath(Path.Combine(Application.dataPath, "../../artifacts/CarReplacement/Unity"));
                Directory.CreateDirectory(output);
                var cars = UnityEngine.Object.FindObjectsByType<ArcadeCarController>(FindObjectsSortMode.None);
                if (cars.Length != 1) throw new InvalidOperationException("Combined runtime must contain exactly one player car.");
                var car = cars[0];
                var spawnPosition = car.transform.position;
                var spawnRotation = car.transform.rotation;
                var camera = Camera.main;
                if (camera == null) throw new InvalidOperationException("Runtime camera missing.");
                // Move only this unsaved review instance onto an existing connector road.
                Physics.SyncTransforms();
                var hits = Physics.RaycastAll(new Vector3(500, 80, -1084), Vector3.down, 100)
                    .Where(h => h.collider is MeshCollider && h.normal.y > .8f && h.collider.transform.root.name.Contains("Road"))
                    .OrderBy(h => Mathf.Abs(h.point.y - 22)).ToArray();
                if (hits.Length == 0) throw new InvalidOperationException("Connector review has no road support.");
                car.transform.SetPositionAndRotation(hits[0].point + Vector3.up * .008f, Quaternion.Euler(0, 90, 0));
                camera.useOcclusionCulling = false;
                camera.fieldOfView = 42;
                string[] names = { "front", "side", "rear", "chase", "environment" };
                Vector3[] offsets = { new Vector3(3.6f, 1.7f, 5.7f), new Vector3(6.6f, 1.4f, 0),
                    new Vector3(-3.6f, 1.7f, -5.7f), new Vector3(0, 2f, -6.5f), new Vector3(8, 3.4f, 10) };
                for (int i = 0; i < names.Length; i++)
                {
                    camera.transform.position = car.transform.TransformPoint(offsets[i]);
                    camera.transform.LookAt(car.transform.position + Vector3.up * .65f);
                    foreach (var culling in UnityEngine.Object.FindObjectsByType<NfsWorldDistanceCulling>(FindObjectsSortMode.None))
                        culling.Refresh();
                    var path = Path.Combine(output, names[i] + ".png");
                    VehicleCapture.Save(camera, path);
                    VehicleCapture.Save(camera, path);
                }
                // Also review the outdoor Downtown spawn under the same authored sun.
                var spawnHits = Physics.RaycastAll(spawnPosition + Vector3.up * 3, Vector3.down, 6)
                    .Where(h => h.collider is MeshCollider && h.normal.y > .8f && h.collider.transform.root.name.Contains("Road"))
                    .OrderBy(h => Mathf.Abs(h.point.y - spawnPosition.y)).ToArray();
                if (spawnHits.Length == 0) throw new InvalidOperationException("Downtown review has no road support.");
                car.transform.SetPositionAndRotation(spawnHits[0].point + Vector3.up * .008f, spawnRotation);
                foreach (int i in new[] { 0, 1, 2, 3 })
                {
                    camera.transform.position = car.transform.TransformPoint(offsets[i]);
                    camera.transform.LookAt(car.transform.position + Vector3.up * .65f);
                    foreach (var culling in UnityEngine.Object.FindObjectsByType<NfsWorldDistanceCulling>(FindObjectsSortMode.None))
                        culling.Refresh();
                    var path = Path.Combine(output, "downtown-" + names[i] + ".png");
                    VehicleCapture.Save(camera, path);
                    VehicleCapture.Save(camera, path);
                }
                var meshes = car.GetComponentsInChildren<MeshFilter>();
                File.WriteAllText(Path.Combine(output, "review.txt"),
                    $"One player car; {meshes.Sum(m => m.sharedMesh.triangles.Length / 3)} triangles; {meshes.Length} meshes.\n" +
                    $"Position: {car.transform.position}; authored district lighting retained; 1920x1080 output.\n" +
                    "Review-only car/camera transforms were not saved to any scene.\n");
                Finish(null);
            }
            catch (Exception error) { Finish(error); }
        }

        private static void Finish(Exception error)
        {
            EditorApplication.update -= Capture;
            if (rendering != null) rendering.Restore();
            if (error != null) Debug.LogException(error);
            if (Application.isBatchMode) EditorApplication.Exit(error == null ? 0 : 1);
        }
    }
}
