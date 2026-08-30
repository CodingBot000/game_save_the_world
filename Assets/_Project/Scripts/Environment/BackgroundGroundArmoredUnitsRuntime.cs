using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using Random = System.Random;

[DisallowMultipleComponent]
[DefaultExecutionOrder(310)]
public sealed class BackgroundGroundArmoredUnitsRuntime : MonoBehaviour
{
    private enum UnitKind
    {
        Tank,
        GatlingCarrier,
        MortarCarrier,
    }

    private enum UnitState
    {
        Cruise,
        Braking,
        Aiming,
        Firing,
        Recovering,
        Accelerating,
    }

    private sealed class GroundUnit
    {
        public Transform Root;
        public BackgroundGroundUnitView View;
        public UnitKind Kind;
        public UnitState State;
        public int RouteIndex;
        public int FormationLeaderIndex = -1;
        public float FormationDistanceOffset;
        public float Distance;
        public float CruiseSpeed;
        public float CurrentSpeed;
        public float Acceleration;
        public float BrakeAcceleration;
        public float RotationResponse;
        public float AimSpeed;
        public float AimNoiseSeed;
        public float StateRemaining;
        public float FireDelay;
        public int ShotsRemaining;
        public float AmbientCooldown;
        public float AmbientRemaining;
        public float AmbientShotDelay;
        public int AmbientShotsRemaining;
        public float MuzzleFlashRemaining;
        public float MuzzleFlashDuration;
        public float MuzzleFlashPulseSeed;
        public Quaternion LastRotation = Quaternion.identity;
        public bool Initialized;
    }

    private sealed class TracerSlot
    {
        public LineRenderer Line;
        public float Remaining;
        public float Duration;
        public Color StartColor;
        public Color EndColor;
    }

    private sealed class ArcSlot
    {
        public LineRenderer Line;
        public float Remaining;
        public float Duration;
        public Vector3 Start;
        public Vector3 Target;
        public float Height;
    }

    private sealed class ExplosionSlot
    {
        public LineRenderer Line;
        public float Remaining;
        public float Duration;
        public Vector3 Center;
        public float MaximumRadius;
    }

    private static readonly Vector3[][] RouteControlPoints =
    {
        new[]
        {
            new Vector3(-13f, 0.12f, -4.5f), new Vector3(-8f, 0.12f, 7.5f),
            new Vector3(1f, 0.12f, 10.5f), new Vector3(12.5f, 0.12f, 5f),
            new Vector3(12f, 0.12f, -6.5f), new Vector3(1f, 0.12f, -10.5f),
        },
        new[]
        {
            new Vector3(-10.5f, 0.16f, -8.5f), new Vector3(-13.5f, 0.16f, 2f),
            new Vector3(-5f, 0.16f, 9f), new Vector3(7.5f, 0.16f, 8f),
            new Vector3(13f, 0.16f, -1.5f), new Vector3(5f, 0.16f, -9.5f),
        },
        new[]
        {
            new Vector3(-15f, 0.2f, 1f), new Vector3(-7.5f, 0.2f, 11f),
            new Vector3(5.5f, 0.2f, 11.5f), new Vector3(15f, 0.2f, 2.5f),
            new Vector3(8f, 0.2f, -11f), new Vector3(-6f, 0.2f, -12f),
        },
    };

    [Header("Runtime References")]
    [SerializeField] private GameObject tankPrefab;
    [SerializeField] private GameObject gatlingCarrierPrefab;
    [SerializeField] private GameObject mortarCarrierPrefab;
    [SerializeField] private Material tracerMaterial;
    [SerializeField] private Material mortarTrailMaterial;
    [SerializeField] private Material explosionMaterial;
    [SerializeField] private Transform groundUnitsRoot;
    [SerializeField] private Transform cosmeticVfxRoot;
    [SerializeField, HideInInspector] private int authoredConfigurationVersion;

    [Header("Composition")]
    [SerializeField] private bool enableGroundArmoredUnits = true;
    [SerializeField] private bool enableTank = true;
    [SerializeField] private bool enableGatlingCarrier = true;
    [SerializeField] private bool enableMortarCarrier = true;
    [SerializeField] private bool enableFormationMovement = true;
    [SerializeField, Range(0.1f, 4f)] private float visualScale = 2f;

    [Header("Movement")]
    [SerializeField, Range(0.1f, 3f)] private float globalSpeedScale = 1f;
    [SerializeField, Range(1f, 20f)] private float routeRotationResponse = 7f;
    [SerializeField] private int randomSeed = 20260830;

    [Header("Cosmetic Combat")]
    [SerializeField] private bool enableGroundCosmeticAttacks = true;
    [SerializeField] private Vector2 primaryAttackIntervalSeconds = new(8f, 14f);
    [SerializeField] private Vector2 ambientCooldownSeconds = new(2.5f, 5.5f);
    [SerializeField, Range(1, 32)] private int tracerPoolSize = 16;
    [SerializeField, Range(1, 12)] private int mortarArcPoolSize = 6;
    [SerializeField, Range(1, 16)] private int explosionPoolSize = 8;

