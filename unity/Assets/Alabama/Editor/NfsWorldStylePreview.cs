using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Alabama.Editor
{
    /// <summary>Separate atmosphere/material study, preserving the playable import baseline.</summary>
    public static class NfsWorldStylePreview
    {
        public const string ScenePath = NfsWorldSetup.BasePath + "/Scenes/NfsWorldStylePreview.unity";
        public const string LightingStudyScenePath = NfsWorldSetup.BasePath + "/Scenes/NfsWorldLightingStudy.unity";

        [MenuItem("Alabama/NFS World/Create Visual Direction Preview")]
        public static void Create()
        {
            if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            var scene = EditorSceneManager.OpenScene(NfsWorldSetup.DistrictScene);
            string directory = NfsWorldSetup.BasePath + "/StylePreview";
            System.IO.Directory.CreateDirectory(directory);
            AssetDatabase.Refresh();
            var copies = new Dictionary<Material, Material>();
            foreach (var root in scene.GetRootGameObjects())
            {
                if (root.name == "E46 driver car") continue;
                foreach (var renderer in root.GetComponentsInChildren<MeshRenderer>())
                {
                    var source = renderer.sharedMaterial;
                    if (source == null || !renderer.enabled) continue;
                    if (!copies.TryGetValue(source, out var material))
                    {
                        string path = directory + "/" + source.name + ".mat";
                        material = AssetDatabase.LoadAssetAtPath<Material>(path);
                        if (material == null)
                        {
                            material = new Material(source);
                            AssetDatabase.CreateAsset(material, path);
                        }
                        else EditorUtility.CopySerialized(source, material);
                        material.SetColor("_BaseColor", new Color(.87f, .85f, .79f));
                        material.SetFloat("_Smoothness", .06f);
                        EditorUtility.SetDirty(material);
                        copies.Add(source, material);
                    }
                    renderer.sharedMaterial = material;
                }
            }
            RenderSettings.ambientSkyColor = new Color(.48f, .53f, .59f);
            RenderSettings.ambientEquatorColor = new Color(.36f, .37f, .39f);
            RenderSettings.ambientGroundColor = new Color(.24f, .21f, .17f);
            // Batch scene creation does not bake an environment probe. Supply diffuse fill
            // explicitly so tall-city shadows remain readable in the saved local study.
            var ambient = new SphericalHarmonicsL2();
            ambient.AddAmbientLight(new Color(.36f, .39f, .44f));
            RenderSettings.ambientProbe = ambient;
            RenderSettings.fogColor = new Color(.56f, .55f, .50f);
            RenderSettings.fogDensity = .0011f;
            RenderSettings.sun.color = new Color(1, .78f, .53f);
            RenderSettings.sun.intensity = 2.0f;
            RenderSettings.sun.transform.rotation = Quaternion.Euler(23, -35, 0);
            var volume = new GameObject("Grounded autumn visual study").AddComponent<Volume>();
            volume.isGlobal = true;
            string profilePath = directory + "/Atmosphere.asset";
            var profile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(profilePath);
            if (profile == null) { profile = ScriptableObject.CreateInstance<VolumeProfile>(); AssetDatabase.CreateAsset(profile, profilePath); }
            if (!profile.TryGet<ColorAdjustments>(out var colour)) { colour = profile.Add<ColorAdjustments>(true); AssetDatabase.AddObjectToAsset(colour, profile); }
            colour.postExposure.Override(.65f);
            colour.contrast.Override(8);
            colour.saturation.Override(-18);
            if (!profile.TryGet<Tonemapping>(out var tone)) { tone = profile.Add<Tonemapping>(true); AssetDatabase.AddObjectToAsset(tone, profile); }
            tone.mode.Override(TonemappingMode.ACES);
            volume.sharedProfile = profile;
            Camera.main.GetUniversalAdditionalCameraData().renderPostProcessing = true;
            EditorUtility.SetDirty(profile);
            AssetDatabase.SaveAssets();
            EditorSceneManager.SaveScene(scene, ScenePath);
            EditorSceneManager.SaveScene(scene, LightingStudyScenePath, true);
            Debug.Log("Separate NFS World visual direction preview saved: " + ScenePath);
        }
    }
}
