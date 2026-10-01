using System.IO;
using Alabama.Bootstrap;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace Alabama.Editor
{
    public static class VehicleReviewScene
    {
        public const string ScenePath = "Assets/Alabama/Scenes/VehicleReview.unity";
        private const string ArtPath = "Assets/Alabama/Art/VehicleReview";

        public static void Create()
        {
            if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            Directory.CreateDirectory(ArtPath);
            AssetDatabase.Refresh();
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var bootstrap = new GameObject("Review settings").AddComponent<DemoBootstrap>();
            var serializedBootstrap = new SerializedObject(bootstrap);
            serializedBootstrap.FindProperty("settings").objectReferenceValue = AssetDatabase.LoadAssetAtPath<DemoSettings>(ProjectFoundation.SettingsPath);
            serializedBootstrap.ApplyModifiedPropertiesWithoutUndo();
            PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(VehicleAssetSetup.PrefabPath));
            var asphalt = Material("Asphalt", new Color(.20f, .21f, .22f), .12f);
            const string environmentPath = "Assets/Alabama/Art/Environment/PolyHaven/";
            var normalImporter = (TextureImporter)AssetImporter.GetAtPath(environmentPath + "asphalt_04_nor_gl_1k.png");
            normalImporter.textureType = TextureImporterType.NormalMap;
            normalImporter.SaveAndReimport();
            // Reacquire assets after imports to avoid retaining stale references.
            asphalt = AssetDatabase.LoadAssetAtPath<Material>(ArtPath + "/Asphalt.mat");
            asphalt.SetColor("_BaseColor", Color.white);
            asphalt.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>(environmentPath + "asphalt_04_diff_1k.jpg"));
            asphalt.SetTexture("_BumpMap", AssetDatabase.LoadAssetAtPath<Texture2D>(environmentPath + "asphalt_04_nor_gl_1k.png"));
            asphalt.SetTextureScale("_BaseMap", new Vector2(6, 53.33f));
            asphalt.SetFloat("_BumpScale", .4f);
            asphalt.EnableKeyword("_NORMALMAP");
            EditorUtility.SetDirty(asphalt);
            AssetDatabase.SaveAssets();
            var skyImporter = (TextureImporter)AssetImporter.GetAtPath(environmentPath + "industrial_sunset_puresky_2k.hdr");
            skyImporter.textureShape = TextureImporterShape.TextureCube;
            skyImporter.generateCubemap = TextureImporterGenerateCubemap.AutoCubemap;
            skyImporter.sRGBTexture = false;
            skyImporter.SaveAndReimport();
            string skyPath = ArtPath + "/SunsetSky.mat";
            var sky = AssetDatabase.LoadAssetAtPath<Material>(skyPath);
            if (sky == null)
            {
                sky = new Material(Shader.Find("Skybox/Cubemap"));
                AssetDatabase.CreateAsset(sky, skyPath);
            }
            var skyCube = AssetDatabase.LoadAssetAtPath<Cubemap>(environmentPath + "industrial_sunset_puresky_2k.hdr");
            sky.SetTexture("_Tex", skyCube);
            sky.SetFloat("_Exposure", .7f);
            EditorUtility.SetDirty(sky);
            RenderSettings.skybox = sky;
            RenderSettings.defaultReflectionMode = DefaultReflectionMode.Custom;
            RenderSettings.customReflectionTexture = skyCube;
            RenderSettings.reflectionIntensity = .7f;
            var concrete = Material("Concrete", new Color(.31f, .29f, .25f), .15f);
            var warehouse = Material("Warehouse", new Color(.16f, .18f, .20f), .2f);
            var stripe = Material("RoadMarking", new Color(.65f, .49f, .22f), .1f);
            var road = GameObject.CreatePrimitive(PrimitiveType.Plane);
            road.name = "Road";
            road.transform.position = new Vector3(0, -.015f, 20);
            road.transform.localScale = new Vector3(1.8f, 1, 16);
            road.GetComponent<Renderer>().sharedMaterial = AssetDatabase.LoadAssetAtPath<Material>(ArtPath + "/Asphalt.mat");
            for (int i = -4; i < 12; i++)
            {
                Cube("Lane line", new Vector3(3, -.005f, i * 7), new Vector3(.12f, .015f, 3), stripe);
                foreach (int side in new[] { -1, 1 })
                    Cube("Barrier", new Vector3(side * 8.3f, .45f, i * 7), new Vector3(.5f, .9f, 6.7f), concrete);
            }
            for (int i = 0; i < 7; i++)
                foreach (int side in new[] { -1, 1 })
                    Cube("Industrial block", new Vector3(side * (15 + i % 2 * 3), 4 + i % 3 * 2, -15 + i * 17), new Vector3(10, 8 + i % 3 * 4, 12), warehouse);
            var sun = new GameObject("Late afternoon sun").AddComponent<Light>();
            sun.type = LightType.Directional;
            sun.color = new Color(1, .79f, .55f);
            sun.intensity = 2.1f;
            sun.shadows = LightShadows.Soft;
            sun.transform.rotation = Quaternion.Euler(32, -35, 0);
            RenderSettings.sun = sun;
            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(.42f, .49f, .59f);
            RenderSettings.ambientEquatorColor = new Color(.28f, .30f, .34f);
            RenderSettings.ambientGroundColor = new Color(.15f, .13f, .10f);
            RenderSettings.fog = true;
            RenderSettings.fogColor = new Color(.51f, .51f, .48f);
            RenderSettings.fogMode = FogMode.Exponential;
            RenderSettings.fogDensity = .003f;
            var camera = new GameObject("Review camera").AddComponent<Camera>();
            camera.tag = "MainCamera";
            camera.transform.position = new Vector3(-3.9f, 1.8f, -6);
            camera.transform.LookAt(new Vector3(0, .7f, 0));
            camera.fieldOfView = 42;
            camera.clearFlags = CameraClearFlags.Skybox;
            camera.backgroundColor = RenderSettings.fogColor;
            camera.nearClipPlane = .1f;
            camera.farClipPlane = 300;
            AssetDatabase.SaveAssets();
            EditorSceneManager.SaveScene(scene, ScenePath);
            Debug.Log("Vehicle review scene saved: " + ScenePath);
        }

        private static Material Material(string name, Color color, float smoothness)
        {
            string path = ArtPath + "/" + name + ".mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                material = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                AssetDatabase.CreateAsset(material, path);
            }
            material.SetColor("_BaseColor", color);
            material.SetFloat("_Smoothness", smoothness);
            EditorUtility.SetDirty(material);
            return material;
        }

        private static void Cube(string name, Vector3 position, Vector3 size, Material material)
        {
            var obj = GameObject.CreatePrimitive(PrimitiveType.Cube);
            obj.name = name;
            obj.transform.position = position;
            obj.transform.localScale = size;
            obj.GetComponent<Renderer>().sharedMaterial = material;
        }
    }
}
