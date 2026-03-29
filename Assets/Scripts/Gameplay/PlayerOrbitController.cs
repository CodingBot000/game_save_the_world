using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

[DefaultExecutionOrder(0)]
public class PlayerOrbitController : MonoBehaviour
{
    private const float DefaultHorizontalScreenSpeed = 0.35f;
    private const float DefaultVerticalScreenSpeed = 0.35f;
    private const float DefaultDepthSpeed = 10f;
    private const float DefaultMinViewportX = 0.12f;
    private const float DefaultMaxViewportX = 0.88f;
    private const float DefaultMinViewportY = 0.16f;
    private const float DefaultMaxViewportY = 0.84f;
    private const float DefaultMinCameraDepth = 6f;
    private const float DefaultMaxCameraDepth = 18f;

    [SerializeField] private bool deriveRotationOffsetFromSceneRotation = true;
    [SerializeField] private float horizontalScreenSpeed = 0.35f;
    [SerializeField] private float verticalScreenSpeed = 0.35f;
    [SerializeField] private float depthSpeed = 10f;
    [SerializeField] private float minViewportX = 0.12f;
    [SerializeField] private float maxViewportX = 0.88f;
    [SerializeField] private float minViewportY = 0.16f;
    [SerializeField] private float maxViewportY = 0.84f;
    [SerializeField] private float minCameraDepth = 6f;
    [SerializeField] private float maxCameraDepth = 18f;
    [SerializeField] private string visualTiltRootName = "PlayerVisualRoot";
    [SerializeField] private float maxVisualTiltAngle = 30f;
    [SerializeField] private float visualTiltDuration = 0.3f;

    private Transform orbitCenter;
    private Transform lookTarget;
    private ArenaCameraRig cameraRig;
    private Camera orbitCamera;
    private PlayerMoveGuide moveGuide;
    private Transform visualTiltRoot;
    private Vector3[] localVisualSamplePoints;
    private bool inputEnabled = true;
    private Quaternion lookRotationOffset = Quaternion.identity;
    private Quaternion visualTiltBaseLocalRotation = Quaternion.identity;
    private Vector2 currentVisualTilt;
    private Vector2 movementInput;
    private float viewportX;
    private float viewportY;
    private float cameraDepth;

