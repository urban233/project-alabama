using System;
using System.IO;
using System.Linq;
using Alabama.Bootstrap;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

namespace Alabama.Editor
{
    public static class ProjectFoundation
    {
        public const string ScenePath = "Assets/Alabama/Scenes/Foundation.unity";
        public const string SettingsPath = "Assets/Alabama/Settings/DemoSettings.asset";
        public const string CalibrationPath = "Assets/Alabama/Art/Calibration/calibration.fbx";
        private const string MaterialPath = "Assets/Alabama/Art/Calibration/CalibrationMaterial.mat";

        [MenuItem("Alabama/Foundation/Set Up")]
        public static void Setup()
        {
            if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            {
                return;
            }
            ConfigureProject();
            EnsureFolder("Assets/Alabama/Settings");
            EnsureFolder("Assets/Alabama/Scenes");
            EnsureFolder("Assets/Alabama/Art/Calibration");

            var settings = AssetDatabase.LoadAssetAtPath<DemoSettings>(SettingsPath);
            if (settings == null)
            {
                settings = ScriptableObject.CreateInstance<DemoSettings>();
                AssetDatabase.CreateAsset(settings, SettingsPath);
            }
            settings.Validate();
            // Persist newly created configuration before a scene serializes its reference.
            AssetDatabase.SaveAssets();

            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath) == null)
            {
                CreateScene();
            }
            if (!EditorBuildSettings.scenes.Any(scene => scene.enabled && scene.path == ScenePath))
            {
                EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) }
                    .Concat(EditorBuildSettings.scenes.Where(scene => scene.path != ScenePath)).ToArray();
            }
            AssetDatabase.SaveAssets();
            Verify();
            Debug.Log("Alabama foundation setup completed.");
        }

        [MenuItem("Alabama/Foundation/Verify")]
        public static void Verify()
        {
            Require(Application.unityVersion == "6000.3.25f1", "Use the pinned Unity 6000.3.25f1 editor.");
            Require(GraphicsSettings.defaultRenderPipeline is UniversalRenderPipelineAsset, "URP must be the default pipeline.");
            Require(QualitySettings.renderPipeline is UniversalRenderPipelineAsset, "The active quality level must use URP.");
            Require(EditorSettings.serializationMode == SerializationMode.ForceText, "Use text asset serialization.");
            Require(VersionControlSettings.mode == "Visible Meta Files", "Unity metadata must be visible.");
            Require(PlayerSettings.colorSpace == ColorSpace.Linear, "Use linear color space.");
            Require(EditorBuildSettings.scenes.Any(scene => scene.enabled && scene.path == ScenePath), "Foundation must be an enabled build scene.");

            var settings = AssetDatabase.LoadAssetAtPath<DemoSettings>(SettingsPath);
            Require(settings != null, "The DemoSettings asset is missing; run Setup.");
            settings.Validate();
            Require(AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath) != null, "The foundation scene is missing.");
            VerifySceneConfiguration(settings);

            var model = AssetDatabase.LoadAssetAtPath<GameObject>(CalibrationPath);
            Require(model != null, "Export the Blender calibration FBX before running Setup.");
            var mesh = model.GetComponentsInChildren<MeshFilter>().Single().sharedMesh;
            Require(mesh != null && mesh.vertexCount > 0, "Calibration mesh import is empty.");

            // Mesh bounds alone omit the importer's root conversion and child transforms.
            var instance = (GameObject)PrefabUtility.InstantiatePrefab(model);
            try
            {
                var dimensions = instance.GetComponentInChildren<Renderer>().bounds.size;
                Require((dimensions - Vector3.one).sqrMagnitude < 0.0001f, $"Calibration import is not one metre: {dimensions}.");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(instance);
            }
            Debug.Log("Alabama foundation verification passed.");
        }

        private static void VerifySceneConfiguration(DemoSettings settings)
        {
            var scene = SceneManager.GetSceneByPath(ScenePath);
            var openedForVerification = !scene.IsValid() || !scene.isLoaded;
            if (openedForVerification)
            {
                scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Additive);
            }
            try
            {
                var bootstraps = scene.GetRootGameObjects()
                    .SelectMany(root => root.GetComponentsInChildren<DemoBootstrap>(true)).ToArray();
                Require(bootstraps.Length == 1, "Foundation must have exactly one DemoBootstrap.");
                Require(bootstraps[0].Settings == settings, "Foundation must reference the saved DemoSettings asset.");
                var block = scene.GetRootGameObjects().SingleOrDefault(root => root.name == "CalibrationBody");
                Require(block != null, "Foundation must contain the calibration body.");
                var renderer = block.GetComponentInChildren<Renderer>();
                var material = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
                Require(renderer != null && material != null && renderer.sharedMaterial == material,
                    "The calibration body must reference its saved material.");
                Require(material.shader != null && material.shader.name == "Universal Render Pipeline/Lit",
                    "The calibration material must use the URP Lit shader.");
            }
            finally
            {
                if (openedForVerification)
                {
                    EditorSceneManager.CloseScene(scene, true);
                }
            }
        }

        private static void ConfigureProject()
        {
            var pipeline = AssetDatabase.FindAssets("t:UniversalRenderPipelineAsset")
                .Select(AssetDatabase.GUIDToAssetPath)
                .OrderBy(path => path.IndexOf("PC", StringComparison.OrdinalIgnoreCase) >= 0 ? 0 : 1)
                .Select(AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>)
                .FirstOrDefault(asset => asset != null);
            Require(pipeline != null, "Create the project with the Universal 3D template first.");
            GraphicsSettings.defaultRenderPipeline = pipeline;
            QualitySettings.renderPipeline = pipeline;
            pipeline.renderScale = 1f;
            // The pinned editor reproduces incorrect material constants with SRP batching
            // in the vehicle review on both D3D11 and D3D12. See docs/vehicle-review-status.md.
            pipeline.useSRPBatcher = false;
            pipeline.msaaSampleCount = 4;
            pipeline.shadowDistance = 150f;
            EditorUtility.SetDirty(pipeline);
            PlayerSettings.companyName = "Project Alabama";
            PlayerSettings.productName = "Project Alabama";
            PlayerSettings.defaultScreenWidth = 1920;
            PlayerSettings.defaultScreenHeight = 1080;
            PlayerSettings.fullScreenMode = FullScreenMode.Windowed;
            PlayerSettings.colorSpace = ColorSpace.Linear;
            EditorSettings.serializationMode = SerializationMode.ForceText;
            VersionControlSettings.mode = "Visible Meta Files";
            EditorSettings.enterPlayModeOptionsEnabled = false;
            Time.fixedDeltaTime = 1f / 120f;
            QualitySettings.vSyncCount = 0;
        }

        private static void CreateScene()
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            // Scene replacement can unload assets held only by local managed references.
            var settings = AssetDatabase.LoadAssetAtPath<DemoSettings>(SettingsPath);
            Require(settings != null, "The saved DemoSettings asset is unavailable.");
            var model = AssetDatabase.LoadAssetAtPath<GameObject>(CalibrationPath);
            Require(model != null, "Run the Blender calibration export before Setup.");
            var root = new GameObject("DemoBootstrap");
            var bootstrap = root.AddComponent<DemoBootstrap>();
            var serializedBootstrap = new SerializedObject(bootstrap);
            serializedBootstrap.FindProperty("settings").objectReferenceValue = settings;
            serializedBootstrap.ApplyModifiedPropertiesWithoutUndo();

            var floor = GameObject.CreatePrimitive(PrimitiveType.Cube);
            floor.name = "FoundationFloor";
            floor.transform.position = new Vector3(0f, -0.5f, 0f);
            floor.transform.localScale = new Vector3(30f, 1f, 30f);
            floor.isStatic = true;

            var material = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
            if (material == null)
            {
                var shader = Shader.Find("Universal Render Pipeline/Lit");
                Require(shader != null, "The URP Lit shader is unavailable.");
                material = new Material(shader) { name = "CalibrationMaterial" };
                material.SetColor("_BaseColor", new Color(0.18f, 0.27f, 0.37f));
                material.SetFloat("_Smoothness", 0.35f);
                AssetDatabase.CreateAsset(material, MaterialPath);
            }

            var block = (GameObject)PrefabUtility.InstantiatePrefab(model, scene);
            block.name = "CalibrationBody";
            block.transform.position = new Vector3(0f, 2f, 0f);
            var renderer = block.GetComponentInChildren<Renderer>();
            renderer.sharedMaterial = material;
            var collider = block.AddComponent<BoxCollider>();
            collider.center = block.transform.InverseTransformPoint(renderer.bounds.center);
            collider.size = Vector3.one;
            var body = block.AddComponent<Rigidbody>();
            body.mass = 1f;
            body.interpolation = RigidbodyInterpolation.Interpolate;
            body.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;

            var lightObject = new GameObject("AfternoonSun");
            var sun = lightObject.AddComponent<Light>();
            sun.type = LightType.Directional;
            sun.color = new Color(1f, 0.84f, 0.63f);
            sun.intensity = 1.5f;
            sun.shadows = LightShadows.Soft;
            sun.transform.rotation = Quaternion.Euler(28f, -35f, 0f);
            RenderSettings.sun = sun;
            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(0.48f, 0.55f, 0.62f);
            RenderSettings.ambientEquatorColor = new Color(0.30f, 0.32f, 0.34f);
            RenderSettings.ambientGroundColor = new Color(0.17f, 0.16f, 0.14f);
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Exponential;
            RenderSettings.fogColor = new Color(0.55f, 0.59f, 0.63f);
            RenderSettings.fogDensity = 0.004f;

            var cameraObject = new GameObject("Main Camera");
            cameraObject.tag = "MainCamera";
            var camera = cameraObject.AddComponent<Camera>();
            camera.transform.position = new Vector3(6f, 4f, -8f);
            camera.transform.LookAt(new Vector3(0f, 0.75f, 0f));
            camera.nearClipPlane = 0.1f;
            camera.farClipPlane = 300f;
            cameraObject.AddComponent<AudioListener>();
            camera.GetUniversalAdditionalCameraData().renderPostProcessing = true;
            AssetDatabase.SaveAssets();
            Require(EditorSceneManager.SaveScene(scene, ScenePath), "Unable to save the foundation scene.");
        }

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path))
            {
                return;
            }
            var parent = Path.GetDirectoryName(path).Replace('\\', '/');
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
        }

        private static void Require(bool condition, string message)
        {
            if (!condition)
            {
                throw new InvalidOperationException(message);
            }
        }
    }
}
