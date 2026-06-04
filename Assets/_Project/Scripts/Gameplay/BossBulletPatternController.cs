using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public enum BossBulletPatternType
{
    FanSpread,
    AimedBurst,
    SpiralRing,
    FallingBomb,
    SplitShot,
}

[System.Serializable]
public sealed class BossBulletPatternDefinition
{
    public string displayName = "Pattern";
    public BossBulletPatternType patternType = BossBulletPatternType.FanSpread;
    public bool enabled = true;
    [Range(0f, 1f)] public float minHealthRatio = 0f;
    [Range(0f, 1f)] public float maxHealthRatio = 1f;
    [Min(0.1f)] public float cooldownMultiplier = 1f;
    [Min(1)] public int projectileCount = 1;
    [Min(1)] public int secondaryProjectileCount = 1;
    [Min(1)] public int burstCount = 1;
    [Min(0f)] public float burstInterval = 0.25f;
    [Min(0f)] public float spreadAngle = 30f;
    [Min(0.1f)] public float speedMultiplier = 1f;
    [Min(0.1f)] public float secondarySpeedMultiplier = 1f;
    [Min(0.1f)] public float damageMultiplier = 1f;
    [Min(0.1f)] public float secondaryDamageMultiplier = 1f;
    [Min(0f)] public float ringRotationStep = 18f;
    [Min(0.05f)] public float telegraphDuration = 0.35f;
    [Min(0.05f)] public float flashingDuration = 0.4f;
    [Min(0.1f)] public float warningWidth = 2.6f;
    [Min(0.1f)] public float warningHeight = 10f;
    [Min(0.1f)] public float warningDepth = 1.3f;
    [Min(0.1f)] public float overheadHeight = 12f;
    [Min(0.1f)] public float splitDistance = 11f;
}

public class BossBulletPatternController : MonoBehaviour
{
    [SerializeField] private float startupDelay = 1f;
    [SerializeField] private float aimedBurstShotInterval = 0.14f;
    // Thin line to mimic a weapon laser sight rather than a chunky warning beam.
    [SerializeField] private float warningLineThickness = 0.045f;
    // Keep pattern fields data-shaped so later balancing can move out to ScriptableObjects
    // or per-boss assets without rewriting the actual spawn/execution code.
    [SerializeField] private List<BossBulletPatternDefinition> patternSequence = new();

    private readonly List<GameObject> runtimeTelegraphs = new();

    private BossAttackController attackController;
    private BattleController battleController;
    private BossController bossController;
    private PlayerCombatController playerCombatController;
    private Coroutine activePatternRoutine;
    private Material telegraphMaterialTemplate;
    private float attackCooldownRemaining;
    private int nextPatternIndex;
    private float spiralRotationDegrees;

    public float DebugStartupDelay => startupDelay;
    public float DebugAimedBurstShotInterval => aimedBurstShotInterval;
    public float DebugWarningLineThickness => warningLineThickness;
    public IReadOnlyList<BossBulletPatternDefinition> DebugPatternSequence
    {
        get
        {
            EnsureDefaultPatterns();
            return patternSequence;
        }
    }

    private void Reset()
    {
        EnsureDefaultPatterns();
    }

    private void Awake()
    {
        EnsureDefaultPatterns();
        attackCooldownRemaining = startupDelay;
    }

    private void OnDisable()
    {
        CancelActivePattern();
        CleanupTelegraphs();
    }

    public void Configure(BossAttackController attack, BattleController battle, BossController boss, PlayerCombatController player)
    {
        attackController = attack;
        battleController = battle;
        bossController = boss;
        playerCombatController = player;
        attackCooldownRemaining = Mathf.Max(attackCooldownRemaining, startupDelay);
    }

