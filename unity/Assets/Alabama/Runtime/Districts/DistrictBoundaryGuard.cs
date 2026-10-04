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
        private readonly RaycastHit[] hits = new RaycastHit[32];
        // Match the E46's 1.9 x 4.45 metre chassis collider, including overhangs.
        private static readonly Vector3[] Footprint = { new Vector3(-.95f, 0, -2.225f),
            new Vector3(-.95f, 0, 2.225f), new Vector3(.95f, 0, -2.225f), new Vector3(.95f, 0, 2.225f) };
        public int BlockedMoves { get; private set; }

        public void Configure(DistrictRuntime owner)
        { runtime = owner; car = owner.Player; hasSafePosition = false; }

        public void ResetHistory() => hasSafePosition = false;

        public bool ContainsFootprint(Vector3 position, Quaternion rotation, Func<Scene, bool> ownsScene)
        {
            // Yaw-only chassis footprint keeps slopes and short crest jumps valid.
            var yaw = Quaternion.Euler(0, rotation.eulerAngles.y, 0);
            foreach (var offset in Footprint)
                {
                    var point = position + yaw * offset;
                    int count = Physics.RaycastNonAlloc(point + Vector3.up * 3, Vector3.down,
                        hits, 12, ~0, QueryTriggerInteraction.Ignore);
                    bool supported = false;
                    for (int i = 0; i < count; i++)
                        if (hits[i].collider.attachedRigidbody == null && hits[i].normal.y > .5f &&
                            hits[i].point.y <= point.y + .6f &&
                            ownsScene(hits[i].collider.gameObject.scene)) { supported = true; break; }
                    if (!supported) return false;
                }
            return true;
        }

        private void FixedUpdate()
        {
            if (runtime == null || !runtime.Ready || runtime.Busy || car.Body.isKinematic) return;
            bool Owned(Scene scene) => runtime.OwnsCollision(scene);
            var body = car.Body;
            bool current = ContainsFootprint(body.position, body.rotation, Owned);
            if (!hasSafePosition)
            {
                if (!current) { runtime.Recover(); return; }
                safePosition = body.position; safeRotation = body.rotation; hasSafePosition = true;
            }
            // Sweep the upcoming step, rather than waiting until the car falls.
            // Sub-metre probes also catch short gaps at speed and sideways exits.
            var movement = body.linearVelocity * Time.fixedDeltaTime;
            int steps = Mathf.Max(1, Mathf.CeilToInt(movement.magnitude / .5f));
            bool next = current;
            for (int i = 1; next && i <= steps; i++)
                next = ContainsFootprint(body.position + movement * (i / (float)steps), body.rotation, Owned);
            if (next)
            { safePosition = body.position; safeRotation = body.rotation; return; }
            body.position = safePosition; body.rotation = safeRotation;
            body.linearVelocity = Vector3.zero; body.angularVelocity = Vector3.zero;
            Physics.SyncTransforms();
            BlockedMoves++;
        }
    }
}
