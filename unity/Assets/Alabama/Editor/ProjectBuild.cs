using System;
using System.IO;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace Alabama.Editor
{
    public static class ProjectBuild
    {
        [MenuItem("Alabama/Build/Windows Development")]
        public static void BuildWindows()
        {
            NfsWorldArtDirection.BuildGame();
        }

        [MenuItem("Alabama/Build/Foundation Review")]
        public static void BuildFoundationReview()
        {
            ProjectFoundation.Verify();
            BuildScene(ProjectFoundation.ScenePath, "foundation-review");
        }

        [MenuItem("Alabama/Build/Vehicle Review")]
        public static void BuildVehicleReview()
        {
            VehicleAssetSetup.Verify();
            BuildScene(VehicleReviewScene.ScenePath, "vehicle-review");
        }

        [MenuItem("Alabama/Build/Industrial Street")]
        public static void BuildIndustrialStreet()
        {
            IndustrialStreetVerify.Run();
            BuildScene(IndustrialStreetScene.ScenePath, "industrial-street");
        }

        [MenuItem("Alabama/Build/Handling Course")]
        public static void BuildHandlingCourse()
        {
            HandlingCourseSetup.Verify();
            BuildScene(HandlingCourseSetup.ScenePath, "handling-course");
        }

        [MenuItem("Alabama/Build/District Loop")]
        public static void BuildDistrictLoop()
        {
            DistrictLoopScene.Verify();
            BuildScene(DistrictLoopScene.ScenePath, "district-loop");
        }

        [MenuItem("Alabama/Build/District Loop Release")]
        public static void BuildDistrictLoopRelease()
        {
            DistrictLoopScene.Verify();
            BuildScene(DistrictLoopScene.ScenePath, "district-loop-release", false);
        }

        private static void BuildScene(string scenePath, string directory, bool development = true)
        {
            if (!BuildPipeline.IsBuildTargetSupported(BuildTargetGroup.Standalone, BuildTarget.StandaloneWindows64))
            {
                throw new InvalidOperationException("Install Windows build support for the pinned Unity editor.");
            }
            var root = Directory.GetParent(Application.dataPath).Parent.FullName;
            var output = Path.Combine(root, "builds", directory, "Alabama.exe");
            Directory.CreateDirectory(Path.GetDirectoryName(output));
            PlayerSettings.SetScriptingBackend(UnityEditor.Build.NamedBuildTarget.Standalone, ScriptingImplementation.Mono2x);
            PlayerSettings.enableFrameTimingStats = true;
            var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
            {
                scenes = new[] { scenePath },
                locationPathName = output,
                target = BuildTarget.StandaloneWindows64,
                options = (development ? BuildOptions.Development : BuildOptions.None) | BuildOptions.StrictMode
            });
            if (report.summary.result != BuildResult.Succeeded || report.summary.totalErrors != 0)
            {
                throw new InvalidOperationException($"Windows build failed: {report.summary.result}, {report.summary.totalErrors} errors.");
            }
            Debug.Log($"Windows {(development ? "development" : "release")} build succeeded: {output}");
        }
    }
}
