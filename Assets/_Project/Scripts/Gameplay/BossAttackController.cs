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

    private BattleController battleController;
    private BossController bossController;
    private PlayerCombatController playerCombatController;
    private GameObject projectileTemplate;
    private float attackTimer = 1f;
    private int attackSequence;

    private void Update()
    {
        if (battleController == null || bossController == null || playerCombatController == null || projectileTemplate == null)
        {
            return;
        }

        if (!battleController.IsBattleActive || !bossController.IsAlive || !playerCombatController.IsAlive)
        {
            return;
        }

        attackTimer -= Time.deltaTime;
        if (attackTimer > 0f)
        {
            return;
        }

        float interval = Mathf.Lerp(enragedAttackInterval, baseAttackInterval, bossController.HealthRatio);
        attackTimer = interval;
        attackSequence++;

        bool useSpreadAttack = bossController.HealthRatio < 0.7f && attackSequence % 3 == 0;
        if (useSpreadAttack)
        {
            FireSpreadBurst();
        }
        else
        {
            Fire();
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
    }

    private void Fire()
    {
        Vector3 origin = firePoint != null ? firePoint.position : bossController.HitPoint;
        Vector3 target = playerCombatController.HitPoint;
        Vector3 direction = (target - origin).normalized;

        SpawnProjectile(origin, direction, projectileSpeed, projectileDamage);
    }

    private void FireSpreadBurst()
    {
        Vector3 origin = firePoint != null ? firePoint.position : bossController.HitPoint;
        Vector3 target = playerCombatController.HitPoint;
        Vector3 forward = (target - origin).normalized;
        Quaternion centerRotation = Quaternion.LookRotation(forward, Vector3.up);

        float step = spreadShotCount > 1 ? spreadAngle / (spreadShotCount - 1) : 0f;
        float start = -spreadAngle * 0.5f;

        for (int i = 0; i < spreadShotCount; i++)
        {
            float angle = start + step * i;
            Vector3 direction = centerRotation * Quaternion.AngleAxis(angle, Vector3.up) * Vector3.forward;
            SpawnProjectile(origin, direction, projectileSpeed * 0.9f, projectileDamage * 0.7f);
        }
    }

    private void SpawnProjectile(Vector3 origin, Vector3 direction, float speed, float damage)
    {
        GameObject projectileInstance = Instantiate(projectileTemplate, origin, Quaternion.LookRotation(direction));
        projectileInstance.name = "BossProjectileRuntime";
        projectileInstance.SetActive(true);

        ProjectileController projectile = projectileInstance.GetComponent<ProjectileController>();
        if (projectile != null)
        {
            projectile.Launch(battleController, ProjectileTeam.Boss, direction, speed, damage);
        }
    }
}
