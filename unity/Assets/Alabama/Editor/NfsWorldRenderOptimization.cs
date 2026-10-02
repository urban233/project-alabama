using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace Alabama.Editor
{
    /// <summary>Reduce scene-local render copies and AO bandwidth, retaining the approved sun/shadows.</summary>
    public static class NfsWorldRenderOptimization
    {
        public static void Run()
        {
            string directory = NfsWorldSetup.BasePath + "/ArtPass/Optimized";
            var pipeline = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(directory + "/BatchedPipeline.asset");
            NfsWorldSetup.Require(pipeline != null, "Generate the art scene first.");
            Configure(pipeline, directory);
        }

        public static void Configure(UniversalRenderPipelineAsset pipeline, string directory)
        {
            // The scene's Lit/array materials never sample scene colour. AO still
            // requests its depth input, even without the pipeline's blanket copy.
            pipeline.supportsCameraOpaqueTexture = false;
            pipeline.supportsCameraDepthTexture = false;
            // Keep the display/HUD at 1080p, while reducing integrated-GPU fill cost.
            pipeline.renderScale = .75f;
            pipeline.upscalingFilter = UpscalingFilterSelection.FSR;
            pipeline.fsrOverrideSharpness = true;
            pipeline.fsrSharpness = .5f;
            string path = directory + "/PerformanceRenderer.asset";
            var source = pipeline.rendererDataList[0];
            var renderer = AssetDatabase.LoadAssetAtPath<UniversalRendererData>(path);
            if (renderer == null)
            {
                renderer = Object.Instantiate((UniversalRendererData)source);
                renderer.name = "Rockport scene-local renderer";
                // Clone features as subassets: never alter the project renderer.
                renderer.rendererFeatures.Clear();
                AssetDatabase.CreateAsset(renderer, path);
                foreach (var feature in source.rendererFeatures)
                {
                    var copy = Object.Instantiate(feature);
                    copy.name = feature.name;
                    AssetDatabase.AddObjectToAsset(copy, renderer);
                    renderer.rendererFeatures.Add(copy);
                }
            }
            foreach (var feature in renderer.rendererFeatures)
            {
                if (feature.GetType().Name != "ScreenSpaceAmbientOcclusion") continue;
                var data = new SerializedObject(feature);
                var settings = data.FindProperty("m_Settings");
                settings.FindPropertyRelative("Source").intValue = 0; // Reconstruct from depth.
                settings.FindPropertyRelative("NormalSamples").intValue = 2; // High-quality reconstruction.
                settings.FindPropertyRelative("Downsample").boolValue = true;
                data.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(feature);
            }
            // AO already requests a depth prepass. Reuse it to reject hidden
            // opaque fragments before expensive lighting instead of shading
            // layers that another surface will cover in the same frame.
            renderer.depthPrimingMode = DepthPrimingMode.Forced;
            var rendererData = new SerializedObject(renderer);
            var map = rendererData.FindProperty("m_RendererFeatureMap");
            map.arraySize = renderer.rendererFeatures.Count;
            for (int index = 0; index < renderer.rendererFeatures.Count; index++)
            {
                AssetDatabase.TryGetGUIDAndLocalFileIdentifier(renderer.rendererFeatures[index], out string _, out long id);
                map.GetArrayElementAtIndex(index).longValue = id;
            }
            rendererData.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(renderer);
            var pipelineData = new SerializedObject(pipeline);
            pipelineData.FindProperty("m_RendererDataList").GetArrayElementAtIndex(0).objectReferenceValue = renderer;
            pipelineData.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(pipeline);
            AssetDatabase.SaveAssets();
            File.WriteAllText(Path.GetFullPath(Path.Combine(Application.dataPath, "../../artifacts/NfsWorld/render-optimization.json")),
                "{\"renderScale\":0.75,\"upscaling\":\"FSR1\",\"sharpness\":0.5,\"opaqueCopy\":false,\"blanketDepthCopy\":false,\"depthPriming\":true,\"ao\":\"half-resolution high-quality depth reconstruction\",\"sunAndShadowsChanged\":false}\n");
        }
    }
}
