using System.Collections;
using Alabama.Bootstrap;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Alabama.Tests
{
    public sealed class FoundationSceneTests
    {
        private float previousFixedDeltaTime;
        private int previousTargetFrameRate;
        private int previousVSyncCount;

        [SetUp]
        public void SaveGlobalSettings()
        {
            previousFixedDeltaTime = Time.fixedDeltaTime;
            previousTargetFrameRate = Application.targetFrameRate;
            previousVSyncCount = QualitySettings.vSyncCount;
        }

        [UnityTearDown]
        public IEnumerator RestoreGlobalSettings()
        {
            SceneManager.CreateScene("TestCleanup");
            var foundation = SceneManager.GetSceneByName("Foundation");
            if (foundation.IsValid() && foundation.isLoaded)
            {
                yield return SceneManager.UnloadSceneAsync(foundation);
            }
            Time.fixedDeltaTime = previousFixedDeltaTime;
            Application.targetFrameRate = previousTargetFrameRate;
            QualitySettings.vSyncCount = previousVSyncCount;
        }

        [UnityTest]
        public IEnumerator SavedSceneBootsAndCalibrationBodySettlesOnFloor()
        {
            yield return ReviewSceneLoader.Load("Foundation");
            var bootstrap = UnityEngine.Object.FindFirstObjectByType<DemoBootstrap>();
            Assert.That(bootstrap, Is.Not.Null);
            Assert.That(bootstrap.Settings, Is.Not.Null);
            Assert.That(Time.fixedDeltaTime, Is.EqualTo(1f / bootstrap.Settings.PhysicsRate).Within(0.000001f));
            Assert.That(Application.targetFrameRate, Is.EqualTo(bootstrap.Settings.TargetFrameRate));

            var block = GameObject.Find("CalibrationBody");
            Assert.That(block, Is.Not.Null);
            var body = block.GetComponent<Rigidbody>();
            Assert.That(body, Is.Not.Null);
            var initialHeight = body.position.y;
            yield return new WaitForSeconds(2f);
            Assert.That(body.position.y, Is.LessThan(initialHeight - 0.5f), "The body must actually fall.");
            Assert.That(block.GetComponentInChildren<Renderer>().bounds.min.y, Is.EqualTo(0f).Within(0.05f), "The imported body should rest on the floor.");
            Assert.That(body.linearVelocity.sqrMagnitude, Is.LessThan(0.01f), "The contact should settle.");
        }
    }
}