    private readonly List<GroundUnit> units = new();
    private readonly List<TracerSlot> tracerSlots = new();
    private readonly List<ArcSlot> arcSlots = new();
    private readonly List<ExplosionSlot> explosionSlots = new();
    private BackgroundGroundRoute[] routes;
    private BackgroundCosmeticCombatBudget combatBudget;
    private BattleController battleController;
    private BossController bossController;
    private Camera baseCamera;
    private Transform stageVisualRoot;
    private Transform runtimeVfxPoolRoot;
    private Random random;
    private GroundUnit activePrimaryUnit;
    private float nextPrimaryDelay;
    private bool configured;
    private bool warnedMissingSetup;
    private int totalCosmeticShots;
    private int totalMuzzleFlashes;
    private int totalPrimaryAttacks;

    public int SpawnedUnitCount => units.Count;
    public int ActivePrimaryAttackCount => activePrimaryUnit != null ? 1 : 0;
    public int ActiveAmbientAttackCount => combatBudget != null ? combatBudget.AmbientOwnerCount : 0;
    public int TotalCosmeticShots => totalCosmeticShots;
    public int TotalMuzzleFlashes => totalMuzzleFlashes;
    public int TotalPrimaryAttacks => totalPrimaryAttacks;
    public Transform StageVisualRoot => stageVisualRoot;

    public int VisibleMuzzleFlashCount
    {
        get
        {
            int count = 0;
            for (int i = 0; i < units.Count; i++)
            {
                if (units[i].View != null && units[i].View.IsMuzzleFlashVisible)
                {
                    count++;
                }
            }

            return count;
        }
    }

    public int ActiveVfxCount
    {
        get
        {
            int count = 0;
            for (int i = 0; i < tracerSlots.Count; i++) count += tracerSlots[i].Line != null && tracerSlots[i].Line.enabled ? 1 : 0;
            for (int i = 0; i < arcSlots.Count; i++) count += arcSlots[i].Line != null && arcSlots[i].Line.enabled ? 1 : 0;
            for (int i = 0; i < explosionSlots.Count; i++) count += explosionSlots[i].Line != null && explosionSlots[i].Line.enabled ? 1 : 0;
            return count;
        }
    }

    private void OnValidate()
    {
        visualScale = Mathf.Clamp(visualScale, 0.1f, 4f);
        globalSpeedScale = Mathf.Clamp(globalSpeedScale, 0.1f, 3f);
        routeRotationResponse = Mathf.Clamp(routeRotationResponse, 1f, 20f);
        tracerPoolSize = Mathf.Clamp(tracerPoolSize, 1, 32);
        mortarArcPoolSize = Mathf.Clamp(mortarArcPoolSize, 1, 12);
        explosionPoolSize = Mathf.Clamp(explosionPoolSize, 1, 16);
        NormalizeRange(ref primaryAttackIntervalSeconds, 0.1f);
        NormalizeRange(ref ambientCooldownSeconds, 0.1f);
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
        stageVisualRoot = configuredStageVisualRoot != null
            ? configuredStageVisualRoot
            : FindSceneTransform("StageVisualRoot");
        combatBudget = GetComponentInParent<BackgroundCosmeticCombatBudget>();

        if (!enableGroundArmoredUnits || stageVisualRoot == null || bossController == null
            || tankPrefab == null || gatlingCarrierPrefab == null || mortarCarrierPrefab == null)
        {
            if (!warnedMissingSetup)
            {
                Debug.LogWarning(
                    $"Background ground armored units are disabled. enabled={enableGroundArmoredUnits} " +
                    $"stage={stageVisualRoot != null} boss={bossController != null} " +
                    $"tank={tankPrefab != null} gatling={gatlingCarrierPrefab != null} mortar={mortarCarrierPrefab != null}.",
                    this);
                warnedMissingSetup = true;
            }

            configured = false;
            return;
        }

        warnedMissingSetup = false;
        EnsureRoots();
        random = new Random(randomSeed);
        BuildRoutes();
        RebuildUnits();
        RebuildVfxPools();
        nextPrimaryDelay = NextFloat(primaryAttackIntervalSeconds);
        configured = true;
        SnapAllUnits();
    }

    public void ConfigureAssetsForEditor(
        GameObject configuredTankPrefab,
        GameObject configuredGatlingPrefab,
        GameObject configuredMortarPrefab,
        Material configuredTracerMaterial,
        Material configuredMortarTrailMaterial,
        Material configuredExplosionMaterial,
        Transform configuredUnitsRoot,
        Transform configuredVfxRoot)
    {
        tankPrefab = configuredTankPrefab;
        gatlingCarrierPrefab = configuredGatlingPrefab;
        mortarCarrierPrefab = configuredMortarPrefab;
        tracerMaterial = configuredTracerMaterial;
        mortarTrailMaterial = configuredMortarTrailMaterial;
        explosionMaterial = configuredExplosionMaterial;
        groundUnitsRoot = configuredUnitsRoot;
        cosmeticVfxRoot = configuredVfxRoot;
    }

