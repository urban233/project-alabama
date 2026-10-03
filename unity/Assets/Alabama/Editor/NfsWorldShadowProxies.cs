using System.Collections.Generic;
using System.IO;
using Alabama.Driving;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

namespace Alabama.Editor
{
    /// <summary>Exact source surfaces in smaller shadow batches for tighter cascade culling.</summary>
    public static class NfsWorldShadowProxies
    {
        private sealed class Batch
        {
            public Material material;
            public float distance;
            public List<CombineInstance> meshes = new List<CombineInstance>();
        }
        private static string AssetPath = NfsWorldSetup.BasePath + "/ArtPass/ShadowChunks";

        public static void Run()
        {
            var scene = EditorSceneManager.OpenScene(NfsWorldArtPass.ScenePath);
            AssetPath = NfsWorldSetup.BasePath + "/ArtPass/ShadowChunks";
            PartitionLoadedScene(scene, "artifacts/NfsWorld/shadow-proxies.json");
        }

        internal static void RunContent(UnityEngine.SceneManagement.Scene scene, string assetRoot)
        {
            AssetPath = assetRoot + "/ArtPass/ShadowChunks";
            PartitionLoadedScene(scene, "artifacts/NfsWorld/Rosewood/shadow-proxies.json");
        }

        private static void PartitionLoadedScene(UnityEngine.SceneManagement.Scene scene, string evidence)
        {
            var root = GameObject.Find("Optimized district visuals");
            NfsWorldSetup.Require(root != null, "Art scene is absent.");
            var old = GameObject.Find("Spatial shadow casters");
            if (old != null) UnityEngine.Object.DestroyImmediate(old);
            var shadows = new GameObject("Spatial shadow casters");
            Directory.CreateDirectory(AssetPath); AssetDatabase.Refresh();
            var culling = UnityEngine.Object.FindFirstObjectByType<NfsWorldDistanceCulling>();
            var batches = new Dictionary<string, Batch>();
            var temporary = new List<Mesh>();
            int sourceTriangles = 0, shadowTriangles = 0;
            foreach (var renderer in root.GetComponentsInChildren<MeshRenderer>())
            {
                NfsWorldSetup.Require(renderer.transform.localToWorldMatrix == Matrix4x4.identity,
                    "Shadow partition expects already world-baked source meshes.");
                var mesh = renderer.GetComponent<MeshFilter>().sharedMesh;
                float limit = culling.DistanceFor(renderer);
                sourceTriangles += mesh.triangles.Length / 3;
                foreach (var chunk in NfsWorldVisualOptimization.Partition(mesh, 32))
                {
                    string key = renderer.sharedMaterial.name + "_" + chunk.cell.x + "_" + chunk.cell.y + "_" + (int)limit;
                    if (!batches.TryGetValue(key, out var batch))
                        batches[key] = batch = new Batch { material = renderer.sharedMaterial, distance = limit };
                    batch.meshes.Add(new CombineInstance { mesh = chunk.mesh, transform = Matrix4x4.identity });
                    temporary.Add(chunk.mesh);
                }
                renderer.shadowCastingMode = ShadowCastingMode.Off;
            }
            var targets = new Dictionary<float, List<Renderer>>();
            AssetDatabase.StartAssetEditing();
            try
            {
                int processed = 0;
                foreach (var pair in batches)
                {
                    var mesh = new Mesh { name = pair.Key, indexFormat = IndexFormat.UInt32 };
                    mesh.CombineMeshes(pair.Value.meshes.ToArray(), true, true, false); mesh.RecalculateBounds();
                    shadowTriangles += mesh.triangles.Length / 3;
                    string asset = AssetPath + "/" + pair.Key + ".asset";
                    var existing = AssetDatabase.LoadAssetAtPath<Mesh>(asset);
                    if (existing == null) AssetDatabase.CreateAsset(mesh, asset);
                    else { EditorUtility.CopySerialized(mesh, existing); UnityEngine.Object.DestroyImmediate(mesh); mesh = existing; }
                    var obj = new GameObject(pair.Key); obj.transform.SetParent(shadows.transform, false);
                    obj.AddComponent<MeshFilter>().sharedMesh = mesh;
                    var renderer = obj.AddComponent<MeshRenderer>(); renderer.sharedMaterial = pair.Value.material;
                    renderer.shadowCastingMode = ShadowCastingMode.ShadowsOnly;
                    if (!targets.TryGetValue(pair.Value.distance, out var group)) targets[pair.Value.distance] = group = new List<Renderer>();
                    group.Add(renderer);
                    if (++processed % 1000 == 0) Debug.Log($"Shadow batches saved: {processed}/{batches.Count}.");
                }
            }
            finally { AssetDatabase.StopAssetEditing(); }
            foreach (var mesh in temporary) UnityEngine.Object.DestroyImmediate(mesh);
            NfsWorldSetup.Require(sourceTriangles == shadowTriangles, "Shadow partition lost or duplicated triangles.");
            foreach (var group in targets) culling.AddTargets(group.Value.ToArray(), group.Key);
            EditorUtility.SetDirty(culling); AssetDatabase.SaveAssets(); EditorSceneManager.SaveScene(scene);
            File.WriteAllText(Path.GetFullPath(Path.Combine(Application.dataPath, "../../" + evidence)),
                $"{{\"shadowBatches\":{batches.Count},\"chunkMetres\":32,\"sourceTriangles\":{sourceTriangles},\"shadowTriangles\":{shadowTriangles},\"visibleGeometryChanged\":false,\"collisionChanged\":false}}\n");
            Debug.Log($"Exact shadow surfaces: {sourceTriangles} triangles in {batches.Count} 32-metre batches.");
        }
    }
}
