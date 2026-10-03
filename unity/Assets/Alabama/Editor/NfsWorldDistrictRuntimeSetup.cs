using System;
using System.IO;
using System.Linq;
using Alabama.Bootstrap;
using Alabama.Districts;
using Alabama.Driving;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Rendering;

namespace Alabama.Editor
{
    /// <summary>Derive additive scenes without rewriting accepted standalone artwork.</summary>
    public static class NfsWorldDistrictRuntimeSetup
    {
        public const string DirectoryPath = NfsWorldSetup.BasePath + "/Runtime";
        public const string RuntimeScene = DirectoryPath + "/DistrictRuntime.unity";
        public const string DowntownScene = DirectoryPath + "/DowntownContent.unity";
        public const string FixtureRuntime = DirectoryPath + "/FixtureRuntime.unity";
        public const string FixtureA = DirectoryPath + "/FixtureA.unity";
        public const string FixtureB = DirectoryPath + "/FixtureB.unity";
        public const string FixtureDuplicate = DirectoryPath + "/FixtureDuplicate.unity";
        public const string FixtureUnsupported = DirectoryPath + "/FixtureUnsupported.unity";
        public static string[] BuildScenes => new[] { RuntimeScene, DowntownScene, FixtureA, FixtureB, FixtureDuplicate, FixtureUnsupported }
            .Concat(File.Exists(NfsWorldRosewoodSetup.ScenePath) ? new[] { NfsWorldRosewoodSetup.ScenePath } : Array.Empty<string>()).ToArray();

        private static bool IncludesRosewood()
        {
            if (!File.Exists(NfsWorldRosewoodSetup.ScenePath)) return false;
            var runtime = UnityEngine.Object.FindFirstObjectByType<DistrictRuntime>();
            if (runtime == null) return false;
            var initial = new SerializedObject(runtime).FindProperty("initialDistrictScenes");
            for (int i = 0; i < initial.arraySize; i++)
                if (initial.GetArrayElementAtIndex(i).stringValue == NfsWorldRosewoodSetup.ScenePath) return true;
            return false;
        }

        private static bool Shared(GameObject root) =>
            root.GetComponent<ArcadeCarController>() != null || root.GetComponent<Camera>() != null ||
            root.GetComponent<DemoBootstrap>() != null || root.GetComponent<DriveTelemetry>() != null ||
            root.GetComponent<NfsWorldRenderSettings>() != null || root.GetComponent<NfsWorldBenchmark>() != null ||
            root.GetComponent<Light>() != null || root.GetComponent<Volume>() != null;

