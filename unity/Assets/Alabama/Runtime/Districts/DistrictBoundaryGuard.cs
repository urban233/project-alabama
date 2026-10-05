using System;
using System.Collections;
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
        private float lastGroundHeight;
        private float lastObstacleContact = float.NegativeInfinity;
        private float lastBoundaryBlock = float.NegativeInfinity;
        // Match the E46's 1.9 x 4.45 metre chassis collider, including overhangs.
        private static readonly Vector3[] Footprint = { new Vector3(-.95f, 0, -2.225f),
            new Vector3(-.95f, 0, 2.225f), new Vector3(.95f, 0, -2.225f), new Vector3(.95f, 0, 2.225f) };
        public int BlockedMoves { get; private set; }
        public int LocalRecoveries { get; private set; }
        public int RecoveryPoseCount => roadHistory.Count;

        private void OnEnable() { if (Application.isPlaying) StartCoroutine(CheckAfterPhysics()); }
        private void OnDisable() => StopAllCoroutines();

        private IEnumerator CheckAfterPhysics()
        {
            var step = new WaitForFixedUpdate();
            while (true)
            {
                yield return step;
                if (runtime == null || !runtime.Ready || runtime.Busy || car.Body.isKinematic || !hasSafePosition) continue;
                var body = car.Body;
                if (BoundarySupported(body.position,body.rotation,0))
                { safePosition = body.position; safeRotation = body.rotation; continue; }
                // Suspension/contact impulses happen after FixedUpdate's prediction. Resolve
                // an actual edge crossing before the render/input frame, retaining safe motion.
                if (!BoundarySupported(safePosition,safeRotation,0))
                { runtime.RecoverNearby(); continue; }
                var travelled = body.position-safePosition;
                var rotation = body.rotation;
                body.position = safePosition; body.rotation = safeRotation;
                // Preserve the supported part of the completed step as well as its velocity.
                // Rolling back all translation would trap a car repeatedly pushed toward an edge.
                body.position += SupportedSlide(travelled,0);
                if (BoundarySupported(body.position,rotation,0)) body.rotation = rotation;
                var velocity = body.linearVelocity;
                var allowed = SupportedSlide(velocity*Time.fixedDeltaTime,0);
                body.linearVelocity = new Vector3(allowed.x/Time.fixedDeltaTime,velocity.y,allowed.z/Time.fixedDeltaTime);
                float turn = body.angularVelocity.y*Time.fixedDeltaTime*Mathf.Rad2Deg;
                if (!SupportedStep(allowed,turn,0))
                { var angular = body.angularVelocity; angular.y = 0; body.angularVelocity = angular; }
                lastBoundaryBlock = Time.fixedTime;
                body.WakeUp(); Physics.SyncTransforms(); BlockedMoves++;
                safePosition = body.position; safeRotation = body.rotation;
            }
        }

        public void Configure(DistrictRuntime owner)
        { runtime = owner; car = owner.Player; wheels = car.GetComponentsInChildren<WheelCollider>(); ResetHistory(); }

        public void ResetHistory()
        { hasSafePosition = false; roadHistory.Clear(); nextRoadPose = 0; wedgedSeconds = 0;
            lastGroundHeight = car == null || car.Body == null ? transform.position.y : car.Body.position.y;
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
            lastGroundHeight = position.y;
            wedgedSeconds = 0; LocalRecoveries++;
        }

        private bool TryRoadPose(Vector3 point, Vector3 forward, out Vector3 position, out Quaternion rotation)
        {
            position = default; rotation = default;
            int count = Physics.RaycastNonAlloc(point+Vector3.up*3,Vector3.down,hits,15,~0,QueryTriggerInteraction.Ignore);
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
            bool requireVisibleGround = false, float edgeInset = 0, float maximumDrop = 3)
        {
            // Yaw-only chassis footprint keeps slopes and short crest jumps valid.
            var yaw = Quaternion.Euler(0, rotation.eulerAngles.y, 0);
            foreach (var offset in Footprint)
                {
                    var expanded = offset + new Vector3(Mathf.Sign(offset.x) * edgeInset, 0,
                        Mathf.Sign(offset.z) * edgeInset);
                    var point = position + yaw * expanded;
                    int count = Physics.RaycastNonAlloc(point + Vector3.up * 3, Vector3.down,
                        hits, 3+Mathf.Clamp(maximumDrop,3,12), ~0, QueryTriggerInteraction.Ignore);
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
            bool directlySupported = ContainsFootprint(body.position,body.rotation,runtime.OwnsCollision);
            // Tyres can touch a tall prop during a roll. Only road-supported poses may
            // update the height reference; a prop top must not replace the road below.
            if (directlySupported)
                foreach (var wheel in wheels)
                    if (wheel.GetGroundHit(out var ground))
                    { lastGroundHeight = ground.point.y + .24f; break; }
            bool current = directlySupported || BoundarySupported(body.position, body.rotation, 0);
            if (!hasSafePosition)
            {
                if (!current) { runtime.RecoverNearby(); return; }
                safePosition = body.position; safeRotation = body.rotation; hasSafePosition = true;
            }
            if (!current)
            {
                lastBoundaryBlock = Time.fixedTime;
                if (!BoundarySupported(safePosition, safeRotation, 0))
                { runtime.RecoverNearby(); return; }
                body.position = safePosition; body.rotation = safeRotation;
                body.linearVelocity = Vector3.zero; body.angularVelocity = Vector3.zero;
                body.WakeUp(); Physics.SyncTransforms(); BlockedMoves++; return;
            }
            safePosition = body.position; safeRotation = body.rotation;
            bool marginSupported = BoundarySupported(body.position, body.rotation, inset);
            // Retain a small contact allowance when the full margin no longer fits.
            // This absorbs suspension/yaw corrections without blocking a car already on the edge.
            float movementInset = marginSupported ? inset : BoundarySupported(body.position,body.rotation,.03f) ? .03f : 0;
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
                next = BoundarySupported(body.position + movement * fraction, rotation, movementInset);
            }
            if (next)
            { safePosition = body.position; safeRotation = body.rotation; return; }
            // Cancel an unsafe turn independently, so it cannot cancel a valid reverse movement.
            bool translationSafe = true;
            for (int i = 1; translationSafe && i <= steps; i++)
                translationSafe = BoundarySupported(body.position + movement * (i/(float)steps), body.rotation,
                    movementInset);
            if (translationSafe)
            { var angular = body.angularVelocity; angular.y = 0; body.angularVelocity = angular; return; }
            // Keep supported tangential motion and pitch/roll impulses. Only a real map edge
            // constrains translation; normal collisions are left to PhysX without a speed cap.
            var allowed = SupportedSlide(movement, movementInset);
            var velocity = body.linearVelocity;
            body.linearVelocity = new Vector3(allowed.x/Time.fixedDeltaTime,velocity.y,allowed.z/Time.fixedDeltaTime);
            // A slide and yaw can each fit separately but cross an edge when combined.
            if (!SupportedStep(allowed,turn,movementInset))
            { var angular = body.angularVelocity; angular.y = 0; body.angularVelocity = angular; }
            body.WakeUp();
            lastBoundaryBlock = Time.fixedTime;
            BlockedMoves++;
        }

        private bool BoundarySupported(Vector3 position, Quaternion rotation, float inset)
        {
            if (ContainsFootprint(position,rotation,runtime.OwnsCollision,edgeInset:inset)) return true;
            // Short airborne crashes/jumps may exceed the ordinary six-metre road ray.
            // Check the same horizontal footprint at the last grounded height, never empty space.
            if (!hasSafePosition || position.y <= lastGroundHeight+.5f || position.y > lastGroundHeight+12) return false;
            position.y = lastGroundHeight;
            return ContainsFootprint(position,rotation,runtime.OwnsCollision,edgeInset:inset);
        }

        private bool SupportedStep(Vector3 movement, float turn, float inset)
        {
            int steps = Mathf.Max(1,Mathf.CeilToInt((movement.magnitude+Mathf.Abs(turn)*Mathf.Deg2Rad*2.5f)/.5f));
            for (int i = 1; i <= steps; i++)
                if (!BoundarySupported(car.Body.position + movement*(i/(float)steps),
                    Quaternion.Euler(0,turn*(i/(float)steps),0)*car.Body.rotation,inset)) return false;
            return true;
        }

        private Vector3 SupportedSlide(Vector3 movement, float inset)
        {
            var horizontal = new Vector3(movement.x,0,movement.z);
            var best = Vector3.zero;
            // Project rather than redirect momentum. No candidate adds speed or blocks inward travel.
            for (int direction = 0; direction < 8; direction++)
            {
                var axis = Quaternion.Euler(0,direction*22.5f,0)*Vector3.forward;
                var candidate = Vector3.Project(horizontal,axis); candidate.y = movement.y;
                if (candidate.x*candidate.x+candidate.z*candidate.z <= best.x*best.x+best.z*best.z+.0000001f) continue;
                if (SupportedStep(candidate,0,inset)) best = candidate;
            }
            return best;
        }
    }
}
