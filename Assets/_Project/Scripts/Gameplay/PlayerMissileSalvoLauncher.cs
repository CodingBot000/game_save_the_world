using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public enum SalvoStartStatus
{
    Prepared,
    Busy,
    Rejected,
}

public enum SalvoCommitStatus
{
    Started,
    Rejected,
    Canceled,
}

public enum SalvoTargetLossPolicy
{
    InertialFlightThenExpire,
}

public readonly struct SalvoStartResult
{
    private SalvoStartResult(
        SalvoStartStatus status,
        string reason,
        int blockingSalvoId)
    {
        Status = status;
        Reason = reason ?? string.Empty;
        BlockingSalvoId = blockingSalvoId;
    }

    public SalvoStartStatus Status { get; }
    public string Reason { get; }
    public int BlockingSalvoId { get; }
    public bool IsPrepared => Status == SalvoStartStatus.Prepared;

    public static SalvoStartResult Prepared()
    {
        return new SalvoStartResult(SalvoStartStatus.Prepared, string.Empty, 0);
    }

    public static SalvoStartResult Busy(int blockingSalvoId)
    {
        return new SalvoStartResult(
            SalvoStartStatus.Busy,
            "Another missile salvo is already prepared or launching.",
            blockingSalvoId);
    }

    public static SalvoStartResult Rejected(string reason)
    {
        return new SalvoStartResult(SalvoStartStatus.Rejected, reason, 0);
    }
}

public readonly struct SalvoCommitResult
{
    private SalvoCommitResult(SalvoCommitStatus status, string reason, int salvoId)
    {
        Status = status;
        Reason = reason ?? string.Empty;
        SalvoId = salvoId;
    }

    public SalvoCommitStatus Status { get; }
    public string Reason { get; }
    public int SalvoId { get; }
    public bool IsStarted => Status == SalvoCommitStatus.Started;

    public static SalvoCommitResult Started(int salvoId)
    {
        return new SalvoCommitResult(SalvoCommitStatus.Started, string.Empty, salvoId);
    }

    public static SalvoCommitResult Rejected(string reason, int salvoId = 0)
    {
        return new SalvoCommitResult(SalvoCommitStatus.Rejected, reason, salvoId);
    }

    public static SalvoCommitResult Canceled(string reason, int salvoId = 0)
    {
        return new SalvoCommitResult(SalvoCommitStatus.Canceled, reason, salvoId);
    }
}

public sealed class SalvoTargetSnapshot
{
    public SalvoTargetSnapshot(
        Transform target,
        string targetId,
        bool weakPointOpen,
        float damageMultiplier,
        SalvoTargetLossPolicy targetLossPolicy = SalvoTargetLossPolicy.InertialFlightThenExpire)
    {
        Target = target;
        TargetId = string.IsNullOrWhiteSpace(targetId)
            ? target != null ? target.GetEntityId().ToString() : "UNKNOWN"
            : targetId;
        WeakPointOpen = weakPointOpen;
        DamageMultiplier = Mathf.Max(0f, damageMultiplier);
        TargetLossPolicy = targetLossPolicy;
        TargetWorldPosition = target != null ? target.position : Vector3.zero;
    }

    public Transform Target { get; }
    public string TargetId { get; }
    public bool WeakPointOpen { get; }
    public float DamageMultiplier { get; }
    public SalvoTargetLossPolicy TargetLossPolicy { get; }
    public Vector3 TargetWorldPosition { get; }
}

public sealed class SalvoMissileProfileSnapshot
{
    private SalvoMissileProfileSnapshot()
    {
    }

    public float LaunchSpeed { get; private set; }
    public float CruiseSpeed { get; private set; }
    public float Acceleration { get; private set; }
    public float TurnRate { get; private set; }
    public float LockOnDelay { get; private set; }
    public float StraightPhaseDuration { get; private set; }
    public float StraightPhaseDistance { get; private set; }
    public float TurnPhaseDuration { get; private set; }
    public float BoostPhaseDuration { get; private set; }
    public float Lifetime { get; private set; }
    public float HitRadius { get; private set; }
    public GameObject VisualTemplate { get; private set; }
    public GameObject SmokeTemplate { get; private set; }
    public GameObject ImpactEffectTemplate { get; private set; }
    public Texture2D VisualTexture { get; private set; }
    public Texture2D SmokeTexture { get; private set; }
    public float VisualScale { get; private set; }
    public float SmokeScale { get; private set; }
    public float ImpactEffectScale { get; private set; }
    public bool UseTemplateOriginalMaterials { get; private set; }
    public Color TemplateTint { get; private set; }
    public Vector3 TemplateLocalEulerAngles { get; private set; }

