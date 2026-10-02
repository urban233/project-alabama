using System.Collections.Generic;
using System.Linq;
using Alabama.Driving;
using Alabama.Editor;
using NUnit.Framework;
using UnityEngine;

namespace Alabama.Tests
{
    public sealed class NfsWorldShadowPartitionTests
    {
        [Test]
        public void ShadowCellsPreserveTriangleCornersNormalsAndTiledCutoutUvs()
        {
            var points = new[] { new Vector3(-2, 0, -2), new Vector3(0, 0, -2), new Vector3(-2, 0, 0),
                new Vector3(40, 1, 40), new Vector3(42, 1, 40), new Vector3(40, 1, 42) };
            var coordinates = Enumerable.Range(0, 6).Select(i => new Vector2(i - 2, i * 3)).ToArray();
            var layers = Enumerable.Range(0, 6).Select(i => new Vector2(i < 3 ? 3 : 7, i < 3 ? .4f : .7f)).ToList();
            var source = new Mesh { vertices = points, normals = Enumerable.Repeat(Vector3.up, 6).ToArray(),
                uv = coordinates, triangles = new[] { 0, 2, 1, 3, 5, 4 } };
            source.SetUVs(2, layers);
            var chunks = NfsWorldVisualOptimization.Partition(source, 32).ToArray();
            try
            {
                Assert.That(chunks.Select(c => c.cell), Is.EquivalentTo(new[] { new Vector2Int(-1, -1), new Vector2Int(1, 1) }));
                var orderedTriangles = new List<string>();
                foreach (var chunk in chunks)
                {
                    var uvLayers = new List<Vector2>(); chunk.mesh.GetUVs(2, uvLayers);
                    var originalIndices = new List<int>();
                    foreach (int corner in chunk.mesh.triangles)
                    {
                        int original = System.Array.IndexOf(points, chunk.mesh.vertices[corner]);
                        Assert.That(original, Is.GreaterThanOrEqualTo(0)); originalIndices.Add(original);
                        Assert.That(chunk.mesh.normals[corner], Is.EqualTo(Vector3.up));
                        Assert.That(chunk.mesh.uv[corner], Is.EqualTo(coordinates[original]));
                        Assert.That(uvLayers[corner], Is.EqualTo(layers[original]));
                    }
                    orderedTriangles.Add(string.Join(",", originalIndices));
                }
                Assert.That(orderedTriangles, Is.EquivalentTo(new[] { "0,2,1", "3,5,4" }));
                Assert.That(source.vertices, Is.EqualTo(points));
            }
            finally { foreach (var chunk in chunks) Object.DestroyImmediate(chunk.mesh); Object.DestroyImmediate(source); }
        }

        [Test]
        public void ReplacementTargetsKeepTheirOwnLimitsAndCullFromNearestBounds()
        {
            var previousMainCameras = Object.FindObjectsByType<Camera>(FindObjectsSortMode.None).Where(c => c.CompareTag("MainCamera")).ToArray();
            foreach (var previous in previousMainCameras) previous.tag = "Untagged";
            var cameraObject = new GameObject("Culling fixture camera"); cameraObject.tag = "MainCamera";
            cameraObject.AddComponent<Camera>();
            var root = new GameObject("Culling fixture"); var culling = root.AddComponent<NfsWorldDistanceCulling>();
            var old = GameObject.CreatePrimitive(PrimitiveType.Cube);
            var replacement = GameObject.CreatePrimitive(PrimitiveType.Cube);
            replacement.transform.position = Vector3.right * 100; replacement.transform.localScale = Vector3.one * 100;
            var renderer = replacement.GetComponent<Renderer>();
            try
            {
                culling.Configure(new[] { old.GetComponent<Renderer>() }, new[] { 1f });
                Object.DestroyImmediate(old);
                culling.AddTargets(new[] { renderer }, 55);
                Assert.That(culling.DistanceFor(renderer), Is.EqualTo(55));
                culling.Refresh(); Assert.That(renderer.forceRenderingOff, Is.False);
                cameraObject.transform.position = Vector3.left * 10;
                culling.Refresh(); Assert.That(renderer.forceRenderingOff, Is.True);
                culling.enabled = false; culling.ShowAll(); Assert.That(renderer.forceRenderingOff, Is.False);
            }
            finally
            {
                Object.DestroyImmediate(root); Object.DestroyImmediate(replacement); Object.DestroyImmediate(cameraObject); if (old != null) Object.DestroyImmediate(old);
                foreach (var previous in previousMainCameras) if (previous != null) previous.tag = "MainCamera";
            }
        }
    }
}
