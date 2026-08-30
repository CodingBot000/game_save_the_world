using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using Random = System.Random;

[DisallowMultipleComponent]
[DefaultExecutionOrder(300)]
public sealed class BackgroundAllyArmyController : MonoBehaviour
{
    private enum FlightState
    {
        Patrol,
        Approach,
        AttackRun,
        BreakAway,
        Rejoin,
    }

    private enum UnitLifeState
    {
        Active,
        Crashing,
        RespawnWait,
    }

    private sealed class AirUnit
    {
        public Transform Root;
        public BackgroundAllyUnitView View;
        public int WingSide;
        public float HeightOffset;
        public float NoiseSeed;
        public Vector3 PositionVelocity;
        public Vector3 LastForward;
        public bool Initialized;
        public UnitLifeState LifeState;
        public float GatlingBurstRemaining;
        public float GatlingCooldownRemaining;
        public float GatlingShotDelay;
        public float MuzzleFlashRemaining;
        public float CrashElapsed;
        public float CrashDuration;
        public float RespawnRemaining;
        public Vector3 CrashVelocity;
        public Vector3 CrashSpinAxis;
        public float CrashSpinDegreesPerSecond;
        public float CrashAccumulatedRotation;
    }

    private sealed class FlightGroup
    {
        public readonly List<AirUnit> Units = new();
        public bool IsFormation;
        public float OrbitAngle;
        public float AngularSpeed;
        public float RadiusScale;
        public float DepthOffset;
        public float NoiseSeed;
        public FlightState State;
        public float StateElapsed;
        public float StateDuration;
        public float AttackSide;
        public Vector3 LeaderTarget;
        public Vector3 PreviousLeaderTarget;
        public bool HasPreviousLeaderTarget;
        public Vector3 AttackStart;
        public Vector3 ApproachTarget;
        public Vector3 RunTarget;
        public Vector3 BreakTarget;
        public int ShotCount;
        public int ShotsFired;
    }

    private sealed class TracerSlot
    {
        public LineRenderer Line;
        public float Remaining;
        public float Duration;
        public Color StartColor;
        public Color EndColor;
    }

    [Header("Runtime References")]
    [SerializeField] private GameObject chopperPrefab;
    [SerializeField] private Material tracerMaterial;
    [SerializeField] private Transform airRoot;
    [SerializeField] private Transform cosmeticVfxRoot;
    [SerializeField, HideInInspector] private int authoredConfigurationVersion;

    [Header("Air Composition")]
    [SerializeField, Range(1, 2)] private int soloCount = 1;
    [SerializeField, Range(2, 3)] private int formationSize = 3;
    [SerializeField, Min(0.01f)] private float visualScale = 0.9f;

    [Header("Patrol Orbit")]
    [SerializeField, Min(0.1f)] private float orbitRadiusX = 11f;
    [SerializeField, Min(0.1f)] private float orbitRadiusY = 5f;
    [SerializeField, Range(-4f, 12f)] private float orbitVerticalOffset = 5.5f;
    [SerializeField, Range(-6f, 10f)] private float depthBehindBoss = -2f;
    [SerializeField, Min(1f)] private float orbitPeriodSeconds = 28f;
    [SerializeField, Min(0f)] private float formationTrailDistance = 1.5f;
    [SerializeField, Min(0f)] private float formationLateralDistance = 0.85f;
    [SerializeField, Min(0.01f)] private float formationFollowSmoothTime = 0.28f;
    [SerializeField, Range(0f, 20f)] private float maximumBankDegrees = 8f;
    [SerializeField, Range(0f, 15f)] private float maximumPitchDegrees = 7f;
    [SerializeField, Min(0f)] private float rotationResponse = 5.5f;
    [SerializeField, Min(0f)] private float verticalNoiseAmplitude = 0.22f;
    [SerializeField, Min(0f)] private float lateralNoiseAmplitude = 0.12f;
    [SerializeField, Min(0f)] private float noiseFrequency = 0.18f;

    [Header("Rotor Blur")]
    [SerializeField] private float mainRotorDegreesPerSecond = 720f;
    [SerializeField] private float tailRotorDegreesPerSecond = 1080f;

    [Header("Cosmetic Attack")]
    [SerializeField] private bool enableCosmeticAttacks = true;
    [SerializeField] private Vector2 attackIntervalSeconds = new(9f, 16f);
    [SerializeField, Range(0f, 1f)] private float attackAttemptChance = 0.38f;
    [SerializeField] private Vector2 approachDurationSeconds = new(0.8f, 1.2f);
    [SerializeField] private Vector2 attackRunDurationSeconds = new(1f, 1.5f);
    [SerializeField] private Vector2 breakAwayDurationSeconds = new(1.5f, 2.5f);
    [SerializeField, Min(0.1f)] private float rejoinDurationSeconds = 1.8f;
    [SerializeField, Range(0.1f, 1f)] private float attackMotionSpeedScale = 0.5f;
    [SerializeField, Range(1, 6)] private int minimumShotsPerRun = 2;
    [SerializeField, Range(1, 8)] private int maximumShotsPerRun = 4;
    [SerializeField] private int randomSeed = 20260829;

    [Header("Cosmetic Tracers")]
    [SerializeField, Range(1, 32)] private int tracerPoolSize = 12;
    [SerializeField, Min(0.01f)] private float tracerLifetime = 0.18f;
    [SerializeField, Min(0.001f)] private float tracerStartWidth = 0.09f;
    [SerializeField, Min(0.001f)] private float tracerEndWidth = 0.025f;
    [SerializeField] private Color tracerStartColor = new(1f, 0.72f, 0.22f, 0.95f);
    [SerializeField] private Color tracerEndColor = new(1f, 0.42f, 0.08f, 0.14f);

