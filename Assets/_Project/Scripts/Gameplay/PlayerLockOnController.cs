using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

public enum LockOnCombatState
{
    Ready,
    Charging,
    Release,
    ReuseWait,
}

public enum LockOnInputSource
{
    None,
    MouseRight,
    MobileHud,
    Debug,
}

public enum LockOnCancelReason
{
    PointerExit,
    GamePaused,
    NoSuccessfulLocks,
    TargetsUnavailableOnRelease,
    TargetGraceExpired,
    BattleUnavailable,
    PlayerDamaged,
    ReleaseRejected,
    ComponentDisabled,
}

public sealed class LockOnReleaseIntent
{
    internal LockOnReleaseIntent(
        int chargeStage,
        int successfulLockCount,
        SalvoTargetSnapshot[] targetSnapshots,
        LockOnInputSource inputSource,
        int randomSeed)
    {
        ChargeStage = chargeStage;
        SuccessfulLockCount = successfulLockCount;
        TargetSnapshots = targetSnapshots ?? Array.Empty<SalvoTargetSnapshot>();
        InputSource = inputSource;
        RandomSeed = randomSeed;
    }

    public int ChargeStage { get; }
    public int SuccessfulLockCount { get; }
    public IReadOnlyList<SalvoTargetSnapshot> TargetSnapshots { get; }
    public LockOnInputSource InputSource { get; }
    public int RandomSeed { get; }
}

[DisallowMultipleComponent]
public sealed class PlayerLockOnController : MonoBehaviour
{
    private static readonly float[] LegacyDefaultStageChargeTimes =
        { 0.35f, 0.75f, 1.25f, 1.80f, 2.50f };
    private static readonly float[] PreviousDefaultStageChargeTimes =
        { 3.00f, 3.40f, 3.90f, 4.45f, 5.15f };
    private static readonly float[] DefaultStageChargeTimes =
        { 1.00f, 2.50f, 4.50f, 7.00f, 10.00f };
    private static readonly int[] DefaultMissileCountsBySuccessfulLocks =
        { 5, 10, 15, 20, 30 };
    private static readonly float[] DefaultTotalDamagesBySuccessfulLocks =
        { 9f, 20f, 35f, 60f, 100f };

    [SerializeField, Tooltip(
        "Cumulative thresholds derived from per-stage durations 1 / 1.5 / 2 / 2.5 / 3 seconds.")]
    private float[] stageChargeTimes =
        { 1.00f, 2.50f, 4.50f, 7.00f, 10.00f };
    [SerializeField, Range(0.1f, 3f)] private float targetGraceTime = 1.25f;
    [Header("Missile Salvo")]
    [SerializeField] private int[] missileCountsBySuccessfulLocks =
        { 5, 10, 15, 20, 30 };
    [SerializeField, Tooltip("Fixed total base damage for 1 through 5 successful locks.")]
    private float[] totalDamagesBySuccessfulLocks =
        { 9f, 20f, 35f, 60f, 100f };
    [SerializeField, Min(1)] private int missilesPerVolley = 4;
    [SerializeField, Min(0.01f)] private float salvoLaunchDuration = 0.6f;
    [SerializeField, Min(0f)] private float lockReuseWaitDuration = 5f;

    private readonly List<BossLockOnTarget> lockedTargets = new();
    private readonly List<BossLockOnTarget> candidateBuffer = new();
    private readonly List<SalvoTargetSnapshot> snapshotBuffer = new();
    private readonly HashSet<BossLockOnTarget> usedTargets = new();

    private BattleController battleController;
    private PlayerCombatController playerCombatController;
    private BossLockOnTargetProvider targetProvider;
    private PlayerMissileSalvoLauncher salvoLauncher;
    private LockOnCombatFeedback combatFeedback;
    private HUDPresenter hudPresenter;
    private LockOnCombatState state = LockOnCombatState.Ready;
    private LockOnInputSource activeInputSource;
    private float chargeElapsed;
    private int chargeStage;
    private float noTargetSince = -1f;
    private float reuseWaitRemaining;
    private int chargeSessionSeed;
    private bool lastAvailability;
    private bool configured;
    private bool refreshingLockAssignments;
    private int currentLockOnSalvoId;
    private int currentLockOnMissilesFired;
    private bool ownsSalvoInvincibility;
    private BossLockOnTarget currentChargingTarget;

