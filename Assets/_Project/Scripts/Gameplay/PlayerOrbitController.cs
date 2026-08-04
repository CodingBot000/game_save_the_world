using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Serialization;

[DefaultExecutionOrder(0)]
public class PlayerOrbitController : MonoBehaviour
{
    private const string DamageHurtboxName = "CrashObserver";
    private const float PreviousDefaultAxisMovementSpeed = 8f;
    private const float DefaultStrafeSpeed = 7.2f;
    private const float DefaultAltitudeSpeed = 7.2f;
    private const float DefaultForwardSpeed = 10f;
    private const float DefaultLockOnChargingMovementSpeedMultiplier = 0.6f;
    private const float DefaultFullSalvoVisualRestoreDelay = 1f;
    private static readonly Rect DefaultViewportRect = Rect.MinMaxRect(0f, 0f, 1f, 1f);

    [FormerlySerializedAs("horizontalScreenSpeed")]
    [SerializeField] private float strafeSpeed = 7.2f;
    [FormerlySerializedAs("verticalScreenSpeed")]
    [SerializeField] private float altitudeSpeed = 7.2f;
    [FormerlySerializedAs("depthSpeed")]
    [SerializeField] private float forwardSpeed = 10f;
    [Tooltip("Movement multiplier used only while lock-on is actively charging.")]
    [SerializeField, Range(0.01f, 1f)]
    private float lockOnChargingMovementSpeedMultiplier = DefaultLockOnChargingMovementSpeedMultiplier;
    [Tooltip("One-time guard for upgrading old viewport-based movement speeds to world-space speeds.")]
    [SerializeField] private bool movementSpeedsMigratedToWorldSpace;
    [Tooltip("Moves the helicopter on a fixed camera-depth plane so it behaves like a 2D screen object.")]
    [SerializeField] private bool useCameraPlaneMovement = true;
    [SerializeField] private PlayerMovementBounds movementBounds;
    [SerializeField] private PlayerMoveGuide playerMoveGuide;
    [Tooltip("Starts the helicopter at a normalized camera viewport position instead of the authored world position.")]
    [SerializeField] private bool useInitialViewportPlacement = true;
    [SerializeField] private Vector2 initialViewportPosition = new(0.28f, 0.5f);
    [SerializeField] private string visualTiltRootName = "PlayerVisualRoot";
    [Tooltip("Keeps the visual helicopter pose independent from the movement anchor rotation.")]
    [SerializeField] private bool lockVisualRootToCamera = true;
    [SerializeField] private bool centerOriginalVisualOnMovementAnchor = true;
    [SerializeField] private string playerVisualLayerName = "PlayerVisual";
    [Tooltip("Initializes the movement anchor to the actual gameplay camera viewport once per visual setup.")]
    [SerializeField] private bool fitMovementToFullGameplayViewport = true;
    [SerializeField] private Vector2 gameplayViewportEdgePaddingPixels = Vector2.zero;
    [Tooltip("Tilts only the visible helicopter model toward movement input; the movement anchor stays locked to the 2D plane.")]
    [SerializeField] private bool enableVisualTilt = true;
    [SerializeField] private float maxVisualTiltAngle = 12f;
    [SerializeField] private float visualTiltDuration = 0.18f;
    [SerializeField] private Vector3 cinematicRearViewEulerOffset = Vector3.zero;
    [SerializeField] private Vector3 cinematicFrontViewEulerOffset = Vector3.zero;
    [Tooltip("Keeps the helicopter facing the gameplay camera for this long after a full lock-on salvo finishes launching.")]
    [SerializeField, Min(0f)]
    private float fullSalvoVisualRestoreDelay = DefaultFullSalvoVisualRestoreDelay;

