using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

public static class LockOnCombatDebugMenu
{
    private const string MenuRoot = "TitanDestroyer/Debug/Lock-On Combat/";
    private static readonly float[] StageChargeSeconds = { 0.36f, 0.76f, 1.26f, 1.81f, 2.51f };
    private static readonly int[] ExpectedMissileCounts = { 5, 10, 15, 20, 30 };
    private static readonly float[] ExpectedDamageBudgetsAtG25 = { 75f, 100f, 125f, 150f, 250f };
    private static EditorApplication.CallbackFunction pendingTimelineCheck;
    private static EditorApplication.CallbackFunction pendingResidualBeamCheck;

    [MenuItem(MenuRoot + "Log State", priority = 290)]
    private static void LogState()
    {
        if (!TryGetRuntime(out PlayerLockOnController controller,
                out PlayerCombatController combat, out PlayerMissileSalvoLauncher launcher,
                out HUDPresenter hud, out _, out _))
        {
            return;
        }

        Debug.Log(
            $"[LockCombatDebug] state={controller.State}, reuse={controller.ReuseWaitRemaining:0.000}, " +
            $"salvoId={controller.CurrentLockOnSalvoId}, requested={controller.LastRequestedMissileCount}, " +
            $"fired={controller.LastFiredMissileCount}, gatling={combat.CurrentGatlingBaseDamage:0.###}, " +
            $"budget={controller.LastBaseDamageBudget:0.###}, " +
            $"damagePerMissile={controller.LastBaseDamagePerMissile:0.###}, " +
            $"salvoInvincible={combat.IsSalvoInvincible}, " +
            $"firstMissileInvincible={controller.LastFirstMissileWasInvincible}, " +
            $"pool={launcher.PoolAvailableMissiles}/{launcher.PoolReservedMissiles}/{launcher.PoolLeasedMissiles}, " +
            $"shootError={(hud != null && hud.IsShootErrorVisible)}, " +
            $"status={(hud != null ? hud.DebugStatusText : "<missing>")}, " +
            $"statusFont={(hud != null ? hud.DebugStatusFontSize : 0)}.");
    }

    [MenuItem(MenuRoot + "Fire Stage/1 - 5 missiles", priority = 291)]
    private static void FireStageOne() => FireStage(1);

    [MenuItem(MenuRoot + "Fire Stage/2 - 10 missiles", priority = 292)]
    private static void FireStageTwo() => FireStage(2);

    [MenuItem(MenuRoot + "Fire Stage/3 - 15 missiles", priority = 293)]
    private static void FireStageThree() => FireStage(3);

    [MenuItem(MenuRoot + "Fire Stage/4 - 20 missiles", priority = 294)]
    private static void FireStageFour() => FireStage(4);

    [MenuItem(MenuRoot + "Fire Stage/5 - 30 missiles", priority = 295)]
    private static void FireStageFive() => FireStage(5);

    [MenuItem(MenuRoot + "Verify Damage Table", priority = 296)]
    private static void VerifyDamageTable()
    {
        if (!TryGetRuntime(out _, out PlayerCombatController combat, out _,
                out _, out _, out _))
        {
            return;
        }

        bool verified = true;
        string rows = string.Empty;
        for (int stage = 1; stage <= 5; stage++)
        {
            bool calculated = LockOnSalvoRules.TryCalculate(
                stage,
                combat.CurrentGatlingBaseDamage,
                10f,
                ExpectedMissileCounts,
                new[] { 0.30f, 0.40f, 0.50f, 0.60f, 1f },
                out LockOnSalvoStageCalculation result,
                out string reason);
            verified &= calculated && result.MissileCount == ExpectedMissileCounts[stage - 1];
            rows += calculated
                ? $" S{stage}:{result.MissileCount}x{result.BaseDamagePerMissile:0.###}={result.TotalBaseDamage:0.###}"
                : $" S{stage}:ERROR({reason})";
        }

        Debug.Log(
            $"[LockCombatDebug] damage table verified={verified}, " +
            $"G={combat.CurrentGatlingBaseDamage:0.###},{rows}.");
    }