    [Header("Patrol Gatling Muzzle Flash")]
    [SerializeField] private bool enablePatrolGatling = true;
    [SerializeField] private Vector2 gatlingBurstDurationSeconds = new(0.55f, 0.9f);
    [SerializeField] private Vector2 gatlingCooldownSeconds = new(1.25f, 2.2f);
    [SerializeField] private Vector2 gatlingShotIntervalSeconds = new(0.075f, 0.11f);
    [SerializeField, Min(0.01f)] private float muzzleFlashDurationSeconds = 0.045f;

    [Header("Random Cosmetic Crash")]
    [SerializeField] private bool enableRandomCrashes = true;
    [SerializeField] private Vector2 crashIntervalSeconds = new(20f, 34f);
    [SerializeField] private Vector2 crashDurationSeconds = new(3.2f, 4.5f);
    [SerializeField] private Vector2 crashRespawnDelaySeconds = new(4f, 7f);
    [SerializeField] private Vector2 crashForwardSpeed = new(0.6f, 1.2f);
    [SerializeField] private Vector2 crashInitialDownSpeed = new(0.5f, 1f);
    [SerializeField, Min(0.1f)] private float crashGravity = 2.4f;
    [SerializeField] private Vector2 crashSpinDegreesPerSecond = new(240f, 420f);

    private readonly List<FlightGroup> flightGroups = new();
    private readonly List<TracerSlot> tracerSlots = new();
    private BattleController battleController;
    private BossController bossController;
    private Camera baseCamera;
    private Transform stageVisualRoot;
    private BackgroundCosmeticCombatBudget combatBudget;
    private Random random;
    private FlightGroup activeAttackGroup;
    private AirUnit activeCrashUnit;
    private float nextAttackDelay;
    private float nextCrashDelay;
    private bool configured;
    private bool warnedMissingSetup;
    private int totalCosmeticShotsFired;
    private int totalGatlingFlashes;
    private int totalCrashesStarted;

    public int SpawnedAirUnitCount
    {
        get
        {
            int count = 0;
            for (int i = 0; i < flightGroups.Count; i++)
            {
                count += flightGroups[i].Units.Count;
            }

            return count;
        }
    }

    public int ActiveCosmeticAttackCount => activeAttackGroup != null ? 1 : 0;
    public int TotalCosmeticShotsFired => totalCosmeticShotsFired;
    public int TotalGatlingFlashes => totalGatlingFlashes;
    public int ActiveCrashCount => activeCrashUnit != null ? 1 : 0;
    public int TotalCrashesStarted => totalCrashesStarted;
    public float AttackMotionSpeedScale => attackMotionSpeedScale;
    public Transform ActiveCrashTransform => activeCrashUnit?.Root;
    public ParticleSystem ActiveCrashSmoke => activeCrashUnit?.View != null ? activeCrashUnit.View.CrashSmoke : null;
    public float ActiveCrashAccumulatedRotation => activeCrashUnit != null ? activeCrashUnit.CrashAccumulatedRotation : 0f;
    public Transform StageVisualRoot => stageVisualRoot;

    public int ActiveTracerCount
    {
        get
        {
            int count = 0;
            for (int i = 0; i < tracerSlots.Count; i++)
            {
                if (tracerSlots[i].Line != null && tracerSlots[i].Line.enabled)
                {
                    count++;
                }
            }

            return count;
        }
    }

    private void OnValidate()
    {
        soloCount = Mathf.Clamp(soloCount, 1, 2);
        formationSize = Mathf.Clamp(formationSize, 2, 3);
        visualScale = Mathf.Max(0.01f, visualScale);
        orbitRadiusX = Mathf.Max(0.1f, orbitRadiusX);
        orbitRadiusY = Mathf.Max(0.1f, orbitRadiusY);
        orbitVerticalOffset = Mathf.Clamp(orbitVerticalOffset, -4f, 12f);
        depthBehindBoss = Mathf.Clamp(depthBehindBoss, -6f, 10f);
        orbitPeriodSeconds = Mathf.Max(1f, orbitPeriodSeconds);
        formationFollowSmoothTime = Mathf.Max(0.01f, formationFollowSmoothTime);
        maximumPitchDegrees = Mathf.Clamp(maximumPitchDegrees, 0f, 15f);
        attackMotionSpeedScale = Mathf.Clamp(attackMotionSpeedScale, 0.1f, 1f);
        tracerPoolSize = Mathf.Clamp(tracerPoolSize, 1, 32);
        minimumShotsPerRun = Mathf.Clamp(minimumShotsPerRun, 1, 6);
        maximumShotsPerRun = Mathf.Clamp(maximumShotsPerRun, minimumShotsPerRun, 8);
        NormalizeRange(ref attackIntervalSeconds, 0.1f);
        NormalizeRange(ref approachDurationSeconds, 0.05f);
        NormalizeRange(ref attackRunDurationSeconds, 0.05f);
        NormalizeRange(ref breakAwayDurationSeconds, 0.05f);
        NormalizeRange(ref gatlingBurstDurationSeconds, 0.05f);
        NormalizeRange(ref gatlingCooldownSeconds, 0.05f);
        NormalizeRange(ref gatlingShotIntervalSeconds, 0.01f);
        NormalizeRange(ref crashIntervalSeconds, 0.1f);
        NormalizeRange(ref crashDurationSeconds, 0.1f);
        NormalizeRange(ref crashRespawnDelaySeconds, 0.1f);
        NormalizeRange(ref crashForwardSpeed, 0f);
        NormalizeRange(ref crashInitialDownSpeed, 0f);
        NormalizeRange(ref crashSpinDegreesPerSecond, 0f);
    }