    private Transform orbitCenter;
    private Transform lookTarget;
    private Transform visualTiltRoot;
    private Transform visualPoseRoot;
    private PlayerVisualOverlayRenderer playerVisualOverlayRenderer;
    private PlayerLockOnController playerLockOnController;
    private bool inputEnabled = true;
    private Quaternion sceneBaseRotation = Quaternion.identity;
    private Quaternion visualTiltBaseLocalRotation = Quaternion.identity;
    private Quaternion lockedVisualWorldRotation = Quaternion.identity;
    private Quaternion lockedVisualCameraRelativeRotation = Quaternion.identity;
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
    private bool hasPreviousWorldPosition;
    private bool cinematicVisualOverrideActive;
    private bool movementViewportInitializationEnabled;
    private bool movementViewportInitializedForDisplay;
    private Vector2Int initializedGameplayPixelSize;
    private float initializedGameplayAspect;
    private Quaternion cinematicVisualDisplayRotation = Quaternion.identity;
    private Quaternion cinematicReturnDisplayRotation = Quaternion.identity;
    private Coroutine airPressureImpulseRoutine;
    private Coroutine airPressureRollRoutine;
    private Coroutine fullSalvoVisualRestoreRoutine;
    private float airPressureRollDegrees;
    private float airPressureSwayDegrees;
    private int fullSalvoVisualSalvoId;
    private bool fullSalvoFrontViewActive;

    public float CurrentDistance { get; private set; }
    public Vector3 CurrentWorldVelocity { get; private set; }
    public float DebugStrafeSpeed => strafeSpeed;
    public float DebugAltitudeSpeed => altitudeSpeed;
    public float DebugForwardSpeed => forwardSpeed;
    public float DebugMovementSpeedMultiplier => ResolveMovementSpeedMultiplier();
    public float DebugEffectiveStrafeSpeed => strafeSpeed * ResolveMovementSpeedMultiplier();
    public float DebugEffectiveAltitudeSpeed => altitudeSpeed * ResolveMovementSpeedMultiplier();
    public float DebugMaxVisualTiltAngle => maxVisualTiltAngle;
    public float DebugVisualTiltDuration => visualTiltDuration;
    public float DebugFullSalvoVisualRestoreDelay => fullSalvoVisualRestoreDelay;
    public bool IsFullSalvoFrontViewActive => fullSalvoFrontViewActive;
    public int FullSalvoVisualSalvoId => fullSalvoVisualSalvoId;
    public bool IsAirPressureRotationActive =>
        airPressureRollRoutine != null ||
        Mathf.Abs(airPressureRollDegrees) > 0.001f ||
        Mathf.Abs(airPressureSwayDegrees) > 0.001f;
    public Vector3 OrbitCenterPosition => orbitCenter != null ? orbitCenter.position : Vector3.zero;
    public bool IsUsingOriginalVisualOverlay =>
        playerVisualOverlayRenderer != null &&
        playerVisualOverlayRenderer.IsConfigured;
    public PlayerVisualOverlayRenderer OriginalVisualOverlayRenderer => playerVisualOverlayRenderer;
    public Rect DebugMovementViewportRect => GetEffectiveMovementViewportRect();
    public bool IsMovementViewportInitializedForDisplay => movementViewportInitializedForDisplay;
    public Vector2Int InitializedGameplayPixelSize => initializedGameplayPixelSize;
    public float InitializedGameplayAspect => initializedGameplayAspect;
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

    private void OnEnable()
    {
        SubscribeLockOnController();
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
        EnsurePlayerVisualOutputReady();
        UpdateVisualTilt(movementInput);
        playerVisualOverlayRenderer?.SyncNow();
        UpdateWorldVelocity();
    }

    private void OnDisable()
    {
        UnsubscribeLockOnController();
        ResetFullSalvoVisualImmediate();
    }

    private void OnDestroy()
    {
        UnsubscribeLockOnController();
        ResetFullSalvoVisualImmediate();
        playerVisualOverlayRenderer?.Shutdown();
    }

    private void OnValidate()
    {
        initialViewportPosition.x = Mathf.Clamp01(initialViewportPosition.x);
        initialViewportPosition.y = Mathf.Clamp01(initialViewportPosition.y);
        gameplayViewportEdgePaddingPixels.x = Mathf.Max(0f, gameplayViewportEdgePaddingPixels.x);
        gameplayViewportEdgePaddingPixels.y = Mathf.Max(0f, gameplayViewportEdgePaddingPixels.y);
        lockOnChargingMovementSpeedMultiplier = Mathf.Clamp(
            lockOnChargingMovementSpeedMultiplier,
            0.01f,
            1f);
        if (string.IsNullOrWhiteSpace(playerVisualLayerName))
        {
            playerVisualLayerName = "PlayerVisual";
        }
    }

