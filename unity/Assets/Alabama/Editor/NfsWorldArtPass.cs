using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace Alabama.Editor
{
    /// <summary>Non-destructive model/texture study with the approved atmosphere.</summary>
    public static class NfsWorldArtPass
    {
        public const string ScenePath = NfsWorldSetup.BasePath + "/Scenes/NfsWorldArtPass.unity";
        private const string DirectoryPath = NfsWorldSetup.BasePath + "/ArtPass";
        [Serializable] private sealed class MeshRecord { public string name; public bool changed; }
        [Serializable] private sealed class MeshManifest { public bool collisionChanged; public bool finiteCoordinatesValidated; public MeshRecord[] meshes; }
        [Serializable] private sealed class TextureRecord { public string source; public string file; public string assetPath; public bool alpha; }
        [Serializable] private sealed class TextureManifest { public TextureRecord[] textures; }
        [Serializable] private sealed class Validation { public string[] acceptedMeshes; public string[] rejectedMeshes; public string[] exactFacetedMeshes; public int overriddenMaterials; public float boundaryToleranceMetres; public bool collisionChanged; }
        private static int overriddenMaterials;

        public static HashSet<string> Prepare(string directoryPath = DirectoryPath, string meshManifestPath = null)
        {
            var manifest = JsonUtility.FromJson<MeshManifest>(File.ReadAllText(meshManifestPath ?? Path.GetFullPath(
                Path.Combine(Application.dataPath, "../../artifacts/NfsWorld/art-meshes.json"))));
            NfsWorldSetup.Require(manifest.finiteCoordinatesValidated && !manifest.collisionChanged,
                "Generate and validate the separate Blender art meshes first.");
            var textures = JsonUtility.FromJson<TextureManifest>(File.ReadAllText(directoryPath + "/textures.json"));
            string TexturePath(TextureRecord entry) => string.IsNullOrEmpty(entry.assetPath)
                ? directoryPath + "/Textures/" + entry.file : entry.assetPath;
            var replacements = new Dictionary<string, Texture2D>();
            var sizes = new Dictionary<string, int>();
            foreach (var entry in textures.textures)
            {
                var source = AssetDatabase.LoadAssetAtPath<Texture2D>(NfsWorldSetup.BasePath + "/Textures/" + entry.source);
                NfsWorldSetup.Require(source != null, "Unknown source texture: " + entry.source);
                sizes.TryGetValue(TexturePath(entry), out int previous);
                sizes[TexturePath(entry)] = Mathf.Max(previous, Mathf.Max(source.width, source.height));
            }
            var configured = new Dictionary<string, bool>();
            var importers = new Dictionary<string, TextureImporter>();
            foreach (var entry in textures.textures)
            {
                string path = TexturePath(entry);
                var importer = AssetImporter.GetAtPath(path) as TextureImporter;
                NfsWorldSetup.Require(importer != null, "Missing generated art texture: " + path);
                var source = AssetDatabase.LoadAssetAtPath<Texture2D>(NfsWorldSetup.BasePath + "/Textures/" + entry.source);
                NfsWorldSetup.Require(source != null, "Unknown source texture: " + entry.source);
                importer.GetSourceTextureWidthAndHeight(out int width, out int height);
                NfsWorldSetup.Require(Mathf.Abs((float)width / height - (float)source.width / source.height) < .005f,
                    "Replacement texture changed the UV canvas aspect: " + entry.file);
                var sourceImporter = (TextureImporter)AssetImporter.GetAtPath(NfsWorldSetup.BasePath + "/Textures/" + entry.source);
                // Some AC alpha-test materials use fully opaque PNGs. Unity
                // reports those as having no meaningful alpha; no mask is lost.
                NfsWorldSetup.Require(!entry.alpha || importer.DoesSourceTextureHaveAlpha() ||
                    !sourceImporter.DoesSourceTextureHaveAlpha(), "Replacement lost a source cutout mask: " + entry.file);
                if (configured.TryGetValue(path, out bool alpha))
                    NfsWorldSetup.Require(alpha == entry.alpha, "Conflicting alpha settings: " + entry.file);
                else
                {
                    configured.Add(path, entry.alpha);
                    importers.Add(path, importer);
                }
            }
            // Hundreds of source-sized bakes must import as one batch. Load the
            // replacement assets only after all pending imports have completed.
            AssetDatabase.StartAssetEditing();
            try
            {
            foreach (var pair in importers)
            {
                var importer = pair.Value;
                bool alpha = configured[pair.Key];
                var alphaSource = alpha ? TextureImporterAlphaSource.FromInput : TextureImporterAlphaSource.None;
                bool changed = importer.textureType != TextureImporterType.Default || !importer.sRGBTexture ||
                    importer.alphaSource != alphaSource || importer.alphaIsTransparency != alpha ||
                    importer.maxTextureSize != sizes[pair.Key] || !importer.mipmapEnabled ||
                    importer.wrapMode != TextureWrapMode.Repeat || importer.filterMode != FilterMode.Trilinear ||
                    importer.anisoLevel != 4 || importer.textureCompression != TextureImporterCompression.Compressed ||
                    importer.isReadable;
                if (!changed) continue;
                importer.textureType = TextureImporterType.Default;
                importer.sRGBTexture = true;
                importer.alphaSource = alphaSource;
                importer.alphaIsTransparency = alpha;
                importer.maxTextureSize = sizes[pair.Key];
                importer.mipmapEnabled = true;
                importer.wrapMode = TextureWrapMode.Repeat;
                importer.filterMode = FilterMode.Trilinear;
                importer.anisoLevel = 4;
                importer.textureCompression = TextureImporterCompression.Compressed;
                importer.isReadable = false;
                importer.SaveAndReimport();
            }
            }
            finally { AssetDatabase.StopAssetEditing(); }
            foreach (var entry in textures.textures)
                replacements.Add(entry.source, AssetDatabase.LoadAssetAtPath<Texture2D>(TexturePath(entry)));
            Directory.CreateDirectory(directoryPath + "/Materials");
            AssetDatabase.Refresh();
            var copies = new Dictionary<Material, Material>();
            overriddenMaterials = 0;
            AssetDatabase.StartAssetEditing();
            try
            {
            foreach (var root in UnityEngine.SceneManagement.SceneManager.GetActiveScene().GetRootGameObjects())
            {
                if (root.name == "E46 driver car") continue;
                foreach (var renderer in root.GetComponentsInChildren<MeshRenderer>().Where(r => r.enabled))
                {
                    var original = renderer.sharedMaterial;
                    if (!copies.TryGetValue(original, out var copy))
                    {
                        string path = directoryPath + "/Materials/" + original.name + ".mat";
                        copy = AssetDatabase.LoadAssetAtPath<Material>(path);
                        if (copy == null) { copy = new Material(original); AssetDatabase.CreateAsset(copy, path); }
                        else EditorUtility.CopySerialized(original, copy);
                        var texture = original.GetTexture("_BaseMap");
                        if (texture != null && replacements.TryGetValue(Path.GetFileName(AssetDatabase.GetAssetPath(texture)), out var replacement))
                        { copy.SetTexture("_BaseMap", replacement); overriddenMaterials++; }
                        EditorUtility.SetDirty(copy);
                        copies.Add(original, copy);
                    }
                    renderer.sharedMaterial = copy;
                }
            }
            }
            finally { AssetDatabase.StopAssetEditing(); }
            AssetDatabase.SaveAssets();
            NfsWorldSetup.Require(overriddenMaterials > 0, "The art study did not bind any replacement textures.");
            return new HashSet<string>(manifest.meshes.Where(m => m.changed).Select(m => m.name));
        }

        public static void SaveValidation(List<string> accepted, List<string> rejected, List<string> faceted,
            string reportName = "art-validation.json")
        {
            File.WriteAllText(Path.GetFullPath(Path.Combine(Application.dataPath, "../../artifacts/NfsWorld/" + reportName)),
                JsonUtility.ToJson(new Validation { acceptedMeshes = accepted.ToArray(), rejectedMeshes = rejected.ToArray(),
                    exactFacetedMeshes = faceted.ToArray(),
                    overriddenMaterials = overriddenMaterials, boundaryToleranceMetres = .02f, collisionChanged = false }, true));
        }
    }
}
