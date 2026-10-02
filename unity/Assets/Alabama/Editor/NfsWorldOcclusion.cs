using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Alabama.Editor
{
    /// <summary>Bake visibility against opaque source surfaces without changing their geometry.</summary>
    public static class NfsWorldOcclusion
    {
        private static double deadline;

        public static void Run()
        {
            var scene = EditorSceneManager.OpenScene(NfsWorldArtPass.ScenePath);
            var root = GameObject.Find("Optimized district visuals");
            NfsWorldSetup.Require(root != null, "Generate the art scene first.");
            foreach (var renderer in root.GetComponentsInChildren<MeshRenderer>())
            {
                var flags = GameObjectUtility.GetStaticEditorFlags(renderer.gameObject);
                flags |= StaticEditorFlags.OccludeeStatic;
                if (renderer.sharedMaterial.IsKeywordEnabled("_ALPHATEST_ON")) flags &= ~StaticEditorFlags.OccluderStatic;
                else flags |= StaticEditorFlags.OccluderStatic;
                GameObjectUtility.SetStaticEditorFlags(renderer.gameObject, flags);
            }
            var previous = GameObject.Find("District occlusion view volume");
            if (previous != null) UnityEngine.Object.DestroyImmediate(previous);
            var roads = GameObject.Find("RoadsPhysical").GetComponentsInChildren<MeshCollider>();
            var bounds = roads[0].bounds;
            foreach (var road in roads.Skip(1)) bounds.Encapsulate(road.bounds);
            var volume = new GameObject("District occlusion view volume").AddComponent<OcclusionArea>();
            volume.center = bounds.center;
            volume.size = bounds.size + new Vector3(32, 30, 32);
            var volumeData = new SerializedObject(volume);
            var viewVolume = volumeData.FindProperty("m_IsViewVolume");
            NfsWorldSetup.Require(viewVolume != null, "Occlusion view-volume field is unavailable.");
            viewVolume.boolValue = true;
            volumeData.ApplyModifiedPropertiesWithoutUndo();
            Camera.main.useOcclusionCulling = true;
            EditorSceneManager.SaveScene(scene);
            StaticOcclusionCulling.smallestOccluder = 10;
            StaticOcclusionCulling.smallestHole = 4;
            StaticOcclusionCulling.backfaceThreshold = 100;
            NfsWorldSetup.Require(StaticOcclusionCulling.Compute(), "Occlusion bake did not start.");
            deadline = EditorApplication.timeSinceStartup + 1200;
            EditorApplication.update += Update;
        }

        private static void Update()
        {
            if (StaticOcclusionCulling.isRunning && EditorApplication.timeSinceStartup < deadline) return;
            EditorApplication.update -= Update;
            if (StaticOcclusionCulling.isRunning)
            {
                StaticOcclusionCulling.Cancel();
                Debug.LogError("District occlusion bake exceeded twenty minutes.");
                if (Application.isBatchMode) EditorApplication.Exit(1);
                return;
            }
            EditorSceneManager.SaveScene(UnityEngine.SceneManagement.SceneManager.GetActiveScene());
            AssetDatabase.SaveAssets();
            File.WriteAllText(Path.GetFullPath(Path.Combine(Application.dataPath, "../../artifacts/NfsWorld/occlusion.json")),
                "{\"smallestOccluder\":10,\"smallestHole\":4,\"backfaceThreshold\":100,\"alphaCardsAreOccluders\":false,\"sourceGeometryChanged\":false}\n");
            Debug.Log("District occlusion bake completed.");
            if (Application.isBatchMode) EditorApplication.Exit(0);
        }
    }
}