    public void Configure(
        BattleController battle,
        BossController boss,
        Camera camera,
        Transform configuredStageVisualRoot)
    {
        battleController = battle;
        bossController = boss;
        baseCamera = camera != null ? camera : Camera.main;
        stageVisualRoot = configuredStageVisualRoot;
        combatBudget = GetComponent<BackgroundCosmeticCombatBudget>();

        if (chopperPrefab == null || bossController == null || baseCamera == null)
        {
            if (!warnedMissingSetup)
            {
                Debug.LogWarning(
                    "Background ally air army is disabled because its chopper prefab, boss, or Base camera is missing.",
                    this);
                warnedMissingSetup = true;
            }

            configured = false;
            return;
        }

        warnedMissingSetup = false;
        EnsureRoots();
        random = new Random(randomSeed);
        RebuildAirUnits();
        RebuildTracerPool();
        ScheduleNextAttack();
        ScheduleNextCrash();
        configured = true;
        SnapAllGroups();
    }

    public void ConfigureAssetsForEditor(
        GameObject configuredChopperPrefab,
        Material configuredTracerMaterial,
        Transform configuredAirRoot,
        Transform configuredVfxRoot)
    {
        chopperPrefab = configuredChopperPrefab;
        tracerMaterial = configuredTracerMaterial;
        airRoot = configuredAirRoot;
        cosmeticVfxRoot = configuredVfxRoot;
    }

    public void ApplyAuthoredDefaultsForEditorIfNeeded()
    {
        if (authoredConfigurationVersion >= 9)
        {
            return;
        }

        if (authoredConfigurationVersion == 0 || visualScale <= 0.3f)
        {
            visualScale = 0.55f;
        }

        if (authoredConfigurationVersion < 3 && Mathf.Abs(depthBehindBoss - 5f) <= 0.01f)
        {
            depthBehindBoss = 1.5f;
        }

        if (authoredConfigurationVersion < 4 && Mathf.Abs(depthBehindBoss - 1.5f) <= 0.01f)
        {
            depthBehindBoss = -2f;
        }

        if (authoredConfigurationVersion < 5 && Mathf.Abs(visualScale - 0.55f) <= 0.01f)
        {
            visualScale = 0.9f;
        }

        if (authoredConfigurationVersion < 6 && Mathf.Abs(orbitVerticalOffset) <= 0.01f)
        {
            orbitVerticalOffset = 5.5f;
        }

        if (authoredConfigurationVersion < 7 && Mathf.Abs(tracerLifetime - 0.13f) <= 0.01f)
        {
            tracerLifetime = 0.18f;
            tracerStartWidth = 0.09f;
            tracerEndWidth = 0.025f;
            tracerStartColor = new Color(1f, 0.72f, 0.22f, 0.95f);
            tracerEndColor = new Color(1f, 0.42f, 0.08f, 0.14f);
        }

        maximumPitchDegrees = Mathf.Clamp(maximumPitchDegrees, 0f, 7f);
        attackMotionSpeedScale = 0.5f;
        authoredConfigurationVersion = 9;
    }

    private void LateUpdate()
    {
        if (!configured || bossController == null || baseCamera == null)
        {
            return;
        }

        float deltaTime = Time.deltaTime;
        bool cosmeticCombatActive = deltaTime > 0f
                                    && bossController.IsAlive
                                    && (battleController == null || battleController.IsBattleActive);
        bool mayAttack = enableCosmeticAttacks && cosmeticCombatActive;

        UpdateAttackDirector(deltaTime, mayAttack);
        UpdateCrashDirector(deltaTime, cosmeticCombatActive);

        for (int i = 0; i < flightGroups.Count; i++)
        {
            UpdateFlightGroup(flightGroups[i], deltaTime, snap: false, cosmeticCombatActive);
        }

        UpdateTracerPool(deltaTime);
    }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
    [ContextMenu("Force Cosmetic Air Attack")]
    public void ForceCosmeticAirAttackForDebug()
    {
        if (!configured || activeAttackGroup != null || flightGroups.Count == 0)
        {
            return;
        }

        BeginAttack(flightGroups[0]);
    }

    [ContextMenu("Force Random Cosmetic Crash")]
    public void ForceRandomCrashForDebug()
    {
        if (!configured || activeCrashUnit != null)
        {
            return;
        }

        AirUnit candidate = FindCrashCandidate();
        if (candidate != null)
        {
            BeginCrash(candidate);
        }
    }
#endif

    private void EnsureRoots()
    {
        airRoot = EnsureChildRoot(airRoot, "AirRoot");
        cosmeticVfxRoot = EnsureChildRoot(cosmeticVfxRoot, "CosmeticVfxRoot");
    }

    private Transform EnsureChildRoot(Transform current, string childName)
    {
        if (current != null)
        {
            return current;
        }

        Transform existing = transform.Find(childName);
        if (existing != null)
        {
            return existing;
        }

        GameObject child = new(childName);
        child.transform.SetParent(transform, false);
        return child.transform;
    }

    private void RebuildAirUnits()
    {
        ClearAirUnits();

        float baseAngularSpeed = BackgroundAllyArmyMath.TwoPi / Mathf.Max(1f, orbitPeriodSeconds);
        for (int i = 0; i < soloCount; i++)
        {
            FlightGroup solo = new()
            {
                IsFormation = false,
                OrbitAngle = BackgroundAllyArmyMath.NormalizeRadians((0.2f + i * 0.47f) * BackgroundAllyArmyMath.TwoPi),
                AngularSpeed = baseAngularSpeed * (i % 2 == 0 ? -0.91f : 0.84f),
                RadiusScale = 1.08f + i * 0.08f,
                DepthOffset = 1.2f + i * 0.9f,
                NoiseSeed = NextFloat(0f, 100f),
                State = FlightState.Patrol,
            };
            solo.Units.Add(CreateAirUnit($"Solo_{i}", wingSide: 0, i));
            flightGroups.Add(solo);
        }

        FlightGroup formation = new()
        {
            IsFormation = true,
            OrbitAngle = 1.08f * Mathf.PI,
            AngularSpeed = baseAngularSpeed,
            RadiusScale = 1f,
            DepthOffset = 0f,
            NoiseSeed = NextFloat(0f, 100f),
            State = FlightState.Patrol,
        };
        formation.Units.Add(CreateAirUnit("Formation_Leader", wingSide: 0, 10));
        if (formationSize >= 2)
        {
            formation.Units.Add(CreateAirUnit("Formation_WingLeft", wingSide: -1, 11));
        }

        if (formationSize >= 3)
        {
            formation.Units.Add(CreateAirUnit("Formation_WingRight", wingSide: 1, 12));
        }

        flightGroups.Add(formation);
    }

