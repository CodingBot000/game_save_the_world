using UnityEngine;

[DefaultExecutionOrder(720)]
public class BattleEventBroadcastTrigger : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private BattleEventBroadcastView broadcastView;
    [SerializeField] private BossAttackController bossAttackController;
    [SerializeField] private PlayerSpecialAttackController specialAttackController;

    [Header("Timing")]
    [SerializeField, Min(0.1f)] private float firstBossAttackDuration = 4f;
    [SerializeField, Min(0.1f)] private float specialSalvoDuration = 4f;

    private bool firstBossAttackShown;
    private bool subscribedToBossAttack;
    private bool subscribedToSpecialAttack;

    private void Awake()
    {
        ResolveReferences();
    }

    private void Reset()
    {
        ResolveReferences();
    }

    private void OnValidate()
    {
        ResolveReferences();
    }

    private void Start()
    {
        ResolveReferences();
        Subscribe();
    }

    private void Update()
    {
        ResolveReferences();
        Subscribe();
    }

    private void OnDisable()
    {
        Unsubscribe();
    }

    private void HandleBossGameplayAttackStarted()
    {
        if (firstBossAttackShown || broadcastView == null)
        {
            return;
        }

        firstBossAttackShown = true;
        broadcastView.ShowNormal(firstBossAttackDuration);
    }

    private void HandleSpecialMissileSalvoCompleted()
    {
        if (broadcastView == null)
        {
            return;
        }

        broadcastView.ShowSurprise(specialSalvoDuration);
    }

    private void Subscribe()
    {
        if (!subscribedToBossAttack && bossAttackController != null)
        {
            bossAttackController.GameplayAttackStarted += HandleBossGameplayAttackStarted;
            subscribedToBossAttack = true;
        }

        if (!subscribedToSpecialAttack && specialAttackController != null)
        {
            specialAttackController.SpecialMissileSalvoCompleted += HandleSpecialMissileSalvoCompleted;
            subscribedToSpecialAttack = true;
        }
    }

    private void Unsubscribe()
    {
        if (subscribedToBossAttack && bossAttackController != null)
        {
            bossAttackController.GameplayAttackStarted -= HandleBossGameplayAttackStarted;
        }

        if (subscribedToSpecialAttack && specialAttackController != null)
        {
            specialAttackController.SpecialMissileSalvoCompleted -= HandleSpecialMissileSalvoCompleted;
        }

        subscribedToBossAttack = false;
        subscribedToSpecialAttack = false;
    }

    private void ResolveReferences()
    {
        if (broadcastView == null)
        {
            broadcastView = GetComponent<BattleEventBroadcastView>();
        }

        if (broadcastView == null)
        {
            broadcastView = FindAnyObjectByType<BattleEventBroadcastView>();
        }

        if (bossAttackController == null)
        {
            bossAttackController = FindAnyObjectByType<BossAttackController>();
        }

        if (specialAttackController == null)
        {
            specialAttackController = FindAnyObjectByType<PlayerSpecialAttackController>();
        }
    }
}
