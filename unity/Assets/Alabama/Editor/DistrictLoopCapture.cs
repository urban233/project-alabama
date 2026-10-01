using System;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace Alabama.Editor
{
    /// <summary>Actual URP chase and route overview renders from the saved district scene.</summary>
    public static class DistrictLoopCapture
    {
        private static int frames;
        private static double deadline;

        public static void Run()
        {
            DistrictLoopScene.Verify();
            frames = 0;
            deadline = EditorApplication.timeSinceStartup + 90;
            EditorApplication.update += CaptureWhenReady;
        }

        private static void CaptureWhenReady()
        {
            if (EditorApplication.timeSinceStartup > deadline)
            {
                Finish(new TimeoutException("District capture did not become ready."));
                return;
            }
            if (++frames < 30 || EditorApplication.isCompiling || EditorApplication.isUpdating) return;
            EditorApplication.update -= CaptureWhenReady;
            try
            {
                var camera = Camera.main;
                if (camera == null) throw new InvalidOperationException("Chase camera is missing.");
                var car = GameObject.Find("E46 driver car");
                if (car == null) throw new InvalidOperationException("Drive car is missing.");
                string output = Path.GetFullPath(Path.Combine(Application.dataPath, "../../artifacts/LoopCapture"));
                Directory.CreateDirectory(output);
                var carPosition = car.transform.position;
                var carRotation = car.transform.rotation;
                var cameraPosition = camera.transform.position;
                var cameraRotation = camera.transform.rotation;
                float fieldOfView = camera.fieldOfView;
                bool fog = RenderSettings.fog;
                try
                {
                    VehicleCapture.Save(camera, Path.Combine(output, "chase.png"));
                    car.transform.SetPositionAndRotation(new Vector3(202.3f, .22f, 180), Quaternion.Euler(0, 180, 0));
                    camera.transform.position = car.transform.TransformPoint(new Vector3(0, 1.9f, -6));
                    camera.transform.LookAt(car.transform.TransformPoint(new Vector3(0, 1.2f, 12)));
                    VehicleCapture.Save(camera, Path.Combine(output, "return-street.png"));
                    camera.orthographic = true;
                    camera.orthographicSize = 215;
                    camera.transform.position = new Vector3(100, 300, 115);
                    camera.transform.LookAt(new Vector3(100, 0, 115));
                    RenderSettings.fog = false; // Diagnostic layout view only.
                    VehicleCapture.Save(camera, Path.Combine(output, "layout.png"));
                }
                finally
                {
                    RenderSettings.fog = fog;
                    camera.orthographic = false;
                    camera.fieldOfView = fieldOfView;
                    camera.transform.SetPositionAndRotation(cameraPosition, cameraRotation);
                    car.transform.SetPositionAndRotation(carPosition, carRotation);
                }
                Debug.Log("District captures saved: " + output);
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
