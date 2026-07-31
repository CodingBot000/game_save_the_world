using System;
using System.Collections.Generic;
using UnityEngine;

// Temporary compatibility adapter. The production lock-on path calls
// PlayerMissileSalvoLauncher directly and this component is removed with the legacy HUD.
public sealed class PlayerSpecialAttackController : MonoBehaviour
{
    private const int DefaultMissileCountPerSide = 15;
    private const int DefaultMissilesPerVolley = 4;
    private const float DefaultSalvoDuration = 0.6f;

    [Header("Legacy Test Adapter")]
    [SerializeField] private int missileCountPerSide = DefaultMissileCountPerSide;
    [SerializeField] private int missilesPerVolley = DefaultMissilesPerVolley;
    [SerializeField] private float missileSalvoDuration = DefaultSalvoDuration;
    [SerializeField] private float specialMissileDamage;

    private BattleController battleController;
    private BossController bossController;
    private PlayerCombatController playerCombatController;
    private PlayerMissileSalvoLauncher salvoLauncher;
    private BossLockOnTargetProvider lockOnTargetProvider;
    private int adapterSalvoId;

    public string LastAttemptReason { get; private set; } = string.Empty;

    public bool IsActive => salvoLauncher != null && salvoLauncher.IsSalvoActive(adapterSalvoId);

    // Kept only so existing scene scripts compile while the broadcast path is removed later.
    // The adapter deliberately never raises it, so a missile salvo cannot trigger a broadcast.
    public event Action SpecialMissileSalvoCompleted
    {
        add { }
        remove { }
    }

    public void Configure(
        BattleController battle,
        BossController boss,
        BossAttackController bossAttack,
        PlayerCombatController playerCombat,
        PlayerOrbitController playerOrbit,
        ArenaCameraRig cameraRig,
        HUDPresenter hud,
        BattleAimPointTargetingPresenter targetingPresenter = null)
    {
        battleController = battle;
        bossController = boss;
        playerCombatController = playerCombat;
        lockOnTargetProvider = battle != null
            ? battle.BossLockOnTargetProvider
            : bossController != null
                ? bossController.GetComponent<BossLockOnTargetProvider>()
                : null;

        if (salvoLauncher != null)
        {
            salvoLauncher.SalvoCompleted -= HandleSalvoCompleted;
            salvoLauncher.SalvoCanceled -= HandleSalvoCanceled;
        }

        salvoLauncher = GetComponent<PlayerMissileSalvoLauncher>();
        if (salvoLauncher == null)
        {
            salvoLauncher = gameObject.AddComponent<PlayerMissileSalvoLauncher>();
        }

        salvoLauncher.Configure(battleController, playerCombatController);
        salvoLauncher.SalvoCompleted += HandleSalvoCompleted;
        salvoLauncher.SalvoCanceled += HandleSalvoCanceled;
    }

    public bool TryActivate()
    {
        int totalMissileCount = Mathf.Max(1, missileCountPerSide) * 2;
        return TryActivateForDebug(totalMissileCount);
    }

    public bool TryActivateForDebug(int totalMissileCount)
    {
        LastAttemptReason = string.Empty;
        if (!CanActivate())
        {
            LastAttemptReason = GetUnavailableReason();
            return false;
        }

        List<SalvoTargetSnapshot> targets = BuildLockOnTargetSnapshots();
        SalvoRequest request = new(
            "LegacySpecialTestAdapter",
            totalMissileCount,
            Mathf.Max(0f, specialMissileDamage),
            targets,
            Mathf.Max(1, missilesPerVolley),
            Mathf.Max(0f, missileSalvoDuration),
            randomSeed: 0,
            SalvoMissileProfileSnapshot.Capture(playerCombatController));

        SalvoStartResult prepareResult = salvoLauncher.TryPrepareSalvo(request, out SalvoHandle handle);
        if (!prepareResult.IsPrepared)
        {
            LastAttemptReason = prepareResult.Reason;
            Debug.LogWarning(
                $"Legacy Special salvo prepare failed: {prepareResult.Status} {prepareResult.Reason}",
                this);
            return false;
        }

        SalvoCommitResult commitResult = salvoLauncher.StartPreparedSalvo(handle);
        if (!commitResult.IsStarted)
        {
            LastAttemptReason = commitResult.Reason;
            Debug.LogWarning(
                $"Legacy Special salvo start failed: {commitResult.Status} {commitResult.Reason}",
                this);
            return false;
        }

        adapterSalvoId = commitResult.SalvoId;
        return true;
    }

    public bool CanActivate()
    {
        return string.IsNullOrEmpty(GetUnavailableReason());
    }

    public string GetUnavailableReason()
    {
        if (salvoLauncher == null)
        {
            return "Special launcher unavailable.";
        }

        if (salvoLauncher.IsBusy)
        {
            return "Special attack is already active.";
        }

        if (battleController == null || !battleController.IsBattleActive)
        {
            return "Special attack unavailable.";
        }

        if (playerCombatController == null || !playerCombatController.IsAlive)
        {
            return "Player destroyed.";
        }

        if (bossController == null || !bossController.IsAlive)
        {
            return "No special attack target.";
        }

        if (playerCombatController.MissileLauncherLeft == null &&
            playerCombatController.MissileLauncherRight == null)
        {
            return "Special launcher offline.";
        }

        return HasLockOnTarget()
            ? string.Empty
            : "No special attack target.";
    }

    private bool HasLockOnTarget()
    {
        return lockOnTargetProvider != null && lockOnTargetProvider.HasValidTargets;
    }

    private List<SalvoTargetSnapshot> BuildLockOnTargetSnapshots()
    {
        List<SalvoTargetSnapshot> targets = new();
        if (lockOnTargetProvider == null)
        {
            return targets;
        }

        List<BossLockOnTarget> selectedTargets = new();
        int seed = unchecked(Environment.TickCount ^ (Time.frameCount * 397));
        lockOnTargetProvider.BuildTargetSequence(5, seed, selectedTargets);
        for (int i = 0; i < selectedTargets.Count; i++)
        {
            SalvoTargetSnapshot snapshot =
                lockOnTargetProvider.CreateSalvoSnapshot(selectedTargets[i]);
            if (snapshot == null)
            {
                continue;
            }

            targets.Add(snapshot);
        }

        return targets;
    }

    private void HandleSalvoCompleted(int salvoId)
    {
        if (salvoId == adapterSalvoId)
        {
            adapterSalvoId = 0;
        }
    }

    private void HandleSalvoCanceled(int salvoId, string reason)
    {
        if (salvoId == adapterSalvoId)
        {
            adapterSalvoId = 0;
        }
    }

    private void OnDestroy()
    {
        if (salvoLauncher == null)
        {
            return;
        }

        salvoLauncher.SalvoCompleted -= HandleSalvoCompleted;
        salvoLauncher.SalvoCanceled -= HandleSalvoCanceled;
    }
}
