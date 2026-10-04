using System.Collections.Generic;
using UnityEngine;

namespace Alabama.MicroSlice
{
    // A trigger releases the structure before a heavy car contacts its static shell.
    [RequireComponent(typeof(BoxCollider))]
    public sealed class BreakableStructure : MonoBehaviour
    {
        [SerializeField] private Rigidbody[] pieces;
        [SerializeField] private float minimumActivatorMass = 200;
        private Vector3[] positions;
        private Quaternion[] rotations;
        private readonly List<(Collider piece, Collider car)> ignored = new();
        public bool Released { get; private set; }

        public void Configure(Rigidbody[] parts) { pieces = parts; Capture(); }
        private void Awake()
        {
            GetComponent<BoxCollider>().isTrigger = true;
            Capture();
        }
        private void Capture()
        {
            if (pieces == null) return;
            positions = new Vector3[pieces.Length]; rotations = new Quaternion[pieces.Length];
            for (int i = 0; i < pieces.Length; i++)
            { positions[i] = pieces[i].transform.localPosition; rotations[i] = pieces[i].transform.localRotation; }
        }
        private void OnTriggerEnter(Collider other)
        {
            var body = other.attachedRigidbody;
            if (body != null && body.mass >= minimumActivatorMass) Release(body);
        }
        public void Release(Rigidbody activator)
        {
            if (Released || activator == null || pieces == null || pieces.Length == 0) return;
            Released = true;
            var carColliders = activator.GetComponentsInChildren<Collider>();
            foreach (var piece in pieces)
            {
                foreach (var collider in piece.GetComponentsInChildren<Collider>())
                    foreach (var car in carColliders)
                    {
                        if (car.isTrigger || collider.isTrigger) continue;
                        Physics.IgnoreCollision(collider, car, true);
                        ignored.Add((collider, car));
                    }
                piece.isKinematic = false;
                piece.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
                piece.AddForce(new Vector3(.3f, -.1f, .3f), ForceMode.Impulse);
            }
        }
        public void Rearm()
        {
            foreach (var pair in ignored)
                if (pair.piece != null && pair.car != null) Physics.IgnoreCollision(pair.piece, pair.car, false);
            ignored.Clear();
            if (pieces != null && positions != null)
                for (int i = 0; i < pieces.Length; i++)
                {
                    if (!pieces[i].isKinematic)
                    { pieces[i].linearVelocity = Vector3.zero; pieces[i].angularVelocity = Vector3.zero; }
                    pieces[i].isKinematic = true;
                    pieces[i].collisionDetectionMode = CollisionDetectionMode.Discrete;
                    pieces[i].transform.SetLocalPositionAndRotation(positions[i], rotations[i]);
                }
            Released = false;
        }
        private void OnDestroy()
        {
            foreach (var pair in ignored)
                if (pair.piece != null && pair.car != null) Physics.IgnoreCollision(pair.piece, pair.car, false);
        }
    }
}
