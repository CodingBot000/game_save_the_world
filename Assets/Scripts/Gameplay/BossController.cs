using System;
using UnityEngine;

public class BossController : MonoBehaviour
{
    [SerializeField] private bool deriveFacingOffsetFromSceneRotation = true;
    [SerializeField] private float maxHealth = 2000f;
    [SerializeField] private float hitRadius = 3.8f;
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

    public event Action Died;

    public bool IsAlive => currentHealth > 0f;
    public float CurrentHealth => currentHealth;
    public float MaxHealth => maxHealth;
    public float HealthRatio => maxHealth > 0f ? currentHealth / maxHealth : 0f;
    public float HitRadius => hitRadius;
    public Transform AimPoint => aimPoint != null ? aimPoint : transform;
    public Transform OrbitCenter => transform;
    public Vector3 HitPoint => AimPoint != null ? AimPoint.position : transform.position + Vector3.up * 5f;

    private void Awake()
    {
        if (aimPoint == null)
        {
            Transform foundAimPoint = transform.Find("AimPoint");
            if (foundAimPoint != null)
            {
                aimPoint = foundAimPoint;
            }
        }

        currentHealth = maxHealth;
        basePosition = transform.position;
        baseScale = transform.localScale;
        cachedRenderers = GetComponentsInChildren<Renderer>();
        rendererBaseColors = CacheBaseColors(cachedRenderers);
    }

    private void Update()
    {
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
        Vector3 flatDirection = worldTarget - transform.position;
        flatDirection.y = 0f;
        if (flatDirection.sqrMagnitude < 0.001f)
        {
            return;
        }

        Quaternion desiredRotation = Quaternion.LookRotation(flatDirection.normalized, Vector3.up) * facingRotationOffset;
        transform.rotation = Quaternion.Slerp(transform.rotation, desiredRotation, 1f - Mathf.Exp(-6f * Time.deltaTime));
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
