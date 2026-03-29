using System;
using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerCombatController : MonoBehaviour
{
    private static readonly Quaternion PlayerProjectileVisualRotation = Quaternion.Euler(90f, 0f, 0f);

    [SerializeField] private float maxHealth = 100f;
    [SerializeField] private float fireCooldown = 0.15f;
    [SerializeField] private float projectileSpeed = 60f;
    [SerializeField] private float projectileDamage = 25f;
    [SerializeField] private float invulnerabilityDuration = 0.5f;
    [SerializeField] private float hitRadius = 1.4f;
    [SerializeField] private Transform muzzle;

    private BattleController battleController;
    private BossController bossController;
    private GameObject projectileTemplate;
    private Renderer[] cachedRenderers;
    private Color[] rendererBaseColors;
    private float shootCooldownRemaining;
    private float invulnerabilityRemaining;
    private bool combatEnabled = true;
    private float currentHealth;
    private ParticleSystem muzzleFlash;
    private Material muzzleFlashMaterial;
    private Mesh muzzleFlashParticleMesh;
    private float pulseTimer;
    private Vector3 baseScale;

    public event Action Died;

    public bool IsAlive => currentHealth > 0f;
    public float CurrentHealth => currentHealth;
    public float MaxHealth => maxHealth;
    public float HitRadius => hitRadius;
    public Vector3 HitPoint => transform.position + Vector3.up * 1.2f;

    private void Awake()
    {
        currentHealth = maxHealth;
        baseScale = transform.localScale;
        cachedRenderers = GetComponentsInChildren<Renderer>();
        rendererBaseColors = CacheBaseColors(cachedRenderers);
    }

    private void Update()
    {
        shootCooldownRemaining -= Time.deltaTime;
        invulnerabilityRemaining -= Time.deltaTime;
        pulseTimer = Mathf.Max(0f, pulseTimer - Time.deltaTime * 5f);
        transform.localScale = baseScale * (1f + pulseTimer * 0.06f);

        if (!combatEnabled || !IsAlive || battleController == null || bossController == null || !bossController.IsAlive)
        {
            return;
        }

        Mouse mouse = Mouse.current;
        Keyboard keyboard = Keyboard.current;
        bool mouseFire = mouse != null && mouse.leftButton.isPressed;
        bool keyboardFire = keyboard != null && keyboard.spaceKey.isPressed;
        if (mouseFire || keyboardFire)
        {
            TryFire();
        }
    }

    public void Configure(BattleController owner, BossController boss, GameObject projectileTemplateSource)
    {
        battleController = owner;
        bossController = boss;
        projectileTemplate = projectileTemplateSource;

        if (muzzle == null)
        {
            Transform foundMuzzle = transform.Find("Muzzle");
            if (foundMuzzle != null)
            {
                muzzle = foundMuzzle;
            }
        }

        EnsureMuzzleFlash();
    }

    public void SetCombatEnabled(bool enabled)
    {
        combatEnabled = enabled;
    }

    public bool ApplyDamage(float damage)
    {
        if (!IsAlive || invulnerabilityRemaining > 0f)
        {
            return false;
        }

        currentHealth = Mathf.Max(0f, currentHealth - damage);
        invulnerabilityRemaining = invulnerabilityDuration;
        pulseTimer = 1f;
        ApplyTint(Color.red);
        Invoke(nameof(RestoreBaseColors), 0.12f);

        if (currentHealth <= 0f)
        {
            Died?.Invoke();
        }

        return true;
    }

    private void TryFire()
    {
        if (shootCooldownRemaining > 0f || projectileTemplate == null)
        {
            return;
        }

        shootCooldownRemaining = fireCooldown;
        Vector3 origin = muzzle != null ? muzzle.position : HitPoint;
        Vector3 target = bossController.AimPoint != null ? bossController.AimPoint.position : bossController.transform.position;
        Vector3 direction = (target - origin).normalized;

        GameObject projectileInstance = Instantiate(projectileTemplate, origin, Quaternion.LookRotation(direction) * PlayerProjectileVisualRotation);
        projectileInstance.name = "PlayerProjectileRuntime";
        projectileInstance.SetActive(true);
        PlayMuzzleFlash();

        ProjectileController projectile = projectileInstance.GetComponent<ProjectileController>();
        if (projectile != null)
        {
            projectile.Launch(battleController, ProjectileTeam.Player, direction, projectileSpeed, projectileDamage);
        }
    }

    private void EnsureMuzzleFlash()
    {
        if (muzzle == null || muzzleFlash != null)
        {
            return;
        }

        Transform existing = muzzle.Find("MuzzleFlash");
        if (existing != null)
        {
            muzzleFlash = existing.GetComponent<ParticleSystem>();
            if (muzzleFlash != null)
            {
                return;
            }
        }

        GameObject flashObject = new("MuzzleFlash");
        flashObject.transform.SetParent(muzzle, false);
        flashObject.transform.localPosition = new Vector3(0f, 0f, 0.08f);
        flashObject.transform.localRotation = Quaternion.identity;

        muzzleFlash = flashObject.AddComponent<ParticleSystem>();
        ParticleSystem.MainModule main = muzzleFlash.main;
        main.loop = false;
        main.playOnAwake = false;
        main.duration = 0.08f;
        main.simulationSpace = ParticleSystemSimulationSpace.Local;
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.04f, 0.08f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(7f, 12f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.135f, 0.27f);
        main.startColor = new Color(1f, 0.82f, 0.28f, 0.95f);
        main.maxParticles = 36;

        ParticleSystem.EmissionModule emission = muzzleFlash.emission;
        emission.enabled = true;
        emission.rateOverTime = 0f;
        emission.SetBursts(new[] { new ParticleSystem.Burst(0f, 14, 18) });

        ParticleSystem.ShapeModule shape = muzzleFlash.shape;
        shape.enabled = true;
        shape.shapeType = ParticleSystemShapeType.Cone;
        shape.angle = 10f;
        shape.radius = 0.03f;
        shape.radiusThickness = 0.2f;

        ParticleSystem.ColorOverLifetimeModule colorOverLifetime = muzzleFlash.colorOverLifetime;
        colorOverLifetime.enabled = true;

        Gradient flashGradient = new();
        flashGradient.SetKeys(
            new[]
            {
                new GradientColorKey(new Color(1f, 0.95f, 0.7f), 0f),
                new GradientColorKey(new Color(1f, 0.65f, 0.15f), 0.45f),
                new GradientColorKey(new Color(0.35f, 0.35f, 0.35f), 1f),
            },
            new[]
            {
                new GradientAlphaKey(1f, 0f),
                new GradientAlphaKey(0.7f, 0.45f),
                new GradientAlphaKey(0f, 1f),
            });
        colorOverLifetime.color = new ParticleSystem.MinMaxGradient(flashGradient);

        ParticleSystem.SizeOverLifetimeModule sizeOverLifetime = muzzleFlash.sizeOverLifetime;
        sizeOverLifetime.enabled = true;
        AnimationCurve sizeCurve = new();
        sizeCurve.AddKey(0f, 0.35f);
        sizeCurve.AddKey(0.3f, 1f);
        sizeCurve.AddKey(1f, 0.1f);
        sizeOverLifetime.size = new ParticleSystem.MinMaxCurve(1f, sizeCurve);

        ParticleSystemRenderer renderer = muzzleFlash.GetComponent<ParticleSystemRenderer>();
        renderer.renderMode = ParticleSystemRenderMode.Mesh;
        renderer.mesh = GetOrCreateMuzzleFlashParticleMesh();
        renderer.maxParticleSize = 0.33f;
        renderer.sharedMaterial = GetOrCreateMuzzleFlashMaterial();

        muzzleFlash.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
    }

    private void PlayMuzzleFlash()
    {
        if (muzzleFlash == null)
        {
            EnsureMuzzleFlash();
            if (muzzleFlash == null)
            {
                return;
            }
        }

        muzzleFlash.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        muzzleFlash.Play(true);
    }

    private Mesh GetOrCreateMuzzleFlashParticleMesh()
    {
        if (muzzleFlashParticleMesh != null)
        {
            return muzzleFlashParticleMesh;
        }

        GameObject temp = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        MeshFilter meshFilter = temp.GetComponent<MeshFilter>();
        muzzleFlashParticleMesh = meshFilter != null ? meshFilter.sharedMesh : null;
        Destroy(temp);
        return muzzleFlashParticleMesh;
    }

    private Material GetOrCreateMuzzleFlashMaterial()
    {
        if (muzzleFlashMaterial != null)
        {
            return muzzleFlashMaterial;
        }

        Shader shader = Shader.Find("Universal Render Pipeline/Particles/Unlit");
        shader ??= Shader.Find("Particles/Standard Unlit");
        shader ??= Shader.Find("Universal Render Pipeline/Unlit");
        shader ??= Shader.Find("Sprites/Default");
        if (shader == null)
        {
            return null;
        }

        muzzleFlashMaterial = new Material(shader)
        {
            name = "RuntimeMuzzleFlashMaterial",
            hideFlags = HideFlags.HideAndDontSave,
        };

        if (muzzleFlashMaterial.HasProperty("_Surface"))
        {
            muzzleFlashMaterial.SetFloat("_Surface", 1f);
        }

        if (muzzleFlashMaterial.HasProperty("_Blend"))
        {
            muzzleFlashMaterial.SetFloat("_Blend", 0f);
        }

        if (muzzleFlashMaterial.HasProperty("_SrcBlend"))
        {
            muzzleFlashMaterial.SetFloat("_SrcBlend", 5f);
        }

        if (muzzleFlashMaterial.HasProperty("_DstBlend"))
        {
            muzzleFlashMaterial.SetFloat("_DstBlend", 10f);
        }

        if (muzzleFlashMaterial.HasProperty("_ZWrite"))
        {
            muzzleFlashMaterial.SetFloat("_ZWrite", 0f);
        }

        if (muzzleFlashMaterial.HasProperty("_BaseColor"))
        {
            muzzleFlashMaterial.SetColor("_BaseColor", new Color(1f, 0.72f, 0.22f, 0.85f));
        }

        if (muzzleFlashMaterial.HasProperty("_Color"))
        {
            muzzleFlashMaterial.SetColor("_Color", new Color(1f, 0.72f, 0.22f, 0.85f));
        }

        return muzzleFlashMaterial;
    }

    private void OnDestroy()
    {
        if (muzzleFlashMaterial != null)
        {
            Destroy(muzzleFlashMaterial);
        }
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
