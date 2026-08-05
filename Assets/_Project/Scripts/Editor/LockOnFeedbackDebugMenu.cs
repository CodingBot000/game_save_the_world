using UnityEditor;
using UnityEngine;

public static class LockOnFeedbackDebugMenu
{
    private const string MenuRoot = "TitanDestroyer/Debug/Lock-On Feedback/";
    private const double IgnitionVerificationDelay = 0.72;
    private const double FrontPoseHoldVerificationDelay = 2.05;
    private const double ReturnCompletionVerificationDelay = 2.85;
    private const float MinimumVisibleWorldShakePixels = 25f;
    private static EditorApplication.CallbackFunction pendingVerification;
    private static VerificationContext context;

    [MenuItem(MenuRoot + "Run Stage 5 Feedback Verification", priority = 330)]
    private static void RunStageFiveFeedbackVerification()
    {
        RunFullSalvoVerification();
    }

    private static void RunFullSalvoVerification()
    {
        const int requestedActualLockCount = 5;
        AbortPending();
        if (!Application.isPlaying)
        {
            Debug.LogWarning("[LockFeedbackDebug] Enter Play Mode before running feedback verification.");
            return;
        }

        PlayerLockOnController lockOn = Object.FindAnyObjectByType<PlayerLockOnController>();
        PlayerMissileSalvoLauncher launcher = Object.FindAnyObjectByType<PlayerMissileSalvoLauncher>();
        PlayerOrbitController orbit = Object.FindAnyObjectByType<PlayerOrbitController>();
        LockOnHudPresenter hud = Object.FindAnyObjectByType<LockOnHudPresenter>();
        LockOnCombatFeedback feedback = lockOn != null ? lockOn.CombatFeedback : null;
        MountedSidewinderCosmeticController mountedSidewinders =
            lockOn != null ? lockOn.MountedSidewinderCosmeticController : null;
        PlayerCombatController combat = Object.FindAnyObjectByType<PlayerCombatController>();
        PlayerVisualOverlayRenderer overlay = orbit != null
            ? orbit.OriginalVisualOverlayRenderer
            : null;
        Camera camera = Camera.main;
        if (lockOn == null || launcher == null || orbit == null || feedback == null ||
            mountedSidewinders == null || combat == null || hud == null || overlay == null ||
            camera == null)
        {
            Debug.LogError(
                $"[LockFeedbackDebug] Dependencies missing. lockOn={lockOn != null}, " +
                $"launcher={launcher != null}, orbit={orbit != null}, feedback={feedback != null}, " +
                $"mountedSidewinders={mountedSidewinders != null}, " +
                $"combat={combat != null}, hud={hud != null}, overlay={overlay != null}, " +
                $"camera={camera != null}.");
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
            RequestedActualLockCount = requestedActualLockCount,
            LockOn = lockOn,
            Launcher = launcher,
            Orbit = orbit,
            Feedback = feedback,
            MountedSidewinders = mountedSidewinders,
            Combat = combat,
            Hud = hud,
            Overlay = overlay,
            Camera = camera,
            BaseProjection = camera.projectionMatrix,
            PreSalvoDisplayRotation = orbit.DebugCurrentVisualDisplayRotation,
            WorldProbePosition = Vector3.zero,
            StageSfxBefore = feedback.StageSfxPlayCount,
            ReleaseSfxBefore = feedback.ReleaseSfxPlayCount,
            BoostSfxBefore = feedback.BoostSfxPlayCount,
            FullSalvoBefore = feedback.FullSalvoFeedbackCount,
        };
        Vector3 worldProbeViewport = camera.WorldToViewportPoint(context.WorldProbePosition);
        context.HasWorldProbeViewport = worldProbeViewport.z > 0f;
        context.WorldProbeViewportBeforeShake = worldProbeViewport;

        context.InputSource = LockOnInputSource.MouseRight;
        context.Began = lockOn.TryBeginCharging(context.InputSource);
        context.LocksImmediatelyAfterBegin = lockOn.SuccessfulLockCount;
        if (context.Began)
        {
            lockOn.AdvanceChargeForDebug(
                lockOn.GetCumulativeChargeTimeForStage(requestedActualLockCount) + 0.01f);
        }

        hud.RefreshForDebug();
        context.ActualLockCountBeforeRelease = lockOn.SuccessfulLockCount;
        context.MarkersBeforeRelease = hud.VisibleMarkerCount;
        context.PulsesBeforeRelease = hud.ActiveMarkerPulseCount;
        context.Released = lockOn.TryReleaseCharging(context.InputSource);
        context.IntentSuccessfulLockCount = lockOn.LastReleaseIntent?.SuccessfulLockCount ?? -1;
        context.IntentSalvoProfileLockCount =
            lockOn.LastReleaseIntent?.SalvoProfileLockCount ?? -1;
        context.VisualTurnStarted = orbit.IsFullSalvoVisualTurning;
        context.VisualTurnProgressImmediately = orbit.FullSalvoVisualTurnProgress;
        context.VisualTurnDuration = orbit.DebugFullSalvoVisualTurnDuration;
        context.VisualReturnDuration = orbit.DebugFullSalvoVisualReturnDuration;
        context.AudioPlayingImmediately = feedback.IsFeedbackAudioPlaying;
        context.ShakeActiveImmediately = feedback.IsProjectionShakeActive;
        context.MountedIgnitingImmediately = mountedSidewinders.IsIgniting;
        context.MountedWaitingForTurnImmediately = mountedSidewinders.IsWaitingForVisualTurn;
        context.ActiveExhaustsImmediately = mountedSidewinders.ActiveExhaustCount;
        context.DetachedSidewindersImmediately =
            mountedSidewinders.LastDetachedSidewinderCount;
        context.InvincibleImmediately = combat.IsSalvoInvincible;
        context.HelicopterProjectionStableImmediately =
            feedback.KeepsHelicopterProjectionStable;
        context.StartedAt = EditorApplication.timeSinceStartup;
        context.CheckAt = context.StartedAt + IgnitionVerificationDelay;
        pendingVerification = TickVerification;
        EditorApplication.update += pendingVerification;
        Debug.Log(
            $"[LockFeedbackDebug] Started. requestedActualLocks={requestedActualLockCount}, " +
            $"input={context.InputSource}, immediateLocks={context.LocksImmediatelyAfterBegin}, " +
            $"began={context.Began}, released={context.Released}, " +
            $"actualLocks={context.ActualLockCountBeforeRelease}, " +
            $"intentLocks={context.IntentSuccessfulLockCount}, " +
            $"profileLocks={context.IntentSalvoProfileLockCount}, " +
            $"markers={context.MarkersBeforeRelease}, pulses={context.PulsesBeforeRelease}, " +
            $"turnStarted={context.VisualTurnStarted}, " +
            $"turnProgress={context.VisualTurnProgressImmediately:0.000}, " +
            $"turnDuration={context.VisualTurnDuration:0.000}, " +
            $"returnDuration={context.VisualReturnDuration:0.000}, " +
            $"waitingForTurn={context.MountedWaitingForTurnImmediately}, " +
            $"mountedIgniting={context.MountedIgnitingImmediately}, " +
            $"activeExhausts={context.ActiveExhaustsImmediately}, " +
            $"detached={context.DetachedSidewindersImmediately}, " +
            $"audioNow={context.AudioPlayingImmediately}, shakeNow={context.ShakeActiveImmediately}, " +
            $"invincibleNow={context.InvincibleImmediately}, " +
            $"helicopterStable={context.HelicopterProjectionStableImmediately}.");
    }

