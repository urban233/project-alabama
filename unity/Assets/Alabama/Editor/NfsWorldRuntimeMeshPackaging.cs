using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Alabama.Editor
{
    /// <summary>Exact runtime mesh copies in GUID buckets; original artwork remains untouched.</summary>
    internal static class NfsWorldRuntimeMeshPackaging
    {
        internal static void Apply(Scene scene, string sourceRoot, string destination)
        {
            var filters = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<MeshFilter>(true)).ToArray();
            var sources = filters.Select(f => f.sharedMesh).Where(m => m != null).Distinct()
                .Where(m => AssetDatabase.GetAssetPath(m).StartsWith(sourceRoot + "/", StringComparison.Ordinal) &&
                    AssetDatabase.GetAssetPath(m).EndsWith(".asset", StringComparison.Ordinal) &&
                    !AssetDatabase.GetAssetPath(m).StartsWith(destination + "/", StringComparison.Ordinal)).ToArray();
            if (sources.Length == 0) return;
            Directory.CreateDirectory(destination); AssetDatabase.Refresh();
            var replacements = new Dictionary<Mesh, Mesh>();
            int verified = 0;
            foreach (var bucket in sources.GroupBy(m => Key(m).Substring(0, 2)).OrderBy(g => g.Key))
            {
                string path = destination + "/bucket_" + bucket.Key + ".asset";
                if (!File.Exists(path)) AssetDatabase.CreateAsset(new Mesh { name = "Runtime mesh bucket " + bucket.Key }, path);
                var existing = AssetDatabase.LoadAllAssetsAtPath(path).OfType<Mesh>().ToDictionary(m => m.name);
                AssetDatabase.StartAssetEditing();
                try
                {
                    foreach (var original in bucket)
                    {
                        string key = Key(original);
                        if (!existing.TryGetValue(key, out var copy))
                        {
                            copy = UnityEngine.Object.Instantiate(original); copy.name = key;
                            AssetDatabase.AddObjectToAsset(copy, path);
                        }
                        else { EditorUtility.CopySerialized(original, copy); copy.name = key; }
                        VerifyCopy(original, copy);
                        EditorUtility.SetDirty(copy); replacements.Add(original, copy); verified++;
                    }
                }
                finally { AssetDatabase.StopAssetEditing(); }
                AssetDatabase.SaveAssets();
            }
            foreach (var filter in filters)
                if (filter.sharedMesh != null && replacements.TryGetValue(filter.sharedMesh, out var copy))
                { filter.sharedMesh = copy; EditorUtility.SetDirty(filter); }
            foreach (var collider in scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<MeshCollider>(true)))
                if (collider.sharedMesh != null && replacements.TryGetValue(collider.sharedMesh, out var copy))
                { collider.sharedMesh = copy; EditorUtility.SetDirty(collider); }
            Physics.SyncTransforms(); EditorSceneManager.SaveScene(scene);
            string evidence = Path.GetFullPath(Path.Combine(Application.dataPath, "../../artifacts/NfsWorld/Rosewood/mesh-packaging-" + scene.name + ".json"));
            File.WriteAllText(evidence, $"{{\"passed\":true,\"meshesVerified\":{verified},\"vertexAndIndexBytesIdentical\":true,\"originalAssetsChanged\":false}}\n");
            Debug.Log($"Runtime mesh packaging: {verified} exact copies in {sources.Select(m => Key(m).Substring(0, 2)).Distinct().Count()} buckets for {scene.name}; original assets retained.");
        }

        private static string Key(Mesh mesh)
        {
            NfsWorldSetup.Require(AssetDatabase.TryGetGUIDAndLocalFileIdentifier(mesh, out string guid, out long localId), "Runtime mesh lacks a persistent source identity.");
            return guid + "_" + localId;
        }

        private static void VerifyCopy(Mesh source, Mesh copy)
        {
            NfsWorldSetup.Require(source.vertexCount == copy.vertexCount && source.subMeshCount == copy.subMeshCount &&
                source.indexFormat == copy.indexFormat && source.bounds == copy.bounds && source.isReadable == copy.isReadable &&
                source.blendShapeCount == 0 && copy.blendShapeCount == 0 && source.bindposes.Length == 0 && copy.bindposes.Length == 0,
                "Runtime packaging must retain static source mesh structure: " + source.name);
            using var data = Mesh.AcquireReadOnlyMeshData(new[] { source, copy });
            NfsWorldSetup.Require(data[0].vertexBufferCount == data[1].vertexBufferCount, "Mesh streams changed.");
            for (int stream = 0; stream < data[0].vertexBufferCount; stream++)
            {
                NfsWorldSetup.Require(data[0].GetVertexBufferStride(stream) == data[1].GetVertexBufferStride(stream), "Mesh stream layout changed.");
                Equal(data[0].GetVertexData<byte>(stream), data[1].GetVertexData<byte>(stream));
            }
            Equal(data[0].GetIndexData<byte>(), data[1].GetIndexData<byte>());
            for (int index = 0; index < source.subMeshCount; index++)
                NfsWorldSetup.Require(data[0].GetSubMesh(index).Equals(data[1].GetSubMesh(index)), "Submesh draw ranges changed.");
        }

        private static void Equal(Unity.Collections.NativeArray<byte> a, Unity.Collections.NativeArray<byte> b)
        {
            NfsWorldSetup.Require(a.Length == b.Length, "Runtime mesh buffer length changed.");
            for (int index = 0; index < a.Length; index++)
                if (a[index] != b[index]) throw new InvalidOperationException("Runtime mesh buffer bytes changed.");
        }
    }
}
