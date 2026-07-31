using System;
using UnityEngine;

public class BossController : MonoBehaviour
{
    private const string DefaultDamageHurtboxName = "BossHurtbox";

    [SerializeField] private bool deriveFacingOffsetFromSceneRotation = true;
    [SerializeField] private float maxHealth = 2000f;
    [SerializeField] private float hitRadius = 3.8f;
    [SerializeField] private Collider[] damageHurtboxes = Array.Empty<Collider>();
    [SerializeField] private float idleBobAmplitude = 0.18f;
    [SerializeField] private float idleBobSpeed = 1.4f;
    [SerializeField] private Transform aimPoint;

    private float currentHealth;
    private Vector3 basePosition;
    private Vector3 baseScale;
    private float pulseTimer;
    private Renderer[] cachedRenderers;
    private Color[] rendererBaseColors;
    private Quaternion facingRotationOffset = Quaternion.identity;
    private bool cinematicPaused;

    public event Action Died;

    public bool IsAlive => currentHealth > 0f;
    public float CurrentHealth => currentHealth;
    public float MaxHealth => maxHealth;
    public float HealthRatio => maxHealth > 0f ? currentHealth / maxHealth : 0f;
    public float HitRadius => hitRadius;
    public Transform AimPoint
    {
        get
        {
            ResolveAimPoint();
            return aimPoint != null ? aimPoint : transform;
        }
    }
    public Transform OrbitCenter => transform;
    public Vector3 HitPoint
    {
        get
        {
            if (TryGetDamageHurtboxBounds(out Bounds bounds))
            {
                return bounds.center;
            }

            return AimPoint != null ? AimPoint.position : transform.position + Vector3.up * 5f;
        }
    }
    public float DebugIdleBobAmplitude => idleBobAmplitude;
    public float DebugIdleBobSpeed => idleBobSpeed;

    private void Awake()
    {
        ResolveAimPoint();
        ResolveDamageHurtboxes();
        currentHealth = maxHealth;
        basePosition = transform.position;
        baseScale = transform.localScale;
        cachedRenderers = GetComponentsInChildren<Renderer>();
        rendererBaseColors = CacheBaseColors(cachedRenderers);
    }

    private void Update()
    {
        if (cinematicPaused)
        {
            return;
        }

        float bobOffset = Mathf.Sin(Time.time * idleBobSpeed) * idleBobAmplitude;
        transform.position = new Vector3(basePosition.x, basePosition.y + bobOffset, basePosition.z);

        pulseTimer = Mathf.Max(0f, pulseTimer - Time.deltaTime * 4f);
        transform.localScale = baseScale * (1f + pulseTimer * 0.05f);
    }

    public void CaptureBasePose()
    {
        basePosition = transform.position;
        baseScale = transform.localScale;
    }

    public void ConfigureEncounter(float encounterMaxHealth)
    {
        maxHealth = encounterMaxHealth;
        currentHealth = encounterMaxHealth;
    }

    public void SetMaxHealthForDebug(float value, bool refill)
    {
        maxHealth = Mathf.Max(1f, value);
        currentHealth = refill ? maxHealth : Mathf.Clamp(currentHealth, 0f, maxHealth);
    }

    public void SetCurrentHealthForDebug(float value)
    {
        currentHealth = Mathf.Clamp(value, 0f, maxHealth);
    }

    public void FullHealForDebug()
    {
        currentHealth = maxHealth;
    }

    public void SetHitRadiusForDebug(float value)
    {
        hitRadius = Mathf.Max(0f, value);
    }

    public void SetIdleBobForDebug(float amplitude, float speed)
    {
        idleBobAmplitude = Mathf.Max(0f, amplitude);
        idleBobSpeed = Mathf.Max(0f, speed);
    }

    public void SetCinematicPaused(bool paused)
    {
        cinematicPaused = paused;
    }

    public void AdoptSceneRotation(Vector3 worldTarget)
    {
        if (!deriveFacingOffsetFromSceneRotation)
        {
            facingRotationOffset = Quaternion.identity;
            return;
        }

        Vector3 flatDirection = worldTarget - transform.position;
        flatDirection.y = 0f;
        if (flatDirection.sqrMagnitude < 0.001f)
        {
            facingRotationOffset = Quaternion.identity;
            return;
        }

        Quaternion desiredRotation = Quaternion.LookRotation(flatDirection.normalized, Vector3.up);
        facingRotationOffset = Quaternion.Inverse(desiredRotation) * transform.rotation;
    }

    public void FaceTarget(Vector3 worldTarget)
    {
        if (cinematicPaused)
        {
            return;
        }

        Vector3 flatDirection = worldTarget - transform.position;
        flatDirection.y = 0f;
        if (flatDirection.sqrMagnitude < 0.001f)
        {
            return;
        }

        Quaternion desiredRotation = Quaternion.LookRotation(flatDirection.normalized, Vector3.up) * facingRotationOffset;
        transform.rotation = Quaternion.Slerp(transform.rotation, desiredRotation, 1f - Mathf.Exp(-6f * Time.deltaTime));
    }

