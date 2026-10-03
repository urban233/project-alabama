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
        public static void ArtDirectionBefore() => Start("ArtDirectionBefore", NfsWorldArtPass.ScenePath);
        public static void ArtDirection() => Start("ArtDirection", NfsWorldArtDirection.ScenePath);
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
                // Fixed material comparisons must not include differences from a new visibility bake.
                if (variant.StartsWith("ArtDirection")) camera.useOcclusionCulling = false;
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
                        var road = roads.OrderBy(c => (c.bounds.ClosestPoint(probes[view]) - probes[view]).sqrMagnitude).ThenBy(c => c.name)
                            .First(c => c.Raycast(new Ray(c.bounds.center + Vector3.up * 100, Vector3.down), out var hit, 250)
                                        && hit.normal.y > .5f);
                        road.Raycast(new Ray(road.bounds.center + Vector3.up * 100, Vector3.down), out var ground, 250);
                        car.transform.position = ground.point + Vector3.up * .24f;
                        camera.transform.position = car.transform.TransformPoint(new Vector3(0, 1.9f, -6));
                        camera.transform.LookAt(car.transform.TransformPoint(new Vector3(0, 1.2f, 12)));
                        Physics.SyncTransforms();
                        if (culling != null) culling.Refresh();
                        Save(camera, Path.Combine(output, "road-view-" + (view + 1) + ".png"));
                        if (variant.StartsWith("ArtDirection") && view == 1)
                        {
                            camera.transform.position = ground.point + new Vector3(6, 2.4f, -2);
                            camera.transform.LookAt(ground.point + new Vector3(0, 5, 14));
                            if (culling != null) culling.Refresh();
                            Save(camera, Path.Combine(output, "facade-close.png"));
                        }
                    }
                    if (variant.StartsWith("ArtDirection")) CapturePassage(camera, culling, roads, output);
                }
                camera.orthographic = true;
                camera.useOcclusionCulling = false; // Overview is outside the baked driving view volume.
                camera.orthographicSize = variant == "Sample" ? 230 : 2200;
                camera.farClipPlane = 10000;
                camera.transform.position = new Vector3(variant == "Sample" ? 0 : -160, 4500, variant == "Sample" ? 0 : 730);
                camera.transform.rotation = Quaternion.Euler(90, 0, 0);
                RenderSettings.fog = false;
                if (culling != null)
                {
                    culling.enabled = false;
                    culling.ShowAll(); // Editor callbacks do not run for ordinary runtime components.
                }
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

        private static void CapturePassage(Camera camera, NfsWorldDistanceCulling culling, MeshCollider[] roads, string output)
        {
            // Visual walls do not all own gameplay collision. Temporary blockers
            // let the review camera reject a clear physical road hidden by a facade.
            var blockers = new System.Collections.Generic.List<MeshCollider>();
            var visuals = GameObject.Find("Optimized district visuals").GetComponentsInChildren<MeshFilter>();
            try
            {
            // Find a real lower roadway with an overhead source-road deck.
            foreach (var road in roads.OrderBy(r => r.name))
            {
                var hits = Physics.RaycastAll(road.bounds.center + Vector3.up * 100, Vector3.down, 250)
                    .Where(h => h.collider.transform.root.name == "RoadsPhysical" && h.normal.y > .5f)
                    .OrderBy(h => h.point.y).ToArray();
                if (hits.Length < 2 || hits.Last().point.y - hits[0].point.y < 5 ||
                    hits.Last().point.y - hits[0].point.y > 25) continue;
                var eye = hits[0].point + Vector3.up * 2;
                foreach (var filter in visuals)
                {
                    var renderer = filter.GetComponent<MeshRenderer>();
                    if (renderer == null || !renderer.enabled || renderer.bounds.SqrDistance(eye) > 40 * 40 ||
                        filter.GetComponent<MeshCollider>() != null) continue;
                    var blocker = filter.gameObject.AddComponent<MeshCollider>();
                    blocker.sharedMesh = filter.sharedMesh; blockers.Add(blocker);
                }
                Physics.SyncTransforms();
                if (Physics.CheckSphere(eye, .5f)) continue;
                Vector3 forward = Vector3.zero;
                for (int angle = 0; angle < 360; angle += 45)
                {
                    var direction = Quaternion.Euler(0, angle, 0) * Vector3.forward;
                    if (Physics.Raycast(eye, direction, 30)) continue;
                    var ahead = Physics.RaycastAll(hits[0].point + direction * 20 + Vector3.up * 2, Vector3.down, 4)
                        .Where(h => h.collider.transform.root.name == "RoadsPhysical" && h.normal.y > .5f).ToArray();
                    if (ahead.Length == 0) continue;
                    forward = direction; break;
                }
                if (forward == Vector3.zero) continue;
                camera.transform.position = eye;
                camera.transform.LookAt(eye + forward * 20);
                Physics.SyncTransforms();
                if (culling != null) culling.Refresh();
                Save(camera, Path.Combine(output, "shadowed-passage.png"));
                File.WriteAllText(Path.Combine(output, "passage-pose.json"), JsonUtility.ToJson(
                    new CapturePose { road = road.name, position = camera.transform.position, eulerAngles = camera.transform.eulerAngles }, true));
                return;
            }
            throw new System.InvalidOperationException("No real shadowed source-road passage was found.");
            }
            finally { foreach (var blocker in blockers) UnityEngine.Object.DestroyImmediate(blocker); }
        }

        [System.Serializable] private sealed class CapturePose { public string road; public Vector3 position; public Vector3 eulerAngles; }

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
