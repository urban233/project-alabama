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

        private static void Finish(Exception error)
        {
            EditorApplication.update -= Update;
            if (rendering != null) rendering.Restore();
            if (error != null) Debug.LogException(error);
            if (Application.isBatchMode) EditorApplication.Exit(error == null ? 0 : 1);
        }
    }
}
