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
    // Keep the hitbox reference separate from the rendered mesh so future
    // projectile patterns can reuse the same Launch flow while changing only prefab visuals.
    [SerializeField] private Collider hitColliderOverride;
    [SerializeField] private bool useColliderBasedHitRadius = true;

    private BattleController battleController;
    private ProjectileTeam team;
    private float speed;
    private float damage;
    private Vector3 velocity;
    private float remainingLifetime;
    private Collider cachedHitCollider;
    private float effectiveHitRadius;

    private void Awake()
    {
        CacheHitCollider();
        effectiveHitRadius = ResolveHitRadius();
    }

    public void Launch(BattleController owner, ProjectileTeam projectileTeam, Vector3 direction, float speedOverride, float damageOverride)
    {
        battleController = owner;
        team = projectileTeam;
        speed = speedOverride > 0f ? speedOverride : defaultSpeed;
        damage = damageOverride > 0f ? damageOverride : defaultDamage;
        velocity = direction.normalized * speed;
        remainingLifetime = lifetime;
        CacheHitCollider();
        effectiveHitRadius = ResolveHitRadius();
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
            ? battleController.TryHitBoss(transform.position, effectiveHitRadius, damage)
            : battleController.TryHitPlayer(transform.position, effectiveHitRadius, damage, cachedHitCollider);

        if (hit)
        {
            Destroy(gameObject);
        }
    }

    private void CacheHitCollider()
    {
        // Prefer an explicit hit collider so the prefab can shrink or stylize visuals
        // without changing gameplay collision. Fall back to root/children for older prefabs.
        cachedHitCollider = hitColliderOverride;
        if (cachedHitCollider == null)
        {
            cachedHitCollider = GetComponent<Collider>();
        }

        if (cachedHitCollider == null)
        {
            cachedHitCollider = GetComponentInChildren<Collider>();
        }
    }

    private float ResolveHitRadius()
    {
        float fallbackRadius = Mathf.Max(0.01f, hitRadius);
        if (!useColliderBasedHitRadius || cachedHitCollider == null || !cachedHitCollider.enabled)
        {
            return fallbackRadius;
        }

        return Mathf.Max(0.01f, CalculateColliderHitRadius(cachedHitCollider));
    }

    private static float CalculateColliderHitRadius(Collider collider)
    {
        if (collider == null)
        {
            return 0f;
        }

        Vector3 lossyScale = collider.transform.lossyScale;
        switch (collider)
        {
            case SphereCollider sphereCollider:
                float sphereScale = Mathf.Max(Mathf.Abs(lossyScale.x), Mathf.Abs(lossyScale.y), Mathf.Abs(lossyScale.z));
                return sphereCollider.radius * sphereScale;

            case CapsuleCollider capsuleCollider:
                float capsuleRadiusScale = capsuleCollider.direction switch
                {
                    0 => Mathf.Max(Mathf.Abs(lossyScale.y), Mathf.Abs(lossyScale.z)),
                    1 => Mathf.Max(Mathf.Abs(lossyScale.x), Mathf.Abs(lossyScale.z)),
                    _ => Mathf.Max(Mathf.Abs(lossyScale.x), Mathf.Abs(lossyScale.y))
                };
                float capsuleHeightScale = capsuleCollider.direction switch
                {
                    0 => Mathf.Abs(lossyScale.x),
                    1 => Mathf.Abs(lossyScale.y),
                    _ => Mathf.Abs(lossyScale.z)
                };
                float capsuleRadius = capsuleCollider.radius * capsuleRadiusScale;
                float capsuleHalfHeight = capsuleCollider.height * capsuleHeightScale * 0.5f;
                return Mathf.Max(capsuleRadius, capsuleHalfHeight);

            case BoxCollider boxCollider:
                Vector3 scaledHalfSize = Vector3.Scale(
                    boxCollider.size * 0.5f,
                    new Vector3(Mathf.Abs(lossyScale.x), Mathf.Abs(lossyScale.y), Mathf.Abs(lossyScale.z)));
                return scaledHalfSize.magnitude;

            default:
                return collider.bounds.extents.magnitude;
        }
    }
}