    public LockOnCombatState State => state;
    public LockOnInputSource ActiveInputSource => activeInputSource;
    public float ChargeElapsed => chargeElapsed;
    public int ChargeStage => chargeStage;
    public int SuccessfulLockCount => lockedTargets.Count;
    public int AssignedLockCount => CountAssignedTargets();
    public int MaxLockStage => stageChargeTimes != null ? stageChargeTimes.Length : 0;
    public float FullChargeDuration =>
        stageChargeTimes != null && stageChargeTimes.Length > 0
            ? stageChargeTimes[stageChargeTimes.Length - 1]
            : 0f;
    public float NextStageProgress =>
        LockOnChargeRules.GetNextStageProgress(chargeElapsed, stageChargeTimes);
    public float ReuseWaitRemaining => Mathf.Max(0f, reuseWaitRemaining);
    public float TargetGraceTime => targetGraceTime;
    public IReadOnlyList<BossLockOnTarget> LockedTargets => lockedTargets;
    public BossLockOnTarget CurrentChargingTarget => currentChargingTarget;
    public LockOnReleaseIntent LastReleaseIntent { get; private set; }
    public bool IsCharging => state == LockOnCombatState.Charging;
    public bool HasValidTargets => targetProvider != null && targetProvider.HasValidTargets;
    public bool IsLockInputAvailable =>
        state == LockOnCombatState.Ready && CanStartLock();
    public float LockReuseWaitDuration => lockReuseWaitDuration;
    public float SalvoLaunchDuration => salvoLaunchDuration;
    public int CurrentLockOnSalvoId => currentLockOnSalvoId;
    public int LastStartedSalvoId { get; private set; }
    public int LastRequestedMissileCount { get; private set; }
    public int LastFiredMissileCount { get; private set; }
    public float LastBaseDamageBudget { get; private set; }
    public float LastBaseDamagePerMissile { get; private set; }
    public float LastFirstMissileDamageMultiplier { get; private set; }
    public bool LastFirstMissileWasInvincible { get; private set; }
    public string LastSalvoFailureStatus { get; private set; } = string.Empty;
    public string LastSalvoFailureReason { get; private set; } = string.Empty;
    public LockOnCombatFeedback CombatFeedback => combatFeedback;

    public event Action<bool> OnLockAvailabilityChanged;
    public event Action<LockOnInputSource> OnLockStart;
    public event Action<int> OnLockStageUp;
    public event Action<BossLockOnTarget, int> OnLockTargetAdded;
    public event Action<LockOnCancelReason> OnLockCanceled;
    public event Action<LockOnReleaseIntent> OnLockRelease;
    public event Action<int> OnFullSalvoStarting;
    public event Action<int, bool> OnLockOnSalvoFinished;
    public event Action<string, string> OnSalvoPrepareFailed;
    public event Action OnFullSalvo;
    public event Action<float> OnLockReuseWaitStarted;
    public event Action OnLockReuseWaitEnded;
    public event Action<LockOnCombatState> OnLockStateChanged;

    public float GetCumulativeChargeTimeForStage(int stage)
    {
        return stageChargeTimes != null && stage >= 1 && stage <= stageChargeTimes.Length
            ? stageChargeTimes[stage - 1]
            : 0f;
    }

    public void Configure(
        BattleController battle,
        PlayerCombatController playerCombat,
        BossLockOnTargetProvider provider,
        PlayerMissileSalvoLauncher launcher = null,
        HUDPresenter hud = null)
    {
        UnsubscribeProvider();
        UnsubscribeSalvoLauncher();
        UnsubscribePlayerCombat();
        EndOwnedSalvoInvincibility();
        currentLockOnSalvoId = 0;
        battleController = battle;
        playerCombatController = playerCombat;
        targetProvider = provider;
        hudPresenter = hud;
        salvoLauncher = launcher != null
            ? launcher
            : GetComponent<PlayerMissileSalvoLauncher>() ??
              gameObject.AddComponent<PlayerMissileSalvoLauncher>();
        salvoLauncher.Configure(battleController, playerCombatController);
        combatFeedback ??= GetComponent<LockOnCombatFeedback>() ??
                           gameObject.AddComponent<LockOnCombatFeedback>();
        combatFeedback.Configure(this, Camera.main);
        EnsureStageConfiguration();
        EnsureSalvoConfiguration();
        ResetChargeData();
        LastReleaseIntent = null;
        reuseWaitRemaining = 0f;
        state = LockOnCombatState.Ready;
        configured = true;
        SubscribeProvider();
        SubscribeSalvoLauncher();
        SubscribePlayerCombat();
        PublishAvailability(force: true);
    }

