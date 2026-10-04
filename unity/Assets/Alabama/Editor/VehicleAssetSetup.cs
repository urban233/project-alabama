using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace Alabama.Editor
{
    /// <summary>Explicit, repeatable Blender-to-Unity vehicle import contract.</summary>
    public static class VehicleAssetSetup
    {
        public const string DirectoryPath = "Assets/Alabama/Art/Vehicles/E46";
        public const string ModelPath = DirectoryPath + "/E46_Race.fbx";
        public const string PrefabPath = DirectoryPath + "/E46_Race.prefab";
        [Serializable] private sealed class MaterialSet { public MaterialDefinition[] materials; }
        [Serializable] private sealed class MaterialDefinition
        {
            public string name;
            public float[] linearColor;
            public float metallic;
            public float smoothness;
            public string texture;
        }

        [MenuItem("Alabama/Vehicle/Set Up")]
        public static void Setup()
        {
            SetupAssets(true);
        }

        internal static void SetupAssets(bool createReviewScene)
        {
            var contract = JsonUtility.FromJson<MaterialSet>(File.ReadAllText(DirectoryPath + "/E46_Materials.json"));
            var importer = AssetImporter.GetAtPath(ModelPath) as ModelImporter;
            Require(importer != null, "Export the E46 from Blender first.");
            foreach (var definition in contract.materials)
            {
                var path = DirectoryPath + "/" + definition.name + ".mat";
                var material = AssetDatabase.LoadAssetAtPath<Material>(path);
                if (material == null)
                {
                    material = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                    AssetDatabase.CreateAsset(material, path);
                }
                var c = definition.linearColor;
                // The interchange values are linear; Unity's Color property accepts sRGB.
                material.SetColor("_BaseColor", new Color(c[0], c[1], c[2], c[3]).gamma);
                material.SetFloat("_Metallic", definition.metallic);
                material.SetFloat("_Smoothness", definition.smoothness);
                // A previously textured material may now be an authored solid family.
                material.SetTexture("_BaseMap", null);
                if (!string.IsNullOrEmpty(definition.texture))
                {
                    var texturePath = DirectoryPath + "/" + definition.texture;
                    var textureImporter = (TextureImporter)AssetImporter.GetAtPath(texturePath);
                    textureImporter.sRGBTexture = true;
                    textureImporter.mipmapEnabled = true;
                    textureImporter.filterMode = FilterMode.Trilinear;
                    textureImporter.anisoLevel = 4;
                    textureImporter.wrapMode = TextureWrapMode.Clamp;
                    textureImporter.SaveAndReimport();
                    material.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>(texturePath));
                }
                bool transparent = c[3] < 1;
                material.SetFloat("_Surface", transparent ? 1 : 0);
                material.SetFloat("_SrcBlend", (float)(transparent ? BlendMode.SrcAlpha : BlendMode.One));
                material.SetFloat("_DstBlend", (float)(transparent ? BlendMode.OneMinusSrcAlpha : BlendMode.Zero));
                material.SetFloat("_ZWrite", transparent ? 0 : 1);
                material.SetOverrideTag("RenderType", transparent ? "Transparent" : "Opaque");
                material.renderQueue = transparent ? (int)RenderQueue.Transparent : (int)RenderQueue.Geometry;
                if (transparent) material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
                else material.DisableKeyword("_SURFACE_TYPE_TRANSPARENT");
                material.SetShaderPassEnabled("ShadowCaster", !transparent);
                EditorUtility.SetDirty(material);
            }
            AssetDatabase.SaveAssets();
            // Texture reimports can reload importers; acquire/configure the model only afterwards.
            importer = (ModelImporter)AssetImporter.GetAtPath(ModelPath);
            importer.globalScale = 1;
            importer.useFileScale = true;
            // Keep Blender's FBX root conversion so empty axle pivots and meshes agree.
            // Baking axes into geometry changes the empty-transform contract in this export.
            importer.bakeAxisConversion = false;
            importer.importAnimation = false;
            importer.importNormals = ModelImporterNormals.Import;
            importer.materialImportMode = ModelImporterMaterialImportMode.ImportStandard;
            foreach (var definition in contract.materials)
                importer.AddRemap(new AssetImporter.SourceAssetIdentifier(typeof(Material), definition.name),
                    AssetDatabase.LoadAssetAtPath<Material>(DirectoryPath + "/" + definition.name + ".mat"));
            importer.SaveAndReimport();
            var root = new GameObject("E46_Race");
            try
            {
                var model = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(ModelPath));
                model.transform.SetParent(root.transform, false);
                var marker = model.GetComponentsInChildren<Transform>().Single(t => t.name == "FrontMarker");
                var forward = marker.position - model.transform.position;
                forward.y = 0;
                model.transform.rotation = Quaternion.FromToRotation(forward.normalized, Vector3.forward) * model.transform.rotation;
                var collider = root.AddComponent<BoxCollider>();
                collider.center = new Vector3(0, .65f, 0);
                collider.size = new Vector3(1.9f, .85f, 4.45f);
                PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
            }
            finally { UnityEngine.Object.DestroyImmediate(root); }
            AssetDatabase.SaveAssets();
            Verify();
            if (createReviewScene) VehicleReviewScene.Create();
        }

        [MenuItem("Alabama/Vehicle/Verify")]
        public static void Verify()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            Require(prefab != null, "Vehicle prefab is missing.");
            var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            try
            {
                var meshes = instance.GetComponentsInChildren<MeshFilter>();
                int triangles = meshes.Sum(m => m.sharedMesh.triangles.Length / 3);
                Require(meshes.Length == 5, "Expected a body and four independently movable wheel meshes.");
                Require(triangles > 5000 && triangles <= 16000, "Vehicle triangle budget exceeded.");
                var renderers = instance.GetComponentsInChildren<Renderer>();
                foreach (var filter in meshes)
                {
                    var mesh = filter.sharedMesh;
                    Require(mesh.subMeshCount == filter.GetComponent<Renderer>().sharedMaterials.Length, "Submesh/material count mismatch.");
                }
                Require(renderers.All(r => r.sharedMaterials.All(m => m != null && m.shader.name == "Universal Render Pipeline/Lit")), "Missing or unsupported vehicle material.");
                Require(renderers.SelectMany(r => r.sharedMaterials).All(m => AssetDatabase.GetAssetPath(m).EndsWith(".mat")), "Vehicle contains fallback embedded materials.");
                foreach (string name in new[] { "E46_Paint", "E46_TailLens" })
                    Require(AssetDatabase.LoadAssetAtPath<Material>(DirectoryPath + "/" + name + ".mat").GetTexture("_BaseMap") != null, "Required vehicle texture is missing.");
                var bounds = renderers[0].bounds;
                foreach (var renderer in renderers.Skip(1)) bounds.Encapsulate(renderer.bounds);
                Require(bounds.size.z > 4.5f && bounds.size.z < 4.9f && bounds.size.x > 1.8f && bounds.size.x < 2.3f && bounds.size.y > 1.2f && bounds.size.y < 1.6f, "Vehicle metre scale or axes are incorrect: " + bounds);
                var transforms = instance.GetComponentsInChildren<Transform>();
                foreach (string name in new[] { "Wheel_FL", "Wheel_FR", "Wheel_RL", "Wheel_RR" })
                {
                    var wheel = transforms.Single(t => t.name == name);
                    Require(wheel.position.y > .25f && wheel.position.y < .4f, "Wheel pivot must be at axle height.");
                    Require(name.Contains("_F") ? wheel.position.z > 1 : wheel.position.z < -1, "Front/rear wheel axis mismatch.");
                    Require(name.EndsWith("L") ? wheel.position.x < -.7f : wheel.position.x > .7f, "Left/right wheel axis mismatch.");
                }
                Debug.Log($"E46 verified: {triangles} triangles, {meshes.Length} meshes, dimensions {bounds.size} metres.");
            }
            finally { UnityEngine.Object.DestroyImmediate(instance); }
        }

        private static void Require(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException(message);
        }
    }
}
