using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Serialization;
using UnityEngine.UI;

[DefaultExecutionOrder(0)]
public class PlayerOrbitController : MonoBehaviour
{
    private const string DamageHurtboxName = "CrashObserver";
    private const float DefaultStrafeSpeed = 8f;
    private const float DefaultAltitudeSpeed = 8f;
    private const float DefaultForwardSpeed = 10f;
    private static readonly Rect DefaultViewportRect = Rect.MinMaxRect(0.08f, 0.1f, 0.92f, 0.9f);

    [FormerlySerializedAs("horizontalScreenSpeed")]
    [SerializeField] private float strafeSpeed = 8f;
    [FormerlySerializedAs("verticalScreenSpeed")]
    [SerializeField] private float altitudeSpeed = 8f;
    [FormerlySerializedAs("depthSpeed")]
    [SerializeField] private float forwardSpeed = 10f;
    [Tooltip("One-time guard for upgrading old viewport-based movement speeds to world-space speeds.")]
    [SerializeField] private bool movementSpeedsMigratedToWorldSpace;
    [Tooltip("Moves the helicopter on a fixed camera-depth plane so it behaves like a 2D screen object.")]
    [SerializeField] private bool useCameraPlaneMovement = true;
    [SerializeField] private PlayerMovementBounds movementBounds;
    [SerializeField] private PlayerMoveGuide playerMoveGuide;
    [SerializeField] private string visualTiltRootName = "PlayerVisualRoot";
    [Tooltip("Keeps the visual helicopter pose independent from the movement anchor rotation.")]
    [SerializeField] private bool lockVisualRootToCamera = true;
    [Tooltip("Renders the helicopter into a texture, then places that texture in screen space.")]
    [SerializeField] private bool useScreenSpaceVisual = true;
    [SerializeField] private string screenSpaceVisualLayerName = "UI";
    [SerializeField] private float screenSpaceVisualDepth = 10f;
    [SerializeField] private float screenSpaceVisualScaleMultiplier = 0.65f;
    [SerializeField] private int screenSpaceVisualTextureSize = 512;
    [SerializeField] private Vector2 screenSpaceVisualImageSize = new(520f, 360f);
    [SerializeField] private float screenSpaceVisualRenderOrthographicSize = 0.45f;
    [SerializeField] private float screenSpaceVisualFramePadding = 1.15f;
    [Tooltip("Tilts only the visible helicopter model toward movement input; the movement anchor stays locked to the 2D plane.")]
    [SerializeField] private bool enableVisualTilt = true;
    [SerializeField] private float maxVisualTiltAngle = 12f;
    [SerializeField] private float visualTiltDuration = 0.18f;

    private Transform orbitCenter;
    private Transform lookTarget;
    private Transform visualTiltRoot;
    private Transform visualPoseRoot;
    private Transform screenSpaceVisualRoot;
    private Transform screenSpaceVisualInstance;
    private Camera screenSpaceVisualRenderCamera;
    private Canvas screenSpaceVisualCanvas;
    private RawImage screenSpaceVisualImage;
    private RectTransform screenSpaceVisualRect;
    private RenderTexture screenSpaceVisualTexture;
    private bool inputEnabled = true;
    private Quaternion sceneBaseRotation = Quaternion.identity;
    private Quaternion visualTiltBaseLocalRotation = Quaternion.identity;
    private Quaternion lockedVisualWorldRotation = Quaternion.identity;
    private Quaternion lockedVisualCameraRelativeRotation = Quaternion.identity;
    private Quaternion screenSpaceVisualBaseLocalRotation = Quaternion.identity;
    private Vector2 currentVisualTilt;
    private Vector2 movementInput;
    private Vector3 previousWorldPosition;
    private Camera movementCamera;
    private Rect movementViewportRect = DefaultViewportRect;
    private Vector2 viewportPosition;
    private float cameraPlaneDepth;
    private bool hasCameraPlane;
    private float movementPlaneLocalDepth;
    private float movementPlaneWorldZ;
    private bool hasMovementPlaneDepth;
    private bool hasLockedVisualPose;
    private bool screenSpaceVisualActive;
    private bool hasPreviousWorldPosition;

    public float CurrentDistance { get; private set; }
    public Vector3 CurrentWorldVelocity { get; private set; }
    public Vector3 OrbitCenterPosition => orbitCenter != null ? orbitCenter.position : Vector3.zero;
    public Vector3 OutwardDirection
    {
        get
        {
            Vector3 direction = transform.position - OrbitCenterPosition;
            direction.y = 0f;
            return direction.sqrMagnitude > 0.001f ? direction.normalized : Vector3.back;
        }
    }

