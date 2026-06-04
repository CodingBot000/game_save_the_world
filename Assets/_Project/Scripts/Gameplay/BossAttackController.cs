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

    private BattleController battleController;
    private BossController bossController;
    private PlayerCombatController playerCombatController;
    private GameObject projectileTemplate;
    private float attackTimer = 1f;
    private float nextAnimationTriggerTime;
    private int attackSequence;

    public float BaseProjectileSpeed => projectileSpeed;
    public float BaseProjectileDamage => projectileDamage;
    public float DebugBaseAttackInterval => baseAttackInterval;
    public float DebugEnragedAttackInterval => enragedAttackInterval;
    public int DebugSpreadShotCount => spreadShotCount;
    public float DebugSpreadAngle => spreadAngle;
    public float CurrentAttackInterval => Mathf.Lerp(enragedAttackInterval, baseAttackInterval, bossController != null ? bossController.HealthRatio : 1f);
    public Vector3 CurrentFireOrigin => firePoint != null ? firePoint.position : (bossController != null ? bossController.HitPoint : transform.position);
    public Vector3 CurrentBossCenter => bossController != null ? bossController.HitPoint : transform.position;
    public Vector3 CurrentPlayerHitPoint => playerCombatController != null ? playerCombatController.HitPoint : transform.position;

    public bool CanAttack =>
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

    public void Configure(BattleController owner, BossController boss, PlayerCombatController player, GameObject projectileTemplateSource)
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
            bulletPatternController.Configure(this, owner, boss, player);
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

    public void SetLegacySpreadForDebug(int shotCount, float angle)
    {
        spreadShotCount = Mathf.Max(1, shotCount);
        spreadAngle = Mathf.Max(0f, angle);
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

    public ProjectileController SpawnProjectile(Vector3 origin, Vector3 direction, float speed, float damage, string runtimeName = "BossProjectileRuntime")
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
        projectileInstance.SetActive(true);

        ProjectileController projectile = projectileInstance.GetComponent<ProjectileController>();
        if (projectile != null)
        {
            projectile.Launch(battleController, ProjectileTeam.Boss, normalizedDirection, speed, damage);
        }

        return projectile;
    }

    private void FireLegacyDirectShot()
    {
        Vector3 origin = CurrentFireOrigin;
        Vector3 direction = (CurrentPlayerHitPoint - origin).normalized;
        PlayQuickAttackAnimation();
        SpawnProjectile(origin, direction, projectileSpeed, projectileDamage);
    }

    private void FireLegacySpreadBurst()
    {
        Vector3 origin = CurrentFireOrigin;
        Vector3 forward = (CurrentPlayerHitPoint - origin).normalized;
        Quaternion centerRotation = Quaternion.LookRotation(forward, Vector3.up);
        float spreadProjectileSpeed = projectileSpeed * 0.9f;

        PlayQuickAttackAnimation();

        float step = spreadShotCount > 1 ? spreadAngle / (spreadShotCount - 1) : 0f;
        float start = -spreadAngle * 0.5f;

        for (int i = 0; i < spreadShotCount; i++)
        {
            float angle = start + step * i;
            Vector3 direction = centerRotation * Quaternion.AngleAxis(angle, Vector3.up) * Vector3.forward;
            SpawnProjectile(origin, direction, spreadProjectileSpeed, projectileDamage * 0.7f);
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
