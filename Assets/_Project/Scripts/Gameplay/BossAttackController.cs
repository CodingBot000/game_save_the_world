using System.Collections;
using UnityEngine;

public class BossAttackController : MonoBehaviour
{
    private const string DefaultQuickAttackTrigger = "Attack1";
    private const string DefaultHeavyAttackTrigger = "Attack2";

    [SerializeField] private float baseAttackInterval = 1.8f;
    [SerializeField] private float enragedAttackInterval = 0.9f;
    [SerializeField] private float projectileSpeed = 24f;
    [SerializeField] private float projectileDamage = 15f;
    [SerializeField] private int spreadShotCount = 5;
    [SerializeField] private float spreadAngle = 26f;
    [SerializeField] private Transform firePoint;
    [SerializeField] private BossBulletPatternController bulletPatternController;
    [SerializeField] private Animator bossAnimator;
    [SerializeField] private string quickAttackTrigger = "Attack1";
    [SerializeField] private string heavyAttackTrigger = "Attack2";
    [SerializeField] private float minimumAnimationRetriggerInterval = 0.25f;
    [SerializeField, Min(0.1f)] private float projectileScaleMultiplier = 2.5f;
    [SerializeField] private bool playCosmeticProjectileBurst = true;
    [SerializeField, Min(0)] private int cosmeticProjectileBurstCount = 2;
    [SerializeField, Min(0f)] private float cosmeticProjectileBurstInterval = 0.08f;
    [SerializeField, Min(0.01f)] private float cosmeticProjectileLifetime = 1.1f;
    [SerializeField, Min(0.01f)] private float cosmeticProjectileSpeedMultiplier = 1f;

    private BattleController battleController;
    private BossController bossController;
    private PlayerCombatController playerCombatController;
    private GameObject projectileTemplate;
    private float attackTimer = 1f;
    private float nextAnimationTriggerTime;
    private int attackSequence;
    private bool cinematicPaused;
    private float animatorSpeedBeforeCinematic = 1f;
    private bool hasAnimatorSpeedBeforeCinematic;

    public float BaseProjectileSpeed => projectileSpeed;
    public float BaseProjectileDamage => projectileDamage;
    public float DebugBaseAttackInterval => baseAttackInterval;
    public float DebugEnragedAttackInterval => enragedAttackInterval;
    public int DebugSpreadShotCount => spreadShotCount;
    public float DebugSpreadAngle => spreadAngle;
    public float DebugProjectileScaleMultiplier => projectileScaleMultiplier;
    public float CurrentAttackInterval => Mathf.Lerp(enragedAttackInterval, baseAttackInterval, bossController != null ? bossController.HealthRatio : 1f);
    public Vector3 CurrentFireOrigin => firePoint != null ? firePoint.position : (bossController != null ? bossController.HitPoint : transform.position);
    public Vector3 CurrentBossCenter => bossController != null ? bossController.HitPoint : transform.position;
    public Vector3 CurrentPlayerHitPoint => playerCombatController != null ? playerCombatController.HitPoint : transform.position;

    public event System.Action GameplayAttackStarted;

    public bool CanAttack =>
        !cinematicPaused &&
        battleController != null &&
        bossController != null &&
        playerCombatController != null &&
        projectileTemplate != null &&
        battleController.IsBattleActive &&
        bossController.IsAlive &&
        playerCombatController.IsAlive;

    private void Awake()
    {
        bulletPatternController ??= GetComponent<BossBulletPatternController>();
        ResolveAnimator();
    }

    private void Update()
    {
        // Keep a minimal fallback so older scenes still fire even if the new
        // pattern controller component has not been attached yet.
        if (bulletPatternController != null)
        {
            return;
        }

        if (!CanAttack)
        {
            return;
        }

        attackTimer -= Time.deltaTime;
        if (attackTimer > 0f)
        {
            return;
        }

        attackTimer = CurrentAttackInterval;
        attackSequence++;

        bool useSpreadAttack = bossController.HealthRatio < 0.7f && attackSequence % 3 == 0;
        if (useSpreadAttack)
        {
            FireLegacySpreadBurst();
        }
        else
        {
            FireLegacyDirectShot();
        }
    }

