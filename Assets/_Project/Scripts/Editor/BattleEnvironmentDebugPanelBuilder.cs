#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

public static class BattleEnvironmentDebugPanelBuilder
{
    [MenuItem("Tools/Titan Destroyer/Rebuild Environment Debug Panel")]
    private static void RebuildEnvironmentDebugPanel()
    {
        EnvironmentThemeDebugPanel panel = FindScenePanel();
        if (panel == null)
        {
            Debug.LogError("Environment debug panel rebuild failed. EnvironmentThemeDebugPanel was not found in the loaded scene.");
            return;
        }

        panel.RebuildAuthoredUiForEditor();
        Debug.Log("Environment debug panel rebuilt under BattleCanvas.");
    }

    private static EnvironmentThemeDebugPanel FindScenePanel()
    {
        EnvironmentThemeDebugPanel[] panels = Resources.FindObjectsOfTypeAll<EnvironmentThemeDebugPanel>();
        for (int i = 0; i < panels.Length; i++)
        {
            EnvironmentThemeDebugPanel panel = panels[i];
            if (panel == null || !panel.gameObject.scene.IsValid() || EditorUtility.IsPersistent(panel))
            {
                continue;
            }

            return panel;
        }

        return null;
    }
}
#endif