        [MenuItem("Alabama/NFS World/Generate Additive Downtown Runtime")]
        public static void Generate()
        {
            if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            NfsWorldSetup.Require(File.Exists(NfsWorldArtPass.ScenePath), "Receive the pinned private NFS pack first.");
            Directory.CreateDirectory(DirectoryPath); AssetDatabase.Refresh();
            var content = EditorSceneManager.OpenScene(NfsWorldArtPass.ScenePath);
            var car = UnityEngine.Object.FindFirstObjectByType<ArcadeCarController>();
            var pose = new DistrictRecoveryPose { id = "source-pit", position = car.transform.position,
                eulerAngles = car.transform.eulerAngles };
            var geometry = content.GetRootGameObjects().Where(r => !Shared(r)).ToArray();
            Physics.SyncTransforms();
            var colliders = geometry.SelectMany(r => r.GetComponentsInChildren<Collider>()).ToArray();
            var bounds = colliders[0].bounds;
            foreach (var collider in colliders.Skip(1)) bounds.Encapsulate(collider.bounds);
            bounds.Expand(new Vector3(8, 10, 8));
            var closures = GameObject.Find("Downtown exit closures");
            NfsWorldSetup.Require(closures != null && closures.transform.childCount == 6, "Retain all six reviewed exit closures.");
            // Connection records remain unresolved until A supplies reviewed seam data.
            var exits = closures.transform.Cast<Transform>().Select((wall, index) => new DistrictConnection
            {
                id = "downtown-exit-" + (index + 1), neighbourId = "unresolved-" + wall.name,
                reciprocalId = "", seamBounds = wall.GetComponent<Renderer>().bounds,
                outward = wall.forward, collisionOwner = "downtown", closure = wall.gameObject, seamVerified = false
            }).ToArray();
            foreach (var root in content.GetRootGameObjects().Where(Shared)) UnityEngine.Object.DestroyImmediate(root);
            var descriptor = new GameObject("Downtown district metadata").AddComponent<DistrictContent>();
            descriptor.Configure("downtown", bounds, new[] { pose }, exits);
            NfsWorldSetup.Require(descriptor.ValidateContract() == null, descriptor.ValidateContract());
            NfsWorldSetup.Require(descriptor.Supports(pose), "The Downtown spawn footprint must retain collision.");
            EditorSceneManager.SaveScene(content, DowntownScene);
            GenerateHost(RuntimeScene, DowntownScene);
            GenerateFixture(FixtureA, "fixture-a", new Vector3(-10000, 0, 0));
            GenerateFixture(FixtureB, "fixture-b", new Vector3(-10200, 0, 0));
            GenerateFixture(FixtureDuplicate, "fixture-a", new Vector3(-10400, 0, 0));
            GenerateFixture(FixtureUnsupported, "fixture-unsupported", new Vector3(-10600, 0, 0), false);
            GenerateHost(FixtureRuntime, FixtureA);
            AssetDatabase.SaveAssets(); Verify();
            Debug.Log("Generated additive Downtown and fixtures; accepted standalone scenes were not saved.");
        }

        private static void GenerateHost(string path, string initial)
        {
            var scene = EditorSceneManager.OpenScene(NfsWorldArtPass.ScenePath);
            foreach (var root in scene.GetRootGameObjects().Where(r => !Shared(r))) UnityEngine.Object.DestroyImmediate(root);
            var runtime = new GameObject("Persistent district runtime").AddComponent<DistrictRuntime>();
            runtime.Configure(UnityEngine.Object.FindFirstObjectByType<ArcadeCarController>(),
                UnityEngine.Object.FindFirstObjectByType<ChaseCamera>(), initial);
            Camera.main.useOcclusionCulling = false; // Requires a fresh configuration bake.
            EditorSceneManager.SaveScene(scene, path);
        }

        private static void GenerateFixture(string path, string id, Vector3 origin, bool supported = true)
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
            cube.name = "Synthetic district support";
            cube.transform.position = origin - Vector3.up * .2f;
            cube.transform.localScale = new Vector3(100, .4f, 100);
            if (!supported) cube.GetComponent<Collider>().enabled = false;
            var culling = new GameObject("Fixture culling").AddComponent<NfsWorldDistanceCulling>();
            culling.Configure(new[] { cube.GetComponent<Renderer>() }, new[] { 100f });
            var data = new GameObject("Fixture metadata").AddComponent<DistrictContent>();
            data.Configure(id, new Bounds(origin, new Vector3(100, 20, 100)), new[]
            { new DistrictRecoveryPose { id = "fixture-spawn", position = origin + Vector3.up * .24f } });
            EditorSceneManager.SaveScene(scene, path);
        }

