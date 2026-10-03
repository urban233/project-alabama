using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Alabama.Driving;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Alabama.Editor
{
    public static class NfsWorldValidation
    {
        [Serializable] private sealed class Report
        {
            public string variant;
            public int roadMeshes;
            public int roadSamples;
            public int undersideOnlyRoadMeshes;
            public int matchedRoadSamples;
            public int meshColliders;
            public int visibleMeshes;
            public int visibleTriangles;
            public int shadowProxyMeshes;
            public int shadowProxyTriangles;
            public int collisionTriangles;
            public float maximumRoadSampleError;
            public string[] failures;
        }

        public static void Sample() => Verify("Sample", NfsWorldSetup.SampleScene);
        [MenuItem("Alabama/NFS World/Verify Downtown Rockport")]
        public static void District() => Verify("District", NfsWorldSetup.DistrictScene);
        public static void Style() => Verify("Style", NfsWorldStylePreview.ScenePath);
        public static void Art() => Verify("Art", NfsWorldArtPass.ScenePath);
        public static void ArtDirection() => Verify("ArtDirection", NfsWorldArtDirection.ScenePath);
        public static void RuntimeContent() => Verify("RuntimeContent", NfsWorldDistrictRuntimeSetup.DowntownScene);

        private static void Verify(string variant, string path)
        {
            EditorSceneManager.OpenScene(path);
            Physics.SyncTransforms();
            var report = new Report { variant = variant };
            var failures = new List<string>();
            foreach (var root in UnityEngine.SceneManagement.SceneManager.GetActiveScene().GetRootGameObjects())
            {
                if (root.name == "E46 driver car") continue;
                foreach (var filter in root.GetComponentsInChildren<MeshFilter>())
                {
                    var renderer = filter.GetComponent<MeshRenderer>();
                    bool shadowOnly = renderer != null && renderer.shadowCastingMode == UnityEngine.Rendering.ShadowCastingMode.ShadowsOnly;
                    if (shadowOnly)
                    {
                        report.shadowProxyMeshes++;
                        report.shadowProxyTriangles += filter.sharedMesh.triangles.Length / 3;
                    }
                    if (renderer != null && renderer.enabled)
                    {
                        if (!shadowOnly)
                        {
                            report.visibleMeshes++;
                            report.visibleTriangles += filter.sharedMesh.triangles.Length / 3;
                        }
                        NfsWorldSetup.Require(renderer.sharedMaterials.All(m => m != null &&
                            (m.shader.name == "Universal Render Pipeline/Lit" || m.shader.name == "Alabama/Map Texture Array")),
                            "Missing/mismatched URP material: " + filter.name);
                        if (renderer.sharedMaterial.shader.name == "Alabama/Map Texture Array")
                        {
                            var array = renderer.sharedMaterial.GetTexture("_BaseArray") as Texture2DArray;
                            NfsWorldSetup.Require(array != null && array.depth > 0, "Missing source texture array: " + filter.name);
                            var layers = new List<Vector2>();
                            filter.sharedMesh.GetUVs(2, layers);
                            NfsWorldSetup.Require(layers.Count == filter.sharedMesh.vertexCount &&
                                layers.All(uv => float.IsFinite(uv.x) && uv.x >= 0 && uv.x < array.depth &&
                                    float.IsFinite(uv.y) && uv.y >= 0 && uv.y <= 1),
                                "Texture layer/cutoff metadata is invalid: " + filter.name);
                        }
                    }
                    var collider = filter.GetComponent<MeshCollider>();
                    if (collider == null) continue;
                    report.meshColliders++;
                    NfsWorldSetup.Require(!collider.convex && collider.attachedRigidbody == null && collider.sharedMesh == filter.sharedMesh,
                        "Collision must use the static source-aligned mesh: " + filter.name);
                    report.collisionTriangles += collider.sharedMesh.triangles.Length / 3;
                    if (root.name != "RoadsPhysical") continue;
                    report.roadMeshes++;
                    NfsWorldSetup.Require(!renderer.enabled, "Physical road must not duplicate rendered road: " + filter.name);
                    var vertices = filter.sharedMesh.vertices;
                    var indices = filter.sharedMesh.triangles;
                    int sampled = 0;
                    // Query this collider directly so upper bridge levels cannot mask the target road.
                    int step = Mathf.Max(3, indices.Length / 12 / 3 * 3);
                    for (int i = 0; i + 2 < indices.Length && sampled < 12; i += step)
                    {
                        var a = filter.transform.TransformPoint(vertices[indices[i]]);
                        var b = filter.transform.TransformPoint(vertices[indices[i + 1]]);
                        var c = filter.transform.TransformPoint(vertices[indices[i + 2]]);
                        var normal = Vector3.Cross(b - a, c - a).normalized;
                        if (normal.y < .5f) continue; // Bridge undersides are ceiling collision, not driving surfaces.
                        var point = (a + b + c) / 3;
                        report.roadSamples++;
                        sampled++;
                        // Start close to this face: stacked surfaces within a mesh must not mask each other.
                        if (!collider.Raycast(new Ray(point + normal * .01f, -normal), out var hit, .04f))
                        {
                            failures.Add(filter.name + ": downward road ray missed " + point);
                            continue;
                        }
                        float error = Vector3.Distance(hit.point, point);
                        report.maximumRoadSampleError = Mathf.Max(report.maximumRoadSampleError, error);
                        if (error > .02f) failures.Add(filter.name + ": road error " + error);
                        else report.matchedRoadSamples++;
                    }
                    if (sampled == 0) report.undersideOnlyRoadMeshes++;
                }
            }
            NfsWorldSetup.Require(report.roadMeshes > 0 && report.roadSamples > 0, "No road collision samples found.");
            report.failures = failures.ToArray();
            string output = Path.GetFullPath(Path.Combine(Application.dataPath, "../../artifacts/NfsWorld/collision-" + variant + ".json"));
            File.WriteAllText(output, JsonUtility.ToJson(report, true));
            NfsWorldSetup.Require(failures.Count == 0, $"{failures.Count} collision sample failures; read {output}");
            Debug.Log($"NFS World {variant}: {report.matchedRoadSamples}/{report.roadSamples} road samples matched across {report.roadMeshes} meshes. Max error {report.maximumRoadSampleError} m.");
        }

        [MenuItem("Alabama/NFS World/Build Local Windows Prototype")]
        public static void Build()
        {
            District();
            string root = Path.GetFullPath(Path.Combine(Application.dataPath, "../.."));
            string output = Path.Combine(root, "builds/nfs-world/Alabama.exe");
            Directory.CreateDirectory(Path.GetDirectoryName(output));
            PlayerSettings.enableFrameTimingStats = true;
            NfsWorldOptimization.Apply();
            // Add opt-in instrumentation even when a scene was generated before the benchmark code.
            if (UnityEngine.Object.FindFirstObjectByType<NfsWorldBenchmark>() == null)
                new GameObject("Optional local map benchmark").AddComponent<NfsWorldBenchmark>();
            EditorSceneManager.SaveScene(UnityEngine.SceneManagement.SceneManager.GetActiveScene());
            var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
            {
                scenes = new[] { NfsWorldSetup.DistrictScene }, locationPathName = output,
                target = BuildTarget.StandaloneWindows64, options = BuildOptions.StrictMode
            });
            NfsWorldSetup.Require(report.summary.result == BuildResult.Succeeded && report.summary.totalErrors == 0,
                "NFS World build failed: " + report.summary.result);
            Debug.Log("NFS World local Windows prototype built: " + output);
        }

        public static void BuildStyle()
        {
            Style();
            BuildVisualVariant(NfsWorldStylePreview.ScenePath, "nfs-world-style");
        }

        public static void BuildArt()
        {
            Art();
            BuildVisualVariant(NfsWorldArtPass.ScenePath, "nfs-world-art");
        }

        public static void BuildArtDirection()
        {
            NfsWorldArtDirection.Verify();
            BuildVisualVariant(NfsWorldArtDirection.ScenePath, "nfs-world-art-direction");
        }

        private static void BuildVisualVariant(string scenePath, string buildFolder)
        {
            var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
            var benchmark = UnityEngine.Object.FindFirstObjectByType<NfsWorldBenchmark>();
            if (benchmark == null) new GameObject("Optional local map benchmark").AddComponent<NfsWorldBenchmark>();
            EditorSceneManager.SaveScene(scene);
            PlayerSettings.enableFrameTimingStats = true;
            string root = Path.GetFullPath(Path.Combine(Application.dataPath, "../.."));
            string output = Path.Combine(root, "builds", buildFolder, "Alabama.exe");
            Directory.CreateDirectory(Path.GetDirectoryName(output));
            var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
            {
                scenes = new[] { scenePath }, locationPathName = output,
                target = BuildTarget.StandaloneWindows64, options = BuildOptions.StrictMode
            });
            NfsWorldSetup.Require(report.summary.result == BuildResult.Succeeded && report.summary.totalErrors == 0,
                "NFS World style build failed: " + report.summary.result);
        }
    }
}
