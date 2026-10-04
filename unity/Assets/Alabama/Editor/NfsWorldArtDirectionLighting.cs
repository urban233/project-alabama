using System.IO;
using Alabama.Driving;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Alabama.Editor
{
    /// <summary>Staged, scene-local lighting treatment after material/geometry comparison.</summary>
    public static class NfsWorldArtDirectionLighting
    {
        [System.Serializable] internal sealed class Recipe
        {
            public string recipe;
            public float[] sunColour, ambientSky, ambientEquator, ambientGround, diffuseFill, fogColour;
            public float sunIntensity;
        }

        internal static Recipe Read() => JsonUtility.FromJson<Recipe>(File.ReadAllText(
            Path.GetFullPath(Path.Combine(Application.dataPath, "../../docs/art-direction/downtown-lighting-recipe.json"))));
        internal static Color Colour(float[] values) => new Color(values[0], values[1], values[2]);

        [MenuItem("Alabama/NFS World/Apply Reviewed Art Direction Lighting")]
        public static void Apply()
        {
            EditorSceneManager.OpenScene(NfsWorldArtDirection.ScenePath);
            ConfigureActiveScene();
            EditorSceneManager.SaveScene(UnityEngine.SceneManagement.SceneManager.GetActiveScene());
            NfsWorldArtDirection.Verify();
        }

        internal static void ConfigureActiveScene()
        {
            var recipe = Read();
            RenderSettings.ambientSkyColor = Colour(recipe.ambientSky);
            RenderSettings.ambientEquatorColor = Colour(recipe.ambientEquator);
            RenderSettings.ambientGroundColor = Colour(recipe.ambientGround);
            RenderSettings.fogColor = Colour(recipe.fogColour);
            RenderSettings.sun.color = Colour(recipe.sunColour);
            RenderSettings.sun.intensity = recipe.sunIntensity;
            var settings = Object.FindFirstObjectByType<NfsWorldRenderSettings>();
            NfsWorldSetup.Require(settings != null, "Styled scene requires shared rendering settings.");
            settings.ConfigureDiffuseFill(Colour(recipe.diffuseFill));
            var probe = new UnityEngine.Rendering.SphericalHarmonicsL2();
            probe.AddAmbientLight(Colour(recipe.diffuseFill));
            RenderSettings.ambientProbe = probe;
            EditorUtility.SetDirty(settings);
        }
    }
}