    public bool CheckHit(Vector3 worldPoint, float projectileHitRadius, Collider projectileCollider = null)
    {
        if (projectileCollider != null &&
            TryCheckHurtboxColliderHit(projectileCollider, out bool colliderHit) &&
            colliderHit)
        {
            return true;
        }

        float clampedProjectileHitRadius = Mathf.Max(0f, projectileHitRadius);
        if (TryCheckHurtboxHit(worldPoint, clampedProjectileHitRadius, out bool hurtboxHit))
        {
            return hurtboxHit;
        }

        return Vector3.Distance(worldPoint, HitPoint) <= clampedProjectileHitRadius + hitRadius;
    }

    public bool CheckHit(
        Vector3 previousWorldPoint,
        Vector3 worldPoint,
        float projectileHitRadius,
        Collider projectileCollider = null)
    {
        if (CheckHit(worldPoint, projectileHitRadius, projectileCollider))
        {
            return true;
        }

        float clampedProjectileHitRadius = Mathf.Max(0f, projectileHitRadius);
        if (TryCheckHurtboxSegmentHit(previousWorldPoint, worldPoint, clampedProjectileHitRadius, out bool segmentHit))
        {
            return segmentHit;
        }

        return DistancePointToSegment(HitPoint, previousWorldPoint, worldPoint) <= clampedProjectileHitRadius + hitRadius;
    }

    public bool ApplyDamage(float damage)
    {
        if (!IsAlive || damage <= 0f)
        {
            return false;
        }

        currentHealth = Mathf.Max(0f, currentHealth - damage);
        pulseTimer = 1f;
        ApplyTint(Color.white);
        Invoke(nameof(RestoreBaseColors), 0.08f);

        if (currentHealth <= 0f)
        {
            Died?.Invoke();
        }

        return true;
    }

    private void ResolveAimPoint()
    {
        if (aimPoint == null)
        {
            aimPoint = FindDeepChild(transform, "AimPoint");
        }
    }

    private void ResolveDamageHurtboxes()
    {
        if (HasAssignedDamageHurtboxes())
        {
            return;
        }

        Transform defaultHurtboxRoot = FindDeepChild(transform, DefaultDamageHurtboxName);
        if (defaultHurtboxRoot == null)
        {
            damageHurtboxes = Array.Empty<Collider>();
            return;
        }

        Collider[] foundHurtboxes = defaultHurtboxRoot.GetComponentsInChildren<Collider>(true);
        damageHurtboxes = foundHurtboxes != null && foundHurtboxes.Length > 0
            ? foundHurtboxes
            : Array.Empty<Collider>();
    }

    private bool HasAssignedDamageHurtboxes()
    {
        if (damageHurtboxes == null || damageHurtboxes.Length == 0)
        {
            return false;
        }

        int validCount = 0;
        for (int i = 0; i < damageHurtboxes.Length; i++)
        {
            if (damageHurtboxes[i] != null)
            {
                damageHurtboxes[validCount++] = damageHurtboxes[i];
            }
        }

        if (validCount == damageHurtboxes.Length)
        {
            return validCount > 0;
        }

        if (validCount == 0)
        {
            damageHurtboxes = Array.Empty<Collider>();
            return false;
        }

        Array.Resize(ref damageHurtboxes, validCount);
        return true;
    }

    private bool TryCheckHurtboxHit(Vector3 worldPoint, float projectileHitRadius, out bool hit)
    {
        ResolveDamageHurtboxes();
        if (damageHurtboxes == null || damageHurtboxes.Length == 0)
        {
            hit = false;
            return false;
        }

        float maxDistanceSqr = projectileHitRadius * projectileHitRadius;
        bool hasActiveHurtbox = false;
        for (int i = 0; i < damageHurtboxes.Length; i++)
        {
            Collider hurtbox = damageHurtboxes[i];
            if (hurtbox == null || !hurtbox.enabled || !hurtbox.gameObject.activeInHierarchy)
            {
                continue;
            }

            hasActiveHurtbox = true;
            Vector3 closestPoint = hurtbox.ClosestPoint(worldPoint);
            if ((closestPoint - worldPoint).sqrMagnitude <= maxDistanceSqr)
            {
                hit = true;
                return true;
            }
        }

        hit = false;
        return hasActiveHurtbox;
    }