    public void SetTimingForDebug(float initialStartupDelay, float fallbackAimedBurstShotInterval, float telegraphLineThickness)
    {
        startupDelay = Mathf.Max(0f, initialStartupDelay);
        aimedBurstShotInterval = Mathf.Max(0f, fallbackAimedBurstShotInterval);
        warningLineThickness = Mathf.Max(0f, telegraphLineThickness);
        attackCooldownRemaining = Mathf.Max(0f, attackCooldownRemaining);
    }

    public void SetPatternEnabledForDebug(int patternIndex, bool value)
    {
        BossBulletPatternDefinition pattern = GetPatternForDebug(patternIndex);
        if (pattern != null)
        {
            pattern.enabled = value;
        }
    }

    public void SetPatternFloatForDebug(int patternIndex, BossPatternTuningKey key, float value)
    {
        BossBulletPatternDefinition pattern = GetPatternForDebug(patternIndex);
        if (pattern == null)
        {
            return;
        }

        float clampedValue = Mathf.Max(0f, value);
        switch (key)
        {
            case BossPatternTuningKey.MinHealthRatio:
                pattern.minHealthRatio = Mathf.Clamp01(clampedValue);
                break;
            case BossPatternTuningKey.MaxHealthRatio:
                pattern.maxHealthRatio = Mathf.Clamp01(clampedValue);
                break;
            case BossPatternTuningKey.CooldownMultiplier:
                pattern.cooldownMultiplier = clampedValue;
                break;
            case BossPatternTuningKey.BurstInterval:
                pattern.burstInterval = clampedValue;
                break;
            case BossPatternTuningKey.SpreadAngle:
                pattern.spreadAngle = clampedValue;
                break;
            case BossPatternTuningKey.SpeedMultiplier:
                pattern.speedMultiplier = clampedValue;
                break;
            case BossPatternTuningKey.SecondarySpeedMultiplier:
                pattern.secondarySpeedMultiplier = clampedValue;
                break;
            case BossPatternTuningKey.DamageMultiplier:
                pattern.damageMultiplier = clampedValue;
                break;
            case BossPatternTuningKey.SecondaryDamageMultiplier:
                pattern.secondaryDamageMultiplier = clampedValue;
                break;
            case BossPatternTuningKey.RingRotationStep:
                pattern.ringRotationStep = clampedValue;
                break;
            case BossPatternTuningKey.TelegraphDuration:
                pattern.telegraphDuration = clampedValue;
                break;
            case BossPatternTuningKey.FlashingDuration:
                pattern.flashingDuration = clampedValue;
                break;
            case BossPatternTuningKey.WarningWidth:
                pattern.warningWidth = clampedValue;
                break;
            case BossPatternTuningKey.WarningHeight:
                pattern.warningHeight = clampedValue;
                break;
            case BossPatternTuningKey.WarningDepth:
                pattern.warningDepth = clampedValue;
                break;
            case BossPatternTuningKey.OverheadHeight:
                pattern.overheadHeight = clampedValue;
                break;
            case BossPatternTuningKey.SplitDistance:
                pattern.splitDistance = clampedValue;
                break;
        }
    }

    public void SetPatternIntForDebug(int patternIndex, BossPatternTuningKey key, int value)
    {
        BossBulletPatternDefinition pattern = GetPatternForDebug(patternIndex);
        if (pattern == null)
        {
            return;
        }

        int clampedValue = Mathf.Max(1, value);
        switch (key)
        {
            case BossPatternTuningKey.ProjectileCount:
                pattern.projectileCount = clampedValue;
                break;
            case BossPatternTuningKey.SecondaryProjectileCount:
                pattern.secondaryProjectileCount = clampedValue;
                break;
            case BossPatternTuningKey.BurstCount:
                pattern.burstCount = clampedValue;
                break;
        }
    }

    public void CancelActivePatternForDebug()
    {
        CancelActivePattern();
        CleanupTelegraphs();
    }

    private BossBulletPatternDefinition GetPatternForDebug(int patternIndex)
    {
        EnsureDefaultPatterns();
        if (patternSequence == null || patternIndex < 0 || patternIndex >= patternSequence.Count)
        {
            return null;
        }

        return patternSequence[patternIndex];
    }