        public static void Verify()
        {
            var host = EditorSceneManager.OpenScene(RuntimeScene);
            NfsWorldSetup.Require(UnityEngine.Object.FindObjectsByType<ArcadeCarController>(FindObjectsSortMode.None).Length == 1 &&
                UnityEngine.Object.FindObjectsByType<VehicleInput>(FindObjectsSortMode.None).Length == 1 &&
                UnityEngine.Object.FindObjectsByType<ChaseCamera>(FindObjectsSortMode.None).Length == 1 &&
                UnityEngine.Object.FindObjectsByType<DemoBootstrap>(FindObjectsSortMode.None).Length == 1 &&
                UnityEngine.Object.FindObjectsByType<NfsWorldRenderSettings>(FindObjectsSortMode.None).Length == 1 &&
                UnityEngine.Object.FindObjectsByType<Light>(FindObjectsSortMode.None).Count(l => l.type == LightType.Directional) == 1,
                "Runtime must have exactly one gameplay/shared rendering owner of each type.");
            var scene = EditorSceneManager.OpenScene(DowntownScene, OpenSceneMode.Additive);
            var descriptor = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<DistrictContent>()).Single();
            NfsWorldSetup.Require(descriptor.ValidateContract() == null, descriptor.ValidateContract());
            NfsWorldSetup.Require(descriptor.RecoveryPoses.All(descriptor.Supports), "Downtown recovery lacks collision support.");
            NfsWorldSetup.Require(SceneManager.GetActiveScene() == host, "Content cannot own active-scene rendering.");
            if (IncludesRosewood())
            {
                var neighbour = EditorSceneManager.OpenScene(NfsWorldRosewoodSetup.ScenePath, OpenSceneMode.Additive);
                var data = neighbour.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<DistrictContent>()).Single();
                NfsWorldSetup.Require(data.ValidateContract() == null, data.ValidateContract());
                NfsWorldSetup.Require(data.RecoveryPoses.All(data.Supports), "Rosewood recovery lacks collision support.");
                NfsWorldSetup.Require(SceneManager.GetActiveScene() == host, "Neighbour content cannot own active-scene rendering.");
            }
            Debug.Log("District runtime structure, recovery support and culling ownership verified.");
        }

        private static double bakeDeadline;
        public static void BakeOcclusion()
        {
            EditorSceneManager.OpenScene(RuntimeScene);
            if (IncludesRosewood()) { NfsWorldRosewoodSetup.BakeOcclusion(); return; }
            Verify(); Camera.main.useOcclusionCulling = true;
            foreach (var scene in new[] { SceneManager.GetActiveScene(), SceneManager.GetSceneByPath(DowntownScene) })
                EditorSceneManager.SaveScene(scene);
            StaticOcclusionCulling.smallestOccluder = 10; StaticOcclusionCulling.smallestHole = 4;
            StaticOcclusionCulling.backfaceThreshold = 100;
            NfsWorldSetup.Require(StaticOcclusionCulling.Compute(), "Runtime configuration occlusion bake did not start.");
            bakeDeadline = EditorApplication.timeSinceStartup + 1200;
            EditorApplication.update += FinishBake;
        }

        private static void FinishBake()
        {
            if (StaticOcclusionCulling.isRunning && EditorApplication.timeSinceStartup < bakeDeadline) return;
            EditorApplication.update -= FinishBake;
            if (StaticOcclusionCulling.isRunning)
            {
                StaticOcclusionCulling.Cancel(); Debug.LogError("Runtime occlusion bake exceeded twenty minutes.");
                if (Application.isBatchMode) EditorApplication.Exit(1); return;
            }
            foreach (var scene in new[] { SceneManager.GetActiveScene(), SceneManager.GetSceneByPath(DowntownScene) })
                EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();
            Debug.Log("Occlusion baked for runtime plus Downtown; a future neighbour requires a combined bake.");
            if (Application.isBatchMode) EditorApplication.Exit(0);
        }

        public static void Build()
        {
            NfsWorldValidation.RuntimeContent();
            Verify(); PlayerSettings.enableFrameTimingStats = true;
            string output = Path.GetFullPath(Path.Combine(Application.dataPath, "../../builds/nfs-world-runtime/Alabama.exe"));
            Directory.CreateDirectory(Path.GetDirectoryName(output));
            var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
            {
                scenes = BuildScenes, locationPathName = output, target = BuildTarget.StandaloneWindows64,
                options = BuildOptions.StrictMode
            });
            NfsWorldSetup.Require(report.summary.result == BuildResult.Succeeded && report.summary.totalErrors == 0,
                "District runtime Windows build failed: " + report.summary.result);
        }
    }
}
