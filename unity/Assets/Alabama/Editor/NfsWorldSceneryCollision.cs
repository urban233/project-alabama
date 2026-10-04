using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Alabama.Districts;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace Alabama.Editor
{
    /// <summary>Supplement sparse source physics with persistent, full-resolution scenery collision.</summary>
    public static class NfsWorldSceneryCollision
    {
        public const string RootName = "Scenery collision";
        private const string AssetRoot = NfsWorldArtDirection.DirectoryPath + "/SceneryCollision";
        private sealed class Batch
        {
            public readonly List<Vector3> points = new List<Vector3>();
            public readonly List<int> indices = new List<int>();
        }
        [Serializable] private sealed class Report
        {
            public int groundMeshes, obstacleMeshes, groundTriangles, obstacleTriangles;
            public int buildings, props, trees;
            public bool sourceCollisionRetained = true;
        }

        public static void BuildGame()
        {
            NfsWorldStabilityReview.BuildReadabilityGame();
        }

        public static void Apply()
        {
            Directory.CreateDirectory(AssetRoot); AssetDatabase.Refresh();
            var target = EditorSceneManager.OpenScene(NfsWorldArtDirection.ScenePath);
            Generate(target);
            EditorSceneManager.SaveScene(target);
            var generated = target.GetRootGameObjects().Single(r => r.name == RootName);
            var retained = Object.Instantiate(generated);
            retained.name = RootName;
            var transfer = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
            SceneManager.MoveGameObjectToScene(retained, transfer);
            var styled = target;
            target = EditorSceneManager.OpenScene(NfsWorldArtDirection.ContentScene, OpenSceneMode.Additive);
            NfsWorldSetup.Require(retained != null, "Generated scenery transfer was lost.");
            foreach (var previous in target.GetRootGameObjects().Where(r => r.name == RootName)) Object.DestroyImmediate(previous);
            SceneManager.MoveGameObjectToScene(retained, target);
            EditorSceneManager.SaveScene(target);
            EditorSceneManager.CloseScene(transfer, true);
            EditorSceneManager.CloseScene(styled, true);
            SceneManager.SetActiveScene(target);
            AssetDatabase.SaveAssets();
        }

        public static void Verify(Scene scene)
        {
            var root = scene.GetRootGameObjects().Single(r => r.name == RootName);
            NfsWorldSetup.Require(root.GetComponentsInChildren<Renderer>().Length == 0 &&
                root.GetComponentsInChildren<DistrictGroundCoverage>().Length == 1,
                "Collision supplement must be independent of visual culling and LODs.");
            var colliders = root.GetComponentsInChildren<MeshCollider>();
            NfsWorldSetup.Require(colliders.All(c => c.enabled && !c.isTrigger && !c.convex &&
                AssetDatabase.GetAssetPath(c.sharedMesh).StartsWith(AssetRoot + "/")), "Invalid scenery collision assets.");
            foreach (string prefix in new[] { "Buildings_", "Props_", "Trees_", "ground_" })
                NfsWorldSetup.Require(colliders.Any(c => c.name.StartsWith(prefix)), "Missing collision family: " + prefix);
        }

        public static void Generate(Scene target)
        {
            foreach (var previous in target.GetRootGameObjects().Where(r => r.name == RootName))
                Object.DestroyImmediate(previous);
            var contract = NfsWorldSetup.ReadContract("District");
            var materials = contract.materials.ToDictionary(m => m.name);
            var batches = new Dictionary<string, Batch>();
            var textures = new Dictionary<string, Texture2D>();
            var report = new Report();
            var source = EditorSceneManager.OpenScene(NfsWorldSetup.DistrictScene, OpenSceneMode.Additive);
            try
            {
                var filters = source.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<MeshFilter>())
                    .GroupBy(f => f.name).ToDictionary(g => g.Key, g => g.First());
                foreach (var part in contract.parts)
                foreach (var definition in part.meshes)
                {
                    if (!definition.visible || part.category == "Panorama") continue;
                    bool ground = part.category == "Roads" || part.category == "Terrain" ||
                        definition.sourceName.StartsWith("VISROAD", StringComparison.OrdinalIgnoreCase);
                    if (!ground && definition.collision) continue;
                    if (!ground && part.category != "Buildings" && part.category != "Props" && part.category != "Trees") continue;
                    if (Regex.IsMatch(definition.sourceName, "GLOW|SFX|SHADOW|DECAL|SKID|CLOUD|GRASS", RegexOptions.IgnoreCase)) continue;
                    var material = materials[definition.material];
                    if (material.texture == "7bdfdcf633328107ed2e49c9d654662e795ac5b6d3846d17c870a61a89075e3c.png") continue;
                    NfsWorldSetup.Require(filters.TryGetValue(definition.name, out var filter), "Missing source collider mesh: " + definition.name);
                    var mesh = filter.sharedMesh;
                    var matrix = filter.transform.localToWorldMatrix;
                    var points = mesh.vertices.Select(matrix.MultiplyPoint3x4).ToArray();
                    var indices = mesh.triangles; var uv = mesh.uv;
                    Texture2D alpha = null;
                    if (material.alphaClip && !string.IsNullOrEmpty(material.texture))
                    {
                        if (!textures.TryGetValue(material.texture, out alpha))
                        {
                            alpha = new Texture2D(2, 2, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Repeat };
                            NfsWorldSetup.Require(alpha.LoadImage(File.ReadAllBytes(NfsWorldSetup.BasePath + "/Textures/" + material.texture)), "Missing alpha mask.");
                            textures.Add(material.texture, alpha);
                        }
                    }
                    int before = report.obstacleTriangles;
                    for (int i = 0; i < indices.Length; i += 3)
                    {
                        int a = indices[i], b = indices[i+1], c = indices[i+2];
                        if (matrix.determinant < 0) { int swap = a; a = c; c = swap; }
                        AddTriangle(points[a], points[b], points[c], uv[a], uv[b], uv[c], alpha,
                            material.cutoff, ground, part.category, batches, report, 0);
                    }
                    if (!ground && report.obstacleTriangles > before)
                    {
                        if (part.category == "Buildings") report.buildings++;
                        if (part.category == "Props") report.props++;
                        if (part.category == "Trees") report.trees++;
                    }
                }
            }
            finally
            {
                foreach (var texture in textures.Values) Object.DestroyImmediate(texture);
                EditorSceneManager.CloseScene(source, true); SceneManager.SetActiveScene(target);
            }
            var root = new GameObject(RootName); SceneManager.MoveGameObjectToScene(root, target);
            var coverage = new GameObject("Visible ground coverage"); coverage.transform.SetParent(root.transform, false);
            coverage.AddComponent<DistrictGroundCoverage>();
            AssetDatabase.StartAssetEditing();
            try
            {
            foreach (var pair in batches.OrderBy(p => p.Key))
            {
                var mesh = new Mesh { name = pair.Key, indexFormat = IndexFormat.UInt32 };
                mesh.SetVertices(pair.Value.points); mesh.SetTriangles(pair.Value.indices, 0); mesh.RecalculateBounds();
                string path = AssetRoot + "/" + pair.Key + ".asset";
                var existing = AssetDatabase.LoadAssetAtPath<Mesh>(path);
                if (existing == null) AssetDatabase.CreateAsset(mesh, path);
                else { EditorUtility.CopySerialized(mesh, existing); Object.DestroyImmediate(mesh); mesh = existing; }
                EditorUtility.SetDirty(mesh);
                var obj = new GameObject(pair.Key);
                bool ground = pair.Key.StartsWith("ground_");
                obj.transform.SetParent(ground ? coverage.transform : root.transform, false);
                obj.AddComponent<MeshCollider>().sharedMesh = mesh;
                if (ground) report.groundMeshes++; else report.obstacleMeshes++;
            }
            }
            finally { AssetDatabase.StopAssetEditing(); }
            NfsWorldSetup.Require(report.groundTriangles > 0 && report.buildings > 0 && report.props > 0 && report.trees > 0,
                "Scenery collision coverage is incomplete.");
            Physics.SyncTransforms();
            string output = Path.GetFullPath(Path.Combine(Application.dataPath, "../../artifacts/NfsWorld/Stability/scenery-collision.json"));
            Directory.CreateDirectory(Path.GetDirectoryName(output)); File.WriteAllText(output, JsonUtility.ToJson(report, true));
            Debug.Log("Scenery collision: " + JsonUtility.ToJson(report));
        }

        private static void AddTriangle(Vector3 a, Vector3 b, Vector3 c, Vector2 ua, Vector2 ub, Vector2 uc,
            Texture2D alpha, float cutoff, bool ground, string category, Dictionary<string, Batch> batches, Report report, int depth)
        {
            var normal = Vector3.Cross(b-a, c-a);
            if (normal.sqrMagnitude < 1e-10f || (ground && Mathf.Abs(normal.normalized.y) < .5f)) return;
            // Visual source materials render both sides; some visible road faces wind downwards.
            // Coverage still needs an upward query face. Physical support is checked separately.
            if (ground && normal.y < 0)
            { var swap = b; b = c; c = swap; var swapUv = ub; ub = uc; uc = swapUv; }
            // Woody geometry occupies opaque atlas regions. Transparent leaf rectangles must not become solid walls.
            if (alpha != null)
            {
                bool opaque = alpha.GetPixelBilinear(ua.x, ua.y).a >= cutoff &&
                    alpha.GetPixelBilinear(ub.x, ub.y).a >= cutoff && alpha.GetPixelBilinear(uc.x, uc.y).a >= cutoff &&
                    alpha.GetPixelBilinear((ua.x+ub.x)*.5f, (ua.y+ub.y)*.5f).a >= cutoff &&
                    alpha.GetPixelBilinear((ub.x+uc.x)*.5f, (ub.y+uc.y)*.5f).a >= cutoff &&
                    alpha.GetPixelBilinear((uc.x+ua.x)*.5f, (uc.y+ua.y)*.5f).a >= cutoff;
                if (category == "Trees" && !opaque) return;
                if (!opaque && depth < 1 && Mathf.Max((b-a).sqrMagnitude, (c-b).sqrMagnitude, (a-c).sqrMagnitude) > .25f)
                {
                    var ab = (a+b)*.5f; var bc = (b+c)*.5f; var ca = (c+a)*.5f;
                    var uab = (ua+ub)*.5f; var ubc = (ub+uc)*.5f; var uca = (uc+ua)*.5f;
                    AddTriangle(a, ab, ca, ua, uab, uca, alpha, cutoff, ground, category, batches, report, depth+1);
                    AddTriangle(ab, b, bc, uab, ub, ubc, alpha, cutoff, ground, category, batches, report, depth+1);
                    AddTriangle(ca, bc, c, uca, ubc, uc, alpha, cutoff, ground, category, batches, report, depth+1);
                    AddTriangle(ab, bc, ca, uab, ubc, uca, alpha, cutoff, ground, category, batches, report, depth+1);
                    return;
                }
                var centreUv = (ua+ub+uc)/3;
                if (alpha.GetPixelBilinear(centreUv.x, centreUv.y).a < cutoff) return;
            }
            var centre = (a+b+c)/3;
            string key = (ground ? "ground" : category) + "_" + Mathf.FloorToInt(centre.x/128) + "_" + Mathf.FloorToInt(centre.z/128);
            if (!batches.TryGetValue(key, out var batch)) batches.Add(key, batch = new Batch());
            int first = batch.points.Count; batch.points.Add(a); batch.points.Add(b); batch.points.Add(c);
            batch.indices.Add(first); batch.indices.Add(first+1); batch.indices.Add(first+2);
            if (ground) report.groundTriangles++;
            else
            {
                // Imported signs/facades are commonly single sided; vehicles must collide from either direction.
                batch.indices.Add(first+2); batch.indices.Add(first+1); batch.indices.Add(first);
                report.obstacleTriangles += 2;
            }
        }
    }
}
