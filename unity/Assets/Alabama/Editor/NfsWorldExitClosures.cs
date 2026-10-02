using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Alabama.Driving;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Alabama.Editor
{
    /// <summary>Visible low-poly walls at reviewed prototype road exits; source collision stays untouched.</summary>
    public static class NfsWorldExitClosures
    {
        private const string DirectoryPath = NfsWorldSetup.BasePath + "/ArtPass/Exits";
        [Serializable] private sealed class Closure { public string name; public float[] start; public float[] end; public float[] outward; }
        [Serializable] private sealed class Manifest { public bool reviewed; public bool sourceCollisionChanged; public Closure[] closures; }
        private static Vector3 Point(float[] value) => new Vector3(value[0], value[1], value[2]);

        public static void Run()
        {
            var scene = EditorSceneManager.OpenScene(NfsWorldArtPass.ScenePath);
            Apply();
            EditorSceneManager.SaveScene(scene);
        }

        public static void Apply()
        {
            string manifestPath = NfsWorldSetup.BasePath + "/ArtPass/exits.json";
            if (!File.Exists(manifestPath)) return;
            var manifest = JsonUtility.FromJson<Manifest>(File.ReadAllText(manifestPath));
            NfsWorldSetup.Require(manifest.reviewed && !manifest.sourceCollisionChanged && manifest.closures.Length == 6,
                "Review the source-derived six exit spans first.");
            var previous = GameObject.Find("Downtown exit closures");
            if (previous != null) UnityEngine.Object.DestroyImmediate(previous);
            Directory.CreateDirectory(DirectoryPath);
            AssetDatabase.Refresh();
            var materials = new[] { Material("Concrete", new Color(.36f, .36f, .32f)),
                Material("Ochre", new Color(.72f, .47f, .13f)), Material("Charcoal", new Color(.045f, .045f, .05f)) };
            var root = new GameObject("Downtown exit closures");
            var renderers = new List<Renderer>();
            int blocks = 0, groundedBlocks = 0;
            Physics.SyncTransforms();
            for (int index = 0; index < manifest.closures.Length; index++)
            {
                var closure = manifest.closures[index];
                var start = Point(closure.start); var end = Point(closure.end);
                var forward = Point(closure.outward).normalized;
                var group = new GameObject($"Exit {index + 1}: {closure.name}");
                group.transform.SetParent(root.transform, false);
                group.transform.position = (start + end) / 2;
                group.transform.rotation = Quaternion.LookRotation(forward, Vector3.up);
                float width = Vector3.Distance(start, end);
                int count = Mathf.CeilToInt(width / 2.5f);
                var vertices = new List<Vector3>(); var coordinates = new List<Vector2>();
                var triangles = new[] { new List<int>(), new List<int>(), new List<int>() };
                int grounded = 0;
                for (int block = 0; block < count; block++)
                {
                    float length = width / count;
                    float x = -width / 2 + (block + .5f) * length;
                    var position = group.transform.TransformPoint(new Vector3(x, 0, 0));
                    var hits = Physics.RaycastAll(position + Vector3.up * 5, Vector3.down, 10)
                        .Where(hit => hit.collider.transform.root.name == "RoadsPhysical" && hit.normal.y > .5f)
                        .OrderBy(hit => Mathf.Abs(hit.point.y - position.y)).ToArray();
                    float height = hits.Length == 0 ? 0 : hits[0].point.y - group.transform.position.y;
                    if (hits.Length > 0) grounded++;
                    var collider = group.AddComponent<BoxCollider>();
                    collider.center = new Vector3(x, height + .55f, 0);
                    collider.size = new Vector3(length + .02f, 1.5f, 1);
                    AddBlock(vertices, coordinates, triangles, x, height - .08f, length + .01f);
                }
                NfsWorldSetup.Require(grounded >= count / 2, "Exit wall is not on the retained road: " + closure.name);
                blocks += count; groundedBlocks += grounded;
                var mesh = new Mesh { name = "Exit wall " + index, vertices = vertices.ToArray(), uv = coordinates.ToArray(), subMeshCount = 3 };
                for (int material = 0; material < 3; material++) mesh.SetTriangles(triangles[material], material);
                mesh.RecalculateNormals(); mesh.RecalculateBounds();
                string path = DirectoryPath + "/Exit_" + index + ".asset";
                var existing = AssetDatabase.LoadAssetAtPath<Mesh>(path);
                if (existing == null) AssetDatabase.CreateAsset(mesh, path);
                else { EditorUtility.CopySerialized(mesh, existing); UnityEngine.Object.DestroyImmediate(mesh); mesh = existing; }
                group.AddComponent<MeshFilter>().sharedMesh = mesh;
                var renderer = group.AddComponent<MeshRenderer>(); renderer.sharedMaterials = materials;
                renderers.Add(renderer);
            }
            var culling = UnityEngine.Object.FindFirstObjectByType<NfsWorldDistanceCulling>();
            if (culling != null) culling.AddTargets(renderers.ToArray(), 500);
            AssetDatabase.SaveAssets();
            File.WriteAllText(Path.GetFullPath(Path.Combine(Application.dataPath, "../../artifacts/NfsWorld/exit-closures.json")),
                $"{{\"exits\":6,\"boxColliders\":{blocks},\"groundedBlocks\":{groundedBlocks},\"sourceCollisionChanged\":false}}\n");
            Debug.Log($"Six reviewed exit walls added: {blocks} collision blocks, {groundedBlocks} on source road surfaces.");
        }

        private static Material Material(string name, Color colour)
        {
            string path = DirectoryPath + "/" + name + ".mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null) { material = new Material(Shader.Find("Universal Render Pipeline/Lit")); AssetDatabase.CreateAsset(material, path); }
            material.SetColor("_BaseColor", colour); material.SetFloat("_Smoothness", .06f);
            material.SetFloat("_Cull", 0); EditorUtility.SetDirty(material);
            return material;
        }

        private static void AddBlock(List<Vector3> vertices, List<Vector2> uv, List<int>[] triangles, float x, float y, float length)
        {
            var profile = new[] { new Vector2(-.5f, 0), new Vector2(-.5f, .16f), new Vector2(-.19f, .72f),
                new Vector2(-.19f, 1.25f), new Vector2(.19f, 1.25f), new Vector2(.19f, .72f), new Vector2(.5f, .16f), new Vector2(.5f, 0) };
            Vector3 Vertex(float side, Vector2 point) => new Vector3(x + side * length / 2, y + point.y, point.x);
            void Face(int material, params Vector3[] points)
            {
                int offset = vertices.Count;
                foreach (var point in points) { vertices.Add(point); uv.Add(Vector2.zero); }
                for (int corner = 1; corner + 1 < points.Length; corner++)
                { triangles[material].Add(offset); triangles[material].Add(offset + corner); triangles[material].Add(offset + corner + 1); }
            }
            for (int edge = 0; edge < profile.Length; edge++)
            {
                int next = (edge + 1) % profile.Length;
                Face(0, Vertex(-1, profile[next]), Vertex(1, profile[next]), Vertex(1, profile[edge]), Vertex(-1, profile[edge]));
                Face(0, new Vector3(x - length / 2, y + .45f, 0), Vertex(-1, profile[next]), Vertex(-1, profile[edge]));
                Face(0, new Vector3(x + length / 2, y + .45f, 0), Vertex(1, profile[edge]), Vertex(1, profile[next]));
            }
            float half = length * .4f;
            Face(1, new Vector3(x - half, y + .77f, -.195f), new Vector3(x - half, y + 1.18f, -.195f),
                new Vector3(x + half, y + 1.18f, -.195f), new Vector3(x + half, y + .77f, -.195f));
            for (float stripe = -half + .1f; stripe + .35f < half; stripe += .55f)
                Face(2, new Vector3(x + stripe, y + .77f, -.198f), new Vector3(x + stripe + .22f, y + 1.18f, -.198f),
                    new Vector3(x + stripe + .35f, y + 1.18f, -.198f), new Vector3(x + stripe + .13f, y + .77f, -.198f));
        }
    }
}
