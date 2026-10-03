using Alabama.Driving;
using NUnit.Framework;
using UnityEngine;

namespace Alabama.Tests
{
    public sealed class NfsWorldMeshLodTests
    {
        [Test]
        public void DistanceUsesSurfaceBoundsAndVisualChangesLeaveCollisionAlone()
        {
            var owner = new GameObject("LOD test");
            var near = new Mesh { vertices = new[] { Vector3.zero, Vector3.right, Vector3.up },
                triangles = new[] { 0, 1, 2 } };
            var middle = new Mesh(); var far = new Mesh();
            try
            {
                var filter = owner.AddComponent<MeshFilter>(); filter.sharedMesh = near;
                var collision = owner.AddComponent<MeshCollider>(); collision.sharedMesh = near;
                var lod = owner.AddComponent<NfsWorldMeshLods>();
                lod.Configure(new[] { new NfsWorldMeshLods.Entry { target = filter, near = near,
                    middle = middle, far = far, bounds = new Bounds(Vector3.zero, Vector3.one * 100) } });
                lod.Refresh(new Vector3(100, 0, 0)); Assert.That(filter.sharedMesh, Is.SameAs(near));
                lod.Refresh(new Vector3(130, 0, 0)); Assert.That(filter.sharedMesh, Is.SameAs(middle));
                lod.Refresh(new Vector3(250, 0, 0)); Assert.That(filter.sharedMesh, Is.SameAs(far));
                Assert.That(collision.sharedMesh, Is.SameAs(near));
                Assert.That(lod.TargetsBelongTo(owner.scene), Is.True);
                lod.enabled = false; Assert.That(filter.sharedMesh, Is.SameAs(near));
            }
            finally
            {
                Object.DestroyImmediate(owner); Object.DestroyImmediate(near);
                Object.DestroyImmediate(middle); Object.DestroyImmediate(far);
            }
        }

        [Test]
        public void CullingLookupTracksReconfigurationAndAddedTargets()
        {
            var owner = new GameObject("Culling test"); var other = new GameObject("Other target");
            try
            {
                var first = owner.AddComponent<MeshRenderer>(); var second = other.AddComponent<MeshRenderer>();
                var culling = owner.AddComponent<NfsWorldDistanceCulling>();
                culling.Configure(new Renderer[] { first }, new[] { 100f });
                Assert.That(culling.DistanceFor(first), Is.EqualTo(100));
                culling.AddTargets(new Renderer[] { first, second }, 250);
                Assert.That(culling.DistanceFor(first), Is.EqualTo(250));
                Assert.That(culling.DistanceFor(second), Is.EqualTo(250));
                culling.Configure(new Renderer[] { second }, new[] { 50f });
                Assert.That(culling.DistanceFor(first), Is.EqualTo(90000));
                Assert.That(culling.DistanceFor(second), Is.EqualTo(50));
            }
            finally { Object.DestroyImmediate(owner); Object.DestroyImmediate(other); }
        }
    }
}
