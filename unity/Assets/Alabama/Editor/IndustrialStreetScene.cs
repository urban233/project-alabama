using System;
using System.IO;
using Alabama.Bootstrap;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Alabama.Editor
{
    /// <summary>Deterministic, limited-scope industrial street art review.</summary>
    public static class IndustrialStreetScene
    {
        public const string ScenePath = "Assets/Alabama/Scenes/IndustrialStreet.unity";
        private const string ArtPath = "Assets/Alabama/Art/IndustrialStreet";
        private const string ReviewArt = "Assets/Alabama/Art/VehicleReview";

        public static void Setup()
        {
            IndustrialKitSetup.Setup();
            Create();
        }

        [MenuItem("Alabama/Street/Create Review Scene")]
        public static void Create()
        {
            VehicleAssetSetup.Verify();
            IndustrialKitSetup.Verify();
            if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            Directory.CreateDirectory(ArtPath);
            AssetDatabase.Refresh();
            var asphalt = AssetDatabase.LoadAssetAtPath<Material>(ReviewArt + "/Asphalt.mat");
            var sky = AssetDatabase.LoadAssetAtPath<Material>(ReviewArt + "/SunsetSky.mat");
            Require(asphalt != null && sky != null, "Run Alabama/Vehicle/Set Up first to prepare the CC0 asphalt and sky.");
            var concrete = Material("ShoulderConcrete", new Color(.28f, .27f, .24f), .13f);
            var dark = Material("DarkFacade", new Color(.11f, .13f, .14f), .22f);
            var white = Material("WeatheredWhite", new Color(.54f, .54f, .48f), .08f);
            var yellow = Material("AgedYellow", new Color(.63f, .46f, .17f), .08f);
            var green = AssetDatabase.LoadAssetAtPath<Material>(IndustrialKitSetup.DirectoryPath + "/Kit_SignGreen.mat");
            var black = AssetDatabase.LoadAssetAtPath<Material>(IndustrialKitSetup.DirectoryPath + "/Kit_Black.mat");
            var profile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(ArtPath + "/StreetGrade.asset");
            if (profile == null)
            {
                profile = ScriptableObject.CreateInstance<VolumeProfile>();
                AssetDatabase.CreateAsset(profile, ArtPath + "/StreetGrade.asset");
            }
            if (!profile.TryGet(out Tonemapping tone)) tone = profile.Add<Tonemapping>(true);
            tone.mode.Override(TonemappingMode.ACES);
            if (!profile.TryGet(out ColorAdjustments grade)) grade = profile.Add<ColorAdjustments>(true);
            grade.postExposure.Override(-.42f);
            grade.contrast.Override(19);
            grade.saturation.Override(-7);
            EditorUtility.SetDirty(profile);
            SignLettering.Ensure("RIVERSIDE");
            SignLettering.Ensure("DOWNTOWN");
            AssetDatabase.SaveAssets();

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            asphalt = AssetDatabase.LoadAssetAtPath<Material>(ReviewArt + "/Asphalt.mat");
            sky = AssetDatabase.LoadAssetAtPath<Material>(ReviewArt + "/SunsetSky.mat");
            concrete = AssetDatabase.LoadAssetAtPath<Material>(ArtPath + "/ShoulderConcrete.mat");
            dark = AssetDatabase.LoadAssetAtPath<Material>(ArtPath + "/DarkFacade.mat");
            white = AssetDatabase.LoadAssetAtPath<Material>(ArtPath + "/WeatheredWhite.mat");
            yellow = AssetDatabase.LoadAssetAtPath<Material>(ArtPath + "/AgedYellow.mat");
            green = AssetDatabase.LoadAssetAtPath<Material>(IndustrialKitSetup.DirectoryPath + "/Kit_SignGreen.mat");
            black = AssetDatabase.LoadAssetAtPath<Material>(IndustrialKitSetup.DirectoryPath + "/Kit_Black.mat");
            profile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(ArtPath + "/StreetGrade.asset");

            var bootstrap = new GameObject("Demo settings").AddComponent<DemoBootstrap>();
            var serializedBootstrap = new SerializedObject(bootstrap);
            serializedBootstrap.FindProperty("settings").objectReferenceValue = AssetDatabase.LoadAssetAtPath<DemoSettings>(ProjectFoundation.SettingsPath);
            serializedBootstrap.ApplyModifiedPropertiesWithoutUndo();
            var car = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(VehicleAssetSetup.PrefabPath));
            car.transform.position = new Vector3(-2.3f, 0, 2);
            car.name = "E46 review vehicle";

            Box("Industrial ground", new Vector3(0, -.45f, 105), new Vector3(400, .8f, 450), dark);
            Box("Road surface", new Vector3(0, -.055f, 105), new Vector3(18, .10f, 250), asphalt);
            foreach (int side in new[] { -1, 1 })
            {
                Box("Raised shoulder", new Vector3(side * 11.45f, .02f, 105), new Vector3(4.9f, .22f, 250), concrete);
                for (int i = 0; i < 42; i++)
                {
                    float z = -17 + 5.8f * i;
                    var barrier = Place("RoadBarrier", new Vector3(side * 9.05f, .12f, z));
                    barrier.AddComponent<BoxCollider>().size = new Vector3(.75f, 1, 5.8f);
                    if (i % 2 == 0)
                        Place("FencePanel", new Vector3(side * 12.7f, .14f, z + 2.9f), Quaternion.Euler(0, 90, 0));
                }
                for (int i = 0; i < 9; i++)
                {
                    var lamp = Place("StreetLamp", new Vector3(side * 11.2f, .15f, -7 + i * 29));
                    lamp.transform.rotation = Quaternion.Euler(0, side < 0 ? 90 : -90, 0);
                }
            }
            for (int i = 0; i < 38; i++)
            {
                float z = -13 + i * 6.5f;
                foreach (float x in new[] { -3.7f, 3.7f })
                    Box("Broken lane marking", new Vector3(x, .002f, z), new Vector3(.13f, .015f, 3.5f), white);
            }
            foreach (float x in new[] { -.16f, .16f })
                Box("Double yellow centre line", new Vector3(x, .005f, 105), new Vector3(.11f, .015f, 250), yellow);

            Buildings(dark, concrete);
            Foliage();
            ElevatedRoad(concrete, dark);
            Place("PortCrane", new Vector3(35, .0f, 91), Quaternion.Euler(0, -28, 0));
            Place("PortCrane", new Vector3(-42, .0f, 148), Quaternion.Euler(0, 55, 0));
            Place("SignGantry", new Vector3(0, .08f, 64));
            Sign("RIVERSIDE", -3.7f, 64, green, white);
            Sign("DOWNTOWN", 3.7f, 64, green, white);
            var billboard = Place("Billboard", new Vector3(-20, .2f, 11), Quaternion.Euler(0, 90, 0));
            billboard.name = "Alabama performance billboard";
            Place("ChevronPanel", new Vector3(8.7f, .1f, 55), Quaternion.Euler(0, -15, 0));
            Place("ChevronPanel", new Vector3(8.7f, .1f, 62), Quaternion.Euler(0, -15, 0));

            RenderSettings.skybox = sky;
            RenderSettings.defaultReflectionMode = DefaultReflectionMode.Custom;
            RenderSettings.customReflectionTexture = AssetDatabase.LoadAssetAtPath<Cubemap>("Assets/Alabama/Art/Environment/PolyHaven/industrial_sunset_puresky_2k.hdr");
            RenderSettings.reflectionIntensity = .65f;
            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(.44f, .49f, .55f);
            RenderSettings.ambientEquatorColor = new Color(.26f, .29f, .30f);
            RenderSettings.ambientGroundColor = new Color(.12f, .11f, .10f);
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Exponential;
            RenderSettings.fogColor = new Color(.48f, .49f, .48f);
            RenderSettings.fogDensity = .0042f;
            var sun = new GameObject("Low warm sun").AddComponent<Light>();
            sun.type = LightType.Directional;
            sun.color = new Color(1, .81f, .60f);
            sun.intensity = 1.8f;
            sun.shadows = LightShadows.Soft;
            sun.transform.rotation = Quaternion.Euler(27, -43, 0);
            RenderSettings.sun = sun;
            var volume = new GameObject("Street color grade").AddComponent<Volume>();
            volume.isGlobal = true;
            volume.sharedProfile = profile;
            var camera = new GameObject("Chase camera").AddComponent<Camera>();
            camera.tag = "MainCamera";
            camera.transform.position = new Vector3(-2.3f, 1.9f, -3.8f);
            camera.transform.LookAt(new Vector3(-1.7f, 1.35f, 27));
            camera.fieldOfView = 66;
            camera.nearClipPlane = .1f;
            camera.farClipPlane = 500;
            camera.clearFlags = CameraClearFlags.Skybox;
            camera.GetUniversalAdditionalCameraData().renderPostProcessing = true;
            EditorSceneManager.SaveScene(scene, ScenePath);
            Debug.Log("Industrial street review saved: " + ScenePath);
        }

        private static void Buildings(Material dark, Material concrete)
        {
            for (int i = 0; i < 12; i++)
            {
                float z = -12 + i * 17;
                Place("WarehouseBay", new Vector3(-20, .14f, z), Quaternion.Euler(0, 90, 0));
                if (i % 2 == 0) Place("WarehouseBay", new Vector3(23, .14f, z + 7), Quaternion.Euler(0, -90, 0));
            }
            for (int i = 0; i < 24; i++)
            {
                float x = (i % 2 == 0 ? -1 : 1) * (49 + i % 4 * 9);
                float z = 48 + i * 7;
                float height = 14 + i % 5 * 5;
                Box("Industrial skyline", new Vector3(x, height / 2, z), new Vector3(12 + i % 3 * 5, height, 12 + i % 4 * 5), i % 5 == 0 ? concrete : dark);
            }
            for (int i = 0; i < 17; i++)
            {
                float x = -70 + i * 9;
                float height = 25 + (i * 7) % 31;
                Box("Distant city silhouette", new Vector3(x, height / 2, 245 + i % 4 * 18),
                    new Vector3(8 + i % 3 * 4, height, 9), dark);
            }
        }

        private static void Foliage()
        {
            var random = new System.Random(1973);
            string[] trees = { "AutumnTreeA", "AutumnTreeB", "AutumnTreeC" };
            for (int i = 0; i < 35; i++)
            {
                int side = i % 2 == 0 ? -1 : 1;
                float z = -12 + i * 6.6f + (float)random.NextDouble() * 5;
                float x = side * (15.5f + (float)random.NextDouble() * 3);
                Place(trees[i % 3], new Vector3(x, .02f, z), Quaternion.Euler(0, i * 71 % 360, 0));
            }
        }

        private static void ElevatedRoad(Material concrete, Material dark)
        {
            // A diagonal crossing supplies the horizontal silhouette seen above the street.
            var rotation = Quaternion.Euler(0, 70, 0);
            var direction = rotation * Vector3.forward;
            for (int i = -2; i <= 2; i++)
                Place("BridgeDeck", new Vector3(0, 8.45f, 92) + direction * (i * 16), rotation);
            foreach (int side in new[] { -1, 1 })
                Place("BridgeColumn", new Vector3(side * 23, .0f, 92 - side * 8));
            Box("Further elevated road", new Vector3(0, 13.5f, 203), new Vector3(120, 1, 9), concrete);
            for (int i = -2; i <= 2; i++)
                Box("Further bridge column", new Vector3(i * 26, 6.5f, 203), new Vector3(1.5f, 13, 2.5f), dark);
        }

        private static void Sign(string label, float x, float z, Material green, Material white)
        {
            Box(label + " direction sign", new Vector3(x, 5.7f, z), new Vector3(6.5f, 2.5f, .12f), green);
            Box("Sign top rule", new Vector3(x, 6.88f, z - .075f), new Vector3(6.2f, .045f, .025f), white);
            var letters = new GameObject(label + " lettering");
            letters.transform.position = new Vector3(x, 5.77f, z - .09f);
            letters.AddComponent<MeshFilter>().sharedMesh = AssetDatabase.LoadAssetAtPath<Mesh>(SignLettering.Path(label));
            letters.AddComponent<MeshRenderer>().sharedMaterial = white;
            // Geometric downward arrow reads clearly at chase distance and needs no font asset.
            Box("Down arrow shaft", new Vector3(x, 5.15f, z - .09f), new Vector3(.11f, .60f, .02f), white);
            var left = Box("Down arrow left", new Vector3(x - .16f, 4.84f, z - .09f), new Vector3(.11f, .40f, .02f), white);
            left.transform.rotation = Quaternion.Euler(0, 0, -45);
            var right = Box("Down arrow right", new Vector3(x + .16f, 4.84f, z - .09f), new Vector3(.11f, .40f, .02f), white);
            right.transform.rotation = Quaternion.Euler(0, 0, 45);
        }

        private static GameObject Place(string name, Vector3 position, Quaternion? rotation = null)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(IndustrialKitSetup.PrefabPath(name));
            Require(prefab != null, "Missing kit module " + name);
            var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            instance.transform.SetPositionAndRotation(position, rotation ?? Quaternion.identity);
            return instance;
        }

        private static GameObject Box(string name, Vector3 position, Vector3 size, Material material)
        {
            var result = GameObject.CreatePrimitive(PrimitiveType.Cube);
            result.name = name;
            result.transform.position = position;
            result.transform.localScale = size;
            result.GetComponent<Renderer>().sharedMaterial = material;
            return result;
        }

        private static Material Material(string name, Color color, float smoothness)
        {
            var path = ArtPath + "/" + name + ".mat";
            var result = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (result == null)
            {
                result = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                AssetDatabase.CreateAsset(result, path);
            }
            result.SetColor("_BaseColor", color);
            result.SetFloat("_Smoothness", smoothness);
            EditorUtility.SetDirty(result);
            return result;
        }

        private static void Require(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException(message);
        }
    }
}