    public void Configure(
        BattleController owner,
        BossController boss,
        PlayerCombatController player,
        GameObject projectileTemplateSource,
        PlayerOrbitController playerOrbit = null)
    {
        battleController = owner;
        bossController = boss;
        playerCombatController = player;
        projectileTemplate = projectileTemplateSource;
        ResolveAnimator();

        if (firePoint == null && bossController != null)
        {
            Transform explicitFirePoint = bossController.transform.Find("AimPoint");
            firePoint = explicitFirePoint != null ? explicitFirePoint : bossController.AimPoint;
        }

        bulletPatternController ??= GetComponent<BossBulletPatternController>();
        if (bulletPatternController != null)
        {
            bulletPatternController.Configure(this, owner, boss, player, playerOrbit);
        }
    }

    public void SetCinematicPaused(bool paused)
    {
        if (cinematicPaused == paused)
        {
            return;
        }

        cinematicPaused = paused;
        ResolveAnimator();
        if (bossAnimator == null)
        {
            return;
        }

        if (paused)
        {
            animatorSpeedBeforeCinematic = bossAnimator.speed;
            hasAnimatorSpeedBeforeCinematic = true;
            bossAnimator.speed = 0f;
        }
        else if (hasAnimatorSpeedBeforeCinematic)
        {
            bossAnimator.speed = animatorSpeedBeforeCinematic;
            hasAnimatorSpeedBeforeCinematic = false;
        }
    }

    public void SetAttackTimingForDebug(float baseInterval, float enragedInterval)
    {
        baseAttackInterval = Mathf.Max(0f, baseInterval);
        enragedAttackInterval = Mathf.Max(0f, enragedInterval);
        attackTimer = Mathf.Min(attackTimer, CurrentAttackInterval);
    }

    public void SetProjectileTuningForDebug(float speed, float damage)
    {
        projectileSpeed = Mathf.Max(0f, speed);
        projectileDamage = Mathf.Max(0f, damage);
    }

    public void SetProjectileScaleMultiplierForDebug(float scaleMultiplier)
    {
        projectileScaleMultiplier = Mathf.Max(0.01f, scaleMultiplier);
    }

    public void SetLegacySpreadForDebug(int shotCount, float angle)
    {
        spreadShotCount = Mathf.Max(1, shotCount);
        spreadAngle = Mathf.Max(0f, angle);
    }

    public void NotifyGameplayAttackStarted()
    {
        GameplayAttackStarted?.Invoke();
    }

    public void PlayQuickAttackAnimation()
    {
        PlayAttackAnimation(
            ResolveTriggerName(quickAttackTrigger, DefaultQuickAttackTrigger),
            ResolveTriggerName(heavyAttackTrigger, DefaultHeavyAttackTrigger));
    }

    public void PlayHeavyAttackAnimation()
    {
        PlayAttackAnimation(
            ResolveTriggerName(heavyAttackTrigger, DefaultHeavyAttackTrigger),
            ResolveTriggerName(quickAttackTrigger, DefaultQuickAttackTrigger));
    }

