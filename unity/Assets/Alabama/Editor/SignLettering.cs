using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace Alabama.Editor
{
    /// <summary>Small original mesh alphabet for street signs; no machine font dependency.</summary>
    internal static class SignLettering
    {
        private static readonly Dictionary<char, string[]> Glyphs = new Dictionary<char, string[]>
        {
            ['R'] = new[] { "11110", "10001", "10001", "11110", "10100", "10010", "10001" },
            ['I'] = new[] { "11111", "00100", "00100", "00100", "00100", "00100", "11111" },
            ['V'] = new[] { "10001", "10001", "10001", "10001", "10001", "01010", "00100" },
            ['E'] = new[] { "11111", "10000", "10000", "11110", "10000", "10000", "11111" },
            ['S'] = new[] { "01111", "10000", "10000", "01110", "00001", "00001", "11110" },
            ['D'] = new[] { "11110", "10001", "10001", "10001", "10001", "10001", "11110" },
            ['O'] = new[] { "01110", "10001", "10001", "10001", "10001", "10001", "01110" },
            ['W'] = new[] { "10001", "10001", "10001", "10101", "10101", "10101", "01010" },
            ['N'] = new[] { "10001", "11001", "10101", "10011", "10001", "10001", "10001" },
            ['T'] = new[] { "11111", "00100", "00100", "00100", "00100", "00100", "00100" },
        };

        public static string Path(string label) => "Assets/Alabama/Art/IndustrialStreet/" + label + "Lettering.asset";

        public static void Ensure(string label)
        {
            if (AssetDatabase.LoadAssetAtPath<Mesh>(Path(label)) != null) return;
            const float pixel = .075f;
            var vertices = new List<Vector3>();
            var triangles = new List<int>();
            foreach (var (letter, column) in Enumerate(label))
            {
                var glyph = Glyphs[letter];
                for (int row = 0; row < 7; row++)
                    for (int bit = 0; bit < 5; bit++)
                        if (glyph[row][bit] == '1')
                        {
                            float x = (column + bit) * pixel - (label.Length * 6 - 1) * pixel / 2;
                            float y = (6 - row) * pixel;
                            int first = vertices.Count;
                            vertices.Add(new Vector3(x, y, 0));
                            vertices.Add(new Vector3(x, y + pixel, 0));
                            vertices.Add(new Vector3(x + pixel, y + pixel, 0));
                            vertices.Add(new Vector3(x + pixel, y, 0));
                            triangles.AddRange(new[] { first, first + 1, first + 2, first, first + 2, first + 3 });
                        }
            }
            var mesh = new Mesh { name = label + " sign lettering" };
            mesh.SetVertices(vertices);
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            AssetDatabase.CreateAsset(mesh, Path(label));
        }

        private static IEnumerable<(char letter, int column)> Enumerate(string label)
        {
            for (int i = 0; i < label.Length; i++) yield return (label[i], i * 6);
        }
    }
}
