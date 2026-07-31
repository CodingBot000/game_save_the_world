using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

public static class BossLockOnDebugMenu
{
    private const string MenuRoot = "TitanDestroyer/Debug/Boss Lock-On/";
    private const int DiagnosticSeed = 7301;

    [MenuItem(MenuRoot + "Set Phase 1", priority = 240)]
    private static void SetPhaseOne() => SetPhase(1);

    [MenuItem(MenuRoot + "Set Phase 2", priority = 241)]
    private static void SetPhaseTwo() => SetPhase(2);

    [MenuItem(MenuRoot + "Set Phase 3", priority = 242)]
    private static void SetPhaseThree() => SetPhase(3);

    [MenuItem(MenuRoot + "Advance Phase", priority = 243)]
    private static void AdvancePhase()
    {
        if (!TryGetState(out BossTestState state))
        {
            return;
        }

        state.AdvancePhase();
        LogState("phase advanced");
    }

    [MenuItem(MenuRoot + "Weak Point/Open", priority = 250)]
    private static void OpenWeakPoint() => SetWeakPoint(true);

    [MenuItem(MenuRoot + "Weak Point/Close", priority = 251)]
    private static void CloseWeakPoint() => SetWeakPoint(false);

    [MenuItem(MenuRoot + "Weak Point/Toggle", priority = 252)]
    private static void ToggleWeakPoint()
    {
        if (!TryGetState(out BossTestState state))
        {
            return;
        }

        state.ToggleWeakPoint();
        LogState("weak point toggled");
    }

    [MenuItem(MenuRoot + "Targets/Enable All", priority = 260)]
    private static void EnableAllTargets() => SetAllTargetsAttackable(true);

    [MenuItem(MenuRoot + "Targets/Disable All", priority = 261)]
    private static void DisableAllTargets() => SetAllTargetsAttackable(false);

    [MenuItem(MenuRoot + "Target Priority/Set Sample Strong And Recent", priority = 263)]
    private static void SetSampleTargetPriorities()
    {
        if (!TryGetProvider(out BossLockOnTargetProvider provider))
        {
            return;
        }

        for (int i = 0; i < provider.Targets.Count; i++)
        {
            BossLockOnTarget target = provider.Targets[i];
            if (target == null)
            {
                continue;
            }

            target.SetPreparingStrongAttack(target.TargetId == "boss.core");
            if (target.TargetId == "boss.lower")
            {
                target.MarkRecentlyAttacked();
            }
        }

        LogState("sample priorities set");
    }

    [MenuItem(MenuRoot + "Target Priority/Clear Strong", priority = 264)]
    private static void ClearStrongTargetPriority()
    {
        if (!TryGetProvider(out BossLockOnTargetProvider provider))
        {
            return;
        }

        for (int i = 0; i < provider.Targets.Count; i++)
        {
            provider.Targets[i]?.SetPreparingStrongAttack(false);
        }

        LogState("strong priority cleared");
    }

    [MenuItem(MenuRoot + "Flash Component/Enable", priority = 265)]
    private static void EnableFlashComponent() => SetFlashComponentEnabled(true);

    [MenuItem(MenuRoot + "Flash Component/Disable", priority = 266)]
    private static void DisableFlashComponent() => SetFlashComponentEnabled(false);

    [MenuItem(MenuRoot + "Target Lifecycle/Verify Runtime Registration", priority = 267)]
    private static void VerifyRuntimeTargetRegistration()
    {
        if (!TryGetProvider(out BossLockOnTargetProvider provider))
        {
            return;
        }

        int beforeCount = provider.Targets.Count;
        GameObject probeObject = new("LockOnTarget_RuntimeRegistrationProbe");
        probeObject.transform.SetParent(provider.transform, false);
        BossLockOnTarget probe = probeObject.AddComponent<BossLockOnTarget>();
        bool registered = false;
        for (int i = 0; i < provider.Targets.Count; i++)
        {
            if (provider.Targets[i] == probe)
            {
                registered = true;
                break;
            }
        }

        Debug.Log(
            $"[LockOnDebug] runtime registration verified={registered}, " +
            $"before={beforeCount}, after={provider.Targets.Count}, valid={provider.ValidTargetCount}.");
        Object.Destroy(probeObject);
    }

    [MenuItem(MenuRoot + "Target Lifecycle/Verify Core Anchor Rebind", priority = 268)]
    private static void VerifyCoreAnchorRebind()
    {
        if (!TryGetProvider(out BossLockOnTargetProvider provider))
        {
            return;
        }

        BossLockOnTarget core = null;
        for (int i = 0; i < provider.Targets.Count; i++)
        {
            if (provider.Targets[i] != null && provider.Targets[i].TargetId == "boss.core")
            {
                core = provider.Targets[i];
                break;
            }
        }

        if (core == null)
        {
            Debug.LogError("[LockOnDebug] boss.core target was not found.");
            return;
        }

        SerializedObject serializedTarget = new(core);
        SerializedProperty anchorProperty =
            serializedTarget.FindProperty("anchorTransform");
        if (anchorProperty == null)
        {
            Debug.LogError("[LockOnDebug] anchorTransform property was not found.");
            return;
        }

        anchorProperty.objectReferenceValue = null;
        serializedTarget.ApplyModifiedPropertiesWithoutUndo();
        int validCount = provider.ValidTargetCount;
        bool rebound = core.AnchorTransform != null && core.AnchorTransform.name == "Spine 02";
        Debug.Log(
            $"[LockOnDebug] core anchor rebind verified={rebound}, " +
            $"anchor={(core.AnchorTransform != null ? core.AnchorTransform.name : "<missing>")}, " +
            $"valid={validCount}.");
    }

