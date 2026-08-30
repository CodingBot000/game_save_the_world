using UnityEngine;

[DisallowMultipleComponent]
public sealed class BackgroundGroundUnitView : MonoBehaviour
{
    private const float MaximumPitchUpDegrees = 80f;
    private const float MaximumPitchDownDegrees = 35f;

    [SerializeField] private Transform visualRoot;
    [SerializeField] private Transform turretYaw;
    [SerializeField] private Transform weaponPitch;
    [SerializeField] private Transform muzzle;
    [SerializeField] private Transform muzzleFlashRoot;
    [SerializeField] private Renderer[] muzzleFlashRenderers;
    [SerializeField] private Renderer[] cachedRenderers;

    private Quaternion turretRestRotation = Quaternion.identity;
    private Quaternion weaponRestRotation = Quaternion.identity;
    private Vector3 turretRestAimDirectionInParent = Vector3.forward;
    private Vector3 weaponRestAimDirectionInParent = Vector3.forward;
    private bool restPoseCaptured;

    public Transform VisualRoot => visualRoot != null ? visualRoot : transform;
    public Transform TurretYaw => turretYaw;
    public Transform WeaponPitch => weaponPitch;
    public Transform Muzzle => muzzle != null ? muzzle : VisualRoot;
    public Renderer[] CachedRenderers => cachedRenderers;
    public bool IsMuzzleFlashVisible
    {
        get
        {
            if (muzzleFlashRenderers == null)
            {
                return false;
            }

            for (int i = 0; i < muzzleFlashRenderers.Length; i++)
            {
                if (muzzleFlashRenderers[i] != null && muzzleFlashRenderers[i].enabled)
                {
                    return true;
                }
            }

            return false;
        }
    }

    private void Awake()
    {
        CaptureRestPose();
        CacheRenderers();
        SetMuzzleFlash(false);
    }

    private void OnValidate()
    {
        CaptureRestPose();
        CacheRenderers(force: true);
    }

    public void ConfigureForEditor(
        Transform configuredVisualRoot,
        Transform configuredTurretYaw,
        Transform configuredWeaponPitch,
        Transform configuredMuzzle,
        Transform configuredMuzzleFlashRoot,
        Renderer[] configuredMuzzleFlashRenderers)
    {
        visualRoot = configuredVisualRoot;
        turretYaw = configuredTurretYaw;
        weaponPitch = configuredWeaponPitch;
        muzzle = configuredMuzzle;
        muzzleFlashRoot = configuredMuzzleFlashRoot;
        muzzleFlashRenderers = configuredMuzzleFlashRenderers;
        restPoseCaptured = false;
        CaptureRestPose();
        CacheRenderers(force: true);
    }

    public void AimAt(Vector3 worldTarget, float degreesPerSecond, float deltaTime)
    {
        CaptureRestPose();
        Vector3 worldUp = transform.up.sqrMagnitude > 0.000001f ? transform.up.normalized : Vector3.up;
        float maximumStep = Mathf.Max(0f, degreesPerSecond) * Mathf.Max(0f, deltaTime);

        if (turretYaw != null && turretYaw.parent != null)
        {
            Transform yawParent = turretYaw.parent;
            Vector3 yawAxisInParent = yawParent.InverseTransformDirection(worldUp).normalized;
            Vector3 targetDirectionInParent = yawParent.InverseTransformDirection(worldTarget - turretYaw.position);
            if (BackgroundGroundAimMath.TryCalculateYawRotation(
                    turretRestRotation,
                    turretRestAimDirectionInParent,
                    yawAxisInParent,
                    targetDirectionInParent,
                    out Quaternion targetYaw))
            {
                turretYaw.localRotation = Quaternion.RotateTowards(
                    turretYaw.localRotation,
                    targetYaw,
                    maximumStep);
            }
        }

        if (weaponPitch != null && weaponPitch.parent != null)
        {
            Transform pitchParent = weaponPitch.parent;
            Vector3 upInParent = pitchParent.InverseTransformDirection(worldUp).normalized;
            Vector3 targetDirectionInParent = pitchParent.InverseTransformDirection(worldTarget - weaponPitch.position);
            if (BackgroundGroundAimMath.TryCalculatePitchRotation(
                    weaponRestRotation,
                    weaponRestAimDirectionInParent,
                    upInParent,
                    targetDirectionInParent,
                    MaximumPitchUpDegrees,
                    MaximumPitchDownDegrees,
                    out Quaternion targetPitch))
            {
                weaponPitch.localRotation = Quaternion.RotateTowards(
                    weaponPitch.localRotation,
                    targetPitch,
                    maximumStep);
            }
        }
    }

