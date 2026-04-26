using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class GarageScenePresenter : MonoBehaviour
{
    private const int PreviewLayer = 2;
    private const float ThumbnailBaseYaw = 210f;
    private const float ThumbnailSpinSpeed = 16f;
    private const float ThumbnailDistanceMultiplier = 0.98f;
    private const float PreviewPitchClamp = 40f;

    [SerializeField] private GarageSceneView view;
    [SerializeField] private Transform previewTuningAnchor;

    private readonly Dictionary<string, Image> cardFrames = new Dictionary<string, Image>();
    private readonly Dictionary<string, ThumbnailStage> liveThumbnailStages = new Dictionary<string, ThumbnailStage>();
    private readonly List<GarageHelicopterCardView> spawnedCards = new List<GarageHelicopterCardView>();
    private readonly List<RenderTexture> thumbnailTextures = new List<RenderTexture>();

    private HelicopterSelectionState selectionState;
    private Transform previewStageRoot;
    private Transform previewModelAnchor;
    private Camera previewCamera;
    private RenderTexture previewTexture;
    private GameObject previewInstance;
    private float previewYaw = 210f;
    private float previewPitch;
    private bool viewBound;

    private sealed class ThumbnailStage
    {
        public RenderTexture Texture;
        public Camera Camera;
        public Transform ModelAnchor;
        public GameObject StageRoot;
    }

    private void Awake()
    {
        EnsureStandaloneRuntimeSupport();
        ResolveView();
        BindView();
        BindSelectionState();
    }

    private void OnEnable()
    {
        EnsureStandaloneRuntimeSupport();
        ResolveView();
        BindView();
        BindSelectionState();
    }

    private void OnDisable()
    {
        if (selectionState != null)
        {
            selectionState.SelectionChanged -= RefreshSelection;
        }
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

        foreach (ThumbnailStage stage in liveThumbnailStages.Values)
        {
            if (stage?.StageRoot != null)
            {
                Destroy(stage.StageRoot);
            }
        }

        liveThumbnailStages.Clear();
    }

    private void Update()
    {
        if (!viewBound)
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
        UpdateLiveThumbnailRotation();
    }

    private void EnsureStandaloneRuntimeSupport()
    {
        if (!Application.isPlaying)
        {
            return;
        }

        // VehicleSelectScene 바로 실행시 에러 방지를 위한 방어코드.
        EnsureEventSystem();
        EnsureFallbackDisplayCamera();
    }

    private void EnsureEventSystem()
    {
        if (FindAnyObjectByType<EventSystem>() != null)
        {
            return;
        }

        GameObject eventSystemObject = new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
        eventSystemObject.transform.SetParent(transform, false);
    }

    private void EnsureFallbackDisplayCamera()
    {
        Camera[] cameras = FindObjectsByType<Camera>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
        for (int i = 0; i < cameras.Length; i++)
        {
            Camera candidate = cameras[i];
            if (candidate != null &&
                candidate.enabled &&
                candidate.gameObject.activeInHierarchy &&
                candidate.targetTexture == null)
            {
                return;
            }
        }

        GameObject cameraObject = new GameObject("GarageStandaloneCamera", typeof(Camera));
        cameraObject.transform.SetParent(transform, false);

        Camera fallbackCamera = cameraObject.GetComponent<Camera>();
        fallbackCamera.clearFlags = CameraClearFlags.SolidColor;
        fallbackCamera.backgroundColor = new Color(0.02f, 0.04f, 0.08f, 1f);
        fallbackCamera.cullingMask = 0;
        fallbackCamera.depth = -100f;
        fallbackCamera.nearClipPlane = 0.1f;
        fallbackCamera.farClipPlane = 10f;
        ConfigurePreviewCamera(fallbackCamera);
    }

    private void ResolveView()
    {
        if (view == null)
        {
            view = GetComponent<GarageSceneView>();
        }

        if (view == null)
        {
            view = GetComponentInChildren<GarageSceneView>(true);
        }
    }

    private void BindView()
    {
        if (view == null || !view.IsConfigured)
        {
            viewBound = false;
            return;
        }

        Canvas canvas = view.Canvas;
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.overrideSorting = true;
        canvas.sortingOrder = 100;

        CanvasScaler scaler = canvas.GetComponent<CanvasScaler>() ?? canvas.gameObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = 0.5f;

        if (string.IsNullOrWhiteSpace(view.TitleText.text))
        {
            view.TitleText.text = "Garage";
        }

        view.CloseButton.onClick.RemoveAllListeners();
        view.CloseButton.onClick.AddListener(CloseScene);
        view.HelicopterCardTemplate.gameObject.SetActive(false);

        EnsurePreviewStage();
        viewBound = true;
    }

    private void BindSelectionState()
    {
        HelicopterSelectionState newSelectionState = HelicopterSelectionState.EnsureInitialized();
        if (selectionState == newSelectionState)
        {
            if (viewBound)
            {
                RebuildCards();
                RefreshSelection();
            }

            return;
        }

        if (selectionState != null)
        {
            selectionState.SelectionChanged -= RefreshSelection;
        }

        selectionState = newSelectionState;
        if (selectionState != null)
        {
            selectionState.SelectionChanged += RefreshSelection;
        }

        if (viewBound)
        {
            RebuildCards();
            RefreshSelection();
        }
    }

    private void EnsurePreviewStage()
    {
        if (previewStageRoot == null)
        {
            previewStageRoot = new GameObject("GaragePreviewStage").transform;
            previewStageRoot.SetParent(transform, false);
        }

        if (previewModelAnchor == null)
        {
            previewModelAnchor = new GameObject("PreviewModelAnchor").transform;
            previewModelAnchor.SetParent(previewStageRoot, false);
        }

        if (previewTexture == null)
        {
            previewTexture = new RenderTexture(1024, 1024, 24);
            previewTexture.name = "GaragePreviewTexture";
            previewTexture.Create();
        }

        view.PreviewImage.texture = previewTexture;

        if (previewCamera == null)
        {
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

    private void RebuildCards()
    {
        if (!viewBound || selectionState == null)
        {
            return;
        }

        for (int i = 0; i < spawnedCards.Count; i++)
        {
            if (spawnedCards[i] != null)
            {
                Destroy(spawnedCards[i].gameObject);
            }
        }

        spawnedCards.Clear();
        cardFrames.Clear();

        IReadOnlyList<VehicleDefinition> helicopters = selectionState.OwnedHelicopters;
        for (int i = 0; i < helicopters.Count; i++)
        {
            CreateHelicopterCard(helicopters[i]);
        }

        LayoutRebuilder.ForceRebuildLayoutImmediate(view.HelicopterContent);
    }

    private void CreateHelicopterCard(VehicleDefinition entry)
    {
        GarageHelicopterCardView card = Instantiate(view.HelicopterCardTemplate, view.HelicopterContent);
        card.gameObject.name = $"{entry.Id}Card";
        card.gameObject.SetActive(true);

        card.Button.onClick.RemoveAllListeners();
        card.Button.onClick.AddListener(() =>
        {
            selectionState.SelectHelicopter(entry.Id);
            RefreshSelection();
        });

        card.ThumbnailImage.texture = GetCardThumbnail(entry);
        card.ThumbnailImage.color = Color.white;
        card.NameText.text = entry.DisplayName;
        card.Frame.color = new Color(0.15f, 0.2f, 0.28f, 1f);

        spawnedCards.Add(card);
        cardFrames[entry.Id] = card.Frame;
    }

    private void RefreshSelection()
    {
        if (!viewBound || selectionState == null)
        {
            return;
        }

        VehicleDefinition selectedHelicopter = selectionState.EnsureSelectedHelicopter();
        if (selectedHelicopter == null)
        {
            return;
        }

        view.SelectedHelicopterText.text = $"Selected: {selectedHelicopter.DisplayName}";
        RebuildPreview(selectedHelicopter);
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

    private void RebuildPreview(VehicleDefinition selectedHelicopter)
    {
        if (previewInstance != null)
        {
            Destroy(previewInstance);
        }

        GameObject sourcePrefab = selectedHelicopter.Prefab;
        previewInstance = InstantiatePreviewObject(sourcePrefab, previewModelAnchor);

        previewInstance.name = "SelectedHelicopterPreview";
        previewInstance.transform.localPosition = Vector3.zero;
        previewInstance.transform.localRotation = Quaternion.identity;
        previewInstance.transform.localScale = Vector3.one;

        DisablePreviewBehaviours(previewInstance);
        SetLayerRecursively(previewInstance, PreviewLayer);
        FramePreview(previewInstance);
        ApplyPreviewTuning(previewInstance.transform);
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
        instance.transform.localPosition = Vector3.zero;
        instance.transform.localRotation = Quaternion.identity;
        instance.transform.localScale = Vector3.one;

        if (wrapper.GetComponentsInChildren<Renderer>(true).Length > 0)
        {
            return wrapper;
        }

        Destroy(wrapper);
        return CreateFallbackPreview(parent);
    }

    private Texture GetCardThumbnail(VehicleDefinition entry)
    {
        if (entry == null)
        {
            return null;
        }

        if (liveThumbnailStages.TryGetValue(entry.Id, out ThumbnailStage existingStage) &&
            existingStage != null &&
            existingStage.Texture != null)
        {
            return existingStage.Texture;
        }

        ThumbnailStage thumbnailStage = CreateHelicopterThumbnail(entry);
        liveThumbnailStages[entry.Id] = thumbnailStage;
        return thumbnailStage.Texture;
    }

    private ThumbnailStage CreateHelicopterThumbnail(VehicleDefinition entry)
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
        camera.backgroundColor = new Color(0.03f, 0.05f, 0.08f, 1f);
        camera.fieldOfView = 24f;
        camera.nearClipPlane = 0.1f;
        camera.farClipPlane = 200f;
        camera.cullingMask = 1 << PreviewLayer;
        camera.enabled = false;
        ConfigurePreviewCamera(camera);

        Transform modelAnchor = new GameObject("ThumbnailModelAnchor").transform;
        modelAnchor.SetParent(stageRoot.transform, false);

        CreatePreviewLight(stageRoot.transform, "ThumbnailKeyLight", new Vector3(40f, -35f, 0f), 1.35f);
        CreatePreviewLight(stageRoot.transform, "ThumbnailFillLight", new Vector3(20f, 145f, 0f), 0.7f);

        GameObject sourcePrefab = entry.Prefab;
        GameObject thumbnailInstance = InstantiatePreviewObject(sourcePrefab, modelAnchor);
        thumbnailInstance.transform.localPosition = Vector3.zero;
        thumbnailInstance.transform.localRotation = Quaternion.identity;
        thumbnailInstance.transform.localScale = Vector3.one;

        DisablePreviewBehaviours(thumbnailInstance);
        SetLayerRecursively(thumbnailInstance, PreviewLayer);

        PositionThumbnailCamera(thumbnailInstance, camera);
        modelAnchor.localRotation = Quaternion.Euler(0f, ThumbnailBaseYaw, 0f);
        RenderThumbnailCamera(camera, thumbnailTexture);

        return new ThumbnailStage
        {
            Texture = thumbnailTexture,
            Camera = camera,
            ModelAnchor = modelAnchor,
            StageRoot = stageRoot
        };
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
        float distance = radius / Mathf.Tan(camera.fieldOfView * 0.5f * Mathf.Deg2Rad) * ThumbnailDistanceMultiplier;

        camera.transform.localPosition = new Vector3(0f, radius * 0.18f, -distance);
        camera.transform.localRotation = Quaternion.Euler(8f, 0f, 0f);
    }

    private static float CenterPreviewAtOrigin(GameObject target)
    {
        Bounds bounds = CalculateBounds(target);
        Vector3 localCenter = target.transform.parent != null
            ? target.transform.parent.InverseTransformPoint(bounds.center)
            : bounds.center;
        target.transform.localPosition -= localCenter;

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

    private void HandlePreviewRotation()
    {
        if (view == null || view.PreviewRect == null || previewInstance == null)
        {
            return;
        }

        Mouse mouse = Mouse.current;
        if (mouse != null)
        {
            Vector2 mousePosition = mouse.position.ReadValue();
            if (RectTransformUtility.RectangleContainsScreenPoint(view.PreviewRect, mousePosition))
            {
                float scrollDelta = mouse.scroll.ReadValue().y;
                if (Mathf.Abs(scrollDelta) > 0.01f)
                {
                    previewYaw += scrollDelta * 1.00f;
                }

                if (mouse.leftButton.isPressed)
                {
                    previewYaw += mouse.delta.ReadValue().x * 0.18f;
                    previewPitch = Mathf.Clamp(previewPitch - mouse.delta.ReadValue().y * 0.12f, -PreviewPitchClamp, PreviewPitchClamp);
                }
            }
        }

        Touchscreen touchscreen = Touchscreen.current;
        if (touchscreen != null && touchscreen.primaryTouch.press.isPressed)
        {
            Vector2 touchPosition = touchscreen.primaryTouch.position.ReadValue();
            if (RectTransformUtility.RectangleContainsScreenPoint(view.PreviewRect, touchPosition))
            {
                previewYaw += touchscreen.primaryTouch.delta.ReadValue().x * 0.18f;
                previewPitch = Mathf.Clamp(previewPitch - touchscreen.primaryTouch.delta.ReadValue().y * 0.12f, -PreviewPitchClamp, PreviewPitchClamp);
            }
        }

        ApplyPreviewRotation();
    }

    private void ApplyPreviewRotation()
    {
        if (previewModelAnchor != null)
        {
            previewModelAnchor.localRotation = Quaternion.Euler(previewPitch, previewYaw, 0f);
        }
    }

    private void UpdateLiveThumbnailRotation()
    {
        if (liveThumbnailStages.Count == 0)
        {
            return;
        }

        float animatedYaw = ThumbnailBaseYaw + Time.unscaledTime * ThumbnailSpinSpeed;
        foreach (ThumbnailStage stage in liveThumbnailStages.Values)
        {
            if (stage?.ModelAnchor == null)
            {
                continue;
            }

            stage.ModelAnchor.localRotation = Quaternion.Euler(0f, animatedYaw, 0f);
            if (stage.Camera != null && stage.Texture != null)
            {
                RenderThumbnailCamera(stage.Camera, stage.Texture);
            }
        }
    }

    private static void RenderThumbnailCamera(Camera camera, RenderTexture targetTexture)
    {
        if (camera == null || targetTexture == null)
        {
            return;
        }

        RenderPipeline.StandardRequest request = new RenderPipeline.StandardRequest
        {
            destination = targetTexture,
            mipLevel = 0,
            slice = 0,
            face = CubemapFace.Unknown
        };

        RenderPipeline.SubmitRenderRequest(camera, request);
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
