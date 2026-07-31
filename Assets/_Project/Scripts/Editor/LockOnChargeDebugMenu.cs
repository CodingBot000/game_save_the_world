using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;

public static class LockOnChargeDebugMenu
{
    private const string MenuRoot = "TitanDestroyer/Debug/Lock-On Charge/";
    private static EditorApplication.CallbackFunction pendingGraceCheck;

    [MenuItem(MenuRoot + "Log State", priority = 280)]
    private static void LogState()
    {
        if (!TryGetRuntime(out PlayerLockOnController controller,
                out BossLockOnTargetProvider provider, out LockOnHudPresenter hud))
        {
            return;
        }

        LogSnapshot("state", controller, provider, hud);
    }

    [MenuItem(MenuRoot + "Run Full Charge Preview", priority = 281)]
    private static void RunFullChargePreview()
    {
        if (!TryGetRuntime(out PlayerLockOnController controller,
                out BossLockOnTargetProvider provider, out LockOnHudPresenter hud) ||
            !PrepareReadyState(controller, provider))
        {
            return;
        }

        bool began = controller.TryBeginCharging(LockOnInputSource.Debug);
        controller.AdvanceChargeForDebug(2.6f);
        hud?.RefreshForDebug();
        int chargedStage = controller.ChargeStage;
        int successfulLocks = controller.SuccessfulLockCount;
        int assignedLocks = controller.AssignedLockCount;
        int visibleMarkers = hud != null ? hud.VisibleMarkerCount : -1;
        bool released = controller.TryReleaseCharging(LockOnInputSource.Debug);
        hud?.RefreshForDebug();
        LockOnReleaseIntent intent = controller.LastReleaseIntent;
        PlayerCombatController combat =
            Object.FindAnyObjectByType<PlayerCombatController>();
        bool verified = began && chargedStage == 5 && successfulLocks == 5 &&
                        assignedLocks == 5 && released && intent != null &&
                        intent.SuccessfulLockCount == 5 &&
                        intent.TargetSnapshots.Count == 5 &&
                        controller.State == LockOnCombatState.ReuseWait &&
                        controller.LastRequestedMissileCount == 30 &&
                        controller.LastFirstMissileWasInvincible &&
                        combat != null && combat.IsSalvoInvincible;
        Debug.Log(
            $"[LockChargeDebug] full charge verified={verified}, began={began}, " +
            $"stage={chargedStage}, success={successfulLocks}, assigned={assignedLocks}, " +
            $"visibleMarkers={visibleMarkers}, released={released}, " +
            $"intentLocks={(intent != null ? intent.SuccessfulLockCount : -1)}, " +
            $"snapshots={(intent != null ? intent.TargetSnapshots.Count : -1)}, " +
            $"requested={controller.LastRequestedMissileCount}, " +
            $"firedNow={controller.LastFiredMissileCount}, " +
            $"firstMissileInvincible={controller.LastFirstMissileWasInvincible}, " +
            $"finalState={controller.State}.");
    }

    [MenuItem(MenuRoot + "Run UI Pointer Exit Cancel", priority = 282)]
    private static void RunPointerExitCancel()
    {
        if (!TryGetRuntime(out PlayerLockOnController controller,
                out BossLockOnTargetProvider provider, out LockOnHudPresenter hud) ||
            !PrepareReadyState(controller, provider))
        {
            return;
        }

        LockOnButtonInputRelay relay =
            Object.FindAnyObjectByType<LockOnButtonInputRelay>();
        EventSystem eventSystem = EventSystem.current;
        if (relay == null || eventSystem == null)
        {
            Debug.LogError(
                $"[LockChargeDebug] UI relay test unavailable. RelayFound={relay != null}, " +
                $"EventSystemFound={eventSystem != null}.");
            return;
        }

        PointerEventData pointer = new(eventSystem)
        {
            pointerId = -1,
            button = PointerEventData.InputButton.Left,
        };
        relay.OnPointerDown(pointer);
        bool began = controller.State == LockOnCombatState.Charging && relay.HasActivePointer;
        controller.AdvanceChargeForDebug(0.8f);
        relay.OnPointerExit(pointer);
        hud?.RefreshForDebug();
        bool verified = began && controller.State == LockOnCombatState.Ready &&
                        !relay.HasActivePointer && controller.LastReleaseIntent == null;
        Debug.Log(
            $"[LockChargeDebug] pointer exit verified={verified}, began={began}, " +
            $"finalState={controller.State}, activePointer={relay.HasActivePointer}, " +
            $"releaseIntentCreated={controller.LastReleaseIntent != null}.");
    }