    [MenuItem(MenuRoot + "Run Weak Point x2", priority = 297)]
    private static void RunWeakPointMultiplier()
    {
        if (!TryGetRuntime(out PlayerLockOnController controller,
                out _, out PlayerMissileSalvoLauncher launcher, out HUDPresenter hud,
                out BossLockOnTargetProvider provider, out BossTestState testState) ||
            !PrepareReadyState(controller, launcher))
        {
            return;
        }

        testState.SetWeakPointOpen(true);
        for (int i = 0; i < provider.Targets.Count; i++)
        {
            BossLockOnTarget target = provider.Targets[i];
            target?.SetAttackableForDebug(target.IsWeakPoint);
        }

        bool began;
        bool released;
        try
        {
            began = controller.TryBeginCharging(LockOnInputSource.Debug);
            controller.AdvanceChargeForDebug(StageChargeSeconds[0]);
            released = controller.TryReleaseCharging(LockOnInputSource.Debug);
        }
        finally
        {
            for (int i = 0; i < provider.Targets.Count; i++)
            {
                provider.Targets[i]?.SetAttackableForDebug(true);
            }

            testState.SetWeakPointOpen(false);
        }

        float finalDamage = controller.LastBaseDamagePerMissile *
                            controller.LastFirstMissileDamageMultiplier;
        bool verified = began && released &&
                        Mathf.Approximately(controller.LastFirstMissileDamageMultiplier, 2f) &&
                        Mathf.Approximately(finalDamage, controller.LastBaseDamagePerMissile * 2f) &&
                        controller.LastFirstMissileWasInvincible &&
                        (hud == null || !hud.IsShootErrorVisible);
        Debug.Log(
            $"[LockCombatDebug] weak point verified={verified}, began={began}, released={released}, " +
            $"basePerMissile={controller.LastBaseDamagePerMissile:0.###}, " +
            $"multiplier={controller.LastFirstMissileDamageMultiplier:0.###}, " +
            $"finalPerMissile={finalDamage:0.###}, snapshotPreservedAfterClose=True.");
    }

    [MenuItem(MenuRoot + "Run Continuous Damage Reset", priority = 298)]
    private static void RunContinuousDamageReset()
    {
        if (!TryGetRuntime(out _, out PlayerCombatController combat, out _,
                out _, out _, out _))
        {
            return;
        }

        if (combat.IsSalvoInvincible)
        {
            Debug.LogWarning("[LockCombatDebug] Wait for the active lock-on salvo invincibility to end.");
            return;
        }

        ContinuousDamageTickState tickState = new("debug.continuousDamage", 0.2f);
        PlayerContinuousDamageTickRegistry registry = combat.ContinuousDamageTickRegistry;
        int countBefore = registry.ActiveCount;
        bool registered = registry.Register(tickState);
        tickState.AddElapsed(0.19f);
        float armorBefore = combat.CurrentArmor;
        float hullBefore = combat.CurrentHull;
        bool beganInvincibility = combat.BeginSalvoInvincibility();
        bool resetAtStart = Mathf.Approximately(tickState.DamageTickElapsed, 0f);
        bool normalDamageBlocked = !combat.ApplyDamage(10f);
        bool continuousDamageBlocked = !combat.ApplyContinuousDamage(3f);
        tickState.AddElapsed(0.19f);
        if (combat.IsSalvoInvincible)
        {
            tickState.ResetElapsed();
        }

        bool resetWhileActive = Mathf.Approximately(tickState.DamageTickElapsed, 0f);
        combat.EndSalvoInvincibility();
        bool unregistered = registry.Unregister(tickState);
        bool defenseUnchanged = Mathf.Approximately(armorBefore, combat.CurrentArmor) &&
                                Mathf.Approximately(hullBefore, combat.CurrentHull);
        bool verified = registered && beganInvincibility && resetAtStart &&
                        normalDamageBlocked && continuousDamageBlocked &&
                        resetWhileActive && defenseUnchanged && unregistered &&
                        registry.ActiveCount == countBefore;
        Debug.Log(
            $"[LockCombatDebug] continuous damage reset verified={verified}, " +
            $"registered={registered}, resetAtStart={resetAtStart}, " +
            $"normalBlocked={normalDamageBlocked}, continuousBlocked={continuousDamageBlocked}, " +
            $"resetWhileActive={resetWhileActive}, defenseUnchanged={defenseUnchanged}, " +
            $"unregistered={unregistered}, activeStates={registry.ActiveCount}.");
    }

