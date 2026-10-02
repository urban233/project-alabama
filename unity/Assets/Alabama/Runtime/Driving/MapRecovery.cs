using UnityEngine;

namespace Alabama.Driving
{
    /// <summary>Recover a prototype vehicle after falling through an unimported district exit.</summary>
    [DisallowMultipleComponent]
    public sealed class MapRecovery : MonoBehaviour
    {
        [SerializeField] private ArcadeCarController controller;
        [SerializeField] private float minimumHeight = -80;

        private void FixedUpdate()
        {
            if (controller != null && controller.Body.position.y < minimumHeight) controller.ResetToSpawn();
        }
    }
}
