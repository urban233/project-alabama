using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Alabama.Driving;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Alabama.Editor
{
    /// <summary>Local visual assets: reduced meshes, source-sized texture arrays and spatial batches.</summary>
    public static class NfsWorldVisualOptimization
    {
        private static string DirectoryPath = NfsWorldSetup.BasePath + "/StylePreview/Optimized";
        private sealed class TextureSlot { public Material material; public int layer; }
        private sealed class Batch
        {
            public Material material;
            public float distance;
            public ShadowCastingMode shadow;
            public List<CombineInstance> meshes = new List<CombineInstance>();
        }

        public static void Run() => RunVariant(false);
        public static void RunArt() => RunVariant(true);
        public static void RunArtDirection() => RunVariant(true, true);

        private static void RunVariant(bool art, bool artDirection = false)
        {
            DirectoryPath = NfsWorldSetup.BasePath + (artDirection ? "/ArtDirection/Optimized" :
                art ? "/ArtPass/Optimized" : "/StylePreview/Optimized");
            var artCandidates = new HashSet<string>();
            if (art)
            {
                EditorSceneManager.OpenScene(NfsWorldStylePreview.LightingStudyScenePath);
                string geometryManifest = NfsWorldArtDirection.DirectoryPath + "/geometry.json";
                artCandidates = NfsWorldArtPass.Prepare(artDirection ? NfsWorldArtDirection.DirectoryPath : NfsWorldSetup.BasePath + "/ArtPass",
                    artDirection && File.Exists(geometryManifest) ? geometryManifest : null);
            }
            else NfsWorldStylePreview.Create();
            var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
            Directory.CreateDirectory(DirectoryPath + "/Meshes");
            Directory.CreateDirectory(DirectoryPath + "/Arrays");
            AssetDatabase.Refresh();
            var shader = Shader.Find("Alabama/Map Texture Array");
            NfsWorldSetup.Require(shader != null, "Map array shader missing.");
            var originals = scene.GetRootGameObjects().Where(root => root.name != "E46 driver car")
                .SelectMany(root => root.GetComponentsInChildren<MeshRenderer>()).Where(r => r.enabled).ToArray();
            var slots = CreateArrays(originals, shader);
            if (artDirection)
                foreach (string layers in Directory.GetFiles(DirectoryPath + "/Arrays", "*.layers.txt"))
                    NfsWorldVisualStability.ConfigureWindowSeparation(layers, NfsWorldVisualStability.WindowLayers(layers));
            var culling = UnityEngine.Object.FindFirstObjectByType<NfsWorldDistanceCulling>();
            var batches = new Dictionary<string, Batch>();
            var temporary = new List<Mesh>();
            int originalTriangles = 0, reducedTriangles = 0;
            var acceptedArtMeshes = new List<string>();
            var rejectedArtMeshes = new List<string>();
            var facetedArtMeshes = new List<string>();
            var contract = NfsWorldSetup.ReadContract("District");
            var definitionsByMaterial = contract.materials.ToDictionary(m => m.name);
            foreach (var part in contract.parts)
            {
                if (!part.meshes.Any(m => m.visible)) continue;
                string modelPath = NfsWorldSetup.BasePath + "/" +
                    (art && File.Exists(NfsWorldSetup.BasePath + "/ArtMeshes/" + part.category + ".fbx") ? "ArtMeshes" : "StyleMeshes") +
                    "/" + part.category + ".fbx";
                string authoredModel = NfsWorldArtDirection.DirectoryPath + "/Geometry/" + part.category + ".fbx";
                if (artDirection && File.Exists(authoredModel)) modelPath = authoredModel;
                var importer = AssetImporter.GetAtPath(modelPath) as ModelImporter;
                NfsWorldSetup.Require(importer != null, "Run the Blender visible-mesh reduction first: " + modelPath);
                importer.globalScale = 1;
                importer.useFileScale = true;
                importer.bakeAxisConversion = false;
                importer.importAnimation = false;
                importer.importNormals = ModelImporterNormals.Import;
                importer.isReadable = true;
                importer.weldVertices = false;
                importer.meshCompression = ModelImporterMeshCompression.Off;
                importer.materialImportMode = ModelImporterMaterialImportMode.None;
                importer.SaveAndReimport();
                var model = AssetDatabase.LoadAssetAtPath<GameObject>(modelPath);
                var replacements = model.GetComponentsInChildren<MeshFilter>().ToDictionary(f => f.name, f => f.sharedMesh);
                var root = GameObject.Find(part.category);
                var filters = root.GetComponentsInChildren<MeshFilter>().ToDictionary(f => f.name);
                // Break only local scene instances before removing their obsolete visual references.
                foreach (Transform child in root.transform)
                    if (PrefabUtility.IsAnyPrefabInstanceRoot(child.gameObject))
                        PrefabUtility.UnpackPrefabInstance(child.gameObject, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
                foreach (var definition in part.meshes.Where(m => m.visible))
                {
                    var filter = filters[definition.name];
                    var renderer = filter.GetComponent<MeshRenderer>();
                    float distance = culling.DistanceFor(renderer);
                    // Each art variant chooses its authored candidates explicitly;
                    // the earlier style-only comparison reduces architecture.
                    bool architectural = part.category == "Buildings" || part.category == "Panorama";
                    bool useCandidate = art ? artCandidates.Contains(definition.name) : architectural && distance < 90000;
                    var mesh = useCandidate ? replacements[definition.name] : filter.sharedMesh;
                    originalTriangles += filter.sharedMesh.triangles.Length / 3;
                    var before = WorldBounds(filter.sharedMesh, filter.transform.localToWorldMatrix);
                    var after = WorldBounds(mesh, filter.transform.localToWorldMatrix);
                    if ((before.min - after.min).magnitude >= .04f || (before.max - after.max).magnitude >= .04f ||
                        !PreservesBoundaryCurves(filter.sharedMesh, mesh, art ? .02f : .003f, artDirection ? .001f : 0))
                    {
                        // FBX can discard loose extrema. Reject this candidate and retain
                        // the original visible mesh rather than relaxing the silhouette check.
                        Debug.Log("Retaining source silhouette after FBX import: " + definition.name);
                        mesh = filter.sharedMesh;
                        after = before;
                    }
                    if (art && useCandidate)
                    {
                        if (mesh == filter.sharedMesh) rejectedArtMeshes.Add(definition.name);
                        else acceptedArtMeshes.Add(definition.name);
                    }
                    // A rejected simplification must still receive the authored
                    // faceted appearance. Preserve every source triangle and UV.
                    bool structuralRoad = new[] { "visroad", "roadfake", "bridge", "roadmid" }
                        .Any(term => definition.sourceName.ToLowerInvariant().Contains(term));
                    bool lodEligible = artDirection && !structuralRoad &&
                        (part.category == "Buildings" || part.category == "Props" || part.category == "Trees" ||
                         part.category == "Panorama" || (part.category == "Terrain" &&
                         new[] { "rock", "cliff" }.Any(t => definition.sourceName.ToLowerInvariant().Contains(t))));
                    bool facetSource = art && mesh == filter.sharedMesh && !structuralRoad &&
                        !definitionsByMaterial[definition.material].alphaClip &&
                        (part.category == "Buildings" || part.category == "Props" ||
                         part.category == "Trees" || part.category == "Walls" || part.category == "Panorama");
                    if (facetSource)
                    {
                        mesh = NfsWorldFacetMeshes.Create(mesh);
                        temporary.Add(mesh);
                        facetedArtMeshes.Add(definition.name);
                    }
                    reducedTriangles += mesh.triangles.Length / 3;
                    var slot = slots[renderer.sharedMaterial];
                    // Material, shadow state and distance class stay separate. 256 m chunks bound culling loss.
                    var baked = Bake(mesh, filter.transform.localToWorldMatrix, slot.layer, renderer.sharedMaterial.GetFloat("_Cutoff"));
                    foreach (var chunk in Partition(baked))
                    {
                        string key = slot.material.name + "_" + chunk.cell.x + "_" + chunk.cell.y + "_" +
                            (int)distance + "_" + (int)renderer.shadowCastingMode;
                        if (artDirection) key += lodEligible ? "_lod" : "_fixed";
                        if (!batches.TryGetValue(key, out var batch))
                        {
                            batch = new Batch { material = slot.material, distance = distance, shadow = renderer.shadowCastingMode };
                            batches.Add(key, batch);
                        }
                        temporary.Add(chunk.mesh);
                        batch.meshes.Add(new CombineInstance { mesh = chunk.mesh, transform = Matrix4x4.identity });
                    }
                    UnityEngine.Object.DestroyImmediate(baked);
                    // Collision keeps its exact source mesh/filter. Remove only superseded rendering.
                    UnityEngine.Object.DestroyImmediate(renderer);
                    if (filter.GetComponent<MeshCollider>() == null) UnityEngine.Object.DestroyImmediate(filter);
                }
            }
            var combinedRoot = new GameObject("Optimized district visuals");
            var targets = new List<Renderer>();
            var distances = new List<float>();
            int batchedTriangles = 0, reviewedRemovals = 0;
            AssetDatabase.StartAssetEditing();
            try
            {
            foreach (var pair in batches)
            {
                var mesh = new Mesh { name = pair.Key, indexFormat = IndexFormat.UInt32 };
                mesh.CombineMeshes(pair.Value.meshes.ToArray(), true, true, false);
                mesh.RecalculateBounds();
                if (artDirection)
                {
                    string layers = DirectoryPath + "/Arrays/" + pair.Value.material.name + ".layers.txt";
                    reviewedRemovals += NfsWorldVisualStability.ResolveWindowOverlaps(mesh, NfsWorldVisualStability.WindowLayers(layers));
                    reviewedRemovals += NfsWorldVisualStability.RemoveLayers(mesh, NfsWorldVisualStability.SuppressedLayers(layers));
                }
                batchedTriangles += mesh.triangles.Length / 3;
                string path = DirectoryPath + "/Meshes/" + pair.Key + ".asset";
                var existing = AssetDatabase.LoadAssetAtPath<Mesh>(path);
                if (existing == null) AssetDatabase.CreateAsset(mesh, path);
                else { EditorUtility.CopySerialized(mesh, existing); UnityEngine.Object.DestroyImmediate(mesh); mesh = existing; }
                var obj = new GameObject(pair.Key);
                obj.transform.SetParent(combinedRoot.transform, false);
                obj.isStatic = true;
                obj.AddComponent<MeshFilter>().sharedMesh = mesh;
                var renderer = obj.AddComponent<MeshRenderer>();
                renderer.sharedMaterial = pair.Value.material;
                renderer.shadowCastingMode = pair.Value.shadow;
                targets.Add(renderer); distances.Add(pair.Value.distance);
            }
            }
            finally { AssetDatabase.StopAssetEditing(); }
            foreach (var mesh in temporary) UnityEngine.Object.DestroyImmediate(mesh);
            NfsWorldSetup.Require(batchedTriangles + reviewedRemovals == reducedTriangles, "Spatial batching lost or duplicated visible triangles.");
            culling.Configure(targets.ToArray(), distances.ToArray());
            EditorUtility.SetDirty(culling);
            // Preserve the approved lighting/shadow/AA settings. This local copy
            // enables SRP material batching without changing the project's pipeline.
            var pipeline = GraphicsSettings.currentRenderPipeline as UniversalRenderPipelineAsset;
            NfsWorldSetup.Require(pipeline != null, "Active URP pipeline missing.");
            string pipelinePath = DirectoryPath + "/BatchedPipeline.asset";
            var batchedPipeline = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(pipelinePath);
            if (batchedPipeline == null)
            {
                batchedPipeline = UnityEngine.Object.Instantiate(pipeline);
                AssetDatabase.CreateAsset(batchedPipeline, pipelinePath);
            }
            else EditorUtility.CopySerialized(pipeline, batchedPipeline);
            batchedPipeline.useSRPBatcher = true;
            ConfigureAntialiasing(batchedPipeline);
            if (art) NfsWorldRenderOptimization.Configure(batchedPipeline, DirectoryPath);
            EditorUtility.SetDirty(batchedPipeline);
            new GameObject("Local map render settings").AddComponent<NfsWorldRenderSettings>().Configure(batchedPipeline, art ? 30 : 0);
            if (art) NfsWorldExitClosures.Apply(artDirection ? NfsWorldArtDirection.DirectoryPath + "/Exits" : null);
            AssetDatabase.SaveAssets();
            EditorSceneManager.SaveScene(scene, artDirection ? NfsWorldArtDirection.ScenePath : art ? NfsWorldArtPass.ScenePath : NfsWorldStylePreview.ScenePath);
            File.WriteAllText(Path.GetFullPath(Path.Combine(Application.dataPath, "../../artifacts/NfsWorld/" +
                (artDirection ? "art-direction-batching.json" : art ? "art-batching.json" : "visual-batching.json"))),
                $"{{\"sourceVisibleMeshes\":{originals.Length},\"batchedMeshes\":{batches.Count},\"arrayMaterials\":{slots.Values.Select(s=>s.material).Distinct().Count()},\"originalTriangles\":{originalTriangles},\"reducedTriangles\":{reducedTriangles},\"chunkMetres\":256,\"collisionChanged\":false}}");
            if (art) NfsWorldArtPass.SaveValidation(acceptedArtMeshes, rejectedArtMeshes, facetedArtMeshes,
                artDirection ? "art-direction-mesh-validation.json" : "art-validation.json");
            Debug.Log($"Optimized visual study: {originalTriangles} -> {reducedTriangles} triangles, {originals.Length} -> {batches.Count} renderers.");
        }

        public static void EfficientAntialiasing()
        {
            var scene = EditorSceneManager.OpenScene(NfsWorldStylePreview.ScenePath);
            var pipeline = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(DirectoryPath + "/BatchedPipeline.asset");
            NfsWorldSetup.Require(pipeline != null, "Generate the optimized visual variant first.");
            ConfigureAntialiasing(pipeline);
            AssetDatabase.SaveAssets();
            EditorSceneManager.SaveScene(scene);
        }

        public static void RestoreProjectAntialiasing()
        {
            var pipeline = GraphicsSettings.currentRenderPipeline as UniversalRenderPipelineAsset;
            NfsWorldSetup.Require(pipeline != null, "Project URP pipeline missing.");
            QualitySettings.antiAliasing = pipeline.msaaSampleCount > 1 ? pipeline.msaaSampleCount : 0;
        }

        private static void ConfigureAntialiasing(UniversalRenderPipelineAsset pipeline)
        {
            // Keep the original sun, cascades, shadow maps and render scale.
            // FXAA smooths edges without the four-sample colour/depth bandwidth.
            pipeline.msaaSampleCount = 1;
            Camera.main.GetUniversalAdditionalCameraData().antialiasing = AntialiasingMode.FastApproximateAntialiasing;
            EditorUtility.SetDirty(pipeline);
        }

        private static Dictionary<Material, TextureSlot> CreateArrays(MeshRenderer[] renderers, Shader shader)
        {
            var result = new Dictionary<Material, TextureSlot>();
            var materials = renderers.Select(r => r.sharedMaterial).Distinct().ToArray();
            // Layer order is part of the contract, even when dimensions/counts match.
            bool reusable = true;
            foreach (var group in materials.GroupBy(ArrayKey))
            {
                var textures = group.Select(m => (Texture2D)m.GetTexture("_BaseMap") ?? Texture2D.whiteTexture).Distinct().ToArray();
                var array = AssetDatabase.LoadAssetAtPath<Texture2DArray>(DirectoryPath + "/Arrays/" + group.Key + ".asset");
                var material = AssetDatabase.LoadAssetAtPath<Material>(DirectoryPath + "/Arrays/" + group.Key + ".mat");
                string layerPath = DirectoryPath + "/Arrays/" + group.Key + ".layers.txt";
                if (!File.Exists(layerPath) || File.ReadAllText(layerPath) != LayerOrder(textures) ||
                    array == null || material == null || material.GetTexture("_BaseArray") != array || array.depth != textures.Length ||
                    array.width != textures[0].width || array.height != textures[0].height || array.format != textures[0].format)
                { reusable = false; break; }
                foreach (var source in group)
                    result.Add(source, new TextureSlot { material = material,
                        layer = Array.IndexOf(textures, (Texture2D)source.GetTexture("_BaseMap") ?? Texture2D.whiteTexture) });
            }
            if (reusable) return result;
            result.Clear();
            var importers = materials.Select(m => m.GetTexture("_BaseMap") as Texture2D)
                .Where(t => t != null && !t.isReadable).Select(t => AssetDatabase.GetAssetPath(t)).Distinct()
                .Select(path => AssetImporter.GetAtPath(path) as TextureImporter).Where(i => i != null).ToArray();
            try
            {
                SetReadable(importers, true);
                AssetDatabase.StartAssetEditing();
                try
                {
                // Preserve texture resolution, compression, alpha and mipmaps; no atlas UV distortion.
                var groups = materials.GroupBy(ArrayKey);
                foreach (var group in groups)
                {
                    var textures = group.Select(m => (Texture2D)m.GetTexture("_BaseMap") ?? Texture2D.whiteTexture).Distinct().ToArray();
                    var first = textures[0];
                    var array = new Texture2DArray(first.width, first.height, textures.Length, first.format, first.mipmapCount > 1, false)
                    { name = group.Key, wrapMode = TextureWrapMode.Repeat, anisoLevel = 4, filterMode = FilterMode.Trilinear };
                    for (int layer = 0; layer < textures.Length; layer++)
                        for (int mip = 0; mip < textures[layer].mipmapCount; mip++)
                            array.SetPixelData(textures[layer].GetPixelData<byte>(mip), mip, layer);
                    array.Apply(false, false);
                    string arrayPath = DirectoryPath + "/Arrays/" + group.Key + ".asset";
                    var existing = AssetDatabase.LoadAssetAtPath<Texture2DArray>(arrayPath);
                    if (existing == null) AssetDatabase.CreateAsset(array, arrayPath);
                    else { EditorUtility.CopySerialized(array, existing); UnityEngine.Object.DestroyImmediate(array); array = existing; }
                    string materialPath = DirectoryPath + "/Arrays/" + group.Key + ".mat";
                    var material = AssetDatabase.LoadAssetAtPath<Material>(materialPath);
                    if (material == null) { material = new Material(shader) { name = group.Key }; AssetDatabase.CreateAsset(material, materialPath); }
                    material.SetTexture("_BaseArray", array);
                    material.SetColor("_BaseColor", group.First().GetColor("_BaseColor"));
                    material.SetFloat("_Smoothness", .06f);
                    bool alpha = group.First().IsKeywordEnabled("_ALPHATEST_ON");
                    if (alpha) material.EnableKeyword("_ALPHATEST_ON"); else material.DisableKeyword("_ALPHATEST_ON");
                    material.renderQueue = alpha ? (int)RenderQueue.AlphaTest : (int)RenderQueue.Geometry;
                    EditorUtility.SetDirty(material);
                    File.WriteAllText(DirectoryPath + "/Arrays/" + group.Key + ".layers.txt", LayerOrder(textures));
                    foreach (var source in group)
                    {
                        var texture = (Texture2D)source.GetTexture("_BaseMap") ?? Texture2D.whiteTexture;
                        result.Add(source, new TextureSlot { material = material, layer = Array.IndexOf(textures, texture) });
                    }
                }
                }
                finally { AssetDatabase.StopAssetEditing(); }
            }
            finally
            {
                SetReadable(importers, false);
            }
            // Dependency hashes include importer settings. Record the final restored
            // state, so temporary CPU-readable imports do not invalidate every cache.
            foreach (var group in materials.GroupBy(ArrayKey))
            {
                var textures = group.Select(m => (Texture2D)m.GetTexture("_BaseMap") ?? Texture2D.whiteTexture).Distinct().ToArray();
                File.WriteAllText(DirectoryPath + "/Arrays/" + group.Key + ".layers.txt", LayerOrder(textures));
            }
            return result;
        }

        private static void SetReadable(TextureImporter[] importers, bool readable)
        {
            AssetDatabase.StartAssetEditing();
            try
            {
                foreach (var importer in importers)
                {
                    importer.isReadable = readable;
                    importer.SaveAndReimport();
                }
            }
            finally { AssetDatabase.StopAssetEditing(); }
        }

        private static Mesh Bake(Mesh source, Matrix4x4 matrix, int layer, float cutoff)
        {
            var mesh = new Mesh { indexFormat = IndexFormat.UInt32 };
            mesh.vertices = source.vertices.Select(matrix.MultiplyPoint3x4).ToArray();
            var normalMatrix = matrix.inverse.transpose;
            mesh.normals = source.normals.Select(n => normalMatrix.MultiplyVector(n).normalized).ToArray();
            var uv = source.uv;
            NfsWorldSetup.Require(uv.Length == source.vertexCount &&
                uv.All(value => float.IsFinite(value.x) && float.IsFinite(value.y)),
                "Missing or non-finite texture coordinates: " + source.name);
            mesh.uv = uv;
            mesh.SetUVs(2, Enumerable.Repeat(new Vector2(layer, cutoff), source.vertexCount).ToList());
            var indices = source.triangles;
            if (matrix.determinant < 0)
                for (int index = 0; index < indices.Length; index += 3)
                { int value = indices[index]; indices[index] = indices[index + 2]; indices[index + 2] = value; }
            mesh.triangles = indices;
            mesh.RecalculateBounds();
            return mesh;
        }

        private static Bounds WorldBounds(Mesh mesh, Matrix4x4 matrix)
        {
            var vertices = mesh.vertices;
            var bounds = new Bounds(matrix.MultiplyPoint3x4(vertices[0]), Vector3.zero);
            foreach (var vertex in vertices) bounds.Encapsulate(matrix.MultiplyPoint3x4(vertex));
            return bounds;
        }

        private static string ArrayKey(Material material)
        {
            var texture = (Texture2D)material.GetTexture("_BaseMap") ?? Texture2D.whiteTexture;
            return texture.width + "x" + texture.height + "_" + texture.format + "_" +
                (texture.mipmapCount > 1) + "_" + material.IsKeywordEnabled("_ALPHATEST_ON");
        }

        private static string LayerOrder(Texture2D[] textures) => string.Join("\n", textures.Select(texture =>
            texture == Texture2D.whiteTexture ? "builtin-white" :
            AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(texture)) + ":" +
            AssetDatabase.GetAssetDependencyHash(AssetDatabase.GetAssetPath(texture))));

        internal static bool PreservesBoundaryCurves(Mesh original, Mesh candidate, float tolerance, float weldTolerance = 0)
        {
            if (original == candidate) return true;
            Vector3Int Key(Vector3 point) => new Vector3Int(Mathf.RoundToInt(point.x * 1000),
                Mathf.RoundToInt(point.y * 1000), Mathf.RoundToInt(point.z * 1000));
            List<(Vector3Int a, Vector3Int b)> Boundaries(Mesh mesh)
            {
                // FBX splits vertices at UV/normal seams. Count geometric edges so
                // those intentional splits do not classify every triangle as open.
                var points = mesh.vertices;
                var keys = points.Select(Key).ToArray();
                if (weldTolerance > 0)
                {
                    // KN5 seams can differ by fractions of a millimetre. Search
                    // adjacent buckets so rounding cannot invent open boundaries.
                    var representatives = new List<Vector3>();
                    var buckets = new Dictionary<Vector3Int, List<int>>();
                    for (int vertex = 0; vertex < points.Length; vertex++)
                    {
                        var point = points[vertex];
                        var cell = new Vector3Int(Mathf.FloorToInt(point.x / weldTolerance),
                            Mathf.FloorToInt(point.y / weldTolerance), Mathf.FloorToInt(point.z / weldTolerance));
                        int match = -1;
                        for (int x = -1; x <= 1 && match < 0; x++)
                            for (int y = -1; y <= 1 && match < 0; y++)
                                for (int z = -1; z <= 1 && match < 0; z++)
                                    if (buckets.TryGetValue(cell + new Vector3Int(x, y, z), out var nearby))
                                        foreach (int index in nearby)
                                            if ((representatives[index] - point).sqrMagnitude <= weldTolerance * weldTolerance)
                                            { match = index; break; }
                        if (match < 0)
                        {
                            match = representatives.Count; representatives.Add(point);
                            if (!buckets.TryGetValue(cell, out var bucketIndices)) buckets[cell] = bucketIndices = new List<int>();
                            bucketIndices.Add(match);
                        }
                        keys[vertex] = Key(representatives[match]);
                    }
                }
                var unique = keys.Distinct().ToArray();
                var ids = unique.Select((key, id) => (key, id)).ToDictionary(pair => pair.key, pair => pair.id);
                var edges = new Dictionary<(int a, int b), int>();
                var indices = mesh.triangles;
                for (int index = 0; index < indices.Length; index += 3)
                    for (int corner = 0; corner < 3; corner++)
                    {
                        int a = ids[keys[indices[index + corner]]], b = ids[keys[indices[index + (corner + 1) % 3]]];
                        if (a == b) continue;
                        var edge = (Mathf.Min(a, b), Mathf.Max(a, b));
                        edges.TryGetValue(edge, out int count); edges[edge] = count + 1;
                    }
                var result = new List<(Vector3Int a, Vector3Int b)>();
                foreach (var edge in edges.Where(pair => pair.Value == 1))
                    result.Add((unique[edge.Key.a], unique[edge.Key.b]));
                return result;
            }
            bool Covers(List<(Vector3Int a, Vector3Int b)> segments, List<(Vector3Int a, Vector3Int b)> target)
            {
                var exact = new HashSet<Vector3Int>(segments.SelectMany(edge => new[] { edge.a, edge.b }));
                var exactEdges = new HashSet<(Vector3Int a, Vector3Int b)>(segments);
                foreach (var point in target.Where(edge => !exactEdges.Contains(edge) &&
                    !exactEdges.Contains((edge.b, edge.a))).SelectMany(edge => new[] { edge.a, edge.b,
                    Vector3Int.RoundToInt(((Vector3)edge.a + edge.b) * .5f) }).Distinct())
                {
                    if (exact.Contains(point)) continue;
                    bool onSegment = segments.Any(edge =>
                    {
                        Vector3 direction = (Vector3)edge.b - edge.a;
                        float along = Mathf.Clamp01(Vector3.Dot((Vector3)point - edge.a, direction) / direction.sqrMagnitude);
                        // Keys round three coordinates to 1 mm; rounding both the
                        // endpoints and query can introduce up to 3 mm of noise.
                        return Vector3.SqrMagnitude((Vector3)edge.a + direction * along - point) <=
                            tolerance * tolerance * 1000000;
                    });
                    if (!onSegment) return false;
                }
                return true;
            }
            var before = Boundaries(original);
            var after = Boundaries(candidate);
            // Collinear boundary vertices may disappear, but the same boundary
            // curves must remain in both directions. Newly opened holes fail.
            return Covers(before, after) && Covers(after, before);
        }

        public static IEnumerable<(Vector2Int cell, Mesh mesh)> Partition(Mesh source, int cellMetres = 256)
        {
            var vertices = source.vertices;
            var normals = source.normals;
            var uv = source.uv;
            var layers = new List<Vector2>(); source.GetUVs(2, layers);
            var triangles = source.triangles;
            var cells = new Dictionary<Vector2Int, List<int>>();
            for (int index = 0; index < triangles.Length; index += 3)
            {
                var centre = (vertices[triangles[index]] + vertices[triangles[index + 1]] + vertices[triangles[index + 2]]) / 3;
                var cell = new Vector2Int(Mathf.FloorToInt(centre.x / cellMetres), Mathf.FloorToInt(centre.z / cellMetres));
                if (!cells.TryGetValue(cell, out var indices)) { indices = new List<int>(); cells.Add(cell, indices); }
                indices.Add(triangles[index]); indices.Add(triangles[index + 1]); indices.Add(triangles[index + 2]);
            }
            foreach (var pair in cells)
            {
                var map = new Dictionary<int, int>();
                var compact = new List<int>();
                var indices = new int[pair.Value.Count];
                for (int index = 0; index < indices.Length; index++)
                {
                    int original = pair.Value[index];
                    if (!map.TryGetValue(original, out var value))
                    { value = compact.Count; map.Add(original, value); compact.Add(original); }
                    indices[index] = value;
                }
                var mesh = new Mesh { indexFormat = IndexFormat.UInt32 };
                mesh.vertices = compact.Select(index => vertices[index]).ToArray();
                mesh.normals = compact.Select(index => normals[index]).ToArray();
                mesh.uv = compact.Select(index => uv[index]).ToArray();
                mesh.SetUVs(2, compact.Select(index => layers[index]).ToList());
                mesh.triangles = indices;
                mesh.RecalculateBounds();
                yield return (pair.Key, mesh);
            }
        }
    }
}