    public static SalvoMissileProfileSnapshot Capture(PlayerCombatController playerCombatController)
    {
        if (playerCombatController == null)
        {
            return null;
        }

        float boostDuration = Mathf.Max(0.01f, playerCombatController.DebugMissileBoostPhaseDuration);
        float derivedAcceleration = Mathf.Abs(
            playerCombatController.DebugMissileCruiseSpeed -
            playerCombatController.DebugMissileLaunchSpeed) / boostDuration;

        return new SalvoMissileProfileSnapshot
        {
            LaunchSpeed = playerCombatController.DebugMissileLaunchSpeed,
            CruiseSpeed = playerCombatController.DebugMissileCruiseSpeed,
            Acceleration = Mathf.Max(playerCombatController.DebugMissileAcceleration, derivedAcceleration),
            TurnRate = playerCombatController.DebugMissileTurnRate,
            LockOnDelay = playerCombatController.DebugMissileLockOnDelay,
            StraightPhaseDuration = playerCombatController.DebugMissileStraightPhaseDuration,
            StraightPhaseDistance = playerCombatController.DebugMissileStraightPhaseDistance,
            TurnPhaseDuration = playerCombatController.DebugMissileTurnPhaseDuration,
            BoostPhaseDuration = playerCombatController.DebugMissileBoostPhaseDuration,
            Lifetime = playerCombatController.DebugMissileLifetime,
            HitRadius = playerCombatController.DebugMissileHitRadius,
            VisualTemplate = playerCombatController.DebugMissileVisualTemplate,
            SmokeTemplate = playerCombatController.DebugMissileSmokeTemplate,
            ImpactEffectTemplate = playerCombatController.DebugMissileImpactEffectTemplate,
            VisualTexture = playerCombatController.DebugMissileVisualTexture,
            SmokeTexture = playerCombatController.DebugMissileSmokeTexture,
            VisualScale = playerCombatController.DebugMissileVisualScale,
            SmokeScale = playerCombatController.DebugMissileSmokeScale,
            ImpactEffectScale = playerCombatController.DebugMissileImpactEffectScale,
            UseTemplateOriginalMaterials = playerCombatController.DebugMissileUseTemplateOriginalMaterials,
            TemplateTint = playerCombatController.DebugMissileTemplateTint,
            TemplateLocalEulerAngles = playerCombatController.DebugMissileTemplateLocalEulerAngles,
        };
    }
}

public sealed class SalvoRequest
{
    public SalvoRequest(
        string source,
        int missileCount,
        float damagePerMissile,
        IReadOnlyList<SalvoTargetSnapshot> targets,
        int missilesPerVolley = 4,
        float salvoDuration = 0.6f,
        int randomSeed = 0,
        SalvoMissileProfileSnapshot missileProfile = null,
        int successfulLockCount = 0)
    {
        Source = string.IsNullOrWhiteSpace(source) ? "Unknown" : source;
        MissileCount = missileCount;
        DamagePerMissile = damagePerMissile;
        Targets = targets;
        MissilesPerVolley = missilesPerVolley;
        SalvoDuration = salvoDuration;
        RandomSeed = randomSeed;
        MissileProfile = missileProfile;
        SuccessfulLockCount = successfulLockCount;
    }

    public string Source { get; }
    public int MissileCount { get; }
    public float DamagePerMissile { get; }
    public IReadOnlyList<SalvoTargetSnapshot> Targets { get; }
    public int MissilesPerVolley { get; }
    public float SalvoDuration { get; }
    public int RandomSeed { get; }
    public SalvoMissileProfileSnapshot MissileProfile { get; }
    public int SuccessfulLockCount { get; }
}