    private void Update()
    {
        if (!configured)
        {
            return;
        }

        if (state == LockOnCombatState.Charging)
        {
            if (Time.timeScale <= 0f)
            {
                CancelCharging(LockOnCancelReason.GamePaused);
                return;
            }

            if (!IsCombatAvailable())
            {
                CancelCharging(LockOnCancelReason.BattleUnavailable);
                return;
            }
        }

        HandleMouseInput();

        if (state == LockOnCombatState.Charging)
        {
            AdvanceCharge(Time.deltaTime);
        }
        else if (state == LockOnCombatState.ReuseWait)
        {
            TickReuseWait(Time.deltaTime);
        }

        PublishAvailability(force: false);
    }

    private void HandleMouseInput()
    {
        Mouse mouse = Mouse.current;
        if (mouse == null)
        {
            return;
        }

        bool pointerOverUi = EventSystem.current != null &&
                             EventSystem.current.IsPointerOverGameObject();
        if (state == LockOnCombatState.Ready &&
            mouse.rightButton.wasPressedThisFrame &&
            !pointerOverUi)
        {
            TryBeginCharging(LockOnInputSource.MouseRight);
        }

        if (state == LockOnCombatState.Charging &&
            activeInputSource == LockOnInputSource.MouseRight &&
            mouse.rightButton.wasReleasedThisFrame)
        {
            TryReleaseCharging(LockOnInputSource.MouseRight);
        }
    }

    public bool TryBeginCharging(LockOnInputSource source)
    {
        if (source == LockOnInputSource.None ||
            state != LockOnCombatState.Ready ||
            !CanStartLock())
        {
            PublishAvailability(force: false);
            return false;
        }

        ResetChargeData();
        LastReleaseIntent = null;
        activeInputSource = source;
        chargeSessionSeed = unchecked(Environment.TickCount ^ (Time.frameCount * 397));
        SetState(LockOnCombatState.Charging);
        RefreshLockAssignments();
        OnLockStart?.Invoke(source);
        PublishAvailability(force: true);
        return true;
    }

    public bool TryReleaseCharging(LockOnInputSource source)
    {
        if (state != LockOnCombatState.Charging ||
            activeInputSource != source)
        {
            return false;
        }

        RefreshLockAssignments();
        if (targetProvider == null || !targetProvider.HasValidTargets)
        {
            CancelCharging(LockOnCancelReason.TargetsUnavailableOnRelease);
            return false;
        }

        if (SuccessfulLockCount <= 0)
        {
            CancelCharging(LockOnCancelReason.NoSuccessfulLocks);
            return false;
        }

        snapshotBuffer.Clear();
        for (int i = 0; i < lockedTargets.Count; i++)
        {
            SalvoTargetSnapshot snapshot =
                targetProvider.CreateSalvoSnapshot(lockedTargets[i]);
            if (snapshot != null)
            {
                snapshotBuffer.Add(snapshot);
            }
        }

        if (snapshotBuffer.Count == 0 || snapshotBuffer.Count != SuccessfulLockCount)
        {
            CancelCharging(LockOnCancelReason.TargetsUnavailableOnRelease);
            return false;
        }

        LastReleaseIntent = new LockOnReleaseIntent(
            chargeStage,
            SuccessfulLockCount,
            snapshotBuffer.ToArray(),
            source,
            chargeSessionSeed);
        activeInputSource = LockOnInputSource.None;
        SetState(LockOnCombatState.Release);
        bool accepted = TryStartReleasedSalvo(LastReleaseIntent);
        if (!accepted && state == LockOnCombatState.Release)
        {
            RejectRelease();
        }

        PublishAvailability(force: true);
        return accepted;
    }

    public void HandlePointerExit()
    {
        if (state == LockOnCombatState.Charging &&
            activeInputSource == LockOnInputSource.MobileHud)
        {
            CancelCharging(LockOnCancelReason.PointerExit);
        }
    }

