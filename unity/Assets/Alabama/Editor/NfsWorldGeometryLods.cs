using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Alabama.Driving;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

namespace Alabama.Editor
{
    public static class NfsWorldGeometryLods
    {
        private static string Root => Path.GetFullPath(Path.Combine(Application.dataPath, "../.."));
        private static string Exchange => Path.Combine(Root, "artifacts/NfsWorld/DeveloperBArt/LodExchange");
        private const string Assets = NfsWorldArtDirection.DirectoryPath + "/Lods";
        [Serializable] private sealed class Record { public string name; public string sourceSha256; public int nearTriangles, middleTriangles, farTriangles; }
        [Serializable] private sealed class Manifest { public Record[] meshes; }

        public static void Export()
        {
            EditorSceneManager.OpenScene(NfsWorldArtDirection.ScenePath);
            Directory.CreateDirectory(Exchange);
            var filters = GameObject.Find("Optimized district visuals").GetComponentsInChildren<MeshFilter>()
                .Where(f => f.name.EndsWith("_lod", StringComparison.Ordinal)).ToArray();
            foreach (var filter in filters) WriteMesh(Path.Combine(Exchange, filter.name + ".meshbin"), filter.sharedMesh);
            File.WriteAllText(Path.Combine(Exchange, "inputs.json"), JsonUtility.ToJson(new Manifest
            { meshes = filters.Select(f => new Record { name = f.name, nearTriangles = f.sharedMesh.triangles.Length / 3 }).ToArray() }, true));
            Debug.Log("Exported spatial LOD inputs: " + filters.Length);
        }

        public static void Import()
        {
            EditorSceneManager.OpenScene(NfsWorldArtDirection.ScenePath);
            var root = GameObject.Find("Optimized district visuals");
            var filters = root.GetComponentsInChildren<MeshFilter>().ToDictionary(f => f.name);
            var manifest = JsonUtility.FromJson<Manifest>(File.ReadAllText(Path.Combine(Exchange, "outputs.json")));
            Directory.CreateDirectory(Assets);
            AssetDatabase.Refresh();
            var entries = new List<NfsWorldMeshLods.Entry>();
            int nearCount = 0, middleCount = 0, farCount = 0, rejected = 0;
            using var digest = System.Security.Cryptography.SHA256.Create();
            foreach (var record in manifest.meshes)
            {
                var filter = filters[record.name];
                var near = filter.sharedMesh;
                string hash = BitConverter.ToString(digest.ComputeHash(File.ReadAllBytes(Path.Combine(Exchange, record.name + ".meshbin"))))
                    .Replace("-", "").ToLowerInvariant();
                NfsWorldSetup.Require(hash == record.sourceSha256, "LOD input changed since generation: " + record.name);
                using (var current = new MemoryStream())
                {
                    using var writer = new BinaryWriter(current, System.Text.Encoding.UTF8, true);
                    WriteMesh(writer, near); writer.Flush(); current.Position = 0;
                    string currentHash = BitConverter.ToString(digest.ComputeHash(current)).Replace("-", "").ToLowerInvariant();
                    NfsWorldSetup.Require(hash == currentHash, "Re-export LODs after changing the assembled mesh: " + record.name);
                }
                Mesh Accept(string level)
                {
                    var mesh = ReadMesh(Path.Combine(Exchange, record.name + "." + level + ".meshbin"));
                    bool Valid(Mesh value) => value.triangles.Length < near.triangles.Length &&
                        (value.bounds.min - near.bounds.min).magnitude <= .04f && (value.bounds.max - near.bounds.max).magnitude <= .04f &&
                        NfsWorldVisualOptimization.PreservesBoundaryCurves(near, value, .02f, .001f);
                    if (!Valid(mesh))
                    {
                        UnityEngine.Object.DestroyImmediate(mesh);
                        string fallback = Path.Combine(Exchange, record.name + "." + level + ".planar.meshbin");
                        if (!File.Exists(fallback)) { rejected++; return near; }
                        mesh = ReadMesh(fallback);
                    }
                    if (mesh.triangles.Length >= near.triangles.Length ||
                        (mesh.bounds.min - near.bounds.min).magnitude > .04f || (mesh.bounds.max - near.bounds.max).magnitude > .04f ||
                        !NfsWorldVisualOptimization.PreservesBoundaryCurves(near, mesh, .02f, .001f))
                    { UnityEngine.Object.DestroyImmediate(mesh); rejected++; return near; }
                    string path = Assets + "/" + record.name + "." + level + ".asset";
                    string layers = NfsWorldArtDirection.DirectoryPath + "/Optimized/Arrays/" +
                        filter.GetComponent<Renderer>().sharedMaterial.name + ".layers.txt";
                    NfsWorldVisualStability.ResolveWindowOverlaps(mesh, NfsWorldVisualStability.WindowLayers(layers));
                    NfsWorldVisualStability.RemoveLayers(mesh, NfsWorldVisualStability.SuppressedLayers(layers));
                    var existing = AssetDatabase.LoadAssetAtPath<Mesh>(path);
                    mesh.name = record.name + " " + level;
                    if (existing == null) AssetDatabase.CreateAsset(mesh, path);
                    else { EditorUtility.CopySerialized(mesh, existing); UnityEngine.Object.DestroyImmediate(mesh); mesh = existing; }
                    return mesh;
                }
                var middle = Accept("middle");
                var far = Accept("far");
                if (far.triangles.Length > middle.triangles.Length) far = middle;
                nearCount += near.triangles.Length / 3;
                middleCount += middle.triangles.Length / 3;
                farCount += far.triangles.Length / 3;
                if (near == middle && near == far) continue;
                // Runtime mesh swaps must use their own buffers, rather than the
                // build-time copy made by Unity static batching. Keep occlusion flags.
                GameObjectUtility.SetStaticEditorFlags(filter.gameObject,
                    GameObjectUtility.GetStaticEditorFlags(filter.gameObject) & ~StaticEditorFlags.BatchingStatic);
                entries.Add(new NfsWorldMeshLods.Entry { target = filter, near = near, middle = middle, far = far,
                    bounds = filter.GetComponent<Renderer>().bounds });
            }
            var owner = root.GetComponent<NfsWorldMeshLods>();
            if (owner == null) owner = root.AddComponent<NfsWorldMeshLods>();
            owner.Configure(entries.ToArray());
            EditorUtility.SetDirty(owner);
            AssetDatabase.SaveAssets();
            EditorSceneManager.SaveScene(UnityEngine.SceneManagement.SceneManager.GetActiveScene());
            File.WriteAllText(Path.Combine(Root, "artifacts/NfsWorld/DeveloperBArt/lod-validation.json"),
                $"{{\"spatialGroups\":{manifest.meshes.Length},\"activeLodGroups\":{entries.Count},\"nearTriangles\":{nearCount},\"middleTriangles\":{middleCount},\"farTriangles\":{farCount},\"rejectedCandidates\":{rejected},\"middleMetres\":80,\"farMetres\":200,\"roadMeshesChanged\":false,\"collisionChanged\":false}}\n");
        }