    private AirUnit CreateAirUnit(string unitName, int wingSide, int seedOffset)
    {
        GameObject instance = Instantiate(chopperPrefab, airRoot);
        instance.name = unitName;
        instance.transform.localScale = Vector3.one * visualScale;
        BackgroundAllyUnitView view = instance.GetComponent<BackgroundAllyUnitView>();
        if (view == null)
        {
            view = instance.AddComponent<BackgroundAllyUnitView>();
        }

        return new AirUnit
        {
            Root = instance.transform,
            View = view,
            WingSide = wingSide,
            HeightOffset = wingSide == 0 ? 0f : NextFloat(-0.18f, 0.18f),
            NoiseSeed = NextFloat(0f, 100f) + seedOffset * 3.17f,
            LastForward = Vector3.forward,
            LifeState = UnitLifeState.Active,
            GatlingCooldownRemaining = NextFloat(0.1f, 1.2f),
        };
    }

    private void ClearAirUnits()
    {
        if (activeAttackGroup != null)
        {
            combatBudget?.ReleasePrimary(activeAttackGroup);
        }

        for (int groupIndex = 0; groupIndex < flightGroups.Count; groupIndex++)
        {
            List<AirUnit> units = flightGroups[groupIndex].Units;
            for (int unitIndex = 0; unitIndex < units.Count; unitIndex++)
            {
                if (units[unitIndex].Root != null)
                {
                    Destroy(units[unitIndex].Root.gameObject);
                }
            }
        }

        flightGroups.Clear();
        activeAttackGroup = null;
        activeCrashUnit = null;
    }

    private void SnapAllGroups()
    {
        for (int i = 0; i < flightGroups.Count; i++)
        {
            UpdateFlightGroup(flightGroups[i], 0f, snap: true, allowGatling: false);
        }
    }

    private void UpdateFlightGroup(FlightGroup group, float deltaTime, bool snap, bool allowGatling)
    {
        if (group == null || group.Units.Count == 0)
        {
            return;
        }

        if (!snap && deltaTime > 0f)
        {
            group.OrbitAngle = BackgroundAllyArmyMath.NormalizeRadians(group.OrbitAngle + group.AngularSpeed * deltaTime);
            UpdateFlightState(group, deltaTime);
        }

        Vector3 center = GetOrbitCenter(group);
        Vector3 orbitPosition = GetOrbitPosition(group, center);
        Vector3 orbitTangent = BackgroundAllyArmyMath.EvaluateOrbitTangent(
            baseCamera.transform.right,
            baseCamera.transform.up,
            orbitRadiusX * group.RadiusScale,
            orbitRadiusY * group.RadiusScale,
            group.OrbitAngle,
            Mathf.Sign(group.AngularSpeed));

        Vector3 leaderTarget = EvaluateLeaderTarget(group, orbitPosition);
        Vector3 leaderForward = group.HasPreviousLeaderTarget
            ? leaderTarget - group.PreviousLeaderTarget
            : orbitTangent;
        if (leaderForward.sqrMagnitude <= 0.000001f)
        {
            leaderForward = orbitTangent;
        }
        leaderForward.Normalize();

        group.LeaderTarget = leaderTarget;
        group.PreviousLeaderTarget = leaderTarget;
        group.HasPreviousLeaderTarget = true;

        Vector3 radial = Vector3.ProjectOnPlane(leaderTarget - center, baseCamera.transform.forward);
        if (radial.sqrMagnitude <= 0.000001f)
        {
            radial = baseCamera.transform.right;
        }
        radial.Normalize();

        for (int i = 0; i < group.Units.Count; i++)
        {
            AirUnit unit = group.Units[i];
            if (unit.LifeState != UnitLifeState.Active)
            {
                UpdateCrashedUnit(unit, deltaTime);
                continue;
            }

            Vector3 desiredPosition = i == 0
                ? leaderTarget
                : BackgroundAllyArmyMath.EvaluateFormationTarget(
                    leaderTarget,
                    leaderForward,
                    radial,
                    formationTrailDistance,
                    formationLateralDistance,
                    unit.WingSide);

            desiredPosition += baseCamera.transform.up * unit.HeightOffset;
            desiredPosition += EvaluateUnitNoise(unit, deltaTime);
            UpdateUnitPose(group, unit, desiredPosition, leaderForward, deltaTime, snap);
            UpdateUnitGatling(unit, deltaTime, allowGatling && !snap);
        }
    }

    private Vector3 GetOrbitCenter(FlightGroup group)
    {
        return bossController.HitPoint
               + baseCamera.transform.up * orbitVerticalOffset
               + baseCamera.transform.forward * (depthBehindBoss + group.DepthOffset);
    }

    private Vector3 GetOrbitPosition(FlightGroup group, Vector3 center)
    {
        return BackgroundAllyArmyMath.EvaluateOrbitPosition(
            center,
            baseCamera.transform.right,
            baseCamera.transform.up,
            orbitRadiusX * group.RadiusScale,
            orbitRadiusY * group.RadiusScale,
            group.OrbitAngle);
    }

