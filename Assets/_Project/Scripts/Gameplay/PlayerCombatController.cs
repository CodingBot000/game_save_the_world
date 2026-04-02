using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;

public class PlayerCombatController : MonoBehaviour
{
    private static readonly Quaternion PlayerProjectileVisualRotation = Quaternion.Euler(90f, 0f, 0f);
    private const string VehiclePlayerStateCatalogResourcePath = "Vehicles/VehiclePlayerStateCatalog";
    private const string DefaultDamageHurtboxName = "CrashObserver";
    private const string DamageHurtboxDebugProxyName = "__DamageHurtboxDebugProxy";

    [SerializeField] private float fireCooldown = 0.15f;
    [SerializeField] private float projectileSpeed = 60f;
    [SerializeField] private float projectileDamage = 25f;
    [SerializeField] private float invulnerabilityDuration = 0.5f;
    [SerializeField] private float hitRadius = 1.4f;
    [SerializeField] private Collider[] damageHurtboxes = Array.Empty<Collider>();
    [SerializeField] private bool showDamageHurtboxDebugVisual = true;
    [SerializeField] private Color damageHurtboxDebugColor = new(0.1f, 1f, 0.25f, 0.92f);
    [SerializeField] private float damageHurtboxDebugScaleMultiplier = 1.12f;
    [SerializeField] private Transform muzzle;
    [SerializeField] private float missileCooldown = 2.6f;
    [SerializeField] private float missileDamage = 150f;
    [SerializeField] private float missileLaunchSpeed = 18f;
    [SerializeField] private float missileCruiseSpeed = 72f;
    [SerializeField] private float missileAcceleration = 130f;
    [SerializeField] private float missileTurnRate = 280f;
    [SerializeField] private float missileLockOnDelay = 0.2f;
    [SerializeField] private float missileStraightPhaseDuration = 0.2f;
    [SerializeField] private float missileStraightPhaseDistance = 1.0f;
    [SerializeField] private float missileTurnPhaseDuration = 0.4f;
    [SerializeField] private float missileBoostPhaseDuration = 0.6f;
    [SerializeField] private float missileLifetime = 6f;
    [SerializeField] private float missileHitRadius = 1.8f;
    [SerializeField] private GameObject missileVisualTemplate;
    [SerializeField] private GameObject missileSmokeTemplate;
    [SerializeField] private GameObject missileImpactEffectTemplate;
    [SerializeField] private Texture2D missileVisualTexture;
    [SerializeField] private Texture2D missileSmokeTexture;
    [SerializeField] private float missileVisualScale = 0.78f;
    [SerializeField] private float missileSmokeScale = 0.22f;
    [SerializeField] private float missileImpactEffectScale = 0.08f;
    [SerializeField] private bool missileUseTemplateOriginalMaterials;
    [SerializeField] private Color missileTemplateTint = new(0.94f, 0.95f, 0.98f, 1f);
    [SerializeField] private Vector3 missileTemplateLocalEulerAngles = Vector3.zero;
    [SerializeField] private Transform missileLauncherLeft;
    [SerializeField] private Transform missileLauncherRight;

    private BattleController battleController;
    private BossController bossController;
    private GameObject projectileTemplate;
    private Renderer[] cachedRenderers;
    private Color[] rendererBaseColors;
    private float shootCooldownRemaining;
    private float invulnerabilityRemaining;
    private bool combatEnabled = true;
    private float maxHull;
    private float currentHull;
    private float maxArmor;
    private float currentArmor;
    private float armorRepairRate;
    private float armorRepairDelay;
    private float brokenRecoverThreshold;
    private float hullDamageMultiplierWhenBroken = 1f;
    private bool armorBroken;
    private float armorRepairCooldownRemaining;
    private float missileCooldownRemaining;
    private ParticleSystem muzzleFlash;
    private Material muzzleFlashMaterial;
    private Mesh muzzleFlashParticleMesh;
    private float pulseTimer;
    private Vector3 baseScale;
    private bool launchLeftMissileNext = true;
    private VehiclePlayerStateCatalog vehiclePlayerStateCatalog;
    private Material damageHurtboxDebugMaterial;
    private string lastHitDebugSummary = "LastHit: none";

