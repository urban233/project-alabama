using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Alabama.Driving;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace Alabama.Editor
{
    public static class NfsWorldOptimization
    {
        private sealed class Rule { public Regex[] patterns; public float distance; }

        internal static void Apply()
        {
            string path = Path.GetFullPath(Path.Combine(Application.dataPath,
                "../../source-art/maps/nfs-world/original/mauleous_nfs_world/extension/ext_config.ini"));
            NfsWorldSetup.Require(File.Exists(path), "Source distance configuration missing.");
            var rules = new List<Rule>();
            foreach (string section in Regex.Split(File.ReadAllText(path), @"(?m)^\["))
            {
                if (!section.StartsWith("SHADER_REPLACEMENT_")) continue;
                var fields = new Dictionary<string, string>();
                foreach (string line in section.Split('\n').Skip(1))
                {
                    var value = line.Split(';')[0];
                    int separator = value.IndexOf('=');
                    if (separator > 0) fields[value.Substring(0, separator).Trim()] = value.Substring(separator + 1).Trim();
                }
                if (fields.TryGetValue("ACTIVE", out var active) && active == "0") continue;
                if (!fields.TryGetValue("MESHES", out var meshes) || !fields.TryGetValue("LOD_OUT", out var distance)) continue;
                rules.Add(new Rule
                {
                    distance = float.Parse(distance, System.Globalization.CultureInfo.InvariantCulture),
                    patterns = meshes.Split(',').Select(pattern => new Regex("^" + Regex.Escape(pattern.Trim())
                        .Replace(@"\?", ".*").Replace(@"\*", ".*") + "$")).ToArray()
                });
            }
            var targets = new List<Renderer>();
            var limits = new List<float>();
            foreach (var part in NfsWorldSetup.ReadContract("District").parts)
            {
                var root = GameObject.Find(part.category);
                var meshes = root.GetComponentsInChildren<MeshRenderer>().ToDictionary(r => r.name);
                foreach (var definition in part.meshes)
                {
                    if (!definition.visible) continue;
                    float distance = 0;
                    foreach (var rule in rules)
                        if (rule.patterns.Any(pattern => pattern.IsMatch(definition.sourceName))) distance = rule.distance;
                    // Source landmark exceptions remain effectively unlimited at the camera's far plane.
                    if (distance <= 0 || distance >= 90000) continue;
                    targets.Add(meshes[definition.name]);
                    limits.Add(distance);
                }
            }
            var culling = UnityEngine.Object.FindFirstObjectByType<NfsWorldDistanceCulling>();
            if (culling == null) culling = new GameObject("Source visual distance limits").AddComponent<NfsWorldDistanceCulling>();
            culling.Configure(targets.ToArray(), limits.ToArray());
            EditorUtility.SetDirty(culling);
            // Explicit diffuse probe makes the configured ambient fill available in a batch-created scene.
            var ambient = new SphericalHarmonicsL2();
            ambient.AddAmbientLight(RenderSettings.ambientEquatorColor);
            RenderSettings.ambientProbe = ambient;
            Debug.Log($"Source distance limits configured for {targets.Count} renderers; collision retained.");
        }
    }
}
