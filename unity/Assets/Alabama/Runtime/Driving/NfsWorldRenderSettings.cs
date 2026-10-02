using UnityEngine;
using UnityEngine.Rendering;

namespace Alabama.Driving
{
    /// <summary>Scene-local pipeline override; restores the previous pipeline on exit.</summary>
    [DefaultExecutionOrder(-20)]
    public sealed class NfsWorldRenderSettings : MonoBehaviour
    {
        [SerializeField] private RenderPipelineAsset pipeline;
        [SerializeField] private int targetFrameRate;
        private RenderPipelineAsset previous;
        private int previousAntialiasing;
        private int previousFrameRate;
        private bool frameRateApplied;
        private bool applied;

        public void Configure(RenderPipelineAsset value, int frameRate = 0)
        { pipeline = value; targetFrameRate = frameRate; }
        private void OnEnable() { if (Application.isPlaying) Apply(); }

        private void Start()
        {
            // Bootstrap finishes Awake first. Benchmarks can then override the
            // map's cap in their ordinary-order Start without changing assets.
            if (targetFrameRate <= 0) return;
            previousFrameRate = Application.targetFrameRate;
            Application.targetFrameRate = targetFrameRate;
            frameRateApplied = true;
        }

        public void Apply()
        {
            if (applied || pipeline == null) return;
            previous = QualitySettings.renderPipeline;
            previousAntialiasing = QualitySettings.antiAliasing;
            QualitySettings.renderPipeline = pipeline;
            applied = true;
        }

        public void Restore()
        {
            if (frameRateApplied)
            {
                if (Application.targetFrameRate == targetFrameRate) Application.targetFrameRate = previousFrameRate;
                frameRateApplied = false;
            }
            if (!applied) return;
            if (QualitySettings.renderPipeline == pipeline)
            {
                QualitySettings.renderPipeline = previous;
                // URP synchronizes this global value when it creates the local
                // pipeline. Restore it too, including during editor captures.
                QualitySettings.antiAliasing = previousAntialiasing;
            }
            applied = false;
        }

        private void OnDisable() => Restore();
    }
}