    public void HandleGamePaused()
    {
        if (state == LockOnCombatState.Charging)
        {
            CancelCharging(LockOnCancelReason.GamePaused);
        }
    }

    public void AdvanceChargeForDebug(float deltaSeconds)
    {
        if (state == LockOnCombatState.Charging)
        {
            AdvanceCharge(Mathf.Max(0f, deltaSeconds));
        }
    }

    public void AdvanceReuseWaitForDebug(float deltaSeconds)
    {
        if (state == LockOnCombatState.ReuseWait)
        {
            TickReuseWait(Mathf.Max(0f, deltaSeconds));
        }
    }

    public bool ConfirmReleaseAccepted(float reuseWaitDuration)
    {
        if (state != LockOnCombatState.Release)
        {
            return false;
        }

        float duration = Mathf.Max(0f, reuseWaitDuration);
        ResetChargeData();
        reuseWaitRemaining = duration;
        SetState(duration > 0f
            ? LockOnCombatState.ReuseWait
            : LockOnCombatState.Ready);
        if (duration > 0f)
        {
            OnLockReuseWaitStarted?.Invoke(duration);
        }

        PublishAvailability(force: true);
        return true;
    }

    public bool RejectRelease()
    {
        if (state != LockOnCombatState.Release)
        {
            return false;
        }

        ResetChargeData();
        SetState(LockOnCombatState.Ready);
        OnLockCanceled?.Invoke(LockOnCancelReason.ReleaseRejected);
        PublishAvailability(force: true);
        return true;
    }

    private bool TryStartReleasedSalvo(LockOnReleaseIntent intent)
    {
        ResetReleaseDiagnostics();
        if (intent == null || salvoLauncher == null || playerCombatController == null)
        {
            ReportPlayerSalvoFailure("Rejected", "LockOnSalvoDependenciesUnavailable", 0, 0);
            return false;
        }

        if (!LockOnSalvoRules.TryCalculate(
                intent.SuccessfulLockCount,
                missileCountsBySuccessfulLocks,
                totalDamagesBySuccessfulLocks,
                out LockOnSalvoStageCalculation calculation,
                out string calculationFailure))
        {
            ReportPlayerSalvoFailure("Rejected", calculationFailure, 0, 0);
            return false;
        }

        LastRequestedMissileCount = calculation.MissileCount;
        LastBaseDamageBudget = calculation.TotalBaseDamage;
        LastBaseDamagePerMissile = calculation.BaseDamagePerMissile;
        SalvoRequest request = new(
            "LockOn",
            calculation.MissileCount,
            calculation.BaseDamagePerMissile,
            intent.TargetSnapshots,
            missilesPerVolley,
            salvoLaunchDuration,
            intent.RandomSeed,
            SalvoMissileProfileSnapshot.Capture(playerCombatController),
            intent.SuccessfulLockCount);

        SalvoStartResult prepareResult = salvoLauncher.TryPrepareSalvo(
            request,
            out SalvoHandle handle);
        if (!prepareResult.IsPrepared || handle == null)
        {
            ReportPlayerSalvoFailure(
                prepareResult.Status.ToString(),
                prepareResult.Reason,
                calculation.MissileCount,
                prepareResult.BlockingSalvoId);
            return false;
        }

        currentLockOnSalvoId = handle.SalvoId;
        currentLockOnMissilesFired = 0;
        LastStartedSalvoId = handle.SalvoId;
        if (!playerCombatController.BeginSalvoInvincibility())
        {
            salvoLauncher.CancelPreparedSalvo(handle, "SalvoInvincibilityAlreadyActive");
            currentLockOnSalvoId = 0;
            ReportPlayerSalvoFailure(
                "Rejected",
                "SalvoInvincibilityAlreadyActive",
                calculation.MissileCount,
                handle.SalvoId);
            return false;
        }

        ownsSalvoInvincibility = true;
        bool isFullSalvo = intent.SuccessfulLockCount == MaxLockStage;
        if (isFullSalvo)
        {
            // Visual listeners must run before StartPreparedSalvo because its coroutine
            // launches the first missile wave synchronously before returning.
            OnFullSalvoStarting?.Invoke(handle.SalvoId);
        }

        SalvoCommitResult commitResult = salvoLauncher.StartPreparedSalvo(handle);
        if (!commitResult.IsStarted)
        {
            playerCombatController.EndSalvoInvincibility();
            if (isFullSalvo)
            {
                OnLockOnSalvoFinished?.Invoke(handle.SalvoId, true);
            }

            currentLockOnSalvoId = 0;
            ReportPlayerSalvoFailure(
                commitResult.Status.ToString(),
                commitResult.Reason,
                calculation.MissileCount,
                commitResult.SalvoId);
            return false;
        }

        if (!ConfirmReleaseAccepted(lockReuseWaitDuration))
        {
            Debug.LogError(
                $"Lock-on salvo {commitResult.SalvoId} started but the release state could not enter reuse wait.",
                this);
        }

        OnLockRelease?.Invoke(intent);
        if (isFullSalvo)
        {
            OnFullSalvo?.Invoke();
        }

        return true;
    }

