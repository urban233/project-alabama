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
        private readonly NfsWorldCameraMotion motion = new NfsWorldCameraMotion();
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
            => Refresh(cameraPosition, motion.Predict(cameraPosition));

        public void Refresh(Vector3 cameraPosition, Vector3 predictedPosition)
        {
            foreach (var entry in entries)
            {
                if (entry.target == null) continue;
                float distance = Mathf.Sqrt(Mathf.Min(entry.bounds.SqrDistance(cameraPosition),
                    entry.bounds.SqrDistance(predictedPosition)));
                float middleBand = Mathf.Clamp(middleDistance * .08f, 2, 8);
                float farBand = Mathf.Clamp(farDistance * .08f, 4, 16);
                var current = entry.target.sharedMesh;
                Mesh selected;
                if (current == entry.near)
                    selected = distance > farDistance + farBand ? entry.far :
                        distance > middleDistance + middleBand ? entry.middle : entry.near;
                else if (current == entry.middle)
                    selected = distance < middleDistance - middleBand ? entry.near :
                        distance > farDistance + farBand ? entry.far : entry.middle;
                else
                    selected = distance < middleDistance - middleBand ? entry.near :
                        distance < farDistance - farBand ? entry.middle : entry.far;
                if (entry.target.sharedMesh != selected) entry.target.sharedMesh = selected;
            }
        }

        private void LateUpdate()
        {
            if (Camera.main == null) return;
            Refresh(Camera.main.transform.position);
        }

        public void Restore()
        {
            motion.Reset();
            foreach (var entry in entries)
                if (entry.target != null) entry.target.sharedMesh = entry.near;
        }
        private void OnDisable() => Restore();
    }

    // Reused by visibility and LODs. Teleports and pause/review calls never imply high-speed travel.
    internal sealed class NfsWorldCameraMotion
    {
        private Vector3 previous, velocity;
        private float previousTime;
        private bool initialized;
        public Vector3 Predict(Vector3 position)
        {
            float now = Time.unscaledTime, elapsed = now - previousTime;
            if (!initialized || (position - previous).sqrMagnitude > 625 || elapsed > 1)
            { previous = position; previousTime = now; velocity = Vector3.zero; initialized = true; }
            else if (elapsed > .001f)
            {
                var delta = position - previous; delta.y = 0;
                velocity = Vector3.Lerp(velocity, Vector3.ClampMagnitude(delta / elapsed, 90), .5f);
                previous = position; previousTime = now;
            }
            return position + Vector3.ClampMagnitude(velocity * .35f, 30);
        }
        public void Reset() { initialized = false; velocity = Vector3.zero; }
    }
}