    private void Awake()
    {
        EnsureRuntimeDefaults();
        CaptureRootRotation(transform.position);
        CaptureMovementPlane(transform.position);
        CacheVisualTiltRoot();
        ResetVelocityTracking();
    }

    private void LateUpdate()
    {
        if (lookTarget == null && orbitCenter == null)
        {
            UpdateVisualTilt(Vector2.zero);
            UpdateWorldVelocity();
            return;
        }

        if (inputEnabled)
        {
            UpdateInput();
        }
        else
        {
            movementInput = Vector2.zero;
        }

        RepositionImmediate();
        EnsureScreenSpaceVisualReady();
        UpdateVisualTilt(movementInput);
        UpdateScreenSpaceVisual();
        UpdateWorldVelocity();
    }

    private void OnDestroy()
    {
        ReleaseScreenSpaceVisualTexture();
    }

    public void Configure(Transform center, Transform targetToLookAt, PlayerMovementBounds bounds, PlayerMoveGuide moveGuide = null)
    {
        EnsureRuntimeDefaults();
        orbitCenter = center;
        lookTarget = targetToLookAt;
        movementBounds = bounds;
        playerMoveGuide = moveGuide;
        CaptureRootRotation(transform.position);
        CaptureMovementPlane(transform.position);
        CacheVisualTiltRoot();
        ResetVisualTiltImmediate();
        ResetVelocityTracking();
    }

    public void RefreshVisualBindings()
    {
        CacheVisualTiltRoot();
        ResetVisualTiltImmediate();
    }

    public void SetInputEnabled(bool enabled)
    {
        inputEnabled = enabled;
    }

    public void AdoptScenePlacement(Vector3 worldPosition)
    {
        CaptureRootRotation(worldPosition);
        CaptureMovementPlane(worldPosition);

        transform.position = ClampToMovementPlane(worldPosition);
        RepositionImmediate();
        ResetVelocityTracking();
    }

    public void RepositionImmediate()
    {
        transform.position = ClampToMovementPlane(transform.position);

        transform.rotation = sceneBaseRotation;
        CurrentDistance = orbitCenter != null
            ? Vector3.Distance(
                new Vector3(transform.position.x, 0f, transform.position.z),
                new Vector3(orbitCenter.position.x, 0f, orbitCenter.position.z))
            : 0f;
    }

    private void UpdateInput()
    {
        Keyboard keyboard = Keyboard.current;

        float horizontal = 0f;
        if (keyboard != null && (keyboard.aKey.isPressed || keyboard.leftArrowKey.isPressed))
        {
            horizontal -= 1f;
        }

        if (keyboard != null && (keyboard.dKey.isPressed || keyboard.rightArrowKey.isPressed))
        {
            horizontal += 1f;
        }

        float altitude = 0f;
        if (keyboard != null && (keyboard.wKey.isPressed || keyboard.upArrowKey.isPressed))
        {
            altitude += 1f;
        }

        if (keyboard != null && (keyboard.sKey.isPressed || keyboard.downArrowKey.isPressed))
        {
            altitude -= 1f;
        }

        movementInput = Vector2.ClampMagnitude(new Vector2(horizontal, altitude), 1f);

        ResolveMovementAxes(out Vector3 right, out Vector3 up, out _);
        Vector3 movementDelta =
            right * (horizontal * strafeSpeed * Time.deltaTime) +
            up * (altitude * altitudeSpeed * Time.deltaTime);

        if (movementDelta.sqrMagnitude <= 0.000001f)
        {
            return;
        }

        transform.position += movementDelta;
    }

    private void CacheVisualTiltRoot()
    {
        visualTiltRoot = string.IsNullOrWhiteSpace(visualTiltRootName) ? null : transform.Find(visualTiltRootName);
        if (visualTiltRoot == null)
        {
            visualPoseRoot = null;
            visualTiltBaseLocalRotation = Quaternion.identity;
            lockedVisualWorldRotation = Quaternion.identity;
            lockedVisualCameraRelativeRotation = Quaternion.identity;
            hasLockedVisualPose = false;
            currentVisualTilt = Vector2.zero;
            return;
        }

        visualPoseRoot = ResolveVisualPoseRoot();
        visualTiltBaseLocalRotation = visualPoseRoot != null ? visualPoseRoot.localRotation : visualTiltRoot.localRotation;
        CaptureLockedVisualPose();
        RefreshScreenSpaceVisual();
        currentVisualTilt = Vector2.zero;
    }

