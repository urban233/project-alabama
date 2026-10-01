using System;
using UnityEngine;

namespace Alabama.Bootstrap
{
    [CreateAssetMenu(menuName = "Alabama/Demo Settings", fileName = "DemoSettings")]
    public sealed class DemoSettings : ScriptableObject
    {
        [SerializeField, Min(1)] private int screenWidth = 1920;
        [SerializeField, Min(1)] private int screenHeight = 1080;
        [SerializeField, Min(1)] private int targetFrameRate = 60;
        [SerializeField, Range(30, 240)] private int physicsRate = 120;

        public int ScreenWidth => screenWidth;
        public int ScreenHeight => screenHeight;
        public int TargetFrameRate => targetFrameRate;
        public int PhysicsRate => physicsRate;

        public void Validate()
        {
            if (screenWidth <= 0 || screenHeight <= 0)
            {
                throw new InvalidOperationException("Demo resolution must have positive dimensions.");
            }
            if (targetFrameRate <= 0)
            {
                throw new InvalidOperationException("The normal demo frame cap must be positive.");
            }
            if (physicsRate < 30 || physicsRate > 240)
            {
                throw new InvalidOperationException("The physics rate must be between 30 and 240 Hz.");
            }
        }
    }
}