    public event Action Died;

    public bool IsAlive => currentHull > 0f;
    public float CurrentHealth => currentHull;
    public float MaxHealth => maxHull;
    public float CurrentHull => currentHull;
    public float MaxHull => maxHull;
    public float CurrentArmor => currentArmor;
    public float MaxArmor => maxArmor;
    public bool ArmorBroken => armorBroken;
    public float HitRadius => hitRadius;
    public Vector3 HitPoint => TryGetDamageHurtboxBounds(out Bounds bounds) ? bounds.center : transform.position + Vector3.up * 1.2f;
    public string LastHitDebugSummary => lastHitDebugSummary;
    public bool HasMissileLaunchers => ResolveMissileLaunchers();
    public bool MissileSystemAvailable =>
        combatEnabled &&
        IsAlive &&
        battleController != null &&
        bossController != null &&
        bossController.IsAlive &&
        HasMissileLaunchers;
    public bool MissileReady => MissileSystemAvailable && missileCooldownRemaining <= 0f;
    public bool MissileInputAvailable => MissileSystemAvailable && (GameplayDebugFlags.IgnoreMissileCooldown || missileCooldownRemaining <= 0f);
    public float MissileCooldownRemaining => Mathf.Max(0f, missileCooldownRemaining);
    public float MissileCooldownDuration => Mathf.Max(0.01f, missileCooldown);

    public string GetMissileUnavailableReason()
    {
        if (!combatEnabled)
        {
            return "Missile system offline.";
        }

        if (!IsAlive)
        {
            return "Player destroyed.";
        }

        if (!ResolveMissileLaunchers())
        {
            return "Missile launcher offline.";
        }

        if (battleController == null || bossController == null)
        {
            return "Missile lock unavailable.";
        }

        if (!bossController.IsAlive)
        {
            return "No missile target.";
        }

        if (missileCooldownRemaining > 0f && !GameplayDebugFlags.IgnoreMissileCooldown)
        {
            return $"Missile reloading {MissileCooldownRemaining:0.0}s";
        }

        return "Missile blocked.";
    }

    private void Awake()
    {
        ApplySelectedVehicleState(resetRuntimeValues: true);
        baseScale = transform.localScale;
        cachedRenderers = GetComponentsInChildren<Renderer>();
        rendererBaseColors = CacheBaseColors(cachedRenderers);
        ResolveDamageHurtboxes();
        UpdateDamageHurtboxDebugVisuals();
    }

    private void Update()
    {
        shootCooldownRemaining -= Time.deltaTime;
        missileCooldownRemaining -= Time.deltaTime;
        invulnerabilityRemaining -= Time.deltaTime;
        pulseTimer = Mathf.Max(0f, pulseTimer - Time.deltaTime * 5f);
        transform.localScale = baseScale * (1f + pulseTimer * 0.06f);
        UpdateArmorRepair();

        if (!combatEnabled || !IsAlive || battleController == null || bossController == null || !bossController.IsAlive)
        {
            return;
        }

        Mouse mouse = Mouse.current;
        Keyboard keyboard = Keyboard.current;
        bool pointerOverUi = EventSystem.current != null && EventSystem.current.IsPointerOverGameObject();
        bool mouseFire = mouse != null && mouse.leftButton.isPressed && !pointerOverUi;
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
        ApplySelectedVehicleState(resetRuntimeValues: true);

        if (muzzle == null)
        {
            Transform foundMuzzle = transform.Find("Muzzle");
            if (foundMuzzle != null)
            {
                muzzle = foundMuzzle;
            }
        }

        ResolveMissileLaunchers();
        ResolveDamageHurtboxes();
        UpdateDamageHurtboxDebugVisuals();
        EnsureMuzzleFlash();
    }

    public void RefreshVisualBindings()
    {
        cachedRenderers = GetComponentsInChildren<Renderer>(true);
        rendererBaseColors = CacheBaseColors(cachedRenderers);
        ResolveMissileLaunchers();
        ResolveDamageHurtboxes();
        UpdateDamageHurtboxDebugVisuals();
        EnsureMuzzleFlash();
    }

