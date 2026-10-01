using UnityEngine;

namespace Alabama.Driving
{
    /// <summary>A complete control sample. Input devices never write to physics directly.</summary>
    public readonly struct VehicleCommand
    {
        public readonly float Throttle;
        public readonly float Brake;
        public readonly float Steer;
        public readonly bool Handbrake;

        public VehicleCommand(float throttle, float brake, float steer, bool handbrake)
        {
            Throttle = float.IsFinite(throttle) ? Mathf.Clamp01(throttle) : 0;
            Brake = float.IsFinite(brake) ? Mathf.Clamp01(brake) : 0;
            Steer = float.IsFinite(steer) ? Mathf.Clamp(steer, -1, 1) : 0;
            Handbrake = handbrake;
        }
    }
}