    private bool TryCheckHurtboxSegmentHit(
        Vector3 previousWorldPoint,
        Vector3 worldPoint,
        float projectileHitRadius,
        out bool hit)
    {
        ResolveDamageHurtboxes();
        if (damageHurtboxes == null || damageHurtboxes.Length == 0)
        {
            hit = false;
            return false;
        }

        float maxDistanceSqr = projectileHitRadius * projectileHitRadius;
        Vector3 segment = worldPoint - previousWorldPoint;
        float segmentLength = segment.magnitude;
        bool canRaycast = segmentLength > 0.0001f;
        Ray segmentRay = canRaycast ? new Ray(previousWorldPoint, segment / segmentLength) : default;

        bool hasActiveHurtbox = false;
        for (int i = 0; i < damageHurtboxes.Length; i++)
        {
            Collider hurtbox = damageHurtboxes[i];
            if (hurtbox == null || !hurtbox.enabled || !hurtbox.gameObject.activeInHierarchy)
            {
                continue;
            }

            hasActiveHurtbox = true;
            if (IsPointWithinHurtboxRadius(hurtbox, previousWorldPoint, maxDistanceSqr) ||
                IsPointWithinHurtboxRadius(hurtbox, worldPoint, maxDistanceSqr))
            {
                hit = true;
                return true;
            }

            if (canRaycast && hurtbox.Raycast(segmentRay, out RaycastHit _, segmentLength))
            {
                hit = true;
                return true;
            }

            Vector3 nearestSegmentPoint = ClosestPointOnSegment(hurtbox.bounds.center, previousWorldPoint, worldPoint);
            if (IsPointWithinHurtboxRadius(hurtbox, nearestSegmentPoint, maxDistanceSqr))
            {
                hit = true;
                return true;
            }
        }

        hit = false;
        return hasActiveHurtbox;
    }

    private bool TryCheckHurtboxColliderHit(Collider projectileCollider, out bool hit)
    {
        ResolveDamageHurtboxes();
        if (projectileCollider == null || !projectileCollider.enabled || damageHurtboxes == null || damageHurtboxes.Length == 0)
        {
            hit = false;
            return false;
        }

        bool hasActiveHurtbox = false;
        for (int i = 0; i < damageHurtboxes.Length; i++)
        {
            Collider hurtbox = damageHurtboxes[i];
            if (hurtbox == null || !hurtbox.enabled || !hurtbox.gameObject.activeInHierarchy)
            {
                continue;
            }

            hasActiveHurtbox = true;
            if (Physics.ComputePenetration(
                projectileCollider,
                projectileCollider.transform.position,
                projectileCollider.transform.rotation,
                hurtbox,
                hurtbox.transform.position,
                hurtbox.transform.rotation,
                out _,
                out _))
            {
                hit = true;
                return true;
            }
        }

        hit = false;
        return hasActiveHurtbox;
    }

    private bool TryGetDamageHurtboxBounds(out Bounds bounds)
    {
        ResolveDamageHurtboxes();
        if (damageHurtboxes == null || damageHurtboxes.Length == 0)
        {
            bounds = default;
            return false;
        }

        bool hasActiveHurtbox = false;
        bounds = default;
        for (int i = 0; i < damageHurtboxes.Length; i++)
        {
            Collider hurtbox = damageHurtboxes[i];
            if (hurtbox == null || !hurtbox.enabled || !hurtbox.gameObject.activeInHierarchy)
            {
                continue;
            }

            if (!hasActiveHurtbox)
            {
                bounds = hurtbox.bounds;
                hasActiveHurtbox = true;
                continue;
            }

            bounds.Encapsulate(hurtbox.bounds);
        }

        return hasActiveHurtbox;
    }

    private static bool IsPointWithinHurtboxRadius(Collider hurtbox, Vector3 worldPoint, float maxDistanceSqr)
    {
        Vector3 closestPoint = hurtbox.ClosestPoint(worldPoint);
        return (closestPoint - worldPoint).sqrMagnitude <= maxDistanceSqr;
    }

    private static Vector3 ClosestPointOnSegment(Vector3 point, Vector3 segmentStart, Vector3 segmentEnd)
    {
        Vector3 segment = segmentEnd - segmentStart;
        float lengthSqr = segment.sqrMagnitude;
        if (lengthSqr <= 0.000001f)
        {
            return segmentEnd;
        }

        float t = Mathf.Clamp01(Vector3.Dot(point - segmentStart, segment) / lengthSqr);
        return segmentStart + segment * t;
    }

    private static float DistancePointToSegment(Vector3 point, Vector3 segmentStart, Vector3 segmentEnd)
    {
        return Vector3.Distance(point, ClosestPointOnSegment(point, segmentStart, segmentEnd));
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

    private static Color[] CacheBaseColors(Renderer[] renderers)
    {
        Color[] colors = new Color[renderers.Length];
        for (int i = 0; i < renderers.Length; i++)
        {
            Material material = renderers[i].material;
            if (material.HasProperty("_BaseColor"))
            {
                colors[i] = material.GetColor("_BaseColor");
            }
            else if (material.HasProperty("_Color"))
            {
                colors[i] = material.color;
            }
            else
            {
                colors[i] = Color.white;
            }
        }

        return colors;
    }

    private void ApplyTint(Color tint)
    {
        for (int i = 0; i < cachedRenderers.Length; i++)
        {
            Material material = cachedRenderers[i].material;
            if (material.HasProperty("_BaseColor"))
            {
                material.SetColor("_BaseColor", tint);
            }
            else if (material.HasProperty("_Color"))
            {
                material.color = tint;
            }
        }
    }

    private void RestoreBaseColors()
    {
        for (int i = 0; i < cachedRenderers.Length; i++)
        {
            Material material = cachedRenderers[i].material;
            Color color = rendererBaseColors[i];

            if (material.HasProperty("_BaseColor"))
            {
                material.SetColor("_BaseColor", color);
            }
            else if (material.HasProperty("_Color"))
            {
                material.color = color;
            }
        }
    }
}