    public void SetCombatEnabled(bool enabled)
    {
        combatEnabled = enabled;
    }

    public bool ApplyDamage(float damage)
    {
        if (!IsAlive || invulnerabilityRemaining > 0f || damage <= 0f)
        {
            return false;
        }

        armorRepairCooldownRemaining = armorRepairDelay;

        float remainingDamage = damage;
        if (!armorBroken && currentArmor > 0f)
        {
            currentArmor -= remainingDamage;
            if (currentArmor <= 0f)
            {
                remainingDamage = -currentArmor;
                currentArmor = 0f;
                armorBroken = true;
            }
            else
            {
                remainingDamage = 0f;
            }
        }

        if (armorBroken && remainingDamage > 0f)
        {
            float hullDamage = remainingDamage * Mathf.Max(0.01f, hullDamageMultiplierWhenBroken);
            currentHull = Mathf.Max(0f, currentHull - hullDamage);
        }

        invulnerabilityRemaining = invulnerabilityDuration;
        pulseTimer = 1f;
        ApplyTint(Color.red);
        Invoke(nameof(RestoreBaseColors), 0.12f);

        if (currentHull <= 0f)
        {
            Died?.Invoke();
        }

        return true;
    }

    public bool CheckHit(Vector3 worldPoint, float projectileHitRadius, Collider projectileCollider = null)
    {
        if (projectileCollider != null && TryCheckHurtboxColliderHit(projectileCollider, out bool colliderHit))
        {
            return colliderHit;
        }

        float clampedProjectileHitRadius = Mathf.Max(0f, projectileHitRadius);
        if (TryCheckHurtboxHit(worldPoint, clampedProjectileHitRadius, out bool hit))
        {
            return hit;
        }

        return Vector3.Distance(worldPoint, HitPoint) <= clampedProjectileHitRadius + hitRadius;
    }

    private void ApplySelectedVehicleState(bool resetRuntimeValues)
    {
        HelicopterSelectionState selectionState = HelicopterSelectionState.EnsureInitialized();
        VehicleDefinition selectedVehicle = selectionState != null ? selectionState.EnsureSelectedHelicopter() : null;
        string selectedVehicleId = selectedVehicle != null ? selectedVehicle.Id : string.Empty;

        VehiclePlayerStateDefinition stateDefinition = LoadVehiclePlayerStateCatalog()?.GetState(selectedVehicleId);
        if (stateDefinition == null)
        {
            Debug.LogWarning("Vehicle player state catalog is missing. Player defensive state was not configured.");
            return;
        }

        maxHull = Mathf.Max(1f, stateDefinition.HullHp);
        maxArmor = Mathf.Max(0f, stateDefinition.ArmorHp);
        armorRepairRate = Mathf.Max(0f, stateDefinition.RepairRate);
        armorRepairDelay = Mathf.Max(0f, stateDefinition.RepairDelay);
        brokenRecoverThreshold = Mathf.Clamp(stateDefinition.BrokenRecoverThreshold, 0f, maxArmor);
        hullDamageMultiplierWhenBroken = Mathf.Max(0.01f, stateDefinition.HullDamageMultiplierWhenBroken);

        if (!resetRuntimeValues)
        {
            return;
        }

        currentHull = maxHull;
        currentArmor = maxArmor;
        armorBroken = currentArmor <= 0f;
        armorRepairCooldownRemaining = armorRepairDelay;
        invulnerabilityRemaining = 0f;
    }

    private VehiclePlayerStateCatalog LoadVehiclePlayerStateCatalog()
    {
        if (vehiclePlayerStateCatalog == null)
        {
            vehiclePlayerStateCatalog = Resources.Load<VehiclePlayerStateCatalog>(VehiclePlayerStateCatalogResourcePath);
        }

        return vehiclePlayerStateCatalog;
    }

