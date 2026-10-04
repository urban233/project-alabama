using System.Linq;
using UnityEditor;
using UnityEditor.PackageManager;
using UnityEditor.PackageManager.Requests;
using UnityEngine;

namespace Alabama.Editor
{
    public static class MicroSlicePackages
    {
        private static AddAndRemoveRequest request;
        private static double deadline;

        // Batch installer owns shutdown because UPM completes asynchronously.
        public static void Install()
        {
            request = Client.AddAndRemove(new[] { "com.unity.splines@2.8.4", "com.unity.probuilder@6.0.3" });
            deadline = EditorApplication.timeSinceStartup + 600;
            EditorApplication.update += Poll;
        }

        private static void Poll()
        {
            if (!request.IsCompleted)
            {
                if (EditorApplication.timeSinceStartup < deadline) return;
                EditorApplication.update -= Poll;
                Debug.LogError("Micro-slice package installation timed out.");
                EditorApplication.Exit(2);
                return;
            }
            EditorApplication.update -= Poll;
            if (request.Status == StatusCode.Success)
            {
                Debug.Log("Micro-slice packages resolved: " + string.Join(", ", request.Result.Select(p => p.name + "@" + p.version)));
                EditorApplication.Exit(0);
            }
            else
            {
                Debug.LogError(request.Error?.message);
                EditorApplication.Exit(1);
            }
        }
    }
}
