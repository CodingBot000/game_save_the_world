using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class GarageScenePresenter : MonoBehaviour
{
    private const int PreviewLayer = 2;
    private const float HelicopterCardScale = 1.7f;
    private const float HelicopterCardWidth = 220f * HelicopterCardScale;
    private const float HelicopterCardHeight = 300f;
    private static readonly Vector3 BattleVisualLocalPosition = new Vector3(0f, 1f, 0f);
    private static readonly Vector3 BattleVisualLocalScale = new Vector3(1f, 0.6920179f, 1f);
    private static readonly Quaternion BattleVisualLocalRotation = Quaternion.Euler(-90f, 0f, 0f);

    [SerializeField] private bool autoBuildUi = true;
    [SerializeField] private Transform previewTuningAnchor;

    private readonly Dictionary<string, Image> cardFrames = new Dictionary<string, Image>();
    private readonly List<RenderTexture> thumbnailTextures = new List<RenderTexture>();

    private Canvas canvas;
    private bool uiBuilt;
    private RectTransform previewRect;
    private RawImage previewImage;
    private Text selectedHelicopterText;
    private Transform previewStageRoot;
    private Transform previewModelAnchor;
    private Camera previewCamera;
    private RenderTexture previewTexture;
    private GameObject previewInstance;
    private float previewYaw = 210f;

    private void Awake()
    {
        ResolveCanvas();
        TryBuildUi();
    }

    private void OnEnable()
    {
        ResolveCanvas();
        TryBuildUi();
    }

    private void OnDestroy()
    {
        if (previewTexture != null)
        {
            previewTexture.Release();
            Destroy(previewTexture);
        }

        for (int i = 0; i < thumbnailTextures.Count; i++)
        {
            if (thumbnailTextures[i] == null)
            {
                continue;
            }

            thumbnailTextures[i].Release();
            Destroy(thumbnailTextures[i]);
        }

        thumbnailTextures.Clear();
    }

    private void Update()
    {
        if (!uiBuilt)
        {
            return;
        }

        Keyboard keyboard = Keyboard.current;
        if (keyboard != null && keyboard.escapeKey.wasPressedThisFrame)
        {
            CloseScene();
            return;
        }

        HandlePreviewRotation();
    }

    private void ResolveCanvas()
    {
        canvas = GetComponent<Canvas>();
        if (canvas == null)
        {
            canvas = GetComponentInParent<Canvas>();
        }

        if (canvas == null)
        {
            GameObject canvasObject = new GameObject(
                "GarageCanvas",
                typeof(RectTransform),
                typeof(Canvas),
                typeof(CanvasScaler),
                typeof(GraphicRaycaster));
            canvasObject.transform.SetParent(transform, false);
            canvas = canvasObject.GetComponent<Canvas>();
        }
    }

    private void TryBuildUi()
    {
        if (uiBuilt || !autoBuildUi || canvas == null)
        {
            return;
        }

        BuildUi();
        uiBuilt = true;
    }

    private void BuildUi()
    {
        HelicopterSelectionState selectionState = HelicopterSelectionState.EnsureInitialized();
        Font runtimeFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.overrideSorting = true;
        canvas.sortingOrder = 100;

        CanvasScaler scaler = canvas.GetComponent<CanvasScaler>() ?? canvas.gameObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = 0.5f;

        GameObject root = SimpleUiFactory.FindOrCreateUiObject("GarageRoot", canvas.transform);
        SimpleUiFactory.StretchFull(root.GetComponent<RectTransform>());

        Image dimmer = SimpleUiFactory.CreateImage("Dimmer", root.transform, new Color(0.02f, 0.04f, 0.08f, 0.84f));
        SimpleUiFactory.StretchFull(dimmer.rectTransform);

        Image panel = SimpleUiFactory.CreateImage("Panel", root.transform, new Color(0.07f, 0.11f, 0.16f, 0.98f));
        SimpleUiFactory.StretchFull(panel.rectTransform);

        Image accent = SimpleUiFactory.CreateImage("Accent", panel.transform, new Color(0.16f, 0.55f, 0.9f, 1f));
        SimpleUiFactory.SetAnchoredLayout(
            accent.rectTransform,
            new Vector2(0f, 1f),
            new Vector2(1f, 1f),
            new Vector2(0.5f, 1f),
            new Vector2(0f, 10f),
            Vector2.zero);

        Button closeButton = SimpleUiFactory.CreateButton(
            "CloseButton",
            root.transform,
            runtimeFont,
            "X",
            new Color(0.69f, 0.18f, 0.18f, 1f),
            CloseScene,
            24);
        SimpleUiFactory.SetAnchoredLayout(
            closeButton.GetComponent<RectTransform>(),
            new Vector2(0f, 1f),
            new Vector2(0f, 1f),
            new Vector2(0f, 1f),
            new Vector2(72f, 72f),
            new Vector2(44f, -44f));

        Text title = SimpleUiFactory.CreateText("Title", panel.transform, runtimeFont, 42, FontStyle.Bold, TextAnchor.MiddleLeft, Color.white);
        title.text = "Garage";
        SimpleUiFactory.SetAnchoredLayout(
            title.rectTransform,
            new Vector2(0f, 1f),
            new Vector2(0f, 1f),
            new Vector2(0f, 1f),
            new Vector2(320f, 56f),
            new Vector2(132f, -52f));

        Text subtitle = SimpleUiFactory.CreateText("Subtitle", panel.transform, runtimeFont, 20, FontStyle.Normal, TextAnchor.UpperLeft, new Color(0.84f, 0.89f, 0.96f));
        subtitle.text = "";
        SimpleUiFactory.SetAnchoredLayout(
            subtitle.rectTransform,
            new Vector2(0f, 1f),
            new Vector2(1f, 1f),
            new Vector2(0f, 1f),
            new Vector2(-320f, 64f),
            new Vector2(56f, -136f));

        selectedHelicopterText = SimpleUiFactory.CreateText("SelectedHelicopter", panel.transform, runtimeFont, 22, FontStyle.Bold, TextAnchor.MiddleRight, new Color(0.9f, 0.95f, 1f));
        SimpleUiFactory.SetAnchoredLayout(
            selectedHelicopterText.rectTransform,
            new Vector2(1f, 1f),
            new Vector2(1f, 1f),
            new Vector2(1f, 1f),
            new Vector2(340f, 34f),
            new Vector2(-56f, -78f));

        Image previewFrame = SimpleUiFactory.CreateImage("PreviewFrame", panel.transform, new Color(0.09f, 0.14f, 0.2f, 1f));
        previewRect = previewFrame.rectTransform;
        SimpleUiFactory.SetAnchoredLayout(
            previewRect,
            new Vector2(0.18f, 0.28f),
            new Vector2(0.82f, 0.88f),
            new Vector2(0.5f, 0.5f),
            Vector2.zero,
            new Vector2(0f, -12f));

        GameObject previewImageObject = SimpleUiFactory.FindOrCreateUiObject("PreviewImage", previewFrame.transform);
        previewImage = previewImageObject.GetComponent<RawImage>() ?? previewImageObject.AddComponent<RawImage>();

        SimpleUiFactory.StretchFull(previewImage.rectTransform);
        previewImage.color = Color.white;

        BuildPreviewStage();
        BuildHelicopterScroller(panel.transform, runtimeFont, selectionState);
        RefreshSelection();
    }

    private void BuildPreviewStage()
    {
        previewStageRoot = new GameObject("GaragePreviewStage").transform;
        previewStageRoot.SetParent(transform, false);

        previewModelAnchor = new GameObject("PreviewModelAnchor").transform;
        previewModelAnchor.SetParent(previewStageRoot, false);

        previewTexture = new RenderTexture(1024, 1024, 24);
        previewTexture.name = "GaragePreviewTexture";
        previewTexture.Create();
        previewImage.texture = previewTexture;

        GameObject cameraObject = new GameObject("GaragePreviewCamera", typeof(Camera));
        cameraObject.transform.SetParent(previewStageRoot, false);
        previewCamera = cameraObject.GetComponent<Camera>();
        previewCamera.clearFlags = CameraClearFlags.SolidColor;
        previewCamera.backgroundColor = new Color(0.03f, 0.05f, 0.08f, 0f);
        previewCamera.fieldOfView = 28f;
        previewCamera.nearClipPlane = 0.1f;
        previewCamera.farClipPlane = 200f;
        previewCamera.targetTexture = previewTexture;
        previewCamera.cullingMask = 1 << PreviewLayer;
        ConfigurePreviewCamera(previewCamera);

        CreatePreviewLight("KeyLight", new Vector3(40f, -35f, 0f), 1.4f);
        CreatePreviewLight("FillLight", new Vector3(20f, 145f, 0f), 0.75f);
    }

    private void CreatePreviewLight(string name, Vector3 eulerAngles, float intensity)
    {
        CreatePreviewLight(previewStageRoot, name, eulerAngles, intensity);
    }

    private static void CreatePreviewLight(Transform parent, string name, Vector3 eulerAngles, float intensity)
    {
        GameObject lightObject = new GameObject(name, typeof(Light));
        lightObject.transform.SetParent(parent, false);
        lightObject.transform.localRotation = Quaternion.Euler(eulerAngles);
        lightObject.layer = PreviewLayer;

        Light light = lightObject.GetComponent<Light>();
        light.type = LightType.Directional;
        light.intensity = intensity;
        light.color = Color.white;
        light.cullingMask = 1 << PreviewLayer;
    }

    private void BuildHelicopterScroller(Transform parent, Font font, HelicopterSelectionState selectionState)
    {
        Image listPanel = SimpleUiFactory.CreateImage("HelicopterScrollerPanel", parent, new Color(0.05f, 0.08f, 0.12f, 0.96f));
        SimpleUiFactory.SetAnchoredLayout(
            listPanel.rectTransform,
            new Vector2(0f, 0f),
            new Vector2(1f, 0f),
            new Vector2(0.5f, 0f),
            new Vector2(-48f, 360f),
            new Vector2(0f, 18f));

        GameObject viewport = SimpleUiFactory.FindOrCreateUiObject("Viewport", listPanel.transform);
        RectTransform viewportRect = viewport.GetComponent<RectTransform>();
        viewportRect.anchorMin = new Vector2(0f, 0f);
        viewportRect.anchorMax = new Vector2(1f, 1f);
        viewportRect.pivot = new Vector2(0.5f, 0.5f);
        viewportRect.offsetMin = new Vector2(10f, 10f);
        viewportRect.offsetMax = new Vector2(-10f, -10f);

        Image viewportImage = viewport.GetComponent<Image>() ?? viewport.AddComponent<Image>();
        viewportImage.color = new Color(1f, 1f, 1f, 0.01f);
        viewportImage.raycastTarget = true;

        Mask existingMask = viewport.GetComponent<Mask>();
        if (existingMask != null)
        {
            Destroy(existingMask);
        }

        RectMask2D rectMask = viewport.GetComponent<RectMask2D>() ?? viewport.AddComponent<RectMask2D>();

        GameObject content = SimpleUiFactory.FindOrCreateUiObject("Content", viewport.transform);
        RectTransform contentRect = content.GetComponent<RectTransform>();
        contentRect.anchorMin = new Vector2(0f, 0f);
        contentRect.anchorMax = new Vector2(0f, 1f);
        contentRect.pivot = new Vector2(0f, 0.5f);
        contentRect.anchoredPosition = Vector2.zero;
        contentRect.sizeDelta = new Vector2(0f, 0f);

        HorizontalLayoutGroup layoutGroup = content.GetComponent<HorizontalLayoutGroup>() ?? content.AddComponent<HorizontalLayoutGroup>();
        layoutGroup.spacing = 10f;
        layoutGroup.childAlignment = TextAnchor.MiddleLeft;
        layoutGroup.childControlWidth = false;
        layoutGroup.childControlHeight = false;
        layoutGroup.childForceExpandWidth = false;
        layoutGroup.childForceExpandHeight = false;
        layoutGroup.padding = new RectOffset(4, 4, 4, 4);

        ContentSizeFitter contentSizeFitter = content.GetComponent<ContentSizeFitter>() ?? content.AddComponent<ContentSizeFitter>();
        contentSizeFitter.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
        contentSizeFitter.verticalFit = ContentSizeFitter.FitMode.Unconstrained;

        ScrollRect scrollRect = listPanel.GetComponent<ScrollRect>() ?? listPanel.gameObject.AddComponent<ScrollRect>();
        scrollRect.viewport = viewportRect;
        scrollRect.content = contentRect;
        scrollRect.horizontal = true;
        scrollRect.vertical = false;
        scrollRect.inertia = true;
        scrollRect.movementType = ScrollRect.MovementType.Clamped;
        scrollRect.scrollSensitivity = 24f;

        IReadOnlyList<HelicopterGarageEntry> helicopters = selectionState.OwnedHelicopters;
        for (int i = 0; i < helicopters.Count; i++)
        {
            CreateHelicopterCard(content.transform, font, helicopters[i], selectionState, HelicopterCardWidth, HelicopterCardHeight);
        }

        Canvas.ForceUpdateCanvases();
        LayoutRebuilder.ForceRebuildLayoutImmediate(contentRect);
        LayoutRebuilder.ForceRebuildLayoutImmediate(viewportRect);
        LayoutRebuilder.ForceRebuildLayoutImmediate(listPanel.rectTransform);
    }

    private void CreateHelicopterCard(
        Transform parent,
        Font font,
        HelicopterGarageEntry entry,
        HelicopterSelectionState selectionState,
        float cardWidth,
        float cardHeight)
    {
        GameObject cardObject = SimpleUiFactory.FindOrCreateUiObject($"{entry.Id}Card", parent);
        Image cardFrame = cardObject.GetComponent<Image>() ?? cardObject.AddComponent<Image>();
        cardFrame.color = new Color(0.15f, 0.2f, 0.28f, 1f);

        Button button = cardObject.GetComponent<Button>() ?? cardObject.AddComponent<Button>();
        button.targetGraphic = cardFrame;
        button.onClick.RemoveAllListeners();
        button.onClick.AddListener(() =>
        {
            selectionState.SelectHelicopter(entry.Id);
            RefreshSelection();
        });

        RectTransform cardRect = cardObject.GetComponent<RectTransform>();
        cardRect.anchorMin = new Vector2(0f, 0.5f);
        cardRect.anchorMax = new Vector2(0f, 0.5f);
        cardRect.pivot = new Vector2(0f, 0.5f);
        cardRect.sizeDelta = new Vector2(cardWidth, cardHeight);
        cardRect.anchoredPosition = Vector2.zero;

        LayoutElement layoutElement = cardObject.GetComponent<LayoutElement>() ?? cardObject.AddComponent<LayoutElement>();
        layoutElement.preferredWidth = cardWidth;
        layoutElement.preferredHeight = cardHeight;
        layoutElement.minWidth = cardWidth;
        layoutElement.minHeight = cardHeight;
        layoutElement.flexibleWidth = 0f;
        layoutElement.flexibleHeight = 0f;

        Image colorSwatch = SimpleUiFactory.CreateImage("ColorSwatch", cardObject.transform, new Color(0.12f, 0.16f, 0.24f, 1f));
        colorSwatch.rectTransform.anchorMin = Vector2.zero;
        colorSwatch.rectTransform.anchorMax = Vector2.one;
        colorSwatch.rectTransform.pivot = new Vector2(0.5f, 0.5f);
        colorSwatch.rectTransform.offsetMin = new Vector2(4f, 42f);
        colorSwatch.rectTransform.offsetMax = new Vector2(-4f, -4f);

        RawImage thumbnailImage = SimpleUiFactory.FindOrCreateUiObject("Thumbnail", colorSwatch.transform).GetComponent<RawImage>();
        if (thumbnailImage == null)
        {
            thumbnailImage = SimpleUiFactory.FindOrCreateUiObject("Thumbnail", colorSwatch.transform).AddComponent<RawImage>();
        }

        SimpleUiFactory.StretchFull(thumbnailImage.rectTransform);
        Texture2D bakedThumbnail = Resources.Load<Texture2D>($"Garage/HelicopterThumbnails/{entry.DisplayName}");
        thumbnailImage.texture = bakedThumbnail != null ? bakedThumbnail : CreateHelicopterThumbnail(selectionState, entry);
        thumbnailImage.color = Color.white;
        thumbnailImage.raycastTarget = false;

        Image labelBar = SimpleUiFactory.CreateImage("LabelBar", cardObject.transform, new Color(0.08f, 0.11f, 0.16f, 0.96f));
        labelBar.rectTransform.anchorMin = new Vector2(0f, 0f);
        labelBar.rectTransform.anchorMax = new Vector2(1f, 0f);
        labelBar.rectTransform.pivot = new Vector2(0.5f, 0f);
        labelBar.rectTransform.sizeDelta = new Vector2(-8f, 30f);
        labelBar.rectTransform.anchoredPosition = new Vector2(0f, 4f);
        labelBar.raycastTarget = false;

        Text nameText = SimpleUiFactory.CreateText("Name", cardObject.transform, font, Mathf.RoundToInt(14f * HelicopterCardScale), FontStyle.Bold, TextAnchor.MiddleCenter, Color.white);
        nameText.text = entry.DisplayName;
        nameText.rectTransform.anchorMin = new Vector2(0f, 0f);
        nameText.rectTransform.anchorMax = new Vector2(1f, 0f);
        nameText.rectTransform.pivot = new Vector2(0.5f, 0f);
        nameText.rectTransform.sizeDelta = new Vector2(-12f, 26f);
        nameText.rectTransform.anchoredPosition = new Vector2(0f, 6f);
        nameText.horizontalOverflow = HorizontalWrapMode.Overflow;
        nameText.verticalOverflow = VerticalWrapMode.Truncate;
        nameText.raycastTarget = false;

        cardFrames[entry.Id] = cardFrame;
    }

    private void RefreshSelection()
    {
        HelicopterSelectionState selectionState = HelicopterSelectionState.EnsureInitialized();
        HelicopterGarageEntry selectedHelicopter = selectionState.SelectedHelicopter;
        if (selectedHelicopter == null)
        {
            return;
        }

        selectedHelicopterText.text = $"Selected: {selectedHelicopter.DisplayName}";
        RebuildPreview(selectionState, selectedHelicopter);
        RefreshCardHighlights(selectedHelicopter.Id);
    }

    private void RefreshCardHighlights(string selectedId)
    {
        foreach (KeyValuePair<string, Image> pair in cardFrames)
        {
            pair.Value.color = pair.Key == selectedId
                ? new Color(0.21f, 0.45f, 0.74f, 1f)
                : new Color(0.15f, 0.2f, 0.28f, 1f);
        }
    }

    private void RebuildPreview(HelicopterSelectionState selectionState, HelicopterGarageEntry selectedHelicopter)
    {
        if (previewInstance != null)
        {
            Destroy(previewInstance);
        }

        GameObject sourcePrefab = selectionState.LoadPreviewPrefab(selectedHelicopter);
        previewInstance = InstantiatePreviewObject(sourcePrefab, previewModelAnchor);

        previewInstance.name = "SelectedHelicopterPreview";
        previewInstance.transform.localPosition = Vector3.zero;
        previewInstance.transform.localRotation = Quaternion.identity;
        previewInstance.transform.localScale = Vector3.one * 1.65f;

        DisablePreviewBehaviours(previewInstance);
        SetLayerRecursively(previewInstance, PreviewLayer);
        ApplyTint(previewInstance, selectedHelicopter.Tint);
        ApplyPreviewTuning(previewInstance.transform);
        FramePreview(previewInstance);
        ApplyPreviewTuningOffset(previewInstance.transform);
        ApplyPreviewRotation();
    }

    private static GameObject InstantiatePreviewObject(GameObject sourcePrefab, Transform parent)
    {
        if (sourcePrefab == null)
        {
            return CreateFallbackPreview(parent);
        }

        GameObject wrapper = new GameObject("HelicopterVisualRoot");
        wrapper.transform.SetParent(parent, false);

        GameObject instance = Instantiate(sourcePrefab, wrapper.transform);
        instance.transform.localPosition = BattleVisualLocalPosition;
        instance.transform.localRotation = BattleVisualLocalRotation;
        instance.transform.localScale = BattleVisualLocalScale;

        if (wrapper.GetComponentsInChildren<Renderer>(true).Length > 0)
        {
            return wrapper;
        }

        Destroy(wrapper);
        return CreateFallbackPreview(parent);
    }

    private RenderTexture CreateHelicopterThumbnail(HelicopterSelectionState selectionState, HelicopterGarageEntry entry)
    {
        RenderTexture thumbnailTexture = new RenderTexture(512, 512, 24);
        thumbnailTexture.name = $"{entry.Id}_Thumbnail";
        thumbnailTexture.Create();
        thumbnailTextures.Add(thumbnailTexture);

        GameObject stageRoot = new GameObject($"{entry.Id}_ThumbnailStage");
        stageRoot.transform.SetParent(transform, false);

        GameObject cameraObject = new GameObject("ThumbnailCamera", typeof(Camera));
        cameraObject.transform.SetParent(stageRoot.transform, false);
        stageRoot.transform.position = new Vector3(2000f + thumbnailTextures.Count * 20f, 2000f, 2000f);

        Camera camera = cameraObject.GetComponent<Camera>();
        camera.clearFlags = CameraClearFlags.SolidColor;
        camera.backgroundColor = new Color(0.03f, 0.05f, 0.08f, 0f);
        camera.fieldOfView = 24f;
        camera.nearClipPlane = 0.1f;
        camera.farClipPlane = 200f;
        camera.targetTexture = thumbnailTexture;
        camera.cullingMask = 1 << PreviewLayer;
        camera.enabled = true;
        ConfigurePreviewCamera(camera);

        Transform modelAnchor = new GameObject("ThumbnailModelAnchor").transform;
        modelAnchor.SetParent(stageRoot.transform, false);

        CreatePreviewLight(stageRoot.transform, "ThumbnailKeyLight", new Vector3(40f, -35f, 0f), 1.35f);
        CreatePreviewLight(stageRoot.transform, "ThumbnailFillLight", new Vector3(20f, 145f, 0f), 0.7f);

        GameObject sourcePrefab = selectionState.LoadPreviewPrefab(entry);
        GameObject thumbnailInstance = InstantiatePreviewObject(sourcePrefab, modelAnchor);
        thumbnailInstance.transform.localPosition = Vector3.zero;
        thumbnailInstance.transform.localRotation = Quaternion.identity;
        thumbnailInstance.transform.localScale = Vector3.one * 1.35f;

        DisablePreviewBehaviours(thumbnailInstance);
        SetLayerRecursively(thumbnailInstance, PreviewLayer);
        ApplyTint(thumbnailInstance, entry.Tint);

        PositionThumbnailCamera(thumbnailInstance, camera);
        modelAnchor.localRotation = Quaternion.Euler(0f, 210f, 0f);

        return thumbnailTexture;
    }

    private void ApplyPreviewTuning(Transform target)
    {
        if (previewTuningAnchor == null)
        {
            return;
        }

        target.localRotation = previewTuningAnchor.localRotation * target.localRotation;
        target.localScale = Vector3.Scale(target.localScale, previewTuningAnchor.localScale);
    }

    private void ApplyPreviewTuningOffset(Transform target)
    {
        if (previewTuningAnchor == null)
        {
            return;
        }

        target.localPosition += previewTuningAnchor.localPosition;
    }

    private void FramePreview(GameObject target)
    {
        float radius = CenterPreviewAtOrigin(target);
        float distance = radius / Mathf.Tan(previewCamera.fieldOfView * 0.5f * Mathf.Deg2Rad) * 2f;

        previewCamera.transform.localPosition = new Vector3(0f, radius * 0.25f, -distance);
        previewCamera.transform.localRotation = Quaternion.Euler(10f, 0f, 0f);
    }

    private static void PositionThumbnailCamera(GameObject target, Camera camera)
    {
        float radius = CenterPreviewAtOrigin(target);
        float distance = radius / Mathf.Tan(camera.fieldOfView * 0.5f * Mathf.Deg2Rad) * 2.15f;

        camera.transform.localPosition = new Vector3(0f, radius * 0.18f, -distance);
        camera.transform.localRotation = Quaternion.Euler(8f, 0f, 0f);
    }

    private static float CenterPreviewAtOrigin(GameObject target)
    {
        Bounds bounds = CalculateBounds(target);
        target.transform.localPosition -= bounds.center;

        bounds = CalculateBounds(target);
        return Mathf.Max(bounds.extents.x, bounds.extents.y, bounds.extents.z);
    }

    private static void ConfigurePreviewCamera(Camera camera)
    {
        UniversalAdditionalCameraData additionalCameraData =
            camera.GetComponent<UniversalAdditionalCameraData>() ??
            camera.gameObject.AddComponent<UniversalAdditionalCameraData>();

        additionalCameraData.renderType = CameraRenderType.Base;
        additionalCameraData.requiresColorOption = CameraOverrideOption.Off;
        additionalCameraData.requiresDepthOption = CameraOverrideOption.Off;
        additionalCameraData.SetRenderer(0);
    }

    private static Bounds CalculateBounds(GameObject target)
    {
        Renderer[] renderers = target.GetComponentsInChildren<Renderer>(true);
        if (renderers.Length == 0)
        {
            return new Bounds(Vector3.zero, Vector3.one);
        }

        Bounds bounds = renderers[0].bounds;
        for (int i = 1; i < renderers.Length; i++)
        {
            bounds.Encapsulate(renderers[i].bounds);
        }

        return bounds;
    }

    private static void DisablePreviewBehaviours(GameObject target)
    {
        MonoBehaviour[] behaviours = target.GetComponentsInChildren<MonoBehaviour>(true);
        for (int i = 0; i < behaviours.Length; i++)
        {
            behaviours[i].enabled = false;
        }
    }

    private static void SetLayerRecursively(GameObject target, int layer)
    {
        target.layer = layer;
        for (int i = 0; i < target.transform.childCount; i++)
        {
            SetLayerRecursively(target.transform.GetChild(i).gameObject, layer);
        }
    }

    private static void ApplyTint(GameObject target, Color tint)
    {
        Renderer[] renderers = target.GetComponentsInChildren<Renderer>(true);
        for (int i = 0; i < renderers.Length; i++)
        {
            Material[] materials = renderers[i].materials;
            for (int j = 0; j < materials.Length; j++)
            {
                if (materials[j].HasProperty("_BaseColor"))
                {
                    materials[j].SetColor("_BaseColor", tint);
                }
                else if (materials[j].HasProperty("_Color"))
                {
                    materials[j].SetColor("_Color", tint);
                }
            }
        }
    }

    private void HandlePreviewRotation()
    {
        if (previewRect == null || previewInstance == null)
        {
            return;
        }

        Mouse mouse = Mouse.current;
        if (mouse != null)
        {
            Vector2 mousePosition = mouse.position.ReadValue();
            if (RectTransformUtility.RectangleContainsScreenPoint(previewRect, mousePosition))
            {
                float scrollDelta = mouse.scroll.ReadValue().y;
                if (Mathf.Abs(scrollDelta) > 0.01f)
                {
                    previewYaw += scrollDelta * 1.00f;
                }

                if (mouse.leftButton.isPressed)
                {
                    previewYaw += mouse.delta.ReadValue().x * 0.18f;
                }
            }
        }

        Touchscreen touchscreen = Touchscreen.current;
        if (touchscreen != null && touchscreen.primaryTouch.press.isPressed)
        {
            Vector2 touchPosition = touchscreen.primaryTouch.position.ReadValue();
            if (RectTransformUtility.RectangleContainsScreenPoint(previewRect, touchPosition))
            {
                previewYaw += touchscreen.primaryTouch.delta.ReadValue().x * 0.18f;
            }
        }

        ApplyPreviewRotation();
    }

    private void ApplyPreviewRotation()
    {
        if (previewModelAnchor != null)
        {
            previewModelAnchor.localRotation = Quaternion.Euler(0f, previewYaw, 0f);
        }
    }

    private static GameObject CreateFallbackPreview(Transform parent)
    {
        GameObject root = new GameObject("FallbackHelicopter");
        root.transform.SetParent(parent, false);

        GameObject body = GameObject.CreatePrimitive(PrimitiveType.Cube);
        body.name = "Body";
        body.transform.SetParent(root.transform, false);
        body.transform.localScale = new Vector3(1.8f, 0.55f, 3.6f);

        GameObject rotor = GameObject.CreatePrimitive(PrimitiveType.Cube);
        rotor.name = "Rotor";
        rotor.transform.SetParent(root.transform, false);
        rotor.transform.localPosition = new Vector3(0f, 0.55f, 0f);
        rotor.transform.localScale = new Vector3(4.2f, 0.08f, 0.24f);

        GameObject tail = GameObject.CreatePrimitive(PrimitiveType.Cube);
        tail.name = "Tail";
        tail.transform.SetParent(root.transform, false);
        tail.transform.localPosition = new Vector3(0f, 0.1f, -2.2f);
        tail.transform.localScale = new Vector3(0.32f, 0.22f, 1.5f);

        return root;
    }

    private void CloseScene()
    {
        SceneManager.UnloadSceneAsync(gameObject.scene);
    }
}
