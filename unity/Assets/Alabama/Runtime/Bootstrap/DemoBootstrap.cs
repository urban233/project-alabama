using System;
using UnityEngine;

namespace Alabama.Bootstrap
{
    [DisallowMultipleComponent]
    public sealed class DemoBootstrap : MonoBehaviour
    {
        [SerializeField] private DemoSettings settings;

        public DemoSettings Settings => settings;

        private void Awake()
        {
            if (settings == null)
            {
                throw new InvalidOperationException("DemoBootstrap needs a DemoSettings asset.");
            }

            settings.Validate();
            Time.fixedDeltaTime = 1f / settings.PhysicsRate;
            QualitySettings.vSyncCount = 0;
            Application.targetFrameRate = settings.TargetFrameRate;

            // Editor and batch-mode test sessions should not resize a user's display.
            if (!Application.isEditor && !Application.isBatchMode)
            {
                Screen.SetResolution(settings.ScreenWidth, settings.ScreenHeight, FullScreenMode.Windowed);
            }
        }
    }
}
