using Alabama.Driving;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.Rendering;

namespace Alabama.Tests
{
    public sealed class NfsWorldRenderSettingsTests
    {
        [Test]
        public void SceneDiffuseFillRestoresThePreviousProbe()
        {
            var original = RenderSettings.ambientProbe;
            var originalMode = RenderSettings.ambientMode;
            var owner = new GameObject("test diffuse fill");
            try
            {
                var settings = owner.AddComponent<NfsWorldRenderSettings>();
                var colour = new Color(.52f, .56f, .62f);
                settings.ConfigureDiffuseFill(colour);
                settings.Apply();
                var expected = new SphericalHarmonicsL2(); expected.AddAmbientLight(colour);
                Assert.That(RenderSettings.ambientProbe, Is.EqualTo(expected));
                Assert.That(RenderSettings.ambientMode, Is.EqualTo(AmbientMode.Custom));
                settings.Apply(); // A repeat must not replace the saved previous state.
                settings.Restore();
                Assert.That(RenderSettings.ambientProbe, Is.EqualTo(original));
                Assert.That(RenderSettings.ambientMode, Is.EqualTo(originalMode));
            }
            finally { Object.DestroyImmediate(owner); RenderSettings.ambientMode = originalMode; RenderSettings.ambientProbe = original; }
        }

        [Test]
        public void SceneDiffuseFillDoesNotOverwriteAReplacementOnRestore()
        {
            var original = RenderSettings.ambientProbe;
            var originalMode = RenderSettings.ambientMode;
            var owner = new GameObject("test diffuse fill replacement");
            try
            {
                var settings = owner.AddComponent<NfsWorldRenderSettings>();
                settings.ConfigureDiffuseFill(Color.gray); settings.Apply();
                var replacement = new SphericalHarmonicsL2(); replacement.AddAmbientLight(Color.blue);
                RenderSettings.ambientProbe = replacement;
                settings.Restore();
                Assert.That(RenderSettings.ambientProbe, Is.EqualTo(replacement));
            }
            finally { Object.DestroyImmediate(owner); RenderSettings.ambientMode = originalMode; RenderSettings.ambientProbe = original; }
        }
    }
}