    private void ResetReleaseDiagnostics()
    {
        LastStartedSalvoId = 0;
        LastRequestedMissileCount = 0;
        LastFiredMissileCount = 0;
        LastBaseDamageBudget = 0f;
        LastBaseDamagePerMissile = 0f;
        LastFirstMissileDamageMultiplier = 0f;
        LastFirstMissileWasInvincible = false;
        LastSalvoFailureStatus = string.Empty;
        LastSalvoFailureReason = string.Empty;
    }

    private void ReportPlayerSalvoFailure(
        string status,
        string reason,
        int requestedMissiles,
        int salvoId)
    {
        LastSalvoFailureStatus = string.IsNullOrWhiteSpace(status) ? "Rejected" : status;
        LastSalvoFailureReason = string.IsNullOrWhiteSpace(reason) ? "UnknownSalvoFailure" : reason;
        OnSalvoPrepareFailed?.Invoke(LastSalvoFailureStatus, LastSalvoFailureReason);
        hudPresenter?.ShowShootError(LastSalvoFailureReason);
        Debug.LogWarning(
            $"[LockOnSalvo] SHOOT ERROR status={LastSalvoFailureStatus}, " +
            $"reason={LastSalvoFailureReason}, requested={requestedMissiles}, " +
            $"salvoId={salvoId}, available={salvoLauncher?.PoolAvailableMissiles ?? 0}, " +
            $"reserved={salvoLauncher?.PoolReservedMissiles ?? 0}, " +
            $"leased={salvoLauncher?.PoolLeasedMissiles ?? 0}.",
            this);
    }

    private void AdvanceCharge(float deltaSeconds)
    {
        chargeElapsed = Mathf.Max(0f, chargeElapsed + deltaSeconds);
        chargeStage = Mathf.Max(
            chargeStage,
            LockOnChargeRules.GetReachedStage(chargeElapsed, stageChargeTimes));
        RefreshLockAssignments();

        if (targetProvider != null && targetProvider.HasValidTargets)
        {
            noTargetSince = -1f;
            return;
        }

        if (noTargetSince < 0f)
        {
            noTargetSince = Time.time;
        }
        else if (Time.time - noTargetSince >= targetGraceTime)
        {
            CancelCharging(LockOnCancelReason.TargetGraceExpired);
        }
    }

    private void RefreshLockAssignments()
    {
        if (state != LockOnCombatState.Charging || targetProvider == null ||
            refreshingLockAssignments)
        {
            return;
        }

        refreshingLockAssignments = true;
        try
        {
            usedTargets.Clear();
            for (int i = 0; i < lockedTargets.Count; i++)
            {
                BossLockOnTarget target = lockedTargets[i];
                if (target == null || !target.IsSelectable || !usedTargets.Add(target))
                {
                    lockedTargets[i] = null;
                }
            }

            for (int i = 0; i < lockedTargets.Count; i++)
            {
                if (lockedTargets[i] != null)
                {
                    continue;
                }

                BossLockOnTarget replacement = FindNextUniqueCandidate(i + 1);
                if (replacement == null)
                {
                    continue;
                }

                lockedTargets[i] = replacement;
                usedTargets.Add(replacement);
                replacement.MarkLocked(i + 1);
                OnLockTargetAdded?.Invoke(replacement, SuccessfulLockCount);
            }

            if (currentChargingTarget != null &&
                (!currentChargingTarget.IsSelectable || usedTargets.Contains(currentChargingTarget)))
            {
                currentChargingTarget = null;
            }

            while (lockedTargets.Count < chargeStage)
            {
                BossLockOnTarget target = TakeCurrentChargingTargetOrFindCandidate(
                    lockedTargets.Count + 1);
                if (target == null)
                {
                    break;
                }

                lockedTargets.Add(target);
                usedTargets.Add(target);
                target.MarkLocked(lockedTargets.Count);
                OnLockStageUp?.Invoke(SuccessfulLockCount);
                OnLockTargetAdded?.Invoke(target, SuccessfulLockCount);
            }

            RefreshCurrentChargingTarget();
        }
        finally
        {
            refreshingLockAssignments = false;
        }
    }

