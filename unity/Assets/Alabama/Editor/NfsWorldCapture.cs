using System;
using System.IO;
using System.Linq;
using Alabama.Driving;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Alabama.Editor
{
    public static class NfsWorldCapture
    {
        private static int frames;
        private static double deadline;
        private static string variant;
        private static NfsWorldRenderSettings rendering;

        public static void Sample() => Start("Sample", NfsWorldSetup.SampleScene);
        public static void District() => Start("District", NfsWorldSetup.DistrictScene);
        public static void Style() => Start("StylePreview", NfsWorldStylePreview.ScenePath);
        public static void Art() => Start("ArtPass", NfsWorldArtPass.ScenePath);
        public static void ArtExits() => Start("ArtExits", NfsWorldArtPass.ScenePath);
        public static void LightingStudy() => Start("LightingStudy", NfsWorldStylePreview.LightingStudyScenePath);

        private static void Start(string name, string scene)
        {
            variant = name;
            EditorSceneManager.OpenScene(scene);
            rendering = UnityEngine.Object.FindFirstObjectByType<NfsWorldRenderSettings>();
            if (rendering != null) rendering.Apply();
            frames = 0;
            deadline = EditorApplication.timeSinceStartup + 180;
            EditorApplication.update += Update;
        }

        private static void Update()
        {
            if (EditorApplication.timeSinceStartup > deadline) { Finish(new TimeoutException("Capture timed out")); return; }
            if (++frames < 30 || EditorApplication.isCompiling || EditorApplication.isUpdating) return;
            EditorApplication.update -= Update;
            try
            {
                string output = Path.GetFullPath(Path.Combine(Application.dataPath, "../../artifacts/NfsWorld/Captures/" + variant));
                Directory.CreateDirectory(output);
                var camera = Camera.main;
                NfsWorldSetup.Require(camera != null, "Capture camera missing");
                var culling = UnityEngine.Object.FindFirstObjectByType<NfsWorldDistanceCulling>();
                if (variant == "ArtExits")
                {
                    CaptureExits(camera, culling, output);
                    Finish(null);
                    return;
                }
                if (culling != null) culling.Refresh();
                Save(camera, Path.Combine(output, "chase.png"));
                if (variant != "Sample")
                {
                    var car = UnityEngine.Object.FindFirstObjectByType<ArcadeCarController>();
                    var roads = UnityEngine.Object.FindObjectsByType<MeshCollider>(FindObjectsSortMode.None)
                        .Where(c => c.transform.root.name == "RoadsPhysical").ToArray();
                    var probes = new[] { new Vector3(450, 0, 800), new Vector3(-550, 0, 550) };
                    for (int view = 0; view < probes.Length; view++)
                    {
                        // Source geometry determines each road position; no fabricated platform.
                        var road = roads.OrderBy(c => (c.bounds.ClosestPoint(probes[view]) - probes[view]).sqrMagnitude)
                            .First(c => c.Raycast(new Ray(c.bounds.center + Vector3.up * 100, Vector3.down), out var hit, 250)
                                        && hit.normal.y > .5f);
                        road.Raycast(new Ray(road.bounds.center + Vector3.up * 100, Vector3.down), out var ground, 250);
                        car.transform.position = ground.point + Vector3.up * .24f;
                        camera.transform.position = car.transform.TransformPoint(new Vector3(0, 1.9f, -6));
                        camera.transform.LookAt(car.transform.TransformPoint(new Vector3(0, 1.2f, 12)));
                        Physics.SyncTransforms();
                        if (culling != null) culling.Refresh();
                        Save(camera, Path.Combine(output, "road-view-" + (view + 1) + ".png"));
                    }
                }
                camera.orthographic = true;
                camera.orthographicSize = variant == "Sample" ? 230 : 2200;
                camera.farClipPlane = 10000;
                camera.transform.position = new Vector3(variant == "Sample" ? 0 : -160, 4500, variant == "Sample" ? 0 : 730);
                camera.transform.rotation = Quaternion.Euler(90, 0, 0);
                RenderSettings.fog = false;
                if (culling != null) culling.enabled = false; // Overview intentionally shows the complete district.
                Save(camera, Path.Combine(output, "overview.png"));
                Debug.Log("NFS World captures saved: " + output);
                Finish(null);
            }
            catch (Exception error) { Finish(error); }
        }

        private static void Save(Camera camera, string path)
        {
            // The first request after opening a batch scene can show fallback car
            // textures. Read back a warmup frame before keeping the actual capture.
            VehicleCapture.Save(camera, path);
            VehicleCapture.Save(camera, path);
        }

        private static void CaptureExits(Camera camera, NfsWorldDistanceCulling culling, string output)
        {
            var root = GameObject.Find("Downtown exit closures");
            NfsWorldSetup.Require(root != null && root.transform.childCount == 6, "Six exit walls are required.");
            var car = UnityEngine.Object.FindFirstObjectByType<ArcadeCarController>();
            foreach (Transform wall in root.transform)
            {
                float width = wall.GetComponent<MeshFilter>().sharedMesh.bounds.size.x;
                var hits = new RaycastHit[0];
                foreach (float fraction in new[] { -.25f, .25f, 0 })
                {
                    var probe = wall.position - wall.forward * 12 + wall.right * (width * fraction);
                    hits = Physics.RaycastAll(probe + Vector3.up * 5, Vector3.down, 10)
                        .Where(hit => hit.collider.transform.root.name == "RoadsPhysical" && hit.normal.y > .5f)
                        .OrderBy(hit => Mathf.Abs(hit.point.y - probe.y)).ToArray();
                    if (hits.Length > 0) break;
                }
                NfsWorldSetup.Require(hits.Length > 0, "Exit capture approach has no road: " + wall.name);
                car.transform.SetPositionAndRotation(hits[0].point + Vector3.up * .24f, wall.rotation);
                camera.transform.position = car.transform.TransformPoint(new Vector3(0, 1.9f, -6));
                camera.transform.LookAt(wall.position + Vector3.up * 1.2f);
                Physics.SyncTransforms();
                if (culling != null) culling.Refresh();
                Save(camera, Path.Combine(output, "exit-" + (wall.GetSiblingIndex() + 1) + ".png"));
            }
            Debug.Log("Six road-level exit wall captures saved: " + output);
        }

        private static void Finish(Exception error)
        {
            EditorApplication.update -= Update;
            if (rendering != null) rendering.Restore();
            if (error != null) Debug.LogException(error);
            if (Application.isBatchMode) EditorApplication.Exit(error == null ? 0 : 1);
        }
    }
}