    private void UpdateArmorRepair()
    {
        if (!IsAlive || maxArmor <= 0f)
        {
            return;
        }

        armorRepairCooldownRemaining = Mathf.Max(0f, armorRepairCooldownRemaining - Time.deltaTime);
        if (armorRepairCooldownRemaining > 0f || armorRepairRate <= 0f || currentArmor >= maxArmor)
        {
            return;
        }

        currentArmor = Mathf.Min(maxArmor, currentArmor + armorRepairRate * Time.deltaTime);
        if (armorBroken && currentArmor >= brokenRecoverThreshold)
        {
            armorBroken = false;
        }
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

    public bool TryFireMissile()
    {
        if (!MissileSystemAvailable)
        {
            return false;
        }

        if (missileCooldownRemaining > 0f && !GameplayDebugFlags.IgnoreMissileCooldown)
        {
            return false;
        }

        Transform launchTransform = SelectNextMissileLauncher();
        if (launchTransform == null)
        {
            return false;
        }

        Vector3 launchDirection = GetMissileLaunchDirection();
        float boostAcceleration = Mathf.Max(
            missileAcceleration,
            Mathf.Abs(missileCruiseSpeed - missileLaunchSpeed) / Mathf.Max(0.01f, missileBoostPhaseDuration));

        missileCooldownRemaining = Mathf.Max(0.1f, missileCooldown);

        GameObject missileInstance = new("PlayerMissileRuntime");
        missileInstance.transform.position = launchTransform.position;
        missileInstance.transform.rotation = Quaternion.LookRotation(launchDirection.normalized, Vector3.up);

        HomingMissileController missile = missileInstance.AddComponent<HomingMissileController>();
        missile.Launch(
            battleController,
            bossController.AimPoint,
            ProjectileTeam.Player,
            launchDirection,
            missileLaunchSpeed,
            missileCruiseSpeed,
            boostAcceleration,
            missileTurnRate,
            missileLockOnDelay,
            missileStraightPhaseDuration,
            missileStraightPhaseDistance,
            missileTurnPhaseDuration,
            missileBoostPhaseDuration,
            missileLifetime,
            missileDamage,
            missileHitRadius,
            missileVisualTemplate,
            missileSmokeTemplate,
            missileImpactEffectTemplate,
            missileVisualTexture,
            missileSmokeTexture,
            missileVisualScale,
            missileSmokeScale,
            missileImpactEffectScale,
            missileUseTemplateOriginalMaterials,
            missileTemplateTint,
            missileTemplateLocalEulerAngles);
        return true;
    }

    private bool ResolveMissileLaunchers()
    {
        if (missileLauncherLeft == null)
        {
            missileLauncherLeft = transform.Find("MissileLauncherLeft");
        }

        if (missileLauncherRight == null)
        {
            missileLauncherRight = transform.Find("MissileLauncherRight");
        }

        return missileLauncherLeft != null || missileLauncherRight != null;
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

    private void UpdateDamageHurtboxDebugVisuals()
    {
        if (damageHurtboxes == null || damageHurtboxes.Length == 0)
        {
            return;
        }

        Material debugMaterial = showDamageHurtboxDebugVisual ? GetOrCreateDamageHurtboxDebugMaterial() : null;
        for (int i = 0; i < damageHurtboxes.Length; i++)
        {
            Collider hurtbox = damageHurtboxes[i];
            if (hurtbox == null)
            {
                continue;
            }

            MeshRenderer meshRenderer = hurtbox.GetComponent<MeshRenderer>();
            if (meshRenderer == null)
            {
                continue;
            }

            meshRenderer.enabled = false;
            if (debugMaterial != null)
            {
                SyncDamageHurtboxDebugProxy(hurtbox, debugMaterial);
            }
            else
            {
                SetDamageHurtboxDebugProxyActive(hurtbox.transform, false);
            }
        }
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
            float sqrDistance = (closestPoint - worldPoint).sqrMagnitude;
            if (sqrDistance <= maxDistanceSqr)
            {
                RecordApproximateHitDebugInfo(worldPoint, closestPoint, projectileHitRadius, hurtbox, Mathf.Sqrt(sqrDistance));
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
                out float penetrationDistance))
            {
                RecordExactHitDebugInfo(projectileCollider, hurtbox, penetrationDistance);
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

    private Transform SelectNextMissileLauncher()
    {
        ResolveMissileLaunchers();

        if (missileLauncherLeft != null && missileLauncherRight != null)
        {
            Transform selected = launchLeftMissileNext ? missileLauncherLeft : missileLauncherRight;
            launchLeftMissileNext = !launchLeftMissileNext;
            return selected;
        }

        if (missileLauncherLeft != null)
        {
            return missileLauncherLeft;
        }

        if (missileLauncherRight != null)
        {
            return missileLauncherRight;
        }

        return null;
    }

    private Vector3 GetMissileLaunchDirection()
    {
        Camera referenceCamera = Camera.main;
        if (referenceCamera != null)
        {
            Vector3 screenRight = Vector3.ProjectOnPlane(referenceCamera.transform.right, Vector3.up);
            if (screenRight.sqrMagnitude > 0.001f)
            {
                return screenRight.normalized;
            }
        }

        Vector3 fallback = Vector3.ProjectOnPlane(transform.right, Vector3.up);
        if (fallback.sqrMagnitude > 0.001f)
        {
            return fallback.normalized;
        }

        return Vector3.right;
    }

    private void EnsureMuzzleFlash()
    {
        if (muzzle == null)
        {
            return;
        }

        if (muzzleFlash == null)
        {
            Transform existing = muzzle.Find("MuzzleFlash");
            if (existing != null)
            {
                muzzleFlash = existing.GetComponent<ParticleSystem>();
            }
        }

        if (muzzleFlash == null)
        {
            GameObject flashObject = new("MuzzleFlash");
            flashObject.transform.SetParent(muzzle, false);
            flashObject.transform.localPosition = new Vector3(0f, 0f, 0.08f);
            flashObject.transform.localRotation = Quaternion.identity;
            muzzleFlash = flashObject.AddComponent<ParticleSystem>();
        }

        muzzleFlash.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        muzzleFlash.transform.localPosition = new Vector3(0f, 0f, 0.08f);
        muzzleFlash.transform.localRotation = Quaternion.identity;

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

    private Material GetOrCreateDamageHurtboxDebugMaterial()
    {
        if (damageHurtboxDebugMaterial != null)
        {
            ApplyDamageHurtboxDebugMaterialColor(damageHurtboxDebugMaterial);
            return damageHurtboxDebugMaterial;
        }

        Shader shader = Shader.Find("Universal Render Pipeline/Unlit");
        shader ??= Shader.Find("Sprites/Default");
        shader ??= Shader.Find("Universal Render Pipeline/Lit");
        shader ??= Shader.Find("Standard");
        if (shader == null)
        {
            return null;
        }

        damageHurtboxDebugMaterial = new Material(shader)
        {
            name = "RuntimeDamageHurtboxDebugMaterial",
            hideFlags = HideFlags.HideAndDontSave,
            renderQueue = 3000
        };

        if (damageHurtboxDebugMaterial.HasProperty("_Surface"))
        {
            damageHurtboxDebugMaterial.SetFloat("_Surface", 1f);
        }

        if (damageHurtboxDebugMaterial.HasProperty("_Blend"))
        {
            damageHurtboxDebugMaterial.SetFloat("_Blend", 0f);
        }

        if (damageHurtboxDebugMaterial.HasProperty("_SrcBlend"))
        {
            damageHurtboxDebugMaterial.SetFloat("_SrcBlend", 5f);
        }

        if (damageHurtboxDebugMaterial.HasProperty("_DstBlend"))
        {
            damageHurtboxDebugMaterial.SetFloat("_DstBlend", 10f);
        }

        if (damageHurtboxDebugMaterial.HasProperty("_ZWrite"))
        {
            damageHurtboxDebugMaterial.SetFloat("_ZWrite", 0f);
        }

        if (damageHurtboxDebugMaterial.HasProperty("_ZTest"))
        {
            damageHurtboxDebugMaterial.SetFloat("_ZTest", 8f);
        }

        if (damageHurtboxDebugMaterial.HasProperty("_Cull"))
        {
            damageHurtboxDebugMaterial.SetFloat("_Cull", 0f);
        }

        ApplyDamageHurtboxDebugMaterialColor(damageHurtboxDebugMaterial);
        return damageHurtboxDebugMaterial;
    }

    private void ApplyDamageHurtboxDebugMaterialColor(Material material)
    {
        if (material == null)
        {
            return;
        }

        Color debugColor = damageHurtboxDebugColor;
        debugColor.a = Mathf.Max(0.85f, debugColor.a);

        if (material.HasProperty("_BaseColor"))
        {
            material.SetColor("_BaseColor", debugColor);
        }

        if (material.HasProperty("_Color"))
        {
            material.SetColor("_Color", debugColor);
        }
    }

    private void SyncDamageHurtboxDebugProxy(Collider hurtbox, Material debugMaterial)
    {
        if (hurtbox == null)
        {
            return;
        }

        Transform proxyTransform = hurtbox.transform.Find(DamageHurtboxDebugProxyName);
        if (!showDamageHurtboxDebugVisual)
        {
            SetDamageHurtboxDebugProxyActive(proxyTransform, false);
            return;
        }

        MeshFilter sourceMeshFilter = hurtbox.GetComponent<MeshFilter>();
        MeshRenderer sourceMeshRenderer = hurtbox.GetComponent<MeshRenderer>();
        if (sourceMeshFilter == null || sourceMeshFilter.sharedMesh == null || sourceMeshRenderer == null)
        {
            return;
        }

        if (proxyTransform == null)
        {
            GameObject proxyObject = new(DamageHurtboxDebugProxyName);
            proxyTransform = proxyObject.transform;
            proxyTransform.SetParent(hurtbox.transform, false);
            proxyTransform.localPosition = Vector3.zero;
            proxyTransform.localRotation = Quaternion.identity;

            MeshFilter proxyMeshFilter = proxyObject.AddComponent<MeshFilter>();
            proxyMeshFilter.sharedMesh = sourceMeshFilter.sharedMesh;
            proxyObject.AddComponent<MeshRenderer>();
        }

        proxyTransform.localScale = Vector3.one * Mathf.Max(1f, damageHurtboxDebugScaleMultiplier);
        MeshRenderer proxyRenderer = proxyTransform.GetComponent<MeshRenderer>();
        proxyRenderer.sharedMaterial = debugMaterial;
        proxyRenderer.shadowCastingMode = ShadowCastingMode.Off;
        proxyRenderer.receiveShadows = false;
        proxyRenderer.enabled = true;
        proxyTransform.gameObject.SetActive(true);
    }

    private static void SetDamageHurtboxDebugProxyActive(Transform proxyTransform, bool active)
    {
        if (proxyTransform == null)
        {
            return;
        }

        proxyTransform.gameObject.SetActive(active);
    }

    private void RecordExactHitDebugInfo(Collider projectileCollider, Collider hurtbox, float penetrationDistance)
    {
        Vector3 projectileCenter = projectileCollider != null ? projectileCollider.bounds.center : Vector3.zero;
        Vector3 hurtboxCenter = hurtbox != null ? hurtbox.bounds.center : Vector3.zero;
        lastHitDebugSummary =
            $"LastHit: exact pen {penetrationDistance:0.00}\n" +
            $"Proj {FormatDebugVector(projectileCenter)} Hurt {FormatDebugVector(hurtboxCenter)}";
    }

    private void RecordApproximateHitDebugInfo(Vector3 worldPoint, Vector3 closestPoint, float projectileHitRadius, Collider hurtbox, float distance)
    {
        string hurtboxName = hurtbox != null ? hurtbox.name : "unknown";
        lastHitDebugSummary =
            $"LastHit: approx d {distance:0.00} / r {projectileHitRadius:0.00}\n" +
            $"{hurtboxName} {FormatDebugVector(closestPoint)} from {FormatDebugVector(worldPoint)}";
    }

    private static string FormatDebugVector(Vector3 value)
    {
        return $"({value.x:0.0}, {value.y:0.0}, {value.z:0.0})";
    }

    private void OnDestroy()
    {
        if (muzzleFlashMaterial != null)
        {
            Destroy(muzzleFlashMaterial);
        }

        if (damageHurtboxDebugMaterial != null)
        {
            Destroy(damageHurtboxDebugMaterial);
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
