using System;
using System.Collections.Generic;
using Alabama.MicroSlice;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Splines;

namespace Alabama.Editor
{
    public static class MicroSliceRoadBuilder
    {
        public struct Sample
        {
            public Vector3 position, right, up;
            public float width, distance;
        }

        public static List<Sample> SampleRoad(SplineContainer source, SplineRoadProfile profile)
        {
            var spline = source.Spline;
            if (spline.Count < 2 || profile.knotWidths?.Length != spline.Count ||
                profile.bankDegrees?.Length != spline.Count)
                throw new InvalidOperationException("Each road knot requires a width and bank.");
            var result = new List<Sample>();
            int steps = Mathf.Max(32, Mathf.CeilToInt(spline.GetLength() / 1.5f));
            float distance = 0;
            for (int i = 0; i <= steps; i++)
            {
                float t = (float)i / steps;
                source.Evaluate(t, out float3 position, out float3 tangent, out float3 ignoredUp);
                int knot = SplineUtility.SplineToCurveT(spline, t, out float along);
                int next = spline.Closed ? (knot + 1) % spline.Count : Mathf.Min(knot + 1, spline.Count - 1);
                var forward = ((Vector3)tangent).normalized;
                var right = Vector3.Cross(Vector3.up, forward).normalized;
                right = Quaternion.AngleAxis(Mathf.Lerp(profile.bankDegrees[knot], profile.bankDegrees[next], along), forward) * right;
                var p = (Vector3)position;
                if (i > 0) distance += Vector3.Distance(result[^1].position, p);
                result.Add(new Sample { position = p, right = right, up = Vector3.Cross(forward, right).normalized,
                    width = Mathf.Lerp(profile.knotWidths[knot], profile.knotWidths[next], along), distance = distance });
            }
            return result;
        }

        public static Mesh Ribbon(List<Sample> samples, Func<Sample, Vector2> limits, float height = 0,
            Func<Sample, bool> include = null)
        {
            var vertices = new List<Vector3>(); var uv = new List<Vector2>(); var indices = new List<int>();
            for (int i = 0; i < samples.Count - 1; i++)
            {
                var a = samples[i]; var b = samples[i + 1];
                if (include != null && (!include(a) || !include(b))) continue;
                var al = limits(a); var bl = limits(b);
                int first = vertices.Count;
                vertices.Add(a.position + a.right * al.x + a.up * height);
                vertices.Add(a.position + a.right * al.y + a.up * height);
                vertices.Add(b.position + b.right * bl.x + b.up * height);
                vertices.Add(b.position + b.right * bl.y + b.up * height);
                uv.Add(new Vector2(al.x / 4, a.distance / 4));
                uv.Add(new Vector2(al.y / 4, a.distance / 4));
                uv.Add(new Vector2(bl.x / 4, b.distance / 4));
                uv.Add(new Vector2(bl.y / 4, b.distance / 4));
                indices.AddRange(new[] { first, first + 2, first + 1, first + 1, first + 2, first + 3 });
            }
            var mesh = new Mesh { name = "Spline loft", indexFormat = UnityEngine.Rendering.IndexFormat.UInt32 };
            mesh.SetVertices(vertices); mesh.SetUVs(0, uv); mesh.SetTriangles(indices, 0);
            mesh.RecalculateNormals(); mesh.RecalculateBounds();
            return mesh;
        }

        public static Mesh Curb(List<Sample> samples, int side, Func<Sample, bool> include)
        {
            var vertices = new List<Vector3>(); var indices = new List<int>();
            for (int i = 0; i < samples.Count - 1; i++)
            {
                var a = samples[i]; var b = samples[i + 1];
                if (!include(a) || !include(b)) continue;
                int k = vertices.Count;
                vertices.Add(a.position + a.right * (a.width / 2 * side));
                vertices.Add(a.position + a.right * (a.width / 2 * side) + a.up * .15f);
                vertices.Add(b.position + b.right * (b.width / 2 * side));
                vertices.Add(b.position + b.right * (b.width / 2 * side) + b.up * .15f);
                if (side > 0) indices.AddRange(new[] { k, k + 1, k + 2, k + 1, k + 3, k + 2 });
                else indices.AddRange(new[] { k, k + 2, k + 1, k + 1, k + 2, k + 3 });
            }
            var mesh = new Mesh { name = "Separate curb face", indexFormat = UnityEngine.Rendering.IndexFormat.UInt32 };
            mesh.SetVertices(vertices); mesh.SetTriangles(indices, 0); mesh.RecalculateNormals(); mesh.RecalculateBounds();
            return mesh;
        }

        public static bool IsContinuous(List<Sample> samples, bool closed)
        {
            if (samples.Count < 4) return false;
            foreach (var sample in samples)
                if (!float.IsFinite(sample.position.x) || !float.IsFinite(sample.position.y) || sample.width < 4) return false;
            if (!closed) return true;
            return Vector3.Distance(samples[0].position, samples[^1].position) < .001f &&
                Vector3.Distance(samples[0].right, samples[^1].right) < .01f &&
                Mathf.Abs(samples[0].width - samples[^1].width) < .001f;
        }
    }
}