    private Vector3 EvaluateLeaderTarget(FlightGroup group, Vector3 orbitPosition)
    {
        float normalized = group.StateDuration > 0f ? group.StateElapsed / group.StateDuration : 1f;
        float eased = BackgroundAllyArmyMath.Smooth01(normalized);
        return group.State switch
        {
            FlightState.Approach => Vector3.LerpUnclamped(group.AttackStart, group.ApproachTarget, eased),
            FlightState.AttackRun => Vector3.LerpUnclamped(group.ApproachTarget, group.RunTarget, eased),
            FlightState.BreakAway => Vector3.LerpUnclamped(group.RunTarget, group.BreakTarget, eased),
            FlightState.Rejoin => Vector3.LerpUnclamped(group.BreakTarget, orbitPosition, eased),
            _ => orbitPosition,
        };
    }

    private Vector3 EvaluateUnitNoise(AirUnit unit, float deltaTime)
    {
        if (deltaTime <= 0f || noiseFrequency <= 0f)
        {
            return Vector3.zero;
        }

        float time = Time.time * noiseFrequency;
        float vertical = (Mathf.PerlinNoise(unit.NoiseSeed, time) - 0.5f) * 2f * verticalNoiseAmplitude;
        float lateral = (Mathf.PerlinNoise(unit.NoiseSeed + 37.1f, time * 0.83f) - 0.5f) * 2f * lateralNoiseAmplitude;
        return baseCamera.transform.up * vertical + baseCamera.transform.right * lateral;
    }

    private void UpdateUnitPose(
        FlightGroup group,
        AirUnit unit,
        Vector3 desiredPosition,
        Vector3 desiredForward,
        float deltaTime,
        bool snap)
    {
        if (unit.Root == null)
        {
            return;
        }

        Vector3 previousPosition = unit.Root.position;
        Vector3 position;
        if (snap || !unit.Initialized || deltaTime <= 0f)
        {
            position = desiredPosition;
            unit.PositionVelocity = Vector3.zero;
        }
        else
        {
            float smoothTime = unit.WingSide == 0 ? formationFollowSmoothTime * 0.45f : formationFollowSmoothTime;
            position = Vector3.SmoothDamp(
                unit.Root.position,
                desiredPosition,
                ref unit.PositionVelocity,
                Mathf.Max(0.01f, smoothTime),
                Mathf.Infinity,
                deltaTime);
        }

        Vector3 actualMovement = position - previousPosition;
        Vector3 forward = !snap && actualMovement.sqrMagnitude > 0.000025f
            ? actualMovement.normalized
            : desiredForward.sqrMagnitude > 0.000001f
                ? desiredForward.normalized
                : unit.LastForward;
        if (forward.sqrMagnitude <= 0.000001f)
        {
            forward = baseCamera.transform.right;
        }

        float bankSign = group.State switch
        {
            FlightState.Approach => -group.AttackSide,
            FlightState.AttackRun => -group.AttackSide * 0.45f,
            FlightState.BreakAway => group.AttackSide,
            _ => -Mathf.Sign(group.AngularSpeed),
        };
        float bankDegrees = maximumBankDegrees * bankSign;
        Quaternion desiredRotation = BackgroundAllyArmyMath.EvaluateConstrainedFlightRotation(
            forward,
            unit.LastForward,
            Vector3.up,
            maximumPitchDegrees,
            bankDegrees);
        Vector3 currentPlanarForward = Vector3.ProjectOnPlane(unit.Root.forward, Vector3.up);
        Vector3 desiredPlanarForward = Vector3.ProjectOnPlane(forward, Vector3.up);
        float forwardAlignment = currentPlanarForward.sqrMagnitude > 0.000001f && desiredPlanarForward.sqrMagnitude > 0.000001f
            ? Vector3.Dot(currentPlanarForward.normalized, desiredPlanarForward.normalized)
            : 1f;
        float effectiveRotationResponse = forwardAlignment < 0.25f
            ? Mathf.Max(rotationResponse, 60f)
            : rotationResponse;
        Quaternion rotation = snap || !unit.Initialized || deltaTime <= 0f
            ? desiredRotation
            : Quaternion.Slerp(
                unit.Root.rotation,
                desiredRotation,
                BackgroundAllyArmyMath.ExponentialSmoothingFactor(effectiveRotationResponse, deltaTime));

        unit.Root.SetPositionAndRotation(position, rotation);
        unit.Root.localScale = Vector3.one * visualScale;
        unit.LastForward = rotation * Vector3.forward;
        unit.Initialized = true;
        unit.View?.TickRotors(deltaTime, mainRotorDegreesPerSecond, tailRotorDegreesPerSecond);
    }

    private void UpdateUnitGatling(AirUnit unit, float deltaTime, bool allowed)
    {
        if (unit?.View == null || deltaTime <= 0f)
        {
            return;
        }

        if (unit.MuzzleFlashRemaining > 0f)
        {
            unit.MuzzleFlashRemaining -= deltaTime;
            if (unit.MuzzleFlashRemaining <= 0f)
            {
                unit.View.SetMuzzleFlash(false);
            }
        }

        if (!enablePatrolGatling || !allowed || unit.LifeState != UnitLifeState.Active)
        {
            unit.GatlingBurstRemaining = 0f;
            unit.View.SetMuzzleFlash(false);
            return;
        }

        if (unit.GatlingBurstRemaining <= 0f)
        {
            unit.GatlingCooldownRemaining -= deltaTime;
            if (unit.GatlingCooldownRemaining > 0f)
            {
                return;
            }

            unit.GatlingBurstRemaining = NextFloat(gatlingBurstDurationSeconds);
            unit.GatlingShotDelay = 0f;
        }

        unit.GatlingBurstRemaining -= deltaTime;
        unit.GatlingShotDelay -= deltaTime;
        while (unit.GatlingBurstRemaining > 0f && unit.GatlingShotDelay <= 0f)
        {
            unit.View.SetMuzzleFlash(true);
            unit.MuzzleFlashRemaining = muzzleFlashDurationSeconds;
            unit.GatlingShotDelay += NextFloat(gatlingShotIntervalSeconds);
            totalGatlingFlashes++;
        }

        if (unit.GatlingBurstRemaining <= 0f)
        {
            unit.GatlingCooldownRemaining = NextFloat(gatlingCooldownSeconds);
        }
    }

