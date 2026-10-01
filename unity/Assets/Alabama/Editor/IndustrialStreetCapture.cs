using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Alabama.Editor
{
    public static class IndustrialStreetCapture
    {
        private static int frames;
        private static double deadline;

        public static void Run()
        {
            IndustrialStreetVerify.Run();
            frames = 0;
            deadline = EditorApplication.timeSinceStartup + 90;
            EditorApplication.update += CaptureWhenReady;
        }

        private static void CaptureWhenReady()
        {
            if (EditorApplication.timeSinceStartup > deadline)
            {
                Finish(new TimeoutException("Street capture did not become ready."));
                return;
            }
            if (++frames < 30 || EditorApplication.isCompiling || EditorApplication.isUpdating) return;
            EditorApplication.update -= CaptureWhenReady;
            try
            {
                var camera = Camera.main;
                if (camera == null) throw new InvalidOperationException("Chase camera is missing.");
                string output = Path.GetFullPath(Path.Combine(Application.dataPath, "../../artifacts/StreetCapture"));
                Directory.CreateDirectory(output);
                VehicleCapture.Save(camera, Path.Combine(output, "chase.png"));
                camera.transform.position = new Vector3(26, 15, -17);
                camera.transform.LookAt(new Vector3(0, 3, 70));
                camera.fieldOfView = 64;
                VehicleCapture.Save(camera, Path.Combine(output, "overview.png"));
                Debug.Log("Street captures saved: " + output);
                Finish(null);
            }
            catch (Exception exception) { Finish(exception); }
        }

        private static void Finish(Exception error)
        {
            EditorApplication.update -= CaptureWhenReady;
            if (error != null) Debug.LogException(error);
            if (Application.isBatchMode) EditorApplication.Exit(error == null ? 0 : 1);
        }
    }
}
