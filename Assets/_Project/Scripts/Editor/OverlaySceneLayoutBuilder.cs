#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public static class OverlaySceneLayoutBuilder
{
    private const string GarageScenePath = "Assets/Scenes/GarageScene.unity";
    private const string CharacterScenePath = "Assets/Scenes/CharacterScene.unity";

    [MenuItem("Tools/Titan Destroyer/Rebuild Garage Authored Layout")]
    public static void RebuildGarageLayout()
    {
        Scene scene = EditorSceneManager.OpenScene(GarageScenePath, OpenSceneMode.Single);
        Transform root = EnsureRoot(scene, "GarageSceneRoot");

        GarageScenePresenter presenter = root.GetComponent<GarageScenePresenter>() ?? root.gameObject.AddComponent<GarageScenePresenter>();
        GarageSceneView view = root.GetComponent<GarageSceneView>() ?? root.gameObject.AddComponent<GarageSceneView>();
        Transform previewTuningAnchor = EnsurePreviewTuningAnchor(root);

        DestroyChildIfExists(root, "GarageCanvas");
        DestroyRootObjectIfExists(scene, "GarageCanvas");

        Font font = LoadRuntimeFont();
        Canvas canvas = CreateCanvas(scene, "GarageCanvas");
        GameObject rootPanel = CreateUiObject(canvas.transform, "GarageRoot");
        StretchFull(rootPanel.GetComponent<RectTransform>());

        Image dimmer = AddImage(CreateUiObject(rootPanel.transform, "Dimmer"), new Color(0.02f, 0.04f, 0.08f, 0.84f));
        StretchFull(dimmer.rectTransform);

        Image panel = AddImage(CreateUiObject(rootPanel.transform, "Panel"), new Color(0.07f, 0.11f, 0.16f, 0.98f));
        StretchFull(panel.rectTransform);

        Image accent = AddImage(CreateUiObject(panel.transform, "Accent"), new Color(0.16f, 0.55f, 0.9f, 1f));
        SetAnchoredLayout(
            accent.rectTransform,
            new Vector2(0f, 1f),
            new Vector2(1f, 1f),
            new Vector2(0.5f, 1f),
            new Vector2(0f, 10f),
            Vector2.zero);

        Button closeButton = CreateButton(rootPanel.transform, "CloseButton", font, "X", 24, new Color(0.69f, 0.18f, 0.18f, 1f));
        SetAnchoredLayout(
            closeButton.GetComponent<RectTransform>(),
            new Vector2(0f, 1f),
            new Vector2(0f, 1f),
            new Vector2(0f, 1f),
            new Vector2(72f, 72f),
            new Vector2(44f, -44f));

        Text title = CreateText(panel.transform, "Title", font, 42, FontStyle.Bold, TextAnchor.MiddleLeft, Color.white, "Garage");
        SetAnchoredLayout(
            title.rectTransform,
            new Vector2(0f, 1f),
            new Vector2(0f, 1f),
            new Vector2(0f, 1f),
            new Vector2(320f, 56f),
            new Vector2(132f, -52f));

        Text selectedText = CreateText(panel.transform, "SelectedHelicopter", font, 22, FontStyle.Bold, TextAnchor.MiddleRight, new Color(0.9f, 0.95f, 1f), "Selected: Helicopter1");
        SetAnchoredLayout(
            selectedText.rectTransform,
            new Vector2(1f, 1f),
            new Vector2(1f, 1f),
            new Vector2(1f, 1f),
            new Vector2(340f, 34f),
            new Vector2(-56f, -78f));

        Image previewFrame = AddImage(CreateUiObject(panel.transform, "PreviewFrame"), new Color(0.09f, 0.14f, 0.2f, 1f));
        SetAnchoredLayout(
            previewFrame.rectTransform,
            new Vector2(0.18f, 0.28f),
            new Vector2(0.82f, 0.88f),
            new Vector2(0.5f, 0.5f),
            Vector2.zero,
            new Vector2(0f, -12f));

        GameObject previewImageObject = CreateUiObject(previewFrame.transform, "PreviewImage");
        RawImage previewImage = previewImageObject.GetComponent<RawImage>() ?? previewImageObject.AddComponent<RawImage>();
        StretchFull(previewImage.rectTransform);
        previewImage.color = Color.white;

        Image listPanel = AddImage(CreateUiObject(panel.transform, "HelicopterScrollerPanel"), new Color(0.05f, 0.08f, 0.12f, 0.96f));
        SetAnchoredLayout(
            listPanel.rectTransform,
            new Vector2(0f, 0f),
            new Vector2(1f, 0f),
            new Vector2(0.5f, 0f),
            new Vector2(-48f, 360f),
            new Vector2(0f, 18f));

        ScrollRect scrollRect = listPanel.gameObject.GetComponent<ScrollRect>() ?? listPanel.gameObject.AddComponent<ScrollRect>();
        scrollRect.horizontal = true;
        scrollRect.vertical = false;
        scrollRect.inertia = true;
        scrollRect.movementType = ScrollRect.MovementType.Clamped;
        scrollRect.scrollSensitivity = 24f;

        GameObject viewportObject = CreateUiObject(listPanel.transform, "Viewport");
        RectTransform viewportRect = viewportObject.GetComponent<RectTransform>();
        viewportRect.anchorMin = new Vector2(0f, 0f);
        viewportRect.anchorMax = new Vector2(1f, 1f);
        viewportRect.pivot = new Vector2(0.5f, 0.5f);
        viewportRect.offsetMin = new Vector2(10f, 10f);
        viewportRect.offsetMax = new Vector2(-10f, -10f);

        Image viewportImage = AddImage(viewportObject, new Color(1f, 1f, 1f, 0.01f));
        viewportImage.raycastTarget = true;
        if (viewportObject.GetComponent<RectMask2D>() == null)
        {
            viewportObject.AddComponent<RectMask2D>();
        }

        GameObject contentObject = CreateUiObject(viewportObject.transform, "Content");
        RectTransform contentRect = contentObject.GetComponent<RectTransform>();
        contentRect.anchorMin = new Vector2(0f, 0f);
        contentRect.anchorMax = new Vector2(0f, 1f);
        contentRect.pivot = new Vector2(0f, 0.5f);
        contentRect.anchoredPosition = Vector2.zero;
        contentRect.sizeDelta = Vector2.zero;

        HorizontalLayoutGroup layoutGroup = contentObject.GetComponent<HorizontalLayoutGroup>() ?? contentObject.AddComponent<HorizontalLayoutGroup>();
        layoutGroup.spacing = 10f;
        layoutGroup.childAlignment = TextAnchor.MiddleLeft;
        layoutGroup.childControlWidth = false;
        layoutGroup.childControlHeight = false;
        layoutGroup.childForceExpandWidth = false;
        layoutGroup.childForceExpandHeight = false;
        layoutGroup.padding = new RectOffset(4, 4, 4, 4);

        ContentSizeFitter contentSizeFitter = contentObject.GetComponent<ContentSizeFitter>() ?? contentObject.AddComponent<ContentSizeFitter>();
        contentSizeFitter.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
        contentSizeFitter.verticalFit = ContentSizeFitter.FitMode.Unconstrained;

        scrollRect.viewport = viewportRect;
        scrollRect.content = contentRect;

        GarageHelicopterCardView cardTemplate = CreateGarageCardTemplate(contentObject.transform, font);
        cardTemplate.gameObject.SetActive(false);

        view.SetReferences(
            canvas,
            closeButton,
            title,
            selectedText,
            previewFrame.rectTransform,
            previewImage,
            scrollRect,
            contentRect,
            cardTemplate);

        SerializedObject presenterSerialized = new SerializedObject(presenter);
        presenterSerialized.FindProperty("view").objectReferenceValue = view;
        presenterSerialized.FindProperty("previewTuningAnchor").objectReferenceValue = previewTuningAnchor;
        presenterSerialized.ApplyModifiedPropertiesWithoutUndo();

        EditorUtility.SetDirty(root.gameObject);
        EditorUtility.SetDirty(view);
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
    }

    [MenuItem("Tools/Titan Destroyer/Rebuild Character Authored Layout")]
    public static void RebuildCharacterLayout()
    {
        Scene scene = EditorSceneManager.OpenScene(CharacterScenePath, OpenSceneMode.Single);
        Transform root = EnsureRoot(scene, "CharacterSceneRoot");

        CharacterScenePresenter presenter = root.GetComponent<CharacterScenePresenter>() ?? root.gameObject.AddComponent<CharacterScenePresenter>();
        CharacterSceneView view = root.GetComponent<CharacterSceneView>() ?? root.gameObject.AddComponent<CharacterSceneView>();

        DestroyChildIfExists(root, "CharacterCanvas");
        DestroyRootObjectIfExists(scene, "CharacterCanvas");

        Font font = LoadRuntimeFont();
        Canvas canvas = CreateCanvas(scene, "CharacterCanvas");
        GameObject rootPanel = CreateUiObject(canvas.transform, "CharacterRoot");
        StretchFull(rootPanel.GetComponent<RectTransform>());

        Image dimmer = AddImage(CreateUiObject(rootPanel.transform, "Dimmer"), new Color(0.02f, 0.04f, 0.08f, 0.78f));
        StretchFull(dimmer.rectTransform);

        Image panel = AddImage(CreateUiObject(rootPanel.transform, "Panel"), new Color(0.08f, 0.13f, 0.19f, 0.96f));
        StretchFull(panel.rectTransform);

        Image accent = AddImage(CreateUiObject(panel.transform, "PanelAccent"), new Color(0.7f, 0.41f, 0.16f, 1f));
        SetAnchoredLayout(
            accent.rectTransform,
            new Vector2(0f, 1f),
            new Vector2(1f, 1f),
            new Vector2(0.5f, 1f),
            new Vector2(0f, 10f),
            Vector2.zero);

        Button closeButton = CreateButton(rootPanel.transform, "CloseButton", font, "X", 24, new Color(0.68f, 0.18f, 0.18f, 1f));
        SetAnchoredLayout(
            closeButton.GetComponent<RectTransform>(),
            new Vector2(0f, 1f),
            new Vector2(0f, 1f),
            new Vector2(0f, 1f),
            new Vector2(72f, 72f),
            new Vector2(44f, -44f));

        Text title = CreateText(panel.transform, "Title", font, 40, FontStyle.Bold, TextAnchor.MiddleLeft, Color.white, "Character");
        SetAnchoredLayout(
            title.rectTransform,
            new Vector2(0f, 1f),
            new Vector2(0f, 1f),
            new Vector2(0f, 1f),
            new Vector2(320f, 56f),
            new Vector2(132f, -52f));

        Text description = CreateText(
            panel.transform,
            "Description",
            font,
            22,
            FontStyle.Normal,
            TextAnchor.UpperLeft,
            new Color(0.84f, 0.9f, 0.96f),
            "");
        SetAnchoredLayout(
            description.rectTransform,
            new Vector2(0f, 1f),
            new Vector2(1f, 1f),
            new Vector2(0f, 1f),
            new Vector2(-112f, 92f),
            new Vector2(56f, -140f));

        Image contentCard = AddImage(CreateUiObject(panel.transform, "ContentCard"), new Color(0.11f, 0.17f, 0.24f, 1f));
        SetAnchoredLayout(
            contentCard.rectTransform,
            new Vector2(0f, 0f),
            new Vector2(1f, 1f),
            new Vector2(0.5f, 0.5f),
            new Vector2(-112f, -280f),
            new Vector2(0f, -24f));

        Text contentLabel = CreateText(contentCard.transform, "ContentLabel", font, 28, FontStyle.Bold, TextAnchor.MiddleCenter, new Color(0.9f, 0.95f, 1f), "Character\nPlaceholder");
        StretchFull(contentLabel.rectTransform);

        Text footerHint = CreateText(panel.transform, "FooterHint", font, 18, FontStyle.Normal, TextAnchor.MiddleRight, new Color(0.73f, 0.8f, 0.88f), "Press Esc or click X to close");
        SetAnchoredLayout(
            footerHint.rectTransform,
            new Vector2(1f, 0f),
            new Vector2(1f, 0f),
            new Vector2(1f, 0f),
            new Vector2(320f, 26f),
            new Vector2(-56f, 40f));

        view.SetReferences(canvas, closeButton, title, description, contentLabel);

        SerializedObject presenterSerialized = new SerializedObject(presenter);
        presenterSerialized.FindProperty("view").objectReferenceValue = view;
        presenterSerialized.ApplyModifiedPropertiesWithoutUndo();

        EditorUtility.SetDirty(root.gameObject);
        EditorUtility.SetDirty(view);
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
    }

    [MenuItem("Tools/Titan Destroyer/Rebuild Garage And Character Authored Layouts")]
    public static void RebuildGarageAndCharacterLayouts()
    {
        RebuildGarageLayout();
        RebuildCharacterLayout();
    }

    private static Transform EnsureRoot(Scene scene, string rootName)
    {
        GameObject[] rootObjects = scene.GetRootGameObjects();
        for (int i = 0; i < rootObjects.Length; i++)
        {
            if (rootObjects[i].name == rootName)
            {
                return rootObjects[i].transform;
            }
        }

        GameObject rootObject = new GameObject(rootName);
        SceneManager.MoveGameObjectToScene(rootObject, scene);
        return rootObject.transform;
    }

    private static Transform EnsurePreviewTuningAnchor(Transform root)
    {
        Transform existing = root.Find("PreviewTuningAnchor");
        if (existing != null)
        {
            if (existing.gameObject.GetComponent<PreviewTuningAnchorMarker>() == null)
            {
                existing.gameObject.AddComponent<PreviewTuningAnchorMarker>();
            }

            return existing;
        }

        GameObject anchorObject = new GameObject("PreviewTuningAnchor");
        anchorObject.transform.SetParent(root, false);
        anchorObject.transform.localPosition = new Vector3(0f, -1.124f, 0f);
        anchorObject.transform.localRotation = Quaternion.identity;
        anchorObject.transform.localScale = Vector3.one * 1.54919517f;
        anchorObject.AddComponent<PreviewTuningAnchorMarker>();
        return anchorObject.transform;
    }

    private static void DestroyChildIfExists(Transform parent, string childName)
    {
        Transform existing = parent.Find(childName);
        if (existing != null)
        {
            Object.DestroyImmediate(existing.gameObject);
        }
    }

    private static void DestroyRootObjectIfExists(Scene scene, string objectName)
    {
        GameObject[] rootObjects = scene.GetRootGameObjects();
        for (int i = 0; i < rootObjects.Length; i++)
        {
            if (rootObjects[i].name == objectName)
            {
                Object.DestroyImmediate(rootObjects[i]);
                return;
            }
        }
    }

    private static Canvas CreateCanvas(Scene scene, string name)
    {
        GameObject canvasObject = new GameObject(name, typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        SceneManager.MoveGameObjectToScene(canvasObject, scene);
        RectTransform rectTransform = canvasObject.GetComponent<RectTransform>();
        rectTransform.localScale = Vector3.one;
        rectTransform.localRotation = Quaternion.identity;
        rectTransform.anchorMin = Vector2.zero;
        rectTransform.anchorMax = Vector2.one;
        rectTransform.pivot = new Vector2(0.5f, 0.5f);
        rectTransform.anchoredPosition = Vector2.zero;
        rectTransform.sizeDelta = Vector2.zero;
        rectTransform.offsetMin = Vector2.zero;
        rectTransform.offsetMax = Vector2.zero;

        Canvas canvas = canvasObject.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.overrideSorting = true;
        canvas.sortingOrder = 100;

        CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = 0.5f;
        return canvas;
    }

    private static GarageHelicopterCardView CreateGarageCardTemplate(Transform parent, Font font)
    {
        GameObject cardObject = CreateUiObject(parent, "HelicopterCardTemplate");
        RectTransform cardRect = cardObject.GetComponent<RectTransform>();
        cardRect.anchorMin = new Vector2(0f, 0.5f);
        cardRect.anchorMax = new Vector2(0f, 0.5f);
        cardRect.pivot = new Vector2(0f, 0.5f);
        cardRect.sizeDelta = new Vector2(374f, 300f);

        Image frame = AddImage(cardObject, new Color(0.15f, 0.2f, 0.28f, 1f));
        Button button = cardObject.GetComponent<Button>() ?? cardObject.AddComponent<Button>();
        button.targetGraphic = frame;

        LayoutElement layoutElement = cardObject.GetComponent<LayoutElement>() ?? cardObject.AddComponent<LayoutElement>();
        layoutElement.preferredWidth = 374f;
        layoutElement.preferredHeight = 300f;
        layoutElement.minWidth = 374f;
        layoutElement.minHeight = 300f;
        layoutElement.flexibleWidth = 0f;
        layoutElement.flexibleHeight = 0f;

        Image swatch = AddImage(CreateUiObject(cardObject.transform, "ColorSwatch"), new Color(0.12f, 0.16f, 0.24f, 1f));
        swatch.rectTransform.anchorMin = Vector2.zero;
        swatch.rectTransform.anchorMax = Vector2.one;
        swatch.rectTransform.pivot = new Vector2(0.5f, 0.5f);
        swatch.rectTransform.offsetMin = new Vector2(4f, 42f);
        swatch.rectTransform.offsetMax = new Vector2(-4f, -4f);

        RawImage thumbnail = swatch.gameObject.GetComponent<RawImage>();
        if (thumbnail != null)
        {
            Object.DestroyImmediate(thumbnail);
        }

        GameObject thumbnailObject = CreateUiObject(swatch.transform, "Thumbnail");
        RawImage thumbnailImage = thumbnailObject.GetComponent<RawImage>() ?? thumbnailObject.AddComponent<RawImage>();
        StretchFull(thumbnailImage.rectTransform);
        thumbnailImage.raycastTarget = false;

        Image labelBar = AddImage(CreateUiObject(cardObject.transform, "LabelBar"), new Color(0.08f, 0.11f, 0.16f, 0.96f));
        labelBar.rectTransform.anchorMin = new Vector2(0f, 0f);
        labelBar.rectTransform.anchorMax = new Vector2(1f, 0f);
        labelBar.rectTransform.pivot = new Vector2(0.5f, 0f);
        labelBar.rectTransform.sizeDelta = new Vector2(-8f, 30f);
        labelBar.rectTransform.anchoredPosition = new Vector2(0f, 4f);
        labelBar.raycastTarget = false;

        Text nameText = CreateText(cardObject.transform, "Name", font, 24, FontStyle.Bold, TextAnchor.MiddleCenter, Color.white, "Helicopter");
        nameText.rectTransform.anchorMin = new Vector2(0f, 0f);
        nameText.rectTransform.anchorMax = new Vector2(1f, 0f);
        nameText.rectTransform.pivot = new Vector2(0.5f, 0f);
        nameText.rectTransform.sizeDelta = new Vector2(-12f, 26f);
        nameText.rectTransform.anchoredPosition = new Vector2(0f, 6f);
        nameText.horizontalOverflow = HorizontalWrapMode.Overflow;
        nameText.verticalOverflow = VerticalWrapMode.Truncate;
        nameText.raycastTarget = false;

        GarageHelicopterCardView cardView = cardObject.GetComponent<GarageHelicopterCardView>() ?? cardObject.AddComponent<GarageHelicopterCardView>();
        cardView.SetReferences(button, frame, thumbnailImage, nameText);
        return cardView;
    }

    private static GameObject CreateUiObject(Transform parent, string name)
    {
        GameObject gameObject = new GameObject(name, typeof(RectTransform));
        gameObject.transform.SetParent(parent, false);
        return gameObject;
    }

    private static Image AddImage(GameObject gameObject, Color color)
    {
        Image image = gameObject.GetComponent<Image>() ?? gameObject.AddComponent<Image>();
        image.color = color;
        return image;
    }

    private static Button CreateButton(Transform parent, string name, Font font, string label, int fontSize, Color color)
    {
        GameObject buttonObject = CreateUiObject(parent, name);
        Image image = AddImage(buttonObject, color);
        Button button = buttonObject.GetComponent<Button>() ?? buttonObject.AddComponent<Button>();
        button.targetGraphic = image;

        Text labelText = CreateText(buttonObject.transform, $"{name}Label", font, fontSize, FontStyle.Bold, TextAnchor.MiddleCenter, Color.white, label);
        StretchFull(labelText.rectTransform);
        return button;
    }

    private static Text CreateText(Transform parent, string name, Font font, int fontSize, FontStyle fontStyle, TextAnchor alignment, Color color, string textValue)
    {
        GameObject textObject = CreateUiObject(parent, name);
        Text text = textObject.GetComponent<Text>() ?? textObject.AddComponent<Text>();
        text.font = font;
        text.fontSize = fontSize;
        text.fontStyle = fontStyle;
        text.alignment = alignment;
        text.color = color;
        text.horizontalOverflow = HorizontalWrapMode.Overflow;
        text.verticalOverflow = VerticalWrapMode.Overflow;
        text.text = textValue;
        return text;
    }

    private static void StretchFull(RectTransform rectTransform)
    {
        rectTransform.localScale = Vector3.one;
        rectTransform.localRotation = Quaternion.identity;
        rectTransform.anchorMin = Vector2.zero;
        rectTransform.anchorMax = Vector2.one;
        rectTransform.pivot = new Vector2(0.5f, 0.5f);
        rectTransform.anchoredPosition = Vector2.zero;
        rectTransform.sizeDelta = Vector2.zero;
        rectTransform.offsetMin = Vector2.zero;
        rectTransform.offsetMax = Vector2.zero;
    }

    private static void SetAnchoredLayout(
        RectTransform rectTransform,
        Vector2 anchorMin,
        Vector2 anchorMax,
        Vector2 pivot,
        Vector2 size,
        Vector2 anchoredPosition)
    {
        rectTransform.anchorMin = anchorMin;
        rectTransform.anchorMax = anchorMax;
        rectTransform.pivot = pivot;
        rectTransform.localScale = Vector3.one;
        rectTransform.localRotation = Quaternion.identity;
        rectTransform.sizeDelta = size;
        rectTransform.anchoredPosition = anchoredPosition;
    }

    private static Font LoadRuntimeFont()
    {
        return Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf")
            ?? Resources.GetBuiltinResource<Font>("Arial.ttf");
    }
}
#endif