    private void UpdateCrashDirector(float deltaTime, bool mayStartCrash)
    {
        if (!enableRandomCrashes || !mayStartCrash || deltaTime <= 0f || activeCrashUnit != null)
        {
            return;
        }

        nextCrashDelay -= deltaTime;
        if (nextCrashDelay > 0f)
        {
            return;
        }

        AirUnit candidate = FindCrashCandidate();
        if (candidate != null)
        {
            BeginCrash(candidate);
        }
        else
        {
            ScheduleNextCrash();
        }
    }

    private AirUnit FindCrashCandidate()
    {
        int activeCount = 0;
        for (int groupIndex = 0; groupIndex < flightGroups.Count; groupIndex++)
        {
            List<AirUnit> units = flightGroups[groupIndex].Units;
            for (int unitIndex = 0; unitIndex < units.Count; unitIndex++)
            {
                if (units[unitIndex].LifeState == UnitLifeState.Active)
                {
                    activeCount++;
                }
            }
        }

        if (activeCount == 0)
        {
            return null;
        }

        int selected = random.Next(0, activeCount);
        for (int groupIndex = 0; groupIndex < flightGroups.Count; groupIndex++)
        {
            List<AirUnit> units = flightGroups[groupIndex].Units;
            for (int unitIndex = 0; unitIndex < units.Count; unitIndex++)
            {
                AirUnit unit = units[unitIndex];
                if (unit.LifeState != UnitLifeState.Active)
                {
                    continue;
                }

                if (selected-- == 0)
                {
                    return unit;
                }
            }
        }

        return null;
    }

    private void BeginCrash(AirUnit unit)
    {
        if (unit == null || unit.Root == null || unit.LifeState != UnitLifeState.Active || activeCrashUnit != null)
        {
            return;
        }

        FlightGroup owner = FindGroupForUnit(unit);
        if (owner != null && activeAttackGroup == owner && owner.Units.Count > 0 && owner.Units[0] == unit)
        {
            CancelActiveAttack();
        }

        activeCrashUnit = unit;
        unit.LifeState = UnitLifeState.Crashing;
        unit.CrashElapsed = 0f;
        unit.CrashDuration = NextFloat(crashDurationSeconds);
        unit.CrashVelocity = unit.Root.forward * NextFloat(crashForwardSpeed)
                             + Vector3.down * NextFloat(crashInitialDownSpeed);
        unit.CrashSpinAxis = new Vector3(
            NextFloat(-0.35f, 0.35f),
            NextFloat(0.55f, 1f),
            NextFloat(0.45f, 1f)).normalized;
        unit.CrashSpinDegreesPerSecond = NextFloat(crashSpinDegreesPerSecond);
        unit.CrashAccumulatedRotation = 0f;
        unit.PositionVelocity = Vector3.zero;
        unit.GatlingBurstRemaining = 0f;
        unit.MuzzleFlashRemaining = 0f;
        unit.View?.SetMuzzleFlash(false);
        unit.View?.SetCrashSmoke(true);
        totalCrashesStarted++;
    }

    private void UpdateCrashedUnit(AirUnit unit, float deltaTime)
    {
        if (unit?.Root == null || deltaTime <= 0f)
        {
            return;
        }

        if (unit.LifeState == UnitLifeState.RespawnWait)
        {
            unit.RespawnRemaining -= deltaTime;
            if (unit.RespawnRemaining <= 0f)
            {
                unit.LifeState = UnitLifeState.Active;
                unit.Initialized = false;
                unit.PositionVelocity = Vector3.zero;
                unit.LastForward = Vector3.forward;
                unit.GatlingCooldownRemaining = NextFloat(0.4f, 1.4f);
                unit.View?.SetRenderEnabled(true);
                unit.View?.SetMuzzleFlash(false);
                unit.View?.SetCrashSmoke(false);
                if (activeCrashUnit == unit)
                {
                    activeCrashUnit = null;
                    ScheduleNextCrash();
                }
            }

            return;
        }

        unit.CrashElapsed += deltaTime;
        unit.CrashVelocity += Vector3.down * (crashGravity * deltaTime);
        unit.Root.position += unit.CrashVelocity * deltaTime;
        unit.Root.Rotate(unit.CrashSpinAxis, unit.CrashSpinDegreesPerSecond * deltaTime, Space.Self);
        unit.CrashAccumulatedRotation += Mathf.Abs(unit.CrashSpinDegreesPerSecond) * deltaTime;
        unit.View?.TickRotors(deltaTime, mainRotorDegreesPerSecond * 0.65f, tailRotorDegreesPerSecond * 0.65f);

        Vector3 viewport = baseCamera != null ? baseCamera.WorldToViewportPoint(unit.Root.position) : Vector3.one;
        bool belowArena = bossController != null && unit.Root.position.y <= bossController.HitPoint.y - 18f;
        if (unit.CrashElapsed >= unit.CrashDuration || viewport.y <= -0.15f || viewport.z <= 0f || belowArena)
        {
            unit.LifeState = UnitLifeState.RespawnWait;
            unit.RespawnRemaining = NextFloat(crashRespawnDelaySeconds);
            unit.View?.SetMuzzleFlash(false);
            unit.View?.SetCrashSmoke(false);
            unit.View?.SetRenderEnabled(false);
        }
    }

    private FlightGroup FindGroupForUnit(AirUnit unit)
    {
        for (int i = 0; i < flightGroups.Count; i++)
        {
            if (flightGroups[i].Units.Contains(unit))
            {
                return flightGroups[i];
            }
        }

        return null;
    }