    public ProjectileController SpawnProjectile(
        Vector3 origin,
        Vector3 direction,
        float speed,
        float damage,
        string runtimeName = "BossProjectileRuntime",
        float scaleMultiplier = 1f,
        bool spawnCosmeticBurst = true)
    {
        if (projectileTemplate == null || battleController == null)
        {
            return null;
        }

        Vector3 normalizedDirection = direction.sqrMagnitude > 0.0001f ? direction.normalized : Vector3.forward;

        // Patterns only provide origin, direction, and tunable values. The projectile prefab
        // continues to own visuals and hitbox behavior so future bullet styles can swap freely.
        GameObject projectileInstance = Instantiate(projectileTemplate, origin, Quaternion.LookRotation(normalizedDirection));
        projectileInstance.name = runtimeName;
        float clampedScaleMultiplier = ResolveProjectileScaleMultiplier(scaleMultiplier);
        if (!Mathf.Approximately(clampedScaleMultiplier, 1f))
        {
            projectileInstance.transform.localScale *= clampedScaleMultiplier;
        }

        projectileInstance.SetActive(true);

        ProjectileController projectile = projectileInstance.GetComponent<ProjectileController>();
        if (projectile != null)
        {
            projectile.SetFallbackHitRadiusMultiplier(clampedScaleMultiplier);
            projectile.Launch(battleController, ProjectileTeam.Boss, normalizedDirection, speed, damage);
        }

        if (spawnCosmeticBurst)
        {
            PlayCosmeticProjectileBurst(origin, normalizedDirection, speed, clampedScaleMultiplier, runtimeName);
        }

        return projectile;
    }

    public void SpawnVisualOnlyProjectile(
        Vector3 origin,
        Vector3 direction,
        float speed,
        float scaleMultiplier = 1f,
        string runtimeName = "BossProjectileVisualOnly",
        float lifetimeOverride = -1f)
    {
        if (projectileTemplate == null)
        {
            return;
        }

        Vector3 normalizedDirection = direction.sqrMagnitude > 0.0001f ? direction.normalized : Vector3.forward;
        SpawnCosmeticProjectile(
            origin,
            normalizedDirection,
            Mathf.Max(0f, speed),
            ResolveProjectileScaleMultiplier(scaleMultiplier),
            runtimeName,
            lifetimeOverride);
    }

    private void FireLegacyDirectShot()
    {
        Vector3 origin = CurrentFireOrigin;
        Vector3 direction = (CurrentPlayerHitPoint - origin).normalized;
        NotifyGameplayAttackStarted();
        PlayQuickAttackAnimation();
        SpawnProjectile(origin, direction, projectileSpeed, projectileDamage);
    }

    private void FireLegacySpreadBurst()
    {
        Vector3 origin = CurrentFireOrigin;
        Vector3 forward = (CurrentPlayerHitPoint - origin).normalized;
        Quaternion centerRotation = Quaternion.LookRotation(forward, Vector3.up);
        float spreadProjectileSpeed = projectileSpeed * 0.9f;

        NotifyGameplayAttackStarted();
        PlayQuickAttackAnimation();

        float step = spreadShotCount > 1 ? spreadAngle / (spreadShotCount - 1) : 0f;
        float start = -spreadAngle * 0.5f;

        for (int i = 0; i < spreadShotCount; i++)
        {
            float angle = start + step * i;
            Vector3 direction = centerRotation * Quaternion.AngleAxis(angle, Vector3.up) * Vector3.forward;
            SpawnProjectile(origin, direction, spreadProjectileSpeed, projectileDamage * 0.7f, spawnCosmeticBurst: false);
        }
    }

    public float ResolveProjectileScaleMultiplier(float scaleMultiplier)
    {
        return Mathf.Max(0.01f, scaleMultiplier) * Mathf.Max(0.01f, projectileScaleMultiplier);
    }

    private void PlayCosmeticProjectileBurst(
        Vector3 origin,
        Vector3 direction,
        float speed,
        float resolvedScaleMultiplier,
        string sourceRuntimeName)
    {
        if (!playCosmeticProjectileBurst || cosmeticProjectileBurstCount <= 0 || projectileTemplate == null)
        {
            return;
        }

        StartCoroutine(SpawnCosmeticProjectileBurstRoutine(
            origin,
            direction,
            Mathf.Max(0f, speed),
            Mathf.Max(0.01f, resolvedScaleMultiplier),
            string.IsNullOrEmpty(sourceRuntimeName) ? "BossProjectileRuntime" : sourceRuntimeName));
    }

