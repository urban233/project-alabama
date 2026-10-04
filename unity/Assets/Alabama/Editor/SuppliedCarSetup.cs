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
        [Serializable] private sealed class Report
        {
            public float wheelRadius;
            public int triangles;
            public int bodyTriangles;
            public int paintTriangles;
            public int geometryRevision;
        }

        private static void VerifyChassis(GameObject car, Report report)
        {
            if (report.geometryRevision != 2 || report.bodyTriangles > 10000 || report.paintTriangles > 3500)
                throw new InvalidOperationException("Restore the faceted chassis revision of the private base pack.");
            var body = car.GetComponentsInChildren<MeshFilter>().Single(m => m.name == "Body_Mesh");
            var mesh = body.sharedMesh;
            var materials = body.GetComponent<MeshRenderer>().sharedMaterials;
            int paintSlot = Array.FindIndex(materials, m => m.name == "E46_Paint");
            if (paintSlot < 0 || mesh.triangles.Length / 3 > 10000)
                throw new InvalidOperationException("Replacement chassis material or geometry budget is invalid.");
            var indices = mesh.GetTriangles(paintSlot);
            if (indices.Length / 3 > 3500 || Mathf.Abs(indices.Length / 3 - report.paintTriangles) > 10)
                throw new InvalidOperationException("Painted chassis does not match the reduced export.");
            var vertices = mesh.vertices;
            var normals = mesh.normals;
            int faces = 0, flatFaces = 0;
            for (int i = 0; i < indices.Length; i += 3)
            {
                int a = indices[i], b = indices[i + 1], c = indices[i + 2];
                var face = Vector3.Cross(vertices[b] - vertices[a], vertices[c] - vertices[a]);
                // Mesh-local coordinates retain FBX unit conversion. Unity's
                // Vector3.Normalize cutoff can zero a valid small triangle;
                // normalize explicitly before comparing imported normals.
                if (face.sqrMagnitude < 1e-24f) continue;
                face /= Mathf.Sqrt(face.sqrMagnitude);
                faces++;
                // Require constant vertex normals instead of interpolation;
                // allow a small alignment error in tiny imported triangles.
                if (Vector3.Dot(normals[a], normals[b]) > .999f && Vector3.Dot(normals[a], normals[c]) > .999f &&
                    Vector3.Dot(face, normals[a]) > .95f) flatFaces++;
            }
            if (faces == 0 || flatFaces < faces * .98f)
                throw new InvalidOperationException($"Painted chassis must retain flat panel normals: {flatFaces}/{faces} faces.");
            Debug.Log($"Faceted chassis: {mesh.triangles.Length / 3} body triangles, {indices.Length / 3} painted triangles, " +
                $"{flatFaces}/{faces} painted faces retain flat normals.");
        }

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
                VerifyChassis(car, report);
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
                VerifyChassis(car, report);
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
