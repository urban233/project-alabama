#if UNITY_EDITOR
using System.Collections;
using System.Linq;
using Alabama.Districts;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Alabama.Tests
{
    public sealed class DistrictConnectionGateTests
    {
        private DistrictRuntime runtime;
        private EditorBuildSettingsScene[] previous;
        [UnityTest]
        public IEnumerator VerifiedGateRequiresReciprocalContentAndClosesOnUnload()
        {
            const string root = "Assets/Alabama/Art/Maps/NfsWorld/Runtime/";
            previous = EditorBuildSettings.scenes;
            EditorBuildSettings.scenes = previous.Concat(new[] { new EditorBuildSettingsScene(root + "FixtureA.unity", true),
                new EditorBuildSettingsScene(root + "FixtureB.unity", true) }).GroupBy(s => s.path).Select(g => g.Last()).ToArray();
            try
            {
                yield return EditorSceneManager.LoadSceneInPlayMode(root + "FixtureRuntime.unity", new LoadSceneParameters(LoadSceneMode.Single));
                runtime = DistrictRuntime.Instance;
                float deadline = Time.realtimeSinceStartup + 30;
                while (!runtime.Ready && Time.realtimeSinceStartup < deadline) yield return null;
                Assert.That(runtime.Ready, Is.True, runtime.LastFailure);
                var a = runtime.LoadedDistricts.Single();
                var wallA = new GameObject("A verified gate"); SceneManager.MoveGameObjectToScene(wallA, a.gameObject.scene);
                var seam = new Bounds(Vector3.zero, Vector3.one * 10);
                a.Configure(a.Id, a.Bounds, a.RecoveryPoses, new[] { new DistrictConnection { id = "a-b", neighbourId = "fixture-b",
                    reciprocalId = "b-a", collisionOwner = "fixture-a", seamBounds = seam, outward = Vector3.forward, seamVerified = true, closure = wallA } });
                var gateA = a.gameObject.AddComponent<DistrictConnectionGate>();
                Assert.That(wallA.activeSelf, Is.True, "Absent neighbour must keep the verified gate closed.");
                yield return runtime.LoadDistrict(root + "FixtureB.unity");
                Assert.That(runtime.LastFailure, Is.Null);
                Assert.That(wallA.activeSelf, Is.True, "Unpaired content must not open a connection.");
                var b = runtime.LoadedDistricts.Single(d => d.Id == "fixture-b");
                var wallB = new GameObject("B verified gate"); SceneManager.MoveGameObjectToScene(wallB, b.gameObject.scene);
                b.Configure(b.Id, b.Bounds, b.RecoveryPoses, new[] { new DistrictConnection { id = "b-a", neighbourId = a.Id,
                    reciprocalId = "a-b", collisionOwner = "fixture-a", seamBounds = seam, outward = Vector3.back, seamVerified = true, closure = wallB } });
                var gateB = b.gameObject.AddComponent<DistrictConnectionGate>(); gateA.Refresh(); gateB.Refresh();
                Assert.That(wallA.activeSelf || wallB.activeSelf, Is.False, "Matching verified reciprocal content opens both gates.");
                yield return runtime.UnloadDistrict("fixture-b");
                Assert.That(runtime.LastFailure, Is.Null);
                Assert.That(wallA.activeSelf, Is.True, "Unloading the neighbour must close the retained district's gate.");
            }
            finally { EditorBuildSettings.scenes = previous; }
        }

        [UnityTearDown]
        public IEnumerator Cleanup()
        {
            if (previous != null) EditorBuildSettings.scenes = previous;
            SceneManager.SetActiveScene(SceneManager.CreateScene("Gate test cleanup"));
            if (runtime == null) yield break;
            foreach (var content in runtime.LoadedDistricts.ToArray()) yield return SceneManager.UnloadSceneAsync(content.gameObject.scene);
            yield return SceneManager.UnloadSceneAsync(runtime.gameObject.scene);
        }
    }
}
#endif