    private BossLockOnTarget TakeCurrentChargingTargetOrFindCandidate(int slotNumber)
    {
        BossLockOnTarget target = currentChargingTarget;
        currentChargingTarget = null;
        if (target != null && target.IsSelectable && !usedTargets.Contains(target))
        {
            return target;
        }

        return FindNextUniqueCandidate(slotNumber);
    }

    private void RefreshCurrentChargingTarget()
    {
        if (state != LockOnCombatState.Charging ||
            chargeStage >= MaxLockStage ||
            lockedTargets.Count >= MaxLockStage)
        {
            currentChargingTarget = null;
            return;
        }

        if (currentChargingTarget != null &&
            currentChargingTarget.IsSelectable &&
            !usedTargets.Contains(currentChargingTarget))
        {
            return;
        }

        currentChargingTarget = FindNextUniqueCandidate(lockedTargets.Count + 1);
    }

    private BossLockOnTarget FindNextUniqueCandidate(int slotNumber)
    {
        int requestedCount = Mathf.Max(MaxLockStage, targetProvider.ValidTargetCount);
        int seed = unchecked(chargeSessionSeed ^ (slotNumber * 0x4B3D));
        targetProvider.BuildTargetSequence(
            requestedCount,
            seed,
            candidateBuffer,
            recordLockAssignments: false);
        for (int i = 0; i < candidateBuffer.Count; i++)
        {
            BossLockOnTarget candidate = candidateBuffer[i];
            if (candidate != null && candidate.IsSelectable && !usedTargets.Contains(candidate))
            {
                return candidate;
            }
        }

        return null;
    }

    private void TickReuseWait(float deltaSeconds)
    {
        reuseWaitRemaining = Mathf.Max(0f, reuseWaitRemaining - Mathf.Max(0f, deltaSeconds));
        if (reuseWaitRemaining > 0f)
        {
            return;
        }

        SetState(LockOnCombatState.Ready);
        OnLockReuseWaitEnded?.Invoke();
        PublishAvailability(force: true);
    }

    private void CancelCharging(LockOnCancelReason reason)
    {
        if (state != LockOnCombatState.Charging)
        {
            return;
        }

        ResetChargeData();
        SetState(LockOnCombatState.Ready);
        OnLockCanceled?.Invoke(reason);
        PublishAvailability(force: true);
    }

    private bool CanStartLock()
    {
        return IsCombatAvailable() &&
               targetProvider != null &&
               targetProvider.HasValidTargets;
    }

    private bool IsCombatAvailable()
    {
        return battleController != null &&
               battleController.IsBattleActive &&
               playerCombatController != null &&
               playerCombatController.IsAlive;
    }

    private int CountAssignedTargets()
    {
        int count = 0;
        for (int i = 0; i < lockedTargets.Count; i++)
        {
            if (lockedTargets[i] != null && lockedTargets[i].IsSelectable)
            {
                count++;
            }
        }

        return count;
    }

    private void ResetChargeData()
    {
        lockedTargets.Clear();
        candidateBuffer.Clear();
        snapshotBuffer.Clear();
        usedTargets.Clear();
        currentChargingTarget = null;
        chargeElapsed = 0f;
        chargeStage = 0;
        noTargetSince = -1f;
        activeInputSource = LockOnInputSource.None;
    }

    private void SetState(LockOnCombatState nextState)
    {
        if (state == nextState)
        {
            return;
        }

        state = nextState;
        OnLockStateChanged?.Invoke(state);
    }

