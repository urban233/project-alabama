using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace Alabama.Driving
{
    /// <summary>Scene-local pipeline override; restores the previous pipeline on exit.</summary>
    [DefaultExecutionOrder(-20)]
    public sealed class NfsWorldRenderSettings : MonoBehaviour
    {
        [SerializeField] private RenderPipelineAsset pipeline;
        [SerializeField] private int targetFrameRate;
        [SerializeField] private bool useDiffuseFill;
        [SerializeField] private Color diffuseFill;
        private SphericalHarmonicsL2 previousProbe, appliedProbe;
        private bool probeApplied;
        private AmbientMode previousAmbientMode;
        private RenderPipelineAsset previous;
        private int previousAntialiasing;
        private int previousFrameRate;
        private bool frameRateApplied;
        private bool applied;
        private AutomaticPerformance automaticPerformance;

        public void Configure(RenderPipelineAsset value, int frameRate = 0)
        { pipeline = value; targetFrameRate = frameRate; }
        public void ConfigureDiffuseFill(Color colour) { useDiffuseFill = true; diffuseFill = colour; }
        public bool UsesDiffuseFill(Color colour) => useDiffuseFill && diffuseFill == colour;
        public bool DiffuseFillConfigured => useDiffuseFill;
        public bool DiffuseFillActive => probeApplied && RenderSettings.ambientProbe.Equals(appliedProbe);
        private void OnEnable() { if (Application.isPlaying) Apply(); }

        private void Start()
        {
            // Bootstrap finishes Awake first. Benchmarks can then override the
            // map's cap in their ordinary-order Start without changing assets.
            if (targetFrameRate > 0)
            {
                previousFrameRate = Application.targetFrameRate;
                Application.targetFrameRate = targetFrameRate;
                frameRateApplied = true;
            }
            if (!Application.isEditor && AutomaticPerformance.AllowsAutomatic(System.Environment.GetCommandLineArgs()) &&
                pipeline is UnityEngine.Rendering.Universal.UniversalRenderPipelineAsset urp)
            {
                automaticPerformance = gameObject.AddComponent<AutomaticPerformance>();
                automaticPerformance.Initialize(urp);
            }
        }

        public void Apply()
        {
            if (useDiffuseFill && !probeApplied)
            {
                previousProbe = RenderSettings.ambientProbe;
                previousAmbientMode = RenderSettings.ambientMode;
                appliedProbe = new SphericalHarmonicsL2();
                appliedProbe.AddAmbientLight(diffuseFill);
                // Trilight/Skybox regeneration can replace a manually assigned
                // probe after loading. Custom mode gives this host ownership.
                RenderSettings.ambientMode = AmbientMode.Custom;
                RenderSettings.ambientProbe = appliedProbe;
                probeApplied = true;
                SceneManager.sceneLoaded += SceneLoaded;
                SceneManager.activeSceneChanged += ActiveSceneChanged;
            }
            if (applied || pipeline == null) return;
            previous = QualitySettings.renderPipeline;
            previousAntialiasing = QualitySettings.antiAliasing;
            QualitySettings.renderPipeline = pipeline;
            applied = true;
        }

        private void RefreshDiffuseFill()
        {
            if (!probeApplied || SceneManager.GetActiveScene() != gameObject.scene) return;
            RenderSettings.ambientMode = AmbientMode.Custom;
            RenderSettings.ambientProbe = appliedProbe;
        }
        private void SceneLoaded(Scene scene, LoadSceneMode mode) => RefreshDiffuseFill();
        private void ActiveSceneChanged(Scene previousScene, Scene nextScene) => RefreshDiffuseFill();

        public void Restore()
        {
            if (automaticPerformance != null) automaticPerformance.Restore();
            if (probeApplied)
            {
                SceneManager.sceneLoaded -= SceneLoaded;
                SceneManager.activeSceneChanged -= ActiveSceneChanged;
                if (RenderSettings.ambientProbe.Equals(appliedProbe))
                {
                    RenderSettings.ambientProbe = previousProbe;
                    if (RenderSettings.ambientMode == AmbientMode.Custom) RenderSettings.ambientMode = previousAmbientMode;
                }
                probeApplied = false;
            }
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
