#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public static class BattleEventBroadcastHudBuilder
{
    private const string BattleArenaScenePath = "Assets/Scenes/BattleArena.unity/BattleArena.unity";
    private const string TvSpritePath = "Assets/Art/UI/Battle/EventBroadcast/tv_frame.png";
    private const string CanvasName = "BattleCanvas";
    private const string ReferenceHudName = "EventDialogueHud";
    private const string RootName = "EventBroadcastHud";
    private const string ReporterFrameRootName = "ReporterFrameRoot";
    private const string TvFrameName = "TvFrame";
    private const float DefaultWidth = 420f;
    private const float ScreenX = 80f / 1211f;
    private const float ScreenTop = 250f / 853f;
    private const float ScreenRight = 982f / 1211f;
    private const float ScreenBottom = 746f / 853f;

    [MenuItem("Tools/Titan Destroyer/Rebuild Event Broadcast HUD")]
    private static void RebuildLoadedScene()
    {
        ConfigureTvSpriteImporter();

        Scene scene = SceneManager.GetActiveScene();
        if (!scene.IsValid())
        {
            Debug.LogError("Event broadcast HUD rebuild failed. No valid scene is loaded.");
            return;
        }

        if (BuildInScene(scene))
        {
            EditorSceneManager.MarkSceneDirty(scene);
            Debug.Log("Event broadcast HUD rebuilt under BattleCanvas.");
        }
    }

    public static void RebuildBattleArenaForBatch()
    {
        ConfigureTvSpriteImporter();
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
            Debug.LogError($"Event broadcast HUD rebuild failed. {CanvasName} was not found.");
            return false;
        }

        Sprite tvSprite = AssetDatabase.LoadAssetAtPath<Sprite>(TvSpritePath);
        if (tvSprite == null)
        {
            Debug.LogError($"Event broadcast HUD rebuild failed. TV sprite was not found at {TvSpritePath}.");
            return false;
        }

        RectTransform referenceRect = canvas.transform.Find(ReferenceHudName) as RectTransform;
        GameObject rootObject = FindOrCreateUiObject(RootName, canvas.transform);
        RectTransform rootRect = rootObject.GetComponent<RectTransform>();
        ApplyRootRect(rootRect, referenceRect, tvSprite);

        CanvasGroup canvasGroup = rootObject.GetComponent<CanvasGroup>();
        if (canvasGroup == null)
        {
            canvasGroup = rootObject.AddComponent<CanvasGroup>();
        }
        canvasGroup.alpha = 1f;
        canvasGroup.interactable = false;
        canvasGroup.blocksRaycasts = false;

        GameObject reporterRootObject = FindOrCreateUiObject(ReporterFrameRootName, rootObject.transform);
        RectTransform reporterRect = reporterRootObject.GetComponent<RectTransform>();
        ApplyReporterFrameRect(reporterRect);
        reporterRootObject.transform.SetSiblingIndex(0);

        GameObject tvFrameObject = FindOrCreateUiObject(TvFrameName, rootObject.transform);
        RectTransform tvRect = tvFrameObject.GetComponent<RectTransform>();
        StretchToParent(tvRect);

        Image tvImage = tvFrameObject.GetComponent<Image>();
        if (tvImage == null)
        {
            tvImage = tvFrameObject.AddComponent<Image>();
        }
        tvImage.sprite = tvSprite;
        tvImage.preserveAspect = true;
        tvImage.raycastTarget = false;
        tvImage.color = Color.white;
        tvFrameObject.transform.SetAsLastSibling();

        EditorUtility.SetDirty(rootObject);
        EditorUtility.SetDirty(canvas.gameObject);
        return true;
    }

    private static void ConfigureTvSpriteImporter()
    {
        TextureImporter importer = AssetImporter.GetAtPath(TvSpritePath) as TextureImporter;
        if (importer == null)
        {
            AssetDatabase.ImportAsset(TvSpritePath, ImportAssetOptions.ForceUpdate);
            importer = AssetImporter.GetAtPath(TvSpritePath) as TextureImporter;
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

    private static void ApplyRootRect(RectTransform target, RectTransform referenceRect, Sprite tvSprite)
    {
        float width = referenceRect != null ? referenceRect.sizeDelta.x : DefaultWidth;
        float height = width * tvSprite.rect.height / Mathf.Max(1f, tvSprite.rect.width);

        if (referenceRect != null)
        {
            target.anchorMin = referenceRect.anchorMin;
            target.anchorMax = referenceRect.anchorMax;
            target.pivot = referenceRect.pivot;
            target.anchoredPosition = referenceRect.anchoredPosition;
        }
        else
        {
            target.anchorMin = Vector2.one;
            target.anchorMax = Vector2.one;
            target.pivot = Vector2.one;
            target.anchoredPosition = new Vector2(-28f, -28f);
        }

        target.sizeDelta = new Vector2(width, height);
        target.localScale = Vector3.one;
        target.localRotation = Quaternion.identity;
    }

    private static void ApplyReporterFrameRect(RectTransform rect)
    {
        rect.anchorMin = new Vector2(ScreenX, 1f - ScreenBottom);
        rect.anchorMax = new Vector2(ScreenRight, 1f - ScreenTop);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
        rect.localScale = Vector3.one;
        rect.localRotation = Quaternion.identity;
    }

    private static void StretchToParent(RectTransform rect)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
        rect.localScale = Vector3.one;
        rect.localRotation = Quaternion.identity;
    }

    private static GameObject FindOrCreateUiObject(string objectName, Transform parent)
    {
        Transform existing = parent.Find(objectName);
        if (existing != null)
        {
            return existing.gameObject;
        }

        GameObject created = new(objectName, typeof(RectTransform));
        created.transform.SetParent(parent, false);
        return created;
    }
}
#endif
