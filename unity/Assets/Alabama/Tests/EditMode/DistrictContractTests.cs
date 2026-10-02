using Alabama.Districts;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Alabama.Tests
{
    public sealed class DistrictContractTests
    {
        [Test]
        public void ContractRejectsIndependentOriginDuplicateRecoveryAndUnclosedExits()
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var data = new GameObject("District").AddComponent<DistrictContent>();
            var pose = new DistrictRecoveryPose { id = "spawn", position = Vector3.up * .24f };
            var bounds = new Bounds(Vector3.zero, Vector3.one * 100);
            data.Configure("test", bounds, new[] { pose });
            Assert.That(data.ValidateContract(), Is.Null);
            data.transform.position = Vector3.right;
            Assert.That(data.ValidateContract(), Does.Contain("shared frame"));
            data.transform.position = Vector3.zero;
            data.Configure("test", bounds, new[] { pose, pose });
            Assert.That(data.ValidateContract(), Does.Contain("recovery"));
            data.Configure("test", bounds, new[] { pose }, new[] { new DistrictConnection
            { id = "exit", neighbourId = "other", collisionOwner = "test", outward = Vector3.forward,
                seamBounds = new Bounds(Vector3.zero, Vector3.one * 10) } });
            Assert.That(data.ValidateContract(), Does.Contain("closure"));
            Object.DestroyImmediate(data.gameObject);
        }

        [Test]
        public void RecoveryCannotBorrowSupportFromAnotherSceneOrDifferentElevation()
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var data = new GameObject("District").AddComponent<DistrictContent>();
            var pose = new DistrictRecoveryPose { id = "spawn", position = Vector3.up * .24f };
            data.Configure("test", new Bounds(Vector3.zero, Vector3.one * 100), new[] { pose });
            const string directory = "Assets/Alabama/Art/Maps/NfsWorld/Runtime";
            System.IO.Directory.CreateDirectory(directory);
            string scratch = directory + "/RecoveryTest-" + System.Guid.NewGuid().ToString("N") + ".unity";
            // The Editor requires the first scene to be saved before an additive
            // scene is created. This unique, ignored fixture is deleted in finally.
            EditorSceneManager.SaveScene(scene, scratch);
            try
            {
                var other = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
                var road = GameObject.CreatePrimitive(PrimitiveType.Cube);
                UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(road, other);
                road.transform.position = Vector3.down * .2f; road.transform.localScale = new Vector3(20, .4f, 20);
                Assert.That(data.Supports(pose), Is.False);
                UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(road, scene);
                Assert.That(data.Supports(pose), Is.True);
                road.transform.position += Vector3.up * 5;
                Assert.That(data.Supports(pose), Is.False);
                Object.DestroyImmediate(data.gameObject); Object.DestroyImmediate(road);
                EditorSceneManager.CloseScene(other, true);
            }
            finally
            {
                EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
                UnityEditor.AssetDatabase.DeleteAsset(scratch);
            }
        }
    }
}
