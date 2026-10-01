using UnityEngine;

namespace Alabama.Driving
{
    /// <summary>Repeatable conservative driving command for route tests and performance runs.</summary>
    public static class RouteFollower
    {
        public static VehicleCommand Command(ArcadeCarController car, RouteProgress route)
        {
            float speed = Mathf.Max(0, car.SignedForwardSpeed);
            float distance = route.DistanceMetres;
            float curve = Vector3.Angle(route.DirectionAt(distance), route.DirectionAt(distance + 26));
            float desiredSpeed = Mathf.Lerp(26, 12, Mathf.Clamp01(curve / 50));
            Vector3 target = route.PositionAt(distance + Mathf.Lerp(12, 20, Mathf.Clamp01(speed / 25)));
            Vector3 local = car.transform.InverseTransformPoint(target);
            float steer = Mathf.Clamp(local.x / Mathf.Max(7, local.z), -1, 1);
            float throttle = speed < desiredSpeed - 1 ? .85f : 0;
            float brake = speed > desiredSpeed + 1 ? .45f : 0;
            return new VehicleCommand(throttle, brake, steer, false);
        }
    }
}
