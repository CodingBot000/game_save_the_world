using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering.Universal;

[DisallowMultipleComponent]
public sealed class PlayerVisualOverlayRenderer : MonoBehaviour
{
    public const string OverlayCameraName = "PlayerVisualOverlayCamera";
    private const string DamageHurtboxName = "CrashObserver";
    private const float VisualContentRefreshInterval = 0.25f;

    private readonly Dictionary<GameObject, int> originalVisualLayers = new();
    private readonly HashSet<GameObject> currentVisualObjects = new();
    private readonly List<GameObject> staleVisualObjects = new();
    private Camera baseCamera;
    private Camera overlayCamera;
    private UniversalAdditionalCameraData baseCameraData;
    private UniversalAdditionalCameraData overlayCameraData;
    private Transform visualRoot;
    private int visualLayer = -1;
    private int visualLayerMask;
    private bool baseCameraOriginallyRenderedVisualLayer;
    private bool baseMaskCaptured;
    private Vector3 visualRootOriginalLocalPosition;
    private bool visualRootPositionCaptured;
    private bool centerVisualOnOwner;
    private float nextVisualContentRefreshTime;

    public bool IsConfigured { get; private set; }
    public Camera BaseCamera => baseCamera;
    public Camera OverlayCamera => overlayCamera;
    public Transform VisualRoot => visualRoot;
    public int VisualLayer => visualLayer;
    public int VisualRendererCount { get; private set; }
    public int RendererColliderLayerConflictCount { get; private set; }
    public bool CentersVisualOnOwner => centerVisualOnOwner;
    public Vector3 LastAlignmentWorldOffset { get; private set; }
    public bool BaseCameraExcludesVisualLayer =>
        baseCamera != null && visualLayerMask != 0 &&
        (baseCamera.cullingMask & visualLayerMask) == 0;
    public bool OverlayClearsDepth => overlayCameraData != null && overlayCameraData.clearDepth;
    public bool BaseRendererSupportsCameraStacking =>
        baseCameraData != null &&
        baseCameraData.scriptableRenderer != null &&
        baseCameraData.scriptableRenderer.SupportsCameraStackingType(CameraRenderType.Base);
    public bool OverlayRendererSupportsCameraStacking =>
        overlayCameraData != null &&
        overlayCameraData.scriptableRenderer != null &&
        overlayCameraData.scriptableRenderer.SupportsCameraStackingType(CameraRenderType.Overlay);
    public bool IsInBaseCameraStack
    {
        get
        {
            List<Camera> stack = baseCameraData != null ? baseCameraData.cameraStack : null;
            return stack != null && overlayCamera != null && stack.Contains(overlayCamera);
        }
    }

    public bool Configure(
        Camera targetBaseCamera,
        Transform targetVisualRoot,
        string layerName,
        bool centerOnOwner)
    {
        int targetLayer = LayerMask.NameToLayer(layerName);
        if (targetBaseCamera == null || targetVisualRoot == null || targetLayer < 0)
        {
            Shutdown();
            Debug.LogError(
                $"Player visual overlay configuration failed. BaseCamera={targetBaseCamera != null}, " +
                $"VisualRoot={targetVisualRoot != null}, Layer='{layerName}'({targetLayer}).",
                this);
            return false;
        }

        if (IsConfigured && baseCamera == targetBaseCamera && visualRoot == targetVisualRoot &&
            visualLayer == targetLayer &&
            centerVisualOnOwner == centerOnOwner &&
            overlayCamera != null)
        {
            RefreshVisualContentLayers();
            SyncNow();
            return true;
        }

        Shutdown();
        baseCamera = targetBaseCamera;
        visualRoot = targetVisualRoot;
        centerVisualOnOwner = centerOnOwner;
        visualRootOriginalLocalPosition = visualRoot.localPosition;
        visualRootPositionCaptured = true;
        visualLayer = targetLayer;
        visualLayerMask = 1 << visualLayer;
        baseCameraOriginallyRenderedVisualLayer =
            (baseCamera.cullingMask & visualLayerMask) != 0;
        baseMaskCaptured = true;
        baseCamera.cullingMask &= ~visualLayerMask;

        if (!EnsureOverlayCamera())
        {
            Shutdown();
            return false;
        }

        RefreshVisualContentLayers();
        IsConfigured = VisualRendererCount > 0 &&
                       RendererColliderLayerConflictCount == 0 &&
                       IsInBaseCameraStack;
        if (!IsConfigured)
        {
            Debug.LogError(
                $"Player visual overlay could not become active. Renderers={VisualRendererCount}, " +
                $"RendererColliderConflicts={RendererColliderLayerConflictCount}, " +
                $"CameraStack={IsInBaseCameraStack}.",
                this);
            Shutdown();
            return false;
        }

        SyncNow();
        return true;
    }

