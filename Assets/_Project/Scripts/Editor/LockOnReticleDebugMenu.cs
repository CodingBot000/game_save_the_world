using UnityEditor;
using UnityEngine;

public static class LockOnReticleDebugMenu
{
    private const string MenuPath =
        "TitanDestroyer/Debug/Lock-On Feedback/Run Reticle Persistence Verification";
    private const double TimeoutSeconds = 5.0;
    private static VerificationContext context;

    [MenuItem(MenuPath, priority = 331)]
    private static void RunVerification()
    {
        AbortPending();
        if (!Application.isPlaying)
        {
            Debug.LogWarning("[LockReticleDebug] Enter Play Mode before running verification.");
            return;
        }

        PlayerLockOnController lockOn = Object.FindAnyObjectByType<PlayerLockOnController>();
        PlayerMissileSalvoLauncher launcher = Object.FindAnyObjectByType<PlayerMissileSalvoLauncher>();
        LockOnHudPresenter hud = Object.FindAnyObjectByType<LockOnHudPresenter>();
        if (lockOn == null || launcher == null || hud == null)
        {
            Debug.LogError(
                $"[LockReticleDebug] Dependencies missing. lockOn={lockOn != null}, " +
                $"launcher={launcher != null}, hud={hud != null}.");
            return;
        }

        if (launcher.IsBusy)
        {
            Debug.LogWarning($"[LockReticleDebug] Wait for active salvo {launcher.ActiveSalvoId}.");
            return;
        }

        if (lockOn.State == LockOnCombatState.Charging)
        {
            lockOn.HandleGamePaused();
        }

        if (lockOn.State == LockOnCombatState.ReuseWait)
        {
            lockOn.AdvanceReuseWaitForDebug(lockOn.LockReuseWaitDuration + 0.1f);
        }

        if (lockOn.State != LockOnCombatState.Ready)
        {
            Debug.LogWarning($"[LockReticleDebug] Expected Ready, state={lockOn.State}.");
            return;
        }

        context = new VerificationContext
        {
            LockOn = lockOn,
            Launcher = launcher,
            Hud = hud,
            StartedAt = EditorApplication.timeSinceStartup,
        };

        context.Began = lockOn.TryBeginCharging(LockOnInputSource.Debug);
        context.StageMarkerCounts = new int[lockOn.MaxLockStage];
        context.StageImageCounts = new int[lockOn.MaxLockStage];
        for (int i = 0; i < lockOn.MaxLockStage; i++)
        {
            float targetElapsed = lockOn.GetCumulativeChargeTimeForStage(i + 1) + 0.01f;
            lockOn.AdvanceChargeForDebug(Mathf.Max(0f, targetElapsed - lockOn.ChargeElapsed));
            hud.RefreshForDebug();
            context.StageMarkerCounts[i] = hud.VisibleMarkerCount;
            context.StageImageCounts[i] = hud.VisibleTargetingImageCount;
        }

        context.Released = lockOn.TryReleaseCharging(LockOnInputSource.Debug);
        hud.RefreshForDebug();
        context.AtReleaseMarkers = hud.VisibleMarkerCount;
        context.AtReleaseImages = hud.VisibleTargetingImageCount;
        context.ReleaseActiveAtRelease = hud.ReleaseMarkersActive;
        context.Phase = VerificationPhase.WaitingForSalvoEnd;
        EditorApplication.update += TickVerification;

        Debug.Log(
            $"[LockReticleDebug] Started. began={context.Began}, released={context.Released}, " +
            $"stageMarkers=[{string.Join(",", context.StageMarkerCounts)}], " +
            $"stageImages=[{string.Join(",", context.StageImageCounts)}], " +
            $"atRelease={context.AtReleaseMarkers}/{context.AtReleaseImages}.");
    }

