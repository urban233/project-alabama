using System;
using System.Collections.Generic;
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
        private readonly Collider[] overlaps = new Collider[128];
        private readonly List<(Vector3 position, Quaternion rotation)> roadHistory = new List<(Vector3, Quaternion)>();
        private WheelCollider[] wheels;
        private float nextRoadPose, wedgedSeconds;
        private float lastObstacleContact = float.NegativeInfinity;
        private float lastBoundaryBlock = float.NegativeInfinity;
        // Match the E46's 1.9 x 4.45 metre chassis collider, including overhangs.
        private static readonly Vector3[] Footprint = { new Vector3(-.95f, 0, -2.225f),
            new Vector3(-.95f, 0, 2.225f), new Vector3(.95f, 0, -2.225f), new Vector3(.95f, 0, 2.225f) };
        public int BlockedMoves { get; private set; }
        public int LocalRecoveries { get; private set; }
        public int RecoveryPoseCount => roadHistory.Count;

        public void Configure(DistrictRuntime owner)
        { runtime = owner; car = owner.Player; wheels = car.GetComponentsInChildren<WheelCollider>(); ResetHistory(); }

        public void ResetHistory()
        { hasSafePosition = false; roadHistory.Clear(); nextRoadPose = 0; wedgedSeconds = 0;
            lastObstacleContact = lastBoundaryBlock = float.NegativeInfinity; }

        private void OnCollisionStay(Collision collision)
        {
            if (runtime == null || !runtime.OwnsCollision(collision.gameObject.scene)) return;
            for (int i = 0; i < collision.contactCount; i++)
                if (Mathf.Abs(collision.GetContact(i).normal.y) < .75f)
                { lastObstacleContact = Time.fixedTime; break; }
        }

        private bool ClearBody(Vector3 position, Quaternion rotation, float clearance = -.04f)
        {
            int count = Physics.OverlapBoxNonAlloc(position + rotation * new Vector3(0, .65f, 0),
                new Vector3(.95f + clearance, .425f + Mathf.Min(clearance,.05f), 2.225f + clearance),
                overlaps, rotation, ~0, QueryTriggerInteraction.Ignore);
            if (count == overlaps.Length) return false;
            for (int i = 0; i < count; i++)
                if (overlaps[i].attachedRigidbody == null && runtime.OwnsCollision(overlaps[i].gameObject.scene) &&
                    overlaps[i].GetComponentInParent<DistrictGroundCoverage>() == null) return false;
            return true;
        }

        public bool TryRestoreNearby(float minimumDistance = 0)
        {
            if (runtime == null || car == null) return false;
            for (int i = roadHistory.Count - 1; i >= 0; i--)
            {
                var pose = roadHistory[i]; var delta = pose.position - car.Body.position; delta.y = 0;
                if (delta.sqrMagnitude > 625 || delta.sqrMagnitude < minimumDistance*minimumDistance ||
                    !ContainsFootprint(pose.position, pose.rotation, runtime.OwnsCollision, edgeInset: .15f) ||
                    !ClearBody(pose.position, pose.rotation, .35f)) continue;
                RestorePose(pose.position, pose.rotation); return true;
            }
            // A kerb/impact can leave no usable wheel-supported history. Search nearby
            // real road surfaces rather than sending an otherwise local crash to the pit.
            var origin = car.Body.position;
            var forward = Quaternion.Euler(0,car.Body.rotation.eulerAngles.y,0)*Vector3.forward;
            foreach (float radius in new[] { 0f,2,4,6,10,16,24 })
            {
                if (radius < minimumDistance) continue;
                for (int direction = 0; direction < (radius == 0 ? 1 : 8); direction++)
                {
                    var point = origin + Quaternion.Euler(0,180+direction*45,0)*forward*radius;
                    if (TryRoadPose(point, forward, out var position, out var rotation))
                    { RestorePose(position, rotation); return true; }
                }
            }
            return false;
        }

        private void RestorePose(Vector3 position, Quaternion rotation)
        {
            car.Body.position = position; car.Body.rotation = rotation;
            car.Body.linearVelocity = Vector3.zero; car.Body.angularVelocity = Vector3.zero;
            car.SetCommand(default); car.Body.WakeUp(); Physics.SyncTransforms();
            safePosition = position; safeRotation = rotation; hasSafePosition = true;
            wedgedSeconds = 0; LocalRecoveries++;
        }

        private bool TryRoadPose(Vector3 point, Vector3 forward, out Vector3 position, out Quaternion rotation)
        {
            position = default; rotation = default;
            int count = Physics.RaycastNonAlloc(point+Vector3.up*3,Vector3.down,hits,6,~0,QueryTriggerInteraction.Ignore);
            var best = default(RaycastHit); float closest = float.PositiveInfinity;
            for (int i = 0; i < count; i++)
            {
                var hit = hits[i];
                if (hit.collider.attachedRigidbody != null || hit.normal.y < .8f ||
                    hit.point.y > point.y+.6f || !runtime.OwnsCollision(hit.collider.gameObject.scene) ||
                    hit.collider.GetComponentInParent<DistrictGroundCoverage>() != null) continue;
                bool visible = !runtime.RequiresVisibleGround(hit.collider.gameObject.scene);
                for (int j = 0; !visible && j < count; j++)
                    visible = hits[j].normal.y > .5f && Mathf.Abs(hits[j].point.y-hit.point.y) <= .35f &&
                        runtime.OwnsCollision(hits[j].collider.gameObject.scene) &&
                        hits[j].collider.GetComponentInParent<DistrictGroundCoverage>() != null;
                float distance = Mathf.Abs(hit.point.y-(point.y-.24f));
                if (visible && distance < closest) { best = hit; closest = distance; }
            }
            if (best.collider == null) return false;
            position = best.point + Vector3.up*.24f;
            rotation = Quaternion.LookRotation(Vector3.ProjectOnPlane(forward,best.normal),best.normal);
            return ContainsFootprint(position,rotation,runtime.OwnsCollision,edgeInset:.15f) &&
                ClearBody(position,rotation,.35f);
        }

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
            // Run before rollback branches as well: repeated boundary corrections must not
            // prevent the trapped-car timer from advancing. Free driving avoids overlap queries.
            if (car.HasDriveInput && car.SpeedMetresPerSecond < 1.2f)
            {
                int rearGrounded = 0;
                foreach (var wheel in wheels)
                    if (wheel.transform.localPosition.z < 0 && wheel.GetGroundHit(out _)) rearGrounded++;
                bool wedged = Vector3.Dot(transform.up,Vector3.up) < .8f || rearGrounded == 0 ||
                    Time.fixedTime-lastObstacleContact < .1f || Time.fixedTime-lastBoundaryBlock < .1f ||
                    !ClearBody(body.position,body.rotation);
                wedgedSeconds = wedged ? wedgedSeconds+Time.fixedDeltaTime : 0;
                if (wedgedSeconds > 2 && TryRestoreNearby(2))
                { runtime.Chase.SnapToTarget(); return; }
            }
            else wedgedSeconds = 0;
            bool current = ContainsFootprint(body.position, body.rotation, Owned);
            if (!hasSafePosition)
            {
                if (!current) { runtime.RecoverNearby(); return; }
                safePosition = body.position; safeRotation = body.rotation; hasSafePosition = true;
            }
            if (!current)
            {
                lastBoundaryBlock = Time.fixedTime;
                if (!ContainsFootprint(safePosition, safeRotation, Owned))
                { runtime.RecoverNearby(); return; }
                body.position = safePosition; body.rotation = safeRotation;
                body.linearVelocity = Vector3.zero; body.angularVelocity = Vector3.zero;
                body.WakeUp(); Physics.SyncTransforms(); BlockedMoves++; return;
            }
            safePosition = body.position; safeRotation = body.rotation;
            bool marginSupported = ContainsFootprint(body.position, body.rotation, Owned, edgeInset: inset);
            if (Time.unscaledTime >= nextRoadPose)
            {
                nextRoadPose = Time.unscaledTime + .25f;
                int grounded = 0;
                foreach (var wheel in wheels) if (wheel.GetGroundHit(out _)) grounded++;
                if (marginSupported && grounded >= 3 && Vector3.Dot(transform.up, Vector3.up) > .9f &&
                    Time.fixedTime-lastObstacleContact > .3f && ClearBody(body.position, body.rotation, .35f) &&
                    (roadHistory.Count == 0 || (roadHistory[roadHistory.Count-1].position-body.position).sqrMagnitude > .25f))
                {
                    roadHistory.Add((body.position, body.rotation));
                    if (roadHistory.Count > 96) roadHistory.RemoveAt(0);
                }
            }
            // Sweep the upcoming step, rather than waiting until the car falls.
            // Sub-metre probes also catch short gaps at speed and sideways exits.
            var movement = body.linearVelocity * Time.fixedDeltaTime;
            float turn = body.angularVelocity.y * Time.fixedDeltaTime * Mathf.Rad2Deg;
            // Reject corrupt/extreme crash impulses before they create an unbounded query loop.
            if (!float.IsFinite(movement.sqrMagnitude) || !float.IsFinite(turn) ||
                movement.magnitude + Mathf.Abs(turn) * Mathf.Deg2Rad * 2.5f > 128)
            { body.linearVelocity = Vector3.zero; body.angularVelocity = Vector3.zero; BlockedMoves++; return; }
            int steps = Mathf.Max(1, Mathf.CeilToInt((movement.magnitude + Mathf.Abs(turn) * Mathf.Deg2Rad * 2.5f) / .5f));
            bool next = current;
            for (int i = 1; next && i <= steps; i++)
            {
                float fraction = i / (float)steps;
                var rotation = Quaternion.Euler(0, turn * fraction, 0) * body.rotation;
                // A crash can leave the exact chassis supported but its margin over a kerb/edge.
                // Such a car must still be able to back inward before the full margin becomes valid.
                next = ContainsFootprint(body.position + movement * fraction, rotation, Owned,
                    edgeInset: marginSupported ? inset : 0);
            }
            if (next)
            { safePosition = body.position; safeRotation = body.rotation; return; }
            // Cancel an unsafe turn independently, so it cannot cancel a valid reverse movement.
            bool translationSafe = true;
            for (int i = 1; translationSafe && i <= steps; i++)
                translationSafe = ContainsFootprint(body.position + movement * (i/(float)steps), body.rotation, Owned,
                    edgeInset: marginSupported ? inset : 0);
            if (translationSafe)
            { var angular = body.angularVelocity; angular.y = 0; body.angularVelocity = angular; return; }
            body.linearVelocity = Vector3.zero; body.angularVelocity = Vector3.zero;
            body.WakeUp();
            lastBoundaryBlock = Time.fixedTime;
            BlockedMoves++;
        }
    }
}
