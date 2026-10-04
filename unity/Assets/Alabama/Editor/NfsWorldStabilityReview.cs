using System;
using System.IO;
using System.Linq;
using Alabama.Driving;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using Object = UnityEngine.Object;

namespace Alabama.Editor
{
    public static class NfsWorldStabilityReview
    {
        public static void BuildReadabilityGame()
        {
            NfsWorldSceneryCollision.Apply();
            NfsWorldVisualStability.Apply();
            NfsWorldArtDirection.VerifyRuntime();
            VerifyReadabilityAssets();
            NfsWorldArtDirection.BuildGame();
        }

        public static void VerifyReadabilityAssets()
        {
            // BuildGame leaves the runtime host and its additive content open.
            var root = GameObject.Find("Optimized district visuals");
            var meshes = new System.Collections.Generic.Dictionary<Mesh, string>();
            foreach (var filter in root.GetComponentsInChildren<MeshFilter>())
                meshes[filter.sharedMesh] = filter.GetComponent<Renderer>().sharedMaterial.name;
            var lods = new SerializedObject(root.GetComponent<NfsWorldMeshLods>()).FindProperty("entries");
            for (int i = 0; i < lods.arraySize; i++)
            {
                var entry = lods.GetArrayElementAtIndex(i);
                var target = (MeshFilter)entry.FindPropertyRelative("target").objectReferenceValue;
                string material = target.GetComponent<Renderer>().sharedMaterial.name;
                foreach (string level in new[] { "near", "middle", "far" })
                    meshes[(Mesh)entry.FindPropertyRelative(level).objectReferenceValue] = material;
            }
            int removedCardTrianglesRemaining = 0;
            foreach (var pair in meshes)
            {
                string layers = NfsWorldArtDirection.DirectoryPath + "/Optimized/Arrays/" + pair.Value + ".layers.txt";
                var suppressed = NfsWorldVisualStability.SuppressedLayers(layers);
                var metadata = new System.Collections.Generic.List<Vector2>(); pair.Key.GetUVs(2, metadata);
                var indices = pair.Key.triangles;
                for (int i = 0; i < indices.Length; i += 3)
                    if (suppressed.Contains((int)metadata[indices[i]].x)) removedCardTrianglesRemaining++;
                var material = AssetDatabase.LoadAssetAtPath<Material>(layers.Replace(".layers.txt", ".mat"));
                NfsWorldSetup.Require(material.GetTexture("_WindowWeights") != null,
                    "Missing reviewed window weights: " + pair.Value);
            }
            NfsWorldSetup.Require(removedCardTrianglesRemaining == 0, "Opaque sign light cards remain in active/LOD geometry.");
            string output = Path.GetFullPath(Path.Combine(Application.dataPath, "../../artifacts/NfsWorld/Stability/readability-verification.json"));
            File.WriteAllText(output, $"{{\"activeAndLodMeshes\":{meshes.Count},\"opaqueSignCardTrianglesRemaining\":0,\"windowWeightsPresent\":true}}\n");
            Debug.Log("Readability geometry and material verification passed for " + meshes.Count + " active/LOD meshes.");
        }

        public static void PrepareMapReview()
        {
            EditorSceneManager.OpenScene(NfsWorldArtDirection.ScenePath);
            var signs = new System.Collections.Generic.List<(Vector3 point, Vector3 normal)>();
            var panes = new System.Collections.Generic.List<(Vector3 point, Vector3 normal)>();
            foreach (var filter in GameObject.Find("Optimized district visuals").GetComponentsInChildren<MeshFilter>())
            {
                string layers = NfsWorldArtDirection.DirectoryPath + "/Optimized/Arrays/" + filter.GetComponent<Renderer>().sharedMaterial.name + ".layers.txt";
                var windows = NfsWorldVisualStability.WindowLayers(layers);
                var lights = NfsWorldVisualStability.SuppressedLayers(layers);
                if (windows.Count == 0 && lights.Count == 0) continue;
                var mesh = filter.sharedMesh; var points = mesh.vertices; var normals = mesh.normals;
                var indices = mesh.triangles; var uv = new System.Collections.Generic.List<Vector2>(); mesh.GetUVs(2, uv);
                for (int i = 0; i < indices.Length; i += 3)
                {
                    int layer = (int)uv[indices[i]].x;
                    if (!windows.Contains(layer) && !lights.Contains(layer)) continue;
                    var point = (points[indices[i]] + points[indices[i+1]] + points[indices[i+2]]) / 3;
                    var normal = (normals[indices[i]] + normals[indices[i+1]] + normals[indices[i+2]]).normalized;
                    if (normal.sqrMagnitude < .5f || Mathf.Abs(normal.y) > .3f) continue;
                    if (lights.Contains(layer)) signs.Add((point, normal));
                    else if (point.y > 2 && point.y < 18) panes.Add((point, normal));
                }
            }
            var views = new System.Collections.Generic.List<NfsWorldMapReview.View>();
            foreach (var category in new[] { (name: "sign", values: signs), (name: "window", values: panes) })
            {
                var chosen = new System.Collections.Generic.List<Vector3>();
                foreach (var sample in category.values.OrderBy(p => p.point.sqrMagnitude))
                {
                    if (chosen.Any(p => Vector3.Distance(p, sample.point) < 35)) continue;
                    chosen.Add(sample.point);
                    views.Add(new NfsWorldMapReview.View { label = category.name + "-" + chosen.Count.ToString("D2"),
                        position = sample.point + sample.normal * (category.name == "sign" ? 18 : 9), target = sample.point });
                    if (chosen.Count == 12) break;
                }
            }
            string output = Path.GetFullPath(Path.Combine(Application.dataPath, "../../artifacts/NfsWorld/Stability/map-review-plan.json"));
            File.WriteAllText(output, JsonUtility.ToJson(new NfsWorldMapReview.Plan { views = views.ToArray() }, true));
            Debug.Log("Map review views: " + views.Count);
        }
        public static void CaptureWindows()
        {
            EditorSceneManager.OpenScene(NfsWorldArtDirection.ScenePath);
            warmup = 0;
            EditorApplication.update += CaptureWindowFrames;
        }

