using UnityEngine;
using UnityEngine.Rendering;

namespace Alabama.Driving
{
    /// <summary>Scene-local pipeline override; restores the previous pipeline on exit.</summary>
    public sealed class NfsWorldRenderSettings : MonoBehaviour
    {
        [SerializeField] private RenderPipelineAsset pipeline;
        private RenderPipelineAsset previous;
        private int previousAntialiasing;
        private bool applied;

        public void Configure(RenderPipelineAsset value) => pipeline = value;
        private void OnEnable() { if (Application.isPlaying) Apply(); }

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