    private void UpdateFlightState(FlightGroup group, float deltaTime)
    {
        if (group.State == FlightState.Patrol)
        {
            return;
        }

        group.StateElapsed += deltaTime;
        if (group.State == FlightState.AttackRun)
        {
            FireScheduledShots(group);
        }

        if (group.StateElapsed < group.StateDuration)
        {
            return;
        }

        switch (group.State)
        {
            case FlightState.Approach:
                EnterState(group, FlightState.AttackRun, ScaleAttackDuration(NextFloat(attackRunDurationSeconds)));
                break;
            case FlightState.AttackRun:
                FireRemainingShots(group);
                EnterState(group, FlightState.BreakAway, ScaleAttackDuration(NextFloat(breakAwayDurationSeconds)));
                break;
            case FlightState.BreakAway:
                EnterState(group, FlightState.Rejoin, ScaleAttackDuration(rejoinDurationSeconds));
                break;
            case FlightState.Rejoin:
                EnterState(group, FlightState.Patrol, 0f);
                if (activeAttackGroup == group)
                {
                    combatBudget?.ReleasePrimary(group);
                    activeAttackGroup = null;
                    ScheduleNextAttack();
                }
                break;
        }
    }

    private void UpdateAttackDirector(float deltaTime, bool mayAttack)
    {
        if (!mayAttack)
        {
            if (activeAttackGroup != null)
            {
                CancelActiveAttack();
            }

            return;
        }

        if (activeAttackGroup != null)
        {
            return;
        }

        nextAttackDelay -= deltaTime;
        if (nextAttackDelay > 0f)
        {
            return;
        }

        if (flightGroups.Count > 0 && NextFloat(0f, 1f) <= attackAttemptChance)
        {
            FlightGroup candidate = FindAttackCandidate();
            if (candidate != null)
            {
                BeginAttack(candidate);
                return;
            }
        }

        ScheduleNextAttack();
    }

    private void BeginAttack(FlightGroup group)
    {
        if (group == null || group.Units.Count == 0 || group.Units[0].LifeState != UnitLifeState.Active || activeAttackGroup != null)
        {
            return;
        }

        if (combatBudget != null && !combatBudget.TryAcquirePrimary(group))
        {
            nextAttackDelay = NextFloat(1.2f, 2.2f);
            return;
        }

        activeAttackGroup = group;
        group.AttackSide = Vector3.Dot(group.LeaderTarget - bossController.HitPoint, baseCamera.transform.right) >= 0f ? 1f : -1f;
        group.AttackStart = group.LeaderTarget;
        float attackDepth = depthBehindBoss >= 0f
            ? Mathf.Max(0.5f, depthBehindBoss * 0.42f)
            : Mathf.Min(-0.35f, depthBehindBoss * 0.42f);
        Vector3 attackCenter = bossController.HitPoint + baseCamera.transform.forward * attackDepth;
        group.ApproachTarget = attackCenter
                               + baseCamera.transform.right * (group.AttackSide * orbitRadiusX * 0.62f)
                               + baseCamera.transform.up * (orbitRadiusY * 0.28f);
        group.RunTarget = attackCenter
                          - baseCamera.transform.right * (group.AttackSide * orbitRadiusX * 0.62f)
                          + baseCamera.transform.up * NextFloat(-0.45f, 0.55f);
        group.BreakTarget = bossController.HitPoint
                            + baseCamera.transform.forward * (depthBehindBoss + 2f)
                            - baseCamera.transform.right * (group.AttackSide * orbitRadiusX * 1.08f)
                            + baseCamera.transform.up * (orbitRadiusY * 1.08f);
        group.ShotCount = random.Next(minimumShotsPerRun, maximumShotsPerRun + 1);
        group.ShotsFired = 0;
        EnterState(group, FlightState.Approach, ScaleAttackDuration(NextFloat(approachDurationSeconds)));
    }

    private FlightGroup FindAttackCandidate()
    {
        int count = 0;
        for (int i = 0; i < flightGroups.Count; i++)
        {
            if (flightGroups[i].Units.Count > 0 && flightGroups[i].Units[0].LifeState == UnitLifeState.Active)
            {
                count++;
            }
        }

        if (count == 0)
        {
            return null;
        }

        int selected = random.Next(0, count);
        for (int i = 0; i < flightGroups.Count; i++)
        {
            FlightGroup group = flightGroups[i];
            if (group.Units.Count == 0 || group.Units[0].LifeState != UnitLifeState.Active)
            {
                continue;
            }

            if (selected-- == 0)
            {
                return group;
            }
        }

        return null;
    }

    private float ScaleAttackDuration(float duration)
    {
        return Mathf.Max(0.05f, duration) / Mathf.Max(0.1f, attackMotionSpeedScale);
    }

    private void CancelActiveAttack()
    {
        FlightGroup group = activeAttackGroup;
        activeAttackGroup = null;
        combatBudget?.ReleasePrimary(group);
        if (group != null)
        {
            group.BreakTarget = group.LeaderTarget;
            group.ShotsFired = group.ShotCount;
            EnterState(group, FlightState.Rejoin, Mathf.Min(1.8f, ScaleAttackDuration(rejoinDurationSeconds)));
        }

        DisableAllTracers();
        ScheduleNextAttack();
    }

    private static void EnterState(FlightGroup group, FlightState state, float duration)
    {
        group.State = state;
        group.StateElapsed = 0f;
        group.StateDuration = Mathf.Max(0f, duration);
    }