        private static void CaptureWindowFrames()
        {
            if (++warmup < 30 || EditorApplication.isCompiling || EditorApplication.isUpdating) return;
            EditorApplication.update -= CaptureWindowFrames;
            int exit = 0;
            var settings = Object.FindFirstObjectByType<NfsWorldRenderSettings>();
            Mesh original = null;
            try
            {
                settings.Apply();
                string root = Path.GetFullPath(Path.Combine(Application.dataPath, "../.."));
                const string batch = "256x256_DXT1_True_False_1_-1_1000_1_lod";
                var filter = GameObject.Find("Optimized district visuals").GetComponentsInChildren<MeshFilter>().Single(f => f.name == batch);
                var corrected = filter.sharedMesh;
                original = NfsWorldGeometryLods.ReadMesh(Path.Combine(root, "artifacts/NfsWorld/DeveloperBArt/LodExchange/" + batch + ".meshbin"));
                var camera = Camera.main;
                camera.useOcclusionCulling = false;
                var centre = new Vector3(442.2f, 11.5f, -155.5f);
                var normal = new Vector3(-.866f, 0, .5f);
                foreach (string variant in new[] { "windows-before", "windows-after" })
                {
                    filter.sharedMesh = variant == "windows-before" ? original : corrected;
                    var directory = Path.Combine(root, "artifacts/NfsWorld/Stability", variant); Directory.CreateDirectory(directory);
                    for (int frame = 0; frame < 8; frame++)
                    {
                        camera.transform.position = centre + normal * 9 + Vector3.up * .4f + Vector3.right * frame * .025f;
                        camera.transform.LookAt(centre);
                        Object.FindFirstObjectByType<NfsWorldDistanceCulling>()?.Refresh();
                        VehicleCapture.Save(camera, Path.Combine(directory, frame.ToString("D2") + ".png"));
                    }
                }
                filter.sharedMesh = corrected;
            }
            catch (Exception error) { Debug.LogException(error); exit = 1; }
            finally { settings.Restore(); if (original != null) Object.DestroyImmediate(original); }
            if (Application.isBatchMode) EditorApplication.Exit(exit);
        }

        public static void AuditGeometry()
        {
            EditorSceneManager.OpenScene(NfsWorldArtDirection.ScenePath);
            var triangles = new System.Collections.Generic.Dictionary<string, string>();
            var duplicates = new System.Collections.Generic.Dictionary<string, int>();
            int scanned = 0, invalidNormals = 0;
            foreach (var filter in GameObject.Find("Optimized district visuals").GetComponentsInChildren<MeshFilter>())
            {
                var mesh = filter.sharedMesh;
                if (mesh.bounds.SqrDistance(Vector3.zero) > 350 * 350) continue;
                var vertices = mesh.vertices; var indices = mesh.triangles; var uv = new System.Collections.Generic.List<Vector2>();
                mesh.GetUVs(2, uv);
                invalidNormals += mesh.normals.Count(n => !float.IsFinite(n.x) || n.sqrMagnitude < .01f);
                for (int i = 0; i < indices.Length; i += 3)
                {
                    string Point(int index) { var p = vertices[index]; return $"{Mathf.RoundToInt(p.x*1000)},{Mathf.RoundToInt(p.y*1000)},{Mathf.RoundToInt(p.z*1000)}"; }
                    string key = string.Join("/", new[] { Point(indices[i]), Point(indices[i+1]), Point(indices[i+2]) }.OrderBy(v => v, StringComparer.Ordinal));
                    string owner = filter.name + ":layer=" + uv[indices[i]].x;
                    if (triangles.TryGetValue(key, out string previous))
                    {
                        string pair = previous + " <> " + owner;
                        duplicates[pair] = duplicates.TryGetValue(pair, out int count) ? count+1 : 1;
                    }
                    else triangles.Add(key, owner);
                    scanned++;
                }
            }
            var path = Path.GetFullPath(Path.Combine(Application.dataPath, "../../artifacts/NfsWorld/Stability/geometry-audit.txt"));
            File.WriteAllText(path, $"scanned={scanned}, invalidNormals={invalidNormals}, duplicateTriangles={duplicates.Values.Sum()}\n" +
                string.Join("\n", duplicates.OrderByDescending(p => p.Value).Select(p => p.Value + " " + p.Key)));
        }
        private static int warmup;
        public static void Diagnose()
        {
            EditorSceneManager.OpenScene(NfsWorldArtDirection.ScenePath);
            warmup = 0;
            EditorApplication.update += Capture;
        }

