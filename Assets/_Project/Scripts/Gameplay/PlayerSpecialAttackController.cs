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
    private int adapterSalvoId;

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
        if (!CanActivate())
        {
            return false;
        }

        List<SalvoTargetSnapshot> targets = BuildLegacyTargetSnapshots();
        int totalMissileCount = Mathf.Max(1, missileCountPerSide) * 2;
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
            Debug.LogWarning(
                $"Legacy Special salvo prepare failed: {prepareResult.Status} {prepareResult.Reason}",
                this);
            return false;
        }

        SalvoCommitResult commitResult = salvoLauncher.StartPreparedSalvo(handle);
        if (!commitResult.IsStarted)
        {
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

        return HasLegacyTarget()
            ? string.Empty
            : "No special attack target.";
    }

    private bool HasLegacyTarget()
    {
        int aimPointCount = bossController != null
            ? bossController.GetCombatAimPointCount()
            : 0;
        for (int i = 0; i < aimPointCount; i++)
        {
            Transform candidate = bossController.GetCombatAimPoint(i);
            if (candidate != null && candidate.gameObject.activeInHierarchy)
            {
                return true;
            }
        }

        Transform fallback = bossController != null ? bossController.AimPoint : null;
        return fallback != null && fallback.gameObject.activeInHierarchy;
    }

    private List<SalvoTargetSnapshot> BuildLegacyTargetSnapshots()
    {
        List<SalvoTargetSnapshot> targets = new();
        if (bossController == null)
        {
            return targets;
        }

        int aimPointCount = bossController.GetCombatAimPointCount();
        for (int i = 0; i < aimPointCount; i++)
        {
            Transform target = bossController.GetCombatAimPoint(i);
            if (target == null || !target.gameObject.activeInHierarchy)
            {
                continue;
            }

            targets.Add(new SalvoTargetSnapshot(
                target,
                $"LegacyAimPoint:{target.GetEntityId()}",
                weakPointOpen: false,
                damageMultiplier: 1f));
        }

        if (targets.Count == 0)
        {
            Transform fallback = bossController.AimPoint != null
                ? bossController.AimPoint
                : bossController.transform;
            if (fallback != null && fallback.gameObject.activeInHierarchy)
            {
                targets.Add(new SalvoTargetSnapshot(
                    fallback,
                    $"LegacyBoss:{fallback.GetEntityId()}",
                    weakPointOpen: false,
                    damageMultiplier: 1f));
            }
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
