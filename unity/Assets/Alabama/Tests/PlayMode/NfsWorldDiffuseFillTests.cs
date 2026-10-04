using System.Collections;
using Alabama.Driving;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Alabama.Tests
{
    public sealed class NfsWorldDiffuseFillTests
    {
        [UnityTest]
        public IEnumerator HostDiffuseFillSurvivesActiveSceneChanges()
        {
            var originalScene = SceneManager.GetActiveScene();
            var host = SceneManager.CreateScene("diffuse-fill host fixture");
            var content = SceneManager.CreateScene("diffuse-fill content fixture");
            SceneManager.SetActiveScene(host);
            var owner = new GameObject("diffuse-fill fixture");
            var settings = owner.AddComponent<NfsWorldRenderSettings>();
            settings.ConfigureDiffuseFill(new Color(.52f, .56f, .62f));
            settings.Apply();
            try
            {
                yield return null;
                Assert.That(settings.DiffuseFillActive, Is.True);
                Assert.That(RenderSettings.ambientMode, Is.EqualTo(AmbientMode.Custom));
                SceneManager.SetActiveScene(content);
                RenderSettings.ambientMode = AmbientMode.Trilight;
                RenderSettings.ambientProbe = new SphericalHarmonicsL2();
                SceneManager.SetActiveScene(host);
                yield return null;
                Assert.That(settings.DiffuseFillActive, Is.True, "Host must reclaim its fill when made active.");
                Assert.That(RenderSettings.ambientMode, Is.EqualTo(AmbientMode.Custom));
                yield return SceneManager.UnloadSceneAsync(content);
                yield return null;
                Assert.That(settings.DiffuseFillActive, Is.True);
            }
            finally
            {
                settings.Restore();
                Object.Destroy(owner);
                SceneManager.SetActiveScene(originalScene);
            }
            yield return SceneManager.UnloadSceneAsync(host);
        }
    }
}