    public void SyncNow()
    {
        if (!IsConfigured || baseCamera == null || overlayCamera == null || visualRoot == null)
        {
            return;
        }

        if (Time.unscaledTime >= nextVisualContentRefreshTime)
        {
            nextVisualContentRefreshTime = Time.unscaledTime + VisualContentRefreshInterval;
            RefreshVisualContentLayers();
        }

        AlignVisualCenterToOwner();

        baseCamera.cullingMask &= ~visualLayerMask;
        EnsureCameraStackRegistration();
        Transform baseTransform = baseCamera.transform;
        overlayCamera.transform.SetPositionAndRotation(baseTransform.position, baseTransform.rotation);
        overlayCamera.transform.localScale = Vector3.one;
        overlayCamera.orthographic = baseCamera.orthographic;
        overlayCamera.orthographicSize = baseCamera.orthographicSize;
        overlayCamera.fieldOfView = baseCamera.fieldOfView;
        overlayCamera.nearClipPlane = baseCamera.nearClipPlane;
        overlayCamera.farClipPlane = baseCamera.farClipPlane;
        overlayCamera.aspect = baseCamera.aspect;
        overlayCamera.rect = baseCamera.rect;
        overlayCamera.targetDisplay = baseCamera.targetDisplay;
        overlayCamera.allowHDR = baseCamera.allowHDR;
        overlayCamera.allowMSAA = baseCamera.allowMSAA;
        overlayCamera.allowDynamicResolution = baseCamera.allowDynamicResolution;
        overlayCamera.useOcclusionCulling = false;
        overlayCamera.cullingMask = visualLayerMask;
        overlayCamera.depth = baseCamera.depth + 1f;
        overlayCamera.projectionMatrix = baseCamera.projectionMatrix;
        overlayCamera.enabled = true;
    }

    public bool TryGetVisualViewportExtents(
        out float left,
        out float right,
        out float bottom,
        out float top)
    {
        left = 0f;
        right = 0f;
        bottom = 0f;
        top = 0f;
        if (!IsConfigured || baseCamera == null || visualRoot == null ||
            !TryGetRendererBounds(visualRoot, out Bounds bounds))
        {
            return false;
        }

        Vector3 pivotViewport = baseCamera.WorldToViewportPoint(transform.position);
        if (pivotViewport.z <= 0f)
        {
            return false;
        }

        Vector3 min = bounds.min;
        Vector3 max = bounds.max;
        float minX = float.PositiveInfinity;
        float minY = float.PositiveInfinity;
        float maxX = float.NegativeInfinity;
        float maxY = float.NegativeInfinity;
        for (int cornerIndex = 0; cornerIndex < 8; cornerIndex++)
        {
            Vector3 corner = new(
                (cornerIndex & 1) == 0 ? min.x : max.x,
                (cornerIndex & 2) == 0 ? min.y : max.y,
                (cornerIndex & 4) == 0 ? min.z : max.z);
            Vector3 viewport = baseCamera.WorldToViewportPoint(corner);
            if (viewport.z <= 0f)
            {
                continue;
            }

            minX = Mathf.Min(minX, viewport.x);
            minY = Mathf.Min(minY, viewport.y);
            maxX = Mathf.Max(maxX, viewport.x);
            maxY = Mathf.Max(maxY, viewport.y);
        }

        if (float.IsInfinity(minX) || float.IsInfinity(minY) ||
            float.IsInfinity(maxX) || float.IsInfinity(maxY))
        {
            return false;
        }

        left = Mathf.Max(0f, pivotViewport.x - minX);
        right = Mathf.Max(0f, maxX - pivotViewport.x);
        bottom = Mathf.Max(0f, pivotViewport.y - minY);
        top = Mathf.Max(0f, maxY - pivotViewport.y);
        return true;
    }

    public bool TryGetVisualViewportRect(out Rect viewportRect)
    {
        viewportRect = default;
        if (baseCamera == null || visualRoot == null ||
            !TryGetRendererBounds(visualRoot, out Bounds bounds))
        {
            return false;
        }

        Vector3 min = bounds.min;
        Vector3 max = bounds.max;
        float minX = float.PositiveInfinity;
        float minY = float.PositiveInfinity;
        float maxX = float.NegativeInfinity;
        float maxY = float.NegativeInfinity;
        for (int cornerIndex = 0; cornerIndex < 8; cornerIndex++)
        {
            Vector3 corner = new(
                (cornerIndex & 1) == 0 ? min.x : max.x,
                (cornerIndex & 2) == 0 ? min.y : max.y,
                (cornerIndex & 4) == 0 ? min.z : max.z);
            Vector3 viewport = baseCamera.WorldToViewportPoint(corner);
            if (viewport.z <= 0f)
            {
                continue;
            }

            minX = Mathf.Min(minX, viewport.x);
            minY = Mathf.Min(minY, viewport.y);
            maxX = Mathf.Max(maxX, viewport.x);
            maxY = Mathf.Max(maxY, viewport.y);
        }

        if (float.IsInfinity(minX) || float.IsInfinity(minY) ||
            float.IsInfinity(maxX) || float.IsInfinity(maxY))
        {
            return false;
        }

        viewportRect = Rect.MinMaxRect(minX, minY, maxX, maxY);
        return true;
    }