    private static void TickVerification()
    {
        if (context == null)
        {
            return;
        }

        if (context.Feedback != null && context.Feedback.IsProjectionShakeActive)
        {
            SampleWorldScreenShake();
        }

        if (context.IgnitionCheckpointCaptured &&
            (context.Feedback != null && context.Feedback.IsProjectionShakeActive ||
             context.Combat != null && context.Combat.IsSalvoInvincible))
        {
            SampleHelicopterStability();
        }

        if (context.Orbit != null && context.Orbit.IsFullSalvoVisualReturning)
        {
            context.VisualReturnObserved = true;
            float returnProgress = context.Orbit.FullSalvoVisualReturnProgress;
            if (returnProgress > 0.01f && returnProgress < 0.99f)
            {
                context.VisualReturnIntermediateFrameObserved = true;
            }
        }

        if (EditorApplication.timeSinceStartup < context.CheckAt)
        {
            return;
        }

        if (!context.IgnitionCheckpointCaptured)
        {
            CaptureIgnitionCheckpoint();
            context.IgnitionCheckpointCaptured = true;
            context.CheckAt = context.StartedAt + FrontPoseHoldVerificationDelay;
            return;
        }

        if (!context.FrontPoseHoldCheckpointCaptured)
        {
            CaptureFrontPoseHoldCheckpoint();
            context.FrontPoseHoldCheckpointCaptured = true;
            context.CheckAt = context.StartedAt + ReturnCompletionVerificationDelay;
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
        float returnAngleError = context.Orbit != null
            ? Quaternion.Angle(
                context.PreSalvoDisplayRotation,
                context.Orbit.DebugCurrentVisualDisplayRotation)
            : float.PositiveInfinity;
        bool visualReturnCompleted = context.Orbit != null &&
                                     !context.Orbit.IsFullSalvoVisualReturning &&
                                     Mathf.Approximately(
                                         context.Orbit.FullSalvoVisualReturnProgress,
                                         1f) &&
                                     !context.Orbit.IsFullSalvoFrontViewActive &&
                                     context.Orbit.FullSalvoVisualSalvoId == 0 &&
                                     returnAngleError <= 0.5f;
        bool mountedSequenceCompleted = context.MountedSidewinders != null &&
                                        context.MountedSidewinders.LastDetachedSidewinderCount == 2 &&
                                        context.MountedSidewinders.LastIgnitionStartedAfterVisualTurn &&
                                        context.MountedSidewinders.LastIgnitionVisualTurnProgress >= 0.999f;
        int expectedMarkerCount = context.RequestedActualLockCount < 5
            ? context.RequestedActualLockCount + 1
            : context.RequestedActualLockCount;
        int expectedPulseCount = context.RequestedActualLockCount < 5 ? 1 : 0;
        bool helicopterActuallyStable = context.HasHelicopterViewportSample &&
                                        context.PeakHelicopterViewportDrift <= 0.0005f &&
                                        context.PeakAlignmentWorldOffset <= 0.0005f &&
                                        context.PeakVisualRootLocalPositionDrift <= 0.0005f &&
                                        context.PeakPlayerWorldPositionDrift <= 0.0005f;
        bool verified = context.Began && context.Released &&
                        !context.LockOn.ForceFullChargeOnMouseRightForTesting &&
                        !context.LockOn.PromoteThreeOrMoreLocksToFullSalvoForTesting &&
                        context.LocksImmediatelyAfterBegin == 0 &&
                        context.ActualLockCountBeforeRelease == context.RequestedActualLockCount &&
                        context.IntentSuccessfulLockCount == context.RequestedActualLockCount &&
                        context.IntentSalvoProfileLockCount == 5 &&
                        context.MarkersBeforeRelease == expectedMarkerCount &&
                        context.PulsesBeforeRelease == expectedPulseCount &&
                        stageSfxDelta == context.RequestedActualLockCount &&
                        releaseSfxDelta == 1 && boostSfxDelta == 1 &&
                        fullSalvoDelta == 1 && feedback != null &&
                        feedback.HasGeneratedFeedbackAudio && feedback.LastFeedbackStage == 5 &&
                        context.AudioPlayingImmediately && context.ShakeActiveImmediately &&
                        context.InvincibleImmediately &&
                        context.HelicopterProjectionStableImmediately &&
                        context.MountedWaitingForTurnImmediately &&
                        !context.MountedIgnitingImmediately &&
                        context.ActiveExhaustsImmediately == 0 &&
                        context.DetachedSidewindersImmediately == 0 &&
                        context.IgnitionCheckpointVerified &&
                        helicopterActuallyStable &&
                        feedback.LastShakeAmplitude >= 0.0599f &&
                        feedback.LastShakeAmplitude <= 0.0601f &&
                        feedback.PeakShakeProjectionOffset > 0f &&
                        context.HasWorldProbeViewport &&
                        context.PeakWorldScreenShakePixels >= MinimumVisibleWorldShakePixels &&
                        !feedback.IsProjectionShakeActive &&
                        feedback.LastShakeStartedWhileSalvoInvincible &&
                        feedback.LastShakeStoppedAtSalvoInvincibilityEnd &&
                        feedback.ProjectionRestoredAfterShake && projectionRestored &&
                        context.VisualTurnStarted &&
                        context.VisualTurnProgressImmediately >= 0f &&
                        context.VisualTurnProgressImmediately < 1f &&
                        Mathf.Approximately(context.VisualTurnDuration, 0.3f) &&
                        Mathf.Approximately(context.VisualReturnDuration, 0.3f) &&
                        context.FrontPoseHoldCheckpointVerified &&
                        context.VisualReturnObserved &&
                        context.VisualReturnIntermediateFrameObserved &&
                        visualReturnCompleted && mountedSequenceCompleted &&
                        context.Combat != null && !context.Combat.IsSalvoInvincible &&
                        context.MountedSidewinders.MinimumObservedFlightSpeed <=
                            context.MountedSidewinders.LastInitialFlightSpeed + 0.001f &&
                        context.MountedSidewinders.PeakObservedFlightSpeed >
                            context.MountedSidewinders.LastInitialFlightSpeed &&
                        context.LockOn != null &&
                        context.LockOn.LastRequestedMissileCount == 30 &&
                        context.LockOn.LastFiredMissileCount == 30 &&
                        context.Launcher != null && !context.Launcher.IsBusy;

        Debug.Log(
            $"[LockFeedbackDebug] verified={verified}, " +
            $"requestedActualLocks={context.RequestedActualLockCount}, " +
            $"immediateLocks={context.LocksImmediatelyAfterBegin}, " +
            $"intentLocks={context.IntentSuccessfulLockCount}, " +
            $"profileLocks={context.IntentSalvoProfileLockCount}, " +
            $"stageSfx={stageSfxDelta}, " +
            $"releaseSfx={releaseSfxDelta}, boostSfx={boostSfxDelta}, fullSalvo={fullSalvoDelta}, " +
            $"audioGenerated={feedback != null && feedback.HasGeneratedFeedbackAudio}, " +
            $"lastStage={(feedback != null ? feedback.LastFeedbackStage : -1)}, " +
            $"shakeAmplitude={(feedback != null ? feedback.LastShakeAmplitude : 0f):0.0000}, " +
            $"shakePeak={(feedback != null ? feedback.PeakShakeProjectionOffset : 0f):0.000000}, " +
            $"worldScreenShake={context.PeakWorldScreenShakePixels:0.00}px, " +
            $"shakeEnded={feedback != null && !feedback.IsProjectionShakeActive}, " +
            $"shakeTrackedInvincibility={feedback != null && feedback.LastShakeStoppedAtSalvoInvincibilityEnd}, " +
            $"turnStarted={context.VisualTurnStarted}, " +
            $"turnProgressNow={(context.Orbit != null ? context.Orbit.FullSalvoVisualTurnProgress : -1f):0.000}, " +
            $"frontPoseHold={context.FrontPoseHoldCheckpointVerified}, " +
            $"returnObserved={context.VisualReturnObserved}, " +
            $"returnIntermediate={context.VisualReturnIntermediateFrameObserved}, " +
            $"returnProgress={(context.Orbit != null ? context.Orbit.FullSalvoVisualReturnProgress : -1f):0.000}, " +
            $"returnCompleted={visualReturnCompleted}, returnAngleError={returnAngleError:0.000}, " +
            $"ignitionCheckpoint={context.IgnitionCheckpointVerified}, " +
            $"ignitionAfterTurn={context.MountedSidewinders != null && context.MountedSidewinders.LastIgnitionStartedAfterVisualTurn}, " +
            $"activeExhausts={(context.MountedSidewinders != null ? context.MountedSidewinders.ActiveExhaustCount : -1)}, " +
            $"detached={(context.MountedSidewinders != null ? context.MountedSidewinders.LastDetachedSidewinderCount : -1)}, " +
            $"initialSpeed={(context.MountedSidewinders != null ? context.MountedSidewinders.LastInitialFlightSpeed : 0f):0.00}, " +
            $"peakSpeed={(context.MountedSidewinders != null ? context.MountedSidewinders.PeakObservedFlightSpeed : 0f):0.00}, " +
            $"helicopterActuallyStable={helicopterActuallyStable}, " +
            $"helicopterViewportDrift={context.PeakHelicopterViewportDrift:0.000000}, " +
            $"visualRootCorrection={context.PeakAlignmentWorldOffset:0.000000}, " +
            $"visualRootLocalDrift={context.PeakVisualRootLocalPositionDrift:0.000000}, " +
            $"playerWorldDrift={context.PeakPlayerWorldPositionDrift:0.000000}, " +
            $"overlayCameraPositionDrift={context.PeakOverlayCameraPositionDrift:0.000000}, " +
            $"overlayCameraRotationDrift={context.PeakOverlayCameraRotationDrift:0.000000}, " +
            $"overlayProjectionDrift={context.PeakOverlayProjectionDrift:0.000000}, " +
            $"stableMovementProjection={context.UsesStableMovementProjectionAtIgnition}, " +
            $"centeringRenderers={context.CenteringRenderersAtIgnition}, " +
            $"ignoredDynamicVfx={context.IgnoredDynamicCenteringRenderersAtIgnition}, " +
            $"ignoredAttachments={context.IgnoredAttachmentCenteringRenderersAtIgnition}, " +
            $"projectionRestored={projectionRestored}, " +
            $"requested={context.LockOn?.LastRequestedMissileCount ?? 0}, " +
            $"fired={context.LockOn?.LastFiredMissileCount ?? 0}.");
        AbortPending();
    }

    private static void CaptureFrontPoseHoldCheckpoint()
    {
        if (context == null)
        {
            return;
        }

        context.FrontPoseHoldCheckpointVerified = context.Orbit != null &&
                                                  !context.Orbit.IsFullSalvoVisualTurning &&
                                                  Mathf.Approximately(
                                                      context.Orbit.FullSalvoVisualTurnProgress,
                                                      1f) &&
                                                  context.Orbit.IsFullSalvoFrontViewActive &&
                                                  !context.Orbit.IsFullSalvoVisualReturning;

        Debug.Log(
            $"[LockFeedbackDebug] Front-pose hold checkpoint " +
            $"verified={context.FrontPoseHoldCheckpointVerified}, " +
            $"turnProgress={(context.Orbit != null ? context.Orbit.FullSalvoVisualTurnProgress : -1f):0.000}, " +
            $"frontPose={context.Orbit != null && context.Orbit.IsFullSalvoFrontViewActive}, " +
            $"returning={context.Orbit != null && context.Orbit.IsFullSalvoVisualReturning}.");
    }

    private static void CaptureIgnitionCheckpoint()
    {
        if (context == null)
        {
            return;
        }

        MountedSidewinderCosmeticController mounted = context.MountedSidewinders;
        LockOnCombatFeedback feedback = context.Feedback;
        Rect renderedVisualRect = default;
        bool hasRenderedVisualRect = context.Overlay != null &&
                                     context.Overlay.TryGetRenderedVisualViewportRect(
                                         out renderedVisualRect);
        context.HasHelicopterViewportSample =
            TryGetHelicopterViewportPivot(out Vector2 helicopterViewportPivot);
        if (context.HasHelicopterViewportSample)
        {
            context.HelicopterViewportCenterAtIgnition = helicopterViewportPivot;
        }

        if (context.Overlay?.VisualRoot != null)
        {
            context.VisualRootLocalPositionAtIgnition =
                context.Overlay.VisualRoot.localPosition;
        }

        context.PlayerWorldPositionAtIgnition = context.Orbit != null
            ? context.Orbit.transform.position
            : Vector3.zero;
        if (context.Overlay?.OverlayCamera != null)
        {
            context.OverlayCameraPositionAtIgnition =
                context.Overlay.OverlayCamera.transform.position;
            context.OverlayCameraRotationAtIgnition =
                context.Overlay.OverlayCamera.transform.rotation;
            context.OverlayProjectionAtIgnition =
                context.Overlay.OverlayCamera.projectionMatrix;
        }

        context.CenteringRenderersAtIgnition =
            context.Overlay != null ? context.Overlay.CenteringRendererCount : 0;
        context.IgnoredDynamicCenteringRenderersAtIgnition =
            context.Overlay != null ? context.Overlay.IgnoredDynamicCenteringRendererCount : 0;
        context.IgnoredAttachmentCenteringRenderersAtIgnition =
            context.Overlay != null ? context.Overlay.IgnoredAttachmentCenteringRendererCount : 0;
        context.PeakAlignmentWorldOffset = context.Overlay != null
            ? context.Overlay.LastAlignmentWorldOffset.magnitude
            : float.PositiveInfinity;
        context.UsesStableMovementProjectionAtIgnition =
            context.Orbit != null && context.Overlay != null &&
            context.Orbit.DebugMovementProjectionCamera == context.Overlay.OverlayCamera;
        context.IgnitionCheckpointVerified = mounted != null &&
                                                context.Orbit != null &&
                                                !context.Orbit.IsFullSalvoVisualTurning &&
                                                Mathf.Approximately(
                                                    context.Orbit.FullSalvoVisualTurnProgress,
                                                    1f) &&
                                                mounted.IsIgniting &&
                                                !mounted.IsWaitingForVisualTurn &&
                                                mounted.ActiveExhaustCount == 2 &&
                                                mounted.LastIgnitionExhaustCount == 2 &&
                                                mounted.LastDetachedSidewinderCount == 0 &&
                                                mounted.LastIgnitionStartedAfterVisualTurn &&
                                                mounted.LastIgnitionVisualTurnProgress >= 0.999f &&
                                                context.Combat != null &&
                                                context.Combat.IsSalvoInvincible &&
                                                feedback != null &&
                                                feedback.IsProjectionShakeActive &&
                                                feedback.KeepsHelicopterProjectionStable &&
                                                hasRenderedVisualRect &&
                                                context.HasHelicopterViewportSample &&
                                                context.UsesStableMovementProjectionAtIgnition &&
                                                context.CenteringRenderersAtIgnition > 0 &&
                                                context.IgnoredDynamicCenteringRenderersAtIgnition >= 4 &&
                                                context.IgnoredAttachmentCenteringRenderersAtIgnition >= 2;

        Debug.Log(
            $"[LockFeedbackDebug] Ignition checkpoint verified={context.IgnitionCheckpointVerified}, " +
            $"turnProgress={(context.Orbit != null ? context.Orbit.FullSalvoVisualTurnProgress : -1f):0.000}, " +
            $"igniting={mounted != null && mounted.IsIgniting}, " +
            $"exhausts={(mounted != null ? mounted.ActiveExhaustCount : -1)}, " +
            $"detached={(mounted != null ? mounted.LastDetachedSidewinderCount : -1)}, " +
            $"invincible={context.Combat != null && context.Combat.IsSalvoInvincible}, " +
            $"cameraShake={feedback != null && feedback.IsProjectionShakeActive}, " +
            $"helicopterStable={feedback != null && feedback.KeepsHelicopterProjectionStable}, " +
            $"centeringRenderers={context.CenteringRenderersAtIgnition}, " +
            $"ignoredDynamicVfx={context.IgnoredDynamicCenteringRenderersAtIgnition}, " +
            $"ignoredAttachments={context.IgnoredAttachmentCenteringRenderersAtIgnition}, " +
            $"visualRootCorrection={context.PeakAlignmentWorldOffset:0.000000}, " +
            $"visualRootLocalDrift={context.PeakVisualRootLocalPositionDrift:0.000000}, " +
            $"stableMovementProjection={context.UsesStableMovementProjectionAtIgnition}.");
    }

    private static void SampleHelicopterStability()
    {
        if (context?.Overlay == null)
        {
            return;
        }

        context.PeakAlignmentWorldOffset = Mathf.Max(
            context.PeakAlignmentWorldOffset,
            context.Overlay.LastAlignmentWorldOffset.magnitude);
        if (context.Overlay.VisualRoot != null)
        {
            context.PeakVisualRootLocalPositionDrift = Mathf.Max(
                context.PeakVisualRootLocalPositionDrift,
                Vector3.Distance(
                    context.VisualRootLocalPositionAtIgnition,
                    context.Overlay.VisualRoot.localPosition));
        }


        if (context.Orbit != null)
        {
            context.PeakPlayerWorldPositionDrift = Mathf.Max(
                context.PeakPlayerWorldPositionDrift,
                Vector3.Distance(
                    context.PlayerWorldPositionAtIgnition,
                    context.Orbit.transform.position));
        }

        if (context.Overlay.OverlayCamera != null)
        {
            context.PeakOverlayCameraPositionDrift = Mathf.Max(
                context.PeakOverlayCameraPositionDrift,
                Vector3.Distance(
                    context.OverlayCameraPositionAtIgnition,
                    context.Overlay.OverlayCamera.transform.position));
            context.PeakOverlayCameraRotationDrift = Mathf.Max(
                context.PeakOverlayCameraRotationDrift,
                Quaternion.Angle(
                    context.OverlayCameraRotationAtIgnition,
                    context.Overlay.OverlayCamera.transform.rotation));
            context.PeakOverlayProjectionDrift = Mathf.Max(
                context.PeakOverlayProjectionDrift,
                MaxMatrixElementDelta(
                    context.OverlayProjectionAtIgnition,
                    context.Overlay.OverlayCamera.projectionMatrix));
        }

        if (!TryGetHelicopterViewportPivot(out Vector2 helicopterViewportPivot))
        {
            return;
        }

        if (!context.HasHelicopterViewportSample)
        {
            context.HasHelicopterViewportSample = true;
            context.HelicopterViewportCenterAtIgnition = helicopterViewportPivot;
            return;
        }

        context.PeakHelicopterViewportDrift = Mathf.Max(
            context.PeakHelicopterViewportDrift,
            Vector2.Distance(
                context.HelicopterViewportCenterAtIgnition,
                helicopterViewportPivot));
    }

    private static void SampleWorldScreenShake()
    {
        if (context?.Camera == null || !context.HasWorldProbeViewport)
        {
            return;
        }

        Vector3 viewport = context.Camera.WorldToViewportPoint(context.WorldProbePosition);
        if (viewport.z <= 0f)
        {
            return;
        }

        float pixelDeltaX =
            (viewport.x - context.WorldProbeViewportBeforeShake.x) * context.Camera.pixelWidth;
        float pixelDeltaY =
            (viewport.y - context.WorldProbeViewportBeforeShake.y) * context.Camera.pixelHeight;
        context.PeakWorldScreenShakePixels = Mathf.Max(
            context.PeakWorldScreenShakePixels,
            new Vector2(pixelDeltaX, pixelDeltaY).magnitude);
    }

    private static bool TryGetHelicopterViewportPivot(out Vector2 viewportPivot)
    {
        viewportPivot = default;
        PlayerVisualOverlayRenderer overlay = context?.Overlay;
        if (overlay?.OverlayCamera == null || overlay.VisualRoot == null)
        {
            return false;
        }

        Vector3 viewport = overlay.OverlayCamera.WorldToViewportPoint(
            overlay.VisualRoot.position);
        if (viewport.z <= 0f)
        {
            return false;
        }

        viewportPivot = new Vector2(viewport.x, viewport.y);
        return true;
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

    private static float MaxMatrixElementDelta(Matrix4x4 left, Matrix4x4 right)
    {
        float maximum = 0f;
        for (int i = 0; i < 16; i++)
        {
            maximum = Mathf.Max(maximum, Mathf.Abs(left[i] - right[i]));
        }

        return maximum;
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
        public PlayerMissileSalvoLauncher Launcher;
        public PlayerOrbitController Orbit;
        public LockOnCombatFeedback Feedback;
        public MountedSidewinderCosmeticController MountedSidewinders;
        public PlayerCombatController Combat;
        public LockOnHudPresenter Hud;
        public PlayerVisualOverlayRenderer Overlay;
        public Camera Camera;
        public Matrix4x4 BaseProjection;
        public Quaternion PreSalvoDisplayRotation;
        public Vector3 WorldProbePosition;
        public Vector3 WorldProbeViewportBeforeShake;
        public bool HasWorldProbeViewport;
        public float PeakWorldScreenShakePixels;
        public int StageSfxBefore;
        public int ReleaseSfxBefore;
        public int BoostSfxBefore;
        public int FullSalvoBefore;
        public int RequestedActualLockCount;
        public int LocksImmediatelyAfterBegin;
        public int ActualLockCountBeforeRelease;
        public int IntentSuccessfulLockCount;
        public int IntentSalvoProfileLockCount;
        public int MarkersBeforeRelease;
        public int PulsesBeforeRelease;
        public bool Began;
        public bool Released;
        public bool VisualTurnStarted;
        public float VisualTurnProgressImmediately;
        public float VisualTurnDuration;
        public float VisualReturnDuration;
        public bool AudioPlayingImmediately;
        public bool ShakeActiveImmediately;
        public bool MountedIgnitingImmediately;
        public bool MountedWaitingForTurnImmediately;
        public int ActiveExhaustsImmediately;
        public int DetachedSidewindersImmediately;
        public bool InvincibleImmediately;
        public bool HelicopterProjectionStableImmediately;
        public bool IgnitionCheckpointCaptured;
        public bool IgnitionCheckpointVerified;
        public bool FrontPoseHoldCheckpointCaptured;
        public bool FrontPoseHoldCheckpointVerified;
        public bool VisualReturnObserved;
        public bool VisualReturnIntermediateFrameObserved;
        public bool HasHelicopterViewportSample;
        public Vector2 HelicopterViewportCenterAtIgnition;
        public float PeakHelicopterViewportDrift;
        public float PeakAlignmentWorldOffset;
        public Vector3 VisualRootLocalPositionAtIgnition;
        public float PeakVisualRootLocalPositionDrift;
        public Vector3 PlayerWorldPositionAtIgnition;
        public float PeakPlayerWorldPositionDrift;
        public Vector3 OverlayCameraPositionAtIgnition;
        public Quaternion OverlayCameraRotationAtIgnition;
        public Matrix4x4 OverlayProjectionAtIgnition;
        public float PeakOverlayCameraPositionDrift;
        public float PeakOverlayCameraRotationDrift;
        public float PeakOverlayProjectionDrift;
        public int CenteringRenderersAtIgnition;
        public int IgnoredDynamicCenteringRenderersAtIgnition;
        public int IgnoredAttachmentCenteringRenderersAtIgnition;
        public bool UsesStableMovementProjectionAtIgnition;
        public LockOnInputSource InputSource;
        public double StartedAt;
        public double CheckAt;
    }
}
