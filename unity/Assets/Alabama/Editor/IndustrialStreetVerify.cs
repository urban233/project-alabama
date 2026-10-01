using System;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Alabama.Editor
{
    public static class IndustrialStreetVerify
    {
        [MenuItem("Alabama/Street/Verify Review Scene")]
        public static void Run()
        {
            VehicleAssetSetup.Verify();
            IndustrialKitSetup.Verify();
            var scene = EditorSceneManager.OpenScene(IndustrialStreetScene.ScenePath);
            var roots = scene.GetRootGameObjects();
            Require(scene.isLoaded && roots.Length > 220, "Street was not assembled.");
            Require(roots.Any(r => r.name == "E46 review vehicle"), "Review vehicle is absent.");
            Require(roots.Count(r => r.name == "RoadBarrier") >= 80, "Road protection is incomplete.");
            Require(roots.Count(r => r.name.StartsWith("AutumnTree")) == 35, "Autumn planting is incomplete.");
            Require(roots.Count(r => r.name == "WarehouseBay") >= 18, "Factory frontage is incomplete.");
            Require(roots.Count(r => r.name == "BridgeDeck") == 5, "Elevated road is incomplete.");
            var camera = Camera.main;
            Require(camera != null && camera.fieldOfView >= 65 && camera.transform.position.z < 0, "Chase camera is misconfigured.");
            Require(roots.Any(r => r.name == "Road surface" && r.transform.localScale.z >= 220), "Road length is outside demo scope.");
            Debug.Log($"Industrial street verified: {roots.Length} scene roots, 220+ metres, 35 autumn trees.");
        }

        private static void Require(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException(message);
        }
    }
}