    private void PublishAvailability(bool force)
    {
        bool available = IsLockInputAvailable;
        if (!force && available == lastAvailability)
        {
            return;
        }

        lastAvailability = available;
        OnLockAvailabilityChanged?.Invoke(available);
    }

    private void HandleTargetsChanged()
    {
        if (state == LockOnCombatState.Charging)
        {
            RefreshLockAssignments();
        }

        PublishAvailability(force: false);
    }

    private void HandlePlayerDamageApplied()
    {
        if (state == LockOnCombatState.Charging)
        {
            CancelCharging(LockOnCancelReason.PlayerDamaged);
        }
    }

    private void SubscribeProvider()
    {
        if (targetProvider != null)
        {
            targetProvider.TargetsChanged -= HandleTargetsChanged;
            targetProvider.TargetsChanged += HandleTargetsChanged;
        }
    }

    private void HandleSalvoStarted(int salvoId, string source)
    {
        if (salvoId == currentLockOnSalvoId)
        {
            hudPresenter?.ClearShootError();
        }
    }

    private void HandleMissileFired(int salvoId, SalvoTargetSnapshot target)
    {
        if (salvoId != currentLockOnSalvoId)
        {
            return;
        }

        currentLockOnMissilesFired++;
        LastFiredMissileCount = currentLockOnMissilesFired;
        if (currentLockOnMissilesFired == 1)
        {
            LastFirstMissileWasInvincible = playerCombatController != null &&
                                            playerCombatController.IsSalvoInvincible;
            LastFirstMissileDamageMultiplier = target != null
                ? target.DamageMultiplier
                : 0f;
        }
    }

    private void HandleSalvoCompleted(int salvoId)
    {
        if (salvoId != currentLockOnSalvoId)
        {
            return;
        }

        EndOwnedSalvoInvincibility();
        OnLockOnSalvoFinished?.Invoke(salvoId, false);
        currentLockOnSalvoId = 0;
    }

    private void HandleSalvoCanceled(int salvoId, string reason)
    {
        if (salvoId != currentLockOnSalvoId)
        {
            return;
        }

        bool canceledBeforeFirstMissile = currentLockOnMissilesFired == 0;
        EndOwnedSalvoInvincibility();
        OnLockOnSalvoFinished?.Invoke(salvoId, true);
        currentLockOnSalvoId = 0;
        if (canceledBeforeFirstMissile && state == LockOnCombatState.ReuseWait)
        {
            ReportPlayerSalvoFailure(
                "Canceled",
                string.IsNullOrWhiteSpace(reason) ? "SalvoCanceledBeforeFirstMissile" : reason,
                LastRequestedMissileCount,
                salvoId);
        }
        else if (!canceledBeforeFirstMissile)
        {
            Debug.LogWarning(
                $"[LockOnSalvo] Salvo {salvoId} canceled after " +
                $"{currentLockOnMissilesFired}/{LastRequestedMissileCount} missiles: {reason}",
                this);
        }
    }

    private void EndOwnedSalvoInvincibility()
    {
        if (!ownsSalvoInvincibility)
        {
            return;
        }

        ownsSalvoInvincibility = false;
        playerCombatController?.EndSalvoInvincibility();
    }

    private void SubscribeSalvoLauncher()
    {
        if (salvoLauncher == null)
        {
            return;
        }

        salvoLauncher.SalvoStarted -= HandleSalvoStarted;
        salvoLauncher.MissileFired -= HandleMissileFired;
        salvoLauncher.SalvoCompleted -= HandleSalvoCompleted;
        salvoLauncher.SalvoCanceled -= HandleSalvoCanceled;
        salvoLauncher.SalvoStarted += HandleSalvoStarted;
        salvoLauncher.MissileFired += HandleMissileFired;
        salvoLauncher.SalvoCompleted += HandleSalvoCompleted;
        salvoLauncher.SalvoCanceled += HandleSalvoCanceled;
    }

    private void UnsubscribeSalvoLauncher()
    {
        if (salvoLauncher == null)
        {
            return;
        }

        salvoLauncher.SalvoStarted -= HandleSalvoStarted;
        salvoLauncher.MissileFired -= HandleMissileFired;
        salvoLauncher.SalvoCompleted -= HandleSalvoCompleted;
        salvoLauncher.SalvoCanceled -= HandleSalvoCanceled;
    }