    private void Update()
    {
        if (!CanRunPatterns())
        {
            CancelActivePattern();
            CleanupTelegraphs();
            return;
        }

        if (activePatternRoutine != null)
        {
            return;
        }

        attackCooldownRemaining -= Time.deltaTime;
        if (attackCooldownRemaining > 0f)
        {
            return;
        }

        BossBulletPatternDefinition pattern = SelectNextPattern();
        if (pattern == null)
        {
            attackCooldownRemaining = ResolveCooldown(1f);
            return;
        }

        activePatternRoutine = StartCoroutine(ExecutePatternRoutine(pattern));
    }

    private bool CanRunPatterns()
    {
        return enabled &&
               attackController != null &&
               battleController != null &&
               bossController != null &&
               playerCombatController != null &&
               attackController.CanAttack;
    }

    private void EnsureDefaultPatterns()
    {
        if (patternSequence != null && patternSequence.Count > 0)
        {
            return;
        }

        patternSequence = new List<BossBulletPatternDefinition>
        {
            new()
            {
                displayName = "Fan Spread",
                patternType = BossBulletPatternType.FanSpread,
                enabled = true,
                minHealthRatio = 0.5f,
                maxHealthRatio = 1f,
                cooldownMultiplier = 1f,
                projectileCount = 7,
                burstCount = 3,
                burstInterval = 0.4f,
                spreadAngle = 60f,
                speedMultiplier = 0.5f,
                damageMultiplier = 1f,
            },
            new()
            {
                displayName = "Aimed Burst",
                patternType = BossBulletPatternType.AimedBurst,
                enabled = true,
                minHealthRatio = 0.4f,
                maxHealthRatio = 1f,
                cooldownMultiplier = 0.95f,
                projectileCount = 3,
                burstInterval = 0.14f,
                speedMultiplier = 1.3f,
                damageMultiplier = 0.9f,
                telegraphDuration = 0.3f,
            },
            new()
            {
                displayName = "Spiral Ring",
                patternType = BossBulletPatternType.SpiralRing,
                enabled = true,
                minHealthRatio = 0.25f,
                maxHealthRatio = 0.8f,
                cooldownMultiplier = 1.2f,
                projectileCount = 12,
                speedMultiplier = 0.8f,
                damageMultiplier = 0.8f,
                ringRotationStep = 18f,
            },
            new()
            {
                displayName = "Falling Bomb",
                patternType = BossBulletPatternType.FallingBomb,
                enabled = true,
                minHealthRatio = 0f,
                maxHealthRatio = 0.55f,
                cooldownMultiplier = 1.25f,
                speedMultiplier = 1f,
                damageMultiplier = 1.8f,
                telegraphDuration = 2f,
                flashingDuration = 0.6f,
                warningWidth = 2.8f,
                warningHeight = 10f,
                warningDepth = 1.4f,
            },
            new()
            {
                displayName = "Split Shot",
                patternType = BossBulletPatternType.SplitShot,
                enabled = true,
                minHealthRatio = 0f,
                maxHealthRatio = 0.45f,
                cooldownMultiplier = 1.1f,
                projectileCount = 1,
                secondaryProjectileCount = 5,
                spreadAngle = 70f,
                speedMultiplier = 0.6f,
                secondarySpeedMultiplier = 0.9f,
                damageMultiplier = 0.9f,
                secondaryDamageMultiplier = 0.75f,
                splitDistance = 11f,
            },
        };
    }

    private BossBulletPatternDefinition SelectNextPattern()
    {
        if (patternSequence == null || patternSequence.Count == 0)
        {
            return null;
        }

        float healthRatio = bossController != null ? bossController.HealthRatio : 1f;
        int count = patternSequence.Count;

        for (int offset = 0; offset < count; offset++)
        {
            int index = (nextPatternIndex + offset) % count;
            BossBulletPatternDefinition candidate = patternSequence[index];
            if (!IsPatternEligible(candidate, healthRatio))
            {
                continue;
            }

            nextPatternIndex = (index + 1) % count;
            return candidate;
        }

        return null;
    }