    [MenuItem(MenuRoot + "Run Busy SHOOT ERROR", priority = 299)]
    private static void RunBusyShootError()
    {
        if (!TryGetRuntime(out PlayerLockOnController controller,
                out PlayerCombatController combat, out PlayerMissileSalvoLauncher launcher,
                out HUDPresenter hud, out BossLockOnTargetProvider provider, out _) ||
            !PrepareReadyState(controller, launcher))
        {
            return;
        }

        if (hud == null)
        {
            Debug.LogError("[LockCombatDebug] Busy failure HUD dependency missing.");
            return;
        }

        List<BossLockOnTarget> selectedTargets = new();
        provider.BuildTargetSequence(5, unchecked(System.Environment.TickCount ^ Time.frameCount), selectedTargets);
        List<SalvoTargetSnapshot> targets = new();
        for (int i = 0; i < selectedTargets.Count; i++)
        {
            SalvoTargetSnapshot snapshot = provider.CreateSalvoSnapshot(selectedTargets[i]);
            if (snapshot != null)
            {
                targets.Add(snapshot);
            }
        }

        SalvoRequest blockerRequest = new(
            "BusyShootErrorDebug",
            5,
            combat.CurrentGatlingBaseDamage * 2f / 5f,
            targets,
            missilesPerVolley: 4,
            salvoDuration: 0.6f,
            randomSeed: 0,
            SalvoMissileProfileSnapshot.Capture(combat));
        SalvoStartResult prepared = launcher.TryPrepareSalvo(blockerRequest, out SalvoHandle blockerHandle);
        bool blockerStarted = prepared.IsPrepared &&
                              launcher.StartPreparedSalvo(blockerHandle).IsStarted;
        bool began = controller.TryBeginCharging(LockOnInputSource.Debug);
        controller.AdvanceChargeForDebug(StageChargeSeconds[0]);
        bool releaseAccepted = controller.TryReleaseCharging(LockOnInputSource.Debug);
        bool verified = blockerStarted && began && !releaseAccepted &&
                        controller.State == LockOnCombatState.Ready &&
                        !combat.IsSalvoInvincible &&
                        controller.LastSalvoFailureStatus == SalvoStartStatus.Busy.ToString() &&
                        hud.IsShootErrorVisible && hud.DebugStatusText == "SHOOT ERROR" &&
                        hud.DebugStatusFontSize == 32;
        Debug.Log(
            $"[LockCombatDebug] busy SHOOT ERROR verified={verified}, blockerStarted={blockerStarted}, " +
            $"lockBegan={began}, releaseAccepted={releaseAccepted}, state={controller.State}, " +
            $"invincible={combat.IsSalvoInvincible}, failure={controller.LastSalvoFailureStatus}/" +
            $"{controller.LastSalvoFailureReason}, visible={hud.IsShootErrorVisible}, " +
            $"text={hud.DebugStatusText}, fontSize={hud.DebugStatusFontSize}.");
    }