        private static void Capture()
        {
            if (++warmup < 30 || EditorApplication.isCompiling || EditorApplication.isUpdating) return;
            EditorApplication.update -= Capture;
            int exit = 0;
            var settings = Object.FindFirstObjectByType<NfsWorldRenderSettings>();
            settings.Apply();
            var original = (UniversalRenderPipelineAsset)QualitySettings.renderPipeline;
            var pipeline = Object.Instantiate(original);
            var renderer = Object.Instantiate((UniversalRendererData)original.rendererDataList[0]);
            var features = renderer.rendererFeatures.Select(Object.Instantiate).ToArray();
            renderer.rendererFeatures.Clear(); renderer.rendererFeatures.AddRange(features);
            var data = new SerializedObject(pipeline);
            data.FindProperty("m_RendererDataList").GetArrayElementAtIndex(0).objectReferenceValue = renderer;
            data.ApplyModifiedPropertiesWithoutUndo();
            QualitySettings.renderPipeline = pipeline;
            try
            {
                string output = Path.GetFullPath(Path.Combine(Application.dataPath, "../../artifacts/NfsWorld/Stability"));
                Directory.CreateDirectory(output);
                var camera = Camera.main;
                var origin = camera.transform.position;
                var culling = Object.FindFirstObjectByType<NfsWorldDistanceCulling>();
                var lights = Object.FindObjectsByType<Light>(FindObjectsSortMode.None);
                File.WriteAllText(Path.Combine(output, "audit.txt"),
                    $"Camera near={camera.nearClipPlane}, far={camera.farClipPlane}, occlusion={camera.useOcclusionCulling}\n" +
                    $"Shadow bias={pipeline.shadowDepthBias}, normal bias={pipeline.shadowNormalBias}, distance={pipeline.shadowDistance}\n" +
                    string.Join("\n", lights.Select(l => $"Light {l.name}: {l.type}, intensity={l.intensity}, shadows={l.shadows}")));
                foreach (string variant in new[] { "baseline", "no-occlusion", "no-ao", "no-priming", "stable-ao" })
                {
                    camera.useOcclusionCulling = variant != "no-occlusion";
                    renderer.depthPrimingMode = variant == "no-priming" ? DepthPrimingMode.Disabled : DepthPrimingMode.Forced;
                    foreach (var feature in features)
                    {
                        feature.SetActive(variant != "no-ao");
                        if (feature.GetType().Name != "ScreenSpaceAmbientOcclusion") continue;
                        var serialized = new SerializedObject(feature);
                        var ao = serialized.FindProperty("m_Settings");
                        ao.FindPropertyRelative("AOMethod").intValue = variant == "stable-ao" ? 1 : 0;
                        serialized.ApplyModifiedPropertiesWithoutUndo();
                    }
                    renderer.SetDirty();
                    var folder = Path.Combine(output, variant); Directory.CreateDirectory(folder);
                    for (int frame = 0; frame < 12; frame++)
                    {
                        // Four identical frames reveal stochastic shading; then a slow start.
                        camera.transform.position = origin + camera.transform.forward * Mathf.Max(0, frame - 3) * .3f;
                        culling?.Refresh();
                        Object.FindFirstObjectByType<NfsWorldMeshLods>()?.Refresh(camera.transform.position);
                        VehicleCapture.Save(camera, Path.Combine(folder, frame.ToString("D2") + ".png"));
                    }
                }
            }
            catch (Exception error) { Debug.LogException(error); exit = 1; }
            finally
            {
                QualitySettings.renderPipeline = original;
                settings.Restore();
                Object.DestroyImmediate(pipeline); Object.DestroyImmediate(renderer);
                foreach (var feature in features) Object.DestroyImmediate(feature);
            }
            if (Application.isBatchMode) EditorApplication.Exit(exit);
        }
    }
}