    public void Shutdown()
    {
        IsConfigured = false;
        RemoveCameraStackRegistration();
        RestoreBaseCameraMask();
        RestoreVisualLayers();
        RestoreVisualRootPosition();
        if (overlayCamera != null)
        {
            overlayCamera.enabled = false;
            GameObject cameraObject = overlayCamera.gameObject;
            overlayCamera = null;
            overlayCameraData = null;
            if (Application.isPlaying)
            {
                Destroy(cameraObject);
            }
            else
            {
                DestroyImmediate(cameraObject);
            }
        }

        baseCamera = null;
        baseCameraData = null;
        visualRoot = null;
        visualLayer = -1;
        visualLayerMask = 0;
        VisualRendererCount = 0;
        RendererColliderLayerConflictCount = 0;
        LastAlignmentWorldOffset = Vector3.zero;
        centerVisualOnOwner = false;
        nextVisualContentRefreshTime = 0f;
    }

    private void AlignVisualCenterToOwner()
    {
        LastAlignmentWorldOffset = Vector3.zero;
        if (!centerVisualOnOwner || baseCamera == null || visualRoot == null)
        {
            return;
        }

        Vector3 ownerViewport = baseCamera.WorldToViewportPoint(transform.position);
        if (ownerViewport.z <= 0f)
        {
            return;
        }

        // A rotated or enlarged 3D model can have a projected screen-rectangle center
        // that differs from Renderer.bounds.center because its corners have different
        // camera depths. Align the actual projected rectangle so visual scaling keeps
        // the helicopter centered on the gameplay movement anchor.
        for (int pass = 0; pass < 2; pass++)
        {
            if (!TryGetVisualViewportRect(out Rect visualViewportRect))
            {
                return;
            }

            Vector2 viewportDelta =
                new Vector2(ownerViewport.x, ownerViewport.y) - visualViewportRect.center;
            if (viewportDelta.sqrMagnitude <= 0.00000001f)
            {
                break;
            }

            Vector3 currentCenterAtOwnerDepth = baseCamera.ViewportToWorldPoint(
                new Vector3(visualViewportRect.center.x, visualViewportRect.center.y, ownerViewport.z));
            Vector3 desiredCenterAtOwnerDepth = baseCamera.ViewportToWorldPoint(ownerViewport);
            Vector3 offset = desiredCenterAtOwnerDepth - currentCenterAtOwnerDepth;
            visualRoot.position += offset;
            LastAlignmentWorldOffset += offset;
        }
    }

    private void RestoreVisualRootPosition()
    {
        if (visualRootPositionCaptured && visualRoot != null)
        {
            visualRoot.localPosition = visualRootOriginalLocalPosition;
        }

        visualRootPositionCaptured = false;
    }

    private bool EnsureOverlayCamera()
    {
        if (baseCamera == null)
        {
            return false;
        }

        baseCameraData = baseCamera.GetComponent<UniversalAdditionalCameraData>() ??
                         baseCamera.gameObject.AddComponent<UniversalAdditionalCameraData>();
        if (baseCameraData.renderType != CameraRenderType.Base)
        {
            Debug.LogError(
                $"Player visual overlay requires a URP Base camera. " +
                $"Camera={baseCamera.name}, Type={baseCameraData.renderType}.",
                baseCamera);
            return false;
        }

        if (overlayCamera == null)
        {
            GameObject cameraObject = new(OverlayCameraName);
            overlayCamera = cameraObject.AddComponent<Camera>();
            overlayCameraData = cameraObject.AddComponent<UniversalAdditionalCameraData>();
        }

        overlayCamera.name = OverlayCameraName;
        overlayCamera.tag = "Untagged";
        overlayCamera.clearFlags = CameraClearFlags.Depth;
        overlayCamera.backgroundColor = Color.clear;
        overlayCamera.cullingMask = visualLayerMask;
        overlayCameraData ??= overlayCamera.GetComponent<UniversalAdditionalCameraData>();
        overlayCameraData.renderType = CameraRenderType.Overlay;
        overlayCameraData.renderPostProcessing = false;
        overlayCameraData.requiresColorTexture = false;
        overlayCameraData.requiresDepthTexture = false;
        overlayCameraData.renderShadows = true;
        EnsureCameraStackRegistration();
        return IsInBaseCameraStack &&
               overlayCameraData.clearDepth &&
               BaseRendererSupportsCameraStacking &&
               OverlayRendererSupportsCameraStacking;
    }