public sealed class SalvoHandle
{
    internal enum HandleState
    {
        Prepared,
        Launching,
        Completed,
        Canceled,
    }

    internal SalvoHandle(
        int salvoId,
        int preparedFrame,
        SalvoRequestSnapshot request,
        MissilePoolReservation reservation)
    {
        SalvoId = salvoId;
        PreparedFrame = preparedFrame;
        Request = request;
        Reservation = reservation;
        State = HandleState.Prepared;
    }

    public int SalvoId { get; }
    public string Source => Request.Source;
    public int MissileCount => Request.MissileCount;
    public int SuccessfulLockCount => Request.SuccessfulLockCount;
    internal int PreparedFrame { get; }
    internal SalvoRequestSnapshot Request { get; }
    internal MissilePoolReservation Reservation { get; }
    internal HandleState State { get; set; }
    internal string LastReason { get; set; }
}

internal sealed class SalvoRequestSnapshot
{
    public string Source;
    public int MissileCount;
    public int SuccessfulLockCount;
    public float DamagePerMissile;
    public SalvoTargetSnapshot[] Targets;
    public int MissilesPerVolley;
    public float SalvoDuration;
    public int RandomSeed;
    public SalvoMissileProfileSnapshot MissileProfile;
    public int[] TargetAssignment;
    public int[] TargetAssignmentCounts;
}

public sealed class PlayerMissileSalvoLauncher : MonoBehaviour
{
    private const int DefaultMissilesPerVolley = 4;
    private const float DefaultSalvoDuration = 0.6f;
    public const int MaxSalvoMissileCount = 30;
    public const int MissilePoolCapacity = 40;

    [Header("Missile Strike Distribution")]
    [SerializeField] private float targetSpreadRadius = 1.6f;
    [SerializeField] private float targetSpreadVerticalScale = 1.25f;
    [SerializeField] private float targetSpreadDepth = 0.2f;

    [Header("Missile Strike Flight")]
    [SerializeField] private float fanOutDuration = 0.28f;
    [SerializeField] private float fanOutDistance = 5.5f;
    [SerializeField] private float fanOutHorizontal = 1f;
    [SerializeField] private float fanOutVertical = 0.65f;
    [SerializeField] private float arcDuration = 0.75f;
    [SerializeField] private float arcDurationJitter = 0.18f;
    [SerializeField] private float arcHorizontalRadius = 10f;
    [SerializeField] private float arcVerticalRadius = 7f;
    [SerializeField] private float terminalEntryDistance = 8f;

    private BattleController battleController;
    private PlayerCombatController playerCombatController;
    private SpecialMissilePool missilePool;
    private SalvoHandle currentHandle;
    private Coroutine activeRoutine;
    private int nextSalvoId;

    public bool IsBusy => currentHandle != null;
    public int ActiveSalvoId => currentHandle != null ? currentHandle.SalvoId : 0;
    public int PoolAvailableMissiles => missilePool != null ? missilePool.AvailableMissiles : 0;
    public int PoolReservedMissiles => missilePool != null ? missilePool.ReservedMissiles : 0;
    public int PoolLeasedMissiles => missilePool != null ? missilePool.LeasedMissiles : 0;
    public bool HasValidPoolCounts => missilePool != null && missilePool.HasValidMissileCounts;
    public event Action<int, string> SalvoStarted;
    public event Action<int, SalvoTargetSnapshot> MissileFired;
    public event Action<int> SalvoCompleted;
    public event Action<int, string> SalvoCanceled;

    public void Configure(BattleController battle, PlayerCombatController playerCombat)
    {
        battleController = battle;
        playerCombatController = playerCombat;
        EnsureMissilePool();
    }

