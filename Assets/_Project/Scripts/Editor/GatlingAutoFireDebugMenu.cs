using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem;

public static class GatlingAutoFireDebugMenu
{
    private const string MenuPath =
        "TitanDestroyer/Debug/Player Combat/Run Automatic Gatling Cycle";
    private const double FireSampleTime = 0.35;
    private const double CooldownStartSampleTime = 2.25;
    private const double CooldownEndSampleTime = 3.75;
    private const double NextBurstSampleTime = 4.35;
    private const double Timeout = 6.0;
    private static VerificationContext context;

    [MenuItem(MenuPath, priority = 270)]
    private static void RunVerification()
    {
        AbortPending();
        if (!Application.isPlaying)
        {
            Debug.LogWarning("[GatlingAutoDebug] Enter Play Mode before running verification.");
            return;
        }

        PlayerCombatController combat =
            Object.FindAnyObjectByType<PlayerCombatController>();
        BattleController battle = Object.FindAnyObjectByType<BattleController>();
        BossController boss = Object.FindAnyObjectByType<BossController>();
        if (combat == null || battle == null || boss == null ||
            !battle.IsBattleActive || !combat.IsAlive || !boss.IsAlive)
        {
            Debug.LogError(
                $"[GatlingAutoDebug] Runtime unavailable. combat={combat != null}, " +
                $"battleActive={battle != null && battle.IsBattleActive}, " +
                $"playerAlive={combat != null && combat.IsAlive}, " +
                $"bossAlive={boss != null && boss.IsAlive}.");
            return;
        }

        Mouse mouse = Mouse.current;
        Keyboard keyboard = Keyboard.current;
        bool manualInputIdle =
            (mouse == null || !mouse.leftButton.isPressed) &&
            (keyboard == null || !keyboard.spaceKey.isPressed);
        combat.ResetAutomaticGatlingCycleForDebug();
        context = new VerificationContext
        {
            Combat = combat,
            StartedAt = EditorApplication.timeSinceStartup,
            ManualInputIdle = manualInputIdle,
        };
        EditorApplication.update += TickVerification;
        Debug.Log(
            $"[GatlingAutoDebug] Started. manualInputIdle={manualInputIdle}, " +
            $"burst={combat.DebugAutomaticFireBurstDuration:0.00}s, " +
            $"cooldown={combat.DebugAutomaticFireCooldownDuration:0.00}s.");
    }

    private static void TickVerification()
    {
        if (context == null)
        {
            return;
        }

        if (!Application.isPlaying || context.Combat == null)
        {
            Debug.LogError("[GatlingAutoDebug] Play Mode ended during verification.");
            AbortPending();
            return;
        }

        double elapsed = EditorApplication.timeSinceStartup - context.StartedAt;
        if (elapsed > Timeout)
        {
            Debug.LogError("[GatlingAutoDebug] Verification timed out.");
            AbortPending();
            return;
        }

        if (!context.FireSampleCaptured && elapsed >= FireSampleTime)
        {
            context.FireSampleCaptured = true;
            context.ShotsDuringFirstBurst = context.Combat.DebugGatlingShotsFired;
            context.FirstBurstObserved = context.Combat.IsAutomaticGatlingFiring &&
                                         context.ShotsDuringFirstBurst > 0;
        }

        if (!context.CooldownStartCaptured && elapsed >= CooldownStartSampleTime)
        {
            context.CooldownStartCaptured = true;
            context.ShotsAtCooldownStart = context.Combat.DebugGatlingShotsFired;
            context.CooldownObserved = !context.Combat.IsAutomaticGatlingFireWindow;
        }

        if (!context.CooldownEndCaptured && elapsed >= CooldownEndSampleTime)
        {
            context.CooldownEndCaptured = true;
            context.ShotsAtCooldownEnd = context.Combat.DebugGatlingShotsFired;
            context.CooldownStayedQuiet =
                !context.Combat.IsAutomaticGatlingFireWindow &&
                context.ShotsAtCooldownEnd == context.ShotsAtCooldownStart;
        }

        if (elapsed < NextBurstSampleTime)
        {
            return;
        }

        int finalShots = context.Combat.DebugGatlingShotsFired;
        bool durationsMatch =
            Mathf.Approximately(context.Combat.DebugAutomaticFireBurstDuration, 2f) &&
            Mathf.Approximately(context.Combat.DebugAutomaticFireCooldownDuration, 2f);
        bool nextBurstObserved = context.Combat.IsAutomaticGatlingFiring &&
                                 finalShots > context.ShotsAtCooldownEnd;
        bool verified = context.ManualInputIdle && durationsMatch &&
                        context.FirstBurstObserved && context.CooldownObserved &&
                        context.CooldownStayedQuiet && nextBurstObserved;
        Debug.Log(
            $"[GatlingAutoDebug] verified={verified}, " +
            $"manualInputIdle={context.ManualInputIdle}, durationsMatch={durationsMatch}, " +
            $"firstBurst={context.FirstBurstObserved}/{context.ShotsDuringFirstBurst}, " +
            $"cooldown={context.CooldownObserved}, " +
            $"cooldownShots={context.ShotsAtCooldownStart}->{context.ShotsAtCooldownEnd}, " +
            $"quiet={context.CooldownStayedQuiet}, " +
            $"nextBurst={nextBurstObserved}, finalShots={finalShots}.");
        AbortPending();
    }

    private static void AbortPending()
    {
        EditorApplication.update -= TickVerification;
        context = null;
    }

    private sealed class VerificationContext
    {
        public PlayerCombatController Combat;
        public double StartedAt;
        public bool ManualInputIdle;
        public bool FireSampleCaptured;
        public bool CooldownStartCaptured;
        public bool CooldownEndCaptured;
        public bool FirstBurstObserved;
        public bool CooldownObserved;
        public bool CooldownStayedQuiet;
        public int ShotsDuringFirstBurst;
        public int ShotsAtCooldownStart;
        public int ShotsAtCooldownEnd;
    }
}