    public void ResetAim(float degreesPerSecond, float deltaTime)
    {
        CaptureRestPose();
        if (turretYaw != null)
        {
            turretYaw.localRotation = Quaternion.RotateTowards(
                turretYaw.localRotation,
                turretRestRotation,
                Mathf.Max(0f, degreesPerSecond) * Mathf.Max(0f, deltaTime));
        }

        if (weaponPitch != null)
        {
            weaponPitch.localRotation = Quaternion.RotateTowards(
                weaponPitch.localRotation,
                weaponRestRotation,
                Mathf.Max(0f, degreesPerSecond) * Mathf.Max(0f, deltaTime));
        }
    }

    public void SetMuzzleFlash(bool visible)
    {
        if (!visible && muzzleFlashRoot != null)
        {
            muzzleFlashRoot.localScale = Vector3.one;
            muzzleFlashRoot.localRotation = Quaternion.identity;
        }

        if (muzzleFlashRenderers == null)
        {
            return;
        }

        for (int i = 0; i < muzzleFlashRenderers.Length; i++)
        {
            if (muzzleFlashRenderers[i] != null)
            {
                muzzleFlashRenderers[i].enabled = visible;
            }
        }
    }

    public void TickMuzzleFlash(float normalizedRemaining, float pulseSeed)
    {
        if (muzzleFlashRoot == null || !IsMuzzleFlashVisible)
        {
            return;
        }

        float progress = 1f - Mathf.Clamp01(normalizedRemaining);
        float envelope = Mathf.Sin(progress * Mathf.PI);
        float flicker = Mathf.Sin(progress * Mathf.PI * 5f + pulseSeed) * 0.08f;
        muzzleFlashRoot.localScale = Vector3.one * Mathf.Max(0.72f, 0.9f + envelope * 0.38f + flicker);
        muzzleFlashRoot.localRotation = Quaternion.Euler(0f, 0f, Mathf.Sin(progress * Mathf.PI * 2f + pulseSeed) * 12f);
    }

    public void SetRenderEnabled(bool visible)
    {
        CacheRenderers();
        for (int i = 0; i < cachedRenderers.Length; i++)
        {
            if (cachedRenderers[i] != null)
            {
                cachedRenderers[i].enabled = visible;
            }
        }
    }

    private void CaptureRestPose()
    {
        if (restPoseCaptured)
        {
            return;
        }

        turretRestRotation = turretYaw != null ? turretYaw.localRotation : Quaternion.identity;
        weaponRestRotation = weaponPitch != null ? weaponPitch.localRotation : Quaternion.identity;
        Vector3 authoredBarrelDirection = ResolveAuthoredBarrelDirection();
        if (turretYaw != null && turretYaw.parent != null)
        {
            turretRestAimDirectionInParent = turretYaw.parent.InverseTransformDirection(authoredBarrelDirection).normalized;
        }

        if (weaponPitch != null && weaponPitch.parent != null)
        {
            weaponRestAimDirectionInParent = weaponPitch.parent.InverseTransformDirection(authoredBarrelDirection).normalized;
        }

        restPoseCaptured = true;
    }

    private Vector3 ResolveAuthoredBarrelDirection()
    {
        if (muzzle != null && weaponPitch != null)
        {
            Vector3 direction = muzzle.position - weaponPitch.position;
            if (direction.sqrMagnitude > 0.000001f)
            {
                return direction.normalized;
            }
        }

        if (muzzle != null && turretYaw != null)
        {
            Vector3 direction = muzzle.position - turretYaw.position;
            if (direction.sqrMagnitude > 0.000001f)
            {
                return direction.normalized;
            }
        }

        return VisualRoot.forward;
    }

    private void CacheRenderers(bool force = false)
    {
        if (force || cachedRenderers == null || cachedRenderers.Length == 0)
        {
            cachedRenderers = GetComponentsInChildren<Renderer>(true);
        }
    }
}
