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
                lod.Refresh(new Vector3(140, 0, 0)); Assert.That(filter.sharedMesh, Is.SameAs(middle));
                lod.Refresh(new Vector3(270, 0, 0)); Assert.That(filter.sharedMesh, Is.SameAs(far));
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
        public void DetailDoesNotToggleAtThresholdAndApproachingGeometryIsPreparedEarly()
        {
            var owner = new GameObject("LOD transition fixture");
            var near = new Mesh(); var middle = new Mesh(); var far = new Mesh();
            try
            {
                var filter = owner.AddComponent<MeshFilter>(); filter.sharedMesh = near;
                var lod = owner.AddComponent<NfsWorldMeshLods>();
                lod.Configure(new[] { new NfsWorldMeshLods.Entry { target = filter, near = near,
                    middle = middle, far = far, bounds = new Bounds(Vector3.zero, Vector3.zero) } });
                foreach (float distance in new[] { 79f, 82, 78, 84, 80 })
                { lod.Refresh(Vector3.right * distance, Vector3.right * distance); Assert.That(filter.sharedMesh, Is.SameAs(near)); }
                lod.Refresh(Vector3.right * 95, Vector3.right * 95); Assert.That(filter.sharedMesh, Is.SameAs(middle));
                foreach (float distance in new[] { 79f, 82, 78, 84, 80 })
                { lod.Refresh(Vector3.right * distance, Vector3.right * distance); Assert.That(filter.sharedMesh, Is.SameAs(middle)); }
                lod.Refresh(Vector3.right * 250, Vector3.right * 250); Assert.That(filter.sharedMesh, Is.SameAs(far));
                lod.Refresh(Vector3.right * 210, Vector3.right * 180); Assert.That(filter.sharedMesh, Is.SameAs(middle));
                lod.Refresh(Vector3.right * 95, Vector3.right * 65); Assert.That(filter.sharedMesh, Is.SameAs(near));
            }
            finally { Object.DestroyImmediate(owner); Object.DestroyImmediate(near); Object.DestroyImmediate(middle); Object.DestroyImmediate(far); }
        }

        [Test]
        public void VisibilityPreparesApproachingObjectsAndDoesNotBlinkAtItsLimit()
        {
            var owner = GameObject.CreatePrimitive(PrimitiveType.Cube);
            try
            {
                var renderer = owner.GetComponent<Renderer>(); var culling = owner.AddComponent<NfsWorldDistanceCulling>();
                culling.Configure(new[] { renderer }, new[] { 100f });
                culling.Refresh(Vector3.right * 120, Vector3.right * 120); Assert.That(renderer.forceRenderingOff, Is.True);
                culling.Refresh(Vector3.right * 120, Vector3.right * 95); Assert.That(renderer.forceRenderingOff, Is.False);
                foreach (float distance in new[] { 100f, 102, 99, 104, 101 })
                { culling.Refresh(Vector3.right * distance, Vector3.right * distance); Assert.That(renderer.forceRenderingOff, Is.False); }
                culling.Refresh(Vector3.right * 115, Vector3.right * 115); Assert.That(renderer.forceRenderingOff, Is.True);
                foreach (float distance in new[] { 104f, 105, 104.5f })
                { culling.Refresh(Vector3.right * distance, Vector3.right * distance); Assert.That(renderer.forceRenderingOff, Is.True); }
            }
            finally { Object.DestroyImmediate(owner); }
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