    public SalvoStartResult TryPrepareSalvo(SalvoRequest request, out SalvoHandle handle)
    {
        handle = null;
        CancelExpiredPreparedHandle();

        if (currentHandle != null)
        {
            return SalvoStartResult.Busy(currentHandle.SalvoId);
        }

        string rejectionReason = ValidateRequest(request);
        if (!string.IsNullOrEmpty(rejectionReason))
        {
            return SalvoStartResult.Rejected(rejectionReason);
        }

        SalvoRequestSnapshot snapshot = CreateRequestSnapshot(request);
        SpecialMissilePool pool = EnsureMissilePool();
        MissilePoolReservationFailure reservationFailure =
            MissilePoolReservationFailure.PoolCapacityUnavailable;
        if (pool == null ||
            !pool.TryReserve(
                request.MissileCount,
                out MissilePoolReservation reservation,
                out reservationFailure))
        {
            string reason = pool == null
                ? "PoolCapacityUnavailable"
                : reservationFailure.ToString();
            return SalvoStartResult.Rejected(reason);
        }

        int salvoId = NextSalvoId();
        handle = new SalvoHandle(salvoId, Time.frameCount, snapshot, reservation);
        currentHandle = handle;
        return SalvoStartResult.Prepared();
    }

    public SalvoCommitResult StartPreparedSalvo(SalvoHandle handle)
    {
        if (handle == null)
        {
            return SalvoCommitResult.Rejected("SalvoHandleNull");
        }

        if (currentHandle != handle || handle.State != SalvoHandle.HandleState.Prepared)
        {
            return SalvoCommitResult.Rejected("SalvoHandleNotPrepared", handle.SalvoId);
        }

        if (handle.PreparedFrame != Time.frameCount)
        {
            CancelPreparedSalvo(handle, "PreparedHandleExpired");
            return SalvoCommitResult.Canceled("PreparedHandleExpired", handle.SalvoId);
        }

        string runtimeReason = ValidateRuntimeState();
        if (!string.IsNullOrEmpty(runtimeReason))
        {
            CancelPreparedSalvo(handle, runtimeReason);
            return SalvoCommitResult.Rejected(runtimeReason, handle.SalvoId);
        }

        try
        {
            handle.State = SalvoHandle.HandleState.Launching;
            SalvoStarted?.Invoke(handle.SalvoId, handle.Source);
            if (currentHandle != handle || handle.State != SalvoHandle.HandleState.Launching)
            {
                return SalvoCommitResult.Rejected(
                    string.IsNullOrEmpty(handle.LastReason) ? "SalvoCanceledBeforeStart" : handle.LastReason,
                    handle.SalvoId);
            }

            Coroutine startedRoutine = StartCoroutine(LaunchMissileSalvo(handle));
            if (startedRoutine == null)
            {
                CancelCurrentHandle(handle, "SalvoCoroutineStartFailed");
                return SalvoCommitResult.Rejected("SalvoCoroutineStartFailed", handle.SalvoId);
            }

            if (handle.State == SalvoHandle.HandleState.Canceled)
            {
                return SalvoCommitResult.Rejected(
                    string.IsNullOrEmpty(handle.LastReason) ? "SalvoStartFailed" : handle.LastReason,
                    handle.SalvoId);
            }

            activeRoutine = currentHandle == handle ? startedRoutine : null;
            return SalvoCommitResult.Started(handle.SalvoId);
        }
        catch (Exception exception)
        {
            Debug.LogException(exception, this);
            CancelCurrentHandle(handle, $"SalvoStartException:{exception.GetType().Name}");
            return SalvoCommitResult.Rejected("SalvoStartException", handle.SalvoId);
        }
    }

    public void CancelPreparedSalvo(SalvoHandle handle, string reason)
    {
        if (handle == null || currentHandle != handle || handle.State != SalvoHandle.HandleState.Prepared)
        {
            return;
        }

        CancelCurrentHandle(handle, string.IsNullOrWhiteSpace(reason) ? "Canceled" : reason);
    }

    public bool IsSalvoActive(int salvoId)
    {
        return salvoId != 0 && currentHandle != null && currentHandle.SalvoId == salvoId;
    }

    private void Update()
    {
        CancelExpiredPreparedHandle();
    }

    private void CancelExpiredPreparedHandle()
    {
        if (currentHandle == null ||
            currentHandle.State != SalvoHandle.HandleState.Prepared ||
            currentHandle.PreparedFrame == Time.frameCount)
        {
            return;
        }

        CancelCurrentHandle(currentHandle, "PreparedHandleExpired");
    }