    public void Configure(
        Transform center,
        Transform targetToLookAt,
        PlayerMovementBounds bounds,
        PlayerMoveGuide moveGuide = null,
        PlayerLockOnController lockOnController = null)
    {
        UnsubscribeLockOnController();
        ResetFullSalvoVisualImmediate();
        EnsureRuntimeDefaults();
        orbitCenter = center;
        lookTarget = targetToLookAt;
        movementBounds = bounds;
        playerMoveGuide = moveGuide;
        playerLockOnController = lockOnController;
        SubscribeLockOnController();
        ResetMovementViewportInitialization();
        movementViewportInitializationEnabled = true;
        CaptureRootRotation(transform.position);
        CaptureMovementPlane(transform.position);
        CacheVisualTiltRoot();
        ResetVisualTiltImmediate();
        ResetVelocityTracking();
    }

    private void SubscribeLockOnController()
    {
        if (playerLockOnController == null || !isActiveAndEnabled)
        {
            return;
        }

        playerLockOnController.OnFullSalvoStarting -= HandleFullSalvoStarting;
        playerLockOnController.OnLockOnSalvoFinished -= HandleLockOnSalvoFinished;
        playerLockOnController.OnFullSalvoStarting += HandleFullSalvoStarting;
        playerLockOnController.OnLockOnSalvoFinished += HandleLockOnSalvoFinished;
    }

    private void UnsubscribeLockOnController()
    {
        if (playerLockOnController == null)
        {
            return;
        }

        playerLockOnController.OnFullSalvoStarting -= HandleFullSalvoStarting;
        playerLockOnController.OnLockOnSalvoFinished -= HandleLockOnSalvoFinished;
    }

    private void HandleFullSalvoStarting(int salvoId)
    {
        if (salvoId <= 0)
        {
            return;
        }

        if (fullSalvoVisualRestoreRoutine != null)
        {
            StopCoroutine(fullSalvoVisualRestoreRoutine);
            fullSalvoVisualRestoreRoutine = null;
        }

        fullSalvoVisualSalvoId = salvoId;
        fullSalvoFrontViewActive = true;
        SetCinematicVisualFacingCamera();
    }

    private void HandleLockOnSalvoFinished(int salvoId, bool canceled)
    {
        if (!fullSalvoFrontViewActive || salvoId != fullSalvoVisualSalvoId)
        {
            return;
        }

        if (canceled)
        {
            ResetFullSalvoVisualImmediate();
            return;
        }

        if (fullSalvoVisualRestoreRoutine != null)
        {
            StopCoroutine(fullSalvoVisualRestoreRoutine);
        }

        fullSalvoVisualRestoreRoutine = StartCoroutine(RestoreFullSalvoVisualAfterDelay());
    }

    private IEnumerator RestoreFullSalvoVisualAfterDelay()
    {
        float delay = Mathf.Max(0f, fullSalvoVisualRestoreDelay);
        if (delay > 0f)
        {
            yield return new WaitForSeconds(delay);
        }

        fullSalvoVisualRestoreRoutine = null;
        fullSalvoVisualSalvoId = 0;
        fullSalvoFrontViewActive = false;
        ClearCinematicVisualOverride();
    }

    private void ResetFullSalvoVisualImmediate()
    {
        if (fullSalvoVisualRestoreRoutine != null)
        {
            StopCoroutine(fullSalvoVisualRestoreRoutine);
            fullSalvoVisualRestoreRoutine = null;
        }

        bool shouldClearVisualOverride = fullSalvoFrontViewActive;
        fullSalvoVisualSalvoId = 0;
        fullSalvoFrontViewActive = false;
        if (shouldClearVisualOverride)
        {
            ClearCinematicVisualOverride();
        }
    }

