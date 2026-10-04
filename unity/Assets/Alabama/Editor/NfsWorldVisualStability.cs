using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace Alabama.Editor
{
    /// <summary>Derived-only corrections for competing window surfaces and noisy vehicle shading.</summary>
    public static class NfsWorldVisualStability
    {
        [Serializable] private sealed class MaterialRules
        {
            public string[] windowTextures;
            public string[] suppressedTextures;
            public string[] authoredWindowTextures;
            public float windowSeparationMetres;
        }
        private static MaterialRules Rules() => JsonUtility.FromJson<MaterialRules>(File.ReadAllText(
            Path.GetFullPath(Path.Combine(Application.dataPath, "../../docs/art-direction/visual-stability-materials.json"))));

        public static void Apply()
        {
            string root = NfsWorldArtDirection.DirectoryPath;
            var pipeline = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(root + "/Optimized/BatchedPipeline.asset");
            NfsWorldSetup.Require(pipeline != null, "Generate the styled district before applying stability corrections.");
            NfsWorldRenderOptimization.Configure(pipeline, root + "/Optimized");
            var changes = new List<string>();
            foreach (string layers in Directory.GetFiles(root + "/Optimized/Arrays", "*.layers.txt"))
            {
                var preferred = WindowLayers(layers);
                var suppressed = SuppressedLayers(layers);
                string prefix = Path.GetFileName(layers).Replace(".layers.txt", "");
                ConfigureWindowSeparation(layers, preferred);
                foreach (string directory in new[] { root + "/Optimized/Meshes", root + "/Lods" })
                    foreach (string path in Directory.GetFiles(directory, prefix + "_*.asset"))
                    {
                        var mesh = AssetDatabase.LoadAssetAtPath<Mesh>(path);
                        if (mesh == null) continue;
                        int windowsRemoved = ResolveWindowOverlaps(mesh, preferred);
                        int cardsRemoved = RemoveLayers(mesh, suppressed);
                        int removed = windowsRemoved + cardsRemoved;
                        if (removed == 0) continue;
                        EditorUtility.SetDirty(mesh);
                        changes.Add(path + ": window overlap triangles=" + windowsRemoved + ", sign light-card triangles=" + cardsRemoved);
                    }
            }
            AssetDatabase.SaveAssets();
            string output = Path.GetFullPath(Path.Combine(Application.dataPath, "../../artifacts/NfsWorld/Stability"));
            Directory.CreateDirectory(output);
            File.WriteAllLines(Path.Combine(output, "applied.txt"), new[] {
                "Styled renderer: stochastic SSAO disabled; map shader safely normalizes zero normals; lighting, paint colours and source assets preserved."
            }.Concat(changes));
        }

        internal static HashSet<int> WindowLayers(string layerFile)
            => LayersMatching(layerFile, new HashSet<string>(Rules().authoredWindowTextures));

        internal static HashSet<int> SuppressedLayers(string layerFile)
            => LayersMatching(layerFile, new HashSet<string>(Rules().suppressedTextures));

        private static HashSet<int> LayersMatching(string layerFile, ISet<string> names)
        {
            var result = new HashSet<int>();
            var entries = File.ReadAllLines(layerFile);
            for (int i = 0; i < entries.Length; i++)
            {
                string path = AssetDatabase.GUIDToAssetPath(entries[i].Split(':')[0]);
                if (names.Contains(Path.GetFileName(path))) result.Add(i);
            }
            return result;
        }

        internal static void ConfigureWindowSeparation(string layers, ISet<int> authored)
        {
            string path = layers.Replace(".layers.txt", "-window-weights.asset");
            int count = File.ReadAllLines(layers).Length;
            var texture = new Texture2D(count, 1, TextureFormat.RGBA32, false, true)
            { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp };
            var windows = LayersMatching(layers, new HashSet<string>(Rules().windowTextures));
            texture.SetPixels(Enumerable.Range(0, count).Select(i => authored.Contains(i) ? Color.white :
                windows.Contains(i) ? new Color(.5f, .5f, .5f, 1) : Color.black).ToArray());
            texture.Apply();
            var existing = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            if (existing == null) AssetDatabase.CreateAsset(texture, path);
            else { EditorUtility.CopySerialized(texture, existing); UnityEngine.Object.DestroyImmediate(texture); texture = existing; }
            var material = AssetDatabase.LoadAssetAtPath<Material>(layers.Replace(".layers.txt", ".mat"));
            material.SetTexture("_WindowWeights", texture);
            material.SetFloat("_WindowSeparation", windows.Count > 0 || authored.Count > 0 ? Rules().windowSeparationMetres : 0);
            EditorUtility.SetDirty(material);
        }

        // Remove only the reviewed fake light cards; lamps, signs and other effects stay intact.
        public static int RemoveLayers(Mesh mesh, ISet<int> removedLayers)
        {
            if (removedLayers.Count == 0) return 0;
            var layers = new List<Vector2>(); mesh.GetUVs(2, layers);
            var source = mesh.triangles; var retained = new List<int>(source.Length);
            for (int i = 0; i < source.Length; i += 3)
                if (!removedLayers.Contains((int)layers[source[i]].x))
                { retained.Add(source[i]); retained.Add(source[i + 1]); retained.Add(source[i + 2]); }
            if (retained.Count != source.Length) mesh.SetTriangles(retained, 0, false);
            return (source.Length - retained.Count) / 3;
        }

        // Exact positions only: near-coincident but separate panes, material seams,
        // foliage cards, collision and all non-window duplicates remain untouched.
        public static int ResolveWindowOverlaps(Mesh mesh, ISet<int> preferredLayers)
        {
            if (preferredLayers.Count == 0) return 0;
            var positions = mesh.vertices;
            var indices = mesh.triangles;
            var layers = new List<Vector2>(); mesh.GetUVs(2, layers);
            if (layers.Count != positions.Length) throw new ArgumentException("Missing texture array layer metadata.");
            var windows = new HashSet<(Vector3, Vector3, Vector3)>();
            (Vector3, Vector3, Vector3) Key(int triangle)
            {
                var a = positions[indices[triangle]];
                var b = positions[indices[triangle + 1]];
                var c = positions[indices[triangle + 2]];
                int Compare(Vector3 p, Vector3 q)
                {
                    int x = p.x.CompareTo(q.x); if (x != 0) return x;
                    int y = p.y.CompareTo(q.y); return y != 0 ? y : p.z.CompareTo(q.z);
                }
                if (Compare(a, b) > 0) (a, b) = (b, a);
                if (Compare(b, c) > 0) (b, c) = (c, b);
                if (Compare(a, b) > 0) (a, b) = (b, a);
                return (a, b, c);
            }
            bool Preferred(int triangle) => preferredLayers.Contains((int)layers[indices[triangle]].x);
            for (int i = 0; i < indices.Length; i += 3)
                if (Preferred(i)) windows.Add(Key(i));
            if (windows.Count == 0) return 0;
            var retained = new List<int>(indices.Length);
            var keptWindows = new HashSet<(Vector3, Vector3, Vector3)>();
            for (int i = 0; i < indices.Length; i += 3)
            {
                if (!Preferred(i) && windows.Contains(Key(i))) continue;
                if (Preferred(i) && !keptWindows.Add(Key(i))) continue;
                retained.Add(indices[i]); retained.Add(indices[i + 1]); retained.Add(indices[i + 2]);
            }
            int removed = (indices.Length - retained.Count) / 3;
            if (removed > 0) mesh.SetTriangles(retained, 0, false); // Keep exact source bounds/UVs/normals.
            return removed;
        }
    }
}