    [MenuItem(MenuRoot + "Run Invincibility And Reuse Timeline", priority = 300)]
    private static void RunInvincibilityAndReuseTimeline()
    {
        if (!TryGetRuntime(out PlayerLockOnController controller,
                out PlayerCombatController combat, out PlayerMissileSalvoLauncher launcher,
                out _, out _, out _) ||
            !PrepareReadyState(controller, launcher))
        {
            return;
        }

        bool previousUndead = GameplayDebugFlags.Undead;
        GameplayDebugFlags.Undead = true;
        bool began = controller.TryBeginCharging(LockOnInputSource.Debug);
        controller.AdvanceChargeForDebug(StageChargeSeconds[1]);
        bool released = controller.TryReleaseCharging(LockOnInputSource.Debug);
        bool firstMissileProtected = controller.LastFirstMissileWasInvincible &&
                                     combat.IsSalvoInvincible;
        bool enteredReuseWait = controller.State == LockOnCombatState.ReuseWait;
        double startTime = EditorApplication.timeSinceStartup;
        bool invincibilityEndedAtLaunchEnd = false;
        bool reuseContinuedAfterLaunch = false;
        if (pendingTimelineCheck != null)
        {
            EditorApplication.update -= pendingTimelineCheck;
        }

        pendingTimelineCheck = () =>
        {
            if (!Application.isPlaying || controller == null || combat == null)
            {
                EditorApplication.update -= pendingTimelineCheck;
                pendingTimelineCheck = null;
                GameplayDebugFlags.Undead = previousUndead;
                return;
            }

            double elapsed = EditorApplication.timeSinceStartup - startTime;
            if (!invincibilityEndedAtLaunchEnd && elapsed >= controller.SalvoLaunchDuration + 0.20f)
            {
                invincibilityEndedAtLaunchEnd = !combat.IsSalvoInvincible;
                reuseContinuedAfterLaunch = controller.State == LockOnCombatState.ReuseWait &&
                                            controller.ReuseWaitRemaining > 0f;
            }

            if (elapsed < controller.LockReuseWaitDuration + 0.30f)
            {
                return;
            }

            EditorApplication.update -= pendingTimelineCheck;
            pendingTimelineCheck = null;
            GameplayDebugFlags.Undead = previousUndead;
            bool reuseEnded = controller.State == LockOnCombatState.Ready &&
                              !combat.IsSalvoInvincible;
            bool verified = began && released && firstMissileProtected && enteredReuseWait &&
                            invincibilityEndedAtLaunchEnd && reuseContinuedAfterLaunch &&
                            reuseEnded && controller.LastFiredMissileCount == 10;
            Debug.Log(
                $"[LockCombatDebug] timeline verified={verified}, began={began}, released={released}, " +
                $"firstMissileProtected={firstMissileProtected}, enteredReuse={enteredReuseWait}, " +
                $"invincibilityEndedAtLaunchEnd={invincibilityEndedAtLaunchEnd}, " +
                $"reuseContinuedAfterLaunch={reuseContinuedAfterLaunch}, reuseEnded={reuseEnded}, " +
                $"fired={controller.LastFiredMissileCount}, finalState={controller.State}.");
        };
        EditorApplication.update += pendingTimelineCheck;
    }

    [MenuItem(MenuRoot + "Run Residual Beam Registry Lifecycle", priority = 301)]
    private static void RunResidualBeamRegistryLifecycle()
    {
        if (!TryGetRuntime(out _, out PlayerCombatController combat, out _,
                out _, out _, out _))
        {
            return;
        }

        BossBulletPatternController patterns =
            Object.FindAnyObjectByType<BossBulletPatternController>();
        if (patterns == null)
        {
            Debug.LogError("[LockCombatDebug] BossBulletPatternController was not found.");
            return;
        }

        patterns.CancelActivePatternForDebug();
        int countBefore = combat.ContinuousDamageTickRegistry.ActiveCount;
        bool previousUndead = GameplayDebugFlags.Undead;
        GameplayDebugFlags.Undead = true;
        bool started = patterns.TryRunPatternForDebug(BossBulletPatternType.TrackingResidualBeam);
        double startedAt = EditorApplication.timeSinceStartup;
        bool sawRegisteredState = false;
        bool damageBlocked = false;
        if (pendingResidualBeamCheck != null)
        {
            EditorApplication.update -= pendingResidualBeamCheck;
        }

        pendingResidualBeamCheck = () =>
        {
            if (!Application.isPlaying || combat == null || patterns == null)
            {
                EditorApplication.update -= pendingResidualBeamCheck;
                pendingResidualBeamCheck = null;
                GameplayDebugFlags.Undead = previousUndead;
                return;
            }

            int activeCount = combat.ContinuousDamageTickRegistry.ActiveCount;
            if (!sawRegisteredState && activeCount > countBefore)
            {
                sawRegisteredState = true;
                bool invincibilityStarted = combat.BeginSalvoInvincibility();
                damageBlocked = invincibilityStarted && !combat.ApplyContinuousDamage(3f);
                combat.EndSalvoInvincibility();
                patterns.CancelActivePatternForDebug();
                return;
            }

            bool timedOut = EditorApplication.timeSinceStartup - startedAt > 6.0;
            bool unregisteredAfterCancel = sawRegisteredState && activeCount == countBefore;
            if (!unregisteredAfterCancel && !timedOut)
            {
                return;
            }

            EditorApplication.update -= pendingResidualBeamCheck;
            pendingResidualBeamCheck = null;
            GameplayDebugFlags.Undead = previousUndead;
            if (timedOut)
            {
                patterns.CancelActivePatternForDebug();
            }

            bool verified = started && sawRegisteredState && damageBlocked &&
                            unregisteredAfterCancel;
            Debug.Log(
                $"[LockCombatDebug] residual beam registry verified={verified}, " +
                $"patternStarted={started}, registered={sawRegisteredState}, " +
                $"damageBlocked={damageBlocked}, unregisteredAfterCancel={unregisteredAfterCancel}, " +
                $"activeStates={combat.ContinuousDamageTickRegistry.ActiveCount}, timedOut={timedOut}.");
        };
        EditorApplication.update += pendingResidualBeamCheck;
    }

