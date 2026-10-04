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
        private readonly RaycastHit[] cameraHits = new RaycastHit[64];
        private int snappedFrame = -1;

        private void Awake()
        {
            cameraComponent = GetComponent<Camera>();
            if (target == null) throw new System.InvalidOperationException("Chase camera needs a car target.");
        }

        private void Start() => SnapToTarget();

        public void SnapToTarget()
        {
            // Transform interpolation may still expose the pre-recovery pose this frame.
            var position = target.Body.position; var rotation = target.Body.rotation;
            transform.position = ResolveCollision(position + rotation*followOffset,
                position + rotation*new Vector3(0,1.3f,0));
            transform.rotation = Quaternion.LookRotation(position + rotation*lookOffset - transform.position, Vector3.up);
            cameraComponent.fieldOfView = baseFieldOfView;
            snappedFrame = Time.frameCount;
        }

        private void LateUpdate()
        {
            float dt = Time.unscaledDeltaTime;
            if (dt <= 0 || Time.frameCount == snappedFrame) return;
            var desired = target.transform.TransformPoint(followOffset);
            float follow = 1 - Mathf.Exp(-followSharpness * dt);
            // Protect the actual smoothed camera position, which trails further behind at speed.
            transform.position = ResolveCollision(Vector3.Lerp(transform.position, desired, follow),
                target.transform.TransformPoint(new Vector3(0,1.3f,0)));
            var direction = target.transform.TransformPoint(lookOffset) - transform.position;
            if (direction.sqrMagnitude > .01f)
                transform.rotation = Quaternion.Slerp(transform.rotation,
                    Quaternion.LookRotation(direction, Vector3.up), 1 - Mathf.Exp(-turnSharpness * dt));
            float fov = baseFieldOfView + speedFieldOfViewGain * Mathf.Clamp01(target.SpeedMetresPerSecond / 55);
            cameraComponent.fieldOfView = Mathf.Lerp(cameraComponent.fieldOfView, fov, follow);
        }

        private Vector3 ResolveCollision(Vector3 desired, Vector3 origin)
        {
            var delta = desired - origin;
            if (delta.sqrMagnitude < .001f) return desired;
            int count = Physics.SphereCastNonAlloc(origin,.24f,delta.normalized,cameraHits,delta.magnitude,
                ~0,QueryTriggerInteraction.Ignore);
            float nearest = float.PositiveInfinity;
            for (int i = 0; i < count; i++)
                if (!cameraHits[i].collider.transform.IsChildOf(target.transform))
                    nearest = Mathf.Min(nearest,cameraHits[i].distance);
            if (float.IsFinite(nearest))
                return origin + delta.normalized*Mathf.Max(.5f,nearest-.18f);
            return desired;
        }
    }
}
