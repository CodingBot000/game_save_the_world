using UnityEngine;

public enum ProjectileTeam
{
    Player,
    Boss,
}

public class ProjectileController : MonoBehaviour
{
    [SerializeField] private float defaultSpeed = 30f;
    [SerializeField] private float defaultDamage = 10f;
    [SerializeField] private float lifetime = 4f;
    [SerializeField] private float hitRadius = 1f;

    private BattleController battleController;
    private ProjectileTeam team;
    private float speed;
    private float damage;
    private Vector3 velocity;
    private float remainingLifetime;

    public void Launch(BattleController owner, ProjectileTeam projectileTeam, Vector3 direction, float speedOverride, float damageOverride)
    {
        battleController = owner;
        team = projectileTeam;
        speed = speedOverride > 0f ? speedOverride : defaultSpeed;
        damage = damageOverride > 0f ? damageOverride : defaultDamage;
        velocity = direction.normalized * speed;
        remainingLifetime = lifetime;
    }

    private void Update()
    {
        if (battleController == null)
        {
            Destroy(gameObject);
            return;
        }

        transform.position += velocity * Time.deltaTime;
        remainingLifetime -= Time.deltaTime;

        if (remainingLifetime <= 0f || transform.position.magnitude > 120f || transform.position.y < -10f)
        {
            Destroy(gameObject);
            return;
        }

        bool hit = team == ProjectileTeam.Player
            ? battleController.TryHitBoss(transform.position, hitRadius, damage)
            : battleController.TryHitPlayer(transform.position, hitRadius, damage);

        if (hit)
        {
            Destroy(gameObject);
        }
    }
}