    public void ApplyAuthoredDefaultsForEditorIfNeeded()
    {
        if (authoredConfigurationVersion >= 1)
        {
            return;
        }

        enableGroundArmoredUnits = true;
        enableGroundCosmeticAttacks = true;
        enableFormationMovement = true;
        visualScale = 2f;
        authoredConfigurationVersion = 1;
    }

    private void LateUpdate()
    {
        if (!configured || !enableGroundArmoredUnits || stageVisualRoot == null || bossController == null)
        {
            return;
        }

        float deltaTime = Time.deltaTime;
        bool combatActive = deltaTime > 0f && bossController.IsAlive
                            && (battleController == null || battleController.IsBattleActive);
        UpdatePrimaryDirector(deltaTime, enableGroundCosmeticAttacks && combatActive);

        for (int i = 0; i < units.Count; i++)
        {
            UpdateUnit(i, deltaTime, combatActive);
        }

        UpdateVfx(deltaTime);
    }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
    [ContextMenu("Force Ground Primary Attack")]
    public void ForceGroundPrimaryAttackForDebug()
    {
        if (configured && activePrimaryUnit == null)
        {
            TryBeginPrimaryAttack(FindPrimaryCandidate());
        }
    }

    [ContextMenu("Force Ground Muzzle Flash")]
    public void ForceGroundMuzzleFlashForDebug()
    {
        if (!configured || units.Count == 0)
        {
            return;
        }

        GroundUnit unit = activePrimaryUnit ?? FindPrimaryCandidate() ?? units[0];
        FireUnit(unit, primary: false);
    }
#endif