    private Transform ResolveVisualPoseRoot()
    {
        if (visualTiltRoot == null)
        {
            return null;
        }

        for (int i = 0; i < visualTiltRoot.childCount; i++)
        {
            Transform child = visualTiltRoot.GetChild(i);
            if (child == null || child.name == DamageHurtboxName)
            {
                continue;
            }

            return child;
        }

        return visualTiltRoot;
    }

    private void CaptureRootRotation(Vector3 referencePosition)
    {
        _ = referencePosition;
        sceneBaseRotation = transform.rotation;
    }

    private void ResetVisualTiltImmediate()
    {
        currentVisualTilt = Vector2.zero;
        if (visualTiltRoot != null)
        {
            ApplyLockedVisualPose(Quaternion.identity);
        }
    }

    private void CaptureLockedVisualPose()
    {
        Transform target = visualPoseRoot != null ? visualPoseRoot : visualTiltRoot;
        if (target == null)
        {
            hasLockedVisualPose = false;
            return;
        }

        lockedVisualWorldRotation = target.rotation;
        if (TryResolveCameraPlane())
        {
            lockedVisualCameraRelativeRotation = Quaternion.Inverse(movementCamera.transform.rotation) * target.rotation;
        }
        else
        {
            lockedVisualCameraRelativeRotation = Quaternion.identity;
        }

        hasLockedVisualPose = true;
    }

    private void ApplyLockedVisualPose(Quaternion visualTiltOffset)
    {
        if (screenSpaceVisualRoot != null || screenSpaceVisualInstance != null)
        {
            ApplyScreenSpaceVisualPose(visualTiltOffset);
            if (screenSpaceVisualActive)
            {
                return;
            }
        }

        Transform target = visualPoseRoot != null ? visualPoseRoot : visualTiltRoot;
        if (target == null)
        {
            return;
        }

        if (!lockVisualRootToCamera)
        {
            target.localRotation = visualTiltBaseLocalRotation * visualTiltOffset;
            return;
        }

        if (!hasLockedVisualPose)
        {
            CaptureLockedVisualPose();
        }

        Quaternion baseWorldRotation = lockedVisualWorldRotation;
        if (TryResolveCameraPlane())
        {
            baseWorldRotation = movementCamera.transform.rotation * lockedVisualCameraRelativeRotation;
        }

        target.rotation = baseWorldRotation * visualTiltOffset;
    }

    private void ApplyScreenSpaceVisualPose(Quaternion visualTiltOffset)
    {
        bool appliedToScreenRoot = false;
        if (screenSpaceVisualRoot != null)
        {
            screenSpaceVisualRoot.localRotation = visualTiltOffset;
            appliedToScreenRoot = true;
        }

        if (screenSpaceVisualInstance == null)
        {
            return;
        }

        screenSpaceVisualInstance.localRotation = appliedToScreenRoot
            ? screenSpaceVisualBaseLocalRotation
            : screenSpaceVisualBaseLocalRotation * visualTiltOffset;
    }

    private void EnsureScreenSpaceVisualReady()
    {
        if (!Application.isPlaying || !useScreenSpaceVisual)
        {
            return;
        }

        if (screenSpaceVisualActive && screenSpaceVisualRoot != null && screenSpaceVisualInstance != null)
        {
            return;
        }

        screenSpaceVisualActive = false;
        CacheVisualTiltRoot();
    }

