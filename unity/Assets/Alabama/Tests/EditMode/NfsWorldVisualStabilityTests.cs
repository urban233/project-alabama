using System.Collections.Generic;
using Alabama.Editor;
using NUnit.Framework;
using UnityEngine;

namespace Alabama.Tests
{
    public sealed class NfsWorldVisualStabilityTests
    {
        [Test]
        public void CoincidentWindowKeepsAuthoredPaneAndPreservesOtherSurfaceData()
        {
            var mesh = new Mesh
            {
                vertices = new[] { Vector3.zero, Vector3.right, Vector3.up, Vector3.up, Vector3.zero, Vector3.right },
                uv = new[] { Vector2.zero, Vector2.right, Vector2.up, Vector2.one, Vector2.zero, Vector2.right },
                triangles = new[] { 0, 1, 2, 3, 4, 5 }
            };
            mesh.SetUVs(2, new List<Vector2> { Vector2.zero, Vector2.zero, Vector2.zero, Vector2.right, Vector2.right, Vector2.right });
            mesh.RecalculateNormals(); mesh.RecalculateBounds();
            var vertices = mesh.vertices; var uv = mesh.uv; var normals = mesh.normals; var bounds = mesh.bounds;
            try
            {
                Assert.That(NfsWorldVisualStability.ResolveWindowOverlaps(mesh, new HashSet<int> { 1 }), Is.EqualTo(1));
                Assert.That(mesh.triangles, Is.EqualTo(new[] { 3, 4, 5 }));
                Assert.That(mesh.vertices, Is.EqualTo(vertices)); Assert.That(mesh.uv, Is.EqualTo(uv));
                Assert.That(mesh.normals, Is.EqualTo(normals)); Assert.That(mesh.bounds, Is.EqualTo(bounds));
                Assert.That(NfsWorldVisualStability.ResolveWindowOverlaps(mesh, new HashSet<int> { 1 }), Is.Zero);
            }
            finally { Object.DestroyImmediate(mesh); }
        }

        [Test]
        public void ReviewedLightCardsAreRemovedWithoutAlteringTheSign()
        {
            var mesh = new Mesh { vertices = new[] { Vector3.zero, Vector3.right, Vector3.up,
                Vector3.forward, Vector3.forward + Vector3.right, Vector3.forward + Vector3.up },
                triangles = new[] { 0, 1, 2, 3, 4, 5 } };
            mesh.SetUVs(2, new List<Vector2> { Vector2.zero, Vector2.zero, Vector2.zero,
                Vector2.right, Vector2.right, Vector2.right });
            var vertices = mesh.vertices;
            try
            {
                Assert.That(NfsWorldVisualStability.RemoveLayers(mesh, new HashSet<int> { 1 }), Is.EqualTo(1));
                Assert.That(mesh.triangles, Is.EqualTo(new[] { 0, 1, 2 }));
                Assert.That(mesh.vertices, Is.EqualTo(vertices));
                Assert.That(NfsWorldVisualStability.RemoveLayers(mesh, new HashSet<int> { 1 }), Is.Zero);
            }
            finally { Object.DestroyImmediate(mesh); }
        }

        [Test]
        public void SeparatePaneAndNonWindowDuplicatesAreNotRemoved()
        {
            var mesh = new Mesh { vertices = new[] { Vector3.zero, Vector3.right, Vector3.up,
                new Vector3(0, 0, .0001f), new Vector3(1, 0, .0001f), new Vector3(0, 1, .0001f) },
                triangles = new[] { 0, 1, 2, 0, 1, 2, 3, 4, 5 } };
            mesh.SetUVs(2, new List<Vector2> { Vector2.zero, Vector2.zero, Vector2.zero, Vector2.right, Vector2.right, Vector2.right });
            try
            {
                Assert.That(NfsWorldVisualStability.ResolveWindowOverlaps(mesh, new HashSet<int> { 1 }), Is.Zero);
                Assert.That(mesh.triangles.Length, Is.EqualTo(9));
            }
            finally { Object.DestroyImmediate(mesh); }
        }
    }
}