    [MenuItem(MenuRoot + "Run Pause Cancel", priority = 283)]
    private static void RunPauseCancel()
    {
        if (!TryGetRuntime(out PlayerLockOnController controller,
                out BossLockOnTargetProvider provider, out LockOnHudPresenter hud) ||
            !PrepareReadyState(controller, provider))
        {
            return;
        }

        LockOnButtonInputRelay relay =
            Object.FindAnyObjectByType<LockOnButtonInputRelay>();
        EventSystem eventSystem = EventSystem.current;
        if (relay == null || eventSystem == null)
        {
            Debug.LogError(
                $"[LockChargeDebug] pause test unavailable. RelayFound={relay != null}, " +
                $"EventSystemFound={eventSystem != null}.");
            return;
        }

        PointerEventData pointer = new(eventSystem)
        {
            pointerId = -1,
            button = PointerEventData.InputButton.Left,
        };
        relay.OnPointerDown(pointer);
        bool began = controller.State == LockOnCombatState.Charging && relay.HasActivePointer;
        controller.AdvanceChargeForDebug(0.8f);
        controller.HandleGamePaused();
        hud?.RefreshForDebug();
        bool verified = began && controller.State == LockOnCombatState.Ready &&
                        controller.LastReleaseIntent == null && !relay.HasActivePointer;
        Debug.Log(
            $"[LockChargeDebug] pause cancel verified={verified}, began={began}, " +
            $"finalState={controller.State}, " +
            $"activePointer={relay.HasActivePointer}, " +
            $"releaseIntentCreated={controller.LastReleaseIntent != null}.");
    }

    [MenuItem(MenuRoot + "Run No-Target Input Block", priority = 284)]
    private static void RunNoTargetInputBlock()
    {
        if (!TryGetRuntime(out PlayerLockOnController controller,
                out BossLockOnTargetProvider provider, out LockOnHudPresenter hud) ||
            !PrepareReadyState(controller, provider))
        {
            return;
        }

        provider.SetAllTargetsAttackableForDebug(false);
        bool beganWithoutTargets = controller.TryBeginCharging(LockOnInputSource.Debug);
        hud?.RefreshForDebug();
        bool disabledWithoutTargets = hud == null || !hud.ButtonInteractable;
        provider.SetAllTargetsAttackableForDebug(true);
        hud?.RefreshForDebug();
        bool restored = provider.HasValidTargets && controller.IsLockInputAvailable;
        bool verified = !beganWithoutTargets && disabledWithoutTargets && restored;
        Debug.Log(
            $"[LockChargeDebug] no-target block verified={verified}, " +
            $"beginAccepted={beganWithoutTargets}, buttonDisabled={disabledWithoutTargets}, " +
            $"restored={restored}.");
    }

    [MenuItem(MenuRoot + "Run Target-Loss Grace", priority = 285)]
    private static void RunTargetLossGrace()
    {
        if (!TryGetRuntime(out PlayerLockOnController controller,
                out BossLockOnTargetProvider provider, out LockOnHudPresenter hud) ||
            !PrepareReadyState(controller, provider))
        {
            return;
        }

        bool began = controller.TryBeginCharging(LockOnInputSource.Debug);
        controller.AdvanceChargeForDebug(0.8f);
        provider.SetAllTargetsAttackableForDebug(false);
        hud?.RefreshForDebug();
        bool chargingDuringGrace = controller.State == LockOnCombatState.Charging;
        bool buttonDisabledDuringGrace = hud == null || !hud.ButtonInteractable;
        double checkAt = EditorApplication.timeSinceStartup +
                         controller.TargetGraceTime + 0.35f;
        if (pendingGraceCheck != null)
        {
            EditorApplication.update -= pendingGraceCheck;
        }

        pendingGraceCheck = () =>
        {
            if (EditorApplication.timeSinceStartup < checkAt)
            {
                return;
            }

            EditorApplication.update -= pendingGraceCheck;
            pendingGraceCheck = null;
            bool canceledAfterGrace = controller != null &&
                                      controller.State == LockOnCombatState.Ready;
            provider?.SetAllTargetsAttackableForDebug(true);
            hud?.RefreshForDebug();
            bool verified = began && chargingDuringGrace &&
                            buttonDisabledDuringGrace && canceledAfterGrace;
            Debug.Log(
                $"[LockChargeDebug] target-loss grace verified={verified}, " +
                $"began={began}, chargingDuringGrace={chargingDuringGrace}, " +
                $"buttonDisabledDuringGrace={buttonDisabledDuringGrace}, " +
                $"canceledAfterGrace={canceledAfterGrace}, " +
                $"grace={controller?.TargetGraceTime:0.00}s, targetsRestored=True.");
        };
        EditorApplication.update += pendingGraceCheck;
    }

