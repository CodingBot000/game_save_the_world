#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class BattleAimPointTargetingBuilder
{
    private const string BattleArenaScenePath = "Assets/Scenes/BattleArena.unity/BattleArena.unity";
    private const string CanvasName = "BattleCanvas";
    private const string NormalBaseSpritePath = "Assets/Art/UI/Battle/Targeting/targeting_normal_base.png";
    private const string NormalInnerSpritePath = "Assets/Art/UI/Battle/Targeting/targeting_normal_inner.png";
    private const string AlertBaseSpritePath = "Assets/Art/UI/Battle/Targeting/targeting_alert_base.png";
    private const string AlertInnerSpritePath = "Assets/Art/UI/Battle/Targeting/targeting_alert_inner.png";
    private static readonly Vector2 MarkerSize = new(66f, 59.333f);

    [MenuItem("Tools/Titan Destroyer/Rebuild AimPoint Targeting HUD")]
    private static void RebuildLoadedScene()
    {
        ConfigureTargetingSpriteImporters();

        Scene scene = SceneManager.GetActiveScene();
        if (!scene.IsValid())
        {
            Debug.LogError("AimPoint targeting HUD rebuild failed. No valid scene is loaded.");
            return;
        }

        if (BuildInScene(scene))
        {
            EditorSceneManager.MarkSceneDirty(scene);
            Debug.Log("AimPoint targeting HUD rebuilt under BattleCanvas.");
        }
    }

    public static void RebuildBattleArenaForBatch()
    {
        ConfigureTargetingSpriteImporters();
        Scene scene = EditorSceneManager.OpenScene(BattleArenaScenePath);
        if (BuildInScene(scene))
        {
            EditorSceneManager.SaveScene(scene);
        }
    }

    private static bool BuildInScene(Scene scene)
    {
        Canvas canvas = FindSceneCanvas(scene);
        if (canvas == null)
        {
            Debug.LogError($"AimPoint targeting HUD rebuild failed. {CanvasName} was not found.");
            return false;
        }

        Sprite normalBase = LoadSprite(NormalBaseSpritePath);
        Sprite normalInner = LoadSprite(NormalInnerSpritePath);
        Sprite alertBase = LoadSprite(AlertBaseSpritePath);
        Sprite alertInner = LoadSprite(AlertInnerSpritePath);
        if (normalBase == null || normalInner == null || alertBase == null || alertInner == null)
        {
            Debug.LogError("AimPoint targeting HUD rebuild failed. One or more targeting sprites are missing.");
            return false;
        }

        BattleAimPointTargetingPresenter presenter =
            canvas.GetComponent<BattleAimPointTargetingPresenter>() ??
            canvas.gameObject.AddComponent<BattleAimPointTargetingPresenter>();

        SerializedObject serializedPresenter = new(presenter);
        SetSerializedReference(serializedPresenter, "targetCanvas", canvas);
        SetSerializedReference(serializedPresenter, "normalBaseSprite", normalBase);
        SetSerializedReference(serializedPresenter, "normalInnerSprite", normalInner);
        SetSerializedReference(serializedPresenter, "alertBaseSprite", alertBase);
        SetSerializedReference(serializedPresenter, "alertInnerSprite", alertInner);
        SetSerializedVector2(serializedPresenter, "markerSize", MarkerSize);
        serializedPresenter.ApplyModifiedPropertiesWithoutUndo();

        BattleController battleController = FindSceneComponent<BattleController>(scene);
        if (battleController != null)
        {
            SerializedObject serializedBattleController = new(battleController);
            SetSerializedReference(serializedBattleController, "aimPointTargetingPresenter", presenter);
            serializedBattleController.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(battleController);
        }

        EditorUtility.SetDirty(presenter);
        EditorUtility.SetDirty(canvas.gameObject);
        return true;
    }

    private static void ConfigureTargetingSpriteImporters()
    {
        ConfigureSpriteImporter(NormalBaseSpritePath);
        ConfigureSpriteImporter(NormalInnerSpritePath);
        ConfigureSpriteImporter(AlertBaseSpritePath);
        ConfigureSpriteImporter(AlertInnerSpritePath);
    }

    private static void ConfigureSpriteImporter(string assetPath)
    {
        TextureImporter importer = AssetImporter.GetAtPath(assetPath) as TextureImporter;
        if (importer == null)
        {
            AssetDatabase.ImportAsset(assetPath, ImportAssetOptions.ForceUpdate);
            importer = AssetImporter.GetAtPath(assetPath) as TextureImporter;
        }

        if (importer == null)
        {
            return;
        }

        importer.textureType = TextureImporterType.Sprite;
        importer.spriteImportMode = SpriteImportMode.Single;
        importer.mipmapEnabled = false;
        importer.alphaIsTransparency = true;
        importer.wrapMode = TextureWrapMode.Clamp;
        importer.npotScale = TextureImporterNPOTScale.None;
        EditorUtility.SetDirty(importer);
        importer.SaveAndReimport();
    }

    private static Sprite LoadSprite(string assetPath)
    {
        Sprite sprite = AssetDatabase.LoadAssetAtPath<Sprite>(assetPath);
        if (sprite == null)
        {
            AssetDatabase.ImportAsset(assetPath, ImportAssetOptions.ForceUpdate);
            sprite = AssetDatabase.LoadAssetAtPath<Sprite>(assetPath);
        }

        return sprite;
    }

    private static Canvas FindSceneCanvas(Scene scene)
    {
        foreach (GameObject root in scene.GetRootGameObjects())
        {
            Canvas[] canvases = root.GetComponentsInChildren<Canvas>(true);
            for (int i = 0; i < canvases.Length; i++)
            {
                if (canvases[i].name == CanvasName)
                {
                    return canvases[i];
                }
            }
        }

        return null;
    }

    private static T FindSceneComponent<T>(Scene scene) where T : Component
    {
        foreach (GameObject root in scene.GetRootGameObjects())
        {
            T component = root.GetComponentInChildren<T>(true);
            if (component != null)
            {
                return component;
            }
        }

        return null;
    }

    private static void SetSerializedReference(SerializedObject serializedObject, string propertyName, Object value)
    {
        SerializedProperty property = serializedObject.FindProperty(propertyName);
        if (property != null)
        {
            property.objectReferenceValue = value;
        }
    }

    private static void SetSerializedVector2(SerializedObject serializedObject, string propertyName, Vector2 value)
    {
        SerializedProperty property = serializedObject.FindProperty(propertyName);
        if (property != null)
        {
            property.vector2Value = value;
        }
    }
}
#endif
