using System.Collections.Generic;
using System.IO;
using System.Linq;
using Alabama.Districts;
using Alabama.Driving;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEditor.Build.Reporting;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace Alabama.Editor
{
    /// <summary>Full Downtown visual treatment with independent source driving collision.</summary>
    public static class NfsWorldArtDirection
    {
        public const string DirectoryPath = NfsWorldSetup.BasePath + "/ArtDirection";
        public const string ScenePath = DirectoryPath + "/NfsWorldArtDirection.unity";
        public const string RuntimeScene = DirectoryPath + "/Runtime/DistrictRuntime.unity";
        public const string ContentScene = DirectoryPath + "/Runtime/DowntownContent.unity";
        [System.Serializable] private sealed class MaterialRecipe { public string[] roadDetailSources; }
        [System.Serializable] private sealed class DetailRecipe { public float mean; public float strength; public float repeatsPerMetre; }

        [MenuItem("Alabama/NFS World/Generate Middle Detail Art Direction")]
        public static void Generate()
        {
            if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            NfsWorldSetup.Require(File.Exists(DirectoryPath + "/textures.json"), "Run the reviewed Blender material bake first.");
            NfsWorldVisualOptimization.RunArtDirection();
            ConfigureRoadDetails();
            ConfigureVehicleMaterials();
            var target = SceneManager.GetActiveScene();
            var targetCulling = Object.FindFirstObjectByType<NfsWorldDistanceCulling>();
            // Keep reviewed lighting/shadows fixed during geometry and material
            // comparisons. Source collision and cutout masks retain their identity.
            var baseline = EditorSceneManager.OpenScene(NfsWorldArtPass.ScenePath, OpenSceneMode.Additive);
            NfsWorldSetup.Require(CollisionSignatures(target).SequenceEqual(CollisionSignatures(baseline)),
                "Art direction must retain every source collision mesh, exit collider and transform.");
            var sourceCulling = baseline.GetRootGameObjects()
                .SelectMany(r => r.GetComponentsInChildren<NfsWorldDistanceCulling>()).Single();
            var shadowRoot = baseline.GetRootGameObjects().Single(r => r.name == "Spatial shadow casters");
            var sourceShadows = shadowRoot.GetComponentsInChildren<MeshRenderer>();
            var copy = Object.Instantiate(shadowRoot);
            copy.name = shadowRoot.name;
            SceneManager.MoveGameObjectToScene(copy, target);
            var clonedShadows = copy.GetComponentsInChildren<MeshRenderer>();
            var groups = new Dictionary<float, List<Renderer>>();
            for (int index = 0; index < sourceShadows.Length; index++)
            {
                float distance = sourceCulling.DistanceFor(sourceShadows[index]);
                if (!groups.TryGetValue(distance, out var group)) groups[distance] = group = new List<Renderer>();
                group.Add(clonedShadows[index]);
            }
            foreach (var renderer in target.GetRootGameObjects().Single(r => r.name == "Optimized district visuals")
                .GetComponentsInChildren<MeshRenderer>()) renderer.shadowCastingMode = ShadowCastingMode.Off;
            foreach (var group in groups) targetCulling.AddTargets(group.Value.ToArray(), group.Key);
            EditorSceneManager.CloseScene(baseline, true);
            SceneManager.SetActiveScene(target);
            Camera.main.useOcclusionCulling = false; // A fresh bake targets the new renderers.
            EditorUtility.SetDirty(targetCulling);
            AssetDatabase.SaveAssets();
            EditorSceneManager.SaveScene(target);
            Verify();
        }

        public static void BakeOcclusion() => NfsWorldOcclusion.Bake(ScenePath, "art-direction-occlusion.json");

        public static void ConfigureVehicle()
        {
            EditorSceneManager.OpenScene(ScenePath);
            ConfigureVehicleMaterials();
            AssetDatabase.SaveAssets();
            EditorSceneManager.SaveScene(SceneManager.GetActiveScene());
        }

        private static void ConfigureVehicleMaterials()
        {
            string directory = DirectoryPath + "/Vehicle";
            Directory.CreateDirectory(directory); AssetDatabase.Refresh();
            var response = new Dictionary<string, Vector2>
            {
                { "E46_Paint", new Vector2(.38f, .2f) }, { "E46_Blue", new Vector2(.38f, .2f) },
                { "E46_Silver", new Vector2(.38f, .2f) }, { "E46_Glass", new Vector2(.55f, .12f) },
                { "E46_Alloy", new Vector2(.5f, .75f) }
            };
            var copies = new Dictionary<string, Material>();
            var car = Object.FindFirstObjectByType<ArcadeCarController>();
            foreach (var renderer in car.GetComponentsInChildren<MeshRenderer>(true))
            {
                var bindings = renderer.sharedMaterials;
                bool changed = false;
                for (int index = 0; index < bindings.Length; index++)
                {
                    string role = Path.GetFileNameWithoutExtension(AssetDatabase.GetAssetPath(bindings[index]));
                    if (!response.TryGetValue(role, out var value)) continue;
                    if (!copies.TryGetValue(role, out var copy))
                    {
                        var original = AssetDatabase.LoadAssetAtPath<Material>("Assets/Alabama/Art/Vehicles/E46/" + role + ".mat");
                        NfsWorldSetup.Require(original != null, "Missing reviewed vehicle material: " + role);
                        string path = directory + "/" + role + ".mat";
                        copy = AssetDatabase.LoadAssetAtPath<Material>(path);
                        if (copy == null) { copy = Object.Instantiate(original); AssetDatabase.CreateAsset(copy, path); }
                        else EditorUtility.CopySerialized(original, copy);
                        copy.name = role + " middle detail";
                        copy.SetFloat("_Smoothness", value.x); copy.SetFloat("_Metallic", value.y);
                        EditorUtility.SetDirty(copy); copies.Add(role, copy);
                    }
                    bindings[index] = copy; changed = true;
                }
                if (changed) renderer.sharedMaterials = bindings;
            }
            NfsWorldSetup.Require(copies.ContainsKey("E46_Paint") && copies.ContainsKey("E46_Glass"),
                "Vehicle styling did not reach the body and glazing.");
            File.WriteAllText(Path.GetFullPath(Path.Combine(Application.dataPath, "../../artifacts/NfsWorld/DeveloperBArt/vehicle-style.json")),
                "{\"derivedMaterials\":" + copies.Count + ",\"bodySmoothness\":0.38,\"glassSmoothness\":0.55,\"vehicleGeometryChanged\":false,\"sourceMaterialsChanged\":false}\n");
        }

        public static void ConfigureRoadDetails()
        {
            AssetDatabase.Refresh();
            string root = Path.GetFullPath(Path.Combine(Application.dataPath, "../.."));
            var profile = JsonUtility.FromJson<MaterialRecipe>(File.ReadAllText(Path.Combine(root, "docs/art-direction/downtown-material-recipe.json")));
            var recipe = JsonUtility.FromJson<DetailRecipe>(File.ReadAllText(Path.Combine(root,
                "source-art/maps/nfs-world/art-textures/art-direction/road-detail.json")));
            var sources = new HashSet<string>(profile.roadDetailSources);
            string path = DirectoryPath + "/Textures/road-detail.png";
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            NfsWorldSetup.Require(importer != null, "Receive the private generated road-detail texture first.");
            importer.textureType = TextureImporterType.Default;
            importer.sRGBTexture = false; // Grayscale variation is data, not a new albedo palette.
            importer.mipmapEnabled = true; importer.wrapMode = TextureWrapMode.Repeat;
            importer.filterMode = FilterMode.Trilinear; importer.anisoLevel = 4;
            importer.textureCompression = TextureImporterCompression.Compressed; importer.maxTextureSize = 2048;
            importer.SaveAndReimport();
            var detail = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            int arrays = 0, layers = 0;
            foreach (string layerFile in Directory.GetFiles(DirectoryPath + "/Optimized/Arrays", "*.layers.txt"))
            {
                string materialPath = layerFile.Replace(".layers.txt", ".mat");
                var material = AssetDatabase.LoadAssetAtPath<Material>(materialPath);
                var ids = File.ReadAllLines(layerFile);
                var colours = ids.Select(id => sources.Contains(Path.GetFileName(AssetDatabase.GUIDToAssetPath(id.Split(':')[0])))
                    ? Color.white : Color.black).ToArray();
                material.DisableKeyword("_ROAD_DETAIL");
                if (!colours.Any(c => c == Color.white)) { EditorUtility.SetDirty(material); continue; }
                var weights = new Texture2D(ids.Length, 1, TextureFormat.RGBA32, false, true)
                { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp, name = material.name + " asphalt weights" };
                weights.SetPixels(colours); weights.Apply(false, false);
                string weightsPath = layerFile.Replace(".layers.txt", ".road-weights.asset");
                var existing = AssetDatabase.LoadAssetAtPath<Texture2D>(weightsPath);
                if (existing == null) AssetDatabase.CreateAsset(weights, weightsPath);
                else { EditorUtility.CopySerialized(weights, existing); Object.DestroyImmediate(weights); weights = existing; }
                material.SetTexture("_RoadDetailMap", detail); material.SetTexture("_RoadDetailWeights", weights);
                material.SetFloat("_RoadDetailMean", recipe.mean); material.SetFloat("_RoadDetailStrength", recipe.strength);
                material.SetFloat("_RoadDetailScale", recipe.repeatsPerMetre); material.EnableKeyword("_ROAD_DETAIL");
                EditorUtility.SetDirty(material); arrays++; layers += colours.Count(c => c == Color.white);
            }
            NfsWorldSetup.Require(layers > 0, "No reviewed road layers received the detail material.");
            AssetDatabase.SaveAssets();
            File.WriteAllText(Path.Combine(root, "artifacts/NfsWorld/DeveloperBArt/road-detail.json"),
                $"{{\"arrayMaterials\":{arrays},\"roadLayers\":{layers},\"strength\":{recipe.strength},\"repeatsPerMetre\":{recipe.repeatsPerMetre},\"sourceUvsChanged\":false,\"paintMasked\":true}}\n");
        }

        public static void Verify()
        {
            NfsWorldValidation.ArtDirection();
            var target = SceneManager.GetActiveScene();
            var baseline = EditorSceneManager.OpenScene(NfsWorldArtPass.ScenePath, OpenSceneMode.Additive);
            try
            {
                NfsWorldSetup.Require(CollisionSignatures(target).SequenceEqual(CollisionSignatures(baseline)),
                    "Source collision and exit colliders must match the accepted scene exactly.");
                Light Sun(Scene scene) => scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<Light>())
                    .Single(l => l.type == LightType.Directional);
                var before = Sun(baseline); var after = Sun(target);
                NfsWorldSetup.Require(before.color == after.color && before.intensity == after.intensity &&
                    before.transform.rotation == after.transform.rotation && before.shadows == after.shadows,
                    "Art direction must preserve the reviewed sunlight.");
                Volume Atmosphere(Scene scene) => scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<Volume>()).Single();
                NfsWorldSetup.Require(Atmosphere(target).sharedProfile == Atmosphere(baseline).sharedProfile,
                    "Art direction must preserve the reviewed atmosphere profile.");
                File.WriteAllText(Path.GetFullPath(Path.Combine(Application.dataPath, "../../artifacts/NfsWorld/DeveloperBArt/scene-validation.json")),
                    "{\"sourceAndExitCollisionExact\":true,\"sunlightUnchanged\":true,\"atmosphereProfileUnchanged\":true}\n");
            }
            finally { EditorSceneManager.CloseScene(baseline, true); SceneManager.SetActiveScene(target); }
        }

        private static string[] CollisionSignatures(Scene scene) => scene.GetRootGameObjects()
            .Where(r => !NfsWorldDistrictRuntimeSetup.Shared(r)).SelectMany(r => r.GetComponentsInChildren<Collider>())
            .Select(c => c is MeshCollider mesh ? "mesh:" + AssetDatabase.GetAssetPath(mesh.sharedMesh) + ":" +
                mesh.sharedMesh.name + ":" + mesh.convex + ":" + c.enabled + ":" + c.transform.localToWorldMatrix.ToString("R") :
                c is BoxCollider box ? "box:" + box.center.ToString("R") + ":" + box.size.ToString("R") + ":" +
                c.enabled + ":" + c.transform.localToWorldMatrix.ToString("R") :
                throw new System.InvalidOperationException("Unexpected district collider: " + c.GetType().Name))
            .OrderBy(s => s, System.StringComparer.Ordinal).ToArray();

        public static void GenerateRuntime()
        {
            if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            Directory.CreateDirectory(DirectoryPath + "/Runtime"); AssetDatabase.Refresh();
            var scene = EditorSceneManager.OpenScene(ScenePath);
            NfsWorldOcclusion.ConfigureScene(scene);
            var car = Object.FindFirstObjectByType<ArcadeCarController>();
            var pose = new DistrictRecoveryPose { id = "source-pit", position = car.transform.position,
                eulerAngles = car.transform.eulerAngles };
            Physics.SyncTransforms();
            var colliders = scene.GetRootGameObjects().Where(r => !NfsWorldDistrictRuntimeSetup.Shared(r))
                .SelectMany(r => r.GetComponentsInChildren<Collider>()).ToArray();
            var bounds = colliders[0].bounds;
            foreach (var collider in colliders.Skip(1)) bounds.Encapsulate(collider.bounds);
            bounds.Expand(new Vector3(8, 10, 8));
            var closures = GameObject.Find("Downtown exit closures");
            NfsWorldSetup.Require(closures != null && closures.transform.childCount == 6, "Retain the six closed exits.");
            var exits = closures.transform.Cast<Transform>().Select((wall, index) => new DistrictConnection
            {
                id = "downtown-exit-" + (index + 1), neighbourId = "unresolved-" + wall.name,
                reciprocalId = "", seamBounds = wall.GetComponent<Renderer>().bounds,
                outward = wall.forward, collisionOwner = "downtown", closure = wall.gameObject, seamVerified = false
            }).ToArray();
            foreach (var root in scene.GetRootGameObjects().Where(NfsWorldDistrictRuntimeSetup.Shared)) Object.DestroyImmediate(root);
            var descriptor = new GameObject("Downtown district metadata").AddComponent<DistrictContent>();
            descriptor.Configure("downtown", bounds, new[] { pose }, exits);
            NfsWorldSetup.Require(descriptor.ValidateContract() == null && descriptor.Supports(pose), "Invalid derived Downtown metadata.");
            EditorSceneManager.SaveScene(scene, ContentScene);
            scene = EditorSceneManager.OpenScene(ScenePath);
            foreach (var root in scene.GetRootGameObjects().Where(r => !NfsWorldDistrictRuntimeSetup.Shared(r))) Object.DestroyImmediate(root);
            new GameObject("Persistent district runtime").AddComponent<DistrictRuntime>().Configure(
                Object.FindFirstObjectByType<ArcadeCarController>(), Object.FindFirstObjectByType<ChaseCamera>(), ContentScene);
            Camera.main.useOcclusionCulling = false;
            EditorSceneManager.SaveScene(scene, RuntimeScene);
            VerifyRuntime();
        }

        public static void VerifyRuntime()
        {
            NfsWorldDistrictRuntimeSetup.Verify(RuntimeScene, ContentScene);
            var content = SceneManager.GetSceneByPath(ContentScene);
            var visuals = content.GetRootGameObjects().Single(r => r.name == "Optimized district visuals");
            var lods = visuals.GetComponent<NfsWorldMeshLods>();
            NfsWorldSetup.Require(lods != null && lods.TargetsBelongTo(content),
                "Generate the visual LODs before runtime assembly; all targets must belong to Downtown content.");
            NfsWorldSetup.Require(visuals.GetComponentsInChildren<MeshRenderer>().All(r =>
                !r.sharedMaterial.IsKeywordEnabled("_ALPHATEST_ON") ||
                (GameObjectUtility.GetStaticEditorFlags(r.gameObject) & StaticEditorFlags.OccluderStatic) == 0),
                "Foliage and other cutout cards must not act as solid runtime occluders.");
        }
        public static void BakeRuntimeOcclusion() => NfsWorldDistrictRuntimeSetup.BakeOcclusion(RuntimeScene, ContentScene);

        [MenuItem("Alabama/Play/Open Styled Game")]
        public static void ConfigureGame()
        {
            if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            NfsWorldSetup.Require(File.Exists(RuntimeScene) && File.Exists(ContentScene),
                "Receive or generate Developer B's styled Downtown assets first.");
            VerifyRuntime();
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(RuntimeScene, true),
                new EditorBuildSettingsScene(ContentScene, true) }.Concat(EditorBuildSettings.scenes
                .Where(s => s.path != RuntimeScene && s.path != ContentScene)
                .Select(s => new EditorBuildSettingsScene(s.path, false))).ToArray();
            EditorSceneManager.OpenScene(RuntimeScene);
            Debug.Log("Styled Downtown is the game entry scene; review courses remain available separately.");
        }

        public static void BuildRuntime() => BuildRuntimeAt("nfs-world-art-direction-runtime");
        public static void BuildGame() => BuildRuntimeAt("windows");

        private static void BuildRuntimeAt(string directory)
        {
            Verify();
            VerifyRuntime();
            PlayerSettings.enableFrameTimingStats = true;
            string output = Path.GetFullPath(Path.Combine(Application.dataPath, "../../builds/" + directory + "/Alabama.exe"));
            Directory.CreateDirectory(Path.GetDirectoryName(output));
            var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
            {
                scenes = new[] { RuntimeScene, ContentScene }, locationPathName = output,
                target = BuildTarget.StandaloneWindows64, options = BuildOptions.StrictMode
            });
            NfsWorldSetup.Require(report.summary.result == BuildResult.Succeeded && report.summary.totalErrors == 0,
                "Middle detail runtime build failed: " + report.summary.result);
        }
    }
}