    public float CurrentDistance { get; private set; }
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
        CacheVisualTiltRoot();
        CacheVisualSamplePoints();
    }

    private void LateUpdate()
    {
        if (orbitCenter == null || cameraRig == null)
        {
            UpdateVisualTilt(Vector2.zero);
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
        UpdateVisualTilt(movementInput);
    }

    public void Configure(Transform center, Transform targetToLookAt, ArenaCameraRig rig)
    {
        EnsureRuntimeDefaults();
        CacheVisualTiltRoot();
        CacheVisualSamplePoints();
        orbitCenter = center;
        lookTarget = targetToLookAt;
        cameraRig = rig;
        orbitCamera = rig != null ? rig.GetComponent<Camera>() : null;
        moveGuide = rig != null ? rig.GetComponentInChildren<PlayerMoveGuide>(true) : null;
    }

    public void SetInputEnabled(bool enabled)
    {
        inputEnabled = enabled;
    }

    public void AdoptScenePlacement(Vector3 worldPosition)
    {
        if (cameraRig == null)
        {
            transform.position = worldPosition;
            return;
        }

        if (deriveRotationOffsetFromSceneRotation)
        {
            Quaternion desiredLookRotation = GetDesiredLookRotation(worldPosition);
            lookRotationOffset = Quaternion.Inverse(desiredLookRotation) * transform.rotation;
        }

        if (orbitCamera == null)
        {
            transform.position = worldPosition;
            return;
        }

        Vector3 viewport = orbitCamera.WorldToViewportPoint(worldPosition);
        viewportX = viewport.x;
        viewportY = viewport.y;
        cameraDepth = viewport.z;

        ClampToMovementBounds();

        RepositionImmediate();
    }

    public void RepositionImmediate()
    {
        if (orbitCamera == null)
        {
            return;
        }

        Vector3 desiredPosition = GetDesiredPosition(viewportX, viewportY, cameraDepth);
        transform.position = desiredPosition;
        transform.rotation = GetDesiredLookRotation(desiredPosition) * lookRotationOffset;
        CurrentDistance = orbitCenter != null
            ? Vector3.Distance(new Vector3(transform.position.x, 0f, transform.position.z), new Vector3(orbitCenter.position.x, 0f, orbitCenter.position.z))
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

        float depth = 0f;
        if (keyboard != null && keyboard.qKey.isPressed)
        {
            depth += 1f;
        }

        if (keyboard != null && keyboard.zKey.isPressed)
        {
            depth -= 1f;
        }

        if (Mathf.Abs(horizontal) > 0.001f)
        {
            viewportX += horizontal * horizontalScreenSpeed * Time.deltaTime;
        }

        if (Mathf.Abs(altitude) > 0.001f)
        {
            viewportY += altitude * verticalScreenSpeed * Time.deltaTime;
        }

        if (Mathf.Abs(depth) > 0.001f)
        {
            cameraDepth += depth * depthSpeed * Time.deltaTime;
        }

        ClampToMovementBounds();
    }

    private void CacheVisualTiltRoot()
    {
        visualTiltRoot = string.IsNullOrWhiteSpace(visualTiltRootName) ? null : transform.Find(visualTiltRootName);
        visualTiltBaseLocalRotation = visualTiltRoot != null ? visualTiltRoot.localRotation : Quaternion.identity;
        currentVisualTilt = Vector2.zero;
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

        float clampedMaxAngle = Mathf.Max(0f, maxVisualTiltAngle);
        float clampedDuration = Mathf.Max(0.01f, visualTiltDuration);
        float tiltSpeed = clampedMaxAngle / clampedDuration;
        Vector2 clampedInput = Vector2.ClampMagnitude(input, 1f);

        // Vertical input pitches the helicopter, horizontal input banks it.
        Vector2 targetTilt = new Vector2(-clampedInput.y, clampedInput.x) * clampedMaxAngle;
        currentVisualTilt = Vector2.MoveTowards(currentVisualTilt, targetTilt, tiltSpeed * Time.deltaTime);
        visualTiltRoot.localRotation = visualTiltBaseLocalRotation * Quaternion.Euler(currentVisualTilt.x, 0f, currentVisualTilt.y);
    }

    private Vector3 GetDesiredPosition(float proposedViewportX, float proposedViewportY, float proposedCameraDepth)
    {
        return orbitCamera.ViewportToWorldPoint(new Vector3(proposedViewportX, proposedViewportY, proposedCameraDepth));
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

    private void ClampToMovementBounds()
    {
        float localMinViewportX = minViewportX;
        float localMaxViewportX = maxViewportX;
        float localMinViewportY = minViewportY;
        float localMaxViewportY = maxViewportY;
        float localMinCameraDepth = minCameraDepth;
        float localMaxCameraDepth = maxCameraDepth;

        if (moveGuide != null)
        {
            moveGuide.GetMovementBounds(out Rect viewportRect, out float minimumDepth, out float maximumDepth);
            localMinViewportX = viewportRect.xMin;
            localMaxViewportX = viewportRect.xMax;
            localMinViewportY = viewportRect.yMin;
            localMaxViewportY = viewportRect.yMax;
            localMinCameraDepth = minimumDepth;
            localMaxCameraDepth = maximumDepth;
        }

        cameraDepth = Mathf.Clamp(cameraDepth, localMinCameraDepth, localMaxCameraDepth);

        for (int i = 0; i < 2; i++)
        {
            Vector3 worldPosition = GetDesiredPosition(viewportX, viewportY, cameraDepth);
            Quaternion worldRotation = GetDesiredLookRotation(worldPosition) * lookRotationOffset;
            GetViewportPadding(worldPosition, worldRotation, out float leftPadding, out float rightPadding, out float bottomPadding, out float topPadding);

            float paddedMinX = localMinViewportX + leftPadding;
            float paddedMaxX = localMaxViewportX - rightPadding;
            float paddedMinY = localMinViewportY + bottomPadding;
            float paddedMaxY = localMaxViewportY - topPadding;

            if (paddedMaxX < paddedMinX)
            {
                float centerX = (localMinViewportX + localMaxViewportX) * 0.5f;
                paddedMinX = centerX;
                paddedMaxX = centerX;
            }

            if (paddedMaxY < paddedMinY)
            {
                float centerY = (localMinViewportY + localMaxViewportY) * 0.5f;
                paddedMinY = centerY;
                paddedMaxY = centerY;
            }

            viewportX = Mathf.Clamp(viewportX, paddedMinX, paddedMaxX);
            viewportY = Mathf.Clamp(viewportY, paddedMinY, paddedMaxY);
        }
    }

    private void CacheVisualSamplePoints()
    {
        List<Vector3> points = new();
        MeshFilter[] meshFilters = GetComponentsInChildren<MeshFilter>(true);
        for (int i = 0; i < meshFilters.Length; i++)
        {
            MeshFilter meshFilter = meshFilters[i];
            if (meshFilter == null || meshFilter.sharedMesh == null)
            {
                continue;
            }

            Renderer renderer = meshFilter.GetComponent<Renderer>();
            if (renderer == null)
            {
                continue;
            }

            Bounds meshBounds = meshFilter.sharedMesh.bounds;
            Vector3 center = meshBounds.center;
            Vector3 extents = meshBounds.extents;

            Vector3[] samplePoints =
            {
                center,
                center + new Vector3(-extents.x, -extents.y, -extents.z),
                center + new Vector3(-extents.x, -extents.y, extents.z),
                center + new Vector3(-extents.x, extents.y, -extents.z),
                center + new Vector3(-extents.x, extents.y, extents.z),
                center + new Vector3(extents.x, -extents.y, -extents.z),
                center + new Vector3(extents.x, -extents.y, extents.z),
                center + new Vector3(extents.x, extents.y, -extents.z),
                center + new Vector3(extents.x, extents.y, extents.z),
            };

            for (int j = 0; j < samplePoints.Length; j++)
            {
                Vector3 worldPoint = meshFilter.transform.TransformPoint(samplePoints[j]);
                points.Add(transform.InverseTransformPoint(worldPoint));
            }
        }

        if (points.Count == 0)
        {
            points.Add(Vector3.zero);
        }

        localVisualSamplePoints = points.ToArray();
    }

    private void GetViewportPadding(Vector3 worldPosition, Quaternion worldRotation, out float leftPadding, out float rightPadding, out float bottomPadding, out float topPadding)
    {
        leftPadding = 0f;
        rightPadding = 0f;
        bottomPadding = 0f;
        topPadding = 0f;

        if (orbitCamera == null)
        {
            return;
        }

        if (localVisualSamplePoints == null || localVisualSamplePoints.Length == 0)
        {
            return;
        }

        Vector3 pivotViewport = orbitCamera.WorldToViewportPoint(worldPosition);
        Vector3 rootScale = transform.lossyScale;
        Matrix4x4 localToWorld = Matrix4x4.TRS(worldPosition, worldRotation, rootScale);

        float minX = float.PositiveInfinity;
        float maxX = float.NegativeInfinity;
        float minY = float.PositiveInfinity;
        float maxY = float.NegativeInfinity;

        for (int i = 0; i < localVisualSamplePoints.Length; i++)
        {
            Vector3 sampleWorld = localToWorld.MultiplyPoint3x4(localVisualSamplePoints[i]);
            Vector3 sampleViewport = orbitCamera.WorldToViewportPoint(sampleWorld);
            minX = Mathf.Min(minX, sampleViewport.x);
            maxX = Mathf.Max(maxX, sampleViewport.x);
            minY = Mathf.Min(minY, sampleViewport.y);
            maxY = Mathf.Max(maxY, sampleViewport.y);
        }

        leftPadding = Mathf.Max(0f, pivotViewport.x - minX);
        rightPadding = Mathf.Max(0f, maxX - pivotViewport.x);
        bottomPadding = Mathf.Max(0f, pivotViewport.y - minY);
        topPadding = Mathf.Max(0f, maxY - pivotViewport.y);
    }

    private void EnsureRuntimeDefaults()
    {
        if (horizontalScreenSpeed <= 0.01f)
        {
            horizontalScreenSpeed = DefaultHorizontalScreenSpeed;
        }

        if (verticalScreenSpeed <= 0.01f)
        {
            verticalScreenSpeed = DefaultVerticalScreenSpeed;
        }

        if (depthSpeed <= 0.01f)
        {
            depthSpeed = DefaultDepthSpeed;
        }

        if (maxViewportX <= minViewportX + 0.01f)
        {
            minViewportX = DefaultMinViewportX;
            maxViewportX = DefaultMaxViewportX;
        }

        if (maxViewportY <= minViewportY + 0.01f)
        {
            minViewportY = DefaultMinViewportY;
            maxViewportY = DefaultMaxViewportY;
        }

        if (maxCameraDepth <= minCameraDepth + 0.01f)
        {
            minCameraDepth = DefaultMinCameraDepth;
            maxCameraDepth = DefaultMaxCameraDepth;
        }
    }
}
