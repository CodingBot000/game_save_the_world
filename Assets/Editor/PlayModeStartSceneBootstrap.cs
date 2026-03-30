#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;

[InitializeOnLoad]
public static class PlayModeStartSceneBootstrap
{
    private const string BootScenePath = "Assets/Scenes/Boot.unity/Boot.unity";

    static PlayModeStartSceneBootstrap()
    {
        EnsureBootSceneStart();
    }

    [InitializeOnLoadMethod]
    private static void EnsureBootSceneStart()
    {
        SceneAsset bootScene = AssetDatabase.LoadAssetAtPath<SceneAsset>(BootScenePath);
        if (bootScene == null)
        {
            return;
        }

        if (EditorSceneManager.playModeStartScene == bootScene)
        {
            return;
        }

        EditorSceneManager.playModeStartScene = bootScene;
    }
}
#endif
