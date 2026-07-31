using UnityEditor;
using UnityEngine;

public static class LockOnFeedbackDebugMenu
{
    private const string MenuRoot = "TitanDestroyer/Debug/Lock-On Feedback/";
    private const double VerificationDelay = 0.42;
    private static EditorApplication.CallbackFunction pendingVerification;
    private static VerificationContext context;

    [MenuItem(MenuRoot + "Run Stage 5 Feedback Verification", priority = 330)]
    private static void RunStageFiveFeedbackVerification()
    {
        AbortPending();
        if (!Application.isPlaying)
        {
            Debug.LogWarning("[LockFeedbackDebug] Enter Play Mode before running feedback verification.");
            return;
        }

        PlayerLockOnController lockOn = Object.FindAnyObjectByType<PlayerLockOnController>();
        PlayerMissileSalvoLauncher launcher = Object.FindAnyObjectByType<PlayerMissileSalvoLauncher>();
        LockOnHudPresenter hud = Object.FindAnyObjectByType<LockOnHudPresenter>();
        LockOnCombatFeedback feedback = lockOn != null ? lockOn.CombatFeedback : null;
        Camera camera = Camera.main;
        if (lockOn == null || launcher == null || feedback == null || hud == null || camera == null)
        {
            Debug.LogError(
                $"[LockFeedbackDebug] Dependencies missing. lockOn={lockOn != null}, " +
                $"launcher={launcher != null}, feedback={feedback != null}, " +
                $"hud={hud != null}, camera={camera != null}.");
            return;
        }

        if (launcher.IsBusy)
        {
            Debug.LogWarning($"[LockFeedbackDebug] Wait for active salvo {launcher.ActiveSalvoId}.");
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
            Debug.LogWarning($"[LockFeedbackDebug] Expected Ready, state={lockOn.State}.");
            return;
        }

        context = new VerificationContext
        {
            LockOn = lockOn,
            Feedback = feedback,
            Hud = hud,
            Camera = camera,
            BaseProjection = camera.projectionMatrix,
            StageSfxBefore = feedback.StageSfxPlayCount,
            ReleaseSfxBefore = feedback.ReleaseSfxPlayCount,
            BoostSfxBefore = feedback.BoostSfxPlayCount,
            FullSalvoBefore = feedback.FullSalvoFeedbackCount,
        };

        context.Began = lockOn.TryBeginCharging(LockOnInputSource.Debug);
        lockOn.AdvanceChargeForDebug(2.51f);
        hud.RefreshForDebug();
        context.MarkersAtFullCharge = hud.VisibleMarkerCount;
        context.PulsesAtFullCharge = hud.ActiveMarkerPulseCount;
        context.Released = lockOn.TryReleaseCharging(LockOnInputSource.Debug);
        context.AudioPlayingImmediately = feedback.IsFeedbackAudioPlaying;
        context.ShakeActiveImmediately = feedback.IsProjectionShakeActive;
        context.CheckAt = EditorApplication.timeSinceStartup + VerificationDelay;
        pendingVerification = TickVerification;
        EditorApplication.update += pendingVerification;
        Debug.Log(
            $"[LockFeedbackDebug] Started. began={context.Began}, released={context.Released}, " +
            $"markers={context.MarkersAtFullCharge}, pulses={context.PulsesAtFullCharge}, " +
            $"audioNow={context.AudioPlayingImmediately}, shakeNow={context.ShakeActiveImmediately}.");
    }

    private static void TickVerification()
    {
        if (context == null || EditorApplication.timeSinceStartup < context.CheckAt)
        {
            return;
        }

        LockOnCombatFeedback feedback = context.Feedback;
        bool projectionRestored = context.Camera != null &&
                                  MatrixApproximately(context.BaseProjection, context.Camera.projectionMatrix);
        int stageSfxDelta = feedback != null
            ? feedback.StageSfxPlayCount - context.StageSfxBefore
            : -1;
        int releaseSfxDelta = feedback != null
            ? feedback.ReleaseSfxPlayCount - context.ReleaseSfxBefore
            : -1;
        int boostSfxDelta = feedback != null
            ? feedback.BoostSfxPlayCount - context.BoostSfxBefore
            : -1;
        int fullSalvoDelta = feedback != null
            ? feedback.FullSalvoFeedbackCount - context.FullSalvoBefore
            : -1;
        bool verified = context.Began && context.Released &&
                        context.MarkersAtFullCharge == 5 && context.PulsesAtFullCharge == 5 &&
                        stageSfxDelta == 5 && releaseSfxDelta == 1 && boostSfxDelta == 1 &&
                        fullSalvoDelta == 1 && feedback != null &&
                        feedback.HasGeneratedFeedbackAudio && feedback.LastFeedbackStage == 5 &&
                        context.AudioPlayingImmediately && context.ShakeActiveImmediately &&
                        feedback.LastShakeAmplitude >= 0.009f &&
                        feedback.PeakShakeProjectionOffset > 0f &&
                        !feedback.IsProjectionShakeActive &&
                        feedback.ProjectionRestoredAfterShake && projectionRestored &&
                        context.LockOn != null && context.LockOn.LastRequestedMissileCount == 30;

        Debug.Log(
            $"[LockFeedbackDebug] verified={verified}, stageSfx={stageSfxDelta}, " +
            $"releaseSfx={releaseSfxDelta}, boostSfx={boostSfxDelta}, fullSalvo={fullSalvoDelta}, " +
            $"audioGenerated={feedback != null && feedback.HasGeneratedFeedbackAudio}, " +
            $"lastStage={(feedback != null ? feedback.LastFeedbackStage : -1)}, " +
            $"shakeAmplitude={(feedback != null ? feedback.LastShakeAmplitude : 0f):0.0000}, " +
            $"shakePeak={(feedback != null ? feedback.PeakShakeProjectionOffset : 0f):0.000000}, " +
            $"shakeEnded={feedback != null && !feedback.IsProjectionShakeActive}, " +
            $"projectionRestored={projectionRestored}, requested={context.LockOn?.LastRequestedMissileCount ?? 0}.");
        AbortPending();
    }

    private static bool MatrixApproximately(Matrix4x4 left, Matrix4x4 right)
    {
        for (int i = 0; i < 16; i++)
        {
            if (Mathf.Abs(left[i] - right[i]) > 0.0001f)
            {
                return false;
            }
        }

        return true;
    }

    private static void AbortPending()
    {
        if (pendingVerification != null)
        {
            EditorApplication.update -= pendingVerification;
            pendingVerification = null;
        }

        context = null;
    }

    private sealed class VerificationContext
    {
        public PlayerLockOnController LockOn;
        public LockOnCombatFeedback Feedback;
        public LockOnHudPresenter Hud;
        public Camera Camera;
        public Matrix4x4 BaseProjection;
        public int StageSfxBefore;
        public int ReleaseSfxBefore;
        public int BoostSfxBefore;
        public int FullSalvoBefore;
        public int MarkersAtFullCharge;
        public int PulsesAtFullCharge;
        public bool Began;
        public bool Released;
        public bool AudioPlayingImmediately;
        public bool ShakeActiveImmediately;
        public double CheckAt;
    }
}