        private static void WriteMesh(string path, Mesh mesh)
        {
            using var writer = new BinaryWriter(File.Create(path));
            WriteMesh(writer, mesh);
        }

        private static void WriteMesh(BinaryWriter writer, Mesh mesh)
        {
            var vertices = mesh.vertices; var normals = mesh.normals; var uv = mesh.uv;
            var layers = new List<Vector2>(); mesh.GetUVs(2, layers);
            var triangles = mesh.triangles;
            writer.Write(1); writer.Write(vertices.Length); writer.Write(triangles.Length);
            for (int i = 0; i < vertices.Length; i++)
            {
                writer.Write(vertices[i].x); writer.Write(vertices[i].y); writer.Write(vertices[i].z);
                writer.Write(normals[i].x); writer.Write(normals[i].y); writer.Write(normals[i].z);
                writer.Write(uv[i].x); writer.Write(uv[i].y); writer.Write(layers[i].x); writer.Write(layers[i].y);
            }
            foreach (int index in triangles) writer.Write(index);
        }

        internal static Mesh ReadMesh(string path)
        {
            using var reader = new BinaryReader(File.OpenRead(path));
            NfsWorldSetup.Require(reader.ReadInt32() == 1, "Unknown LOD exchange format.");
            int vertices = reader.ReadInt32(), indices = reader.ReadInt32();
            var positions = new Vector3[vertices]; var normals = new Vector3[vertices];
            var uv = new Vector2[vertices]; var layers = new List<Vector2>(vertices);
            for (int i = 0; i < vertices; i++)
            {
                positions[i] = new Vector3(reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle());
                normals[i] = new Vector3(reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle());
                uv[i] = new Vector2(reader.ReadSingle(), reader.ReadSingle());
                layers.Add(new Vector2(reader.ReadSingle(), reader.ReadSingle()));
            }
            var triangles = new int[indices]; for (int i = 0; i < indices; i++) triangles[i] = reader.ReadInt32();
            var mesh = new Mesh { indexFormat = IndexFormat.UInt32, vertices = positions, normals = normals,
                uv = uv, triangles = triangles };
            mesh.SetUVs(2, layers); mesh.RecalculateBounds();
            return mesh;
        }
    }
}
