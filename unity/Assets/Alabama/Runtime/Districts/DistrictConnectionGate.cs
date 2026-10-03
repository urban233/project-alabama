using System.Linq;
using UnityEngine;

namespace Alabama.Districts
{
    /// <summary>A verified connection opens only while its reciprocal real content is registered.</summary>
    [RequireComponent(typeof(DistrictContent))]
    public sealed class DistrictConnectionGate : MonoBehaviour
    {
        private DistrictContent content;
        private DistrictRuntime runtime;

        private void OnEnable()
        {
            if (!Application.isPlaying) return;
            content = GetComponent<DistrictContent>();
            runtime = DistrictRuntime.Instance;
            if (runtime == null) return;
            runtime.DistrictRegistered += Changed;
            runtime.DistrictUnregistered += Changed;
            Refresh();
        }

        private void Changed(DistrictContent district) => Refresh();

        public void Refresh()
        {
            if (content == null || runtime == null) return;
            foreach (var connection in content.Connections)
            {
                if (!connection.seamVerified || connection.closure == null) continue;
                var neighbour = runtime.LoadedDistricts.FirstOrDefault(d => d.Id == connection.neighbourId);
                bool open = neighbour != null && neighbour.Connections.Any(other =>
                    other.seamVerified && other.id == connection.reciprocalId && other.reciprocalId == connection.id &&
                    other.neighbourId == content.Id && other.collisionOwner == connection.collisionOwner &&
                    Vector3.Distance(other.seamBounds.center, connection.seamBounds.center) < .001f &&
                    Vector3.Distance(other.seamBounds.size, connection.seamBounds.size) < .001f &&
                    Vector3.Dot(other.outward.normalized, connection.outward.normalized) < -.99f);
                connection.closure.SetActive(!open);
            }
        }

        private void OnDisable()
        {
            if (runtime != null)
            { runtime.DistrictRegistered -= Changed; runtime.DistrictUnregistered -= Changed; }
            if (content != null)
                foreach (var connection in content.Connections)
                    if (connection.seamVerified && connection.closure != null) connection.closure.SetActive(true);
            runtime = null;
        }
    }
}