    private IEnumerator SpawnCosmeticProjectileBurstRoutine(
        Vector3 origin,
        Vector3 direction,
        float speed,
        float resolvedScaleMultiplier,
        string sourceRuntimeName)
    {
        Vector3 normalizedDirection = direction.sqrMagnitude > 0.0001f ? direction.normalized : Vector3.forward;
        float interval = Mathf.Max(0f, cosmeticProjectileBurstInterval);
        for (int i = 0; i < cosmeticProjectileBurstCount; i++)
        {
            if (interval > 0f)
            {
                yield return new WaitForSeconds(interval);
            }

            SpawnCosmeticProjectile(
                origin,
                normalizedDirection,
                speed * Mathf.Max(0.01f, cosmeticProjectileSpeedMultiplier),
                resolvedScaleMultiplier,
                $"{sourceRuntimeName}_VisualOnly_{i + 1}");
        }
    }

    private void SpawnCosmeticProjectile(
        Vector3 origin,
        Vector3 direction,
        float speed,
        float resolvedScaleMultiplier,
        string runtimeName,
        float lifetimeOverride = -1f)
    {
        GameObject projectileInstance = Instantiate(projectileTemplate, origin, Quaternion.LookRotation(direction));
        projectileInstance.name = runtimeName;
        projectileInstance.transform.localScale *= resolvedScaleMultiplier;
        StripGameplayFromCosmeticProjectile(projectileInstance);
        projectileInstance.SetActive(true);

        BossCosmeticProjectileRuntime runtime = projectileInstance.AddComponent<BossCosmeticProjectileRuntime>();
        float lifetime = lifetimeOverride > 0f ? lifetimeOverride : cosmeticProjectileLifetime;
        runtime.Launch(direction, speed, lifetime);
    }

    private static void StripGameplayFromCosmeticProjectile(GameObject projectileInstance)
    {
        ProjectileController[] projectileControllers = projectileInstance.GetComponentsInChildren<ProjectileController>(true);
        for (int i = 0; i < projectileControllers.Length; i++)
        {
            projectileControllers[i].enabled = false;
            Destroy(projectileControllers[i]);
        }

        Collider[] colliders = projectileInstance.GetComponentsInChildren<Collider>(true);
        for (int i = 0; i < colliders.Length; i++)
        {
            colliders[i].enabled = false;
        }
    }

    private void ResolveAnimator()
    {
        if (bossAnimator != null)
        {
            return;
        }

        bossAnimator = GetComponentInChildren<Animator>(true);
    }

    private void PlayAttackAnimation(string triggerName, string triggerToReset)
    {
        if (string.IsNullOrEmpty(triggerName))
        {
            return;
        }

        ResolveAnimator();
        if (bossAnimator == null || Time.time < nextAnimationTriggerTime)
        {
            return;
        }

        if (!string.IsNullOrEmpty(triggerToReset))
        {
            bossAnimator.ResetTrigger(triggerToReset);
        }

        bossAnimator.ResetTrigger(triggerName);
        bossAnimator.SetTrigger(triggerName);
        nextAnimationTriggerTime = Time.time + Mathf.Max(0f, minimumAnimationRetriggerInterval);
    }

    private static string ResolveTriggerName(string configuredTriggerName, string fallbackTriggerName)
    {
        return string.IsNullOrEmpty(configuredTriggerName) ? fallbackTriggerName : configuredTriggerName;
    }
}

public class BossCosmeticProjectileRuntime : MonoBehaviour
{
    private Vector3 velocity;
    private float remainingLifetime;

    public void Launch(Vector3 direction, float speed, float lifetime)
    {
        Vector3 normalizedDirection = direction.sqrMagnitude > 0.0001f ? direction.normalized : Vector3.forward;
        velocity = normalizedDirection * Mathf.Max(0f, speed);
        remainingLifetime = Mathf.Max(0.05f, lifetime);
    }

    private void Update()
    {
        transform.position += velocity * Time.deltaTime;
        remainingLifetime -= Time.deltaTime;
        if (remainingLifetime <= 0f || transform.position.magnitude > 120f || transform.position.y < -10f)
        {
            Destroy(gameObject);
        }
    }
}
