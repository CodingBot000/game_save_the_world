#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public static class BattleEventDialogueHudBuilder
{
    private const string BattleArenaScenePath = "Assets/Scenes/BattleArena.unity/BattleArena.unity";
    private const string CanvasName = "BattleCanvas";
    private const string RootName = "EventDialogueHud";
    private const string PlayerShoutImageName = "PlayerShoutImage";
    private const string PlayerShoutSpritePath = "Assets/Art/UI/Battle/EventDialogue/player_shout.png";
    private static readonly Vector2 RootSize = new(420f, 283f);
    private static readonly Vector2 RootAnchoredPosition = Vector2.zero;

    [MenuItem("Tools/Titan Destroyer/Rebuild Event Dialogue HUD")]
    private static void RebuildLoadedScene()
    {
        ConfigurePlayerShoutSpriteImporter();

        Scene scene = SceneManager.GetActiveScene();
        if (!scene.IsValid())
        {
            Debug.LogError("Event dialogue HUD rebuild failed. No valid scene is loaded.");
            return;
        }

        if (BuildInScene(scene))
        {
            EditorSceneManager.MarkSceneDirty(scene);
            Debug.Log("Event dialogue HUD rebuilt under BattleCanvas.");
        }
    }

    public static void RebuildBattleArenaForBatch()
    {
        ConfigurePlayerShoutSpriteImporter();
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
            Debug.LogError($"Event dialogue HUD rebuild failed. {CanvasName} was not found.");
            return false;
        }

        Sprite playerShoutSprite = AssetDatabase.LoadAssetAtPath<Sprite>(PlayerShoutSpritePath);
        if (playerShoutSprite == null)
        {
            Debug.LogError($"Event dialogue HUD rebuild failed. Player shout sprite was not found at {PlayerShoutSpritePath}.");
            return false;
        }

        GameObject rootObject = FindOrCreateUiObject(RootName, canvas.transform);
        RectTransform rootRect = rootObject.GetComponent<RectTransform>();
        ApplyRootRect(rootRect);

        CanvasGroup canvasGroup = rootObject.GetComponent<CanvasGroup>() ?? rootObject.AddComponent<CanvasGroup>();
        canvasGroup.alpha = 0f;
        canvasGroup.interactable = false;
        canvasGroup.blocksRaycasts = false;

        SetChildActive(rootObject.transform, "BubbleBackground", false);
        SetChildActive(rootObject.transform, "FrameAnimationRoot", false);

        GameObject shoutObject = FindOrCreateUiObject(PlayerShoutImageName, rootObject.transform);
        RectTransform shoutRect = shoutObject.GetComponent<RectTransform>();
        ApplyPlayerShoutRect(shoutRect, playerShoutSprite);

        Image shoutImage = shoutObject.GetComponent<Image>() ?? shoutObject.AddComponent<Image>();
        shoutImage.sprite = playerShoutSprite;
        shoutImage.preserveAspect = true;
        shoutImage.raycastTarget = false;
        shoutImage.color = Color.white;
        shoutImage.enabled = false;
        shoutObject.SetActive(false);
        shoutObject.transform.SetAsLastSibling();

        BattleEventDialogueView view = rootObject.GetComponent<BattleEventDialogueView>();
        if (view != null)
        {
            SerializedObject serializedView = new(view);
            SetSerializedReference(serializedView, "canvasGroup", canvasGroup);
            SetSerializedReference(serializedView, "playerShoutImage", shoutImage);
            SetSerializedBool(serializedView, "usePlayerShoutImage", true);
            serializedView.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(view);
        }

        EditorUtility.SetDirty(rootObject);
        EditorUtility.SetDirty(canvas.gameObject);
        return true;
    }

    private static void ConfigurePlayerShoutSpriteImporter()
    {
        TextureImporter importer = AssetImporter.GetAtPath(PlayerShoutSpritePath) as TextureImporter;
        if (importer == null)
        {
            AssetDatabase.ImportAsset(PlayerShoutSpritePath, ImportAssetOptions.ForceUpdate);
            importer = AssetImporter.GetAtPath(PlayerShoutSpritePath) as TextureImporter;
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

    private static void ApplyRootRect(RectTransform rect)
    {
        rect.anchorMin = Vector2.one;
        rect.anchorMax = Vector2.one;
        rect.pivot = Vector2.one;
        rect.anchoredPosition = RootAnchoredPosition;
        rect.sizeDelta = RootSize;
        rect.localScale = Vector3.one;
        rect.localRotation = Quaternion.identity;
    }

    private static void ApplyPlayerShoutRect(RectTransform rect, Sprite sprite)
    {
        float height = RootSize.y;
        float width = height * sprite.rect.width / Mathf.Max(1f, sprite.rect.height);

        rect.anchorMin = Vector2.one;
        rect.anchorMax = Vector2.one;
        rect.pivot = Vector2.one;
        rect.anchoredPosition = Vector2.zero;
        rect.sizeDelta = new Vector2(width, height);
        rect.localScale = Vector3.one;
        rect.localRotation = Quaternion.identity;
    }

    private static void SetChildActive(Transform root, string childName, bool active)
    {
        Transform child = root.Find(childName);
        if (child != null)
        {
            child.gameObject.SetActive(active);
        }
    }

    private static void SetSerializedReference(SerializedObject serializedObject, string propertyName, Object value)
    {
        SerializedProperty property = serializedObject.FindProperty(propertyName);
        if (property != null)
        {
            property.objectReferenceValue = value;
        }
    }

    private static void SetSerializedBool(SerializedObject serializedObject, string propertyName, bool value)
    {
        SerializedProperty property = serializedObject.FindProperty(propertyName);
        if (property != null)
        {
            property.boolValue = value;
        }
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
