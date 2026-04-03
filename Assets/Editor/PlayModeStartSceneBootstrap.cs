#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;

[InitializeOnLoad]
public static class PlayModeStartSceneBootstrap
{
    static PlayModeStartSceneBootstrap()
    {
        EnsureCurrentSceneStart();
    }

    [InitializeOnLoadMethod]
    private static void EnsureCurrentSceneStart()
    {
        if (EditorSceneManager.playModeStartScene == null)
        {
            return;
        }

        EditorSceneManager.playModeStartScene = null;
    }
}
#endif