    private static void TickVerification()
    {
        if (context == null)
        {
            return;
        }

        if (EditorApplication.timeSinceStartup - context.StartedAt > TimeoutSeconds)
        {
            Debug.LogError("[LockReticleDebug] Verification timed out.");
            AbortPending();
            return;
        }

        context.Hud.RefreshForDebug();
        double now = EditorApplication.timeSinceStartup;
        switch (context.Phase)
        {
            case VerificationPhase.WaitingForSalvoEnd:
                if (context.Launcher.IsBusy)
                {
                    context.MinimumMarkersDuringLaunch = context.MinimumMarkersDuringLaunch < 0
                        ? context.Hud.VisibleMarkerCount
                        : Mathf.Min(
                            context.MinimumMarkersDuringLaunch,
                            context.Hud.VisibleMarkerCount);
                    return;
                }

                context.MarkersAtSalvoEnd = context.Hud.VisibleMarkerCount;
                context.ImagesAtSalvoEnd = context.Hud.VisibleTargetingImageCount;
                context.ReleaseActiveAtSalvoEnd = context.Hud.ReleaseMarkersActive;
                context.SalvoEndedAt = now;
                context.Phase = VerificationPhase.WaitingForHalfSecond;
                return;

            case VerificationPhase.WaitingForHalfSecond:
                if (now - context.SalvoEndedAt < 0.50)
                {
                    return;
                }

                context.MarkersAtHalfSecond = context.Hud.VisibleMarkerCount;
                context.ImagesAtHalfSecond = context.Hud.VisibleTargetingImageCount;
                context.ReleaseActiveAtHalfSecond = context.Hud.ReleaseMarkersActive;
                context.Phase = VerificationPhase.WaitingForRemoval;
                return;

            case VerificationPhase.WaitingForRemoval:
                if (now - context.SalvoEndedAt < 1.12)
                {
                    return;
                }

                CompleteVerification();
                return;
        }
    }

    private static void CompleteVerification()
    {
        int markersAfterHold = context.Hud.VisibleMarkerCount;
        int imagesAfterHold = context.Hud.VisibleTargetingImageCount;
        bool releaseActiveAfterHold = context.Hud.ReleaseMarkersActive;
        bool stageCountsMatch = CountsAreOneToFive(context.StageMarkerCounts) &&
                                CountsAreOneToFive(context.StageImageCounts);
        bool verified = context.Began && context.Released && stageCountsMatch &&
                        context.AtReleaseMarkers == 5 && context.AtReleaseImages == 5 &&
                        context.ReleaseActiveAtRelease &&
                        context.MinimumMarkersDuringLaunch == 5 &&
                        context.MarkersAtSalvoEnd == 5 && context.ImagesAtSalvoEnd == 5 &&
                        context.ReleaseActiveAtSalvoEnd &&
                        context.MarkersAtHalfSecond == 5 && context.ImagesAtHalfSecond == 5 &&
                        context.ReleaseActiveAtHalfSecond &&
                        markersAfterHold == 0 && imagesAfterHold == 0 &&
                        !releaseActiveAfterHold;

        Debug.Log(
            $"[LockReticleDebug] verified={verified}, " +
            $"stageMarkers=[{string.Join(",", context.StageMarkerCounts)}], " +
            $"stageImages=[{string.Join(",", context.StageImageCounts)}], " +
            $"release={context.AtReleaseMarkers}/{context.AtReleaseImages}, " +
            $"duringLaunchMin={context.MinimumMarkersDuringLaunch}, " +
            $"salvoEnd={context.MarkersAtSalvoEnd}/{context.ImagesAtSalvoEnd}, " +
            $"halfSecond={context.MarkersAtHalfSecond}/{context.ImagesAtHalfSecond}, " +
            $"afterHold={markersAfterHold}/{imagesAfterHold}, " +
            $"holdDuration={context.Hud.ReleaseMarkerHoldDuration:0.00}s.");
        AbortPending();
    }

    private static bool CountsAreOneToFive(int[] counts)
    {
        if (counts == null || counts.Length != 5)
        {
            return false;
        }

        for (int i = 0; i < counts.Length; i++)
        {
            if (counts[i] != i + 1)
            {
                return false;
            }
        }

        return true;
    }

    private static void AbortPending()
    {
        EditorApplication.update -= TickVerification;
        context = null;
    }

    private enum VerificationPhase
    {
        WaitingForSalvoEnd,
        WaitingForHalfSecond,
        WaitingForRemoval,
    }

    private sealed class VerificationContext
    {
        public PlayerLockOnController LockOn;
        public PlayerMissileSalvoLauncher Launcher;
        public LockOnHudPresenter Hud;
        public int[] StageMarkerCounts;
        public int[] StageImageCounts;
        public int MinimumMarkersDuringLaunch = -1;
        public int AtReleaseMarkers;
        public int AtReleaseImages;
        public int MarkersAtSalvoEnd;
        public int ImagesAtSalvoEnd;
        public int MarkersAtHalfSecond;
        public int ImagesAtHalfSecond;
        public bool Began;
        public bool Released;
        public bool ReleaseActiveAtRelease;
        public bool ReleaseActiveAtSalvoEnd;
        public bool ReleaseActiveAtHalfSecond;
        public double StartedAt;
        public double SalvoEndedAt;
        public VerificationPhase Phase;
    }
}