    private void EnsureRoots()
    {
        groundUnitsRoot = EnsureChildRoot(groundUnitsRoot, "GroundUnitsRoot");
        cosmeticVfxRoot = EnsureChildRoot(cosmeticVfxRoot, "GroundCosmeticVfxRoot");
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

    private void BuildRoutes()
    {
        routes = new BackgroundGroundRoute[RouteControlPoints.Length];
        for (int i = 0; i < routes.Length; i++)
        {
            routes[i] = new BackgroundGroundRoute(RouteControlPoints[i], 16);
        }
    }

    private void RebuildUnits()
    {
        for (int i = 0; i < units.Count; i++)
        {
            if (units[i].Root != null)
            {
                Destroy(units[i].Root.gameObject);
            }
        }

        units.Clear();
        activePrimaryUnit = null;

        int columnLeader = AddUnit("ArmoredColumn_A_Leader", UnitKind.Tank, 0, 0.08f, -1, 0f, 0);
        AddUnit("ArmoredColumn_A_WingTank", UnitKind.Tank, 0, 0.08f, columnLeader, 1.45f, 1);
        AddUnit("ArmoredColumn_A_Gatling", UnitKind.GatlingCarrier, 0, 0.08f, columnLeader, 2.95f, 2);
        AddUnit("FireSupportPair_B_Gatling", UnitKind.GatlingCarrier, 1, 0.57f, -1, 0f, 3);
        AddUnit("FireSupportPair_B_Mortar", UnitKind.MortarCarrier, 1, 0.45f, -1, 0f, 4);
        AddUnit("Independent_Tank", UnitKind.Tank, 2, 0.21f, -1, 0f, 5);
        AddUnit("Independent_Gatling", UnitKind.GatlingCarrier, 2, 0.68f, -1, 0f, 6);
        AddUnit("Independent_Mortar", UnitKind.MortarCarrier, 1, 0.88f, -1, 0f, 7);
    }

    private int AddUnit(
        string unitName,
        UnitKind kind,
        int routeIndex,
        float normalizedPhase,
        int formationLeaderIndex,
        float formationDistanceOffset,
        int seedOffset)
    {
        if (!IsKindEnabled(kind))
        {
            return -1;
        }

        GameObject prefab = GetPrefab(kind);
        GameObject instance = Instantiate(prefab, groundUnitsRoot);
        instance.name = unitName;
        instance.transform.localScale = Vector3.one * visualScale;
        BackgroundGroundUnitView view = instance.GetComponent<BackgroundGroundUnitView>();
        if (view == null)
        {
            view = instance.AddComponent<BackgroundGroundUnitView>();
        }

        float speed = kind switch
        {
            UnitKind.Tank => 1.45f,
            UnitKind.GatlingCarrier => 1.62f,
            _ => 1.18f,
        };
        speed *= NextFloat(0.88f, 1.12f);

        units.Add(new GroundUnit
        {
            Root = instance.transform,
            View = view,
            Kind = kind,
            State = UnitState.Cruise,
            RouteIndex = Mathf.Clamp(routeIndex, 0, routes.Length - 1),
            FormationLeaderIndex = enableFormationMovement ? formationLeaderIndex : -1,
            FormationDistanceOffset = formationDistanceOffset,
            Distance = routes[Mathf.Clamp(routeIndex, 0, routes.Length - 1)].TotalLength * normalizedPhase,
            CruiseSpeed = speed,
            CurrentSpeed = speed,
            Acceleration = kind == UnitKind.MortarCarrier ? 0.7f : 1.05f,
            BrakeAcceleration = kind == UnitKind.MortarCarrier ? 1.1f : 1.55f,
            RotationResponse = routeRotationResponse * NextFloat(0.85f, 1.12f),
            AimSpeed = kind == UnitKind.MortarCarrier ? 32f : kind == UnitKind.Tank ? 55f : 82f,
            AimNoiseSeed = NextFloat(0f, 100f) + seedOffset * 2.17f,
            AmbientCooldown = NextFloat(ambientCooldownSeconds) + seedOffset * 0.17f,
        });
        return units.Count - 1;
    }

    private void SnapAllUnits()
    {
        for (int i = 0; i < units.Count; i++)
        {
            UpdateUnitPose(i, 0f, true);
        }
    }

    private void UpdateUnit(int index, float deltaTime, bool combatActive)
    {
        GroundUnit unit = units[index];
        UpdateMuzzleFlash(unit, deltaTime);

        if (unit.FormationLeaderIndex >= 0 && unit.FormationLeaderIndex < units.Count)
        {
            GroundUnit leader = units[unit.FormationLeaderIndex];
            unit.Distance = BackgroundGroundRoute.WrapDistance(
                leader.Distance - unit.FormationDistanceOffset,
                routes[unit.RouteIndex].TotalLength);
            unit.CurrentSpeed = leader.CurrentSpeed;
        }
        else
        {
            UpdatePrimaryState(unit, deltaTime);
            unit.Distance = BackgroundGroundRoute.WrapDistance(
                unit.Distance + unit.CurrentSpeed * globalSpeedScale * deltaTime,
                routes[unit.RouteIndex].TotalLength);
        }

        UpdateAmbientFire(unit, deltaTime, combatActive);
        UpdateUnitPose(index, deltaTime, false);

        if (combatActive && unit.View != null)
        {
            unit.View.AimAt(GetAimTarget(unit), unit.AimSpeed, deltaTime);
        }
        else
        {
            unit.View?.ResetAim(unit.AimSpeed, deltaTime);
        }
    }

    private void UpdateUnitPose(int index, float deltaTime, bool snap)
    {
        GroundUnit unit = units[index];
        BackgroundGroundRoute route = routes[unit.RouteIndex];
        route.Sample(unit.Distance, out Vector3 localPosition, out Vector3 localTangent);
        Vector3 worldPosition = stageVisualRoot.TransformPoint(localPosition);
        Vector3 worldForward = stageVisualRoot.TransformDirection(localTangent);
        Vector3 worldUp = stageVisualRoot.TransformDirection(Vector3.up);
        Quaternion desiredRotation = Quaternion.LookRotation(worldForward, worldUp);
        Quaternion rotation = snap || !unit.Initialized || deltaTime <= 0f
            ? desiredRotation
            : Quaternion.Slerp(
                unit.LastRotation,
                desiredRotation,
                BackgroundAllyArmyMath.ExponentialSmoothingFactor(unit.RotationResponse, deltaTime));

        unit.Root.SetPositionAndRotation(worldPosition, rotation);
        unit.Root.localScale = Vector3.one * visualScale;
        unit.LastRotation = rotation;
        unit.Initialized = true;
    }

    private void UpdatePrimaryDirector(float deltaTime, bool mayAttack)
    {
        if (!mayAttack)
        {
            CancelPrimaryAttack();
            return;
        }

        if (activePrimaryUnit != null)
        {
            return;
        }

        nextPrimaryDelay -= deltaTime;
        if (nextPrimaryDelay > 0f)
        {
            return;
        }

        if (!TryBeginPrimaryAttack(FindPrimaryCandidate()))
        {
            nextPrimaryDelay = NextFloat(1.2f, 2.2f);
        }
    }

    private GroundUnit FindPrimaryCandidate()
    {
        int eligible = 0;
        for (int i = 0; i < units.Count; i++)
        {
            if (units[i].FormationLeaderIndex < 0 && units[i].State == UnitState.Cruise)
            {
                eligible++;
            }
        }

        if (eligible == 0)
        {
            return null;
        }

        int selected = random.Next(0, eligible);
        for (int i = 0; i < units.Count; i++)
        {
            GroundUnit unit = units[i];
            if (unit.FormationLeaderIndex >= 0 || unit.State != UnitState.Cruise)
            {
                continue;
            }

            if (selected-- == 0)
            {
                return unit;
            }
        }

        return null;
    }

    private bool TryBeginPrimaryAttack(GroundUnit unit)
    {
        if (unit == null || activePrimaryUnit != null || (combatBudget != null && !combatBudget.TryAcquirePrimary(unit)))
        {
            return false;
        }

        activePrimaryUnit = unit;
        unit.State = UnitState.Braking;
        unit.ShotsRemaining = unit.Kind switch
        {
            UnitKind.Tank => random.Next(1, 3),
            UnitKind.GatlingCarrier => 3,
            _ => random.Next(1, 4),
        };
        unit.FireDelay = 0f;
        totalPrimaryAttacks++;
        return true;
    }

    private void UpdatePrimaryState(GroundUnit unit, float deltaTime)
    {
        switch (unit.State)
        {
            case UnitState.Cruise:
                unit.CurrentSpeed = Mathf.MoveTowards(unit.CurrentSpeed, unit.CruiseSpeed, unit.Acceleration * deltaTime);
                break;
            case UnitState.Braking:
                unit.CurrentSpeed = Mathf.MoveTowards(unit.CurrentSpeed, 0f, unit.BrakeAcceleration * deltaTime);
                if (unit.CurrentSpeed <= 0.02f)
                {
                    unit.CurrentSpeed = 0f;
                    unit.State = UnitState.Aiming;
                    unit.StateRemaining = unit.Kind == UnitKind.MortarCarrier ? NextFloat(0.8f, 1.6f) : NextFloat(0.35f, 0.75f);
                }
                break;
            case UnitState.Aiming:
                unit.StateRemaining -= deltaTime;
                if (unit.StateRemaining <= 0f)
                {
                    unit.State = UnitState.Firing;
                    unit.FireDelay = 0f;
                }
                break;
            case UnitState.Firing:
                unit.FireDelay -= deltaTime;
                if (unit.ShotsRemaining > 0 && unit.FireDelay <= 0f)
                {
                    FireUnit(unit, primary: true);
                    unit.ShotsRemaining--;
                    unit.FireDelay = unit.Kind switch
                    {
                        UnitKind.Tank => NextFloat(0.58f, 0.82f),
                        UnitKind.GatlingCarrier => NextFloat(0.22f, 0.34f),
                        _ => NextFloat(0.35f, 0.8f),
                    };
                }

                if (unit.ShotsRemaining <= 0 && unit.FireDelay <= 0f)
                {
                    unit.State = UnitState.Recovering;
                    unit.StateRemaining = unit.Kind == UnitKind.MortarCarrier ? NextFloat(0.8f, 1.5f) : NextFloat(0.45f, 0.9f);
                }
                break;
            case UnitState.Recovering:
                unit.StateRemaining -= deltaTime;
                if (unit.StateRemaining <= 0f)
                {
                    unit.State = UnitState.Accelerating;
                    ReleasePrimary(unit);
                }
                break;
            case UnitState.Accelerating:
                unit.CurrentSpeed = Mathf.MoveTowards(unit.CurrentSpeed, unit.CruiseSpeed, unit.Acceleration * deltaTime);
                if (Mathf.Abs(unit.CurrentSpeed - unit.CruiseSpeed) <= 0.01f)
                {
                    unit.State = UnitState.Cruise;
                }
                break;
        }
    }

    private void UpdateAmbientFire(GroundUnit unit, float deltaTime, bool combatActive)
    {
        if (unit.AmbientRemaining > 0f)
        {
            if (!combatActive || !enableGroundCosmeticAttacks)
            {
                EndAmbient(unit, stopMuzzleFlash: true);
                return;
            }

            unit.AmbientRemaining -= deltaTime;
            unit.AmbientShotDelay -= deltaTime;
            if (unit.AmbientShotsRemaining > 0 && unit.AmbientShotDelay <= 0f)
            {
                FireUnit(unit, primary: false);
                unit.AmbientShotsRemaining--;
                unit.AmbientShotDelay = unit.Kind == UnitKind.GatlingCarrier ? NextFloat(0.12f, 0.2f) : 0.2f;
            }

            if (unit.AmbientRemaining <= 0f || unit.AmbientShotsRemaining <= 0)
            {
                EndAmbient(unit);
            }

            return;
        }

        if (!combatActive || !enableGroundCosmeticAttacks || unit.State != UnitState.Cruise
            || unit.Kind == UnitKind.MortarCarrier || unit == activePrimaryUnit)
        {
            return;
        }

        unit.AmbientCooldown -= deltaTime;
        if (unit.AmbientCooldown > 0f)
        {
            return;
        }

        if (combatBudget != null && !combatBudget.TryAcquireAmbient(unit))
        {
            unit.AmbientCooldown = NextFloat(0.5f, 1.2f);
            return;
        }

        unit.AmbientRemaining = unit.Kind == UnitKind.GatlingCarrier ? NextFloat(0.35f, 0.8f) : 0.18f;
        unit.AmbientShotsRemaining = unit.Kind == UnitKind.GatlingCarrier ? random.Next(1, 4) : 1;
        unit.AmbientShotDelay = 0f;
    }

    private void EndAmbient(GroundUnit unit, bool stopMuzzleFlash = false)
    {
        unit.AmbientRemaining = 0f;
        unit.AmbientShotsRemaining = 0;
        unit.AmbientCooldown = NextFloat(ambientCooldownSeconds);
        combatBudget?.ReleaseAmbient(unit);
        if (stopMuzzleFlash)
        {
            unit.MuzzleFlashRemaining = 0f;
            unit.View?.SetMuzzleFlash(false);
        }
    }

    private void FireUnit(GroundUnit unit, bool primary)
    {
        if (unit?.View == null)
        {
            return;
        }

        Transform muzzle = unit.View.Muzzle;
        Vector3 target = GetShotTarget(unit.Kind);
        unit.View.SetMuzzleFlash(true);
        unit.MuzzleFlashDuration = unit.Kind switch
        {
            UnitKind.GatlingCarrier => NextFloat(0.075f, 0.105f),
            UnitKind.MortarCarrier => NextFloat(0.14f, 0.19f),
            _ => NextFloat(0.12f, 0.16f),
        };
        unit.MuzzleFlashRemaining = unit.MuzzleFlashDuration;
        unit.MuzzleFlashPulseSeed = NextFloat(0f, Mathf.PI * 2f);
        totalCosmeticShots++;
        totalMuzzleFlashes++;

        if (unit.Kind == UnitKind.MortarCarrier)
        {
            ActivateMortarArc(muzzle.position, GetGroundImpactTarget(), NextFloat(2.8f, 5.2f), NextFloat(0.75f, 1.15f));
            return;
        }

        ActivateTracer(muzzle.position, target, unit.Kind == UnitKind.GatlingCarrier ? 0.055f : 0.095f);
        if (primary && unit.Kind == UnitKind.Tank)
        {
            ActivateExplosion(target, 0.62f, 0.34f);
        }
    }

    private Vector3 GetAimTarget(GroundUnit unit)
    {
        Vector3 target = bossController != null ? bossController.HitPoint : stageVisualRoot.position;
        if (baseCamera != null)
        {
            float time = Time.time * 0.22f + unit.AimNoiseSeed;
            target += baseCamera.transform.right * (Mathf.Sin(time) * 1.15f);
            target += baseCamera.transform.up * (unit.Kind == UnitKind.MortarCarrier
                ? 2.1f + Mathf.Sin(time * 0.71f) * 0.55f
                : 0.25f + Mathf.Sin(time * 0.83f) * 0.5f);
        }

        return target;
    }

    private Vector3 GetShotTarget(UnitKind kind)
    {
        Vector3 target = bossController != null ? bossController.HitPoint : stageVisualRoot.position;
        if (baseCamera != null)
        {
            target += baseCamera.transform.right * NextFloat(-1.5f, 1.5f);
            target += baseCamera.transform.up * (kind == UnitKind.MortarCarrier ? NextFloat(1.4f, 3f) : NextFloat(-0.25f, 0.85f));
        }

        return target;
    }

    private Vector3 GetGroundImpactTarget()
    {
        Vector3 local = new(NextFloat(-3.8f, 3.8f), 0.16f, NextFloat(-3.8f, 3.8f));
        return stageVisualRoot.TransformPoint(local);
    }

    private void UpdateMuzzleFlash(GroundUnit unit, float deltaTime)
    {
        if (unit.MuzzleFlashRemaining <= 0f)
        {
            return;
        }

        unit.MuzzleFlashRemaining -= deltaTime;
        if (unit.MuzzleFlashRemaining <= 0f)
        {
            unit.View?.SetMuzzleFlash(false);
            return;
        }

        unit.View?.TickMuzzleFlash(unit.MuzzleFlashRemaining / Mathf.Max(0.0001f, unit.MuzzleFlashDuration), unit.MuzzleFlashPulseSeed);
    }

    private void RebuildVfxPools()
    {
        ClearVfxPools();
        GameObject poolObject = new("RuntimeGroundVfxPool");
        runtimeVfxPoolRoot = poolObject.transform;
        runtimeVfxPoolRoot.SetParent(cosmeticVfxRoot, false);

        for (int i = 0; i < tracerPoolSize; i++)
        {
            tracerSlots.Add(new TracerSlot
            {
                Line = CreateLine($"GroundTracer_{i:00}", tracerMaterial, 2, false, 0.06f),
            });
        }

        for (int i = 0; i < mortarArcPoolSize; i++)
        {
            arcSlots.Add(new ArcSlot
            {
                Line = CreateLine($"MortarArc_{i:00}", mortarTrailMaterial, 10, false, 0.07f),
            });
        }

        for (int i = 0; i < explosionPoolSize; i++)
        {
            explosionSlots.Add(new ExplosionSlot
            {
                Line = CreateLine($"GroundExplosion_{i:00}", explosionMaterial, 16, true, 0.08f),
            });
        }
    }

    private LineRenderer CreateLine(string name, Material material, int positions, bool loop, float width)
    {
        GameObject lineObject = new(name);
        lineObject.transform.SetParent(runtimeVfxPoolRoot, false);
        LineRenderer line = lineObject.AddComponent<LineRenderer>();
        line.sharedMaterial = material;
        line.useWorldSpace = true;
        line.positionCount = positions;
        line.loop = loop;
        line.startWidth = width;
        line.endWidth = width * 0.35f;
        line.numCornerVertices = 1;
        line.numCapVertices = 1;
        line.textureMode = LineTextureMode.Stretch;
        line.shadowCastingMode = ShadowCastingMode.Off;
        line.receiveShadows = false;
        line.lightProbeUsage = LightProbeUsage.Off;
        line.reflectionProbeUsage = ReflectionProbeUsage.Off;
        line.motionVectorGenerationMode = MotionVectorGenerationMode.ForceNoMotion;
        line.enabled = false;
        return line;
    }

    private void ActivateTracer(Vector3 start, Vector3 end, float width)
    {
        for (int i = 0; i < tracerSlots.Count; i++)
        {
            TracerSlot slot = tracerSlots[i];
            if (slot.Line.enabled)
            {
                continue;
            }

            slot.Duration = 0.16f;
            slot.Remaining = slot.Duration;
            slot.StartColor = new Color(1f, 0.56f, 0.18f, 0.72f);
            slot.EndColor = new Color(1f, 0.28f, 0.06f, 0.06f);
            slot.Line.startWidth = width;
            slot.Line.endWidth = width * 0.25f;
            slot.Line.startColor = slot.StartColor;
            slot.Line.endColor = slot.EndColor;
            slot.Line.SetPosition(0, start);
            slot.Line.SetPosition(1, end);
            slot.Line.enabled = true;
            return;
        }
    }

    private void ActivateMortarArc(Vector3 start, Vector3 target, float height, float duration)
    {
        for (int i = 0; i < arcSlots.Count; i++)
        {
            ArcSlot slot = arcSlots[i];
            if (slot.Line.enabled)
            {
                continue;
            }

            slot.Start = start;
            slot.Target = target;
            slot.Height = height;
            slot.Duration = duration;
            slot.Remaining = duration;
            slot.Line.startColor = new Color(1f, 0.48f, 0.14f, 0.58f);
            slot.Line.endColor = new Color(0.92f, 0.25f, 0.06f, 0.04f);
            SetArcPositions(slot, 0f);
            slot.Line.enabled = true;
            return;
        }
    }

    private void SetArcPositions(ArcSlot slot, float progress)
    {
        int count = slot.Line.positionCount;
        float visibleProgress = Mathf.Clamp01(progress);
        for (int i = 0; i < count; i++)
        {
            float t = count > 1 ? i / (float)(count - 1) : 0f;
            float shown = Mathf.Min(t, visibleProgress);
            Vector3 point = Vector3.LerpUnclamped(slot.Start, slot.Target, shown);
            point += Vector3.up * (Mathf.Sin(shown * Mathf.PI) * slot.Height);
            slot.Line.SetPosition(i, point);
        }
    }

    private void ActivateExplosion(Vector3 center, float radius, float duration)
    {
        for (int i = 0; i < explosionSlots.Count; i++)
        {
            ExplosionSlot slot = explosionSlots[i];
            if (slot.Line.enabled)
            {
                continue;
            }

            slot.Center = center;
            slot.MaximumRadius = radius;
            slot.Duration = duration;
            slot.Remaining = duration;
            slot.Line.enabled = true;
            SetExplosionPositions(slot, 0f);
            return;
        }
    }

    private void SetExplosionPositions(ExplosionSlot slot, float progress)
    {
        int count = slot.Line.positionCount;
        float radius = slot.MaximumRadius * BackgroundAllyArmyMath.Smooth01(progress);
        Vector3 right = baseCamera != null ? baseCamera.transform.right : Vector3.right;
        Vector3 up = baseCamera != null ? baseCamera.transform.up : Vector3.up;
        for (int i = 0; i < count; i++)
        {
            float angle = i / (float)count * BackgroundAllyArmyMath.TwoPi;
            slot.Line.SetPosition(i, slot.Center + right * (Mathf.Cos(angle) * radius) + up * (Mathf.Sin(angle) * radius));
        }
    }

    private void UpdateVfx(float deltaTime)
    {
        for (int i = 0; i < tracerSlots.Count; i++)
        {
            TracerSlot slot = tracerSlots[i];
            if (!slot.Line.enabled) continue;
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

        for (int i = 0; i < arcSlots.Count; i++)
        {
            ArcSlot slot = arcSlots[i];
            if (!slot.Line.enabled) continue;
            slot.Remaining -= deltaTime;
            float progress = 1f - Mathf.Clamp01(slot.Remaining / slot.Duration);
            SetArcPositions(slot, progress);
            if (slot.Remaining <= 0f)
            {
                slot.Line.enabled = false;
                ActivateExplosion(slot.Target, 0.8f, 0.42f);
            }
        }

        for (int i = 0; i < explosionSlots.Count; i++)
        {
            ExplosionSlot slot = explosionSlots[i];
            if (!slot.Line.enabled) continue;
            slot.Remaining -= deltaTime;
            float progress = 1f - Mathf.Clamp01(slot.Remaining / slot.Duration);
            SetExplosionPositions(slot, progress);
            float alpha = 1f - progress;
            Color color = new(1f, 0.35f, 0.08f, 0.48f * alpha);
            slot.Line.startColor = color;
            slot.Line.endColor = color;
            if (slot.Remaining <= 0f)
            {
                slot.Line.enabled = false;
            }
        }
    }

    private void ClearVfxPools()
    {
        tracerSlots.Clear();
        arcSlots.Clear();
        explosionSlots.Clear();
        if (runtimeVfxPoolRoot != null)
        {
            Destroy(runtimeVfxPoolRoot.gameObject);
            runtimeVfxPoolRoot = null;
        }
    }

    private void DisableAllVfx()
    {
        for (int i = 0; i < tracerSlots.Count; i++) if (tracerSlots[i].Line != null) tracerSlots[i].Line.enabled = false;
        for (int i = 0; i < arcSlots.Count; i++) if (arcSlots[i].Line != null) arcSlots[i].Line.enabled = false;
        for (int i = 0; i < explosionSlots.Count; i++) if (explosionSlots[i].Line != null) explosionSlots[i].Line.enabled = false;
    }

    private void CancelPrimaryAttack()
    {
        if (activePrimaryUnit == null)
        {
            return;
        }

        GroundUnit unit = activePrimaryUnit;
        unit.ShotsRemaining = 0;
        unit.State = UnitState.Accelerating;
        unit.View?.SetMuzzleFlash(false);
        ReleasePrimary(unit);
        DisableAllVfx();
    }

    private void ReleasePrimary(GroundUnit unit)
    {
        combatBudget?.ReleasePrimary(unit);
        if (activePrimaryUnit == unit)
        {
            activePrimaryUnit = null;
            nextPrimaryDelay = NextFloat(primaryAttackIntervalSeconds);
        }
    }

    private bool IsKindEnabled(UnitKind kind)
    {
        return kind switch
        {
            UnitKind.Tank => enableTank,
            UnitKind.GatlingCarrier => enableGatlingCarrier,
            _ => enableMortarCarrier,
        };
    }

    private GameObject GetPrefab(UnitKind kind)
    {
        return kind switch
        {
            UnitKind.Tank => tankPrefab,
            UnitKind.GatlingCarrier => gatlingCarrierPrefab,
            _ => mortarCarrierPrefab,
        };
    }

    private float NextFloat(Vector2 range)
    {
        return NextFloat(range.x, range.y);
    }

    private float NextFloat(float minimum, float maximum)
    {
        random ??= new Random(randomSeed);
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

    private Transform FindSceneTransform(string objectName)
    {
        Scene scene = gameObject.scene;
        if (!scene.IsValid())
        {
            return null;
        }

        GameObject[] roots = scene.GetRootGameObjects();
        for (int rootIndex = 0; rootIndex < roots.Length; rootIndex++)
        {
            Transform[] descendants = roots[rootIndex].GetComponentsInChildren<Transform>(true);
            for (int i = 0; i < descendants.Length; i++)
            {
                if (descendants[i].name == objectName)
                {
                    return descendants[i];
                }
            }
        }

        return null;
    }

    private void OnDisable()
    {
        CancelPrimaryAttack();
        for (int i = 0; i < units.Count; i++)
        {
            EndAmbient(units[i], stopMuzzleFlash: true);
            units[i].View?.SetMuzzleFlash(false);
        }

        DisableAllVfx();
    }

    private void OnDrawGizmosSelected()
    {
        Transform basis = stageVisualRoot;
        if (basis == null)
        {
            return;
        }

        Color[] colors = { new Color(0.3f, 0.9f, 0.35f), new Color(0.25f, 0.7f, 1f), new Color(1f, 0.65f, 0.2f) };
        for (int route = 0; route < RouteControlPoints.Length; route++)
        {
            Gizmos.color = colors[route % colors.Length];
            Vector3[] points = RouteControlPoints[route];
            for (int i = 0; i < points.Length; i++)
            {
                Vector3 from = basis.TransformPoint(points[i]);
                Vector3 to = basis.TransformPoint(points[(i + 1) % points.Length]);
                Gizmos.DrawLine(from, to);
                Gizmos.DrawSphere(from, 0.12f);
            }
        }
    }
}
