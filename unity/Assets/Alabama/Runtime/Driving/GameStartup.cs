using System.Collections;
using Alabama.Districts;
using UnityEngine;
using UnityEngine.SceneManagement;
using Unity.Profiling;

namespace Alabama.Driving
{
    /// <summary>Small entry scene; map dependencies are not serialized into the bootstrap.</summary>
    public sealed class GameStartup : MonoBehaviour
    {
        [SerializeField] private string gameplayScene;
        [SerializeField] private string coreCatalog;
        private static readonly ProfilerMarker CorePreload = new ProfilerMarker("Alabama.Startup.CorePreload");
        public static double ReadySeconds { get; private set; }
        public static double PreloadSeconds { get; private set; }
        private ThreadPriority previousPriority;
        private int previousUploadBudget, previousBuffer;
        private bool settingsOwned;
        public void Configure(string scene, string catalog) { gameplayScene = scene; coreCatalog = catalog; }
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetMetrics() { ReadySeconds = PreloadSeconds = 0; }

        private IEnumerator Start()
        {
            DontDestroyOnLoad(gameObject);
            previousPriority = Application.backgroundLoadingPriority;
            previousUploadBudget = QualitySettings.asyncUploadTimeSlice;
            previousBuffer = QualitySettings.asyncUploadBufferSize;
            settingsOwned = true;
            Application.backgroundLoadingPriority = ThreadPriority.High;
            QualitySettings.asyncUploadTimeSlice = 8;
            QualitySettings.asyncUploadBufferSize = Mathf.Max(64,previousBuffer);
            double began = Time.realtimeSinceStartupAsDouble;
            ResourceRequest preload;
            using (CorePreload.Auto()) preload = Resources.LoadAsync<StartupPreloadCatalog>(coreCatalog);
            yield return preload;
            if (preload.asset == null) { Debug.LogError("Core preload catalog is missing: "+coreCatalog); Restore(); yield break; }
            PreloadSeconds = Time.realtimeSinceStartupAsDouble-began;
            // Keep the tiny entry scene, including explicitly requested review probes.
            // Gameplay remains owned by the host; it becomes the active scene itself.
            var load = SceneManager.LoadSceneAsync(gameplayScene,LoadSceneMode.Additive);
            if (load == null) { Debug.LogError("Gameplay scene is missing: "+gameplayScene); Restore(); yield break; }
            yield return load;
            if (DistrictRuntime.Instance == null)
            { Debug.LogError("Gameplay scene has no district runtime."); Restore(); yield break; }
            while (DistrictRuntime.Instance != null && !DistrictRuntime.Instance.Ready)
            {
                if (DistrictRuntime.Instance.LastFailure != null) { Restore(); yield break; }
                yield return null;
            }
            ReadySeconds = Time.realtimeSinceStartupAsDouble;
            Restore();
            // Keep the larger persistent ring buffer: reducing it reallocates memory during driving.
            Debug.Log($"Startup ready: engine={ReadySeconds:F3}s, async core={PreloadSeconds:F3}s, district={DistrictRuntime.Instance.InitialSceneSeconds:F3}s, scenery={DistrictRuntime.Instance.InitialVisualsSeconds:F3}s.");
            Destroy(gameObject);
        }

        private void Restore()
        {
            if (!settingsOwned) return;
            Application.backgroundLoadingPriority = previousPriority;
            QualitySettings.asyncUploadTimeSlice = previousUploadBudget;
            settingsOwned = false;
        }
        private void OnDestroy() => Restore();
    }
}
