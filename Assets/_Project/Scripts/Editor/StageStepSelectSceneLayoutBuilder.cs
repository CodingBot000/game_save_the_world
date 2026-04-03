#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public static class StageStepSelectSceneLayoutBuilder
{
    private const string ScenePath = "Assets/Scenes/StageSelectScene.unity/StageStepSelectScene.unity";
    private const string StageSelectScenePath = "Assets/Scenes/StageSelectScene.unity/StageSelectScene.unity";
    private static readonly string[] StageNames = { "Tokyo", "Seoul", "Paris", "Hollywood", "Beijing" };
    private static readonly string[] DifficultyLabels = { "1", "2", "3", "4" };

    [MenuItem("Tools/Titan Destroyer/Rebuild Stage Step Select Layout")]
    public static void RebuildStageStepSelectLayout()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(ScenePath) ?? "Assets/Scenes");

        Scene scene = File.Exists(ScenePath)
            ? EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single)
            : EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

        DestroyRootObjectIfExists(scene, "StageStepSelectSceneRoot");

        GameObject rootObject = new GameObject("StageStepSelectSceneRoot");
        SceneManager.MoveGameObjectToScene(rootObject, scene);
        Transform root = rootObject.transform;

        StageStepSelectScenePresenter presenter =
            rootObject.GetComponent<StageStepSelectScenePresenter>() ??
            rootObject.AddComponent<StageStepSelectScenePresenter>();

        CreateCamera(root);
        CreateEventSystem(root);

        Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        Canvas canvas = CreateCanvas(root);
        GameObject rootPanel = CreateUiObject(canvas.transform, "StageStepSelectRootPanel");
        StretchFull(rootPanel.GetComponent<RectTransform>());

        Image backdrop = AddImage(CreateUiObject(rootPanel.transform, "Backdrop"), new Color(0.94f, 0.94f, 0.92f, 1f));
        StretchFull(backdrop.rectTransform);

        Image frameBorder = AddImage(CreateUiObject(rootPanel.transform, "FrameBorder"), new Color(0.61f, 0.61f, 0.61f, 1f));
        SetAnchoredLayout(
            frameBorder.rectTransform,
            Vector2.zero,
            Vector2.one,
            new Vector2(0.5f, 0.5f),
            new Vector2(-96f, -72f),
            Vector2.zero);

        Image frame = AddImage(CreateUiObject(frameBorder.transform, "Frame"), Color.white);
        StretchToInset(frame.rectTransform, 2f);

        Button closeButton = CreateButton(frame.transform, "CloseButton", font, "X", 32, Color.white, new Color(0f, 0f, 0f, 0f));
        SetAnchoredLayout(
            closeButton.GetComponent<RectTransform>(),
            new Vector2(0f, 1f),
            new Vector2(0f, 1f),
            new Vector2(0f, 1f),
            new Vector2(92f, 92f),
            new Vector2(42f, -42f));
        ApplyOutline(closeButton.gameObject, new Color(0.5f, 0.5f, 0.5f, 1f));
        closeButton.GetComponentInChildren<Text>(true).color = new Color(0.09f, 0.09f, 0.09f, 1f);

        GameObject difficultyPanel = CreateUiObject(frame.transform, "DifficultyPanel");
        RectTransform difficultyPanelRect = difficultyPanel.GetComponent<RectTransform>();
        SetAnchoredLayout(
            difficultyPanelRect,
            new Vector2(1f, 1f),
            new Vector2(1f, 1f),
            new Vector2(1f, 1f),
            new Vector2(340f, 92f),
            new Vector2(-42f, -42f));

        List<Button> difficultyButtons = new List<Button>();
        for (int i = 0; i < DifficultyLabels.Length; i++)
        {
            Button difficultyButton = CreateButton(
                difficultyPanel.transform,
                $"DifficultyButton{i + 1}",
                font,
                DifficultyLabels[i],
                28,
                Color.white,
                new Color(0f, 0f, 0f, 0f));

            SetAnchoredLayout(
                difficultyButton.GetComponent<RectTransform>(),
                new Vector2(0f, 0.5f),
                new Vector2(0f, 0.5f),
                new Vector2(0f, 0.5f),
                new Vector2(82f, 82f),
                new Vector2(84f * i, 0f));

            ApplyOutline(difficultyButton.gameObject, new Color(0.5f, 0.5f, 0.5f, 1f));
            difficultyButton.GetComponentInChildren<Text>(true).color = new Color(0.15f, 0.15f, 0.15f, 1f);
            difficultyButtons.Add(difficultyButton);
        }

        RectTransform viewport = CreateViewport(frame.transform);
        RectTransform pageContainer = CreateUiObject(viewport.transform, "PageContainer").GetComponent<RectTransform>();
        pageContainer.anchorMin = new Vector2(0f, 0f);
        pageContainer.anchorMax = new Vector2(0f, 1f);
        pageContainer.pivot = new Vector2(0f, 0.5f);
        const float authoredPageWidth = 1452f;
        pageContainer.sizeDelta = new Vector2(authoredPageWidth * StageNames.Length, 0f);
        pageContainer.anchoredPosition = Vector2.zero;

        for (int i = 0; i < StageNames.Length; i++)
        {
            CreateStagePage(pageContainer, font, i, StageNames[i], authoredPageWidth);
        }

        Button previousButton = CreateArrowButton(frame.transform, "PreviousButton", font, "<", true);
        Button nextButton = CreateArrowButton(frame.transform, "NextButton", font, ">", false);
        previousButton.gameObject.SetActive(false);
        nextButton.gameObject.SetActive(StageNames.Length > 1);

        presenter.SetReferences(
            canvas,
            viewport,
            pageContainer,
            closeButton,
            previousButton,
            nextButton,
            difficultyButtons.ToArray());

        EditorUtility.SetDirty(rootObject);
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene, ScenePath);
        AssetDatabase.ImportAsset(ScenePath, ImportAssetOptions.ForceUpdate);
        EnsureBuildSettingsScene();
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Selection.activeGameObject = rootObject;
    }

    private static RectTransform CreateViewport(Transform parent)
    {
        GameObject viewportObject = CreateUiObject(parent, "Viewport");
        RectTransform viewportRect = viewportObject.GetComponent<RectTransform>();
        viewportRect.anchorMin = new Vector2(0f, 0f);
        viewportRect.anchorMax = new Vector2(1f, 1f);
        viewportRect.pivot = new Vector2(0.5f, 0.5f);
        viewportRect.offsetMin = new Vector2(140f, 42f);
        viewportRect.offsetMax = new Vector2(-140f, -42f);

        Image viewportImage = AddImage(viewportObject, new Color(1f, 1f, 1f, 0.001f));
        viewportImage.raycastTarget = false;
        viewportObject.AddComponent<RectMask2D>();
        return viewportRect;
    }

    private static void CreateStagePage(Transform parent, Font font, int stageIndex, string stageName, float authoredPageWidth)
    {
        GameObject pageObject = CreateUiObject(parent, $"StagePage_{stageIndex + 1:00}_{stageName.Replace(" ", string.Empty)}");
        RectTransform pageRect = pageObject.GetComponent<RectTransform>();
        pageRect.anchorMin = new Vector2(0f, 0f);
        pageRect.anchorMax = new Vector2(0f, 1f);
        pageRect.pivot = new Vector2(0f, 0.5f);
        pageRect.sizeDelta = new Vector2(authoredPageWidth, 0f);
        pageRect.anchoredPosition = new Vector2(authoredPageWidth * stageIndex, 0f);

        Text stageLabel = CreateText(pageObject.transform, "StageLabel", font, 54, FontStyle.Normal, TextAnchor.MiddleCenter, new Color(0.16f, 0.16f, 0.16f, 1f), $"Stage {stageIndex + 1}");
        SetAnchoredLayout(
            stageLabel.rectTransform,
            new Vector2(0.5f, 1f),
            new Vector2(0.5f, 1f),
            new Vector2(0.5f, 1f),
            new Vector2(640f, 80f),
            new Vector2(0f, -56f));

        Text stageNameLabel = CreateText(pageObject.transform, "StageNameLabel", font, 40, FontStyle.Normal, TextAnchor.MiddleCenter, new Color(0.16f, 0.16f, 0.16f, 1f), stageName);
        SetAnchoredLayout(
            stageNameLabel.rectTransform,
            new Vector2(0.5f, 1f),
            new Vector2(0.5f, 1f),
            new Vector2(0.5f, 1f),
            new Vector2(640f, 64f),
            new Vector2(0f, -118f));

        Button previewButton = CreateButton(pageObject.transform, "StagePreviewButton", font, string.Empty, 20, Color.white, new Color(0f, 0f, 0f, 0f));
        RectTransform previewRect = previewButton.GetComponent<RectTransform>();
        SetAnchoredLayout(
            previewRect,
            new Vector2(0.5f, 0.5f),
            new Vector2(0.5f, 0.5f),
            new Vector2(0.5f, 0.5f),
            new Vector2(760f, 520f),
            new Vector2(0f, -24f));
        ApplyOutline(previewButton.gameObject, new Color(0.5f, 0.5f, 0.5f, 1f));
        CreatePreviewImage(previewButton.transform);

        GameObject starsRow = CreateUiObject(pageObject.transform, "StarsRow");
        HorizontalLayoutGroup starsLayout = starsRow.AddComponent<HorizontalLayoutGroup>();
        starsLayout.childAlignment = TextAnchor.MiddleCenter;
        starsLayout.childControlWidth = false;
        starsLayout.childControlHeight = false;
        starsLayout.childForceExpandWidth = false;
        starsLayout.childForceExpandHeight = false;
        starsLayout.spacing = 26f;

        RectTransform starsRect = starsRow.GetComponent<RectTransform>();
        SetAnchoredLayout(
            starsRect,
            new Vector2(0.5f, 0.5f),
            new Vector2(0.5f, 0.5f),
            new Vector2(0.5f, 0.5f),
            new Vector2(320f, 96f),
            new Vector2(0f, -404f));

        for (int i = 0; i < 3; i++)
        {
            Text star = CreateText(starsRow.transform, $"Star{i + 1}", font, 64, FontStyle.Normal, TextAnchor.MiddleCenter, new Color(0.35f, 0.35f, 0.35f, 1f), "O");
            RectTransform starRect = star.rectTransform;
            starRect.sizeDelta = new Vector2(76f, 76f);
        }
    }

    private static Button CreateArrowButton(Transform parent, string name, Font font, string label, bool leftSide)
    {
        Button button = CreateButton(parent, name, font, label, 72, new Color(1f, 1f, 1f, 0.001f), new Color(0f, 0f, 0f, 0f));
        RectTransform rectTransform = button.GetComponent<RectTransform>();
        SetAnchoredLayout(
            rectTransform,
            new Vector2(leftSide ? 0f : 1f, 0.5f),
            new Vector2(leftSide ? 0f : 1f, 0.5f),
            new Vector2(0.5f, 0.5f),
            new Vector2(88f, 120f),
            new Vector2(leftSide ? 68f : -68f, 0f));

        Text labelText = button.GetComponentInChildren<Text>(true);
        if (labelText != null)
        {
            labelText.color = new Color(0.37f, 0.37f, 0.37f, 1f);
        }

        return button;
    }

    private static void CreatePreviewImage(Transform parent)
    {
        GameObject previewObject = CreateUiObject(parent, "PreviewImage");
        RawImage previewImage = previewObject.GetComponent<RawImage>() ?? previewObject.AddComponent<RawImage>();
        AspectRatioFitter aspectFitter = previewObject.GetComponent<AspectRatioFitter>() ?? previewObject.AddComponent<AspectRatioFitter>();
        RectTransform previewImageRect = previewObject.GetComponent<RectTransform>();
        previewImageRect.anchorMin = Vector2.zero;
        previewImageRect.anchorMax = Vector2.one;
        previewImageRect.offsetMin = new Vector2(18f, 18f);
        previewImageRect.offsetMax = new Vector2(-18f, -18f);
        previewImage.color = new Color(1f, 1f, 1f, 0f);
        previewImage.raycastTarget = false;
        aspectFitter.aspectMode = AspectRatioFitter.AspectMode.FitInParent;
        aspectFitter.aspectRatio = 1f;
    }

    private static void CreateCamera(Transform parent)
    {
        GameObject cameraObject = new GameObject("Main Camera", typeof(Camera), typeof(AudioListener));
        cameraObject.transform.SetParent(parent, false);
        cameraObject.tag = "MainCamera";

        Camera camera = cameraObject.GetComponent<Camera>();
        camera.clearFlags = CameraClearFlags.SolidColor;
        camera.backgroundColor = new Color(0.94f, 0.94f, 0.92f, 1f);
        camera.orthographic = true;
        camera.orthographicSize = 5f;
        camera.nearClipPlane = 0.1f;
        camera.farClipPlane = 20f;
    }

    private static void CreateEventSystem(Transform parent)
    {
        GameObject eventSystemObject = new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
        eventSystemObject.transform.SetParent(parent, false);
    }

    private static Canvas CreateCanvas(Transform parent)
    {
        GameObject canvasObject = new GameObject(
            "StageStepSelectCanvas",
            typeof(RectTransform),
            typeof(Canvas),
            typeof(CanvasScaler),
            typeof(GraphicRaycaster));

        canvasObject.transform.SetParent(parent, false);
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

    private static GameObject CreateUiObject(Transform parent, string name)
    {
        GameObject gameObject = new GameObject(name, typeof(RectTransform));
        gameObject.transform.SetParent(parent, false);
        return gameObject;
    }

    private static Image AddImage(GameObject target, Color color)
    {
        Image image = target.GetComponent<Image>() ?? target.AddComponent<Image>();
        image.color = color;
        return image;
    }

    private static Text CreateText(
        Transform parent,
        string name,
        Font font,
        int fontSize,
        FontStyle fontStyle,
        TextAnchor alignment,
        Color color,
        string value)
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
        text.raycastTarget = false;
        text.text = value;
        return text;
    }

    private static Button CreateButton(
        Transform parent,
        string name,
        Font font,
        string label,
        int fontSize,
        Color backgroundColor,
        Color pressedTint)
    {
        GameObject buttonObject = CreateUiObject(parent, name);
        Image image = AddImage(buttonObject, backgroundColor);

        Button button = buttonObject.GetComponent<Button>() ?? buttonObject.AddComponent<Button>();
        button.targetGraphic = image;

        ColorBlock colors = button.colors;
        colors.normalColor = backgroundColor;
        colors.highlightedColor = backgroundColor * 0.97f;
        colors.pressedColor = pressedTint.a <= 0f ? backgroundColor * 0.92f : pressedTint;
        colors.selectedColor = backgroundColor;
        colors.disabledColor = new Color(backgroundColor.r, backgroundColor.g, backgroundColor.b, 0.4f);
        button.colors = colors;

        Text labelText = CreateText(buttonObject.transform, "Label", font, fontSize, FontStyle.Normal, TextAnchor.MiddleCenter, Color.black, label);
        StretchFull(labelText.rectTransform);
        return button;
    }

    private static void ApplyOutline(GameObject target, Color color)
    {
        Outline outline = target.GetComponent<Outline>() ?? target.AddComponent<Outline>();
        outline.effectColor = color;
        outline.effectDistance = new Vector2(1.2f, -1.2f);
        outline.useGraphicAlpha = false;
    }

    private static void StretchFull(RectTransform rectTransform)
    {
        rectTransform.anchorMin = Vector2.zero;
        rectTransform.anchorMax = Vector2.one;
        rectTransform.offsetMin = Vector2.zero;
        rectTransform.offsetMax = Vector2.zero;
    }

    private static void StretchToInset(RectTransform rectTransform, float inset)
    {
        rectTransform.anchorMin = Vector2.zero;
        rectTransform.anchorMax = Vector2.one;
        rectTransform.offsetMin = new Vector2(inset, inset);
        rectTransform.offsetMax = new Vector2(-inset, -inset);
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
        rectTransform.sizeDelta = size;
        rectTransform.anchoredPosition = anchoredPosition;
    }

    private static void DestroyRootObjectIfExists(Scene scene, string objectName)
    {
        GameObject[] rootObjects = scene.GetRootGameObjects();
        for (int i = 0; i < rootObjects.Length; i++)
        {
            if (rootObjects[i].name != objectName)
            {
                continue;
            }

            Object.DestroyImmediate(rootObjects[i]);
            return;
        }
    }

    private static void EnsureBuildSettingsScene()
    {
        List<EditorBuildSettingsScene> scenes = new List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
        int existingIndex = scenes.FindIndex(scene => scene.path == ScenePath);
        if (existingIndex >= 0)
        {
            scenes[existingIndex].enabled = true;
            EditorBuildSettings.scenes = scenes.ToArray();
            return;
        }

        int insertIndex = scenes.FindIndex(scene => scene.path == StageSelectScenePath);
        EditorBuildSettingsScene newScene = new EditorBuildSettingsScene(ScenePath, true);

        if (insertIndex >= 0)
        {
            scenes.Insert(insertIndex + 1, newScene);
        }
        else
        {
            scenes.Add(newScene);
        }

        EditorBuildSettings.scenes = scenes.ToArray();
    }
}
#endif
