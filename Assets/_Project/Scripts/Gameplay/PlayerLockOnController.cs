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
    private static readonly float[] DefaultStageChargeTimes =
        { 0.35f, 0.75f, 1.25f, 1.80f, 2.50f };

    [SerializeField] private float[] stageChargeTimes =
        { 0.35f, 0.75f, 1.25f, 1.80f, 2.50f };
    [SerializeField, Range(0.1f, 3f)] private float targetGraceTime = 1.25f;

    private readonly List<BossLockOnTarget> lockedTargets = new();
    private readonly List<BossLockOnTarget> candidateBuffer = new();
    private readonly List<SalvoTargetSnapshot> snapshotBuffer = new();
    private readonly HashSet<BossLockOnTarget> usedTargets = new();

    private BattleController battleController;
    private PlayerCombatController playerCombatController;
    private BossLockOnTargetProvider targetProvider;
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

    public LockOnCombatState State => state;
    public LockOnInputSource ActiveInputSource => activeInputSource;
    public float ChargeElapsed => chargeElapsed;
    public int ChargeStage => chargeStage;
    public int SuccessfulLockCount => lockedTargets.Count;
    public int AssignedLockCount => CountAssignedTargets();
    public int MaxLockStage => stageChargeTimes != null ? stageChargeTimes.Length : 0;
    public float NextStageProgress =>
        LockOnChargeRules.GetNextStageProgress(chargeElapsed, stageChargeTimes);
    public float ReuseWaitRemaining => Mathf.Max(0f, reuseWaitRemaining);
    public float TargetGraceTime => targetGraceTime;
    public IReadOnlyList<BossLockOnTarget> LockedTargets => lockedTargets;
    public LockOnReleaseIntent LastReleaseIntent { get; private set; }
    public bool IsCharging => state == LockOnCombatState.Charging;
    public bool HasValidTargets => targetProvider != null && targetProvider.HasValidTargets;
    public bool IsLockInputAvailable =>
        state == LockOnCombatState.Ready && CanStartLock();

    public event Action<bool> OnLockAvailabilityChanged;
    public event Action<LockOnInputSource> OnLockStart;
    public event Action<int> OnLockStageUp;
    public event Action<BossLockOnTarget, int> OnLockTargetAdded;
    public event Action<LockOnCancelReason> OnLockCanceled;
    public event Action<LockOnReleaseIntent> OnLockRelease;
    public event Action<float> OnLockReuseWaitStarted;
    public event Action OnLockReuseWaitEnded;
    public event Action<LockOnCombatState> OnLockStateChanged;

    public void Configure(
        BattleController battle,
        PlayerCombatController playerCombat,
        BossLockOnTargetProvider provider)
    {
        UnsubscribeProvider();
        battleController = battle;
        playerCombatController = playerCombat;
        targetProvider = provider;
        EnsureStageConfiguration();
        ResetChargeData();
        LastReleaseIntent = null;
        reuseWaitRemaining = 0f;
        state = LockOnCombatState.Ready;
        configured = true;
        SubscribeProvider();
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

        if (snapshotBuffer.Count == 0)
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
        OnLockRelease?.Invoke(LastReleaseIntent);

        // Stage 4 exposes the immutable release intent. Stage 5 accepts it through
        // ConfirmReleaseAccepted only after the shared launcher prepares the salvo.
        if (state == LockOnCombatState.Release)
        {
            ResetChargeData();
            SetState(LockOnCombatState.Ready);
        }

        PublishAvailability(force: true);
        return true;
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

            while (lockedTargets.Count < chargeStage)
            {
                BossLockOnTarget target = FindNextUniqueCandidate(lockedTargets.Count + 1);
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
        }
        finally
        {
            refreshingLockAssignments = false;
        }
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

    private void SubscribeProvider()
    {
        if (targetProvider != null)
        {
            targetProvider.TargetsChanged -= HandleTargetsChanged;
            targetProvider.TargetsChanged += HandleTargetsChanged;
        }
    }

    private void UnsubscribeProvider()
    {
        if (targetProvider != null)
        {
            targetProvider.TargetsChanged -= HandleTargetsChanged;
        }
    }

    private void EnsureStageConfiguration()
    {
        if (stageChargeTimes == null || stageChargeTimes.Length != DefaultStageChargeTimes.Length ||
            !LockOnChargeRules.AreStrictlyIncreasing(stageChargeTimes))
        {
            stageChargeTimes = (float[])DefaultStageChargeTimes.Clone();
        }

        targetGraceTime = Mathf.Clamp(targetGraceTime, 0.1f, 3f);
    }

    private void OnDisable()
    {
        if (state == LockOnCombatState.Charging)
        {
            CancelCharging(LockOnCancelReason.ComponentDisabled);
        }

        UnsubscribeProvider();
    }

    private void OnEnable()
    {
        if (configured)
        {
            SubscribeProvider();
            PublishAvailability(force: true);
        }
    }

    private void OnDestroy()
    {
        UnsubscribeProvider();
    }

    private void OnValidate()
    {
        EnsureStageConfiguration();
    }
}
