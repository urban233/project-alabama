using System;
using System.IO;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace Alabama.Editor
{
    public static class MicroSliceCapture
    {
        private static int frames;
        private static double deadline;
        public static void Run()
        {
            MicroSliceSetup.Verify();frames=0;deadline=EditorApplication.timeSinceStartup+120;
            EditorApplication.update+=Capture;
        }
        private static void Capture()
        {
            if (EditorApplication.timeSinceStartup>deadline) {EditorApplication.update-=Capture;Debug.LogError("Capture timed out.");EditorApplication.Exit(1);return;}
            if (++frames<30 || EditorApplication.isCompiling || EditorApplication.isUpdating) return;
            EditorApplication.update-=Capture;
            try
            {
                string output=Path.Combine(MicroSliceSetup.RootPath,"artifacts/MicroSliceCapture");Directory.CreateDirectory(output);
                var camera=Camera.main;
                camera.transform.position=new Vector3(-88,27,65);camera.transform.LookAt(new Vector3(-100,26,41));
                VehicleCapture.Save(camera,Path.Combine(output,"garage-courtyard.png"));
                camera.transform.position=new Vector3(109,25,142);camera.transform.LookAt(new Vector3(183,4,28));
                VehicleCapture.Save(camera,Path.Combine(output,"hillside-descent.png"));
                camera.transform.position=new Vector3(0,340,10);camera.transform.rotation=Quaternion.Euler(90,0,0);
                camera.orthographic=true;camera.orthographicSize=245;RenderSettings.fog=false;
                VehicleCapture.Save(camera,Path.Combine(output,"course-overview.png"));
                Debug.Log("Micro-slice URP captures saved: "+output);
                if (Application.isBatchMode) EditorApplication.Exit(0);
            }
            catch(Exception error) {Debug.LogException(error);if(Application.isBatchMode) EditorApplication.Exit(1);}
        }
        public static void Build()
        {
            MicroSliceSetup.Verify();
            string output=Path.Combine(MicroSliceSetup.RootPath,"builds/micro-slice/Alabama.exe");Directory.CreateDirectory(Path.GetDirectoryName(output));
            PlayerSettings.SetScriptingBackend(UnityEditor.Build.NamedBuildTarget.Standalone,ScriptingImplementation.Mono2x);
            var report=BuildPipeline.BuildPlayer(new BuildPlayerOptions {scenes=new[]{MicroSliceSetup.ScenePath},locationPathName=output,
                target=BuildTarget.StandaloneWindows64,options=BuildOptions.Development|BuildOptions.StrictMode});
            MicroSliceSetup.Require(report.summary.result==BuildResult.Succeeded && report.summary.totalErrors==0,"Micro-slice build failed.");
            Debug.Log("Micro-slice Windows build: "+output);
        }
    }
}