    private IEnumerator LaunchMissileSalvo(SalvoHandle handle)
    {
        try
        {
            SalvoRequestSnapshot request = handle.Request;
            int volleyCount = Mathf.CeilToInt(request.MissileCount / (float)request.MissilesPerVolley);
            float launchInterval = volleyCount > 1
                ? request.SalvoDuration / (volleyCount - 1)
                : 0f;
            int[] targetOrdinals = new int[request.Targets.Length];

            for (int volleyStart = 0; volleyStart < request.MissileCount; volleyStart += request.MissilesPerVolley)
            {
                string unavailableReason = ValidateRuntimeState();
                if (!string.IsNullOrEmpty(unavailableReason))
                {
                    CancelCurrentHandle(handle, unavailableReason, stopRoutine: false);
                    yield break;
                }

                int volleyEnd = Mathf.Min(volleyStart + request.MissilesPerVolley, request.MissileCount);
                for (int missileIndex = volleyStart; missileIndex < volleyEnd; missileIndex++)
                {
                    int targetIndex = request.TargetAssignment[missileIndex];
                    int targetOrdinal = targetOrdinals[targetIndex]++;
                    if (!LaunchMissile(
                            handle,
                            missileIndex,
                            targetIndex,
                            targetOrdinal,
                            request.TargetAssignmentCounts[targetIndex]))
                    {
                        CancelCurrentHandle(handle, "MissileLaunchFailed", stopRoutine: false);
                        yield break;
                    }
                }

                if (volleyEnd < request.MissileCount)
                {
                    yield return new WaitForSeconds(launchInterval);
                }
            }

            CompleteCurrentHandle(handle);
        }
        finally
        {
            if (currentHandle == handle && handle.State == SalvoHandle.HandleState.Launching)
            {
                CancelCurrentHandle(handle, "SalvoRoutineAborted", stopRoutine: false);
            }
        }
    }

    private bool LaunchMissile(
        SalvoHandle handle,
        int missileIndex,
        int targetIndex,
        int targetOrdinal,
        int assignedMissileCount)
    {
        SalvoRequestSnapshot request = handle.Request;
        Transform launcher = SelectLauncher(missileIndex);
        if (launcher == null || request.MissileProfile == null)
        {
            return false;
        }

        SalvoTargetSnapshot target = request.Targets[targetIndex];
        Vector3 targetLocalOffset = MissileStrikeDistribution.GetLocalOffset(
            missileIndex,
            targetIndex,
            targetOrdinal,
            assignedMissileCount,
            request.RandomSeed,
            targetSpreadRadius,
            targetSpreadVerticalScale,
            targetSpreadDepth);
        SpecialMissileStrikePath strikePath = CreateStrikePath(
            launcher,
            missileIndex,
            request.RandomSeed,
            target,
            targetLocalOffset);
        Vector3 launchDirection = strikePath.FanOutDirection;

        SpecialMissilePool pool = EnsureMissilePool();
        if (pool == null || !pool.TryLeaseReserved(handle.Reservation, out SpecialHomingMissileController missile))
        {
            return false;
        }

        try
        {
            SalvoMissileProfileSnapshot profile = request.MissileProfile;
            missile.transform.position = launcher.position;
            missile.transform.rotation = Quaternion.LookRotation(launchDirection.normalized, Vector3.up);
            missile.Launch(
                battleController,
                target.Target,
                ProjectileTeam.Player,
                launchDirection,
                profile.LaunchSpeed,
                profile.CruiseSpeed,
                profile.Acceleration,
                profile.TurnRate,
                profile.LockOnDelay,
                profile.StraightPhaseDuration,
                profile.StraightPhaseDistance,
                profile.TurnPhaseDuration,
                profile.BoostPhaseDuration,
                profile.Lifetime,
                request.DamagePerMissile * target.DamageMultiplier,
                profile.HitRadius,
                profile.VisualTemplate,
                profile.SmokeTemplate,
                profile.ImpactEffectTemplate,
                profile.VisualTexture,
                profile.SmokeTexture,
                profile.VisualScale,
                profile.SmokeScale,
                profile.ImpactEffectScale,
                profile.UseTemplateOriginalMaterials,
                profile.TemplateTint,
                profile.TemplateLocalEulerAngles,
                criticalChanceOverride: 0f);
            missile.ConfigureStrikePath(strikePath);
        }
        catch (Exception exception)
        {
            Debug.LogException(exception, this);
            pool.Release(missile);
            return false;
        }
        try
        {
            MissileFired?.Invoke(handle.SalvoId, target);
        }
        catch (Exception exception)
        {
            Debug.LogException(exception, this);
        }

        return true;
    }