    [MenuItem(MenuRoot + "Run Weak-Point Refresh Preservation", priority = 286)]
    private static void RunWeakPointRefreshPreservation()
    {
        if (!TryGetRuntime(out PlayerLockOnController controller,
                out BossLockOnTargetProvider provider, out LockOnHudPresenter hud) ||
            !PrepareReadyState(controller, provider))
        {
            return;
        }

        BossTestState testState = Object.FindAnyObjectByType<BossTestState>();
        if (testState == null)
        {
            Debug.LogError("[LockChargeDebug] BossTestState was not found.");
            return;
        }

        testState.SetWeakPointOpen(true);
        bool began = controller.TryBeginCharging(LockOnInputSource.Debug);
        controller.AdvanceChargeForDebug(2.6f);
        int successBeforeClose = controller.SuccessfulLockCount;
        testState.SetWeakPointOpen(false);
        hud?.RefreshForDebug();
        int successAfterClose = controller.SuccessfulLockCount;
        int assignedAfterClose = controller.AssignedLockCount;
        bool verified = began && successBeforeClose == 5 &&
                        successAfterClose == 5 && assignedAfterClose == 5 &&
                        controller.State == LockOnCombatState.Charging;
        controller.HandleGamePaused();
        Debug.Log(
            $"[LockChargeDebug] weak-point refresh verified={verified}, began={began}, " +
            $"successBeforeClose={successBeforeClose}, successAfterClose={successAfterClose}, " +
            $"assignedAfterClose={assignedAfterClose}, finalState={controller.State}.");
    }

    private static bool PrepareReadyState(
        PlayerLockOnController controller,
        BossLockOnTargetProvider provider)
    {
        provider.SetAllTargetsAttackableForDebug(true);
        if (controller.State == LockOnCombatState.Charging)
        {
            controller.HandleGamePaused();
        }

        if (controller.State == LockOnCombatState.ReuseWait)
        {
            controller.AdvanceReuseWaitForDebug(controller.LockReuseWaitDuration + 0.1f);
        }

        PlayerMissileSalvoLauncher launcher =
            Object.FindAnyObjectByType<PlayerMissileSalvoLauncher>();
        if (launcher != null && launcher.IsBusy)
        {
            Debug.LogWarning(
                $"[LockChargeDebug] Wait for active salvo {launcher.ActiveSalvoId} to finish before running this diagnostic.");
            return false;
        }

        if (controller.State == LockOnCombatState.Ready)
        {
            return true;
        }

        Debug.LogWarning(
            $"[LockChargeDebug] Expected Ready before diagnostic, state={controller.State}.");
        return false;
    }

    private static bool TryGetRuntime(
        out PlayerLockOnController controller,
        out BossLockOnTargetProvider provider,
        out LockOnHudPresenter hud)
    {
        controller = null;
        provider = null;
        hud = null;
        if (!Application.isPlaying)
        {
            Debug.LogWarning(
                "[LockChargeDebug] Enter Play Mode before using lock-on charge diagnostics.");
            return false;
        }

        controller = Object.FindAnyObjectByType<PlayerLockOnController>();
        provider = Object.FindAnyObjectByType<BossLockOnTargetProvider>();
        hud = Object.FindAnyObjectByType<LockOnHudPresenter>();
        if (controller != null && provider != null)
        {
            return true;
        }

        Debug.LogError(
            $"[LockChargeDebug] Runtime dependencies missing. " +
            $"ControllerFound={controller != null}, ProviderFound={provider != null}, " +
            $"HudFound={hud != null}.");
        return false;
    }

    private static void LogSnapshot(
        string reason,
        PlayerLockOnController controller,
        BossLockOnTargetProvider provider,
        LockOnHudPresenter hud)
    {
        PlayerCombatController combat =
            Object.FindAnyObjectByType<PlayerCombatController>();
        Debug.Log(
            $"[LockChargeDebug] {reason}: state={controller.State}, " +
            $"source={controller.ActiveInputSource}, charge={controller.ChargeElapsed:0.000}, " +
            $"stage={controller.ChargeStage}, success={controller.SuccessfulLockCount}, " +
            $"assigned={controller.AssignedLockCount}, validTargets={provider.ValidTargetCount}, " +
            $"inputAvailable={controller.IsLockInputAvailable}, " +
            $"button={(hud != null ? hud.ButtonLabelText : "<missing>")}, " +
            $"buttonInteractable={(hud != null && hud.ButtonInteractable)}, " +
            $"specialHidden={(hud != null && hud.LegacySpecialHidden)}, " +
            $"legacyMissileInput={(combat != null && combat.LegacyMissileInputEnabled)}.");
    }
}
