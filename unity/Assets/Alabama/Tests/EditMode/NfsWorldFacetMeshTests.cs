using Alabama.Editor;
using NUnit.Framework;
using UnityEngine;

namespace Alabama.Tests
{
    public sealed class NfsWorldFacetMeshTests
    {
        [Test]
        public void CornerFacesKeepExactGeometryAndUvsWithSeparateNormals()
        {
            var points = new[] { Vector3.zero, Vector3.right, Vector3.up, Vector3.forward };
            var coordinates = new[] { Vector2.zero, new Vector2(2, 0), new Vector2(0, 3), new Vector2(4, 5) };
            var source = new Mesh { vertices = points, uv = coordinates, triangles = new[] { 0, 1, 2, 0, 3, 1 } };
            source.RecalculateNormals();
            var previousNormals = source.normals;
            var faceted = NfsWorldFacetMeshes.Create(source);
            try
            {
                var indices = source.triangles;
                for (int index = 0; index < indices.Length; index++)
                {
                    int corner = faceted.triangles[index];
                    Assert.That(faceted.vertices[corner], Is.EqualTo(points[indices[index]]));
                    Assert.That(faceted.uv[corner], Is.EqualTo(coordinates[indices[index]]));
                }
                Assert.That(faceted.normals[0], Is.EqualTo(Vector3.forward));
                Assert.That(faceted.normals[faceted.triangles[3]], Is.EqualTo(Vector3.up));
                Assert.That(source.vertices, Is.EqualTo(points));
                Assert.That(source.uv, Is.EqualTo(coordinates));
                Assert.That(source.normals, Is.EqualTo(previousNormals));
                Assert.That(faceted.bounds, Is.EqualTo(source.bounds));
            }
            finally { Object.DestroyImmediate(faceted); Object.DestroyImmediate(source); }
        }

        [Test]
        public void CoplanarFacesShareVerticesWithoutGrowingTheMesh()
        {
            var source = new Mesh { vertices = new[] { Vector3.zero, Vector3.right, Vector3.up, Vector3.right + Vector3.up },
                uv = new[] { Vector2.zero, Vector2.right, Vector2.up, Vector2.one }, triangles = new[] { 0, 1, 2, 2, 1, 3 } };
            var faceted = NfsWorldFacetMeshes.Create(source);
            try { Assert.That(faceted.vertexCount, Is.EqualTo(4)); Assert.That(faceted.triangles.Length, Is.EqualTo(6)); }
            finally { Object.DestroyImmediate(faceted); Object.DestroyImmediate(source); }
        }
    }
}
