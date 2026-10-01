using System;
using System.Collections.Generic;
using System.Linq;
using Alabama.Driving;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Alabama.Editor
{
    /// <summary>Builds one closed driving route from authored, uniformly sampled road geometry.</summary>
    public static class DistrictLoopScene
    {
        public const string ScenePath = "Assets/Alabama/Scenes/DistrictLoop.unity";
        private const string ArtPath = "Assets/Alabama/Art/DistrictLoop";
        private const float RoadHalfWidth = 9;
        private static readonly string StreetArt = "Assets/Alabama/Art/IndustrialStreet/";

        [MenuItem("Alabama/District Loop/Set Up")]
        public static void Setup()
        {
            HandlingCourseSetup.Verify();
            System.IO.Directory.CreateDirectory(ArtPath);
            AssetDatabase.Refresh();
            var streetAsphalt = AssetDatabase.LoadAssetAtPath<Material>("Assets/Alabama/Art/VehicleReview/Asphalt.mat");
            var asphalt = AssetDatabase.LoadAssetAtPath<Material>(ArtPath + "/LoopAsphalt.mat");
            if (asphalt == null)
            {
                asphalt = new Material(streetAsphalt);
                AssetDatabase.CreateAsset(asphalt, ArtPath + "/LoopAsphalt.mat");
            }
            asphalt.SetTextureScale("_BaseMap", Vector2.one);
            asphalt.SetTextureScale("_BumpMap", Vector2.one);
            EditorUtility.SetDirty(asphalt);
            var nodes = BuildNodes();
            float routeLength = PolylineLength(nodes);
            Require(routeLength > 900 && routeLength < 940, "Route dimensions changed unexpectedly: " + routeLength);
            AssetDatabase.SaveAssets();

            var scene = EditorSceneManager.OpenScene(HandlingCourseSetup.ScenePath);
            var oldObjects = scene.GetRootGameObjects().Where(r =>
                r.name == "Road surface" || r.name == "Raised shoulder" ||
                r.name == "Broken lane marking" || r.name == "Double yellow centre line" ||
                r.name == "RoadBarrier" || r.name == "ChevronPanel" ||
                r.name == "Further elevated road" || r.name == "Further bridge column").ToArray();
            foreach (var old in oldObjects) UnityEngine.Object.DestroyImmediate(old);
            foreach (var silhouette in scene.GetRootGameObjects().Where(r => r.name == "Distant city silhouette"))
                silhouette.transform.position += Vector3.forward * 90;
            var ground = scene.GetRootGameObjects().Single(r => r.name == "Industrial ground");
            ground.transform.position = new Vector3(100, -.45f, 115);
            ground.transform.localScale = new Vector3(560, .8f, 560);

            var roadMaterial = AssetDatabase.LoadAssetAtPath<Material>(ArtPath + "/LoopAsphalt.mat");
            var shoulderMaterial = AssetDatabase.LoadAssetAtPath<Material>(StreetArt + "ShoulderConcrete.mat");
            var white = AssetDatabase.LoadAssetAtPath<Material>(StreetArt + "WeatheredWhite.mat");
            var yellow = AssetDatabase.LoadAssetAtPath<Material>(StreetArt + "AgedYellow.mat");
            AddMesh("Continuous 917 m road", Ribbon(nodes, -RoadHalfWidth, RoadHalfWidth, .006f),
                "RoadMesh", roadMaterial, true);
            AddMesh("Left shoulder", Ribbon(nodes, -13.25f, -RoadHalfWidth, .01f),
                "LeftShoulder", shoulderMaterial, true);
            AddMesh("Right shoulder", Ribbon(nodes, RoadHalfWidth, 13.25f, .01f),
                "RightShoulder", shoulderMaterial, true);
            foreach (float offset in new[] { -.16f, .16f })
                AddMesh("Continuous centre stripe", Markings(nodes, offset, .10f, false),
                    offset < 0 ? "CentreStripeLeft" : "CentreStripeRight", yellow, false);
            foreach (float offset in new[] { -3.7f, 3.7f })
                AddMesh("Broken lane stripe", Markings(nodes, offset, .12f, true),
                    offset < 0 ? "LaneStripeLeft" : "LaneStripeRight", white, false);

            PopulateRoadside(nodes);
            var car = scene.GetRootGameObjects().Single(r => r.name == "E46 driver car");
            car.transform.SetPositionAndRotation(new Vector3(-2.3f, .22f, 2), Quaternion.identity);
            var tracker = new GameObject("Loop progress").AddComponent<RouteProgress>();
            var serialized = new SerializedObject(tracker);
            serialized.FindProperty("vehicle").objectReferenceValue = car.transform;
            var pointsProperty = serialized.FindProperty("centreline");
            pointsProperty.arraySize = nodes.Count;
            for (int i = 0; i < nodes.Count; i++) pointsProperty.GetArrayElementAtIndex(i).vector3Value = nodes[i];
            serialized.ApplyModifiedPropertiesWithoutUndo();
            var vehicleInput = car.GetComponent<VehicleInput>();
            var inputSerialized = new SerializedObject(vehicleInput);
            inputSerialized.FindProperty("route").objectReferenceValue = tracker;
            inputSerialized.ApplyModifiedPropertiesWithoutUndo();
            var telemetry = scene.GetRootGameObjects().Select(r => r.GetComponent<DriveTelemetry>()).First(t => t != null);
            var telemetrySerialized = new SerializedObject(telemetry);
            telemetrySerialized.FindProperty("route").objectReferenceValue = tracker;
            telemetrySerialized.ApplyModifiedPropertiesWithoutUndo();
            EditorSceneManager.SaveScene(scene, ScenePath, true);
            if (!EditorBuildSettings.scenes.Any(item => item.path == ScenePath))
                EditorBuildSettings.scenes = EditorBuildSettings.scenes
                    .Concat(new[] { new EditorBuildSettingsScene(ScenePath, true) }).ToArray();
            Verify();
            Debug.Log($"District loop saved: {routeLength:0.0} m, {nodes.Count} centreline points.");
        }

        [MenuItem("Alabama/District Loop/Verify")]
        public static void Verify()
        {
            var scene = EditorSceneManager.OpenScene(ScenePath);
            var roots = scene.GetRootGameObjects();
            Require(roots.Count(r => r.name == "RoadBarrier") >= 220, "Roadside barriers are incomplete.");
            var road = roots.Single(r => r.name == "Continuous 917 m road");
            Require(road.GetComponent<MeshCollider>() != null && road.GetComponent<MeshFilter>().sharedMesh.vertexCount >= 300,
                "The continuous road mesh/collision surface is missing.");
            var route = roots.Select(r => r.GetComponent<RouteProgress>()).FirstOrDefault(r => r != null);
            Require(route != null && route.PointCount >= 150, "Closed centreline is missing.");
            var car = roots.Single(r => r.name == "E46 driver car");
            Require(new SerializedObject(car.GetComponent<VehicleInput>()).FindProperty("route").objectReferenceValue == route,
                "Reset input is not linked to lap progress.");
            Debug.Log($"District loop verified: {route.PointCount} route points, {roots.Length} scene roots.");
        }

        private static List<Vector3> BuildNodes()
        {
            var points = new List<Vector3> { new Vector3(0, 0, 0) };
            Line(points, new Vector3(0, 0, 230));
            Arc(points, new Vector2(25, 230), 180, 90);
            Line(points, new Vector3(175, 0, 255));
            Arc(points, new Vector2(175, 230), 90, 0);
            Line(points, new Vector3(200, 0, 0));
            Arc(points, new Vector2(175, 0), 0, -90);
            Line(points, new Vector3(25, 0, -25));
            Arc(points, new Vector2(25, 0), -90, -180);
            return points;
        }

        private static void Line(List<Vector3> points, Vector3 end)
        {
            var start = points[^1];
            int steps = Mathf.CeilToInt(Vector3.Distance(start, end) / 4);
            for (int i = 1; i <= steps; i++) points.Add(Vector3.Lerp(start, end, (float)i / steps));
        }

        private static void Arc(List<Vector3> points, Vector2 centre, float fromDegrees, float toDegrees)
        {
            const int steps = 18;
            for (int i = 1; i <= steps; i++)
            {
                float angle = Mathf.Lerp(fromDegrees, toDegrees, (float)i / steps) * Mathf.Deg2Rad;
                points.Add(new Vector3(centre.x + 25 * Mathf.Cos(angle), 0, centre.y + 25 * Mathf.Sin(angle)));
            }
        }

        private static float PolylineLength(IReadOnlyList<Vector3> nodes)
        {
            float length = 0;
            for (int i = 1; i < nodes.Count; i++) length += Vector3.Distance(nodes[i - 1], nodes[i]);
            return length;
        }

        private static Vector3 Right(IReadOnlyList<Vector3> nodes, int index)
        {
            int previous = index == 0 ? nodes.Count - 2 : index - 1;
            int next = index == nodes.Count - 1 ? 1 : index + 1;
            return Vector3.Cross(Vector3.up, (nodes[next] - nodes[previous]).normalized).normalized;
        }

        private static Mesh Ribbon(IReadOnlyList<Vector3> nodes, float left, float right, float y)
        {
            var vertices = new List<Vector3>(nodes.Count * 2);
            var uvs = new List<Vector2>(nodes.Count * 2);
            var triangles = new List<int>((nodes.Count - 1) * 6);
            float distance = 0;
            float length = PolylineLength(nodes);
            int repeats = Mathf.RoundToInt(length / 3);
            for (int i = 0; i < nodes.Count; i++)
            {
                if (i > 0) distance += Vector3.Distance(nodes[i - 1], nodes[i]);
                var across = Right(nodes, i);
                vertices.Add(nodes[i] + across * left + Vector3.up * y);
                vertices.Add(nodes[i] + across * right + Vector3.up * y);
                float along = distance / length * repeats;
                uvs.Add(new Vector2(left / 3, along));
                uvs.Add(new Vector2(right / 3, along));
                if (i == nodes.Count - 1) continue;
                int a = i * 2;
                triangles.AddRange(new[] { a, a + 2, a + 1, a + 1, a + 2, a + 3 });
            }
            return Mesh(vertices, uvs, triangles);
        }

        private static Mesh Markings(IReadOnlyList<Vector3> nodes, float offset, float width, bool dashed)
        {
            var vertices = new List<Vector3>();
            var uvs = new List<Vector2>();
            var triangles = new List<int>();
            float distance = 0;
            for (int i = 0; i < nodes.Count - 1; i++)
            {
                float segment = Vector3.Distance(nodes[i], nodes[i + 1]);
                if (!dashed || Mathf.FloorToInt(distance / 7) % 2 == 0)
                {
                    var a = Right(nodes, i);
                    var b = Right(nodes, i + 1);
                    int first = vertices.Count;
                    vertices.Add(nodes[i] + a * (offset - width / 2) + Vector3.up * .018f);
                    vertices.Add(nodes[i] + a * (offset + width / 2) + Vector3.up * .018f);
                    vertices.Add(nodes[i + 1] + b * (offset - width / 2) + Vector3.up * .018f);
                    vertices.Add(nodes[i + 1] + b * (offset + width / 2) + Vector3.up * .018f);
                    uvs.AddRange(new[] { Vector2.zero, Vector2.right, Vector2.up, Vector2.one });
                    triangles.AddRange(new[] { first, first + 2, first + 1, first + 1, first + 2, first + 3 });
                }
                distance += segment;
            }
            return Mesh(vertices, uvs, triangles);
        }

        private static Mesh Mesh(List<Vector3> vertices, List<Vector2> uvs, List<int> triangles)
        {
            var mesh = new Mesh { indexFormat = UnityEngine.Rendering.IndexFormat.UInt32 };
            mesh.SetVertices(vertices);
            mesh.SetUVs(0, uvs);
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }

        private static void AddMesh(string name, Mesh generated, string assetName, Material material, bool collision)
        {
            string path = ArtPath + "/" + assetName + ".asset";
            var asset = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if (asset == null) AssetDatabase.CreateAsset(generated, path);
            else
            {
                EditorUtility.CopySerialized(generated, asset);
                EditorUtility.SetDirty(asset);
                UnityEngine.Object.DestroyImmediate(generated);
            }
            asset = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            var obj = new GameObject(name);
            obj.AddComponent<MeshFilter>().sharedMesh = asset;
            obj.AddComponent<MeshRenderer>().sharedMaterial = material;
            if (collision) obj.AddComponent<MeshCollider>().sharedMesh = asset;
        }

        private static void PopulateRoadside(IReadOnlyList<Vector3> nodes)
        {
            float length = PolylineLength(nodes);
            float[] cumulative = new float[nodes.Count];
            for (int i = 1; i < nodes.Count; i++) cumulative[i] = cumulative[i - 1] + Vector3.Distance(nodes[i - 1], nodes[i]);
            var barrierPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(IndustrialKitSetup.PrefabPath("RoadBarrier"));
            for (float distance = 0; distance < length; distance += 8)
            {
                int segment = Array.BinarySearch(cumulative, distance);
                if (segment < 0) segment = ~segment - 1;
                segment = Mathf.Clamp(segment, 0, nodes.Count - 2);
                var tangent = (nodes[segment + 1] - nodes[segment]).normalized;
                float t = (distance - cumulative[segment]) / (cumulative[segment + 1] - cumulative[segment]);
                var centre = Vector3.Lerp(nodes[segment], nodes[segment + 1], t);
                var right = Vector3.Cross(Vector3.up, tangent).normalized;
                foreach (float side in new[] { -1f, 1f })
                {
                    var barrier = (GameObject)PrefabUtility.InstantiatePrefab(barrierPrefab);
                    barrier.transform.SetPositionAndRotation(centre + right * (side * 9.5f) + Vector3.up * .04f,
                        Quaternion.LookRotation(tangent));
                    barrier.transform.localScale = new Vector3(1, 1, 8f / 5.8f);
                    var collider = barrier.AddComponent<BoxCollider>();
                    collider.size = new Vector3(.76f, 1, 5.8f);
                }
            }
            var warehousePrefab = AssetDatabase.LoadAssetAtPath<GameObject>(IndustrialKitSetup.PrefabPath("WarehouseBay"));
            var treeNames = new[] { "AutumnTreeA", "AutumnTreeB", "AutumnTreeC" };
            var random = new System.Random(611);
            for (int i = 0; i < 13; i++)
            {
                float z = 15 + i * 17;
                foreach (int side in new[] { -1, 1 })
                {
                    var warehouse = (GameObject)PrefabUtility.InstantiatePrefab(warehousePrefab);
                    warehouse.transform.SetPositionAndRotation(new Vector3(200 + side * 19, .1f, z),
                        Quaternion.Euler(0, side < 0 ? 90 : -90, 0));
                    var tree = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(
                        IndustrialKitSetup.PrefabPath(treeNames[i % treeNames.Length])));
                    tree.transform.SetPositionAndRotation(new Vector3(200 + side * 15.5f, .05f, z + 7),
                        Quaternion.Euler(0, random.Next(0, 360), 0));
                }
            }
            var lampPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(IndustrialKitSetup.PrefabPath("StreetLamp"));
            for (int i = 0; i < 9; i++)
            {
                var lamp = (GameObject)PrefabUtility.InstantiatePrefab(lampPrefab);
                lamp.transform.SetPositionAndRotation(new Vector3(211.5f, .15f, 5 + i * 28), Quaternion.Euler(0, -90, 0));
            }
            var dark = AssetDatabase.LoadAssetAtPath<Material>(StreetArt + "DarkFacade.mat");
            var concrete = AssetDatabase.LoadAssetAtPath<Material>(StreetArt + "ShoulderConcrete.mat");
            for (int i = 0; i < 12; i++)
            {
                int side = i % 2 == 0 ? -1 : 1;
                float z = 25 + i * 18;
                float height = 15 + i % 5 * 5;
                var building = GameObject.CreatePrimitive(PrimitiveType.Cube);
                building.name = "Return street skyline";
                building.transform.position = new Vector3(200 + side * (39 + i % 3 * 6), height / 2, z);
                building.transform.localScale = new Vector3(18, height, 16);
                building.GetComponent<Renderer>().sharedMaterial = i % 4 == 0 ? concrete : dark;
            }
            for (int i = 0; i < 11; i++)
            {
                float x = 15 + i * 17;
                foreach (float z in new[] { -40f, 270f })
                {
                    var tree = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(
                        IndustrialKitSetup.PrefabPath(treeNames[(i + 1) % treeNames.Length])));
                    tree.transform.SetPositionAndRotation(new Vector3(x, 0, z), Quaternion.Euler(0, random.Next(0, 360), 0));
                }
            }
            // Cross streets share the same modular language as the original avenue.
            for (int i = 0; i < 8; i++)
            {
                float x = 32 + i * 20;
                foreach (bool top in new[] { false, true })
                {
                    float roadZ = top ? 255 : -25;
                    foreach (bool outside in new[] { false, true })
                    {
                        float z = roadZ + (top ? 1 : -1) * (outside ? 26 : -26);
                        bool faceNorth = (top && !outside) || (!top && outside);
                        var warehouse = (GameObject)PrefabUtility.InstantiatePrefab(warehousePrefab);
                        warehouse.transform.SetPositionAndRotation(new Vector3(x, .1f, z),
                            Quaternion.Euler(0, faceNorth ? 0 : 180, 0));
                    }
                    if (i % 2 == 0)
                    {
                        var lamp = (GameObject)PrefabUtility.InstantiatePrefab(lampPrefab);
                        lamp.transform.SetPositionAndRotation(new Vector3(x, .1f, roadZ + (top ? 12 : -12)),
                            Quaternion.Euler(0, top ? 180 : 0, 0));
                    }
                }
            }
            var cranePrefab = AssetDatabase.LoadAssetAtPath<GameObject>(IndustrialKitSetup.PrefabPath("PortCrane"));
            foreach (var (position, angle) in new[]
            {
                (new Vector3(107, 0, 170), 22f), (new Vector3(260, 0, 143), -48f),
                (new Vector3(112, 0, 25), 95f)
            })
            {
                var crane = (GameObject)PrefabUtility.InstantiatePrefab(cranePrefab);
                crane.transform.SetPositionAndRotation(position, Quaternion.Euler(0, angle, 0));
            }
            for (int i = 0; i < 14; i++)
            {
                float x = 65 + i % 4 * 29;
                float z = 20 + i / 4 * 52;
                float height = 13 + (i * 7) % 21;
                var building = GameObject.CreatePrimitive(PrimitiveType.Cube);
                building.name = "Central industry mass";
                building.transform.position = new Vector3(x, height / 2, z);
                building.transform.localScale = new Vector3(14 + i % 3 * 4, height, 15 + i % 2 * 5);
                building.GetComponent<Renderer>().sharedMaterial = i % 4 == 0 ? concrete : dark;
            }
            var chevronPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(IndustrialKitSetup.PrefabPath("ChevronPanel"));
            foreach (float distance in new[] { 226f, 241f, 256f, 415f, 430f, 445f,
                         681f, 696f, 711f, 862f, 877f, 892f })
            {
                int segment = Array.BinarySearch(cumulative, distance);
                if (segment < 0) segment = ~segment - 1;
                segment = Mathf.Clamp(segment, 0, nodes.Count - 2);
                float t = (distance - cumulative[segment]) / (cumulative[segment + 1] - cumulative[segment]);
                var centre = Vector3.Lerp(nodes[segment], nodes[segment + 1], t);
                var tangent = (nodes[segment + 1] - nodes[segment]).normalized;
                var right = Vector3.Cross(Vector3.up, tangent).normalized;
                var sign = (GameObject)PrefabUtility.InstantiatePrefab(chevronPrefab);
                sign.transform.SetPositionAndRotation(centre - right * 14 + Vector3.up * .1f,
                    Quaternion.LookRotation(-tangent));
            }
        }

        private static void Require(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException(message);
        }
    }
}