    private static bool IsPatternEligible(BossBulletPatternDefinition pattern, float healthRatio)
    {
        if (pattern == null || !pattern.enabled)
        {
            return false;
        }

        float min = Mathf.Min(pattern.minHealthRatio, pattern.maxHealthRatio);
        float max = Mathf.Max(pattern.minHealthRatio, pattern.maxHealthRatio);
        return healthRatio >= min && healthRatio <= max;
    }

    private IEnumerator ExecutePatternRoutine(BossBulletPatternDefinition pattern)
    {
        yield return pattern.patternType switch
        {
            BossBulletPatternType.FanSpread => ExecuteFanSpread(pattern),
            BossBulletPatternType.AimedBurst => ExecuteAimedBurst(pattern),
            BossBulletPatternType.SpiralRing => ExecuteSpiralRing(pattern),
            BossBulletPatternType.FallingBomb => ExecuteFallingBomb(pattern),
            BossBulletPatternType.SplitShot => ExecuteSplitShot(pattern),
            _ => ExecuteFanSpread(pattern),
        };

        attackCooldownRemaining = ResolveCooldown(pattern.cooldownMultiplier);
        activePatternRoutine = null;
    }

    private IEnumerator ExecuteFanSpread(BossBulletPatternDefinition pattern)
    {
        float projectileSpeed = ResolvePrimarySpeed(pattern);
        float projectileDamage = ResolvePrimaryDamage(pattern);

        for (int burstIndex = 0; burstIndex < Mathf.Max(1, pattern.burstCount); burstIndex++)
        {
            Vector3 origin = attackController.CurrentFireOrigin;
            Vector3 target = playerCombatController.HitPoint;
            attackController.PlayQuickAttackAnimation();
            SpawnSpread(origin, target, Mathf.Max(1, pattern.projectileCount), pattern.spreadAngle, projectileSpeed, projectileDamage);

            if (burstIndex + 1 < Mathf.Max(1, pattern.burstCount))
            {
                yield return new WaitForSeconds(pattern.burstInterval);
            }
        }
    }

    private IEnumerator ExecuteAimedBurst(BossBulletPatternDefinition pattern)
    {
        Vector3 warningOrigin = attackController.CurrentFireOrigin;
        Vector3 warningTarget = playerCombatController.HitPoint;
        GameObject warning = CreateLineTelegraph(
            warningOrigin,
            warningTarget,
            warningLineThickness,
            new Color(1f, 0.08f, 0.08f, 0.3f));
        yield return new WaitForSeconds(pattern.telegraphDuration);
        DestroyTelegraph(warning);

        float projectileSpeed = ResolvePrimarySpeed(pattern);
        float projectileDamage = ResolvePrimaryDamage(pattern);
        float shotInterval = pattern.burstInterval > 0f ? pattern.burstInterval : aimedBurstShotInterval;

        for (int shotIndex = 0; shotIndex < Mathf.Max(1, pattern.projectileCount); shotIndex++)
        {
            Vector3 origin = attackController.CurrentFireOrigin;
            Vector3 target = playerCombatController.HitPoint;
            attackController.PlayQuickAttackAnimation();
            attackController.SpawnProjectile(origin, target - origin, projectileSpeed, projectileDamage);

            if (shotIndex + 1 < Mathf.Max(1, pattern.projectileCount))
            {
                yield return new WaitForSeconds(shotInterval);
            }
        }
    }