    private void FireScheduledShots(FlightGroup group)
    {
        if (group.ShotCount <= 0 || group.Units.Count == 0)
        {
            return;
        }

        float normalized = group.StateDuration > 0f ? Mathf.Clamp01(group.StateElapsed / group.StateDuration) : 1f;
        while (group.ShotsFired < group.ShotCount)
        {
            float threshold = (group.ShotsFired + 1f) / (group.ShotCount + 1f);
            if (normalized < threshold)
            {
                break;
            }

            FireCosmeticTracer(group.Units[0]);
            group.ShotsFired++;
        }
    }

    private void FireRemainingShots(FlightGroup group)
    {
        while (group.ShotsFired < group.ShotCount)
        {
            FireCosmeticTracer(group.Units[0]);
            group.ShotsFired++;
        }
    }

    private void RebuildTracerPool()
    {
        for (int i = 0; i < tracerSlots.Count; i++)
        {
            if (tracerSlots[i].Line != null)
            {
                Destroy(tracerSlots[i].Line.gameObject);
            }
        }

        tracerSlots.Clear();
        if (tracerMaterial == null || cosmeticVfxRoot == null)
        {
            return;
        }

        for (int i = 0; i < tracerPoolSize; i++)
        {
            GameObject tracerObject = new($"AirTracer_{i:00}");
            tracerObject.transform.SetParent(cosmeticVfxRoot, false);
            LineRenderer line = tracerObject.AddComponent<LineRenderer>();
            line.sharedMaterial = tracerMaterial;
            line.useWorldSpace = true;
            line.positionCount = 2;
            line.alignment = LineAlignment.View;
            line.textureMode = LineTextureMode.Stretch;
            line.numCapVertices = 2;
            line.startWidth = tracerStartWidth;
            line.endWidth = tracerEndWidth;
            line.shadowCastingMode = ShadowCastingMode.Off;
            line.receiveShadows = false;
            line.enabled = false;
            tracerSlots.Add(new TracerSlot { Line = line });
        }
    }

    private void FireCosmeticTracer(AirUnit sourceUnit)
    {
        if (sourceUnit?.View == null || sourceUnit.LifeState != UnitLifeState.Active || bossController == null || baseCamera == null)
        {
            return;
        }

        TracerSlot slot = FindAvailableTracer();
        if (slot == null)
        {
            return;
        }

        Vector3 start = sourceUnit.View.Muzzle.position;
        Vector3 target = bossController.HitPoint
                         + baseCamera.transform.right * NextFloat(-0.8f, 0.8f)
                         + baseCamera.transform.up * NextFloat(-0.35f, 0.65f)
                         + baseCamera.transform.forward * 0.35f;
        slot.Duration = Mathf.Max(0.01f, tracerLifetime);
        slot.Remaining = slot.Duration;
        slot.StartColor = tracerStartColor;
        slot.EndColor = tracerEndColor;
        slot.Line.startWidth = tracerStartWidth;
        slot.Line.endWidth = tracerEndWidth;
        slot.Line.startColor = tracerStartColor;
        slot.Line.endColor = tracerEndColor;
        slot.Line.SetPosition(0, start);
        slot.Line.SetPosition(1, target);
        slot.Line.enabled = true;
        totalCosmeticShotsFired++;
    }

    private TracerSlot FindAvailableTracer()
    {
        for (int i = 0; i < tracerSlots.Count; i++)
        {
            if (tracerSlots[i].Line != null && !tracerSlots[i].Line.enabled)
            {
                return tracerSlots[i];
            }
        }

        return null;
    }

    private void UpdateTracerPool(float deltaTime)
    {
        if (deltaTime <= 0f)
        {
            return;
        }

        for (int i = 0; i < tracerSlots.Count; i++)
        {
            TracerSlot slot = tracerSlots[i];
            if (slot.Line == null || !slot.Line.enabled)
            {
                continue;
            }

            slot.Remaining -= deltaTime;
            if (slot.Remaining <= 0f)
            {
                slot.Line.enabled = false;
                continue;
            }

            float alpha = Mathf.Clamp01(slot.Remaining / slot.Duration);
            Color start = slot.StartColor;
            Color end = slot.EndColor;
            start.a *= alpha;
            end.a *= alpha;
            slot.Line.startColor = start;
            slot.Line.endColor = end;
        }
    }

    private void DisableAllTracers()
    {
        for (int i = 0; i < tracerSlots.Count; i++)
        {
            if (tracerSlots[i].Line != null)
            {
                tracerSlots[i].Line.enabled = false;
            }
        }
    }

    private void ScheduleNextAttack()
    {
        nextAttackDelay = NextFloat(attackIntervalSeconds);
    }

    private void ScheduleNextCrash()
    {
        nextCrashDelay = NextFloat(crashIntervalSeconds);
    }

    private float NextFloat(Vector2 range)
    {
        return NextFloat(range.x, range.y);
    }

    private float NextFloat(float minimum, float maximum)
    {
        if (random == null)
        {
            random = new Random(randomSeed);
        }

        float min = Mathf.Min(minimum, maximum);
        float max = Mathf.Max(minimum, maximum);
        return Mathf.Lerp(min, max, (float)random.NextDouble());
    }

    private static void NormalizeRange(ref Vector2 range, float minimum)
    {
        float min = Mathf.Max(minimum, Mathf.Min(range.x, range.y));
        float max = Mathf.Max(min, Mathf.Max(range.x, range.y));
        range = new Vector2(min, max);
    }

    private void OnDisable()
    {
        if (activeAttackGroup != null)
        {
            combatBudget?.ReleasePrimary(activeAttackGroup);
            activeAttackGroup = null;
        }

        DisableAllTracers();
        for (int groupIndex = 0; groupIndex < flightGroups.Count; groupIndex++)
        {
            List<AirUnit> units = flightGroups[groupIndex].Units;
            for (int unitIndex = 0; unitIndex < units.Count; unitIndex++)
            {
                units[unitIndex].View?.SetMuzzleFlash(false);
                units[unitIndex].View?.SetCrashSmoke(false);
            }
        }
    }
}
