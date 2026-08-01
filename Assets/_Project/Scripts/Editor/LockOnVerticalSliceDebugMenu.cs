using System;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;

public static class LockOnVerticalSliceDebugMenu
{
    private const string MenuRoot = "TitanDestroyer/Debug/Lock-On Vertical Slice/";
    private const double InputProbeDuration = 0.55;
    private const double RealReuseInterval = 5.05;
    private const double FullRunTimeout = 35.0;
    private static readonly int[] ExpectedMissiles = { 5, 10, 15, 20, 30 };
    private static readonly float[] ExpectedRatios = { 0.30f, 0.40f, 0.50f, 0.60f, 1f };

    private static EditorApplication.CallbackFunction pendingFullRun;
    private static EditorApplication.CallbackFunction pendingOutcomeCheck;
    private static EditorApplication.CallbackFunction pendingTargetLossCheck;
    private static FullRunContext activeRun;
    private static TargetLossRunContext targetLossRun;

    [MenuItem(MenuRoot + "Run 1-5 Real Cadence", priority = 310)]
    private static void RunFullVerticalSlice()
    {
        AbortFullRun("ReplacedByNewRun", logWarning: false);
        if (!TryGetRuntime(out BattleController battle, out BossController boss,
                out BossAttackController bossAttack, out PlayerOrbitController orbit,
                out PlayerCombatController combat, out PlayerLockOnController lockOn,
                out PlayerMissileSalvoLauncher launcher, out HUDPresenter hud))
        {
            return;
        }

        if (!battle.IsBattleActive || !boss.IsAlive || !combat.IsAlive ||
            lockOn.State != LockOnCombatState.Ready || launcher.IsBusy)
        {
            Debug.LogWarning(
                $"[LockVerticalDebug] Cannot start. battle={battle.IsBattleActive}, " +
                $"bossAlive={boss.IsAlive}, playerAlive={combat.IsAlive}, " +
                $"lockState={lockOn.State}, launcherBusy={launcher.IsBusy}.");
            return;
        }

        activeRun = new FullRunContext
        {
            Battle = battle,
            Boss = boss,
            BossAttack = bossAttack,
            Orbit = orbit,
            Combat = combat,
            LockOn = lockOn,
            Launcher = launcher,
            Hud = hud,
            StartedAt = EditorApplication.timeSinceStartup,
            InitialPlayerPosition = orbit.transform.position,
            InitialBossHealth = boss.CurrentHealth,
            GatlingDamage = combat.CurrentGatlingBaseDamage,
            NextStage = 1,
            PoolInvariantHeld = launcher.HasValidPoolCounts,
            BattleStayedActive = true,
            PlayerStayedAlive = true,
            BossStayedAlive = true,
            PreviousUndead = GameplayDebugFlags.Undead,
        };
        activeRun.BossAttackHandler = () => activeRun.BossAttackCount++;
        bossAttack.GameplayAttackStarted += activeRun.BossAttackHandler;
        GameplayDebugFlags.Undead = true;
        combat.RefillForDebug();

        Keyboard keyboard = Keyboard.current;
        activeRun.KeyboardAvailable = keyboard != null;
        if (keyboard != null)
        {
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.A, Key.Space));
            activeRun.InputPressed = true;
        }

        pendingFullRun = TickFullRun;
        EditorApplication.update += pendingFullRun;
        Debug.Log(
            "[LockVerticalDebug] Started real-cadence 1-5 integration run. " +
            "A+Space is injected for 0.55 seconds; lock reuse is not fast-forwarded.");
    }

    [MenuItem(MenuRoot + "Abort Active Run", priority = 311)]
    private static void AbortActiveRun()
    {
        AbortFullRun("UserAbort", logWarning: true);
        AbortTargetLossRun("UserAbort", logWarning: true);
    }

    [MenuItem(MenuRoot + "Run Victory Flow", priority = 312)]
    private static void RunVictoryFlow()
    {
        if (!TryGetRuntime(out BattleController battle, out BossController boss,
                out _, out _, out PlayerCombatController combat,
                out PlayerLockOnController lockOn, out _, out HUDPresenter hud))
        {
            return;
        }

        if (!battle.IsBattleActive || !boss.IsAlive || !combat.IsAlive)
        {
            Debug.LogWarning(
                "[LockVerticalDebug] Victory flow requires a fresh active battle with both actors alive.");
            return;
        }

        bool damageApplied = boss.ApplyDamage(boss.CurrentHealth + 1f);
        ScheduleOutcomeCheck("victory", () =>
        {
            bool lockRejected = !lockOn.TryBeginCharging(LockOnInputSource.Debug);
            bool verified = damageApplied && !battle.IsBattleActive && !boss.IsAlive &&
                            combat.IsAlive && lockRejected && !combat.IsSalvoInvincible &&
                            hud != null && hud.DebugStatusText.Contains("Boss defeated");
            Debug.Log(
                $"[LockVerticalDebug] victory verified={verified}, damageApplied={damageApplied}, " +
                $"battleActive={battle.IsBattleActive}, bossAlive={boss.IsAlive}, " +
                $"playerAlive={combat.IsAlive}, lockRejected={lockRejected}, " +
                $"salvoInvincible={combat.IsSalvoInvincible}, status={hud?.DebugStatusText}.");
        });
    }

    [MenuItem(MenuRoot + "Run Defeat Flow", priority = 313)]
    private static void RunDefeatFlow()
    {
        if (!TryGetRuntime(out BattleController battle, out BossController boss,
                out _, out _, out PlayerCombatController combat,
                out PlayerLockOnController lockOn, out _, out HUDPresenter hud))
        {
            return;
        }

        if (!battle.IsBattleActive || !boss.IsAlive || !combat.IsAlive)
        {
            Debug.LogWarning(
                "[LockVerticalDebug] Defeat flow requires a fresh active battle with both actors alive.");
            return;
        }

        GameplayDebugFlags.Undead = false;
        combat.EndSalvoInvincibility();
        combat.RefillForDebug();
        bool damageApplied = combat.ApplyContinuousDamage(999999f);
        ScheduleOutcomeCheck("defeat", () =>
        {
            bool lockRejected = !lockOn.TryBeginCharging(LockOnInputSource.Debug);
            bool verified = damageApplied && !battle.IsBattleActive && boss.IsAlive &&
                            !combat.IsAlive && lockRejected && !combat.IsSalvoInvincible &&
                            hud != null && hud.DebugStatusText.Contains("Mission failed");
            Debug.Log(
                $"[LockVerticalDebug] defeat verified={verified}, damageApplied={damageApplied}, " +
                $"battleActive={battle.IsBattleActive}, bossAlive={boss.IsAlive}, " +
                $"playerAlive={combat.IsAlive}, lockRejected={lockRejected}, " +
                $"salvoInvincible={combat.IsSalvoInvincible}, status={hud?.DebugStatusText}.");
        });
    }

    [MenuItem(MenuRoot + "Run Target-Loss 5s Guarantee", priority = 314)]
    private static void RunTargetLossGuarantee()
    {
        if (!TryGetRuntime(out BattleController battle, out BossController boss,
                out _, out _, out PlayerCombatController combat,
                out _, out PlayerMissileSalvoLauncher launcher, out _))
        {
            return;
        }

        if (!battle.IsBattleActive || !boss.IsAlive || !combat.IsAlive || launcher.IsBusy)
        {
            Debug.LogWarning(
                "[LockVerticalDebug] Target-loss guarantee requires a fresh active battle and idle launcher.");
            return;
        }

        AbortTargetLossRun("ReplacedByNewRun", logWarning: false);
        GameObject firstTarget = new("LockVerticalTargetLoss_First");
        firstTarget.transform.position = boss.HitPoint + new Vector3(70f, 25f, 45f);
        SalvoRequest firstRequest = CreateTargetLossRequest(
            combat,
            firstTarget.transform,
            90101,
            useSlowMissileProfile: true);
        SalvoStartResult prepare = launcher.TryPrepareSalvo(firstRequest, out SalvoHandle handle);
        SalvoCommitResult commit = prepare.IsPrepared
            ? launcher.StartPreparedSalvo(handle)
            : SalvoCommitResult.Rejected(prepare.Reason);
        if (!prepare.IsPrepared || !commit.IsStarted)
        {
            UnityEngine.Object.Destroy(firstTarget);
            Debug.LogError(
                $"[LockVerticalDebug] Target-loss first salvo failed. " +
                $"prepare={prepare.Status}/{prepare.Reason}, commit={commit.Status}/{commit.Reason}.");
            return;
        }

        firstTarget.SetActive(false);
        targetLossRun = new TargetLossRunContext
        {
            Battle = battle,
            Boss = boss,
            Combat = combat,
            Launcher = launcher,
            FirstTarget = firstTarget,
            StartedAt = EditorApplication.timeSinceStartup,
            ReclaimedBefore = launcher.PoolCapacityReclaimedMissiles,
            PreviousUndead = GameplayDebugFlags.Undead,
        };
        GameplayDebugFlags.Undead = true;
        pendingTargetLossCheck = TickTargetLossRun;
        EditorApplication.update += pendingTargetLossCheck;
        Debug.Log(
            "[LockVerticalDebug] Target-loss 5-second guarantee started. " +
            "The first 30 missiles retain no valid target; the second 30-slot reservation is attempted at 5.05 seconds.");
    }

    private static void TickFullRun()
    {
        FullRunContext run = activeRun;
        if (run == null)
        {
            RemoveFullRunCallback();
            return;
        }

        try
        {
            if (!Application.isPlaying || run.Battle == null || run.Boss == null ||
                run.Orbit == null || run.Combat == null || run.LockOn == null ||
                run.Launcher == null)
            {
                FinishFullRun(false, "RuntimeStoppedOrDependencyLost");
                return;
            }

            double now = EditorApplication.timeSinceStartup;
            double elapsed = now - run.StartedAt;
            ObserveRuntime(run);

            if (run.InputPressed && elapsed >= InputProbeDuration)
            {
                ReleaseInjectedKeyboard(run);
                run.MovementObserved =
                    Vector3.Distance(run.InitialPlayerPosition, run.Orbit.transform.position) > 0.05f;
            }

            if (elapsed > FullRunTimeout)
            {
                FinishFullRun(false, "Timeout");
                return;
            }

            if (run.AwaitingStageCompletion)
            {
                if (run.LockOn.CurrentLockOnSalvoId != 0 || run.Combat.IsSalvoInvincible)
                {
                    return;
                }

                int stageIndex = run.NextStage - 1;
                bool completionValid =
                    run.LockOn.LastFiredMissileCount == ExpectedMissiles[stageIndex] &&
                    run.Launcher.HasValidPoolCounts;
                run.AllStageChecksPassed &= completionValid;
                run.StageLog.Append(
                    $" S{run.NextStage}:complete={completionValid}" +
                    $"({run.LockOn.LastFiredMissileCount}/{ExpectedMissiles[stageIndex]})");
                run.CompletedStages++;
                run.NextStage++;
                run.AwaitingStageCompletion = false;

                if (run.CompletedStages == ExpectedMissiles.Length)
                {
                    bool verified = BuildFullRunVerdict(run);
                    FinishFullRun(verified, verified ? "Completed" : "VerificationFailed");
                }

                return;
            }

            if (run.InputPressed || run.NextStage > ExpectedMissiles.Length ||
                now < run.NextReleaseAllowedAt || run.LockOn.State != LockOnCombatState.Ready ||
                run.Launcher.IsBusy)
            {
                return;
            }

            StartStage(run, now);
        }
        catch (Exception exception)
        {
            Debug.LogException(exception);
            FinishFullRun(false, $"Exception:{exception.GetType().Name}");
        }
    }

    private static void TickTargetLossRun()
    {
        TargetLossRunContext run = targetLossRun;
        if (run == null)
        {
            RemoveTargetLossCallback();
            return;
        }

        if (!Application.isPlaying || run.Battle == null || run.Boss == null ||
            run.Combat == null || run.Launcher == null)
        {
            AbortTargetLossRun("RuntimeStoppedOrDependencyLost", logWarning: true);
            return;
        }

        if (EditorApplication.timeSinceStartup - run.StartedAt < RealReuseInterval)
        {
            return;
        }

        GameObject secondTarget = new("LockVerticalTargetLoss_Second");
        secondTarget.transform.position = run.Boss.HitPoint + new Vector3(-70f, 30f, 45f);
        SalvoRequest secondRequest = CreateTargetLossRequest(
            run.Combat,
            secondTarget.transform,
            90102,
            useSlowMissileProfile: false);
        int availableBefore = run.Launcher.PoolAvailableMissiles;
        int expectedReclaim = Mathf.Max(
            0,
            PlayerMissileSalvoLauncher.MaxSalvoMissileCount - availableBefore);
        SalvoStartResult result = run.Launcher.TryPrepareSalvo(secondRequest, out SalvoHandle handle);
        int reclaimed = run.Launcher.PoolCapacityReclaimedMissiles - run.ReclaimedBefore;
        bool prepared = result.IsPrepared && handle != null;
        if (prepared)
        {
            run.Launcher.CancelPreparedSalvo(handle, "TargetLossGuaranteeVerified");
        }

        bool verified = prepared && reclaimed == expectedReclaim && run.Launcher.HasValidPoolCounts &&
                        run.Launcher.PoolAvailableMissiles +
                        run.Launcher.PoolReservedMissiles +
                        run.Launcher.PoolLeasedMissiles ==
                        PlayerMissileSalvoLauncher.MissilePoolCapacity;
        Debug.Log(
            $"[LockVerticalDebug] target-loss guarantee verified={verified}, " +
            $"prepare={result.Status}/{result.Reason}, availableBefore={availableBefore}, " +
            $"reclaimed={reclaimed}/{expectedReclaim}, " +
            $"pool={run.Launcher.PoolAvailableMissiles}/" +
            $"{run.Launcher.PoolReservedMissiles}/{run.Launcher.PoolLeasedMissiles}, " +
            $"valid={run.Launcher.HasValidPoolCounts}.");
        UnityEngine.Object.Destroy(secondTarget);
        CompleteTargetLossRun();
    }

    private static SalvoRequest CreateTargetLossRequest(
        PlayerCombatController combat,
        Transform target,
        int seed,
        bool useSlowMissileProfile)
    {
        SalvoTargetSnapshot snapshot = new(
            target,
            target != null ? target.name : "target-loss",
            weakPointOpen: false,
            damageMultiplier: 1f);
        return new SalvoRequest(
            "TargetLossGuaranteeDebug",
            PlayerMissileSalvoLauncher.MaxSalvoMissileCount,
            damagePerMissile: 0f,
            new[] { snapshot },
            missilesPerVolley: 4,
            salvoDuration: 0.6f,
            randomSeed: seed,
            useSlowMissileProfile
                ? CaptureSlowTargetLossMissileProfile(combat)
                : SalvoMissileProfileSnapshot.Capture(combat),
            successfulLockCount: 5);
    }

    private static SalvoMissileProfileSnapshot CaptureSlowTargetLossMissileProfile(
        PlayerCombatController combat)
    {
        float launchSpeed = combat.DebugMissileLaunchSpeed;
        float cruiseSpeed = combat.DebugMissileCruiseSpeed;
        float acceleration = combat.DebugMissileAcceleration;
        float turnRate = combat.DebugMissileTurnRate;
        float lockOnDelay = combat.DebugMissileLockOnDelay;
        float straightDuration = combat.DebugMissileStraightPhaseDuration;
        float straightDistance = combat.DebugMissileStraightPhaseDistance;
        float turnDuration = combat.DebugMissileTurnPhaseDuration;
        float boostDuration = combat.DebugMissileBoostPhaseDuration;
        float lifetime = combat.DebugMissileLifetime;
        float hitRadius = combat.DebugMissileHitRadius;
        try
        {
            combat.SetMissileFlightTuningForDebug(
                launchSpeed: 1f,
                cruiseSpeed: 1f,
                acceleration: 0f,
                turnRate: 0f,
                lockOnDelay,
                straightDuration,
                straightDistance,
                turnDuration,
                boostDuration,
                lifetime: 10f,
                projectileHitRadius: 0.1f);
            return SalvoMissileProfileSnapshot.Capture(combat);
        }
        finally
        {
            combat.SetMissileFlightTuningForDebug(
                launchSpeed,
                cruiseSpeed,
                acceleration,
                turnRate,
                lockOnDelay,
                straightDuration,
                straightDistance,
                turnDuration,
                boostDuration,
                lifetime,
                hitRadius);
        }
    }

    private static void ObserveRuntime(FullRunContext run)
    {
        run.BattleStayedActive &= run.Battle.IsBattleActive;
        run.PlayerStayedAlive &= run.Combat.IsAlive;
        run.BossStayedAlive &= run.Boss.IsAlive;
        run.NoShootError &= run.Hud == null || !run.Hud.IsShootErrorVisible;
        int poolTotal = run.Launcher.PoolAvailableMissiles +
                        run.Launcher.PoolReservedMissiles +
                        run.Launcher.PoolLeasedMissiles;
        run.PoolInvariantHeld &= run.Launcher.HasValidPoolCounts &&
                                 poolTotal == PlayerMissileSalvoLauncher.MissilePoolCapacity;
        run.MaxLeasedMissiles = Mathf.Max(
            run.MaxLeasedMissiles,
            run.Launcher.PoolLeasedMissiles);

        ProjectileController[] projectiles =
            UnityEngine.Object.FindObjectsByType<ProjectileController>(FindObjectsSortMode.None);
        for (int i = 0; i < projectiles.Length; i++)
        {
            ProjectileController projectile = projectiles[i];
            if (projectile == null || !projectile.gameObject.activeInHierarchy ||
                !projectile.name.Contains("Runtime"))
            {
                continue;
            }

            if (projectile.Team == ProjectileTeam.Player &&
                projectile.name == "PlayerProjectileRuntime")
            {
                run.GatlingProjectileObserved = true;
            }
            else if (projectile.Team == ProjectileTeam.Boss)
            {
                run.BossProjectileObserved = true;
            }
        }
    }

    private static void StartStage(FullRunContext run, double now)
    {
        int stageIndex = run.NextStage - 1;
        bool began = run.LockOn.TryBeginCharging(LockOnInputSource.Debug);
        run.LockOn.AdvanceChargeForDebug(
            run.LockOn.GetCumulativeChargeTimeForStage(run.NextStage) + 0.01f);
        bool released = run.LockOn.TryReleaseCharging(LockOnInputSource.Debug);
        float expectedBudget = run.GatlingDamage * 10f * ExpectedRatios[stageIndex];
        bool immediateValid = began && released &&
                              run.LockOn.LastRequestedMissileCount == ExpectedMissiles[stageIndex] &&
                              Mathf.Approximately(run.LockOn.LastBaseDamageBudget, expectedBudget) &&
                              run.LockOn.LastFirstMissileWasInvincible &&
                              run.Combat.IsSalvoInvincible &&
                              run.LockOn.State == LockOnCombatState.ReuseWait &&
                              (run.Hud == null || !run.Hud.IsShootErrorVisible);
        run.AllStageChecksPassed &= immediateValid;
        run.StageLog.Append(
            $" S{run.NextStage}:start={immediateValid}" +
            $"({run.LockOn.LastRequestedMissileCount}/{ExpectedMissiles[stageIndex]})");
        if (!released)
        {
            FinishFullRun(false, $"Stage{run.NextStage}ReleaseRejected");
            return;
        }

        run.NextReleaseAllowedAt = now + RealReuseInterval;
        run.AwaitingStageCompletion = true;
    }

    private static bool BuildFullRunVerdict(FullRunContext run)
    {
        run.MovementObserved |=
            Vector3.Distance(run.InitialPlayerPosition, run.Orbit.transform.position) > 0.05f;
        return run.KeyboardAvailable && run.MovementObserved &&
               run.GatlingProjectileObserved && run.BossAttackCount > 0 &&
               run.BattleStayedActive && run.PlayerStayedAlive && run.BossStayedAlive &&
               run.AllStageChecksPassed && run.CompletedStages == ExpectedMissiles.Length &&
               run.PoolInvariantHeld && run.NoShootError;
    }

    private static void FinishFullRun(bool verified, string reason)
    {
        FullRunContext run = activeRun;
        if (run == null)
        {
            RemoveFullRunCallback();
            return;
        }

        ReleaseInjectedKeyboard(run);
        if (run.BossAttack != null && run.BossAttackHandler != null)
        {
            run.BossAttack.GameplayAttackStarted -= run.BossAttackHandler;
        }

        GameplayDebugFlags.Undead = run.PreviousUndead;
        RemoveFullRunCallback();
        activeRun = null;
        Debug.Log(
            $"[LockVerticalDebug] full run verified={verified}, reason={reason}, " +
            $"stages={run.CompletedStages}/5, movement={run.MovementObserved}, " +
            $"gatling={run.GatlingProjectileObserved}, bossAttacks={run.BossAttackCount}, " +
            $"bossProjectile={run.BossProjectileObserved}, battleActive={run.BattleStayedActive}, " +
            $"playerAlive={run.PlayerStayedAlive}, bossAlive={run.BossStayedAlive}, " +
            $"poolInvariant={run.PoolInvariantHeld}, maxLeased={run.MaxLeasedMissiles}, " +
            $"shootError={!run.NoShootError}, bossDamage={run.InitialBossHealth - run.Boss.CurrentHealth:0.###}, " +
            $"stageChecks={run.AllStageChecksPassed}.{run.StageLog}");
    }

    private static void AbortFullRun(string reason, bool logWarning)
    {
        if (activeRun == null)
        {
            RemoveFullRunCallback();
            return;
        }

        if (logWarning)
        {
            Debug.LogWarning($"[LockVerticalDebug] Active integration run aborted: {reason}.");
        }

        FinishFullRun(false, reason);
    }

    private static void AbortTargetLossRun(string reason, bool logWarning)
    {
        if (targetLossRun == null)
        {
            RemoveTargetLossCallback();
            return;
        }

        if (logWarning)
        {
            Debug.LogWarning($"[LockVerticalDebug] Target-loss run aborted: {reason}.");
        }

        CompleteTargetLossRun();
    }

    private static void CompleteTargetLossRun()
    {
        TargetLossRunContext run = targetLossRun;
        if (run != null)
        {
            GameplayDebugFlags.Undead = run.PreviousUndead;
            if (run.FirstTarget != null)
            {
                UnityEngine.Object.Destroy(run.FirstTarget);
            }
        }

        targetLossRun = null;
        RemoveTargetLossCallback();
    }

    private static void ReleaseInjectedKeyboard(FullRunContext run)
    {
        if (run == null || !run.InputPressed)
        {
            return;
        }

        Keyboard keyboard = Keyboard.current;
        if (keyboard != null)
        {
            InputSystem.QueueStateEvent(keyboard, new KeyboardState());
        }

        run.InputPressed = false;
    }

    private static void RemoveFullRunCallback()
    {
        if (pendingFullRun == null)
        {
            return;
        }

        EditorApplication.update -= pendingFullRun;
        pendingFullRun = null;
    }

    private static void RemoveTargetLossCallback()
    {
        if (pendingTargetLossCheck == null)
        {
            return;
        }

        EditorApplication.update -= pendingTargetLossCheck;
        pendingTargetLossCheck = null;
    }

    private static void ScheduleOutcomeCheck(string label, Action assertion)
    {
        if (pendingOutcomeCheck != null)
        {
            EditorApplication.update -= pendingOutcomeCheck;
        }

        double startedAt = EditorApplication.timeSinceStartup;
        pendingOutcomeCheck = () =>
        {
            if (!Application.isPlaying)
            {
                EditorApplication.update -= pendingOutcomeCheck;
                pendingOutcomeCheck = null;
                return;
            }

            if (EditorApplication.timeSinceStartup - startedAt < 0.25)
            {
                return;
            }

            EditorApplication.update -= pendingOutcomeCheck;
            pendingOutcomeCheck = null;
            try
            {
                assertion?.Invoke();
            }
            catch (Exception exception)
            {
                Debug.LogError($"[LockVerticalDebug] {label} assertion threw {exception.GetType().Name}.");
                Debug.LogException(exception);
            }
        };
        EditorApplication.update += pendingOutcomeCheck;
    }

    private static bool TryGetRuntime(
        out BattleController battle,
        out BossController boss,
        out BossAttackController bossAttack,
        out PlayerOrbitController orbit,
        out PlayerCombatController combat,
        out PlayerLockOnController lockOn,
        out PlayerMissileSalvoLauncher launcher,
        out HUDPresenter hud)
    {
        battle = null;
        boss = null;
        bossAttack = null;
        orbit = null;
        combat = null;
        lockOn = null;
        launcher = null;
        hud = null;
        if (!Application.isPlaying)
        {
            Debug.LogWarning("[LockVerticalDebug] Enter Play Mode before running this diagnostic.");
            return false;
        }

        battle = UnityEngine.Object.FindAnyObjectByType<BattleController>();
        boss = UnityEngine.Object.FindAnyObjectByType<BossController>();
        bossAttack = UnityEngine.Object.FindAnyObjectByType<BossAttackController>();
        orbit = UnityEngine.Object.FindAnyObjectByType<PlayerOrbitController>();
        combat = UnityEngine.Object.FindAnyObjectByType<PlayerCombatController>();
        lockOn = UnityEngine.Object.FindAnyObjectByType<PlayerLockOnController>();
        launcher = UnityEngine.Object.FindAnyObjectByType<PlayerMissileSalvoLauncher>();
        hud = UnityEngine.Object.FindAnyObjectByType<HUDPresenter>();
        bool found = battle != null && boss != null && bossAttack != null && orbit != null &&
                     combat != null && lockOn != null && launcher != null && hud != null;
        if (!found)
        {
            Debug.LogError(
                $"[LockVerticalDebug] Dependencies missing. battle={battle != null}, " +
                $"boss={boss != null}, bossAttack={bossAttack != null}, orbit={orbit != null}, " +
                $"combat={combat != null}, lockOn={lockOn != null}, launcher={launcher != null}, " +
                $"hud={hud != null}.");
        }

        return found;
    }

    private sealed class FullRunContext
    {
        public BattleController Battle;
        public BossController Boss;
        public BossAttackController BossAttack;
        public PlayerOrbitController Orbit;
        public PlayerCombatController Combat;
        public PlayerLockOnController LockOn;
        public PlayerMissileSalvoLauncher Launcher;
        public HUDPresenter Hud;
        public Action BossAttackHandler;
        public readonly StringBuilder StageLog = new();
        public double StartedAt;
        public double NextReleaseAllowedAt;
        public Vector3 InitialPlayerPosition;
        public float InitialBossHealth;
        public float GatlingDamage;
        public int NextStage;
        public int CompletedStages;
        public int BossAttackCount;
        public int MaxLeasedMissiles;
        public bool PreviousUndead;
        public bool KeyboardAvailable;
        public bool InputPressed;
        public bool MovementObserved;
        public bool GatlingProjectileObserved;
        public bool BossProjectileObserved;
        public bool AwaitingStageCompletion;
        public bool PoolInvariantHeld;
        public bool BattleStayedActive;
        public bool PlayerStayedAlive;
        public bool BossStayedAlive;
        public bool AllStageChecksPassed = true;
        public bool NoShootError = true;
    }

    private sealed class TargetLossRunContext
    {
        public BattleController Battle;
        public BossController Boss;
        public PlayerCombatController Combat;
        public PlayerMissileSalvoLauncher Launcher;
        public GameObject FirstTarget;
        public double StartedAt;
        public int ReclaimedBefore;
        public bool PreviousUndead;
    }
}