    private IEnumerator ExecuteSpiralRing(BossBulletPatternDefinition pattern)
    {
        Vector3 origin = attackController.CurrentBossCenter;
        int projectileCount = Mathf.Max(4, pattern.projectileCount);
        float angleStep = 360f / projectileCount;
        float startAngle = spiralRotationDegrees;
        float projectileSpeed = ResolvePrimarySpeed(pattern);
        float projectileDamage = ResolvePrimaryDamage(pattern);

        attackController.PlayHeavyAttackAnimation();

        for (int i = 0; i < projectileCount; i++)
        {
            float angle = startAngle + angleStep * i;
            Vector3 direction = Quaternion.Euler(0f, angle, 0f) * Vector3.forward;
            attackController.SpawnProjectile(origin, direction, projectileSpeed, projectileDamage);
        }

        spiralRotationDegrees = Mathf.Repeat(spiralRotationDegrees + pattern.ringRotationStep, 360f);
        yield break;
    }

    private IEnumerator ExecuteFallingBomb(BossBulletPatternDefinition pattern)
    {
        Vector3 target = playerCombatController.HitPoint;
        attackController.PlayHeavyAttackAnimation();
        GameObject warning = CreateLaneTelegraph(target, pattern);
        yield return new WaitForSeconds(pattern.telegraphDuration);

        float flashInterval = 0.1f;
        float elapsed = 0f;
        bool bright = false;
        while (elapsed < pattern.flashingDuration)
        {
            SetTelegraphColor(warning, bright ? new Color(1f, 0.15f, 0.15f, 0.75f) : new Color(1f, 0.92f, 0.2f, 0.45f));
            bright = !bright;
            elapsed += flashInterval;
            yield return new WaitForSeconds(flashInterval);
        }

        BoxCollider warningCollider = warning != null ? warning.GetComponent<BoxCollider>() : null;
        if (warningCollider != null && playerCombatController.CheckHit(warningCollider.transform.position, 0f, warningCollider))
        {
            playerCombatController.ApplyDamage(ResolvePrimaryDamage(pattern));
        }

        DestroyTelegraph(warning);
    }

    private IEnumerator ExecuteSplitShot(BossBulletPatternDefinition pattern)
    {
        Vector3 origin = attackController.CurrentFireOrigin;
        Vector3 target = playerCombatController.HitPoint;
        attackController.PlayQuickAttackAnimation();
        ProjectileController projectile = attackController.SpawnProjectile(
            origin,
            target - origin,
            ResolvePrimarySpeed(pattern),
            ResolvePrimaryDamage(pattern));

        if (projectile != null)
        {
            BossSplitProjectileRuntime splitRuntime = projectile.gameObject.AddComponent<BossSplitProjectileRuntime>();
            splitRuntime.Configure(
                attackController,
                Mathf.Max(3, pattern.secondaryProjectileCount),
                pattern.spreadAngle,
                pattern.splitDistance,
                ResolveSecondarySpeed(pattern),
                ResolveSecondaryDamage(pattern));
        }

        yield break;
    }

    private void SpawnSpread(Vector3 origin, Vector3 target, int projectileCount, float spreadAngle, float speed, float damage)
    {
        Vector3 forward = (target - origin).normalized;
        Quaternion centerRotation = Quaternion.LookRotation(forward, Vector3.up);
        float step = projectileCount > 1 ? spreadAngle / (projectileCount - 1) : 0f;
        float start = -spreadAngle * 0.5f;

        for (int i = 0; i < projectileCount; i++)
        {
            float angle = start + step * i;
            Vector3 direction = centerRotation * Quaternion.AngleAxis(angle, Vector3.up) * Vector3.forward;
            attackController.SpawnProjectile(origin, direction, speed, damage);
        }
    }

    private GameObject CreateLineTelegraph(Vector3 origin, Vector3 target, float thickness, Color color)
    {
        Vector3 direction = target - origin;
        float length = Mathf.Max(0.1f, direction.magnitude);
        Quaternion rotation = Quaternion.LookRotation(direction.normalized, Vector3.up);
        Vector3 position = origin + direction * 0.5f;
        Vector3 scale = new(thickness, thickness, length);
        return CreateTelegraphPrimitive("BossAimTelegraph", PrimitiveType.Cube, position, rotation, scale, color, false);
    }

