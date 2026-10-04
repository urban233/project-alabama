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
        private static (string name, Vector3 position, Vector3 target)[] propPoses;

        public static void Sample() => Start("Sample", NfsWorldSetup.SampleScene);
        public static void District() => Start("District", NfsWorldSetup.DistrictScene);
        public static void Style() => Start("StylePreview", NfsWorldStylePreview.ScenePath);
        public static void Art() => Start("ArtPass", NfsWorldArtPass.ScenePath);
        public static void ArtExits() => Start("ArtExits", NfsWorldArtPass.ScenePath);
        public static void ArtDirectionBefore() => Start("ArtDirectionBefore", NfsWorldArtPass.ScenePath);
        public static void ArtDirection() => Start("ArtDirection", NfsWorldArtDirection.ScenePath);
        public static void ArtDirectionNear() => Start("ArtDirectionNear", NfsWorldArtDirection.ScenePath);
        public static void ArtDirectionMap() => Start("ArtDirectionMap", NfsWorldArtDirection.ScenePath);
        public static void ArtDirectionSurface()
        {
            NfsWorldArtDirection.ConfigureRoadDetails();
            Start("ArtDirectionSurface", NfsWorldArtDirection.ScenePath);
        }
        public static void ArtDirectionLighting() => Start("ArtDirectionLighting", NfsWorldArtDirection.ScenePath);
        public static void LightingStudy() => Start("LightingStudy", NfsWorldStylePreview.LightingStudyScenePath);

        private static void Start(string name, string scene)
        {
            variant = name;
            propPoses = Array.Empty<(string name, Vector3 position, Vector3 target)>();
            if (variant.StartsWith("ArtDirection") && variant != "ArtDirectionNear" && variant != "ArtDirectionMap") PreparePropPoses();
            EditorSceneManager.OpenScene(scene);
            if (variant == "ArtDirectionLighting")
            {
                NfsWorldArtDirectionLighting.ConfigureActiveScene();
            }
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
                // Fixed visual comparisons must not include differences from a new visibility bake.
                if (variant.StartsWith("ArtDirection")) camera.useOcclusionCulling = false;
                var culling = UnityEngine.Object.FindFirstObjectByType<NfsWorldDistanceCulling>();
                if (variant == "ArtDirectionMap")
                {
                    CaptureMap(camera, culling, output);
                    Finish(null);
                    return;
                }
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
                    if (variant.StartsWith("ArtDirection"))
                    {
                        CapturePassage(camera, culling, roads, output);
                        foreach (var pose in propPoses)
                        {
                            camera.transform.position = pose.position;
                            camera.transform.LookAt(pose.target);
                            if (culling != null) culling.Refresh();
                            Save(camera, Path.Combine(output, pose.name + "-close.png"));
                        }
                    }
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
            var lods = UnityEngine.Object.FindFirstObjectByType<NfsWorldMeshLods>();
            if (variant == "ArtDirectionNear") lods?.Restore();
            else lods?.Refresh(camera.transform.position);
            // The first request after opening a batch scene can show fallback car
            // textures. Read back a warmup frame before keeping the actual capture.
            VehicleCapture.Save(camera, path);
            VehicleCapture.Save(camera, path);
        }

        private static void CaptureMap(Camera camera, NfsWorldDistanceCulling culling, string output)
        {
            var roads = UnityEngine.Object.FindObjectsByType<MeshCollider>(FindObjectsSortMode.None)
                .Where(c => c.transform.root.name == "RoadsPhysical").OrderBy(c => c.name).ToArray();
            var bounds = roads[0].bounds;
            foreach (var road in roads) bounds.Encapsulate(road.bounds);
            var used = new System.Collections.Generic.HashSet<int>();
            var poses = new System.Collections.Generic.List<CapturePose>();
            var car = UnityEngine.Object.FindFirstObjectByType<ArcadeCarController>();
            car.gameObject.SetActive(false);
            // Survey the entire physical district on a regular grid, not only its spawn block.
            for (float z = bounds.min.z + 250; z < bounds.max.z; z += 650)
            for (float x = bounds.min.x + 250; x < bounds.max.x; x += 650)
            {
                var probe = new Vector3(x, bounds.center.y, z);
                foreach (var road in roads.OrderBy(r => r.bounds.SqrDistance(probe)).Take(12))
                {
                    if (used.Contains(road.GetInstanceID())) continue;
                    if (!road.Raycast(new Ray(road.bounds.center + Vector3.up * 150, Vector3.down), out var hit, 400)
                        || hit.normal.y < .7f) continue;
                    if (Vector2.Distance(new Vector2(hit.point.x, hit.point.z), new Vector2(x, z)) > 500) continue;
                    used.Add(road.GetInstanceID());
                    for (int heading = 0; heading < 4; heading++)
                    {
                        camera.transform.position = hit.point + Vector3.up * 2;
                        camera.transform.rotation = Quaternion.Euler(2, heading * 90, 0);
                        culling?.Refresh();
                        string name = "sector-" + used.Count.ToString("D2") + "-" + heading;
                        Save(camera, Path.Combine(output, name + ".png"));
                        poses.Add(new CapturePose { road = road.name, position = camera.transform.position,
                            eulerAngles = camera.transform.eulerAngles });
                    }
                    break;
                }
            }
            File.WriteAllText(Path.Combine(output, "poses.json"), JsonUtility.ToJson(new MapPoses { poses = poses.ToArray() }, true));
            Debug.Log($"Whole-map visual survey: {used.Count} road locations, {poses.Count} views.");
        }

        [Serializable] private sealed class MapPoses { public CapturePose[] poses; }

        private static void PreparePropPoses()
        {
            // Locate real instances in the unchanged source scene. Both comparison
            // variants use these poses even after individual visuals are batched.
            EditorSceneManager.OpenScene(NfsWorldStylePreview.LightingStudyScenePath);
            var probes = new[] { ("barrel", "Props_00061"), ("hydrant", "Props_00050"),
                ("newspaper-box", "Props_00021"), ("waste-bin", "Props_00065"),
                ("crash-barrel", "Props_00062"), ("round-bin", "Props_00063") };
            var roads = UnityEngine.Object.FindObjectsByType<MeshCollider>(FindObjectsSortMode.None)
                .Where(c => c.transform.root.name == "RoadsPhysical").ToArray();
            propPoses = probes.Select(probe =>
            {
                var filter = GameObject.Find(probe.Item2).GetComponent<MeshFilter>();
                var points = filter.sharedMesh.vertices.Select(filter.transform.TransformPoint).ToArray();
                var closest = points.OrderBy(p => (p - new Vector3(-550, 15, 550)).sqrMagnitude).First();
                var cluster = points.Where(p => (p - closest).sqrMagnitude < 1.2f * 1.2f).ToArray();
                var bounds = new Bounds(cluster[0], Vector3.zero);
                foreach (var point in cluster) bounds.Encapsulate(point);
                var target = bounds.center;
                var road = roads.OrderBy(r => r.bounds.SqrDistance(target)).First();
                var direction = road.bounds.ClosestPoint(target) - target; direction.y = 0;
                if (direction.sqrMagnitude < .1f) direction = new Vector3(-1, 0, -1);
                return (probe.Item1, target + direction.normalized * 4 + Vector3.up * 1.7f, target);
            }).ToArray();
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