    private void RefreshScreenSpaceVisual()
    {
        Transform sourceVisual = visualPoseRoot != null ? visualPoseRoot : visualTiltRoot;
        if (!Application.isPlaying || !useScreenSpaceVisual || sourceVisual == null || !TryResolveCameraPlane())
        {
            screenSpaceVisualActive = false;
            if (screenSpaceVisualImage != null)
            {
                screenSpaceVisualImage.enabled = false;
            }

            SetRenderersEnabled(sourceVisual, true);
            return;
        }

        if (!EnsureScreenSpaceVisualOutput())
        {
            screenSpaceVisualActive = false;
            if (screenSpaceVisualImage != null)
            {
                screenSpaceVisualImage.enabled = false;
            }

            SetRenderersEnabled(sourceVisual, true);
            return;
        }

        for (int i = screenSpaceVisualRoot.childCount - 1; i >= 0; i--)
        {
            Destroy(screenSpaceVisualRoot.GetChild(i).gameObject);
        }

        GameObject visualClone = Instantiate(sourceVisual.gameObject, screenSpaceVisualRoot);
        visualClone.name = $"{sourceVisual.name}_ScreenVisual";
        screenSpaceVisualInstance = visualClone.transform;
        screenSpaceVisualInstance.localPosition = Vector3.zero;
        screenSpaceVisualBaseLocalRotation = hasLockedVisualPose
            ? lockedVisualCameraRelativeRotation
            : Quaternion.Inverse(movementCamera.transform.rotation) * sourceVisual.rotation;
        screenSpaceVisualInstance.localRotation = screenSpaceVisualBaseLocalRotation;
        screenSpaceVisualInstance.localScale = sourceVisual.localScale * Mathf.Max(0.01f, screenSpaceVisualScaleMultiplier);

        int visualLayer = ResolveScreenSpaceVisualLayer();
        SetLayerRecursively(visualClone, visualLayer);
        DisableColliders(visualClone.transform);
        SetRenderersEnabled(screenSpaceVisualInstance, true);
        SetRenderersEnabled(FindDeepChild(visualClone.transform, DamageHurtboxName), false);
        FrameScreenSpaceVisualInstance();
        SetRenderersEnabled(sourceVisual, false);

        screenSpaceVisualActive = true;
        screenSpaceVisualImage.enabled = true;
        UpdateScreenSpaceVisual();
    }

    private bool EnsureScreenSpaceVisualOutput()
    {
        if (movementCamera == null && !TryResolveCameraPlane())
        {
            return false;
        }

        int visualLayer = ResolveScreenSpaceVisualLayer();
        EnsureScreenSpaceVisualTexture();

        if (screenSpaceVisualRenderCamera == null)
        {
            GameObject cameraObject = new("PlayerScreenSpaceVisualRenderCamera");
            screenSpaceVisualRenderCamera = cameraObject.AddComponent<Camera>();
        }

        screenSpaceVisualRenderCamera.enabled = true;
        screenSpaceVisualRenderCamera.orthographic = true;
        screenSpaceVisualRenderCamera.orthographicSize = Mathf.Max(0.01f, screenSpaceVisualRenderOrthographicSize);
        screenSpaceVisualRenderCamera.nearClipPlane = 0.01f;
        screenSpaceVisualRenderCamera.farClipPlane = Mathf.Max(screenSpaceVisualDepth + 10f, 50f);
        screenSpaceVisualRenderCamera.clearFlags = CameraClearFlags.SolidColor;
        screenSpaceVisualRenderCamera.backgroundColor = new Color(0f, 0f, 0f, 0f);
        screenSpaceVisualRenderCamera.cullingMask = 1 << visualLayer;
        screenSpaceVisualRenderCamera.targetTexture = screenSpaceVisualTexture;
        screenSpaceVisualRenderCamera.transform.SetPositionAndRotation(new Vector3(10000f, 10000f, 10000f), Quaternion.identity);
        screenSpaceVisualRenderCamera.transform.localScale = Vector3.one;

        if (screenSpaceVisualRoot == null)
        {
            GameObject rootObject = new("PlayerScreenSpaceVisualRoot");
            screenSpaceVisualRoot = rootObject.transform;
        }

        screenSpaceVisualRoot.SetParent(screenSpaceVisualRenderCamera.transform, false);
        screenSpaceVisualRoot.localPosition = new Vector3(0f, 0f, Mathf.Max(screenSpaceVisualRenderCamera.nearClipPlane + 0.01f, screenSpaceVisualDepth));
        screenSpaceVisualRoot.localRotation = Quaternion.identity;
        screenSpaceVisualRoot.localScale = Vector3.one;

        if (screenSpaceVisualCanvas == null)
        {
            GameObject canvasObject = new("PlayerScreenSpaceVisualCanvas");
            screenSpaceVisualCanvas = canvasObject.AddComponent<Canvas>();
            screenSpaceVisualCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
            screenSpaceVisualCanvas.sortingOrder = -10;
        }

        if (screenSpaceVisualImage == null)
        {
            GameObject imageObject = new("PlayerScreenSpaceVisualImage");
            imageObject.transform.SetParent(screenSpaceVisualCanvas.transform, false);
            screenSpaceVisualImage = imageObject.AddComponent<RawImage>();
            screenSpaceVisualImage.raycastTarget = false;
            screenSpaceVisualRect = imageObject.GetComponent<RectTransform>();
        }

        screenSpaceVisualImage.texture = screenSpaceVisualTexture;
        screenSpaceVisualImage.color = Color.white;
        screenSpaceVisualImage.raycastTarget = false;
        screenSpaceVisualRect ??= screenSpaceVisualImage.GetComponent<RectTransform>();
        screenSpaceVisualRect.anchorMin = Vector2.zero;
        screenSpaceVisualRect.anchorMax = Vector2.zero;
        screenSpaceVisualRect.pivot = new Vector2(0.5f, 0.5f);
        return true;
    }

