using System;
using UnityEngine;

namespace Alabama.Driving
{
    [CreateAssetMenu(menuName = "Alabama/Vehicle Tuning", fileName = "VehicleTuning")]
    public sealed class VehicleTuning : ScriptableObject
    {
        [Header("Chassis")]
        [SerializeField, Min(500)] private float mass = 1450;
        [SerializeField] private Vector3 centreOfMass = new Vector3(0, .38f, 0);
        [SerializeField, Range(0, 2)] private float aerodynamicDownforce = .45f;
        [Header("Suspension and tyres")]
        [SerializeField, Range(.2f, .5f)] private float wheelRadius = .33f;
        [SerializeField, Range(.05f, .35f)] private float suspensionDistance = .18f;
        [SerializeField, Min(1000)] private float spring = 35000;
        [SerializeField, Min(100)] private float damper = 4500;
        [SerializeField, Range(.5f, 3)] private float forwardGrip = 1.65f;
        [SerializeField, Range(.5f, 3)] private float lateralGrip = 1.65f;
        [SerializeField, Range(.2f, 1)] private float handbrakeRearGrip = .58f;
        [Header("Arcade response")]
        [SerializeField, Range(5, 45)] private float lowSpeedSteerDegrees = 33;
        [SerializeField, Range(4, 30)] private float highSpeedSteerDegrees = 10;
        [SerializeField, Range(20, 100)] private float highSpeedSteerAtMetresPerSecond = 50;
        [SerializeField, Min(100)] private float driveTorque = 1550;
        [SerializeField, Min(50)] private float reverseTorque = 550;
        [SerializeField, Min(100)] private float serviceBrakeTorque = 3300;
        [SerializeField, Min(100)] private float handbrakeTorque = 2200;
        [SerializeField, Range(20, 100)] private float maximumSpeedMetresPerSecond = 62;

        public float Mass => mass;
        public Vector3 CentreOfMass => centreOfMass;
        public float AerodynamicDownforce => aerodynamicDownforce;
        public float WheelRadius => wheelRadius;
        public float SuspensionDistance => suspensionDistance;
        public float Spring => spring;
        public float Damper => damper;
        public float ForwardGrip => forwardGrip;
        public float LateralGrip => lateralGrip;
        public float HandbrakeRearGrip => handbrakeRearGrip;
        public float LowSpeedSteerDegrees => lowSpeedSteerDegrees;
        public float HighSpeedSteerDegrees => highSpeedSteerDegrees;
        public float HighSpeedSteerAtMetresPerSecond => highSpeedSteerAtMetresPerSecond;
        public float DriveTorque => driveTorque;
        public float ReverseTorque => reverseTorque;
        public float ServiceBrakeTorque => serviceBrakeTorque;
        public float HandbrakeTorque => handbrakeTorque;
        public float MaximumSpeedMetresPerSecond => maximumSpeedMetresPerSecond;

        public void Validate()
        {
            if (!IsPositive(mass) || !IsPositive(wheelRadius) || !IsPositive(suspensionDistance) ||
                !IsPositive(spring) || !IsPositive(damper) || !IsPositive(forwardGrip) ||
                !IsPositive(lateralGrip) || !IsPositive(handbrakeRearGrip) ||
                !IsPositive(lowSpeedSteerDegrees) || !IsPositive(highSpeedSteerDegrees) ||
                !IsPositive(highSpeedSteerAtMetresPerSecond) || !IsPositive(driveTorque) ||
                !IsPositive(reverseTorque) || !IsPositive(serviceBrakeTorque) ||
                !IsPositive(handbrakeTorque) || !IsPositive(maximumSpeedMetresPerSecond) ||
                !float.IsFinite(aerodynamicDownforce) || aerodynamicDownforce < 0 ||
                highSpeedSteerDegrees > lowSpeedSteerDegrees || handbrakeRearGrip > 1 ||
                !IsFinite(centreOfMass))
                throw new InvalidOperationException("Vehicle tuning contains invalid physics parameters.");
        }

        private static bool IsPositive(float value) => float.IsFinite(value) && value > 0;
        private static bool IsFinite(Vector3 value) =>
            float.IsFinite(value.x) && float.IsFinite(value.y) && float.IsFinite(value.z);
    }
}
