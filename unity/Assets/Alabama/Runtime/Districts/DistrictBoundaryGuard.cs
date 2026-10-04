using System;
using Alabama.Driving;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Alabama.Districts
{
    /// <summary>Keep the whole car over loaded collision, including unmarked map edges.</summary>
    [DefaultExecutionOrder(100), DisallowMultipleComponent]
    public sealed class DistrictBoundaryGuard : MonoBehaviour
    {
        private DistrictRuntime runtime;
        private ArcadeCarController car;
        private Vector3 safePosition;
        private Quaternion safeRotation;
        private bool hasSafePosition;
        private readonly RaycastHit[] hits = new RaycastHit[128];
        // Match the E46's 1.9 x 4.45 metre chassis collider, including overhangs.
        private static readonly Vector3[] Footprint = { new Vector3(-.95f, 0, -2.225f),
            new Vector3(-.95f, 0, 2.225f), new Vector3(.95f, 0, -2.225f), new Vector3(.95f, 0, 2.225f) };
        public int BlockedMoves { get; private set; }

        public void Configure(DistrictRuntime owner)
        { runtime = owner; car = owner.Player; hasSafePosition = false; }

        public void ResetHistory() => hasSafePosition = false;

        public bool ContainsFootprint(Vector3 position, Quaternion rotation, Func<Scene, bool> ownsScene,
            bool requireVisibleGround = false, float edgeInset = 0)
        {
            // Yaw-only chassis footprint keeps slopes and short crest jumps valid.
            var yaw = Quaternion.Euler(0, rotation.eulerAngles.y, 0);
            foreach (var offset in Footprint)
                {
                    var expanded = offset + new Vector3(Mathf.Sign(offset.x) * edgeInset, 0,
                        Mathf.Sign(offset.z) * edgeInset);
                    var point = position + yaw * expanded;
                    int count = Physics.RaycastNonAlloc(point + Vector3.up * 3, Vector3.down,
                        hits, 6, ~0, QueryTriggerInteraction.Ignore);
                    bool supported = false, visible = false, needsVisible = requireVisibleGround;
                    for (int i = 0; i < count; i++)
                        if (hits[i].collider.attachedRigidbody == null && hits[i].normal.y > .5f &&
                            hits[i].point.y <= point.y + .6f &&
                            ownsScene(hits[i].collider.gameObject.scene))
                        {
                            needsVisible |= runtime != null && runtime.RequiresVisibleGround(hits[i].collider.gameObject.scene);
                            if (hits[i].collider.GetComponentInParent<DistrictGroundCoverage>() != null) visible = true;
                            else supported = true;
                        }
                    if (!supported || (needsVisible && !visible)) return false;
                    if (needsVisible)
                    {
                        bool matched = false;
                        for (int i = 0; i < count && !matched; i++)
                        {
                            var physical = hits[i];
                            if (physical.collider.attachedRigidbody != null || physical.normal.y <= .5f ||
                                physical.point.y > point.y+.6f || !ownsScene(physical.collider.gameObject.scene) ||
                                physical.collider.GetComponentInParent<DistrictGroundCoverage>() != null) continue;
                            for (int j = 0; j < count; j++)
                                if (hits[j].normal.y > .5f && hits[j].point.y <= point.y+.6f &&
                                    Mathf.Abs(physical.point.y-hits[j].point.y) <= .35f &&
                                    ownsScene(hits[j].collider.gameObject.scene) &&
                                    hits[j].collider.GetComponentInParent<DistrictGroundCoverage>() != null)
                                { matched = true; break; }
                        }
                        if (!matched) return false;
                    }
                }
            return true;
        }

        private void FixedUpdate()
        {
            if (runtime == null || !runtime.Ready || runtime.Busy || car.Body.isKinematic) return;
            bool Owned(Scene scene) => runtime.OwnsCollision(scene);
            var body = car.Body;
            // Leave room for forces and contact corrections applied during the next simulation step.
            // Querying the exact body edge alone lets wheel/kerb impulses cross an edge between checks.
            const float inset = .15f;
            bool current = ContainsFootprint(body.position, body.rotation, Owned, edgeInset: inset);
            if (!hasSafePosition)
            {
                if (!current) { runtime.Recover(); return; }
                safePosition = body.position; safeRotation = body.rotation; hasSafePosition = true;
            }
            // Sweep the upcoming step, rather than waiting until the car falls.
            // Sub-metre probes also catch short gaps at speed and sideways exits.
            var movement = body.linearVelocity * Time.fixedDeltaTime;
            float turn = body.angularVelocity.y * Time.fixedDeltaTime * Mathf.Rad2Deg;
            int steps = Mathf.Max(1, Mathf.CeilToInt((movement.magnitude + Mathf.Abs(turn) * Mathf.Deg2Rad * 2.5f) / .5f));
            bool next = current;
            for (int i = 1; next && i <= steps; i++)
            {
                float fraction = i / (float)steps;
                var rotation = Quaternion.Euler(0, turn * fraction, 0) * body.rotation;
                next = ContainsFootprint(body.position + movement * fraction, rotation, Owned, edgeInset: inset);
            }
            if (next)
            { safePosition = body.position; safeRotation = body.rotation; return; }
            body.position = safePosition; body.rotation = safeRotation;
            body.linearVelocity = Vector3.zero; body.angularVelocity = Vector3.zero;
            Physics.SyncTransforms();
            BlockedMoves++;
        }
    }
}