    public void RefreshVisualBindings()
    {
        if (movementViewportInitializationEnabled)
        {
            ResetMovementViewportInitialization();
        }

        CacheVisualTiltRoot();
        ResetVisualTiltImmediate();
    }

    public bool ReinitializeMovementViewportForCurrentDisplay()
    {
        ResetMovementViewportInitialization();
        bool initialized = TryResolveCameraPlane() && movementViewportInitializedForDisplay;
        if (initialized)
        {
            RepositionImmediate();
        }

        return initialized;
    }

    public void SetInputEnabled(bool enabled)
    {
        inputEnabled = enabled;
    }

    public void SetMovementSpeedsForDebug(float strafe, float altitude, float forward)
    {
        strafeSpeed = Mathf.Max(0f, strafe);
        altitudeSpeed = Mathf.Max(0f, altitude);
        forwardSpeed = Mathf.Max(0f, forward);
    }

    public void SetVisualTiltForDebug(float maxAngle, float duration)
    {
        maxVisualTiltAngle = Mathf.Max(0f, maxAngle);
        visualTiltDuration = Mathf.Max(0f, duration);
    }

    public void ApplyAirPressureImpulse(
        Vector3 worldDirection,
        float distance,
        float pushDuration,
        int rollCount,
        float rollDuration)
    {
        Vector3 planeDirection = ResolveMovementPlaneDirection(worldDirection);
        float clampedDistance = Mathf.Max(0f, distance);
        float clampedPushDuration = Mathf.Max(0.01f, pushDuration);
        int clampedRollCount = Mathf.Max(0, rollCount);
        float clampedRollDuration = Mathf.Max(0.01f, rollDuration);

        if (airPressureImpulseRoutine != null)
        {
            StopCoroutine(airPressureImpulseRoutine);
            airPressureImpulseRoutine = null;
        }

        if (airPressureRollRoutine != null)
        {
            StopCoroutine(airPressureRollRoutine);
            airPressureRollRoutine = null;
        }

        airPressureRollDegrees = 0f;
        airPressureSwayDegrees = 0f;

        if (clampedDistance > 0.001f)
        {
            airPressureImpulseRoutine = StartCoroutine(ApplyAirPressureImpulseRoutine(
                planeDirection,
                clampedDistance,
                clampedPushDuration));
        }

        if (clampedRollCount > 0)
        {
            airPressureRollRoutine = StartCoroutine(ApplyAirPressureRollRoutine(
                ResolveAirPressureRollSign(planeDirection),
                clampedRollCount,
                clampedRollDuration));
        }
    }

    private IEnumerator ApplyAirPressureImpulseRoutine(Vector3 direction, float distance, float duration)
    {
        float elapsed = 0f;
        float previousProgress = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float normalizedTime = Mathf.Clamp01(elapsed / duration);
            float easedProgress = 1f - Mathf.Pow(1f - normalizedTime, 3f);
            float deltaDistance = (easedProgress - previousProgress) * distance;
            transform.position = ClampToMovementPlane(transform.position + direction * deltaDistance);
            previousProgress = easedProgress;
            yield return null;
        }

