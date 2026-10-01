using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace Alabama.Editor
{
    /// <summary>Repeatable import contract for the original Blender environment library.</summary>
    public static class IndustrialKitSetup
    {
        public const string DirectoryPath = "Assets/Alabama/Art/Environment/IndustrialKit";
        [Serializable] private sealed class Contract { public Module[] modules; public Definition[] materials; }
        [Serializable] private sealed class Module { public string name; public int triangles; }
        [Serializable] private sealed class Definition
        {
            public string name;
            public float[] linearColor;
            public float metallic;
            public float smoothness;
        }

        [MenuItem("Alabama/Street/Import Industrial Kit")]
        public static void Setup()
        {
            var contract = ReadContract();
            var shader = Shader.Find("Universal Render Pipeline/Lit");
            Require(shader != null, "URP Lit shader is unavailable.");
            foreach (var definition in contract.materials)
            {
                var path = DirectoryPath + "/" + definition.name + ".mat";
                var material = AssetDatabase.LoadAssetAtPath<Material>(path);
                if (material == null)
                {
                    material = new Material(shader);
                    AssetDatabase.CreateAsset(material, path);
                }
                var c = definition.linearColor;
                Require(c != null && c.Length == 4, "Invalid material color: " + definition.name);
                material.SetColor("_BaseColor", new Color(c[0], c[1], c[2], c[3]).gamma);
                material.SetFloat("_Metallic", definition.metallic);
                material.SetFloat("_Smoothness", definition.smoothness);
                EditorUtility.SetDirty(material);
            }
            AssetDatabase.SaveAssets();
            foreach (var module in contract.modules)
            {
                var path = DirectoryPath + "/" + module.name + ".fbx";
                var importer = AssetImporter.GetAtPath(path) as ModelImporter;
                Require(importer != null, "Missing FBX: " + path);
                importer.globalScale = 1;
                importer.useFileScale = true;
                importer.bakeAxisConversion = false;
                importer.importAnimation = false;
                importer.importNormals = ModelImporterNormals.Import;
                importer.materialImportMode = ModelImporterMaterialImportMode.ImportStandard;
                foreach (var definition in contract.materials)
                    importer.AddRemap(new AssetImporter.SourceAssetIdentifier(typeof(Material), definition.name),
                        AssetDatabase.LoadAssetAtPath<Material>(DirectoryPath + "/" + definition.name + ".mat"));
                importer.SaveAndReimport();
                var root = new GameObject(module.name);
                try
                {
                    var model = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(path));
                    model.transform.SetParent(root.transform, false);
                    // Blender suffixes identical empty names across separately exported modules.
                    var marker = model.GetComponentsInChildren<Transform>().Single(t => t.name.StartsWith("FrontMarker"));
                    var forward = marker.position - model.transform.position;
                    forward.y = 0;
                    model.transform.rotation = Quaternion.FromToRotation(forward.normalized, Vector3.forward) * model.transform.rotation;
                    PrefabUtility.SaveAsPrefabAsset(root, PrefabPath(module.name));
                }
                finally { UnityEngine.Object.DestroyImmediate(root); }
            }
            AssetDatabase.SaveAssets();
            Verify();
        }

        [MenuItem("Alabama/Street/Verify Industrial Kit")]
        public static void Verify()
        {
            var contract = ReadContract();
            Require(contract.modules.Length == 13, "Expected 13 environment modules.");
            int total = 0;
            foreach (var module in contract.modules)
            {
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath(module.name));
                Require(prefab != null, "Missing kit prefab: " + module.name);
                var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
                try
                {
                    var filters = instance.GetComponentsInChildren<MeshFilter>();
                    Require(filters.Length == 1, "Module should be a single joined mesh: " + module.name);
                    var triangles = filters[0].sharedMesh.triangles.Length / 3;
                    Require(triangles == module.triangles, "Triangle contract changed for " + module.name);
                    var renderer = filters[0].GetComponent<Renderer>();
                    Require(filters[0].sharedMesh.subMeshCount == renderer.sharedMaterials.Length, "Material slots mismatch in " + module.name);
                    Require(renderer.sharedMaterials.All(m => m != null && m.shader.name == "Universal Render Pipeline/Lit" &&
                            AssetDatabase.GetAssetPath(m).EndsWith(".mat")), "Missing material remap in " + module.name);
                    total += triangles;
                }
                finally { UnityEngine.Object.DestroyImmediate(instance); }
            }
            Require(total < 20000, "Kit library triangle budget exceeded.");
            Debug.Log($"Industrial kit verified: {contract.modules.Length} modules, {total} library triangles.");
        }

        public static string PrefabPath(string name) => DirectoryPath + "/" + name + ".prefab";
        private static Contract ReadContract() => JsonUtility.FromJson<Contract>(File.ReadAllText(DirectoryPath + "/IndustrialKit.json"));
        private static void Require(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException(message);
        }
    }
}