    private SpecialMissileStrikePath CreateStrikePath(
        Transform launcher,
        int missileIndex,
        int randomSeed,
        SalvoTargetSnapshot target,
        Vector3 targetLocalOffset)
    {
        Vector3 baseDirection = playerCombatController != null
            ? playerCombatController.GetMissileLaunchDirectionForSpecial()
            : launcher.forward;
        if (baseDirection.sqrMagnitude < 0.001f)
        {
            baseDirection = launcher.forward.sqrMagnitude > 0.001f ? launcher.forward : Vector3.forward;
        }

        baseDirection.Normalize();
        Camera mainCamera = Camera.main;
        Vector3 cameraRight = mainCamera != null ? mainCamera.transform.right : launcher.right;
        Vector3 cameraUp = mainCamera != null ? mainCamera.transform.up : Vector3.up;
        Vector3 cameraForward = mainCamera != null ? mainCamera.transform.forward : baseDirection;
        float sideSign = missileIndex % 2 == 0 ? -1f : 1f;
        float outwardAmount = Mathf.Lerp(
            0.58f,
            1f,
            MissileStrikeDistribution.Hash01(randomSeed, missileIndex, 0x31A7));
        float verticalAmount = MissileStrikeDistribution.HashSigned(randomSeed, missileIndex, 0x6E2B);
        Vector3 fanDirection =
            baseDirection * 0.45f +
            cameraRight * (sideSign * Mathf.Max(0f, fanOutHorizontal) * outwardAmount) +
            cameraUp * (verticalAmount * Mathf.Max(0f, fanOutVertical));
        if (fanDirection.sqrMagnitude < 0.001f)
        {
            fanDirection = baseDirection;
        }

        fanDirection.Normalize();
        Vector3 fanEndPosition = launcher.position + fanDirection * Mathf.Max(0f, fanOutDistance);
        Vector3 targetWorldPosition = target.Target != null
            ? target.Target.TransformPoint(targetLocalOffset)
            : target.TargetWorldPosition + targetLocalOffset;
        Vector3 approachVector = targetWorldPosition - fanEndPosition;
        Vector3 approachDirection = approachVector.sqrMagnitude > 0.001f
            ? approachVector.normalized
            : baseDirection;
        float entryDistance = Mathf.Min(
            Mathf.Max(0f, terminalEntryDistance),
            approachVector.magnitude * 0.55f);
        Vector3 terminalEntryPoint = targetWorldPosition - approachDirection * entryDistance;
        Vector3 arcMidPoint = Vector3.Lerp(fanEndPosition, terminalEntryPoint, 0.5f);
        float arcSideAmount = sideSign * Mathf.Lerp(
            0.55f,
            1f,
            MissileStrikeDistribution.Hash01(randomSeed, missileIndex, 0x19C3));
        float arcVerticalAmount = MissileStrikeDistribution.HashSigned(randomSeed, missileIndex, 0x52D1);
        float arcDepthAmount = MissileStrikeDistribution.HashSigned(randomSeed, missileIndex, 0x73A9);
        Vector3 arcControlPoint =
            arcMidPoint +
            cameraRight * (arcSideAmount * Mathf.Max(0f, arcHorizontalRadius)) +
            cameraUp * (arcVerticalAmount * Mathf.Max(0f, arcVerticalRadius)) +
            cameraForward * (arcDepthAmount * Mathf.Max(0f, arcHorizontalRadius) * 0.18f);
        float resolvedArcDuration = Mathf.Max(
            0.1f,
            arcDuration + MissileStrikeDistribution.HashSigned(
                randomSeed,
                missileIndex,
                0x4F1B) * Mathf.Abs(arcDurationJitter));

        return new SpecialMissileStrikePath
        {
            TargetAnchor = target.Target,
            TargetLocalOffset = targetLocalOffset,
            FanOutDirection = fanDirection,
            FanOutDuration = Mathf.Max(0.01f, fanOutDuration),
            FanOutDistance = Mathf.Max(0f, fanOutDistance),
            ArcControlPoint = arcControlPoint,
            TerminalEntryPoint = terminalEntryPoint,
            ArcDuration = resolvedArcDuration,
        };
    }