        airPressureImpulseRoutine = null;
        ResetVelocityTracking();
    }

    private IEnumerator ApplyAirPressureRollRoutine(float rollSign, int rollCount, float duration)
    {
        float elapsed = 0f;
        float totalDegrees = 360f * Mathf.Max(1, rollCount) * (rollSign >= 0f ? 1f : -1f);
        const float swayAmplitudeDegrees = 8f;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float normalizedTime = Mathf.Clamp01(elapsed / duration);
            float easedProgress = 1f - Mathf.Pow(1f - normalizedTime, 3.4f);
            airPressureRollDegrees = totalDegrees * easedProgress;
            airPressureSwayDegrees = Mathf.Sin(normalizedTime * Mathf.PI * 2f) * swayAmplitudeDegrees;
            yield return null;
        }

        airPressureRollDegrees = 0f;
        airPressureSwayDegrees = 0f;
        airPressureRollRoutine = null;
    }

    private Vector3 ResolveMovementPlaneDirection(Vector3 worldDirection)
    {
        ResolveMovementAxes(out Vector3 right, out Vector3 up, out _);
        Vector3 safeDirection = worldDirection.sqrMagnitude > 0.0001f ? worldDirection.normalized : right;
        Vector3 projectedDirection = right * Vector3.Dot(safeDirection, right) + up * Vector3.Dot(safeDirection, up);
        return projectedDirection.sqrMagnitude > 0.0001f ? projectedDirection.normalized : right;
    }

    private float ResolveAirPressureRollSign(Vector3 planeDirection)
    {
        ResolveMovementAxes(out Vector3 right, out _, out _);
        return Vector3.Dot(planeDirection, right) >= 0f ? -1f : 1f;
    }

    public void SetCinematicVisualLookAt(Vector3 worldTarget)
    {
        Vector3 flatDirection = worldTarget - transform.position;
        flatDirection.y = 0f;
        if (flatDirection.sqrMagnitude < 0.001f)
        {
            flatDirection = transform.forward;
            flatDirection.y = 0f;
        }

        if (flatDirection.sqrMagnitude < 0.001f)
        {
            flatDirection = Vector3.forward;
        }

        cinematicVisualDisplayRotation =
            Quaternion.LookRotation(flatDirection.normalized, Vector3.up) *
            Quaternion.Euler(cinematicRearViewEulerOffset) *
            Quaternion.Euler(0f, 180f, 0f);
        cinematicVisualOverrideActive = true;
        currentVisualTilt = Vector2.zero;
        ApplyCinematicVisualPose();
    }

    public void SetCinematicVisualFacingCamera()
    {
        EnsureVisualTargetsReady();
        if (!cinematicVisualOverrideActive)
        {
            cinematicReturnDisplayRotation = ResolveCurrentVisualDisplayRotation();
        }

        cinematicVisualDisplayRotation = ResolveCameraFacingDisplayRotation();
        cinematicVisualOverrideActive = true;
        currentVisualTilt = Vector2.zero;
        ApplyCinematicVisualPose();
    }

    public void SetCinematicVisualTurnToward(Vector3 worldTarget, float maxTurnAngle)
    {
        EnsureVisualTargetsReady();
        Quaternion startRotation = ResolveCurrentVisualDisplayRotation();
        if (!cinematicVisualOverrideActive)
        {
            cinematicReturnDisplayRotation = startRotation;
        }

        Quaternion targetRotation = ResolveTargetFacingDisplayRotation(worldTarget);
        cinematicVisualDisplayRotation = Quaternion.RotateTowards(
            startRotation,
            targetRotation,
            Mathf.Max(0f, maxTurnAngle));
        cinematicVisualOverrideActive = true;
        currentVisualTilt = Vector2.zero;
        ApplyCinematicVisualPose();
    }

    public void ClearCinematicVisualOverride()
    {
        cinematicVisualOverrideActive = false;
        ResetVisualTiltImmediate();
    }

    public IEnumerator ClearCinematicVisualOverrideSmooth(float duration)
    {
        if (!cinematicVisualOverrideActive)
        {
            yield break;
        }

        Quaternion startRotation = ResolveCurrentVisualDisplayRotation();
        Quaternion targetRotation = cinematicReturnDisplayRotation;
        float clampedDuration = Mathf.Max(0f, duration);
        if (clampedDuration <= 0.001f)
        {
            cinematicVisualDisplayRotation = targetRotation;
            ApplyCinematicVisualPose();
            ClearCinematicVisualOverride();
            yield break;
        }

        float elapsed = 0f;
        while (elapsed < clampedDuration)
        {
            elapsed += Time.deltaTime;
            float progress = Mathf.Clamp01(elapsed / clampedDuration);
            float easedProgress = Mathf.SmoothStep(0f, 1f, progress);
            cinematicVisualDisplayRotation = Quaternion.Slerp(startRotation, targetRotation, easedProgress);
            currentVisualTilt = Vector2.zero;
            ApplyCinematicVisualPose();
            yield return null;
        }

        cinematicVisualDisplayRotation = targetRotation;
        ApplyCinematicVisualPose();
        ClearCinematicVisualOverride();
    }

    public void AdoptScenePlacement(Vector3 worldPosition)
    {
        CaptureRootRotation(worldPosition);

        if (TryResolveInitialViewportPlacement(worldPosition, out Vector3 initialWorldPosition))
        {
            transform.position = initialWorldPosition;
            RepositionImmediate();
            ResetVelocityTracking();
            return;
        }

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
        float movementSpeedMultiplier = ResolveMovementSpeedMultiplier();
        Vector3 movementDelta =
            right * (horizontal * strafeSpeed * movementSpeedMultiplier * Time.deltaTime) +
            up * (altitude * altitudeSpeed * movementSpeedMultiplier * Time.deltaTime);

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
        RefreshPlayerVisualOutput();
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
        Quaternion composedVisualOffset = ComposeAirPressureVisualOffset(visualTiltOffset);
        Transform target = visualPoseRoot != null ? visualPoseRoot : visualTiltRoot;
        if (target == null)
        {
            return;
        }

        if (!lockVisualRootToCamera)
        {
            target.localRotation = visualTiltBaseLocalRotation * composedVisualOffset;
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

        target.rotation = baseWorldRotation * composedVisualOffset;
    }

    private Quaternion ComposeAirPressureVisualOffset(Quaternion visualOffset)
    {
        if (Mathf.Abs(airPressureRollDegrees) <= 0.001f && Mathf.Abs(airPressureSwayDegrees) <= 0.001f)
        {
            return visualOffset;
        }

        return visualOffset * Quaternion.Euler(0f, airPressureRollDegrees, airPressureSwayDegrees);
    }

    private void EnsurePlayerVisualOutputReady()
    {
        if (!Application.isPlaying)
        {
            return;
        }

        Transform sourceVisual = visualPoseRoot != null ? visualPoseRoot : visualTiltRoot;
        if (sourceVisual == null)
        {
            CacheVisualTiltRoot();
            sourceVisual = visualPoseRoot != null ? visualPoseRoot : visualTiltRoot;
        }

        if (sourceVisual == null || !TryResolveCameraPlane())
        {
            playerVisualOverlayRenderer?.Shutdown();
            return;
        }

        playerVisualOverlayRenderer ??= GetComponent<PlayerVisualOverlayRenderer>();
        playerVisualOverlayRenderer ??= gameObject.AddComponent<PlayerVisualOverlayRenderer>();
        if (!playerVisualOverlayRenderer.IsConfigured ||
            playerVisualOverlayRenderer.BaseCamera != movementCamera ||
            playerVisualOverlayRenderer.VisualRoot != sourceVisual ||
            playerVisualOverlayRenderer.CentersVisualOnOwner != centerOriginalVisualOnMovementAnchor)
        {
            playerVisualOverlayRenderer.Configure(
                movementCamera,
                sourceVisual,
                playerVisualLayerName,
                centerOriginalVisualOnMovementAnchor);
        }
    }

    private void RefreshPlayerVisualOutput()
    {
        Transform sourceVisual = visualPoseRoot != null ? visualPoseRoot : visualTiltRoot;
        if (!Application.isPlaying)
        {
            return;
        }

        EnsurePlayerVisualOutputReady();
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

    private bool TryResolveInitialViewportPlacement(Vector3 referenceWorldPosition, out Vector3 initialWorldPosition)
    {
        initialWorldPosition = default;
        if (!useInitialViewportPlacement || !TryResolveCameraPlane())
        {
            return false;
        }

        Vector3 referenceViewportPoint = movementCamera.WorldToViewportPoint(referenceWorldPosition);
        CaptureCameraPlane(new Vector3(
            initialViewportPosition.x,
            initialViewportPosition.y,
            referenceViewportPoint.z));

        initialWorldPosition = movementCamera.ViewportToWorldPoint(
            new Vector3(viewportPosition.x, viewportPosition.y, cameraPlaneDepth));
        return true;
    }

    private void CaptureCameraPlane(Vector3 viewportPoint)
    {
        TryInitializeMovementViewportForCurrentDisplay();
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
        SyncAuthoredMovementBoundsToGameplayViewport();
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

        TryInitializeMovementViewportForCurrentDisplay();
        return true;
    }

    private Rect GetEffectiveMovementViewportRect()
    {
        TryInitializeMovementViewportForCurrentDisplay();
        return movementViewportRect;
    }

    private bool TryInitializeMovementViewportForCurrentDisplay()
    {
        if (!movementViewportInitializationEnabled)
        {
            return false;
        }

        if (movementViewportInitializedForDisplay)
        {
            return true;
        }

        if (movementCamera == null || movementCamera.pixelWidth <= 0 ||
            movementCamera.pixelHeight <= 0)
        {
            return false;
        }

        Rect baseViewport = fitMovementToFullGameplayViewport
            ? DefaultViewportRect
            : playerMoveGuide != null
                ? playerMoveGuide.ViewportRect
                : DefaultViewportRect;
        float horizontalPadding =
            Mathf.Max(0f, gameplayViewportEdgePaddingPixels.x) / movementCamera.pixelWidth;
        float verticalPadding =
            Mathf.Max(0f, gameplayViewportEdgePaddingPixels.y) / movementCamera.pixelHeight;
        float minX = Mathf.Clamp01(baseViewport.xMin + horizontalPadding);
        float maxX = Mathf.Clamp01(baseViewport.xMax - horizontalPadding);
        float minY = Mathf.Clamp01(baseViewport.yMin + verticalPadding);
        float maxY = Mathf.Clamp01(baseViewport.yMax - verticalPadding);
        if (minX > maxX)
        {
            minX = maxX = 0.5f;
        }

        if (minY > maxY)
        {
            minY = maxY = 0.5f;
        }

        movementViewportRect = Rect.MinMaxRect(minX, minY, maxX, maxY);
        initializedGameplayPixelSize = new Vector2Int(
            movementCamera.pixelWidth,
            movementCamera.pixelHeight);
        initializedGameplayAspect = movementCamera.aspect;
        movementViewportInitializedForDisplay = true;
        playerMoveGuide?.SetViewportRect(movementViewportRect);
        SyncAuthoredMovementBoundsToGameplayViewport();

        Debug.Log(
            $"[PlayerMovementViewport] Initialized once. pixels=" +
            $"{initializedGameplayPixelSize.x}x{initializedGameplayPixelSize.y}, " +
            $"aspect={initializedGameplayAspect:0.000}, rect=" +
            $"({movementViewportRect.xMin:0.000},{movementViewportRect.yMin:0.000})-" +
            $"({movementViewportRect.xMax:0.000},{movementViewportRect.yMax:0.000}).",
            this);
        return true;
    }

    private void SyncAuthoredMovementBoundsToGameplayViewport()
    {
        if (!movementViewportInitializedForDisplay || !hasCameraPlane ||
            movementBounds == null || movementCamera == null)
        {
            return;
        }

        movementBounds.FitToCameraViewport(
            movementCamera,
            movementViewportRect,
            cameraPlaneDepth);
    }

    private void ResetMovementViewportInitialization()
    {
        movementViewportInitializedForDisplay = false;
        initializedGameplayPixelSize = Vector2Int.zero;
        initializedGameplayAspect = 0f;
        movementViewportRect = fitMovementToFullGameplayViewport
            ? DefaultViewportRect
            : playerMoveGuide != null
                ? playerMoveGuide.ViewportRect
                : DefaultViewportRect;
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

        if (cinematicVisualOverrideActive)
        {
            currentVisualTilt = Vector2.zero;
            ApplyCinematicVisualPose();
            return;
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

    private void ApplyCinematicVisualPose()
    {
        Transform target = visualPoseRoot != null ? visualPoseRoot : visualTiltRoot;
        if (target != null)
        {
            target.rotation = cinematicVisualDisplayRotation;
        }
    }

    private void EnsureVisualTargetsReady()
    {
        if (visualTiltRoot == null)
        {
            CacheVisualTiltRoot();
        }

        EnsurePlayerVisualOutputReady();
    }

    private Quaternion ResolveCurrentVisualDisplayRotation()
    {
        EnsureVisualTargetsReady();
        Transform target = visualPoseRoot != null ? visualPoseRoot : visualTiltRoot;
        if (target != null)
        {
            return target.rotation;
        }

        if (hasLockedVisualPose)
        {
            return TryResolveCameraPlane()
                ? movementCamera.transform.rotation * lockedVisualCameraRelativeRotation
                : lockedVisualWorldRotation;
        }

        return Quaternion.identity;
    }

    private Quaternion ResolveCameraFacingDisplayRotation()
    {
        Camera referenceCamera = movementCamera != null ? movementCamera : Camera.main;
        if (referenceCamera == null && TryResolveCameraPlane())
        {
            referenceCamera = movementCamera;
        }

        Vector3 facingDirection = referenceCamera != null
            ? -referenceCamera.transform.forward
            : Vector3.back;
        Vector3 upDirection = referenceCamera != null
            ? referenceCamera.transform.up
            : Vector3.up;

        if (facingDirection.sqrMagnitude < 0.001f)
        {
            facingDirection = Vector3.back;
        }

        if (upDirection.sqrMagnitude < 0.001f)
        {
            upDirection = Vector3.up;
        }

        return Quaternion.LookRotation(facingDirection.normalized, upDirection.normalized) *
               Quaternion.Euler(cinematicFrontViewEulerOffset);
    }

    private Quaternion ResolveTargetFacingDisplayRotation(Vector3 worldTarget)
    {
        Vector3 flatDirection = worldTarget - transform.position;
        flatDirection.y = 0f;
        if (flatDirection.sqrMagnitude < 0.001f)
        {
            flatDirection = transform.forward;
            flatDirection.y = 0f;
        }

        if (flatDirection.sqrMagnitude < 0.001f)
        {
            flatDirection = Vector3.forward;
        }

        return Quaternion.LookRotation(flatDirection.normalized, Vector3.up) *
               Quaternion.Euler(cinematicRearViewEulerOffset);
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
        MigratePreviousDefaultAxisMovementSpeedsIfNeeded();
        centerOriginalVisualOnMovementAnchor = true;
        lockVisualRootToCamera = true;
        enableVisualTilt = true;

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

        if (lockOnChargingMovementSpeedMultiplier <= 0f ||
            lockOnChargingMovementSpeedMultiplier > 1f)
        {
            lockOnChargingMovementSpeedMultiplier =
                DefaultLockOnChargingMovementSpeedMultiplier;
        }

        if (string.IsNullOrWhiteSpace(playerVisualLayerName))
        {
            playerVisualLayerName = "PlayerVisual";
        }

        gameplayViewportEdgePaddingPixels.x = Mathf.Max(0f, gameplayViewportEdgePaddingPixels.x);
        gameplayViewportEdgePaddingPixels.y = Mathf.Max(0f, gameplayViewportEdgePaddingPixels.y);

        if (maxVisualTiltAngle <= 0.001f || maxVisualTiltAngle > 18f)
        {
            maxVisualTiltAngle = 12f;
        }

        if (visualTiltDuration <= 0.01f || visualTiltDuration > 0.25f)
        {
            visualTiltDuration = 0.18f;
        }

        if (fullSalvoVisualRestoreDelay <= 0f || fullSalvoVisualRestoreDelay > 10f)
        {
            fullSalvoVisualRestoreDelay = DefaultFullSalvoVisualRestoreDelay;
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

    private void MigratePreviousDefaultAxisMovementSpeedsIfNeeded()
    {
        // Existing scenes serialize the former 8-unit defaults. Normalize only
        // those exact legacy defaults so authored custom speeds remain intact.
        if (Mathf.Approximately(strafeSpeed, PreviousDefaultAxisMovementSpeed))
        {
            strafeSpeed = DefaultStrafeSpeed;
        }

        if (Mathf.Approximately(altitudeSpeed, PreviousDefaultAxisMovementSpeed))
        {
            altitudeSpeed = DefaultAltitudeSpeed;
        }
    }

    private float ResolveMovementSpeedMultiplier()
    {
        return playerLockOnController != null && playerLockOnController.IsCharging
            ? lockOnChargingMovementSpeedMultiplier
            : 1f;
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
