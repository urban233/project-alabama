using UnityEditor;
using UnityEditor.PackageManager;
using UnityEditor.PackageManager.Requests;
using UnityEngine;

namespace Alabama.Editor
{
    public static class RoadNavigationPackage
    {
        private static AddAndRemoveRequest request;
        private static double deadline;
        public static void Install()
        {
            request = Client.AddAndRemove(new[] { "com.unity.ai.navigation@2.0.15" },new string[0]);
            deadline = EditorApplication.timeSinceStartup+600;
            EditorApplication.update += Poll;
        }
        private static void Poll()
        {
            if (!request.IsCompleted && EditorApplication.timeSinceStartup < deadline) return;
            EditorApplication.update -= Poll;
            if (request.Status == StatusCode.Success) { Debug.Log("AI Navigation 2.0.15 installed."); EditorApplication.Exit(0); }
            else { Debug.LogError(request.Error?.message ?? "AI Navigation installation timed out."); EditorApplication.Exit(1); }
        }
    }
}