    private GameObject CreateLaneTelegraph(Vector3 target, BossBulletPatternDefinition pattern)
    {
        Vector3 position = new(target.x, target.y, target.z);
        Vector3 scale = new(pattern.warningWidth, pattern.warningHeight, pattern.warningDepth);
        return CreateTelegraphPrimitive(
            "BossFallingBombTelegraph",
            PrimitiveType.Cube,
            position,
            Quaternion.identity,
            scale,
            new Color(1f, 0.85f, 0.15f, 0.35f),
            true);
    }

    private GameObject CreateTelegraphPrimitive(
        string objectName,
        PrimitiveType primitiveType,
        Vector3 position,
        Quaternion rotation,
        Vector3 scale,
        Color color,
        bool keepCollider)
    {
        GameObject telegraphObject = GameObject.CreatePrimitive(primitiveType);
        telegraphObject.name = objectName;
        telegraphObject.transform.SetPositionAndRotation(position, rotation);
        telegraphObject.transform.localScale = scale;

        Collider telegraphCollider = telegraphObject.GetComponent<Collider>();
        if (telegraphCollider != null)
        {
            if (!keepCollider)
            {
                Destroy(telegraphCollider);
            }
            else
            {
                telegraphCollider.enabled = true;
            }
        }

        Renderer telegraphRenderer = telegraphObject.GetComponent<Renderer>();
        if (telegraphRenderer != null)
        {
            telegraphRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            telegraphRenderer.receiveShadows = false;
            telegraphRenderer.material = CreateTelegraphMaterialInstance(color);
        }

        runtimeTelegraphs.Add(telegraphObject);
        return telegraphObject;
    }

    private Material CreateTelegraphMaterialInstance(Color color)
    {
        Material template = GetOrCreateTelegraphMaterialTemplate();
        if (template == null)
        {
            return null;
        }

        Material material = new(template) { color = color, hideFlags = HideFlags.HideAndDontSave };
        return material;
    }

    private Material GetOrCreateTelegraphMaterialTemplate()
    {
        if (telegraphMaterialTemplate != null)
        {
            return telegraphMaterialTemplate;
        }

        Shader shader = Shader.Find("Universal Render Pipeline/Unlit");
        shader ??= Shader.Find("Unlit/Color");
        shader ??= Shader.Find("Sprites/Default");
        if (shader == null)
        {
            return null;
        }

        telegraphMaterialTemplate = new Material(shader)
        {
            name = "RuntimeBossTelegraphMaterial",
            hideFlags = HideFlags.HideAndDontSave,
            color = new Color(1f, 0.8f, 0.2f, 0.35f),
        };

        if (telegraphMaterialTemplate.HasProperty("_Surface"))
        {
            telegraphMaterialTemplate.SetFloat("_Surface", 1f);
        }

        if (telegraphMaterialTemplate.HasProperty("_Blend"))
        {
            telegraphMaterialTemplate.SetFloat("_Blend", 0f);
        }

        if (telegraphMaterialTemplate.HasProperty("_SrcBlend"))
        {
            telegraphMaterialTemplate.SetFloat("_SrcBlend", 5f);
        }

        if (telegraphMaterialTemplate.HasProperty("_DstBlend"))
        {
            telegraphMaterialTemplate.SetFloat("_DstBlend", 10f);
        }

        if (telegraphMaterialTemplate.HasProperty("_ZWrite"))
        {
            telegraphMaterialTemplate.SetFloat("_ZWrite", 0f);
        }

        telegraphMaterialTemplate.renderQueue = 3000;
        return telegraphMaterialTemplate;
    }

    private void SetTelegraphColor(GameObject telegraphObject, Color color)
    {
        if (telegraphObject == null)
        {
            return;
        }

        Renderer telegraphRenderer = telegraphObject.GetComponent<Renderer>();
        if (telegraphRenderer == null || telegraphRenderer.material == null)
        {
            return;
        }

        telegraphRenderer.material.color = color;
    }

