using System;
using System.IO;
using System.Linq;
using Alabama.Driving;
using UnityEditor;
using UnityEngine;

namespace Alabama.Editor
{
    /// <summary>Update the existing prefab identities without rebuilding authored scenes.</summary>
    public static class SuppliedCarSetup
    {
        [Serializable] private sealed class Report { public float wheelRadius; public int triangles; }

        private static Report LoadReport()
        {
            string path = Path.GetFullPath(Path.Combine(Application.dataPath,
                "../../source-art/vehicles/supplied-e46/adaptation-report.json"));
            return JsonUtility.FromJson<Report>(File.ReadAllText(path));
        }

        [MenuItem("Alabama/Vehicle/Install Supplied Car")]
        public static void Install()
        {
            var report = LoadReport();
            if (report.wheelRadius < .25f || report.wheelRadius > .4f)
                throw new InvalidOperationException("The supplied car wheel measurement is invalid.");
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            VehicleAssetSetup.SetupAssets(false);
            var car = PrefabUtility.LoadPrefabContents(HandlingCourseSetup.PrefabPath);
            try
            {
                int triangles = car.GetComponentsInChildren<MeshFilter>().Sum(m => m.sharedMesh.triangles.Length / 3);
                // FBX conversion/import can drop degenerate source faces. Bound
                // that loss while rejecting the larger, superseded car mesh.
                if (triangles > report.triangles || triangles < report.triangles * .99f)
                    throw new InvalidOperationException("Drive prefab is not using the measured replacement geometry.");
                var controller = car.GetComponent<ArcadeCarController>();
                if (controller == null) throw new InvalidOperationException("Existing drive controller missing.");
                var serialized = new SerializedObject(controller);
                string[] names = { "Wheel_FL", "Wheel_FR", "Wheel_RL", "Wheel_RR" };
                string[] fields = { "frontLeft", "frontRight", "rearLeft", "rearRight" };
                for (int i = 0; i < names.Length; i++)
                {
                    var visual = car.GetComponentsInChildren<Transform>().Single(t => t.name == names[i]);
                    var physics = car.GetComponentsInChildren<WheelCollider>().Single(w => w.name == "Physics " + names[i]);
                    physics.transform.localPosition = car.transform.InverseTransformPoint(visual.position);
                    physics.radius = report.wheelRadius;
                    serialized.FindProperty(fields[i]).objectReferenceValue = physics;
                    serialized.FindProperty(fields[i] + "Visual").objectReferenceValue = visual;
                }
                serialized.ApplyModifiedPropertiesWithoutUndo();
                PrefabUtility.SaveAsPrefabAsset(car, HandlingCourseSetup.PrefabPath);
            }
            finally { PrefabUtility.UnloadPrefabContents(car); }
            var tuning = AssetDatabase.LoadAssetAtPath<VehicleTuning>(HandlingCourseSetup.TuningPath);
            var tuningData = new SerializedObject(tuning);
            tuningData.FindProperty("wheelRadius").floatValue = report.wheelRadius;
            tuningData.ApplyModifiedPropertiesWithoutUndo();
            AssetDatabase.SaveAssets();
            Verify();
        }

        public static void Verify()
        {
            VehicleAssetSetup.Verify();
            var car = PrefabUtility.LoadPrefabContents(HandlingCourseSetup.PrefabPath);
            try
            {
                var report = LoadReport();
                int triangles = car.GetComponentsInChildren<MeshFilter>().Sum(m => m.sharedMesh.triangles.Length / 3);
                if (triangles > report.triangles || triangles < report.triangles * .99f)
                    throw new InvalidOperationException("Drive prefab is not using the measured replacement geometry.");
                var controller = car.GetComponent<ArcadeCarController>();
                var serialized = new SerializedObject(controller);
                foreach (string field in new[] { "frontLeft", "frontRight", "rearLeft", "rearRight" })
                {
                    var physics = serialized.FindProperty(field).objectReferenceValue as WheelCollider;
                    var visual = serialized.FindProperty(field + "Visual").objectReferenceValue as Transform;
                    if (physics == null || visual == null || Vector3.Distance(physics.transform.position, visual.position) > .001f)
                        throw new InvalidOperationException("Wheel physics and replacement visual disagree: " + field);
                    if (Mathf.Abs(physics.radius - controller.Tuning.WheelRadius) > .001f)
                        throw new InvalidOperationException("Wheel radius and tuning disagree: " + field);
                    var bounds = visual.GetComponentsInChildren<Renderer>().First().bounds;
                    if (Mathf.Abs(bounds.size.y * .5f - controller.Tuning.WheelRadius) > .025f)
                        throw new InvalidOperationException("Wheel geometry and physics radius disagree: " + field);
                }
                Debug.Log("Supplied car drive prefab: wheel references, measured radius and axle positions verified.");
            }
            finally { PrefabUtility.UnloadPrefabContents(car); }
        }
    }
}