    private void UpdateScreenSpaceVisual()
    {
        if (!screenSpaceVisualActive || screenSpaceVisualRect == null || screenSpaceVisualInstance == null || movementCamera == null)
        {
            return;
        }

        Vector3 viewportPoint = movementCamera.WorldToViewportPoint(transform.position);
        Rect effectiveViewportRect = GetEffectiveMovementViewportRect();
        float clampedX = Mathf.Clamp(viewportPoint.x, effectiveViewportRect.xMin, effectiveViewportRect.xMax);
        float clampedY = Mathf.Clamp(viewportPoint.y, effectiveViewportRect.yMin, effectiveViewportRect.yMax);

        screenSpaceVisualRect.anchoredPosition = new Vector2(clampedX * Screen.width, clampedY * Screen.height);
        screenSpaceVisualRect.sizeDelta = screenSpaceVisualImageSize;
    }

    private void FrameScreenSpaceVisualInstance()
    {
        if (screenSpaceVisualRenderCamera == null || screenSpaceVisualRoot == null || screenSpaceVisualInstance == null)
        {
            return;
        }

        if (!TryGetEnabledRendererBounds(screenSpaceVisualInstance, out Bounds bounds))
        {
            return;
        }

        float depth = Mathf.Max(screenSpaceVisualRenderCamera.nearClipPlane + 0.01f, screenSpaceVisualDepth);
        Vector3 targetCenter = screenSpaceVisualRenderCamera.transform.position + screenSpaceVisualRenderCamera.transform.forward * depth;
        screenSpaceVisualRoot.position += targetCenter - bounds.center;

        float aspect = 1f;
        if (screenSpaceVisualTexture != null && screenSpaceVisualTexture.height > 0)
        {
            aspect = (float)screenSpaceVisualTexture.width / screenSpaceVisualTexture.height;
        }

        float requiredHalfHeight = Mathf.Max(bounds.extents.y, bounds.extents.x / Mathf.Max(0.01f, aspect));
        if (requiredHalfHeight > 0.0001f)
        {
            screenSpaceVisualRenderCamera.orthographicSize = Mathf.Clamp(
                requiredHalfHeight * Mathf.Max(1f, screenSpaceVisualFramePadding),
                0.02f,
                10f);
        }
    }

    private static bool TryGetEnabledRendererBounds(Transform root, out Bounds bounds)
    {
        bounds = default;
        if (root == null)
        {
            return false;
        }

        Renderer[] renderers = root.GetComponentsInChildren<Renderer>(true);
        bool hasBounds = false;
        for (int i = 0; i < renderers.Length; i++)
        {
            Renderer renderer = renderers[i];
            if (renderer == null || !renderer.enabled)
            {
                continue;
            }

            if (!hasBounds)
            {
                bounds = renderer.bounds;
                hasBounds = true;
                continue;
            }

            bounds.Encapsulate(renderer.bounds);
        }

        return hasBounds;
    }

    private void EnsureScreenSpaceVisualTexture()
    {
        int textureSize = Mathf.Clamp(screenSpaceVisualTextureSize, 64, 2048);
        if (screenSpaceVisualTexture != null &&
            screenSpaceVisualTexture.width == textureSize &&
            screenSpaceVisualTexture.height == textureSize)
        {
            return;
        }

        ReleaseScreenSpaceVisualTexture();
        screenSpaceVisualTexture = new RenderTexture(textureSize, textureSize, 16, RenderTextureFormat.ARGB32)
        {
            name = "PlayerScreenSpaceVisualTexture",
            antiAliasing = 2,
            useMipMap = false,
            autoGenerateMips = false
        };
        screenSpaceVisualTexture.Create();
    }

