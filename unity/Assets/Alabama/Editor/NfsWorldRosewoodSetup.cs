using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using Alabama.Districts;
using Alabama.Driving;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace Alabama.Editor
{
    /// <summary>A-owned neighbouring content and paired seam; B's persistent host is retained.</summary>
    public static class NfsWorldRosewoodSetup
    {
        public const string Root = NfsWorldSetup.BasePath + "/Rosewood";
        public const string ScenePath = Root + "/RosewoodContent.unity";
        private static readonly Vector3 Origin = new Vector3(1978.0977783f, 99.6069107f, 1165.7668457f);
        [Serializable] private sealed class MeshReport { public bool collisionChanged; public bool finiteCoordinatesValidated; public MeshRecord[] meshes; }
        [Serializable] private sealed class MeshRecord { public string name; public bool changed; }
        [Serializable] private sealed class Textures { public TextureRecord[] textures; }
        [Serializable] private sealed class TextureRecord { public string source; public string file; public bool alpha; }
        [Serializable] private sealed class ExitManifest { public bool reviewed; public Exit[] closures; }
        [Serializable] private sealed class Exit { public float[] start; public float[] end; public float[] outward; public string name; }
        [Serializable] private sealed class Qualification { public bool passed; public string signature; public int seamCrossings; public int cameraRenders; }
        [Serializable] private sealed class CollisionRecipe { public bool originalModelsChanged; public string owner; public CollisionMesh[] meshes; }
        [Serializable] private sealed class CollisionMesh { public string name; public Vector3[] worldVertices; public int[] indices; public float removedOverlapSquareMetres; }
        [Serializable] private sealed class RouteData { public Vector3 seamCentre; public Vector3 seamOutward; public Vector3 recoveryPosition; public Vector3 recoveryForward; }
        private static Vector3 Point(float[] value) => new Vector3(value[0], value[1], value[2]);
        private static string Evidence(string file) => Path.GetFullPath(Path.Combine(Application.dataPath, "../../artifacts/NfsWorld/Rosewood/" + file));

        public static void Generate()
        {
            if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            var contract = JsonUtility.FromJson<NfsWorldSetup.Contract>(File.ReadAllText(Root + "/District/contract.json"));
            NfsWorldSetup.Require(contract.districtId == "rosewood" && Vector3.Distance(Point(contract.sourceOrigin), Origin) < .0001f,
                "Rosewood must retain Downtown's source origin.");
            Directory.CreateDirectory(Root + "/Materials"); AssetDatabase.Refresh();
            var materials = NfsWorldSetup.CreateMaterials(contract, Root);
            var replacementTextures = JsonUtility.FromJson<Textures>(File.ReadAllText(Root + "/ArtPass/textures.json"));
            foreach (var entry in replacementTextures.textures)
            {
                string path = Root + "/ArtPass/Textures/" + entry.file;
                var importer = AssetImporter.GetAtPath(path) as TextureImporter;
                NfsWorldSetup.Require(importer != null, "Missing Rosewood baked texture: " + path);
                importer.sRGBTexture = true; importer.alphaSource = TextureImporterAlphaSource.FromInput;
                importer.alphaIsTransparency = entry.alpha; importer.mipmapEnabled = true;
                importer.wrapMode = TextureWrapMode.Repeat; importer.anisoLevel = 4;
                importer.maxTextureSize = 2048; importer.textureCompression = TextureImporterCompression.Compressed;
                importer.SaveAndReimport();
                var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
                var original = AssetDatabase.LoadAssetAtPath<Texture2D>(Root + "/Textures/" + entry.source);
                NfsWorldSetup.Require(texture.width == original.width && texture.height == original.height, "Texture aspect/resolution changed.");
                foreach (var definition in contract.materials.Where(m => m.texture == entry.source))
                {
                    NfsWorldSetup.Require(definition.alphaClip == entry.alpha || entry.alpha, "Cutout alpha was lost.");
                    materials[definition.name].SetTexture("_BaseMap", texture);
                    EditorUtility.SetDirty(materials[definition.name]);
                }
            }
            foreach (var part in contract.parts)
            {
                string path = Root + "/District/" + part.file;
                var importer = AssetImporter.GetAtPath(path) as ModelImporter;
                NfsWorldSetup.Require(importer != null, "Missing Rosewood model: " + path);
                importer.globalScale = 1; importer.useFileScale = true; importer.bakeAxisConversion = false;
                importer.importAnimation = false; importer.importNormals = ModelImporterNormals.Import;
                importer.importTangents = ModelImporterTangents.CalculateMikk; importer.isReadable = true;
                importer.meshCompression = ModelImporterMeshCompression.Off; importer.weldVertices = false;
                importer.materialImportMode = ModelImporterMaterialImportMode.None; importer.SaveAndReimport();
            }
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            foreach (var part in contract.parts)
            {
                var wrapper = new GameObject(part.category);
                var model = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(Root + "/District/" + part.file));
                model.transform.SetParent(wrapper.transform, false);
                NfsWorldSetup.AlignAxes(wrapper.transform, model.transform);
                var nodes = model.GetComponentsInChildren<Transform>().ToDictionary(t => t.name);
                foreach (var definition in part.meshes)
                {
                    var node = nodes[definition.name]; var filter = node.GetComponent<MeshFilter>();
                    var renderer = node.GetComponent<MeshRenderer>();
                    NfsWorldSetup.Require(filter.sharedMesh.triangles.Length / 3 == definition.triangles, "Imported triangle count changed: " + definition.name);
                    var low = new Vector3(-definition.boundsMax[0], definition.boundsMin[1], definition.boundsMin[2]);
                    var high = new Vector3(-definition.boundsMin[0], definition.boundsMax[1], definition.boundsMax[2]);
                    NfsWorldSetup.Require(Vector3.Distance(renderer.bounds.min, low) < .02f && Vector3.Distance(renderer.bounds.max, high) < .02f,
                        "Source/Unity bounds changed: " + definition.name);
                    renderer.enabled = definition.visible;
                    renderer.shadowCastingMode = definition.visible && definition.castsShadows ? ShadowCastingMode.On : ShadowCastingMode.Off;
                    renderer.sharedMaterial = materials[definition.material];
                    if (definition.collision) node.gameObject.AddComponent<MeshCollider>().sharedMesh = filter.sharedMesh;
                }
            }
            ApplySeamCollision();
            NfsWorldOptimization.Apply(contract, true);
            var art = JsonUtility.FromJson<MeshReport>(File.ReadAllText(Evidence("art-meshes.json")));
            NfsWorldSetup.Require(!art.collisionChanged && art.finiteCoordinatesValidated, "Rosewood art candidates require validation.");
            NfsWorldVisualOptimization.RunContent(Root, new HashSet<string>(art.meshes.Where(m => m.changed).Select(m => m.name)));
            AddConnectorStudyFill();
            EditorSceneManager.SaveScene(scene, ScenePath);
            NfsWorldShadowProxies.RunContent(scene, Root);
            var exits = JsonUtility.FromJson<ExitManifest>(File.ReadAllText(Root + "/ArtPass/exits.json"));
            NfsWorldSetup.Require(exits.reviewed && exits.closures.Length == 9, "Review all nine audited Rosewood exits.");
            NfsWorldExitClosures.ApplyContent(Root, "Rosewood", 9);
            EditorSceneManager.SaveScene(scene, ScenePath);
            FinalizeLoadedContent(scene, contract, exits);
        }

        // Resume a saved geometry checkpoint after a metadata/support failure.
        public static void FinalizeContent()
        {
            var scene = EditorSceneManager.OpenScene(ScenePath);
            var contract = JsonUtility.FromJson<NfsWorldSetup.Contract>(File.ReadAllText(Root + "/District/contract.json"));
            var exits = JsonUtility.FromJson<ExitManifest>(File.ReadAllText(Root + "/ArtPass/exits.json"));
            NfsWorldExitClosures.ApplyContent(Root, "Rosewood", 9);
            FinalizeLoadedContent(scene, contract, exits);
        }

        private static void FinalizeLoadedContent(Scene scene, NfsWorldSetup.Contract contract, ExitManifest exits)
        {
            Physics.SyncTransforms();
            var route = JsonUtility.FromJson<RouteData>(File.ReadAllText(Root + "/qualification-route.json"));
            var sourceSpawn = route.recoveryPosition;
            var spawnHits = Physics.RaycastAll(sourceSpawn + Vector3.up * 3, Vector3.down, 6)
                .Where(h => h.collider.transform.root.name == "RoadsPhysical" && h.normal.y > .5f)
                .OrderBy(h => Mathf.Abs(h.point.y - sourceSpawn.y)).ToArray();
            NfsWorldSetup.Require(spawnHits.Length > 0, "Rosewood's nearby source lane lacks physical road.");
            var forward = route.recoveryForward.normalized;
            var pose = new DistrictRecoveryPose { id = "source-lane-near-pit", position = spawnHits[0].point + Vector3.up * .24f,
                eulerAngles = Quaternion.LookRotation(forward).eulerAngles };
            var colliders = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<Collider>()).ToArray();
            var bounds = colliders[0].bounds; foreach (var collider in colliders.Skip(1)) bounds.Encapsulate(collider.bounds);
            bounds.Expand(new Vector3(8, 10, 8));
            var walls = GameObject.Find("Rosewood exit closures").transform.Cast<Transform>().ToArray();
            var connections = walls.Select((w, i) => new DistrictConnection { id = "rosewood-exit-" + (i + 1),
                neighbourId = exits.closures[i].name == "DowntownRockport" ? "downtown" : exits.closures[i].name.ToLowerInvariant(),
                reciprocalId = i == 6 ? "downtown-exit-3" : "", seamBounds = w.GetComponent<Renderer>().bounds,
                outward = Point(exits.closures[i].outward), collisionOwner = "rosewood", closure = w.gameObject }).ToArray();
            var existing = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<DistrictContent>()).ToArray();
            foreach (var previous in existing) UnityEngine.Object.DestroyImmediate(previous.gameObject);
            var content = new GameObject("Rosewood district metadata").AddComponent<DistrictContent>();
            content.Configure("rosewood", bounds, new[] { pose }, connections);
            NfsWorldSetup.Require(content.ValidateContract() == null, content.ValidateContract());
            NfsWorldSetup.Require(content.Supports(pose), "Rosewood recovery footprint is unsupported or obstructed.");
            AssetDatabase.SaveAssets(); EditorSceneManager.SaveScene(scene, ScenePath);
            ConfigureCombined(); Verify();
        }

        public static string Signature()
        {
            using var hash = SHA256.Create();
            var bytes = System.Text.Encoding.UTF8.GetBytes(AssetDatabase.GetAssetDependencyHash(ScenePath).ToString() +
                AssetDatabase.GetAssetDependencyHash(NfsWorldDistrictRuntimeSetup.DowntownScene).ToString() +
                AssetDatabase.GetAssetDependencyHash(Root + "/qualification-route.json").ToString() +
                AssetDatabase.GetAssetDependencyHash("Assets/Alabama/Runtime/Districts/RosewoodQualificationProbe.cs").ToString());
            return BitConverter.ToString(hash.ComputeHash(bytes)).Replace("-", "").ToLowerInvariant();
        }

        private static void ApplySeamCollision()
        {
            var recipe = JsonUtility.FromJson<CollisionRecipe>(File.ReadAllText(Root + "/seam-collision.json"));
            NfsWorldSetup.Require(!recipe.originalModelsChanged && recipe.owner == "downtown", "Seam collision ownership is invalid.");
            string directory = Root + "/SeamCollision";
            Directory.CreateDirectory(directory); AssetDatabase.Refresh();
            var filters = GameObject.Find("RoadsPhysical").GetComponentsInChildren<MeshFilter>().ToDictionary(f => f.name);
            foreach (var item in recipe.meshes)
            {
                var filter = filters[item.name]; var collider = filter.GetComponent<MeshCollider>();
                NfsWorldSetup.Require(collider != null && !filter.GetComponent<MeshRenderer>().enabled && item.removedOverlapSquareMetres > 0,
                    "Only hidden source road collision may receive the seam ownership cut.");
                var mesh = new Mesh { name = item.name + " exclusive seam", indexFormat = IndexFormat.UInt32 };
                mesh.vertices = item.worldVertices.Select(filter.transform.InverseTransformPoint).ToArray();
                mesh.triangles = item.indices; mesh.RecalculateNormals(); mesh.RecalculateBounds();
                string path = directory + "/" + item.name + ".asset";
                var existing = AssetDatabase.LoadAssetAtPath<Mesh>(path);
                if (existing == null) AssetDatabase.CreateAsset(mesh, path);
                else { EditorUtility.CopySerialized(mesh, existing); UnityEngine.Object.DestroyImmediate(mesh); mesh = existing; }
                filter.sharedMesh = mesh; collider.sharedMesh = mesh;
            }
        }

        public static void ConfigureCombined()
        {
            var scene = EditorSceneManager.OpenScene(NfsWorldDistrictRuntimeSetup.RuntimeScene);
            var runtime = UnityEngine.Object.FindFirstObjectByType<DistrictRuntime>();
            runtime.Configure(UnityEngine.Object.FindFirstObjectByType<ArcadeCarController>(), UnityEngine.Object.FindFirstObjectByType<ChaseCamera>(),
                NfsWorldDistrictRuntimeSetup.DowntownScene, ScenePath);
            Camera.main.useOcclusionCulling = false; // Downtown-only bake must not hide Rosewood.
            var probe = runtime.GetComponent<RosewoodQualificationProbe>() ?? runtime.gameObject.AddComponent<RosewoodQualificationProbe>();
            probe.Configure(AssetDatabase.LoadAssetAtPath<TextAsset>(Root + "/qualification-route.json"), Signature());
            ConfigureBuildSettings();
            EditorSceneManager.SaveScene(scene);
        }

        private static void AddConnectorStudyFill()
        {
            // One short tunnel section receives cool bounce fill. The shared sun,
            // sky, fog and exposure remain owned by the persistent runtime scene.
            var root = new GameObject("Rosewood connector study fill");
            var positions = new[]
            {
                new Vector3(485f, 29f, -1086f),
                new Vector3(510f, 29f, -1083f),
                new Vector3(535f, 28f, -1081f),
            };
            for (int i = 0; i < positions.Length; i++)
            {
                var light = new GameObject("Cool tunnel fill " + (i + 1)).AddComponent<Light>();
                light.transform.SetParent(root.transform, false);
                light.transform.position = positions[i];
                light.type = LightType.Point;
                light.color = new Color(.76f, .82f, .89f);
                light.intensity = 2.2f;
                light.range = 24f;
                light.shadows = LightShadows.None;
            }
        }

        private static void ConfigureBuildSettings()
        {
            var paths = NfsWorldDistrictRuntimeSetup.BuildScenes;
            var retained = EditorBuildSettings.scenes.Where(s => !paths.Contains(s.path));
            EditorBuildSettings.scenes = paths.Select(p => new EditorBuildSettingsScene(p, true)).Concat(retained).ToArray();
        }

        public static void Verify()
        {
            NfsWorldDistrictRuntimeSetup.Verify();
            var host = SceneManager.GetActiveScene();
            var rosewood = SceneManager.GetSceneByPath(ScenePath);
            if (!rosewood.isLoaded) rosewood = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Additive);
            NfsWorldRuntimeMeshPackaging.Apply(rosewood, Root, Root + "/RuntimeMeshes");
            NfsWorldRuntimeMeshPackaging.Apply(SceneManager.GetSceneByPath(NfsWorldDistrictRuntimeSetup.DowntownScene),
                NfsWorldSetup.BasePath + "/ArtPass", NfsWorldSetup.BasePath + "/Runtime/DowntownMeshes");
            NfsWorldValidation.VerifyLoaded("Rosewood", rosewood);
            var data = Descriptor(rosewood);
            NfsWorldSetup.Require(data.ValidateContract() == null, data.ValidateContract());
            NfsWorldSetup.Require(data.RecoveryPoses.All(data.Supports), "Rosewood spawn lost scene-owned support.");
            NfsWorldSetup.Require(SceneManager.GetActiveScene() == host, "Rosewood must not change the active host.");
            NfsWorldSetup.Require(UnityEngine.Object.FindObjectsByType<Camera>(FindObjectsSortMode.None).Length == 1, "Duplicate camera in combined districts.");
            var downtown = Descriptor(SceneManager.GetSceneByPath(NfsWorldDistrictRuntimeSetup.DowntownScene));
            NfsWorldSetup.Require(downtown.Connections.Count(c => c.closure != null && c.closure.activeSelf) >= 5, "Unqualified Downtown exits must stay closed.");
            VerifySeamSurfaces();
            Debug.Log("Combined runtime and Rosewood content verified; source frame, collision, recovery and retained closures valid.");
        }

        private static void VerifySeamSurfaces()
        {
            Physics.SyncTransforms();
            var route = JsonUtility.FromJson<RouteData>(File.ReadAllText(Root + "/qualification-route.json"));
            var side = Vector3.Cross(Vector3.up, route.seamOutward).normalized;
            int samples = 0;
            for (float along = -15; along <= 15; along += .5f)
                foreach (float across in new[] { -8f, -4f, 0f, 4f, 8f })
                {
                    var probe = route.seamCentre + route.seamOutward * along + side * across;
                    var hits = Physics.RaycastAll(probe + Vector3.up * 5, Vector3.down, 10)
                        .Where(h => h.collider.transform.root.name == "RoadsPhysical" && h.normal.y > .5f)
                        .OrderBy(h => Mathf.Abs(h.point.y - probe.y)).ToArray();
                    NfsWorldSetup.Require(hits.Length > 0 && Mathf.Abs(hits[0].point.y - probe.y) < 2, "Missing or wrong road level at seam: " + probe);
                    NfsWorldSetup.Require(hits.Where(h => Mathf.Abs(h.point.y - hits[0].point.y) < .05f)
                        .Select(h => h.collider.gameObject.scene.handle).Distinct().Count() == 1,
                        "Duplicate district collision at seam: " + probe);
                    samples++;
                }
            File.WriteAllText(Evidence("seam-surfaces.json"), $"{{\"passed\":true,\"samples\":{samples},\"missingSurfaces\":0,\"duplicateDistrictContacts\":0}}");
        }

        private static DistrictContent Descriptor(Scene scene) => scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<DistrictContent>()).Single();

        public static void AcceptSeam()
        {
            string reportPath = Evidence("player-seam.json");
            var report = JsonUtility.FromJson<Qualification>(File.ReadAllText(reportPath));
            NfsWorldSetup.Require(report.passed && report.signature == Signature() && report.seamCrossings >= 2 && report.cameraRenders > 120,
                "A current rendered both-direction seam qualification is required.");
            Verify();
            var downtown = Descriptor(SceneManager.GetSceneByPath(NfsWorldDistrictRuntimeSetup.DowntownScene));
            var rosewood = Descriptor(SceneManager.GetSceneByPath(ScenePath));
            var selected = downtown.Connections.Single(c => c.id == "downtown-exit-3");
            var centre = selected.seamBounds.center + new Vector3(.9855065f, 0, .1696375f) * 5;
            var seam = new Bounds(centre, new Vector3(40, 12, 40));
            Open(downtown, "downtown-exit-3", "rosewood-exit-7", "rosewood", seam, new Vector3(.9855065f, 0, .1696375f));
            Open(rosewood, "rosewood-exit-7", "downtown-exit-3", "downtown", seam, new Vector3(-.9855065f, 0, -.1696375f));
            AssetDatabase.SaveAssets();
            ConfigureCombined();
        }

        private static void Open(DistrictContent data, string id, string reciprocal, string neighbour, Bounds seam, Vector3 outward)
        {
            var exits = data.Connections;
            for (int i = 0; i < exits.Length; i++) if (exits[i].id == id)
            {
                exits[i].neighbourId = neighbour; exits[i].reciprocalId = reciprocal; exits[i].seamBounds = seam;
                exits[i].outward = outward; exits[i].collisionOwner = "downtown-overlap-owner; otherwise-scene-owned-source";
                exits[i].seamVerified = true; exits[i].closure.SetActive(false);
            }
            data.Configure(data.Id, data.Bounds, data.RecoveryPoses, exits, data.MinimumRecoveryHeight);
            if (data.GetComponent<DistrictConnectionGate>() == null) data.gameObject.AddComponent<DistrictConnectionGate>();
            EditorSceneManager.SaveScene(data.gameObject.scene);
        }

        public static void Build()
        {
            ConfigureBuildSettings();
            Verify(); PlayerSettings.enableFrameTimingStats = true;
            var probe = UnityEngine.Object.FindFirstObjectByType<RosewoodQualificationProbe>();
            NfsWorldSetup.Require(probe != null, "The saved combined host requires its qualification probe.");
            var downtown = Descriptor(SceneManager.GetSceneByPath(NfsWorldDistrictRuntimeSetup.DowntownScene));
            var rosewood = Descriptor(SceneManager.GetSceneByPath(ScenePath));
            bool accepted = downtown.Connections.Single(c => c.id == "downtown-exit-3").seamVerified &&
                rosewood.Connections.Single(c => c.id == "rosewood-exit-7").seamVerified;
            // The accepted host carries the publisher's evidence identity. Importer dependency
            // hashes can differ on another machine; rebuilding must preserve received assets.
            // A regenerated trial still needs a fresh identity before its qualification run.
            if (!accepted)
            {
                probe.Configure(AssetDatabase.LoadAssetAtPath<TextAsset>(Root + "/qualification-route.json"), Signature());
                EditorUtility.SetDirty(probe); EditorSceneManager.SaveScene(probe.gameObject.scene);
            }
            string output = Path.GetFullPath(Path.Combine(Application.dataPath, "../../builds/nfs-world-rosewood/Alabama.exe"));
            Directory.CreateDirectory(Path.GetDirectoryName(output));
            var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions { scenes = NfsWorldDistrictRuntimeSetup.BuildScenes.Concat(new[] { ScenePath }).Distinct().ToArray(),
                locationPathName = output, target = BuildTarget.StandaloneWindows64, options = BuildOptions.StrictMode });
            NfsWorldSetup.Require(report.summary.result == BuildResult.Succeeded && report.summary.totalErrors == 0, "Combined district build failed.");
        }

        private static double bakeDeadline;
        public static void BakeOcclusion()
        {
            Verify(); Camera.main.useOcclusionCulling = true;
            EditorSceneManager.SaveOpenScenes();
            StaticOcclusionCulling.smallestOccluder = 10; StaticOcclusionCulling.smallestHole = 4;
            StaticOcclusionCulling.backfaceThreshold = 100;
            NfsWorldSetup.Require(StaticOcclusionCulling.Compute(), "Combined occlusion bake did not start.");
            bakeDeadline = EditorApplication.timeSinceStartup + 1200;
            EditorApplication.update += FinishBake;
        }

        private static void FinishBake()
        {
            if (StaticOcclusionCulling.isRunning && EditorApplication.timeSinceStartup < bakeDeadline) return;
            EditorApplication.update -= FinishBake;
            if (StaticOcclusionCulling.isRunning)
            { StaticOcclusionCulling.Cancel(); Debug.LogError("Combined occlusion bake exceeded twenty minutes."); if (Application.isBatchMode) EditorApplication.Exit(1); return; }
            EditorSceneManager.SaveOpenScenes(); AssetDatabase.SaveAssets();
            var probe = UnityEngine.Object.FindFirstObjectByType<RosewoodQualificationProbe>();
            probe.Configure(AssetDatabase.LoadAssetAtPath<TextAsset>(Root + "/qualification-route.json"), Signature());
            EditorUtility.SetDirty(probe); EditorSceneManager.SaveScene(probe.gameObject.scene);
            Debug.Log("Combined host, Downtown and Rosewood occlusion baked.");
            if (Application.isBatchMode) EditorApplication.Exit(0);
        }
    }
}
