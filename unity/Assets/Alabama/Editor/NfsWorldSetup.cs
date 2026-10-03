using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Alabama.Bootstrap;
using Alabama.Driving;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Alabama.Editor
{
    /// <summary>Explicit local-only static-map import. All generated assets remain under ignored Art.</summary>
    public static class NfsWorldSetup
    {
        public const string BasePath = "Assets/Alabama/Art/Maps/NfsWorld";
        public const string SampleScene = BasePath + "/Scenes/NfsWorldSample.unity";
        public const string DistrictScene = BasePath + "/Scenes/NfsWorldPrototype.unity";
        [Serializable] public sealed class Contract
        {
            public string variant;
            public string districtId;
            public float[] sourceSpawn;
            public float[] sourceOrigin;
            public float[] spawnForward;
            public Part[] parts;
            public MaterialDefinition[] materials;
        }
        [Serializable] public sealed class Part { public string category; public string file; public MeshDefinition[] meshes; public int triangles; }
        [Serializable] public sealed class MeshDefinition
        {
            public string name;
            public string sourceName;
            public string material;
            public bool collision;
            public bool visible;
            public bool castsShadows;
            public int triangles;
            public float[] boundsMin;
            public float[] boundsMax;
        }
        [Serializable] public sealed class MaterialDefinition
        {
            public string name;
            public string texture;
            public bool alphaClip;
            public float cutoff;
            public bool doubleSided;
            public string family;
        }

        [MenuItem("Alabama/NFS World/Set Up Conversion Sample")]
        public static void Sample() => Setup("Sample");
        [MenuItem("Alabama/NFS World/Set Up Downtown Rockport")]
        public static void District() => Setup("District");

        public static Contract ReadContract(string variant) =>
            JsonUtility.FromJson<Contract>(File.ReadAllText(BasePath + "/" + variant + "/contract.json"));

        private static void Setup(string variant)
        {
            if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            var contract = ReadContract(variant);
            Directory.CreateDirectory(BasePath + "/Materials");
            Directory.CreateDirectory(BasePath + "/Scenes");
            AssetDatabase.Refresh();
            var materials = CreateMaterials(contract);
            foreach (var part in contract.parts)
            {
                string path = BasePath + "/" + variant + "/" + part.file;
                var importer = AssetImporter.GetAtPath(path) as ModelImporter;
                Require(importer != null, "Model importer missing: " + path);
                importer.globalScale = 1;
                importer.useFileScale = true;
                importer.bakeAxisConversion = false;
                importer.importAnimation = false;
                importer.importNormals = ModelImporterNormals.Import;
                importer.importTangents = ModelImporterTangents.CalculateMikk;
                importer.isReadable = true; // Import validation and collider cooking need mesh data.
                importer.meshCompression = ModelImporterMeshCompression.Off;
                importer.weldVertices = false;
                importer.materialImportMode = ModelImporterMaterialImportMode.ImportStandard;
                foreach (var material in contract.materials)
                    importer.AddRemap(new AssetImporter.SourceAssetIdentifier(typeof(Material), material.name), materials[material.name]);
                importer.SaveAndReimport();
            }
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var bootstrap = new GameObject("Prototype settings").AddComponent<DemoBootstrap>();
            Assign(bootstrap, "settings", AssetDatabase.LoadAssetAtPath<DemoSettings>(ProjectFoundation.SettingsPath));
            int visibleCount = 0, colliderCount = 0, visibleTriangles = 0, collisionTriangles = 0;
            foreach (var part in contract.parts)
            {
                var wrapper = new GameObject(part.category);
                var model = (GameObject)PrefabUtility.InstantiatePrefab(
                    AssetDatabase.LoadAssetAtPath<GameObject>(BasePath + "/" + variant + "/" + part.file));
                model.transform.SetParent(wrapper.transform, false);
                AlignAxes(wrapper.transform, model.transform);
                var transforms = model.GetComponentsInChildren<Transform>().ToDictionary(t => t.name);
                foreach (var definition in part.meshes)
                {
                    Require(transforms.TryGetValue(definition.name, out var transform), "Missing imported mesh " + definition.name);
                    var filter = transform.GetComponent<MeshFilter>();
                    Require(filter != null && filter.sharedMesh != null, "Mesh filter missing: " + definition.name);
                    int importedTriangles = filter.sharedMesh.triangles.Length / 3;
                    Require(importedTriangles == definition.triangles,
                        $"Triangle count changed: {definition.name}, expected {definition.triangles}, imported {importedTriangles}");
                    var renderer = transform.GetComponent<MeshRenderer>();
                    if (definition.boundsMin != null && definition.boundsMax != null)
                    {
                        var min = new Vector3(-definition.boundsMax[0], definition.boundsMin[1], definition.boundsMin[2]);
                        var max = new Vector3(-definition.boundsMin[0], definition.boundsMax[1], definition.boundsMax[2]);
                        Require(Vector3.Distance(renderer.bounds.min, min) < .02f && Vector3.Distance(renderer.bounds.max, max) < .02f,
                            $"Source/Unity world bounds disagree: {definition.name}, Unity {renderer.bounds}, source {min}..{max}");
                    }
                    renderer.enabled = definition.visible;
                    renderer.shadowCastingMode = definition.visible && definition.castsShadows
                        ? ShadowCastingMode.On : ShadowCastingMode.Off;
                    renderer.receiveShadows = true;
                    renderer.sharedMaterial = materials[definition.material];
                    if (definition.visible) { visibleCount++; visibleTriangles += definition.triangles; }
                    if (definition.collision)
                    {
                        var collider = transform.gameObject.AddComponent<MeshCollider>();
                        collider.convex = false;
                        collider.sharedMesh = filter.sharedMesh;
                        colliderCount++;
                        collisionTriangles += definition.triangles;
                    }
                }
            }
            Physics.SyncTransforms();
            Require(Physics.Raycast(new Vector3(0, 50, 0), Vector3.down, out var ground, 120), "Source pit marker has no imported collision below it.");
            Require(Mathf.Abs(ground.point.y) < 2, "Pit road height disagrees with source coordinates: " + ground.point);
            var carPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(HandlingCourseSetup.PrefabPath);
            Require(carPrefab != null, "Restore the prototype driving prefab first.");
            var car = (GameObject)PrefabUtility.InstantiatePrefab(carPrefab);
            car.name = "E46 driver car";
            var forward = new Vector3(-contract.spawnForward[0], 0, contract.spawnForward[2]).normalized;
            car.transform.SetPositionAndRotation(ground.point + Vector3.up * .24f, Quaternion.LookRotation(forward));
            var controller = car.GetComponent<ArcadeCarController>();
            var camera = new GameObject("Chase camera").AddComponent<Camera>();
            camera.tag = "MainCamera";
            camera.fieldOfView = 66;
            camera.nearClipPlane = .1f;
            camera.farClipPlane = 4000;
            camera.clearFlags = CameraClearFlags.Skybox;
            camera.gameObject.AddComponent<UniversalAdditionalCameraData>();
            Assign(camera.gameObject.AddComponent<ChaseCamera>(), "target", controller);
            camera.transform.position = car.transform.TransformPoint(new Vector3(0, 1.9f, -6));
            camera.transform.LookAt(car.transform.TransformPoint(new Vector3(0, 1.2f, 12)));
            Assign(new GameObject("Driving telemetry").AddComponent<DriveTelemetry>(), "target", controller);
            Assign(car.AddComponent<MapRecovery>(), "controller", controller);
            bootstrap.gameObject.AddComponent<NfsWorldBenchmark>();
            Lighting();
            if (variant == "District") NfsWorldOptimization.Apply();
            AssetDatabase.SaveAssets();
            string scenePath = variant == "Sample" ? SampleScene : DistrictScene;
            EditorSceneManager.SaveScene(scene, scenePath);
            string evidence = Path.GetFullPath(Path.Combine(Application.dataPath, "../../artifacts/NfsWorld"));
            Directory.CreateDirectory(evidence);
            File.WriteAllText(Path.Combine(evidence, "unity-" + variant + ".json"),
                $"{{\"variant\":\"{variant}\",\"visibleMeshes\":{visibleCount},\"colliders\":{colliderCount},\"visibleTriangles\":{visibleTriangles},\"collisionTriangles\":{collisionTriangles},\"spawnGroundHeight\":{ground.point.y.ToString(System.Globalization.CultureInfo.InvariantCulture)}}}");
            Debug.Log($"NFS World {variant}: {visibleCount} visible meshes, {colliderCount} static colliders. Scene: {scenePath}");
        }

        internal static Dictionary<string, Material> CreateMaterials(Contract contract, string assetRoot = BasePath)
        {
            var result = new Dictionary<string, Material>();
            var preparedTextures = new HashSet<string>();
            foreach (var definition in contract.materials)
            {
                if (!string.IsNullOrEmpty(definition.texture) && preparedTextures.Add(definition.texture))
                {
                    string texturePath = assetRoot + "/Textures/" + definition.texture;
                    var textureImporter = AssetImporter.GetAtPath(texturePath) as TextureImporter;
                    Require(textureImporter != null, "Decode source texture first: " + texturePath);
                    bool alphaTransparency = contract.materials.Any(m => m.texture == definition.texture && m.alphaClip);
                    bool changed = !textureImporter.sRGBTexture || textureImporter.alphaSource != TextureImporterAlphaSource.FromInput ||
                                   textureImporter.alphaIsTransparency != alphaTransparency || !textureImporter.mipmapEnabled ||
                                   textureImporter.wrapMode != TextureWrapMode.Repeat || textureImporter.anisoLevel != 4 ||
                                   textureImporter.maxTextureSize != 2048 || textureImporter.textureCompression != TextureImporterCompression.Compressed;
                    textureImporter.sRGBTexture = true;
                    textureImporter.alphaSource = TextureImporterAlphaSource.FromInput;
                    textureImporter.alphaIsTransparency = alphaTransparency;
                    textureImporter.mipmapEnabled = true;
                    textureImporter.wrapMode = TextureWrapMode.Repeat;
                    textureImporter.anisoLevel = 4;
                    textureImporter.maxTextureSize = 2048;
                    textureImporter.textureCompression = TextureImporterCompression.Compressed;
                    if (changed) textureImporter.SaveAndReimport();
                }
                string path = assetRoot + "/Materials/" + definition.name + ".mat";
                var material = AssetDatabase.LoadAssetAtPath<Material>(path);
                if (material == null)
                {
                    material = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                    AssetDatabase.CreateAsset(material, path);
                }
                material.SetColor("_BaseColor", Color.white);
                material.SetTexture("_BaseMap", string.IsNullOrEmpty(definition.texture) ? null :
                    AssetDatabase.LoadAssetAtPath<Texture2D>(assetRoot + "/Textures/" + definition.texture));
                material.SetFloat("_Metallic", 0);
                material.SetFloat("_Smoothness", .12f);
                material.SetFloat("_Cull", definition.doubleSided ? (float)CullMode.Off : (float)CullMode.Back);
                material.SetFloat("_Surface", 0);
                material.SetFloat("_AlphaClip", definition.alphaClip ? 1 : 0);
                material.SetFloat("_Cutoff", definition.cutoff);
                if (definition.alphaClip) material.EnableKeyword("_ALPHATEST_ON");
                else material.DisableKeyword("_ALPHATEST_ON");
                material.renderQueue = definition.alphaClip ? (int)RenderQueue.AlphaTest : (int)RenderQueue.Geometry;
                material.SetOverrideTag("RenderType", definition.alphaClip ? "TransparentCutout" : "Opaque");
                material.enableInstancing = true;
                EditorUtility.SetDirty(material);
                result.Add(definition.name, material);
            }
            AssetDatabase.SaveAssets();
            return result;
        }

        internal static void AlignAxes(Transform wrapper, Transform model)
        {
            var nodes = model.GetComponentsInChildren<Transform>();
            var origin = nodes.Single(t => t.name == "OriginMarker");
            var x = nodes.Single(t => t.name == "XMarker");
            var y = nodes.Single(t => t.name == "YMarker");
            var z = nodes.Single(t => t.name == "ZMarker");
            wrapper.rotation = Quaternion.Inverse(Quaternion.LookRotation((z.position - origin.position).normalized,
                                                                          (y.position - origin.position).normalized));
            wrapper.position = -origin.position;
            // Preserve FBX's right-to-left-handed X reflection. Undoing it mirrors lettering.
            Require(Vector3.Distance(x.position, new Vector3(-10, 0, 0)) < .001f &&
                    Vector3.Distance(y.position, new Vector3(0, 10, 0)) < .001f &&
                    Vector3.Distance(z.position, new Vector3(0, 0, 10)) < .001f,
                    $"FBX axis/scale contract failed: X {x.position}, Y {y.position}, Z {z.position}");
            Debug.Log($"FBX axis contract verified: {wrapper.name}, root reflection {wrapper.localScale.x}");
        }

        private static void Lighting()
        {
            const string environment = "Assets/Alabama/Art/Environment/PolyHaven/";
            var sky = AssetDatabase.LoadAssetAtPath<Material>("Assets/Alabama/Art/IndustrialStreet/SunsetSky.mat");
            if (sky == null) sky = AssetDatabase.LoadAssetAtPath<Material>("Assets/Alabama/Art/VehicleReview/SunsetSky.mat");
            RenderSettings.skybox = sky;
            var skyCube = AssetDatabase.LoadAssetAtPath<Cubemap>(environment + "industrial_sunset_puresky_2k.hdr");
            if (skyCube != null)
            {
                RenderSettings.defaultReflectionMode = DefaultReflectionMode.Custom;
                RenderSettings.customReflectionTexture = skyCube;
                RenderSettings.reflectionIntensity = .45f;
            }
            var sun = new GameObject("Late afternoon sun").AddComponent<Light>();
            sun.type = LightType.Directional;
            sun.color = new Color(1, .83f, .65f);
            sun.intensity = 1.8f;
            sun.shadows = LightShadows.Soft;
            sun.transform.rotation = Quaternion.Euler(28, -35, 0);
            RenderSettings.sun = sun;
            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(.34f, .40f, .48f);
            RenderSettings.ambientEquatorColor = new Color(.25f, .28f, .33f);
            RenderSettings.ambientGroundColor = new Color(.15f, .13f, .11f);
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Exponential;
            RenderSettings.fogDensity = .00065f;
            RenderSettings.fogColor = new Color(.51f, .52f, .53f);
        }

        public static void Assign(UnityEngine.Object target, string property, UnityEngine.Object value)
        {
            var serialized = new SerializedObject(target);
            serialized.FindProperty(property).objectReferenceValue = value;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        public static void Require(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException(message);
        }
    }
}