    private void ReleaseScreenSpaceVisualTexture()
    {
        if (screenSpaceVisualImage != null && screenSpaceVisualImage.texture == screenSpaceVisualTexture)
        {
            screenSpaceVisualImage.texture = null;
        }

        if (screenSpaceVisualTexture == null)
        {
            return;
        }

        screenSpaceVisualTexture.Release();
        if (Application.isPlaying)
        {
            Destroy(screenSpaceVisualTexture);
        }
        else
        {
            DestroyImmediate(screenSpaceVisualTexture);
        }

        screenSpaceVisualTexture = null;
    }

    private int ResolveScreenSpaceVisualLayer()
    {
        int layer = LayerMask.NameToLayer(screenSpaceVisualLayerName);
        return layer >= 0 ? layer : 5;
    }

    private static void SetLayerRecursively(GameObject root, int layer)
    {
        if (root == null)
        {
            return;
        }

        root.layer = layer;
        for (int i = 0; i < root.transform.childCount; i++)
        {
            SetLayerRecursively(root.transform.GetChild(i).gameObject, layer);
        }
    }

    private static void DisableColliders(Transform root)
    {
        if (root == null)
        {
            return;
        }

        Collider[] colliders = root.GetComponentsInChildren<Collider>(true);
        for (int i = 0; i < colliders.Length; i++)
        {
            colliders[i].enabled = false;
        }
    }

    private static void SetRenderersEnabled(Transform root, bool enabled)
    {
        if (root == null)
        {
            return;
        }

        Renderer[] renderers = root.GetComponentsInChildren<Renderer>(true);
        for (int i = 0; i < renderers.Length; i++)
        {
            renderers[i].enabled = enabled;
        }
    }

    private static Transform FindDeepChild(Transform root, string targetName)
    {
        if (root == null || string.IsNullOrWhiteSpace(targetName))
        {
            return null;
        }

        for (int i = 0; i < root.childCount; i++)
        {
            Transform child = root.GetChild(i);
            if (child.name == targetName)
            {
                return child;
            }

            Transform nested = FindDeepChild(child, targetName);
            if (nested != null)
            {
                return nested;
            }
        }

        return null;
    }

    private void CaptureMovementPlane(Vector3 worldPosition)
    {
        if (TryCaptureCameraPlane(worldPosition))
        {
            hasMovementPlaneDepth = true;
            return;
        }

        hasMovementPlaneDepth = true;
        if (movementBounds != null)
        {
            movementPlaneLocalDepth = movementBounds.GetLocalDepth(worldPosition);
            return;
        }

        movementPlaneWorldZ = worldPosition.z;
    }

    private Vector3 ClampToMovementPlane(Vector3 worldPosition)
    {
        if (!hasMovementPlaneDepth)
        {
            CaptureMovementPlane(worldPosition);
        }

        if (TryResolveCameraPlane())
        {
            Vector3 viewportPoint = movementCamera.WorldToViewportPoint(worldPosition);
            if (!hasCameraPlane)
            {
                CaptureCameraPlane(viewportPoint);
            }

            viewportPosition = new Vector2(
                Mathf.Clamp(viewportPoint.x, movementViewportRect.xMin, movementViewportRect.xMax),
                Mathf.Clamp(viewportPoint.y, movementViewportRect.yMin, movementViewportRect.yMax));

            return movementCamera.ViewportToWorldPoint(
                new Vector3(viewportPosition.x, viewportPosition.y, cameraPlaneDepth));
        }

        if (movementBounds != null)
        {
            return movementBounds.ClampWorldPositionToPlane(worldPosition, movementPlaneLocalDepth);
        }

        worldPosition.z = movementPlaneWorldZ;
        return worldPosition;
    }

    private bool TryCaptureCameraPlane(Vector3 worldPosition)
    {
        if (!TryResolveCameraPlane())
        {
            return false;
        }

        CaptureCameraPlane(movementCamera.WorldToViewportPoint(worldPosition));
        return true;
    }

