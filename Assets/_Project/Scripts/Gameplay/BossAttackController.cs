using UnityEngine;

public class BossAttackController : MonoBehaviour
{
    [SerializeField] private float baseAttackInterval = 1.8f;
    [SerializeField] private float enragedAttackInterval = 0.9f;
    [SerializeField] private float projectileSpeed = 24f;
    [SerializeField] private float projectileDamage = 15f;
    [SerializeField] private int spreadShotCount = 5;
    [SerializeField] private float spreadAngle = 26f;
    [SerializeField] private Transform firePoint;
    [SerializeField] private BossBulletPatternController bulletPatternController;

    private BattleController battleController;
    private BossController bossController;
    private PlayerCombatController playerCombatController;
    private GameObject projectileTemplate;
    private float attackTimer = 1f;
    private int attackSequence;

    public float BaseProjectileSpeed => projectileSpeed;
    public float BaseProjectileDamage => projectileDamage;
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
        SpawnProjectile(origin, direction, projectileSpeed, projectileDamage);
    }

    private void FireLegacySpreadBurst()
    {
        Vector3 origin = CurrentFireOrigin;
        Vector3 forward = (CurrentPlayerHitPoint - origin).normalized;
        Quaternion centerRotation = Quaternion.LookRotation(forward, Vector3.up);
        float spreadProjectileSpeed = projectileSpeed * 0.9f;

        float step = spreadShotCount > 1 ? spreadAngle / (spreadShotCount - 1) : 0f;
        float start = -spreadAngle * 0.5f;

        for (int i = 0; i < spreadShotCount; i++)
        {
            float angle = start + step * i;
            Vector3 direction = centerRotation * Quaternion.AngleAxis(angle, Vector3.up) * Vector3.forward;
            SpawnProjectile(origin, direction, spreadProjectileSpeed, projectileDamage * 0.7f);
        }
    }
}
