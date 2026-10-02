using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace Alabama.Editor
{
    /// <summary>Author flat faces without moving vertices, changing UVs or removing triangles.</summary>
    public static class NfsWorldFacetMeshes
    {
        public static Mesh Create(Mesh source)
        {
            var vertices = source.vertices;
            var uv = source.uv;
            var indices = source.triangles;
            NfsWorldSetup.Require(uv.Length == vertices.Length, "Facet source requires UVs: " + source.name);
            var expanded = new List<Vector3>();
            var normals = new List<Vector3>();
            var coordinates = new List<Vector2>();
            var triangles = new int[indices.Length];
            var shared = new Dictionary<(Vector3, Vector2, Vector3Int), int>();
            for (int index = 0; index < indices.Length; index += 3)
            {
                var normal = Vector3.Cross(vertices[indices[index + 1]] - vertices[indices[index]],
                    vertices[indices[index + 2]] - vertices[indices[index]]).normalized;
                var keyNormal = new Vector3Int(Mathf.RoundToInt(normal.x * 10000),
                    Mathf.RoundToInt(normal.y * 10000), Mathf.RoundToInt(normal.z * 10000));
                for (int corner = 0; corner < 3; corner++)
                {
                    int target = index + corner;
                    var point = vertices[indices[target]];
                    var coordinate = uv[indices[target]];
                    var key = (point, coordinate, keyNormal);
                    if (!shared.TryGetValue(key, out int vertex))
                    {
                        vertex = expanded.Count;
                        shared.Add(key, vertex);
                        expanded.Add(point);
                        coordinates.Add(coordinate);
                        normals.Add(((Vector3)keyNormal).normalized);
                    }
                    triangles[target] = vertex;
                }
            }
            var mesh = new Mesh { name = source.name + "_FlatArt", indexFormat = IndexFormat.UInt32,
                vertices = expanded.ToArray(), normals = normals.ToArray(), uv = coordinates.ToArray(),
                triangles = triangles };
            mesh.RecalculateBounds();
            return mesh;
        }
    }
}