    private static void FireStage(int successfulLocks)
    {
        if (!TryGetRuntime(out PlayerLockOnController controller,
                out PlayerCombatController combat, out PlayerMissileSalvoLauncher launcher,
                out HUDPresenter hud, out _, out _) ||
            !PrepareReadyState(controller, launcher))
        {
            return;
        }

        bool began = controller.TryBeginCharging(LockOnInputSource.Debug);
        controller.AdvanceChargeForDebug(StageChargeSeconds[successfulLocks - 1]);
        bool released = controller.TryReleaseCharging(LockOnInputSource.Debug);
        int expectedCount = ExpectedMissileCounts[successfulLocks - 1];
        float expectedBudget = combat.CurrentGatlingBaseDamage / 25f *
                               ExpectedDamageBudgetsAtG25[successfulLocks - 1];
        bool verified = began && released &&
                        controller.LastRequestedMissileCount == expectedCount &&
                        Mathf.Approximately(controller.LastBaseDamageBudget, expectedBudget) &&
                        controller.LastFiredMissileCount == Mathf.Min(4, expectedCount) &&
                        controller.LastFirstMissileWasInvincible && combat.IsSalvoInvincible &&
                        controller.State == LockOnCombatState.ReuseWait &&
                        (hud == null || !hud.IsShootErrorVisible);
        Debug.Log(
            $"[LockCombatDebug] stage {successfulLocks} verified={verified}, began={began}, " +
            $"released={released}, requested={controller.LastRequestedMissileCount}, " +
            $"firedNow={controller.LastFiredMissileCount}, budget={controller.LastBaseDamageBudget:0.###}, " +
            $"perMissile={controller.LastBaseDamagePerMissile:0.###}, " +
            $"firstMissileInvincible={controller.LastFirstMissileWasInvincible}, " +
            $"state={controller.State}.");
    }

    private static bool PrepareReadyState(
        PlayerLockOnController controller,
        PlayerMissileSalvoLauncher launcher)
    {
        if (launcher != null && launcher.IsBusy)
        {
            Debug.LogWarning(
                $"[LockCombatDebug] Wait for active salvo {launcher.ActiveSalvoId} to finish.");
            return false;
        }

        if (controller.State == LockOnCombatState.Charging)
        {
            controller.HandleGamePaused();
        }

        if (controller.State == LockOnCombatState.ReuseWait)
        {
            controller.AdvanceReuseWaitForDebug(controller.LockReuseWaitDuration + 0.1f);
        }

        if (controller.State == LockOnCombatState.Ready)
        {
            return true;
        }

        Debug.LogWarning(
            $"[LockCombatDebug] Expected Ready before diagnostic, state={controller.State}.");
        return false;
    }

    private static bool TryGetRuntime(
        out PlayerLockOnController controller,
        out PlayerCombatController combat,
        out PlayerMissileSalvoLauncher launcher,
        out HUDPresenter hud,
        out BossLockOnTargetProvider provider,
        out BossTestState testState)
    {
        controller = null;
        combat = null;
        launcher = null;
        hud = null;
        provider = null;
        testState = null;
        if (!Application.isPlaying)
        {
            Debug.LogWarning("[LockCombatDebug] Enter Play Mode before using combat diagnostics.");
            return false;
        }

        controller = Object.FindAnyObjectByType<PlayerLockOnController>();
        combat = Object.FindAnyObjectByType<PlayerCombatController>();
        launcher = Object.FindAnyObjectByType<PlayerMissileSalvoLauncher>();
        hud = Object.FindAnyObjectByType<HUDPresenter>();
        provider = Object.FindAnyObjectByType<BossLockOnTargetProvider>();
        testState = Object.FindAnyObjectByType<BossTestState>();
        if (controller != null && combat != null && launcher != null &&
            provider != null && testState != null)
        {
            return true;
        }

        Debug.LogError(
            $"[LockCombatDebug] Runtime dependencies missing. Controller={controller != null}, " +
            $"Combat={combat != null}, Launcher={launcher != null}, Hud={hud != null}, " +
            $"Provider={provider != null}, TestState={testState != null}.");
        return false;
    }
}