    private void EnsureCameraStackRegistration()
    {
        if (baseCameraData == null || overlayCamera == null)
        {
            return;
        }

        List<Camera> stack = baseCameraData.cameraStack;
        if (stack == null)
        {
            return;
        }

        for (int i = stack.Count - 1; i >= 0; i--)
        {
            Camera stackedCamera = stack[i];
            if (stackedCamera == null ||
                (stackedCamera != overlayCamera && stackedCamera.name == OverlayCameraName))
            {
                stack.RemoveAt(i);
            }
        }

        if (!stack.Contains(overlayCamera))
        {
            stack.Add(overlayCamera);
        }
    }

    private void RemoveCameraStackRegistration()
    {
        if (baseCameraData == null || overlayCamera == null)
        {
            return;
        }

        List<Camera> stack = baseCameraData.cameraStack;
        stack?.Remove(overlayCamera);
    }

    private void RefreshVisualContentLayers()
    {
        VisualRendererCount = 0;
        RendererColliderLayerConflictCount = 0;
        currentVisualObjects.Clear();
        staleVisualObjects.Clear();
        if (visualRoot == null || visualLayer < 0)
        {
            return;
        }

        Renderer[] renderers = visualRoot.GetComponentsInChildren<Renderer>(true);
        HashSet<GameObject> countedConflicts = new();
        for (int i = 0; i < renderers.Length; i++)
        {
            Renderer renderer = renderers[i];
            if (renderer == null || IsUnderDamageHurtbox(renderer.transform))
            {
                continue;
            }

            GameObject rendererObject = renderer.gameObject;
            if (rendererObject.GetComponent<Collider>() != null)
            {
                if (countedConflicts.Add(rendererObject))
                {
                    RendererColliderLayerConflictCount++;
                }

                continue;
            }

            currentVisualObjects.Add(rendererObject);
            if (!originalVisualLayers.ContainsKey(rendererObject))
            {
                originalVisualLayers.Add(rendererObject, rendererObject.layer);
            }

            rendererObject.layer = visualLayer;
            VisualRendererCount++;
        }

        foreach (KeyValuePair<GameObject, int> pair in originalVisualLayers)
        {
            if (pair.Key != null && currentVisualObjects.Contains(pair.Key))
            {
                continue;
            }

            if (pair.Key != null)
            {
                pair.Key.layer = pair.Value;
            }

            staleVisualObjects.Add(pair.Key);
        }

        for (int i = 0; i < staleVisualObjects.Count; i++)
        {
            originalVisualLayers.Remove(staleVisualObjects[i]);
        }
    }

    private void RestoreVisualLayers()
    {
        foreach (KeyValuePair<GameObject, int> pair in originalVisualLayers)
        {
            if (pair.Key != null)
            {
                pair.Key.layer = pair.Value;
            }
        }

        originalVisualLayers.Clear();
    }

    private void RestoreBaseCameraMask()
    {
        if (!baseMaskCaptured || baseCamera == null || visualLayerMask == 0)
        {
            baseMaskCaptured = false;
            return;
        }

        if (baseCameraOriginallyRenderedVisualLayer)
        {
            baseCamera.cullingMask |= visualLayerMask;
        }
        else
        {
            baseCamera.cullingMask &= ~visualLayerMask;
        }

        baseMaskCaptured = false;
    }

    private static bool TryGetRendererBounds(Transform root, out Bounds bounds)
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
            if (renderer == null || !renderer.enabled || !renderer.gameObject.activeInHierarchy ||
                IsUnderDamageHurtbox(renderer.transform))
            {
                continue;
            }

            if (!hasBounds)
            {
                bounds = renderer.bounds;
                hasBounds = true;
            }
            else
            {
                bounds.Encapsulate(renderer.bounds);
            }
        }

        return hasBounds;
    }

    private static bool IsUnderDamageHurtbox(Transform target)
    {
        Transform current = target;
        while (current != null)
        {
            if (current.name == DamageHurtboxName)
            {
                return true;
            }

            current = current.parent;
        }

        return false;
    }

    private void OnDisable()
    {
        Shutdown();
    }

    private void OnDestroy()
    {
        Shutdown();
    }
}
