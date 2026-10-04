using System;
using UnityEngine;

namespace Alabama.Driving
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Rigidbody))]
    public sealed class ArcadeCarController : MonoBehaviour
    {
        [SerializeField] private VehicleTuning tuning;
        [SerializeField] private WheelCollider frontLeft;
        [SerializeField] private WheelCollider frontRight;
        [SerializeField] private WheelCollider rearLeft;
        [SerializeField] private WheelCollider rearRight;
        [SerializeField] private Transform frontLeftVisual;
        [SerializeField] private Transform frontRightVisual;
        [SerializeField] private Transform rearLeftVisual;
        [SerializeField] private Transform rearRightVisual;

        private Rigidbody body;
        private VehicleCommand command;
        private Vector3 spawnPosition;
        private Quaternion spawnRotation;
        private WheelFrictionCurve rearLeftGrip;
        private WheelFrictionCurve rearRightGrip;

        public float SpeedMetresPerSecond => body == null ? 0 : body.linearVelocity.magnitude;
        public float SignedForwardSpeed => body == null ? 0 : Vector3.Dot(body.linearVelocity, transform.forward);
        public bool IsReversing { get; private set; }
        public float SteerAngle => frontLeft == null ? 0 : frontLeft.steerAngle;
        public Rigidbody Body => body;
        public VehicleTuning Tuning => tuning;
        public bool HasDriveInput => command.Throttle > .1f || command.Brake > .1f;

        private void Awake()
        {
            if (tuning == null || frontLeft == null || frontRight == null || rearLeft == null || rearRight == null ||
                frontLeftVisual == null || frontRightVisual == null || rearLeftVisual == null || rearRightVisual == null)
                throw new InvalidOperationException("The drive prefab needs tuning, four wheels, and four visual pivots.");
            tuning.Validate();
            body = GetComponent<Rigidbody>();
            body.mass = tuning.Mass;
            body.centerOfMass = tuning.CentreOfMass;
            body.interpolation = RigidbodyInterpolation.Interpolate;
            body.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            body.linearDamping = .018f;
            body.angularDamping = 1.1f;
            spawnPosition = transform.position;
            spawnRotation = transform.rotation;
            foreach (var wheel in Wheels()) ConfigureWheel(wheel);
            rearLeftGrip = rearLeft.sidewaysFriction;
            rearRightGrip = rearRight.sidewaysFriction;
            frontLeft.ConfigureVehicleSubsteps(5, 12, 15);
        }

        public void SetCommand(VehicleCommand value) => command = value;

        // District lifetime may change the safe reset destination, without changing
        // the vehicle/input instance or moving a driving vehicle.
        public void SetRecoveryPose(Vector3 position, Quaternion rotation)
        { spawnPosition = position; spawnRotation = rotation; }

        private void FixedUpdate()
        {
            if (command.Throttle > .1f || command.Brake > .1f) body.WakeUp();
            float signedSpeed = SignedForwardSpeed;
            IsReversing = command.Brake > .1f && signedSpeed < 1.2f && command.Throttle < .1f;
            float serviceBrake = IsReversing ? 0 : command.Brake * tuning.ServiceBrakeTorque;
            float drive = 0;
            if (IsReversing && signedSpeed > -12) drive = -command.Brake * tuning.ReverseTorque;
            else if (command.Throttle > 0 && signedSpeed < tuning.MaximumSpeedMetresPerSecond)
                drive = command.Throttle * tuning.DriveTorque;
            // Positive torque on the rear axle; front wheels steer and brake.
            rearLeft.motorTorque = drive;
            rearRight.motorTorque = drive;
            frontLeft.brakeTorque = serviceBrake;
            frontRight.brakeTorque = serviceBrake;
            rearLeft.brakeTorque = serviceBrake + (command.Handbrake ? tuning.HandbrakeTorque : 0);
            rearRight.brakeTorque = rearLeft.brakeTorque;
            float steerFactor = Mathf.Clamp01(Mathf.Abs(signedSpeed) / tuning.HighSpeedSteerAtMetresPerSecond);
            float steer = command.Steer * Mathf.Lerp(tuning.LowSpeedSteerDegrees, tuning.HighSpeedSteerDegrees, steerFactor);
            frontLeft.steerAngle = steer;
            frontRight.steerAngle = steer;
            SetRearGrip(rearLeft, rearLeftGrip, command.Handbrake);
            SetRearGrip(rearRight, rearRightGrip, command.Handbrake);
            if (SpeedMetresPerSecond > 1)
                body.AddForce(-transform.up * (tuning.AerodynamicDownforce * body.linearVelocity.sqrMagnitude), ForceMode.Force);
        }

        private void LateUpdate()
        {
            MatchVisual(frontLeft, frontLeftVisual);
            MatchVisual(frontRight, frontRightVisual);
            MatchVisual(rearLeft, rearLeftVisual);
            MatchVisual(rearRight, rearRightVisual);
        }

        public void ResetToSpawn()
        {
            body.position = spawnPosition;
            body.rotation = spawnRotation;
            if (!body.isKinematic)
            {
                body.linearVelocity = Vector3.zero;
                body.angularVelocity = Vector3.zero;
            }
            command = default;
            IsReversing = false;
            body.WakeUp();
            Physics.SyncTransforms();
        }

        private void ConfigureWheel(WheelCollider wheel)
        {
            wheel.radius = tuning.WheelRadius;
            wheel.suspensionDistance = tuning.SuspensionDistance;
            wheel.mass = 22;
            wheel.wheelDampingRate = 1.2f;
            wheel.forceAppPointDistance = .05f;
            var suspension = wheel.suspensionSpring;
            suspension.spring = tuning.Spring;
            suspension.damper = tuning.Damper;
            suspension.targetPosition = .5f;
            wheel.suspensionSpring = suspension;
            var forward = wheel.forwardFriction;
            forward.stiffness = tuning.ForwardGrip;
            wheel.forwardFriction = forward;
            var lateral = wheel.sidewaysFriction;
            lateral.stiffness = tuning.LateralGrip;
            wheel.sidewaysFriction = lateral;
        }

        private void SetRearGrip(WheelCollider wheel, WheelFrictionCurve normal, bool handbrake)
        {
            var friction = normal;
            if (handbrake) friction.stiffness *= tuning.HandbrakeRearGrip;
            wheel.sidewaysFriction = friction;
        }

        private static void MatchVisual(WheelCollider wheel, Transform visual)
        {
            wheel.GetWorldPose(out var position, out var rotation);
            visual.SetPositionAndRotation(position, rotation);
        }

        private WheelCollider[] Wheels() => new[] { frontLeft, frontRight, rearLeft, rearRight };
    }
}
