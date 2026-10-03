using System.Collections;
using UnityEngine.SceneManagement;

namespace Alabama.Tests
{
    internal static class ReviewSceneLoader
    {
        public static IEnumerator Load(string name)
        {
#if UNITY_EDITOR
            UnityEditor.SceneManagement.EditorSceneManager.LoadSceneInPlayMode(
                "Assets/Alabama/Scenes/" + name + ".unity", new LoadSceneParameters(LoadSceneMode.Single));
            yield return null;
#else
            yield return SceneManager.LoadSceneAsync(name, LoadSceneMode.Single);
#endif
        }
    }
}
