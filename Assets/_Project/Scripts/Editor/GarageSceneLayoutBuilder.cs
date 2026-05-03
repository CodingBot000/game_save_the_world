#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public static class GarageSceneLayoutBuilder
{
    private const string GarageScenePath = "Assets/Scenes/GarageScene.unity";
    private const string GarageBackgroundPath = "Assets/Art/UI/Garage/Backgrounds/garage_background.png";
    private const string MainMenuScenePath = "Assets/Scenes/MainMenu.unity/MainMenu.unity";
    private const string RequestMarkerPath = "Temp/GarageSceneBuildRequested";

    [InitializeOnLoadMethod]
    private static void RebuildIfRequested()
    {
        EditorApplication.delayCall += () =>
        {
            string requestMarkerPath = GetRequestMarkerPath();
            if (!File.Exists(requestMarkerPath))
            {
                return;
            }

            try
            {
                Debug.Log("[GarageSceneLayoutBuilder] Rebuilding GarageScene from request marker.");
                RebuildGarageScene();
            }
            finally
            {
                File.Delete(requestMarkerPath);
            }
        };
    }

    [MenuItem("Tools/Titan Destroyer/Rebuild Garage Loadout Scene")]
    public static void RebuildGarageScene()
    {
        Scene previousActiveScene = SceneManager.GetActiveScene();
        Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
        EditorSceneManager.SetActiveScene(scene);

        Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

        GameObject root = new GameObject("GarageSceneRoot");
        GarageLoadoutScenePresenter presenter = root.AddComponent<GarageLoadoutScenePresenter>();
        GarageLoadoutSceneView view = root.AddComponent<GarageLoadoutSceneView>();

        PreviewStage previewStage = CreatePreviewStage();
        GameObject eventSystemObject = new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));

        Canvas canvas = CreateCanvas();
        Image backgroundImage = CreateImage(canvas.transform, "HangarBackground", new Color(0.08f, 0.09f, 0.1f, 1f));
        StretchFull(backgroundImage.rectTransform);
        Sprite backgroundSprite = AssetDatabase.LoadAssetAtPath<Sprite>(GarageBackgroundPath);
        if (backgroundSprite != null)
        {
            backgroundImage.sprite = backgroundSprite;
            backgroundImage.color = Color.white;
            backgroundImage.preserveAspect = true;
        }
        else
        {
            CreateHangarPlaceholder(backgroundImage.transform);
        }

        Image shade = CreateImage(canvas.transform, "HangarShade", new Color(0.01f, 0.015f, 0.018f, 0.32f));
        StretchFull(shade.rectTransform);

        Button closeButton = CreateButton(canvas.transform, "CloseButton", font, "X", 24, new Color(0.58f, 0.14f, 0.12f, 0.95f));
        SetAnchor(closeButton.GetComponent<RectTransform>(), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(72f, 72f), new Vector2(36f, -36f));

        RectTransform leftPanel = CreatePanel(canvas.transform, "HelicopterPanel", new Color(0.03f, 0.04f, 0.05f, 0.62f));
        Stretch(leftPanel, new Vector2(0f, 0f), new Vector2(0.4f, 1f), new Vector2(42f, 42f), new Vector2(-22f, -42f));

        Text titleText = CreateText(leftPanel, "Title", font, 42, FontStyle.Bold, TextAnchor.MiddleLeft, Color.white, "Garage");
        SetAnchor(titleText.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0f, 1f), new Vector2(-64f, 62f), new Vector2(32f, -34f));

        Text selectedVehicleText = CreateText(leftPanel, "SelectedVehicle", font, 22, FontStyle.Bold, TextAnchor.MiddleLeft, new Color(0.85f, 0.91f, 0.96f, 1f), "Viper");
        SetAnchor(selectedVehicleText.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0f, 1f), new Vector2(-64f, 38f), new Vector2(32f, -92f));

        RawImage helicopterPreviewImage = CreateRawImage(leftPanel, "HelicopterPreview", new Color(1f, 1f, 1f, 1f));
        Stretch(helicopterPreviewImage.rectTransform, new Vector2(0f, 0f), new Vector2(1f, 1f), new Vector2(28f, 40f), new Vector2(-28f, -146f));

        Text previewHint = CreateText(leftPanel, "PreviewCaption", font, 18, FontStyle.Normal, TextAnchor.MiddleCenter, new Color(0.68f, 0.75f, 0.78f, 1f), "Current helicopter loadout preview");
        SetAnchor(previewHint.rectTransform, new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0.5f, 0f), new Vector2(-64f, 36f), new Vector2(0f, 70f));

        RectTransform rightPanel = CreatePanel(canvas.transform, "ItemPanel", new Color(0.035f, 0.045f, 0.052f, 0.78f));
        Stretch(rightPanel, new Vector2(0.4f, 0f), new Vector2(1f, 1f), new Vector2(22f, 42f), new Vector2(-42f, -42f));

        Text itemTitle = CreateText(rightPanel, "ItemPanelTitle", font, 32, FontStyle.Bold, TextAnchor.MiddleLeft, Color.white, "Loadout");
        SetAnchor(itemTitle.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0f, 1f), new Vector2(-72f, 52f), new Vector2(36f, -34f));

        List<GarageLoadoutItemView> itemViews = new List<GarageLoadoutItemView>();
        CreateItemRow(rightPanel, font, "Primary Weapon", 0, GarageLoadoutSlotType.PrimaryWeapon, itemViews, new[]
        {
            ItemData.Create("gatling_mk1", "Gatling Mk I", new Color(0.68f, 0.72f, 0.75f, 1f)),
            ItemData.Create("heavy_gatling", "Heavy Gatling", new Color(0.86f, 0.48f, 0.24f, 1f)),
            ItemData.Create("burst_gatling", "Burst Gatling", new Color(0.38f, 0.72f, 0.92f, 1f))
        });
        CreateItemRow(rightPanel, font, "Secondary Weapon 1", 1, GarageLoadoutSlotType.SecondaryWeapon1, itemViews, new[]
        {
            ItemData.Create("rocket_pod_a", "Rocket Pod A", new Color(0.42f, 0.78f, 0.43f, 1f)),
            ItemData.Create("micro_missile", "Micro Missile", new Color(0.9f, 0.78f, 0.34f, 1f)),
            ItemData.Create("cluster_rack", "Cluster Rack", new Color(0.68f, 0.48f, 0.92f, 1f))
        });
        CreateItemRow(rightPanel, font, "Secondary Weapon 2", 2, GarageLoadoutSlotType.SecondaryWeapon2, itemViews, new[]
        {
            ItemData.Create("missile_pylon_a", "Missile Pylon A", new Color(0.88f, 0.38f, 0.38f, 1f)),
            ItemData.Create("rail_dart", "Rail Dart", new Color(0.46f, 0.86f, 0.8f, 1f)),
            ItemData.Create("bomb_rail", "Bomb Rail", new Color(0.74f, 0.74f, 0.5f, 1f))
        });
        CreateItemRow(rightPanel, font, "Armor", 3, GarageLoadoutSlotType.Armor, itemViews, new[]
        {
            ItemData.Create("light_armor", "Light Armor", new Color(0.82f, 0.88f, 0.9f, 1f)),
            ItemData.Create("reactive_armor", "Reactive Armor", new Color(0.95f, 0.55f, 0.28f, 1f)),
            ItemData.Create("ceramic_armor", "Ceramic Armor", new Color(0.48f, 0.66f, 0.92f, 1f))
        });

        view.SetReferences(
            canvas,
            backgroundImage,
            closeButton,
            titleText,
            selectedVehicleText,
            helicopterPreviewImage,
            previewStage.Camera,
            previewStage.Anchor,
            previewStage.KeyLight,
            itemViews.ToArray());

        SerializedObject presenterSerialized = new SerializedObject(presenter);
        presenterSerialized.FindProperty("view").objectReferenceValue = view;
        presenterSerialized.ApplyModifiedPropertiesWithoutUndo();

        EditorUtility.SetDirty(root);
        EditorUtility.SetDirty(view);
        EditorUtility.SetDirty(presenter);
        EditorUtility.SetDirty(eventSystemObject);

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene, GarageScenePath);
        UpdateBuildSettings();
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        if (previousActiveScene.IsValid() && previousActiveScene.isLoaded)
        {
            EditorSceneManager.SetActiveScene(previousActiveScene);
        }

        EditorSceneManager.CloseScene(scene, true);
    }

    private static Canvas CreateCanvas()
    {
        GameObject canvasObject = new GameObject("GarageCanvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        Canvas canvas = canvasObject.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.overrideSorting = true;
        canvas.sortingOrder = 120;

        CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = 0.5f;

        return canvas;
    }

    private static PreviewStage CreatePreviewStage()
    {
        GameObject stageRoot = new GameObject("GaragePreviewStage");
        stageRoot.transform.position = new Vector3(0f, -100f, 0f);

        GameObject anchorObject = new GameObject("HelicopterPreviewAnchor");
        anchorObject.transform.SetParent(stageRoot.transform, false);
        anchorObject.transform.localPosition = Vector3.zero;
        anchorObject.transform.localRotation = Quaternion.Euler(0f, 210f, 0f);

        GameObject cameraObject = new GameObject("GaragePreviewCamera", typeof(Camera));
        cameraObject.transform.SetParent(stageRoot.transform, false);
        cameraObject.transform.localPosition = new Vector3(0f, 1.1f, -7.2f);
        cameraObject.transform.localRotation = Quaternion.Euler(8f, 0f, 0f);

        Camera camera = cameraObject.GetComponent<Camera>();
        camera.clearFlags = CameraClearFlags.SolidColor;
        camera.backgroundColor = new Color(0.015f, 0.018f, 0.02f, 1f);
        camera.orthographic = true;
        camera.orthographicSize = 2.25f;
        camera.nearClipPlane = 0.1f;
        camera.farClipPlane = 30f;

        GameObject lightObject = new GameObject("GaragePreviewKeyLight", typeof(Light));
        lightObject.transform.SetParent(stageRoot.transform, false);
        lightObject.transform.localPosition = new Vector3(-2.5f, 4.5f, -3f);
        lightObject.transform.localRotation = Quaternion.Euler(52f, -28f, 0f);

        Light keyLight = lightObject.GetComponent<Light>();
        keyLight.type = LightType.Directional;
        keyLight.intensity = 2.4f;
        keyLight.color = new Color(1f, 0.95f, 0.86f, 1f);

        return new PreviewStage(camera, anchorObject.transform, keyLight);
    }

    private static void CreateHangarPlaceholder(Transform parent)
    {
        Image floor = CreateImage(parent, "PlaceholderFloor", new Color(0.16f, 0.17f, 0.16f, 1f));
        Stretch(floor.rectTransform, new Vector2(0f, 0f), new Vector2(1f, 0.32f), Vector2.zero, Vector2.zero);

        Image backWall = CreateImage(parent, "PlaceholderBackWall", new Color(0.11f, 0.12f, 0.13f, 1f));
        Stretch(backWall.rectTransform, new Vector2(0f, 0.32f), new Vector2(1f, 1f), Vector2.zero, Vector2.zero);

        for (int i = 0; i < 6; i++)
        {
            Image beam = CreateImage(parent, $"HangarBeam{i + 1}", new Color(0.21f, 0.22f, 0.23f, 0.62f));
            float x = 0.08f + i * 0.17f;
            Stretch(beam.rectTransform, new Vector2(x, 0.32f), new Vector2(x, 1f), new Vector2(-3f, 0f), new Vector2(3f, 0f));
        }
    }

    private static void CreateItemRow(RectTransform parent, Font font, string title, int rowIndex, GarageLoadoutSlotType slotType, List<GarageLoadoutItemView> itemViews, ItemData[] items)
    {
        RectTransform row = CreatePanel(parent, $"{title}Row", new Color(0.05f, 0.065f, 0.074f, 0.92f));
        float top = -104f - rowIndex * 220f;
        Stretch(row, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(36f, top - 198f), new Vector2(-36f, top));

        Text label = CreateText(row, "Label", font, 23, FontStyle.Bold, TextAnchor.MiddleLeft, new Color(0.9f, 0.94f, 0.96f, 1f), title);
        SetAnchor(label.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0f, 1f), new Vector2(-40f, 36f), new Vector2(24f, -16f));

        RectTransform slots = CreateUiObject(row, "Slots").GetComponent<RectTransform>();
        Stretch(slots, new Vector2(0f, 0f), new Vector2(1f, 1f), new Vector2(22f, 20f), new Vector2(-22f, -58f));

        HorizontalLayoutGroup layout = slots.gameObject.AddComponent<HorizontalLayoutGroup>();
        layout.spacing = 18f;
        layout.childAlignment = TextAnchor.MiddleCenter;
        layout.childControlWidth = true;
        layout.childControlHeight = true;
        layout.childForceExpandWidth = true;
        layout.childForceExpandHeight = true;

        for (int i = 0; i < items.Length; i++)
        {
            itemViews.Add(CreateItemCard(slots, font, slotType, items[i]));
        }
    }

    private static GarageLoadoutItemView CreateItemCard(RectTransform parent, Font font, GarageLoadoutSlotType slotType, ItemData data)
    {
        GameObject card = CreateUiObject(parent, data.ItemId);
        Image frame = card.AddComponent<Image>();
        frame.color = new Color(0.18f, 0.24f, 0.28f, 1f);
        Button button = card.AddComponent<Button>();
        button.targetGraphic = frame;

        LayoutElement layoutElement = card.AddComponent<LayoutElement>();
        layoutElement.preferredHeight = 120f;
        layoutElement.flexibleWidth = 1f;

        Image inner = CreateImage(card.transform, "Inner", new Color(0.07f, 0.085f, 0.095f, 0.98f));
        Stretch(inner.rectTransform, new Vector2(0f, 0f), new Vector2(1f, 1f), new Vector2(4f, 4f), new Vector2(-4f, -4f));

        Image body = CreateImage(card.transform, "PlaceholderBody", data.Color);
        SetAnchor(body.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(164f, 26f), new Vector2(0f, 8f));

        Image muzzle = CreateImage(card.transform, "PlaceholderMuzzle", new Color(data.Color.r * 0.65f, data.Color.g * 0.65f, data.Color.b * 0.65f, 1f));
        SetAnchor(muzzle.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(24f, 18f), new Vector2(92f, 8f));

        Text nameText = CreateText(card.transform, "Name", font, 18, FontStyle.Bold, TextAnchor.MiddleCenter, Color.white, data.DisplayName);
        SetAnchor(nameText.rectTransform, new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0.5f, 0f), new Vector2(-16f, 28f), new Vector2(0f, 34f));

        Text equippedText = CreateText(card.transform, "Equipped", font, 14, FontStyle.Bold, TextAnchor.MiddleCenter, new Color(1f, 0.82f, 0.36f, 1f), string.Empty);
        SetAnchor(equippedText.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f), new Vector2(-16f, 24f), new Vector2(0f, -20f));

        GarageLoadoutItemView itemView = card.AddComponent<GarageLoadoutItemView>();
        itemView.SetData(slotType, data.ItemId, data.DisplayName, data.Color);
        itemView.SetReferences(button, frame, body, nameText, equippedText);
        itemView.SetSelected(false);

        return itemView;
    }

    private static RectTransform CreatePanel(Transform parent, string name, Color color)
    {
        Image image = CreateImage(parent, name, color);
        return image.rectTransform;
    }

    private static Image CreateImage(Transform parent, string name, Color color)
    {
        GameObject gameObject = CreateUiObject(parent, name);
        Image image = gameObject.AddComponent<Image>();
        image.color = color;
        image.raycastTarget = false;
        return image;
    }

    private static RawImage CreateRawImage(Transform parent, string name, Color color)
    {
        GameObject gameObject = CreateUiObject(parent, name);
        RawImage image = gameObject.AddComponent<RawImage>();
        image.color = color;
        image.raycastTarget = false;
        return image;
    }

    private static Button CreateButton(Transform parent, string name, Font font, string label, int fontSize, Color color)
    {
        Image image = CreateImage(parent, name, color);
        image.raycastTarget = true;
        Button button = image.gameObject.AddComponent<Button>();
        button.targetGraphic = image;

        Text text = CreateText(image.transform, "Label", font, fontSize, FontStyle.Bold, TextAnchor.MiddleCenter, Color.white, label);
        StretchFull(text.rectTransform);
        return button;
    }

    private static Text CreateText(Transform parent, string name, Font font, int fontSize, FontStyle style, TextAnchor alignment, Color color, string value)
    {
        GameObject gameObject = CreateUiObject(parent, name);
        Text text = gameObject.AddComponent<Text>();
        text.font = font;
        text.fontSize = fontSize;
        text.fontStyle = style;
        text.alignment = alignment;
        text.color = color;
        text.text = value;
        text.horizontalOverflow = HorizontalWrapMode.Wrap;
        text.verticalOverflow = VerticalWrapMode.Truncate;
        text.raycastTarget = false;
        return text;
    }

    private static GameObject CreateUiObject(Transform parent, string name)
    {
        GameObject gameObject = new GameObject(name, typeof(RectTransform));
        gameObject.transform.SetParent(parent, false);
        return gameObject;
    }

    private static void StretchFull(RectTransform rectTransform)
    {
        Stretch(rectTransform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
    }

    private static void Stretch(RectTransform rectTransform, Vector2 anchorMin, Vector2 anchorMax, Vector2 offsetMin, Vector2 offsetMax)
    {
        rectTransform.anchorMin = anchorMin;
        rectTransform.anchorMax = anchorMax;
        rectTransform.pivot = new Vector2(0.5f, 0.5f);
        rectTransform.offsetMin = offsetMin;
        rectTransform.offsetMax = offsetMax;
    }

    private static void SetAnchor(RectTransform rectTransform, Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot, Vector2 size, Vector2 anchoredPosition)
    {
        rectTransform.anchorMin = anchorMin;
        rectTransform.anchorMax = anchorMax;
        rectTransform.pivot = pivot;
        rectTransform.sizeDelta = size;
        rectTransform.anchoredPosition = anchoredPosition;
    }

    private static void UpdateBuildSettings()
    {
        List<EditorBuildSettingsScene> scenes = EditorBuildSettings.scenes
            .Where(scene => scene.path != GarageScenePath)
            .ToList();

        int mainMenuIndex = scenes.FindIndex(scene => scene.path == MainMenuScenePath);
        int insertIndex = mainMenuIndex >= 0 ? mainMenuIndex + 1 : scenes.Count;
        scenes.Insert(insertIndex, new EditorBuildSettingsScene(GarageScenePath, true));
        EditorBuildSettings.scenes = scenes.ToArray();
    }

    private static string GetRequestMarkerPath()
    {
        return Path.GetFullPath(Path.Combine(Application.dataPath, "..", RequestMarkerPath));
    }

    private readonly struct PreviewStage
    {
        public readonly Camera Camera;
        public readonly Transform Anchor;
        public readonly Light KeyLight;

        public PreviewStage(Camera camera, Transform anchor, Light keyLight)
        {
            Camera = camera;
            Anchor = anchor;
            KeyLight = keyLight;
        }
    }

    private readonly struct ItemData
    {
        public readonly string ItemId;
        public readonly string DisplayName;
        public readonly Color Color;

        private ItemData(string itemId, string displayName, Color color)
        {
            ItemId = itemId;
            DisplayName = displayName;
            Color = color;
        }

        public static ItemData Create(string itemId, string displayName, Color color)
        {
            return new ItemData(itemId, displayName, color);
        }
    }
}
#endif
