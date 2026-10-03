using System;
using UnityEngine;

namespace Alabama.Driving
{
    /// <summary>Visual-only spatial LODs; collision and shadow ownership stay separate.</summary>
    public sealed class NfsWorldMeshLods : MonoBehaviour
    {
        [Serializable] public struct Entry
        {
            public MeshFilter target;
            public Mesh near, middle, far;
            public Bounds bounds;
        }
        [SerializeField] private Entry[] entries = Array.Empty<Entry>();
        [SerializeField] private float middleDistance = 80;
        [SerializeField] private float farDistance = 200;
        private float nextUpdate;
        public int TargetCount => entries.Length;
        public int StaticBatchTargetCount
        {
            get
            {
                int count = 0;
                foreach (var entry in entries)
                    if (entry.target != null && entry.target.TryGetComponent<Renderer>(out var renderer) &&
                        renderer.isPartOfStaticBatch) count++;
                return count;
            }
        }

        public int ReducedTargetCount
        {
            get
            {
                int count = 0;
                foreach (var entry in entries)
                    if (entry.target != null && entry.target.sharedMesh != entry.near) count++;
                return count;
            }
        }

        public bool TargetsBelongTo(UnityEngine.SceneManagement.Scene scene)
        {
            foreach (var entry in entries)
                if (entry.target == null || entry.target.gameObject.scene != scene ||
                    entry.near == null || entry.middle == null || entry.far == null) return false;
            return entries.Length > 0;
        }

        public void Configure(Entry[] values, float middleMetres = 80, float farMetres = 200)
        {
            if (values == null) throw new ArgumentNullException(nameof(values));
            if (!float.IsFinite(middleMetres) || !float.IsFinite(farMetres) ||
                middleMetres <= 0 || farMetres <= middleMetres)
                throw new ArgumentOutOfRangeException(nameof(farMetres));
            Restore();
            entries = values;
            middleDistance = middleMetres;
            farDistance = farMetres;
        }

        public void Refresh(Vector3 cameraPosition)
        {
            foreach (var entry in entries)
            {
                if (entry.target == null) continue;
                float distanceSquared = entry.bounds.SqrDistance(cameraPosition);
                var selected = distanceSquared >= farDistance * farDistance ? entry.far :
                    distanceSquared >= middleDistance * middleDistance ? entry.middle : entry.near;
                if (entry.target.sharedMesh != selected) entry.target.sharedMesh = selected;
            }
        }

        private void LateUpdate()
        {
            if (Time.unscaledTime < nextUpdate || Camera.main == null) return;
            nextUpdate = Time.unscaledTime + .2f;
            Refresh(Camera.main.transform.position);
        }

        public void Restore()
        {
            foreach (var entry in entries)
                if (entry.target != null) entry.target.sharedMesh = entry.near;
        }
        private void OnDisable() => Restore();
    }
}
