using System;
using UnityEngine;

namespace Alabama.Driving
{
    /// <summary>Tracks progress on an authored closed road centreline without physics shortcuts.</summary>
    public sealed class RouteProgress : MonoBehaviour
    {
        [SerializeField] private Transform vehicle;
        [SerializeField] private Vector3[] centreline;
        [SerializeField, Min(1)] private float maximumTrackingDistance = 12;

        private float[] cumulative;
        private float previousDistance;
        private float forwardDistanceSinceLap;
        private bool hasSample;

        public float LengthMetres { get; private set; }
        public float DistanceMetres { get; private set; }
        public float DistanceFromRoadMetres { get; private set; }
        public int CompletedLaps { get; private set; }
        public float Fraction => LengthMetres > 0 ? DistanceMetres / LengthMetres : 0;
        public int PointCount => centreline?.Length ?? 0;

        private void Awake()
        {
            if (vehicle == null || centreline == null || centreline.Length < 4 ||
                Vector3.Distance(centreline[0], centreline[^1]) > .01f)
                throw new InvalidOperationException("Route progress needs a vehicle and a closed centreline.");
            cumulative = new float[centreline.Length];
            for (int i = 1; i < centreline.Length; i++)
                cumulative[i] = cumulative[i - 1] + Vector3.Distance(centreline[i - 1], centreline[i]);
            LengthMetres = cumulative[^1];
            if (LengthMetres < 100) throw new InvalidOperationException("The route is too short for lap tracking.");
            Sample();
        }

        private void LateUpdate() => Sample();

        public Vector3 PositionAt(float distance)
        {
            if (cumulative == null) throw new InvalidOperationException("Route has not initialized.");
            distance = Mathf.Repeat(distance, LengthMetres);
            int index = Array.BinarySearch(cumulative, distance);
            if (index < 0) index = ~index - 1;
            index = Mathf.Clamp(index, 0, centreline.Length - 2);
            float segment = cumulative[index + 1] - cumulative[index];
            return Vector3.Lerp(centreline[index], centreline[index + 1],
                segment > 0 ? (distance - cumulative[index]) / segment : 0);
        }

        public Vector3 DirectionAt(float distance)
        {
            float ahead = .5f;
            return (PositionAt(distance + ahead) - PositionAt(distance - ahead)).normalized;
        }

        public void ResetProgress()
        {
            CompletedLaps = 0;
            forwardDistanceSinceLap = 0;
            hasSample = false;
            if (cumulative != null) Sample();
        }

        private void Sample()
        {
            var position = vehicle.position;
            float bestSquared = float.PositiveInfinity;
            float nearest = 0;
            for (int i = 0; i < centreline.Length - 1; i++)
            {
                var a = centreline[i];
                var segment = centreline[i + 1] - a;
                float t = Mathf.Clamp01(Vector3.Dot(position - a, segment) / segment.sqrMagnitude);
                var projected = a + t * segment;
                float squared = (position - projected).sqrMagnitude;
                if (squared < bestSquared)
                {
                    bestSquared = squared;
                    nearest = cumulative[i] + t * segment.magnitude;
                }
            }
            DistanceFromRoadMetres = Mathf.Sqrt(bestSquared);
            DistanceMetres = nearest;
            if (!hasSample)
            {
                previousDistance = nearest;
                hasSample = true;
                return;
            }
            float delta = nearest - previousDistance;
            if (delta < -LengthMetres / 2) delta += LengthMetres;
            if (delta > LengthMetres / 2) delta -= LengthMetres;
            bool validStep = DistanceFromRoadMetres <= maximumTrackingDistance && Mathf.Abs(delta) < 15;
            if (validStep)
            {
                forwardDistanceSinceLap = Mathf.Max(0, forwardDistanceSinceLap + delta);
                if (previousDistance > LengthMetres * .9f && nearest < LengthMetres * .1f && delta > 0 &&
                    forwardDistanceSinceLap > LengthMetres * .85f)
                {
                    CompletedLaps++;
                    forwardDistanceSinceLap = 0;
                }
            }
            else if (Mathf.Abs(delta) >= 15)
            {
                // A reset or other teleport cannot grant a shortcut lap.
                forwardDistanceSinceLap = 0;
            }
            previousDistance = nearest;
        }
    }
}
