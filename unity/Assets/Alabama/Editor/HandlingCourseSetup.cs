using System;
using System.Linq;
using Alabama.Driving;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Alabama.Editor
{
    /// <summary>Builds a drive prefab and a separate test scene from the approved visual corridor.</summary>
    public static class HandlingCourseSetup
    {
        public const string ScenePath = "Assets/Alabama/Scenes/HandlingCourse.unity";
        public const string TuningPath = "Assets/Alabama/Settings/E46Tuning.asset";
        public const string PrefabPath = VehicleAssetSetup.DirectoryPath + "/E46_Drive.prefab";

        [MenuItem("Alabama/Handling/Set Up")]
        public static void Run()
        {
            VehicleAssetSetup.Verify();
            IndustrialKitSetup.Verify();
            var tuning = AssetDatabase.LoadAssetAtPath<VehicleTuning>(TuningPath);
            if (tuning == null)
            {
                tuning = ScriptableObject.CreateInstance<VehicleTuning>();
                AssetDatabase.CreateAsset(tuning, TuningPath);
            }
            tuning.Validate();
            var source = AssetDatabase.LoadAssetAtPath<GameObject>(VehicleAssetSetup.PrefabPath);
            var car = (GameObject)PrefabUtility.InstantiatePrefab(source);
            try
            {
                car.name = "E46_Drive";
                car.AddComponent<Rigidbody>();
                var wheelNames = new[] { "Wheel_FL", "Wheel_FR", "Wheel_RL", "Wheel_RR" };
                var wheels = new WheelCollider[4];
                var visuals = new Transform[4];
                for (int i = 0; i < wheelNames.Length; i++)
                {
                    visuals[i] = car.GetComponentsInChildren<Transform>().Single(t => t.name == wheelNames[i]);
                    var child = new GameObject("Physics " + wheelNames[i]);
                    child.transform.SetParent(car.transform, false);
                    child.transform.localPosition = car.transform.InverseTransformPoint(visuals[i].position);
                    wheels[i] = child.AddComponent<WheelCollider>();
                }
                var controller = car.AddComponent<ArcadeCarController>();
                var serialized = new SerializedObject(controller);
                serialized.FindProperty("tuning").objectReferenceValue = tuning;
                string[] colliderFields = { "frontLeft", "frontRight", "rearLeft", "rearRight" };
                string[] visualFields = { "frontLeftVisual", "frontRightVisual", "rearLeftVisual", "rearRightVisual" };
                for (int i = 0; i < 4; i++)
                {
                    serialized.FindProperty(colliderFields[i]).objectReferenceValue = wheels[i];
                    serialized.FindProperty(visualFields[i]).objectReferenceValue = visuals[i];
                }
                serialized.ApplyModifiedPropertiesWithoutUndo();
                car.AddComponent<VehicleInput>();
                PrefabUtility.SaveAsPrefabAsset(car, PrefabPath);
            }
            finally { UnityEngine.Object.DestroyImmediate(car); }
            AssetDatabase.SaveAssets();

            var scene = EditorSceneManager.OpenScene(IndustrialStreetScene.ScenePath);
            var oldCar = scene.GetRootGameObjects().Single(r => r.name == "E46 review vehicle");
            UnityEngine.Object.DestroyImmediate(oldCar);
            var driveCar = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath));
            driveCar.name = "E46 driver car";
            driveCar.transform.SetPositionAndRotation(new Vector3(-2.3f, .22f, 2), Quaternion.identity);
            var controllerInstance = driveCar.GetComponent<ArcadeCarController>();
            var camera = Camera.main;
            if (camera == null) throw new InvalidOperationException("The street camera is missing.");
            var chase = camera.gameObject.AddComponent<ChaseCamera>();
            var chaseSerialized = new SerializedObject(chase);
            chaseSerialized.FindProperty("target").objectReferenceValue = controllerInstance;
            chaseSerialized.ApplyModifiedPropertiesWithoutUndo();
            var telemetry = new GameObject("Drive telemetry").AddComponent<DriveTelemetry>();
            var telemetrySerialized = new SerializedObject(telemetry);
            telemetrySerialized.FindProperty("target").objectReferenceValue = controllerInstance;
            telemetrySerialized.ApplyModifiedPropertiesWithoutUndo();
            var curb = GameObject.CreatePrimitive(PrimitiveType.Cube);
            curb.name = "Suspension test curb";
            curb.transform.position = new Vector3(7.0f, .075f, 43);
            curb.transform.localScale = new Vector3(.45f, .15f, 15);
            curb.GetComponent<Renderer>().sharedMaterial = AssetDatabase.LoadAssetAtPath<Material>(
                "Assets/Alabama/Art/IndustrialStreet/ShoulderConcrete.mat");
            EditorSceneManager.SaveScene(scene, ScenePath, true);
            if (!EditorBuildSettings.scenes.Any(item => item.path == ScenePath))
                EditorBuildSettings.scenes = EditorBuildSettings.scenes
                    .Concat(new[] { new EditorBuildSettingsScene(ScenePath, true) }).ToArray();
            Verify();
            Debug.Log("Handling course saved: " + ScenePath);
        }

        [MenuItem("Alabama/Handling/Verify")]
        public static void Verify()
        {
            var scene = EditorSceneManager.OpenScene(ScenePath);
            var roots = scene.GetRootGameObjects();
            var car = roots.Single(r => r.name == "E46 driver car");
            var controller = car.GetComponent<ArcadeCarController>();
            Require(controller != null && controller.Tuning != null, "Drive controller/tuning missing.");
            controller.Tuning.Validate();
            Require(car.GetComponent<Rigidbody>() != null && car.GetComponent<VehicleInput>() != null,
                "Rigidbody or device input missing.");
            var wheels = car.GetComponentsInChildren<WheelCollider>();
            Require(wheels.Length == 4 && wheels.All(w => w.transform.localPosition.y > .2f && w.transform.localPosition.y < .5f),
                "Wheel collider scale or axle height is wrong.");
            Require(Camera.main != null && Camera.main.GetComponent<ChaseCamera>() != null,
                "Follow camera missing.");
            Require(roots.Any(r => r.name == "Suspension test curb"), "Curb fixture missing.");
            Debug.Log("Handling course verified: four wheel colliders, drive input, chase camera, curb fixture.");
        }

        private static void Require(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException(message);
        }
    }
}
