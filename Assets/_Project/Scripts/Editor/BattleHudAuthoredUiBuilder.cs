#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

public static class BattleHudAuthoredUiBuilder
{
    [MenuItem("Tools/Titan Destroyer/Rebuild Battle HUD Authored UI")]
    private static void RebuildBattleHudAuthoredUi()
    {
        HUDPresenter presenter = FindSceneHudPresenter();
        if (presenter == null)
        {
            Debug.LogError("Battle HUD rebuild failed. HUDPresenter was not found in the loaded scene.");
            return;
        }

        presenter.RebuildAuthoredUiForEditor();
        Debug.Log("Battle HUD authored UI rebuilt under BattleCanvas.");
    }

    private static HUDPresenter FindSceneHudPresenter()
    {
        HUDPresenter[] presenters = Resources.FindObjectsOfTypeAll<HUDPresenter>();
        for (int i = 0; i < presenters.Length; i++)
        {
            HUDPresenter presenter = presenters[i];
            if (presenter == null || !presenter.gameObject.scene.IsValid() || EditorUtility.IsPersistent(presenter))
            {
                continue;
            }

            return presenter;
        }

        return null;
    }
}
#endif