    private void UnsubscribeProvider()
    {
        if (targetProvider != null)
        {
            targetProvider.TargetsChanged -= HandleTargetsChanged;
        }
    }

    private void SubscribePlayerCombat()
    {
        if (playerCombatController == null)
        {
            return;
        }

        playerCombatController.DamageApplied -= HandlePlayerDamageApplied;
        playerCombatController.DamageApplied += HandlePlayerDamageApplied;
    }

    private void UnsubscribePlayerCombat()
    {
        if (playerCombatController != null)
        {
            playerCombatController.DamageApplied -= HandlePlayerDamageApplied;
        }
    }

    private void EnsureStageConfiguration()
    {
        if (stageChargeTimes == null || stageChargeTimes.Length != DefaultStageChargeTimes.Length ||
            !LockOnChargeRules.AreStrictlyIncreasing(stageChargeTimes) ||
            MatchesStageChargeTimes(stageChargeTimes, LegacyDefaultStageChargeTimes) ||
            MatchesStageChargeTimes(stageChargeTimes, PreviousDefaultStageChargeTimes))
        {
            stageChargeTimes = (float[])DefaultStageChargeTimes.Clone();
        }

        targetGraceTime = Mathf.Clamp(targetGraceTime, 0.1f, 3f);
    }

    private static bool MatchesStageChargeTimes(float[] values, float[] expected)
    {
        if (values == null || expected == null || values.Length != expected.Length)
        {
            return false;
        }

        for (int i = 0; i < values.Length; i++)
        {
            if (!Mathf.Approximately(values[i], expected[i]))
            {
                return false;
            }
        }

        return true;
    }

    private void EnsureSalvoConfiguration()
    {
        if (!HasValidMissileCounts(missileCountsBySuccessfulLocks))
        {
            missileCountsBySuccessfulLocks =
                (int[])DefaultMissileCountsBySuccessfulLocks.Clone();
        }

        if (!HasValidStageTotalDamages(totalDamagesBySuccessfulLocks))
        {
            totalDamagesBySuccessfulLocks =
                (float[])DefaultTotalDamagesBySuccessfulLocks.Clone();
        }

        missilesPerVolley = Mathf.Max(1, missilesPerVolley);
        salvoLaunchDuration = Mathf.Max(0.01f, salvoLaunchDuration);
        lockReuseWaitDuration = Mathf.Max(0f, lockReuseWaitDuration);
    }

    private static bool HasValidMissileCounts(int[] values)
    {
        if (values == null || values.Length != DefaultMissileCountsBySuccessfulLocks.Length)
        {
            return false;
        }

        for (int i = 0; i < values.Length; i++)
        {
            if (values[i] <= 0 || values[i] > PlayerMissileSalvoLauncher.MaxSalvoMissileCount)
            {
                return false;
            }
        }

        return true;
    }

    private static bool HasValidStageTotalDamages(float[] values)
    {
        if (values == null || values.Length != DefaultTotalDamagesBySuccessfulLocks.Length)
        {
            return false;
        }

        for (int i = 0; i < values.Length; i++)
        {
            if (values[i] <= 0f || float.IsNaN(values[i]) || float.IsInfinity(values[i]))
            {
                return false;
            }
        }

        return true;
    }

    private void OnDisable()
    {
        if (state == LockOnCombatState.Charging)
        {
            CancelCharging(LockOnCancelReason.ComponentDisabled);
        }

        UnsubscribeProvider();
        UnsubscribeSalvoLauncher();
        UnsubscribePlayerCombat();
        EndOwnedSalvoInvincibility();
        currentLockOnSalvoId = 0;
    }

    private void OnEnable()
    {
        if (configured)
        {
            SubscribeProvider();
            SubscribeSalvoLauncher();
            SubscribePlayerCombat();
            PublishAvailability(force: true);
        }
    }

    private void OnDestroy()
    {
        UnsubscribeProvider();
        UnsubscribeSalvoLauncher();
        UnsubscribePlayerCombat();
        EndOwnedSalvoInvincibility();
    }

    private void OnValidate()
    {
        EnsureStageConfiguration();
        EnsureSalvoConfiguration();
    }
}
