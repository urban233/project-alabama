using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Alabama.Editor
{
    /// <summary>Captures the actual URP output; requires a graphics device.</summary>
    public static class VehicleCapture
    {
        private static int frames;
        private static double deadline;

        public static void Run()
        {
            EditorSceneManager.OpenScene(VehicleReviewScene.ScenePath);
            VehicleAssetSetup.Verify();
            frames = 0;
            deadline = EditorApplication.timeSinceStartup + 90;
            EditorApplication.update += CaptureWhenReady;
        }

        private static void CaptureWhenReady()
        {
            if (EditorApplication.timeSinceStartup > deadline)
            {
                Finish(new TimeoutException("Vehicle capture did not become ready."));
                return;
            }
            if (++frames < 30 || EditorApplication.isCompiling || EditorApplication.isUpdating) return;
            EditorApplication.update -= CaptureWhenReady;
            try
            {
                string output = Path.GetFullPath(Path.Combine(Application.dataPath, "../../artifacts/VehicleCapture"));
                Directory.CreateDirectory(output);
                var camera = Camera.main;
                if (camera == null) throw new InvalidOperationException("Review camera is missing.");
                Save(camera, Path.Combine(output, "rear.png"));
                camera.transform.position = new Vector3(3.9f, 1.8f, 6);
                camera.transform.LookAt(new Vector3(0, .7f, 0));
                Save(camera, Path.Combine(output, "front.png"));
                Debug.Log("Vehicle review captures saved: " + output);
                Finish(null);
            }
            catch (Exception exception) { Finish(exception); }
        }

        internal static void Save(Camera camera, string path)
        {
            var target = new RenderTexture(1920, 1080, 24, RenderTextureFormat.ARGB32);
            target.antiAliasing = 4;
            var texture = new Texture2D(1920, 1080, TextureFormat.RGB24, false);
            var previous = RenderTexture.active;
            try
            {
                target.Create();
                var request = new UniversalRenderPipeline.SingleCameraRequest { destination = target };
                if (!RenderPipeline.SupportsRenderRequest(camera, request))
                    throw new InvalidOperationException("The active pipeline does not support URP capture.");
                RenderPipeline.SubmitRenderRequest(camera, request);
                RenderTexture.active = target;
                texture.ReadPixels(new Rect(0, 0, 1920, 1080), 0, 0);
                texture.Apply();
                File.WriteAllBytes(path, texture.EncodeToPNG());
            }
            finally
            {
                RenderTexture.active = previous;
                target.Release();
                UnityEngine.Object.DestroyImmediate(target);
                UnityEngine.Object.DestroyImmediate(texture);
            }
        }

        private static void Finish(Exception error)
        {
            EditorApplication.update -= CaptureWhenReady;
            if (error != null) Debug.LogException(error);
            if (Application.isBatchMode) EditorApplication.Exit(error == null ? 0 : 1);
        }
    }
}
