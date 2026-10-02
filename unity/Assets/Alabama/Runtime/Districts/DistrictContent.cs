using System;
using System.Linq;
using Alabama.Bootstrap;
using Alabama.Driving;
using UnityEngine;
using UnityEngine.Rendering;

namespace Alabama.Districts
{
    [Serializable]
    public struct DistrictRecoveryPose
    {
        public string id;
        public Vector3 position;
        public Vector3 eulerAngles;
        public Quaternion Rotation => Quaternion.Euler(eulerAngles);
    }

    [Serializable]
    public struct DistrictConnection
    {
        public string id;
        public string neighbourId;
        public string reciprocalId;
        public Bounds seamBounds;
        public Vector3 outward;
        public string collisionOwner;
        public GameObject closure;
        public bool seamVerified;
    }

    /// <summary>Scene-owned metadata. All positions use the shared world frame.</summary>
    [DisallowMultipleComponent]
    public sealed class DistrictContent : MonoBehaviour
    {
        [SerializeField] private string districtId;
        [SerializeField] private Bounds bounds;
        [SerializeField] private float minimumRecoveryHeight = -80;
        [SerializeField] private DistrictRecoveryPose[] recoveryPoses = Array.Empty<DistrictRecoveryPose>();
        [SerializeField] private DistrictConnection[] connections = Array.Empty<DistrictConnection>();
        public string Id => districtId;
        public Bounds Bounds => bounds;
        public float MinimumRecoveryHeight => minimumRecoveryHeight;
        public DistrictRecoveryPose[] RecoveryPoses => (DistrictRecoveryPose[])recoveryPoses.Clone();
        public DistrictConnection[] Connections => (DistrictConnection[])connections.Clone();
        public NfsWorldDistanceCulling[] Culling => gameObject.scene.GetRootGameObjects()
            .SelectMany(root => root.GetComponentsInChildren<NfsWorldDistanceCulling>(true)).ToArray();

        public void Configure(string id, Bounds worldBounds, DistrictRecoveryPose[] poses,
            DistrictConnection[] exits = null, float minimumHeight = -80)
        {
            districtId = id; bounds = worldBounds; recoveryPoses = poses;
            connections = exits ?? Array.Empty<DistrictConnection>(); minimumRecoveryHeight = minimumHeight;
        }

        public string ValidateContract()
        {
            if (string.IsNullOrWhiteSpace(districtId) || !Finite(bounds.center) || !Finite(bounds.size) ||
                bounds.size.x <= 0 || bounds.size.y <= 0 || bounds.size.z <= 0 ||
                !float.IsFinite(minimumRecoveryHeight))
                return "District identity, bounds or recovery threshold is invalid.";
            if (transform.position != Vector3.zero || transform.rotation != Quaternion.identity || transform.lossyScale != Vector3.one)
                return "District metadata must use the shared frame with an identity transform.";
            if (recoveryPoses == null || recoveryPoses.Length == 0 ||
                recoveryPoses.Select(p => p.id).Distinct().Count() != recoveryPoses.Length ||
                recoveryPoses.Any(p => string.IsNullOrWhiteSpace(p.id) || !Finite(p.position) ||
                    !Finite(p.eulerAngles) || !bounds.Contains(p.position) || p.position.y <= minimumRecoveryHeight)) return "District recovery data is invalid.";
            var descriptors = gameObject.scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<DistrictContent>(true));
            if (descriptors.Count() != 1 || !isActiveAndEnabled) return "Exactly one enabled district descriptor is required per content scene.";
            foreach (var root in gameObject.scene.GetRootGameObjects())
                if (root.GetComponentsInChildren<ArcadeCarController>(true).Length != 0 ||
                    root.GetComponentsInChildren<VehicleInput>(true).Length != 0 ||
                    root.GetComponentsInChildren<Camera>(true).Length != 0 ||
                    root.GetComponentsInChildren<DemoBootstrap>(true).Length != 0 ||
                    root.GetComponentsInChildren<NfsWorldRenderSettings>(true).Length != 0 ||
                    root.GetComponentsInChildren<DistrictRuntime>(true).Length != 0 ||
                    root.GetComponentsInChildren<Volume>(true).Any(v => v.isGlobal) ||
                    root.GetComponentsInChildren<Light>(true).Any(l => l.type == LightType.Directional))
                    return "Content contains a persistent gameplay or shared rendering owner: " + root.name;
            if (Culling.Any(c => !c.TargetsBelongTo(gameObject.scene))) return "Culling targets must belong to their district scene.";
            if (connections == null || connections.Select(c => c.id).Distinct().Count() != connections.Length)
                return "Connection IDs must be unique.";
            foreach (var connection in connections)
                if (string.IsNullOrWhiteSpace(connection.id) || string.IsNullOrWhiteSpace(connection.neighbourId) ||
                    string.IsNullOrWhiteSpace(connection.collisionOwner) || !Finite(connection.seamBounds.center) ||
                    !Finite(connection.seamBounds.size) || connection.seamBounds.size.x <= 0 ||
                    connection.seamBounds.size.y <= 0 || connection.seamBounds.size.z <= 0 ||
                    !Finite(connection.outward) || connection.outward.sqrMagnitude < .9f ||
                    (!connection.seamVerified && (connection.closure == null || !connection.closure.activeSelf)) ||
                    (connection.closure != null && connection.closure.scene != gameObject.scene))
                    return "Connection ownership/bounds/closure is invalid: " + connection.id;
            return null;
        }

        // Check four tyre footprint points on THIS scene's enabled collision. A
        // neighbouring scene/upper bridge must never validate an absent spawn road.
        public bool Supports(DistrictRecoveryPose pose)
        {
            Physics.SyncTransforms();
            foreach (float x in new[] { -.8f, .8f })
                foreach (float z in new[] { -1.4f, 1.4f })
                {
                    var point = pose.position + pose.Rotation * new Vector3(x, 0, z);
                    if (!Physics.RaycastAll(point + Vector3.up, Vector3.down, 2, ~0, QueryTriggerInteraction.Ignore)
                        .Any(h => h.collider.gameObject.scene == gameObject.scene && h.normal.y > .5f &&
                            Mathf.Abs(h.point.y - (pose.position.y - .24f)) < .35f)) return false;
                }
            // Also reject poses occupied by walls/props/overhead geometry.
            return !Physics.OverlapBox(pose.position + Vector3.up * .65f, new Vector3(.9f, .45f, 2.1f),
                pose.Rotation, ~0, QueryTriggerInteraction.Ignore).Any(c => c.gameObject.scene == gameObject.scene);
        }

        private static bool Finite(Vector3 v) => float.IsFinite(v.x) && float.IsFinite(v.y) && float.IsFinite(v.z);
        private void OnDisable() { if (DistrictRuntime.Instance != null) DistrictRuntime.Instance.Unregister(this); }
        private void OnEnable()
        {
            if (Application.isPlaying && gameObject.scene.isLoaded && DistrictRuntime.Instance != null)
                DistrictRuntime.Instance.Register(this);
        }
    }
}
