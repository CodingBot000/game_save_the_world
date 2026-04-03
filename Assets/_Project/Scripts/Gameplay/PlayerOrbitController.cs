using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Serialization;

[DefaultExecutionOrder(0)]
public class PlayerOrbitController : MonoBehaviour
{
    private const float DefaultStrafeSpeed = 8f;
    private const float DefaultAltitudeSpeed = 8f;
    private const float DefaultForwardSpeed = 10f;

    [SerializeField] private bool deriveRotationOffsetFromSceneRotation = true;
    [FormerlySerializedAs("horizontalScreenSpeed")]
    [SerializeField] private float strafeSpeed = 8f;
    [FormerlySerializedAs("verticalScreenSpeed")]
    [SerializeField] private float altitudeSpeed = 8f;
    [FormerlySerializedAs("depthSpeed")]
    [SerializeField] private float forwardSpeed = 10f;
    [Tooltip("One-time guard for upgrading old viewport-based movement speeds to world-space speeds.")]
    [SerializeField] private bool movementSpeedsMigratedToWorldSpace;
    [SerializeField] private PlayerMovementBounds movementBounds;
    [SerializeField] private string visualTiltRootName = "PlayerVisualRoot";
    [SerializeField] private float maxVisualTiltAngle = 30f;
    [SerializeField] private float visualTiltDuration = 0.3f;

    private Transform orbitCenter;
    private Transform lookTarget;
    private Transform visualTiltRoot;
    private bool inputEnabled = true;
    private Quaternion lookRotationOffset = Quaternion.identity;
    private Quaternion visualTiltBaseLocalRotation = Quaternion.identity;
    private Vector2 currentVisualTilt;
    private Vector2 movementInput;
    private Vector3 previousWorldPosition;
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
        UpdateVisualTilt(movementInput);
        UpdateWorldVelocity();
    }

    public void Configure(Transform center, Transform targetToLookAt, PlayerMovementBounds bounds)
    {
        EnsureRuntimeDefaults();
        CacheVisualTiltRoot();
        orbitCenter = center;
        lookTarget = targetToLookAt;
        movementBounds = bounds;
        ResetVelocityTracking();
    }

    public void RefreshVisualBindings()
    {
        CacheVisualTiltRoot();
    }

    public void SetInputEnabled(bool enabled)
    {
        inputEnabled = enabled;
    }

    public void AdoptScenePlacement(Vector3 worldPosition)
    {
        if (deriveRotationOffsetFromSceneRotation)
        {
            Quaternion desiredLookRotation = GetDesiredLookRotation(worldPosition);
            lookRotationOffset = Quaternion.Inverse(desiredLookRotation) * transform.rotation;
        }

        transform.position = movementBounds != null
            ? movementBounds.ClampWorldPosition(worldPosition)
            : worldPosition;
        RepositionImmediate();
        ResetVelocityTracking();
    }

    public void RepositionImmediate()
    {
        if (movementBounds != null)
        {
            transform.position = movementBounds.ClampWorldPosition(transform.position);
        }

        transform.rotation = GetDesiredLookRotation(transform.position) * lookRotationOffset;
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

        float forward = 0f;
        if (keyboard != null && keyboard.qKey.isPressed)
        {
            forward += 1f;
        }

        if (keyboard != null && keyboard.zKey.isPressed)
        {
            forward -= 1f;
        }

        movementInput = Vector2.ClampMagnitude(new Vector2(horizontal, altitude), 1f);

        ResolveMovementAxes(out Vector3 right, out Vector3 up, out Vector3 forwardAxis);
        Vector3 movementDelta =
            right * (horizontal * strafeSpeed * Time.deltaTime) +
            up * (altitude * altitudeSpeed * Time.deltaTime) +
            forwardAxis * (forward * forwardSpeed * Time.deltaTime);

        if (movementDelta.sqrMagnitude <= 0.000001f)
        {
            return;
        }

        transform.position += movementDelta;
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