    private string ValidateRequest(SalvoRequest request)
    {
        string runtimeReason = ValidateRuntimeState();
        if (!string.IsNullOrEmpty(runtimeReason))
        {
            return runtimeReason;
        }

        if (request == null)
        {
            return "SalvoRequestNull";
        }

        if (request.MissileCount <= 0)
        {
            return "MissileCountInvalid";
        }

        if (request.MissileCount > MaxSalvoMissileCount)
        {
            return "MissileCountExceedsConfiguredMaximum";
        }

        if (request.MissilesPerVolley <= 0)
        {
            return "MissilesPerVolleyInvalid";
        }

        if (request.SalvoDuration < 0f)
        {
            return "SalvoDurationInvalid";
        }

        if (request.DamagePerMissile < 0f)
        {
            return "DamagePerMissileInvalid";
        }

        if (request.Targets == null || request.Targets.Count == 0)
        {
            return "NoSalvoTargets";
        }

        for (int i = 0; i < request.Targets.Count; i++)
        {
            SalvoTargetSnapshot target = request.Targets[i];
            if (target == null || target.Target == null || !target.Target.gameObject.activeInHierarchy)
            {
                return "SalvoTargetInvalid";
            }
        }

        return string.Empty;
    }

    private string ValidateRuntimeState()
    {
        if (!isActiveAndEnabled)
        {
            return "SalvoLauncherDisabled";
        }

        if (battleController == null || !battleController.IsBattleActive)
        {
            return "BattleInactive";
        }

        if (playerCombatController == null || !playerCombatController.IsAlive)
        {
            return "PlayerUnavailable";
        }

        if (playerCombatController.MissileLauncherLeft == null &&
            playerCombatController.MissileLauncherRight == null)
        {
            return "MissileLauncherUnavailable";
        }

        return string.Empty;
    }

    private SalvoRequestSnapshot CreateRequestSnapshot(SalvoRequest request)
    {
        SalvoTargetSnapshot[] targets = new SalvoTargetSnapshot[request.Targets.Count];
        for (int i = 0; i < targets.Length; i++)
        {
            SalvoTargetSnapshot target = request.Targets[i];
            targets[i] = new SalvoTargetSnapshot(
                target.Target,
                target.TargetId,
                target.WeakPointOpen,
                target.DamageMultiplier,
                target.TargetLossPolicy);
        }

        int resolvedSeed = request.RandomSeed != 0
            ? request.RandomSeed
            : unchecked((Time.frameCount * 397) ^ nextSalvoId ^ Environment.TickCount);
        int[] targetAssignment = CreateTargetAssignment(request.MissileCount, targets.Length, resolvedSeed);
        int[] targetAssignmentCounts = new int[targets.Length];
        for (int i = 0; i < targetAssignment.Length; i++)
        {
            targetAssignmentCounts[targetAssignment[i]]++;
        }

        return new SalvoRequestSnapshot
        {
            Source = request.Source,
            MissileCount = request.MissileCount,
            SuccessfulLockCount = request.SuccessfulLockCount,
            DamagePerMissile = request.DamagePerMissile,
            Targets = targets,
            MissilesPerVolley = request.MissilesPerVolley > 0
                ? request.MissilesPerVolley
                : DefaultMissilesPerVolley,
            SalvoDuration = request.SalvoDuration >= 0f
                ? request.SalvoDuration
                : DefaultSalvoDuration,
            RandomSeed = resolvedSeed,
            MissileProfile = request.MissileProfile ?? SalvoMissileProfileSnapshot.Capture(playerCombatController),
            TargetAssignment = targetAssignment,
            TargetAssignmentCounts = targetAssignmentCounts,
        };
    }

