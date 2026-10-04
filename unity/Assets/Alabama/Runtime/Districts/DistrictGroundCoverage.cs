using UnityEngine;
using Alabama.Driving;

namespace Alabama.Districts
{
    /// <summary>Visible ground used by the boundary query, independent of driving collision.</summary>
    [DisallowMultipleComponent]
    public sealed class DistrictGroundCoverage : MonoBehaviour
    {
        private void Awake()
        {
            // Standalone review scenes also own a car, without the additive runtime.
            foreach (var car in FindObjectsByType<ArcadeCarController>(FindObjectsSortMode.None))
                IgnoreVehicle(car.GetComponentsInChildren<Collider>());
        }

        public void IgnoreVehicle(Collider[] vehicle)
        {
            foreach (var ground in GetComponentsInChildren<Collider>())
                foreach (var chassis in vehicle)
                    Physics.IgnoreCollision(ground, chassis);
        }
    }
}
