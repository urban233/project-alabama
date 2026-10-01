using UnityEngine;

namespace Alabama.Driving
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Camera))]
    public sealed class ChaseCamera : MonoBehaviour
    {
        [SerializeField] private ArcadeCarController target;
        [SerializeField] private Vector3 followOffset = new Vector3(0, 1.9f, -6.0f);
        [SerializeField] private Vector3 lookOffset = new Vector3(0, 1.2f, 12);
        [SerializeField, Min(.1f)] private float followSharpness = 6;
        [SerializeField, Min(.1f)] private float turnSharpness = 8;
        [SerializeField, Range(50, 100)] private float baseFieldOfView = 66;
        [SerializeField, Range(0, 20)] private float speedFieldOfViewGain = 8;

        private Camera cameraComponent;

        private void Awake()
        {
            cameraComponent = GetComponent<Camera>();
            if (target == null) throw new System.InvalidOperationException("Chase camera needs a car target.");
        }

        private void Start() => SnapToTarget();

        public void SnapToTarget()
        {
            transform.position = ResolveCollision(target.transform.TransformPoint(followOffset));
            transform.rotation = Quaternion.LookRotation(target.transform.TransformPoint(lookOffset) - transform.position, Vector3.up);
            cameraComponent.fieldOfView = baseFieldOfView;
        }

        private void LateUpdate()
        {
            float dt = Time.unscaledDeltaTime;
            if (dt <= 0) return;
            var desired = ResolveCollision(target.transform.TransformPoint(followOffset));
            float follow = 1 - Mathf.Exp(-followSharpness * dt);
            transform.position = Vector3.Lerp(transform.position, desired, follow);
            var direction = target.transform.TransformPoint(lookOffset) - transform.position;
            if (direction.sqrMagnitude > .01f)
                transform.rotation = Quaternion.Slerp(transform.rotation,
                    Quaternion.LookRotation(direction, Vector3.up), 1 - Mathf.Exp(-turnSharpness * dt));
            float fov = baseFieldOfView + speedFieldOfViewGain * Mathf.Clamp01(target.SpeedMetresPerSecond / 55);
            cameraComponent.fieldOfView = Mathf.Lerp(cameraComponent.fieldOfView, fov, follow);
        }

        private Vector3 ResolveCollision(Vector3 desired)
        {
            var origin = target.transform.TransformPoint(new Vector3(0, 1.3f, 0));
            var delta = desired - origin;
            if (Physics.SphereCast(origin, .24f, delta.normalized, out var hit, delta.magnitude,
                    ~0, QueryTriggerInteraction.Ignore) && !hit.collider.transform.IsChildOf(target.transform))
                return origin + delta.normalized * Mathf.Max(.5f, hit.distance - .18f);
            return desired;
        }
    }
}
