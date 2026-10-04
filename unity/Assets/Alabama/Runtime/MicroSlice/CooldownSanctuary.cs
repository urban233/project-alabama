using Alabama.Driving;
using UnityEngine;

namespace Alabama.MicroSlice
{
    public sealed class CooldownState
    {
        public float HiddenSeconds { get; private set; }
        public bool Ready => HiddenSeconds >= 3;
        public void Tick(float delta, bool inside, bool hidden, float speed)
        { HiddenSeconds = inside && hidden && speed <= 3 ? HiddenSeconds + Mathf.Max(0, delta) : 0; }
        public void Reset() => HiddenSeconds = 0;
    }

    [RequireComponent(typeof(BoxCollider))]
    public sealed class CooldownSanctuary : MonoBehaviour
    {
        [SerializeField] private ArcadeCarController vehicle;
        [SerializeField] private Transform observer;
        [SerializeField] private LayerMask occluders;
        public CooldownState State { get; } = new CooldownState();
        public bool CooledDown => State.Ready;
        private float lastContact = float.NegativeInfinity;

        public void Configure(ArcadeCarController car, Transform lineOfSightObserver, int mask)
        { vehicle = car; observer = lineOfSightObserver; occluders = mask; }
        public void ResetCooldown() { State.Reset(); lastContact = float.NegativeInfinity; }
        private void OnTriggerStay(Collider other)
        {
            if (other.attachedRigidbody != null && other.attachedRigidbody == vehicle?.Body)
                lastContact = Time.fixedTime;
        }
        private void Update()
        {
            if (vehicle == null || observer == null) { State.Reset(); return; }
            bool inside = Time.fixedTime - lastContact <= Time.fixedDeltaTime * 2.1f;
            bool hidden = Physics.Linecast(observer.position, vehicle.transform.position + Vector3.up,
                occluders, QueryTriggerInteraction.Ignore);
            State.Tick(Time.deltaTime, inside, hidden, vehicle.SpeedMetresPerSecond);
        }
    }
}
