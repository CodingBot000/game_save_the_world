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
    private readonly HashSet<Transform> externalVisualRoots = new();
    private readonly List<Transform> staleExternalVisualRoots = new();
    private readonly HashSet<Transform> centeringIgnoredRoots = new();
    private readonly List<Transform> staleCenteringIgnoredRoots = new();
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
    private bool stableOverlayProjectionActive;
    private Camera stableOverlayProjectionSource;
    private Matrix4x4 stableOverlayProjection;
    private Vector3 stableOverlayCameraPosition;
    private Quaternion stableOverlayCameraRotation = Quaternion.identity;

    public bool IsConfigured { get; private set; }
    public Camera BaseCamera => baseCamera;
    public Camera OverlayCamera => overlayCamera;
    public Transform VisualRoot => visualRoot;
    public int VisualLayer => visualLayer;
    public int VisualRendererCount { get; private set; }
    public int RendererColliderLayerConflictCount { get; private set; }
    public int CenteringRendererCount { get; private set; }
    public int IgnoredDynamicCenteringRendererCount { get; private set; }
    public int IgnoredAttachmentCenteringRendererCount { get; private set; }
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

    public int ExternalVisualRootCount => externalVisualRoots.Count;
    public bool UsesStableOverlayProjectionDuringCameraShake =>
        stableOverlayProjectionActive;

    public void SetStableOverlayProjectionDuringCameraShake(
        Camera sourceCamera,
        Matrix4x4 projection)
    {
        if (!IsConfigured || sourceCamera == null || sourceCamera != baseCamera)
        {
            return;
        }

        stableOverlayProjectionSource = sourceCamera;
        stableOverlayProjection = projection;
        if (overlayCamera != null)
        {
            stableOverlayCameraPosition = overlayCamera.transform.position;
            stableOverlayCameraRotation = overlayCamera.transform.rotation;
        }
        else
        {
            stableOverlayCameraPosition = sourceCamera.transform.position;
            stableOverlayCameraRotation = sourceCamera.transform.rotation;
        }

        stableOverlayProjectionActive = true;
        if (overlayCamera != null)
        {
            overlayCamera.transform.SetPositionAndRotation(
                stableOverlayCameraPosition,
                stableOverlayCameraRotation);
            overlayCamera.projectionMatrix = stableOverlayProjection;
        }
    }

    public void ClearStableOverlayProjectionDuringCameraShake(Camera sourceCamera)
    {
        if (!stableOverlayProjectionActive ||
            (sourceCamera != null && sourceCamera != stableOverlayProjectionSource))
        {
            return;
        }

        stableOverlayProjectionActive = false;
        stableOverlayProjectionSource = null;
        if (overlayCamera != null && baseCamera != null)
        {
            overlayCamera.transform.SetPositionAndRotation(
                baseCamera.transform.position,
                baseCamera.transform.rotation);
            overlayCamera.projectionMatrix = baseCamera.projectionMatrix;
        }
    }

    /// <summary>
    /// Keeps a temporarily detached part of the player model in the player-only
    /// overlay camera. Detached Sidewinders use this so buildings never cover the
    /// cosmetic flight while the helicopter body continues to define centering.
    /// </summary>
    public void RegisterExternalVisualRoot(Transform root)
    {
        if (root == null || !externalVisualRoots.Add(root))
        {
            return;
        }

        RefreshVisualContentLayers();
    }

    public void UnregisterExternalVisualRoot(Transform root)
    {
        if (root == null || !externalVisualRoots.Remove(root))
        {
            return;
        }

        RefreshVisualContentLayers();
    }

    /// <summary>
    /// Keeps a detachable visual attachment from changing the body center when it
    /// leaves or returns to the helicopter hierarchy.
    /// </summary>
    public void RegisterCenteringIgnoredRoot(Transform root)
    {
        if (root != null)
        {
            centeringIgnoredRoots.Add(root);
        }
    }

    public void UnregisterCenteringIgnoredRoot(Transform root)
    {
        if (root != null)
        {
            centeringIgnoredRoots.Remove(root);
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

        SyncOverlayCameraPose();
        overlayCamera.projectionMatrix = stableOverlayProjectionActive
            ? stableOverlayProjection
            : baseCamera.projectionMatrix;
        AlignVisualCenterToOwner();

        baseCamera.cullingMask &= ~visualLayerMask;
        EnsureCameraStackRegistration();
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
        overlayCamera.enabled = true;
    }

    private void SyncOverlayCameraPose()
    {
        if (overlayCamera == null)
        {
            return;
        }

        if (stableOverlayProjectionActive)
        {
            overlayCamera.transform.SetPositionAndRotation(
                stableOverlayCameraPosition,
                stableOverlayCameraRotation);
            return;
        }

        if (baseCamera != null)
        {
            overlayCamera.transform.SetPositionAndRotation(
                baseCamera.transform.position,
                baseCamera.transform.rotation);
        }
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
        return TryGetVisualViewportRect(baseCamera, out viewportRect);
    }

    /// <summary>
    /// Returns the helicopter rectangle through the camera that actually renders it.
    /// During world-camera shake this camera keeps the pre-shake projection, so this
    /// value can be used to verify that the helicopter itself remains stationary.
    /// </summary>
    public bool TryGetRenderedVisualViewportRect(out Rect viewportRect)
    {
        return TryGetVisualViewportRect(overlayCamera, out viewportRect);
    }

    private bool TryGetVisualViewportRect(Camera projectionCamera, out Rect viewportRect)
    {
        viewportRect = default;
        if (projectionCamera == null || visualRoot == null ||
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
            Vector3 viewport = projectionCamera.WorldToViewportPoint(corner);
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
        CenteringRendererCount = 0;
        IgnoredDynamicCenteringRendererCount = 0;
        IgnoredAttachmentCenteringRendererCount = 0;
        LastAlignmentWorldOffset = Vector3.zero;
        centerVisualOnOwner = false;
        nextVisualContentRefreshTime = 0f;
        stableOverlayProjectionActive = false;
        stableOverlayProjectionSource = null;
        stableOverlayProjection = Matrix4x4.identity;
        stableOverlayCameraPosition = Vector3.zero;
        stableOverlayCameraRotation = Quaternion.identity;
        externalVisualRoots.Clear();
        staleExternalVisualRoots.Clear();
        centeringIgnoredRoots.Clear();
        staleCenteringIgnoredRoots.Clear();
    }

    private void AlignVisualCenterToOwner()
    {
        LastAlignmentWorldOffset = Vector3.zero;
        if (!centerVisualOnOwner || baseCamera == null || visualRoot == null)
        {
            return;
        }

        Camera alignmentCamera = stableOverlayProjectionActive && overlayCamera != null
            ? overlayCamera
            : baseCamera;
        Vector3 ownerViewport = alignmentCamera.WorldToViewportPoint(transform.position);
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
            if (!TryGetVisualViewportRect(alignmentCamera, out Rect visualViewportRect))
            {
                return;
            }

            Vector2 viewportDelta =
                new Vector2(ownerViewport.x, ownerViewport.y) - visualViewportRect.center;
            if (viewportDelta.sqrMagnitude <= 0.00000001f)
            {
                break;
            }

            Vector3 currentCenterAtOwnerDepth = alignmentCamera.ViewportToWorldPoint(
                new Vector3(visualViewportRect.center.x, visualViewportRect.center.y, ownerViewport.z));
            Vector3 desiredCenterAtOwnerDepth = alignmentCamera.ViewportToWorldPoint(ownerViewport);
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

        HashSet<GameObject> countedConflicts = new();
        CollectVisualRenderers(visualRoot, countedConflicts);

        staleExternalVisualRoots.Clear();
        foreach (Transform externalRoot in externalVisualRoots)
        {
            if (externalRoot == null)
            {
                staleExternalVisualRoots.Add(externalRoot);
                continue;
            }

            CollectVisualRenderers(externalRoot, countedConflicts);
        }

        for (int i = 0; i < staleExternalVisualRoots.Count; i++)
        {
            externalVisualRoots.Remove(staleExternalVisualRoots[i]);
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

    private void CollectVisualRenderers(
        Transform root,
        HashSet<GameObject> countedConflicts)
    {
        if (root == null)
        {
            return;
        }

        Renderer[] renderers = root.GetComponentsInChildren<Renderer>(true);
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

    private bool TryGetRendererBounds(Transform root, out Bounds bounds)
    {
        bounds = default;
        CenteringRendererCount = 0;
        IgnoredDynamicCenteringRendererCount = 0;
        IgnoredAttachmentCenteringRendererCount = 0;
        if (root == null)
        {
            return false;
        }

        Renderer[] renderers = root.GetComponentsInChildren<Renderer>(true);
        PruneCenteringIgnoredRoots();
        bool hasBounds = false;
        for (int i = 0; i < renderers.Length; i++)
        {
            Renderer renderer = renderers[i];
            if (renderer == null || !renderer.enabled || !renderer.gameObject.activeInHierarchy ||
                IsUnderDamageHurtbox(renderer.transform))
            {
                continue;
            }

            // Particle/trail bounds expand and move every frame. They must still be
            // rendered by the player overlay, but letting them define the helicopter
            // center would move the visual root as Sidewinder exhaust grows.
            if (!CanDefineVisualCenter(renderer))
            {
                IgnoredDynamicCenteringRendererCount++;
                continue;
            }

            if (IsUnderCenteringIgnoredRoot(renderer.transform))
            {
                IgnoredAttachmentCenteringRendererCount++;
                continue;
            }

            CenteringRendererCount++;

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

    private void PruneCenteringIgnoredRoots()
    {
        staleCenteringIgnoredRoots.Clear();
        foreach (Transform ignoredRoot in centeringIgnoredRoots)
        {
            if (ignoredRoot == null)
            {
                staleCenteringIgnoredRoots.Add(ignoredRoot);
            }
        }

        for (int i = 0; i < staleCenteringIgnoredRoots.Count; i++)
        {
            centeringIgnoredRoots.Remove(staleCenteringIgnoredRoots[i]);
        }
    }

    private bool IsUnderCenteringIgnoredRoot(Transform target)
    {
        if (target == null)
        {
            return false;
        }

        foreach (Transform ignoredRoot in centeringIgnoredRoots)
        {
            if (ignoredRoot != null &&
                (target == ignoredRoot || target.IsChildOf(ignoredRoot)))
            {
                return true;
            }
        }

        return false;
    }

    private static bool CanDefineVisualCenter(Renderer renderer)
    {
        return renderer != null &&
               !(renderer is ParticleSystemRenderer) &&
               !(renderer is TrailRenderer) &&
               !(renderer is LineRenderer);
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
