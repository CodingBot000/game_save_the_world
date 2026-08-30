using UnityEngine;

[DisallowMultipleComponent]
public sealed class BackgroundGroundUnitView : MonoBehaviour
{
    [SerializeField] private Transform visualRoot;
    [SerializeField] private Transform turretYaw;
    [SerializeField] private Transform weaponPitch;
    [SerializeField] private Transform muzzle;
    [SerializeField] private Transform muzzleFlashRoot;
    [SerializeField] private Renderer[] muzzleFlashRenderers;
    [SerializeField] private Renderer[] cachedRenderers;

    private Quaternion turretRestRotation = Quaternion.identity;
    private Quaternion weaponRestRotation = Quaternion.identity;
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
        if (turretYaw != null)
        {
            Vector3 localDirection = turretYaw.parent.InverseTransformDirection(worldTarget - turretYaw.position);
            localDirection.y = 0f;
            if (localDirection.sqrMagnitude > 0.000001f)
            {
                Quaternion yaw = Quaternion.LookRotation(localDirection.normalized, Vector3.up);
                turretYaw.localRotation = Quaternion.RotateTowards(
                    turretYaw.localRotation,
                    turretRestRotation * yaw,
                    Mathf.Max(0f, degreesPerSecond) * Mathf.Max(0f, deltaTime));
            }
        }

        if (weaponPitch != null)
        {
            Vector3 localDirection = weaponPitch.parent.InverseTransformDirection(worldTarget - weaponPitch.position);
            float planar = new Vector2(localDirection.x, localDirection.z).magnitude;
            if (localDirection.sqrMagnitude > 0.000001f)
            {
                float pitch = -Mathf.Atan2(localDirection.y, Mathf.Max(0.0001f, planar)) * Mathf.Rad2Deg;
                pitch = Mathf.Clamp(pitch, -35f, 18f);
                Quaternion target = weaponRestRotation * Quaternion.Euler(pitch, 0f, 0f);
                weaponPitch.localRotation = Quaternion.RotateTowards(
                    weaponPitch.localRotation,
                    target,
                    Mathf.Max(0f, degreesPerSecond) * Mathf.Max(0f, deltaTime));
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
        restPoseCaptured = true;
    }

    private void CacheRenderers(bool force = false)
    {
        if (force || cachedRenderers == null || cachedRenderers.Length == 0)
        {
            cachedRenderers = GetComponentsInChildren<Renderer>(true);
        }
    }
}