    [MenuItem(MenuRoot + "Log State", priority = 270)]
    private static void LogCurrentState() => LogState("state requested");

    private static void SetPhase(int phase)
    {
        if (!TryGetState(out BossTestState state))
        {
            return;
        }

        state.SetPhase(phase);
        LogState($"phase set to {phase}");
    }

    private static void SetWeakPoint(bool open)
    {
        if (!TryGetState(out BossTestState state))
        {
            return;
        }

        state.SetWeakPointOpen(open);
        LogState(open ? "weak point opened" : "weak point closed");
    }

    private static void SetAllTargetsAttackable(bool attackable)
    {
        if (!EnsurePlaying())
        {
            return;
        }

        BossLockOnTargetProvider provider =
            Object.FindAnyObjectByType<BossLockOnTargetProvider>();
        if (provider == null)
        {
            Debug.LogError("[LockOnDebug] BossLockOnTargetProvider was not found.");
            return;
        }

        provider.SetAllTargetsAttackableForDebug(attackable);
        LogState(attackable ? "all targets enabled" : "all targets disabled");
    }

    private static void SetFlashComponentEnabled(bool enabled)
    {
        if (!EnsurePlaying())
        {
            return;
        }

        BossWeakPointDebugFlash flash =
            Object.FindAnyObjectByType<BossWeakPointDebugFlash>(FindObjectsInactive.Include);
        if (flash == null)
        {
            Debug.LogError("[LockOnDebug] BossWeakPointDebugFlash was not found.");
            return;
        }

        flash.enabled = enabled;
        LogState(enabled ? "flash component enabled" : "flash component disabled");
    }

    private static bool TryGetState(out BossTestState state)
    {
        state = null;
        if (!EnsurePlaying())
        {
            return false;
        }

        state = Object.FindAnyObjectByType<BossTestState>();
        if (state != null)
        {
            return true;
        }

        Debug.LogError("[LockOnDebug] BossTestState was not found.");
        return false;
    }

    private static bool TryGetProvider(out BossLockOnTargetProvider provider)
    {
        provider = null;
        if (!EnsurePlaying())
        {
            return false;
        }

        provider = Object.FindAnyObjectByType<BossLockOnTargetProvider>();
        if (provider != null)
        {
            return true;
        }

        Debug.LogError("[LockOnDebug] BossLockOnTargetProvider was not found.");
        return false;
    }

    private static bool EnsurePlaying()
    {
        if (Application.isPlaying)
        {
            return true;
        }

        Debug.LogWarning("[LockOnDebug] Enter Play Mode before using boss lock-on diagnostics.");
        return false;
    }

    private static void LogState(string reason)
    {
        if (!EnsurePlaying())
        {
            return;
        }

        BossTestState state = Object.FindAnyObjectByType<BossTestState>();
        BossLockOnTargetProvider provider =
            Object.FindAnyObjectByType<BossLockOnTargetProvider>();
        BossPhaseDebugHud phaseHud = Object.FindAnyObjectByType<BossPhaseDebugHud>();
        BossWeakPointDebugFlash flash =
            Object.FindAnyObjectByType<BossWeakPointDebugFlash>(FindObjectsInactive.Include);
        if (state == null || provider == null)
        {
            Debug.LogError(
                $"[LockOnDebug] Cannot log state. StateFound={state != null}, ProviderFound={provider != null}.");
            return;
        }

        List<BossLockOnTarget> sequence = new();
        provider.BuildTargetSequence(
            5,
            DiagnosticSeed,
            sequence,
            recordLockAssignments: false);
        HashSet<string> uniqueIds = new();
        List<string> targetIds = new(sequence.Count);
        for (int i = 0; i < sequence.Count; i++)
        {
            string id = sequence[i] != null ? sequence[i].TargetId : "<missing>";
            targetIds.Add(id);
            uniqueIds.Add(id);
        }

        List<string> anchors = new(provider.Targets.Count);
        for (int i = 0; i < provider.Targets.Count; i++)
        {
            BossLockOnTarget target = provider.Targets[i];
            string id = target != null ? target.TargetId : "<missing>";
            string anchor = target != null && target.AnchorTransform != null
                ? target.AnchorTransform.name
                : "<missing>";
            anchors.Add($"{id}@{anchor}");
        }

        string phaseText = phaseHud != null && phaseHud.PhaseText != null
            ? phaseHud.PhaseText.text
            : "<missing>";
        bool weakFirst = sequence.Count > 0 && sequence[0] != null &&
                         sequence[0].IsWeakPointOpen;
        string flashSummary = flash != null
            ? $"flashRenderers={flash.FlashRendererCount}, flashing={flash.IsFlashing}"
            : "flash=<missing>";
        Debug.Log(
            $"[LockOnDebug] {reason}. phase={state.CurrentPhase}, phaseText={phaseText}, " +
            $"weakOpen={state.IsWeakPointOpen}, targets={provider.Targets.Count}, " +
            $"valid={provider.ValidTargetCount}, sequence=[{string.Join(",", targetIds)}], " +
            $"unique={uniqueIds.Count == sequence.Count}, weakFirst={weakFirst}, " +
            $"anchors=[{string.Join(",", anchors)}], {flashSummary}");
    }
}
