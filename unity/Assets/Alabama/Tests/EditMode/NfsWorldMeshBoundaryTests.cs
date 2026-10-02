using System.Collections.Generic;
using System.Reflection;
using Alabama.Editor;
using NUnit.Framework;
using UnityEngine;

namespace Alabama.Tests
{
    public sealed class NfsWorldMeshBoundaryTests
    {
        private readonly List<Mesh> meshes = new List<Mesh>();

        [TearDown]
        public void Cleanup()
        {
            foreach (var mesh in meshes) Object.DestroyImmediate(mesh);
            meshes.Clear();
        }

        [Test]
        public void NewlyOpenedFacadeHoleIsRejectedAtArtTolerance()
        {
            var vertices = new List<Vector3>();
            for (int y = 0; y < 4; y++)
                for (int x = 0; x < 4; x++) vertices.Add(new Vector3(x, y, 0));
            var source = new List<int>();
            var damaged = new List<int>();
            for (int y = 0; y < 3; y++)
                for (int x = 0; x < 3; x++)
                {
                    int a = y * 4 + x;
                    var indices = new[] { a, a + 1, a + 5, a, a + 5, a + 4 };
                    source.AddRange(indices);
                    if (x != 1 || y != 1) damaged.AddRange(indices);
                }
            Assert.That(Preserves(NewMesh(vertices.ToArray(), source.ToArray()),
                NewMesh(vertices.ToArray(), damaged.ToArray()), .02f), Is.False);
        }

        [Test]
        public void SmallIntentionalFacetsRequireTheSeparateArtTolerance()
        {
            Mesh Disc(int sides)
            {
                var vertices = new Vector3[sides + 1];
                var triangles = new int[sides * 3];
                for (int i = 0; i < sides; i++)
                {
                    float angle = i * Mathf.PI * 2 / sides;
                    vertices[i + 1] = new Vector3(Mathf.Cos(angle), Mathf.Sin(angle), 0) * .1f;
                    triangles[i * 3] = 0;
                    triangles[i * 3 + 1] = i + 1;
                    triangles[i * 3 + 2] = (i + 1) % sides + 1;
                }
                return NewMesh(vertices, triangles);
            }
            var original = Disc(32);
            var faceted = Disc(8);
            Assert.That(Preserves(original, faceted, .003f), Is.False);
            Assert.That(Preserves(original, faceted, .02f), Is.True);
        }

        [Test]
        public void ImporterSplitsAtUvSeamsDoNotCreateGeometricHoles()
        {
            var original = NewMesh(new[] { Vector3.zero, Vector3.right, Vector3.right + Vector3.up, Vector3.up },
                new[] { 0, 1, 2, 0, 2, 3 });
            var split = NewMesh(new[] { Vector3.zero, Vector3.right, Vector3.right + Vector3.up,
                Vector3.zero, Vector3.right + Vector3.up, Vector3.up }, new[] { 0, 1, 2, 3, 4, 5 });
            Assert.That(Preserves(original, split, .003f), Is.True);
        }

        private Mesh NewMesh(Vector3[] vertices, int[] triangles)
        {
            var mesh = new Mesh { vertices = vertices, triangles = triangles };
            meshes.Add(mesh);
            return mesh;
        }

        private static bool Preserves(Mesh source, Mesh candidate, float tolerance) =>
            (bool)typeof(NfsWorldVisualOptimization).GetMethod("PreservesBoundaryCurves",
                BindingFlags.NonPublic | BindingFlags.Static).Invoke(null, new object[] { source, candidate, tolerance });
    }
}
