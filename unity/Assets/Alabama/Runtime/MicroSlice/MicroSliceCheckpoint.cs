using UnityEngine;

namespace Alabama.MicroSlice
{
    [RequireComponent(typeof(BoxCollider))]
    public sealed class MicroSliceCheckpoint : MonoBehaviour
    {
        [SerializeField] private MicroSliceSession session;
        [SerializeField] private int index;
        public void Configure(MicroSliceSession owner, int checkpoint)
        { session = owner; index = checkpoint; }
        private void OnTriggerEnter(Collider other)
        {
            if (other.attachedRigidbody != null)
                session?.RegisterGate(index, transform.forward, other.attachedRigidbody);
        }
    }
}