    private void CaptureCameraPlane(Vector3 viewportPoint)
    {
        movementViewportRect = GetEffectiveMovementViewportRect();
        if (playerMoveGuide != null)
        {
            cameraPlaneDepth = Mathf.Max(movementCamera.nearClipPlane + 0.01f, playerMoveGuide.PreviewDepth);
        }
        else
        {
            cameraPlaneDepth = viewportPoint.z > movementCamera.nearClipPlane
                ? viewportPoint.z
                : Mathf.Max(movementCamera.nearClipPlane + 0.01f, cameraPlaneDepth);
        }
        viewportPosition = new Vector2(
            Mathf.Clamp(viewportPoint.x, movementViewportRect.xMin, movementViewportRect.xMax),
            Mathf.Clamp(viewportPoint.y, movementViewportRect.yMin, movementViewportRect.yMax));
        hasCameraPlane = true;
    }

    private bool TryResolveCameraPlane()
    {
        if (!useCameraPlaneMovement)
        {
            return false;
        }

        if (movementCamera == null && playerMoveGuide != null)
        {
            movementCamera = playerMoveGuide.TargetCamera;
        }

        if (movementCamera == null)
        {
            movementCamera = Camera.main;
        }

        if (movementCamera == null)
        {
            return false;
        }

        movementViewportRect = GetEffectiveMovementViewportRect();
        return true;
    }

    private Rect GetEffectiveMovementViewportRect()
    {
        Rect configuredRect = playerMoveGuide != null ? playerMoveGuide.ViewportRect : DefaultViewportRect;
        if (!useScreenSpaceVisual || Screen.width <= 0 || Screen.height <= 0)
        {
            return configuredRect;
        }

        Vector2 visualSize = GetScreenSpaceVisualSize();
        float minX = Mathf.Max(configuredRect.xMin, Mathf.Clamp01((visualSize.x * 0.5f) / Screen.width));
        float maxX = Mathf.Min(configuredRect.xMax, Mathf.Clamp01(1f - (visualSize.x * 0.5f) / Screen.width));
        float minY = Mathf.Max(configuredRect.yMin, Mathf.Clamp01((visualSize.y * 0.5f) / Screen.height));
        float maxY = Mathf.Min(configuredRect.yMax, Mathf.Clamp01(1f - (visualSize.y * 0.5f) / Screen.height));

        if (minX > maxX)
        {
            float centerX = Mathf.Clamp((configuredRect.xMin + configuredRect.xMax) * 0.5f, 0f, 1f);
            minX = centerX;
            maxX = centerX;
        }

        if (minY > maxY)
        {
            float centerY = Mathf.Clamp((configuredRect.yMin + configuredRect.yMax) * 0.5f, 0f, 1f);
            minY = centerY;
            maxY = centerY;
        }

        return Rect.MinMaxRect(minX, minY, maxX, maxY);
    }

    private Vector2 GetScreenSpaceVisualSize()
    {
        Vector2 visualSize = screenSpaceVisualImageSize;
        if (screenSpaceVisualRect != null)
        {
            Vector2 rectSize = screenSpaceVisualRect.sizeDelta;
            if (rectSize.x > 1f && rectSize.y > 1f)
            {
                visualSize = rectSize;
            }
        }

        visualSize.x = Mathf.Max(0f, visualSize.x);
        visualSize.y = Mathf.Max(0f, visualSize.y);
        return visualSize;
    }

    private void UpdateVisualTilt(Vector2 input)
    {
        if (visualTiltRoot == null)
        {
            CacheVisualTiltRoot();
            if (visualTiltRoot == null)
            {
                return;
            }
        }

        if (!enableVisualTilt)
        {
            ResetVisualTiltImmediate();
            return;
        }

        float clampedMaxAngle = Mathf.Max(0f, maxVisualTiltAngle);
        if (clampedMaxAngle <= 0.001f)
        {
            currentVisualTilt = Vector2.zero;
            ApplyLockedVisualPose(Quaternion.identity);
            return;
        }

        float clampedDuration = Mathf.Max(0.01f, visualTiltDuration);
        float tiltSpeed = clampedMaxAngle / clampedDuration;
        Vector2 clampedInput = Vector2.ClampMagnitude(input, 1f);

        // Vertical input pitches the helicopter, horizontal input banks it.
        Vector2 targetTilt = new Vector2(-clampedInput.y, clampedInput.x) * clampedMaxAngle;
        currentVisualTilt = Vector2.MoveTowards(currentVisualTilt, targetTilt, tiltSpeed * Time.deltaTime);
        ApplyLockedVisualPose(Quaternion.Euler(currentVisualTilt.x, 0f, currentVisualTilt.y));
    }

