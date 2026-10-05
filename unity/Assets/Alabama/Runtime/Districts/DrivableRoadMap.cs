using System;
using UnityEngine;
using UnityEngine.AI;

namespace Alabama.Districts
{
    /// <summary>Scene-owned, road-only navigation data. This never drives the vehicle.</summary>
    public sealed class DrivableRoadMap : MonoBehaviour
    {
        [Serializable] public struct Direction
        { public Vector3 start, end; }
        [SerializeField] private NavMeshData data;
        [SerializeField] private int roadArea = 3;
        [SerializeField] private int agentType;
        [SerializeField] private Direction[] directions = Array.Empty<Direction>();
        private NavMeshDataInstance instance;
        public bool Available => instance.valid && data != null;

        public void Configure(NavMeshData value, int area, Direction[] roads, int agent = 0)
        { Remove(); data = value; roadArea = area; directions = roads; agentType = agent; Install(); }
        private void OnEnable() => Install();
        private void OnDisable() => Remove();
        private void Install()
        { if (Application.isPlaying && isActiveAndEnabled && data != null && !instance.valid) instance = NavMesh.AddNavMeshData(data); }
        private void Remove() { if (instance.valid) instance.Remove(); instance = default; }

        public bool Sample(Vector3 point, float distance, out NavMeshHit hit)
        {
            hit = default;
            return Available && NavMesh.SamplePosition(point,out hit,distance,
                new NavMeshQueryFilter { agentTypeID = agentType, areaMask = 1 << roadArea });
        }

        public Vector3 Heading(Vector3 position, Vector3 preferred)
        {
            float best = float.PositiveInfinity; var forward = Vector3.forward;
            foreach (var road in directions)
            {
                var segment = road.end-road.start;
                if (segment.sqrMagnitude < .01f) continue;
                var closest = road.start+segment*Mathf.Clamp01(Vector3.Dot(position-road.start,segment)/segment.sqrMagnitude);
                float distance = (position-closest).sqrMagnitude;
                if (distance >= best) continue;
                best = distance; forward = segment.normalized;
            }
            return Vector3.Dot(forward,preferred) < 0 ? -forward : forward;
        }
    }
}