    private static int[] CreateTargetAssignment(int missileCount, int targetCount, int randomSeed)
    {
        int[] assignment = new int[missileCount];
        int[] cycle = new int[targetCount];
        System.Random random = new(randomSeed);
        int writeIndex = 0;

        while (writeIndex < missileCount)
        {
            for (int i = 0; i < targetCount; i++)
            {
                cycle[i] = i;
            }

            for (int i = targetCount - 1; i > 0; i--)
            {
                int swapIndex = random.Next(i + 1);
                (cycle[i], cycle[swapIndex]) = (cycle[swapIndex], cycle[i]);
            }

            for (int i = 0; i < targetCount && writeIndex < missileCount; i++)
            {
                assignment[writeIndex++] = cycle[i];
            }
        }

        return assignment;
    }

    private Transform SelectLauncher(int missileIndex)
    {
        Transform left = playerCombatController != null ? playerCombatController.MissileLauncherLeft : null;
        Transform right = playerCombatController != null ? playerCombatController.MissileLauncherRight : null;

        if (left != null && right != null)
        {
            return missileIndex % 2 == 0 ? left : right;
        }

        return left != null ? left : right;
    }

    private SpecialMissilePool EnsureMissilePool()
    {
        if (missilePool == null)
        {
            missilePool = GetComponentInChildren<SpecialMissilePool>(true);
            if (missilePool == null)
            {
                missilePool = SpecialMissilePool.Create(transform);
            }

            if (!missilePool.InitializeFixedCapacity(MissilePoolCapacity, MaxSalvoMissileCount))
            {
                Debug.LogError(
                    $"Failed to initialize fixed missile pool. Capacity={MissilePoolCapacity}, MaxRequest={MaxSalvoMissileCount}",
                    this);
                return null;
            }

            if (playerCombatController != null)
            {
                missilePool.PrewarmImpacts(
                    playerCombatController.DebugMissileImpactEffectTemplate,
                    MissilePoolCapacity);
            }
        }

        return missilePool;
    }

    private int NextSalvoId()
    {
        nextSalvoId = unchecked(nextSalvoId + 1);
        if (nextSalvoId <= 0)
        {
            nextSalvoId = 1;
        }

        return nextSalvoId;
    }

    private void CompleteCurrentHandle(SalvoHandle handle)
    {
        if (currentHandle != handle)
        {
            return;
        }

        ReleaseUnusedReservation(handle);
        handle.State = SalvoHandle.HandleState.Completed;
        activeRoutine = null;
        currentHandle = null;
        try
        {
            SalvoCompleted?.Invoke(handle.SalvoId);
        }
        catch (Exception exception)
        {
            Debug.LogException(exception, this);
        }
    }

    private void CancelCurrentHandle(
        SalvoHandle handle,
        string reason,
        bool stopRoutine = true)
    {
        if (handle == null || currentHandle != handle)
        {
            return;
        }

        Coroutine routineToStop = activeRoutine;
        ReleaseUnusedReservation(handle);
        handle.State = SalvoHandle.HandleState.Canceled;
        handle.LastReason = reason;
        currentHandle = null;
        activeRoutine = null;

        if (stopRoutine && routineToStop != null)
        {
            StopCoroutine(routineToStop);
        }

        try
        {
            SalvoCanceled?.Invoke(handle.SalvoId, reason);
        }
        catch (Exception exception)
        {
            Debug.LogException(exception, this);
        }
    }

    private void ReleaseUnusedReservation(SalvoHandle handle)
    {
        if (handle == null || handle.Reservation == null || missilePool == null)
        {
            return;
        }

        missilePool.ReleaseUnusedReservation(handle.Reservation, out _);
    }

    private void OnDisable()
    {
        if (currentHandle != null)
        {
            CancelCurrentHandle(currentHandle, "SalvoLauncherDisabled");
        }
    }

    private void OnDestroy()
    {
        if (missilePool != null)
        {
            missilePool.Dispose();
            missilePool = null;
        }
    }
}