    private Quaternion GetDesiredLookRotation(Vector3 worldPosition)
    {
        Vector3 lookPoint = lookTarget != null ? lookTarget.position : orbitCenter != null ? orbitCenter.position : worldPosition + Vector3.forward;
        Vector3 flatLook = lookPoint - worldPosition;
        flatLook.y = 0f;
        if (flatLook.sqrMagnitude < 0.001f)
        {
            return Quaternion.LookRotation(Vector3.forward, Vector3.up);
        }

        return Quaternion.LookRotation(flatLook.normalized, Vector3.up);
    }

    private void ResolveMovementAxes(out Vector3 right, out Vector3 up, out Vector3 forward)
    {
        if (TryResolveCameraPlane())
        {
            Transform cameraTransform = movementCamera.transform;
            right = cameraTransform.right;
            up = cameraTransform.up;
            forward = cameraTransform.forward;
            return;
        }

        if (movementBounds != null)
        {
            movementBounds.GetAxes(out right, out up, out forward);
            return;
        }

        right = Vector3.right;
        up = Vector3.up;
        forward = Vector3.forward;
    }

    private void EnsureRuntimeDefaults()
    {
        MigrateLegacyMovementSpeedsIfNeeded();
        useScreenSpaceVisual = true;
        lockVisualRootToCamera = true;
        enableVisualTilt = true;
        screenSpaceVisualScaleMultiplier = 0.65f;

        if (strafeSpeed <= 0.01f)
        {
            strafeSpeed = DefaultStrafeSpeed;
        }

        if (altitudeSpeed <= 0.01f)
        {
            altitudeSpeed = DefaultAltitudeSpeed;
        }

        if (forwardSpeed <= 0.01f)
        {
            forwardSpeed = DefaultForwardSpeed;
        }

        if (string.IsNullOrWhiteSpace(screenSpaceVisualLayerName))
        {
            screenSpaceVisualLayerName = "UI";
        }

        if (screenSpaceVisualDepth <= 0.01f)
        {
            screenSpaceVisualDepth = 10f;
        }

        if (screenSpaceVisualScaleMultiplier <= 0.01f)
        {
            screenSpaceVisualScaleMultiplier = 0.65f;
        }

        if (screenSpaceVisualTextureSize < 64)
        {
            screenSpaceVisualTextureSize = 512;
        }

        if (screenSpaceVisualImageSize.x <= 1f || screenSpaceVisualImageSize.y <= 1f)
        {
            screenSpaceVisualImageSize = new Vector2(520f, 360f);
        }

        if (screenSpaceVisualRenderOrthographicSize <= 0.01f)
        {
            screenSpaceVisualRenderOrthographicSize = 0.45f;
        }

        if (screenSpaceVisualFramePadding < 1f)
        {
            screenSpaceVisualFramePadding = 1.15f;
        }

        if (maxVisualTiltAngle <= 0.001f || maxVisualTiltAngle > 18f)
        {
            maxVisualTiltAngle = 12f;
        }

        if (visualTiltDuration <= 0.01f || visualTiltDuration > 0.25f)
        {
            visualTiltDuration = 0.18f;
        }
    }

    private void MigrateLegacyMovementSpeedsIfNeeded()
    {
        if (movementSpeedsMigratedToWorldSpace)
        {
            return;
        }

        // The old camera-viewport controller used sub-1.0 speeds like 0.35.
        // In fixed world-space movement those values feel almost stationary, so
        // upgrade them once to sensible world-space defaults.
        if (strafeSpeed > 0.01f && strafeSpeed < 1f)
        {
            strafeSpeed = DefaultStrafeSpeed;
        }

        if (altitudeSpeed > 0.01f && altitudeSpeed < 1f)
        {
            altitudeSpeed = DefaultAltitudeSpeed;
        }

        if (forwardSpeed > 0.01f && forwardSpeed < 1f)
        {
            forwardSpeed = DefaultForwardSpeed;
        }

        movementSpeedsMigratedToWorldSpace = true;
    }

    private void UpdateWorldVelocity()
    {
        if (!hasPreviousWorldPosition)
        {
            ResetVelocityTracking();
            return;
        }

        float deltaTime = Mathf.Max(Time.deltaTime, 0.0001f);
        CurrentWorldVelocity = (transform.position - previousWorldPosition) / deltaTime;
        previousWorldPosition = transform.position;
    }

    private void ResetVelocityTracking()
    {
        previousWorldPosition = transform.position;
        CurrentWorldVelocity = Vector3.zero;
        hasPreviousWorldPosition = true;
    }
}
