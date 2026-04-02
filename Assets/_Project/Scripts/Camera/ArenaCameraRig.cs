using UnityEngine;
using UnityEngine.Serialization;

[DefaultExecutionOrder(-200)]
public class ArenaCameraRig : MonoBehaviour
{
    private const float DefaultOrbitRadius = 24f;
    private const float DefaultHeight = 12f;
    private const float DefaultOrbitSpeedDegrees = 14f;
    private const float DefaultPositionLerpSpeed = 8f;

    [SerializeField] private bool deriveRuntimeOffsetFromScenePlacement = true;
    [Header("Debug")]
    [FormerlySerializedAs("debugFreezeOrbit")]
    [Tooltip("True = stop camera orbit. False = rotate normally. Toggle only this bool when comparing combat behavior.")]
    // Debug toggle: change this one bool only.
    [SerializeField] private bool freezeOrbitWithBoolToggle = true;
    [SerializeField] private float orbitRadius = 24f;
    [SerializeField] private float height = 12f;
    [SerializeField] private float orbitSpeedDegrees = 14f;
    [SerializeField] private float positionLerpSpeed = 8f;

    private Transform orbitCenter;
    private Transform lookTarget;
    private float orbitAngleDegrees;
    private Quaternion lookRotationOffset = Quaternion.identity;

    public Vector3 PlanarForward
    {
        get
        {
            Vector3 forward = Vector3.ProjectOnPlane(transform.forward, Vector3.up);
            return forward.sqrMagnitude > 0.001f ? forward.normalized : Vector3.forward;
        }
    }

    // Runtime debug toggle used by the HUD button.
    public bool FreezeOrbitWithBoolToggle
    {
        get => freezeOrbitWithBoolToggle;
        set => freezeOrbitWithBoolToggle = value;
    }

    public Vector3 PlanarRight
    {
        get
        {
            Vector3 right = Vector3.ProjectOnPlane(transform.right, Vector3.up);
            return right.sqrMagnitude > 0.001f ? right.normalized : Vector3.right;
        }
    }

    public void Configure(Transform center, Transform lookAtTarget)
    {
        EnsureRuntimeDefaults();
        orbitCenter = center;
        lookTarget = lookAtTarget;

        if (deriveRuntimeOffsetFromScenePlacement)
        {
            CaptureScenePlacementOffset();
        }

        SnapImmediate();
    }

    public void ToggleOrbitFreeze()
    {
        freezeOrbitWithBoolToggle = !freezeOrbitWithBoolToggle;
    }

    private void Awake()
    {
        EnsureRuntimeDefaults();
    }

    private void LateUpdate()
    {
        if (orbitCenter == null)
        {
            return;
        }

        if (!freezeOrbitWithBoolToggle)
        {
            orbitAngleDegrees -= orbitSpeedDegrees * Time.deltaTime;
        }
        Vector3 desiredPosition = GetDesiredPosition();
        transform.position = Vector3.Lerp(transform.position, desiredPosition, 1f - Mathf.Exp(-positionLerpSpeed * Time.deltaTime));
        transform.rotation = GetDesiredRotation(transform.position);
    }

    private void EnsureRuntimeDefaults()
    {
        if (orbitRadius <= 0.01f)
        {
            orbitRadius = DefaultOrbitRadius;
        }

        if (Mathf.Abs(height) <= 0.01f)
        {
            height = DefaultHeight;
        }

        if (Mathf.Abs(orbitSpeedDegrees) <= 0.01f)
        {
            orbitSpeedDegrees = DefaultOrbitSpeedDegrees;
        }

        if (positionLerpSpeed <= 0.01f)
        {
            positionLerpSpeed = DefaultPositionLerpSpeed;
        }
    }

    private void SnapImmediate()
    {
        if (orbitCenter == null)
        {
            return;
        }

        transform.position = GetDesiredPosition();
        transform.rotation = GetDesiredRotation(transform.position);
    }

    private void CaptureScenePlacementOffset()
    {
        Vector3 flatOffset = transform.position - orbitCenter.position;
        flatOffset.y = 0f;

        if (flatOffset.sqrMagnitude > 0.001f)
        {
            orbitRadius = flatOffset.magnitude;
            orbitAngleDegrees = Mathf.Atan2(flatOffset.x, flatOffset.z) * Mathf.Rad2Deg;
        }

        height = transform.position.y - orbitCenter.position.y;
        Quaternion desiredLookRotation = GetBaseLookRotation(transform.position);
        lookRotationOffset = Quaternion.Inverse(desiredLookRotation) * transform.rotation;
    }

    private Vector3 GetDesiredPosition()
    {
        float radians = orbitAngleDegrees * Mathf.Deg2Rad;
        Vector3 flatOffset = new Vector3(Mathf.Sin(radians), 0f, Mathf.Cos(radians)) * orbitRadius;
        return orbitCenter.position + flatOffset + Vector3.up * height;
    }

    private Quaternion GetDesiredRotation(Vector3 worldPosition)
    {
        return GetBaseLookRotation(worldPosition) * lookRotationOffset;
    }

    private Quaternion GetBaseLookRotation(Vector3 worldPosition)
    {
        Vector3 lookPosition = lookTarget != null ? lookTarget.position : orbitCenter != null ? orbitCenter.position : worldPosition + Vector3.forward;
        Vector3 direction = lookPosition - worldPosition;
        if (direction.sqrMagnitude < 0.001f)
        {
            return Quaternion.LookRotation(Vector3.forward, Vector3.up);
        }

        return Quaternion.LookRotation(direction.normalized, Vector3.up);
    }
}