    private void DestroyTelegraph(GameObject telegraphObject)
    {
        if (telegraphObject == null)
        {
            return;
        }

        runtimeTelegraphs.Remove(telegraphObject);
        Destroy(telegraphObject);
    }

    private void CleanupTelegraphs()
    {
        for (int i = runtimeTelegraphs.Count - 1; i >= 0; i--)
        {
            GameObject telegraphObject = runtimeTelegraphs[i];
            if (telegraphObject != null)
            {
                Destroy(telegraphObject);
            }
        }

        runtimeTelegraphs.Clear();
    }

    private void CancelActivePattern()
    {
        if (activePatternRoutine == null)
        {
            return;
        }

        StopCoroutine(activePatternRoutine);
        activePatternRoutine = null;
    }

    private float ResolveCooldown(float patternCooldownMultiplier)
    {
        float baseCooldown = attackController != null ? attackController.CurrentAttackInterval : 1f;
        return Mathf.Max(0.1f, baseCooldown * Mathf.Max(0.1f, patternCooldownMultiplier));
    }

    private float ResolvePrimarySpeed(BossBulletPatternDefinition pattern)
    {
        return attackController.BaseProjectileSpeed * Mathf.Max(0.1f, pattern.speedMultiplier);
    }

    private float ResolveSecondarySpeed(BossBulletPatternDefinition pattern)
    {
        return attackController.BaseProjectileSpeed * Mathf.Max(0.1f, pattern.secondarySpeedMultiplier);
    }

    private float ResolvePrimaryDamage(BossBulletPatternDefinition pattern)
    {
        return attackController.BaseProjectileDamage * Mathf.Max(0.1f, pattern.damageMultiplier);
    }

    private float ResolveSecondaryDamage(BossBulletPatternDefinition pattern)
    {
        return attackController.BaseProjectileDamage * Mathf.Max(0.1f, pattern.secondaryDamageMultiplier);
    }
}

public class BossSplitProjectileRuntime : MonoBehaviour
{
    private BossAttackController attackController;
    private ProjectileController projectileController;
    private Vector3 startPosition;
    private float splitDistance;
    private float splitAngle;
    private float childSpeed;
    private float childDamage;
    private int childProjectileCount;
    private bool hasSplit;

    public void Configure(
        BossAttackController owner,
        int childCount,
        float childSpreadAngle,
        float distanceBeforeSplit,
        float childProjectileSpeed,
        float childProjectileDamage)
    {
        attackController = owner;
        projectileController = GetComponent<ProjectileController>();
        startPosition = transform.position;
        childProjectileCount = Mathf.Max(1, childCount);
        splitAngle = childSpreadAngle;
        splitDistance = Mathf.Max(0.1f, distanceBeforeSplit);
        childSpeed = Mathf.Max(0.1f, childProjectileSpeed);
        childDamage = Mathf.Max(0.1f, childProjectileDamage);
    }

    private void Update()
    {
        if (hasSplit)
        {
            return;
        }

        if (attackController == null || projectileController == null)
        {
            Destroy(this);
            return;
        }

        if ((transform.position - startPosition).sqrMagnitude < splitDistance * splitDistance)
        {
            return;
        }

        hasSplit = true;
        Vector3 origin = transform.position;
        Quaternion centerRotation = Quaternion.LookRotation(transform.forward, Vector3.up);
        float step = childProjectileCount > 1 ? splitAngle / (childProjectileCount - 1) : 0f;
        float start = -splitAngle * 0.5f;

        for (int i = 0; i < childProjectileCount; i++)
        {
            float angle = start + step * i;
            Vector3 direction = centerRotation * Quaternion.AngleAxis(angle, Vector3.up) * Vector3.forward;
            attackController.SpawnProjectile(origin, direction, childSpeed, childDamage, "BossSplitChildRuntime");
        }

        Destroy(gameObject);
    }
}
