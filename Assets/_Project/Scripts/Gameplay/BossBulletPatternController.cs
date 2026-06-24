using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public enum BossBulletPatternType
{
    FanSpread,
    AimedBurst,
    SpiralRing,
    FallingBomb,
    SplitShot,
    DebrisThrow,
    LaserSweep,
    ShockwaveRing,
    InterruptibleHeavyShot,
    DebrisSalvo,
    AcceleratingSweepBeam,
    TrackingResidualBeam,
    DebrisFragmentScatter,
}

public enum BossBulletPatternSet
{
    LegacyBulletHell,
    KaijuHeavyThreats,
}

[System.Serializable]
public sealed class BossBulletPatternDefinition
{
    public string displayName = "Pattern";
    public BossBulletPatternType patternType = BossBulletPatternType.FanSpread;
    public bool enabled = true;
    [Range(0f, 1f)] public float minHealthRatio = 0f;
    [Range(0f, 1f)] public float maxHealthRatio = 1f;
    [Min(0.1f)] public float cooldownMultiplier = 1f;
    [Min(1)] public int projectileCount = 1;
    [Min(1)] public int secondaryProjectileCount = 1;
    [Min(1)] public int burstCount = 1;
    [Min(0f)] public float burstInterval = 0.25f;
    [Min(0f)] public float spreadAngle = 30f;
    [Min(0.1f)] public float speedMultiplier = 1f;
    [Min(0.1f)] public float secondarySpeedMultiplier = 1f;
    [Min(0.1f)] public float damageMultiplier = 1f;
    [Min(0.1f)] public float secondaryDamageMultiplier = 1f;
    [Min(0f)] public float ringRotationStep = 18f;
    [Min(0.05f)] public float telegraphDuration = 0.35f;
    [Min(0.05f)] public float flashingDuration = 0.4f;
    [Min(0.1f)] public float warningWidth = 2.6f;
    [Min(0.1f)] public float warningHeight = 10f;
    [Min(0.1f)] public float warningDepth = 1.3f;
    [Min(0.1f)] public float overheadHeight = 12f;
    [Min(0.1f)] public float splitDistance = 11f;
    [Min(0.1f)] public float projectileScale = 1f;
    [Min(0.05f)] public float activeDuration = 1f;
    [Min(0.1f)] public float hazardRadius = 16f;
    [Min(0.1f)] public float hazardThickness = 1.4f;
    [Min(0f)] public float interruptDamageThreshold = 120f;
    [Min(0f)] public float safeRadius = 2.5f;
    [Min(0f)] public float minimumSpacing = 4f;
    [Min(0.05f)] public float fixedDuration = 0.5f;
    [Min(0.05f)] public float slowDuration = 0.4f;
    [Min(0.05f)] public float fastDuration = 0.5f;
    [Min(0.05f)] public float trackingDuration = 3f;
    [Min(0f)] public float trackingTurnRate = 28f;
    [Min(0f)] public float beamWarmupTrackingSpeedMultiplier = 0.5f;
    [Min(0f)] public float beamActiveTrackingSpeedMultiplier = 0.4f;
    [Min(0f)] public float aimJitterPlayerScale = 2f;
    [Min(0.01f)] public float approachStartScale = 0.25f;
    [Min(0.01f)] public float approachEndScale = 0.7f;
    [Min(0.01f)] public float approachInitialSpeedMultiplier = 0.5f;
    [Min(0.05f)] public float approachFlightDuration = 1f;
}

public class BossBulletPatternController : MonoBehaviour
{
    private const string DebrisFragmentCatalogResourcePath = "VFX/MonsterDebrisFragmentCatalog";
    private const string DebrisFragmentFirePointName1 = "BossFootDebrisFirePoint1";
    private const string DebrisFragmentFirePointName2 = "BossFootDebrisFirePoint2";
    private const int DebrisFragmentBatchTableCount = 10;
    private const int DebrisFragmentClusterMinCount = 3;
    private const int DebrisFragmentClusterMaxCount = 4;
    private const float TrackingResidualBeamDamagePerTick = 3f;
    private const float TrackingResidualBeamDamageTickInterval = 0.2f;

    [SerializeField] private float startupDelay = 1f;
    [SerializeField] private float aimedBurstShotInterval = 0.14f;
    // Thin line to mimic a weapon laser sight rather than a chunky warning beam.
    [SerializeField] private float warningLineThickness = 0.045f;
    [SerializeField] private BossBulletPatternSet activePatternSet = BossBulletPatternSet.KaijuHeavyThreats;
    [SerializeField, Min(0.1f)] private float attackSizeMultiplier = 2.5f;
    [SerializeField, Min(0.01f)] private float minimumTelegraphThickness = 0.3f;
    [SerializeField, Min(1)] private int debrisFragmentStompCount = 2;
    [SerializeField, Min(0f)] private float debrisFragmentStompDuration = 0.2f;
    [SerializeField, Min(0f)] private float debrisFragmentJumpHeightRatio = 0.1f;
    [SerializeField, Min(0f)] private float debrisFragmentStompShakeDuration = 0.1f;
    [SerializeField, Min(0f)] private float debrisFragmentStompShakeAmplitude = 0.24f;
    [SerializeField] private DebrisFragmentCatalog debrisFragmentCatalog;
    // Keep pattern fields data-shaped so later balancing can move out to ScriptableObjects
    // or per-boss assets without rewriting the actual spawn/execution code.
    [SerializeField] private List<BossBulletPatternDefinition> patternSequence = new();
    [SerializeField] private List<BossBulletPatternDefinition> kaijuHeavyThreatSequence = new();

    private readonly List<GameObject> runtimeTelegraphs = new();
    private readonly List<Transform> debrisFragmentFirePoints = new();

    private BossAttackController attackController;
    private BattleController battleController;
    private BossController bossController;
    private PlayerCombatController playerCombatController;
    private PlayerOrbitController playerOrbitController;
    private Coroutine activePatternRoutine;
    private Material telegraphMaterialTemplate;
    private float attackCooldownRemaining;
    private int nextPatternIndex;
    private float spiralRotationDegrees;
    private bool preserveActiveTelegraphUntilPatternEnds;
    private bool cinematicPaused;
    private int[][] debrisFragmentBatchIndices;
    private int debrisFragmentBatchSourceCount;
    private int debrisFragmentBatchMinCount;
    private int debrisFragmentBatchMaxCount;
    private int nextDebrisFragmentBatchIndex;
    private Transform activeDebrisJumpTransform;
    private float activeDebrisJumpBaseY;
    private bool hasActiveDebrisJump;
    private Coroutine activeDebrisCameraShakeRoutine;
    private Transform activeDebrisCameraShakeTransform;
    private Vector3 activeDebrisCameraShakeBaseLocalPosition;

    public float DebugStartupDelay => startupDelay;
    public float DebugAimedBurstShotInterval => aimedBurstShotInterval;
    public float DebugWarningLineThickness => warningLineThickness;
    public BossBulletPatternSet DebugPatternSet => activePatternSet;
    public float DebugAttackSizeMultiplier => attackSizeMultiplier;
    public float DebugMinimumTelegraphThickness => minimumTelegraphThickness;
    public IReadOnlyList<BossBulletPatternDefinition> DebugPatternSequence
    {
        get
        {
            EnsureDefaultPatterns();
            return GetActivePatternSequence();
        }
    }

    private void Reset()
    {
        EnsureDefaultPatterns();
    }

    private void Awake()
    {
        EnsureDefaultPatterns();
        EnsureDebrisFragmentBatches(7, 10);
        attackCooldownRemaining = startupDelay;
    }

    private void OnDisable()
    {
        CancelActivePattern();
        CleanupTelegraphs();
    }

    public void Configure(
        BossAttackController attack,
        BattleController battle,
        BossController boss,
        PlayerCombatController player,
        PlayerOrbitController playerOrbit = null)
    {
        attackController = attack;
        battleController = battle;
        bossController = boss;
        playerCombatController = player;
        playerOrbitController = playerOrbit != null ? playerOrbit : FindAnyObjectByType<PlayerOrbitController>();
        CacheDebrisFragmentFirePoints();
        attackCooldownRemaining = Mathf.Max(attackCooldownRemaining, startupDelay);
    }

    public void SetCinematicPaused(bool paused)
    {
        if (cinematicPaused == paused)
        {
            return;
        }

        cinematicPaused = paused;
        if (paused)
        {
            CancelActivePattern();
            CleanupTelegraphs();
        }
    }

    public void SetTimingForDebug(
        float initialStartupDelay,
        float fallbackAimedBurstShotInterval,
        float telegraphLineThickness,
        float sizeMultiplier,
        float minTelegraphThickness)
    {
        startupDelay = Mathf.Max(0f, initialStartupDelay);
        aimedBurstShotInterval = Mathf.Max(0f, fallbackAimedBurstShotInterval);
        warningLineThickness = Mathf.Max(0f, telegraphLineThickness);
        attackSizeMultiplier = Mathf.Max(0.1f, sizeMultiplier);
        minimumTelegraphThickness = Mathf.Max(0.01f, minTelegraphThickness);
        attackCooldownRemaining = Mathf.Max(0f, attackCooldownRemaining);
    }

    public void SetPatternEnabledForDebug(int patternIndex, bool value)
    {
        BossBulletPatternDefinition pattern = GetPatternForDebug(patternIndex);
        if (pattern != null)
        {
            pattern.enabled = value;
        }
    }

    public void SetPatternFloatForDebug(int patternIndex, BossPatternTuningKey key, float value)
    {
        BossBulletPatternDefinition pattern = GetPatternForDebug(patternIndex);
        if (pattern == null)
        {
            return;
        }

        float clampedValue = Mathf.Max(0f, value);
        switch (key)
        {
            case BossPatternTuningKey.MinHealthRatio:
                pattern.minHealthRatio = Mathf.Clamp01(clampedValue);
                break;
            case BossPatternTuningKey.MaxHealthRatio:
                pattern.maxHealthRatio = Mathf.Clamp01(clampedValue);
                break;
            case BossPatternTuningKey.CooldownMultiplier:
                pattern.cooldownMultiplier = clampedValue;
                break;
            case BossPatternTuningKey.BurstInterval:
                pattern.burstInterval = clampedValue;
                break;
            case BossPatternTuningKey.SpreadAngle:
                pattern.spreadAngle = clampedValue;
                break;
            case BossPatternTuningKey.SpeedMultiplier:
                pattern.speedMultiplier = clampedValue;
                break;
            case BossPatternTuningKey.SecondarySpeedMultiplier:
                pattern.secondarySpeedMultiplier = clampedValue;
                break;
            case BossPatternTuningKey.DamageMultiplier:
                pattern.damageMultiplier = clampedValue;
                break;
            case BossPatternTuningKey.SecondaryDamageMultiplier:
                pattern.secondaryDamageMultiplier = clampedValue;
                break;
            case BossPatternTuningKey.RingRotationStep:
                pattern.ringRotationStep = clampedValue;
                break;
            case BossPatternTuningKey.TelegraphDuration:
                pattern.telegraphDuration = clampedValue;
                break;
            case BossPatternTuningKey.FlashingDuration:
                pattern.flashingDuration = clampedValue;
                break;
            case BossPatternTuningKey.WarningWidth:
                pattern.warningWidth = clampedValue;
                break;
            case BossPatternTuningKey.WarningHeight:
                pattern.warningHeight = clampedValue;
                break;
            case BossPatternTuningKey.WarningDepth:
                pattern.warningDepth = clampedValue;
                break;
            case BossPatternTuningKey.OverheadHeight:
                pattern.overheadHeight = clampedValue;
                break;
            case BossPatternTuningKey.SplitDistance:
                pattern.splitDistance = clampedValue;
                break;
            case BossPatternTuningKey.ProjectileScale:
                pattern.projectileScale = Mathf.Max(0.1f, clampedValue);
                break;
            case BossPatternTuningKey.ActiveDuration:
                pattern.activeDuration = Mathf.Max(0.05f, clampedValue);
                break;
            case BossPatternTuningKey.HazardRadius:
                pattern.hazardRadius = Mathf.Max(0.1f, clampedValue);
                break;
            case BossPatternTuningKey.HazardThickness:
                pattern.hazardThickness = Mathf.Max(0.1f, clampedValue);
                break;
            case BossPatternTuningKey.InterruptDamageThreshold:
                pattern.interruptDamageThreshold = clampedValue;
                break;
            case BossPatternTuningKey.SafeRadius:
                pattern.safeRadius = clampedValue;
                break;
            case BossPatternTuningKey.MinimumSpacing:
                pattern.minimumSpacing = clampedValue;
                break;
            case BossPatternTuningKey.FixedDuration:
                pattern.fixedDuration = Mathf.Max(0.05f, clampedValue);
                break;
            case BossPatternTuningKey.SlowDuration:
                pattern.slowDuration = Mathf.Max(0.05f, clampedValue);
                break;
            case BossPatternTuningKey.FastDuration:
                pattern.fastDuration = Mathf.Max(0.05f, clampedValue);
                break;
            case BossPatternTuningKey.TrackingDuration:
                pattern.trackingDuration = Mathf.Max(0.05f, clampedValue);
                break;
            case BossPatternTuningKey.TrackingTurnRate:
                pattern.trackingTurnRate = clampedValue;
                break;
            case BossPatternTuningKey.BeamWarmupTrackingSpeedMultiplier:
                pattern.beamWarmupTrackingSpeedMultiplier = clampedValue;
                break;
            case BossPatternTuningKey.BeamActiveTrackingSpeedMultiplier:
                pattern.beamActiveTrackingSpeedMultiplier = clampedValue;
                break;
            case BossPatternTuningKey.AimJitterPlayerScale:
                pattern.aimJitterPlayerScale = clampedValue;
                break;
            case BossPatternTuningKey.ApproachStartScale:
                pattern.approachStartScale = Mathf.Max(0.01f, clampedValue);
                break;
            case BossPatternTuningKey.ApproachEndScale:
                pattern.approachEndScale = Mathf.Max(0.01f, clampedValue);
                break;
            case BossPatternTuningKey.ApproachInitialSpeedMultiplier:
                pattern.approachInitialSpeedMultiplier = Mathf.Max(0.01f, clampedValue);
                break;
            case BossPatternTuningKey.ApproachFlightDuration:
                pattern.approachFlightDuration = Mathf.Max(0.05f, clampedValue);
                break;
        }
    }

    public void SetPatternIntForDebug(int patternIndex, BossPatternTuningKey key, int value)
    {
        BossBulletPatternDefinition pattern = GetPatternForDebug(patternIndex);
        if (pattern == null)
        {
            return;
        }

        int clampedValue = Mathf.Max(1, value);
        switch (key)
        {
            case BossPatternTuningKey.ProjectileCount:
                pattern.projectileCount = clampedValue;
                break;
            case BossPatternTuningKey.SecondaryProjectileCount:
                pattern.secondaryProjectileCount = clampedValue;
                break;
            case BossPatternTuningKey.BurstCount:
                pattern.burstCount = clampedValue;
                break;
        }
    }

    public void CancelActivePatternForDebug()
    {
        CancelActivePattern();
        CleanupTelegraphs();
    }

    private BossBulletPatternDefinition GetPatternForDebug(int patternIndex)
    {
        EnsureDefaultPatterns();
        List<BossBulletPatternDefinition> activeSequence = GetActivePatternSequence();
        if (activeSequence == null || patternIndex < 0 || patternIndex >= activeSequence.Count)
        {
            return null;
        }

        return activeSequence[patternIndex];
    }

    private void Update()
    {
        if (!CanRunPatterns())
        {
            if (activePatternRoutine != null && preserveActiveTelegraphUntilPatternEnds)
            {
                return;
            }

            CancelActivePattern();
            CleanupTelegraphs();
            return;
        }

        if (activePatternRoutine != null)
        {
            return;
        }

        attackCooldownRemaining -= Time.deltaTime;
        if (attackCooldownRemaining > 0f)
        {
            return;
        }

        BossBulletPatternDefinition pattern = SelectNextPattern();
        if (pattern == null)
        {
            attackCooldownRemaining = ResolveCooldown(1f);
            return;
        }

        attackController.NotifyGameplayAttackStarted();
        activePatternRoutine = StartCoroutine(ExecutePatternRoutine(pattern));
    }

    private bool CanRunPatterns()
    {
        return enabled &&
               !cinematicPaused &&
               attackController != null &&
               battleController != null &&
               bossController != null &&
               playerCombatController != null &&
               attackController.CanAttack;
    }

    private void EnsureDefaultPatterns()
    {
        if (patternSequence == null || patternSequence.Count == 0)
        {
            patternSequence = new List<BossBulletPatternDefinition>
            {
                new()
                {
                    displayName = "Fan Spread",
                    patternType = BossBulletPatternType.FanSpread,
                    enabled = true,
                    minHealthRatio = 0.5f,
                    maxHealthRatio = 1f,
                    cooldownMultiplier = 1f,
                    projectileCount = 7,
                    burstCount = 3,
                    burstInterval = 0.4f,
                    spreadAngle = 60f,
                    speedMultiplier = 0.5f,
                    damageMultiplier = 1f,
                },
                new()
                {
                    displayName = "Aimed Burst",
                    patternType = BossBulletPatternType.AimedBurst,
                    enabled = true,
                    minHealthRatio = 0.4f,
                    maxHealthRatio = 1f,
                    cooldownMultiplier = 0.95f,
                    projectileCount = 3,
                    burstInterval = 0.14f,
                    speedMultiplier = 1.3f,
                    damageMultiplier = 0.9f,
                    telegraphDuration = 0.3f,
                },
                new()
                {
                    displayName = "Spiral Ring",
                    patternType = BossBulletPatternType.SpiralRing,
                    enabled = true,
                    minHealthRatio = 0.25f,
                    maxHealthRatio = 0.8f,
                    cooldownMultiplier = 1.2f,
                    projectileCount = 12,
                    speedMultiplier = 0.8f,
                    damageMultiplier = 0.8f,
                    ringRotationStep = 18f,
                },
                new()
                {
                    displayName = "Falling Bomb",
                    patternType = BossBulletPatternType.FallingBomb,
                    enabled = true,
                    minHealthRatio = 0f,
                    maxHealthRatio = 0.55f,
                    cooldownMultiplier = 1.25f,
                    speedMultiplier = 1f,
                    damageMultiplier = 1.8f,
                    telegraphDuration = 2f,
                    flashingDuration = 0.6f,
                    warningWidth = 2.8f,
                    warningHeight = 10f,
                    warningDepth = 1.4f,
                },
                new()
                {
                    displayName = "Split Shot",
                    patternType = BossBulletPatternType.SplitShot,
                    enabled = true,
                    minHealthRatio = 0f,
                    maxHealthRatio = 0.45f,
                    cooldownMultiplier = 1.1f,
                    projectileCount = 1,
                    secondaryProjectileCount = 5,
                    spreadAngle = 70f,
                    speedMultiplier = 0.6f,
                    secondarySpeedMultiplier = 0.9f,
                    damageMultiplier = 0.9f,
                    secondaryDamageMultiplier = 0.75f,
                    splitDistance = 11f,
                },
            };
        }

        if (kaijuHeavyThreatSequence != null &&
            kaijuHeavyThreatSequence.Count > 0 &&
            !MatchesPreviousKaijuDefaultSequence(kaijuHeavyThreatSequence) &&
            !MatchesOutdatedKaijuMvpDefaultSequence(kaijuHeavyThreatSequence))
        {
            return;
        }

        kaijuHeavyThreatSequence = new List<BossBulletPatternDefinition>
        {
            new()
            {
                displayName = "Debris Fragment Scatter",
                patternType = BossBulletPatternType.DebrisFragmentScatter,
                enabled = true,
                minHealthRatio = 0f,
                maxHealthRatio = 1f,
                cooldownMultiplier = 0.9f,
                projectileCount = 10,
                secondaryProjectileCount = 7,
                burstInterval = 0.025f,
                speedMultiplier = 0.25f,
                damageMultiplier = 0.35f,
                telegraphDuration = 0.25f,
                warningWidth = 0.65f,
                projectileScale = 0.45f,
                aimJitterPlayerScale = 6f,
                safeRadius = 1f,
                hazardThickness = 0.34f,
            },
            new()
            {
                displayName = "Debris Salvo",
                patternType = BossBulletPatternType.DebrisSalvo,
                enabled = true,
                minHealthRatio = 0f,
                maxHealthRatio = 1f,
                cooldownMultiplier = 1.05f,
                projectileCount = 4,
                secondaryProjectileCount = 2,
                burstInterval = 0.2f,
                speedMultiplier = 0.5f,
                damageMultiplier = 1.55f,
                telegraphDuration = 0.3f,
                flashingDuration = 0.04f,
                warningWidth = 0.8f,
                projectileScale = 2.8f,
                aimJitterPlayerScale = 2f,
                approachStartScale = 0.25f,
                approachEndScale = 1.19f,
                approachInitialSpeedMultiplier = 0.5f,
                approachFlightDuration = 1f,
            },
            new()
            {
                displayName = "Accelerating Sweep Beam",
                patternType = BossBulletPatternType.AcceleratingSweepBeam,
                enabled = true,
                minHealthRatio = 0f,
                maxHealthRatio = 1f,
                cooldownMultiplier = 1.2f,
                spreadAngle = 92f,
                speedMultiplier = 1f,
                damageMultiplier = 1.2f,
                telegraphDuration = 0.8f,
                flashingDuration = 0.05f,
                slowDuration = 0.3f,
                fastDuration = 0.2f,
                warningWidth = 0.78f,
                hazardRadius = 28f,
            },
            new()
            {
                displayName = "Tracking Residual Beam",
                patternType = BossBulletPatternType.TrackingResidualBeam,
                enabled = true,
                minHealthRatio = 0f,
                maxHealthRatio = 0.65f,
                cooldownMultiplier = 1.35f,
                damageMultiplier = 0.95f,
                telegraphDuration = 0.7f,
                fixedDuration = 0.5f,
                trackingDuration = 4f,
                trackingTurnRate = 28f,
                beamWarmupTrackingSpeedMultiplier = 0.5f,
                beamActiveTrackingSpeedMultiplier = 0.4f,
                warningWidth = 0.62f,
                hazardRadius = 28f,
                safeRadius = 3.2f,
            },
        };
    }

    private static bool MatchesPreviousKaijuDefaultSequence(List<BossBulletPatternDefinition> sequence)
    {
        return sequence != null &&
               sequence.Count == 4 &&
               sequence[0] != null &&
               sequence[0].patternType == BossBulletPatternType.DebrisThrow &&
               sequence[1] != null &&
               sequence[1].patternType == BossBulletPatternType.LaserSweep &&
               sequence[2] != null &&
               sequence[2].patternType == BossBulletPatternType.ShockwaveRing &&
               sequence[3] != null &&
               sequence[3].patternType == BossBulletPatternType.InterruptibleHeavyShot;
    }

    private static bool MatchesOutdatedKaijuMvpDefaultSequence(List<BossBulletPatternDefinition> sequence)
    {
        return sequence != null &&
               sequence.Count == 3 &&
               sequence[0] != null &&
               sequence[0].patternType == BossBulletPatternType.DebrisSalvo &&
               sequence[1] != null &&
               sequence[1].patternType == BossBulletPatternType.AcceleratingSweepBeam &&
               sequence[2] != null &&
               sequence[2].patternType == BossBulletPatternType.TrackingResidualBeam &&
               (Mathf.Approximately(sequence[0].approachFlightDuration, 0.7f) ||
                Mathf.Approximately(sequence[0].telegraphDuration, 0.1f) ||
                Mathf.Approximately(sequence[0].burstInterval, 0.11f) ||
                Mathf.Approximately(sequence[0].approachEndScale, 0.7f) ||
                Mathf.Approximately(sequence[0].approachEndScale, 1.19f) ||
                Mathf.Approximately(sequence[2].trackingDuration, 3f));
    }

    private List<BossBulletPatternDefinition> GetActivePatternSequence()
    {
        return activePatternSet == BossBulletPatternSet.KaijuHeavyThreats ? kaijuHeavyThreatSequence : patternSequence;
    }

    private BossBulletPatternDefinition SelectNextPattern()
    {
        List<BossBulletPatternDefinition> activeSequence = GetActivePatternSequence();
        if (activeSequence == null || activeSequence.Count == 0)
        {
            return null;
        }

        float healthRatio = bossController != null ? bossController.HealthRatio : 1f;
        int count = activeSequence.Count;

        for (int offset = 0; offset < count; offset++)
        {
            int index = (nextPatternIndex + offset) % count;
            BossBulletPatternDefinition candidate = activeSequence[index];
            if (!IsPatternEligible(candidate, healthRatio))
            {
                continue;
            }

            nextPatternIndex = (index + 1) % count;
            return candidate;
        }

        return null;
    }

    private static bool IsPatternEligible(BossBulletPatternDefinition pattern, float healthRatio)
    {
        if (pattern == null || !pattern.enabled)
        {
            return false;
        }

        float min = Mathf.Min(pattern.minHealthRatio, pattern.maxHealthRatio);
        float max = Mathf.Max(pattern.minHealthRatio, pattern.maxHealthRatio);
        return healthRatio >= min && healthRatio <= max;
    }

    private IEnumerator ExecutePatternRoutine(BossBulletPatternDefinition pattern)
    {
        yield return pattern.patternType switch
        {
            BossBulletPatternType.FanSpread => ExecuteFanSpread(pattern),
            BossBulletPatternType.AimedBurst => ExecuteAimedBurst(pattern),
            BossBulletPatternType.SpiralRing => ExecuteSpiralRing(pattern),
            BossBulletPatternType.FallingBomb => ExecuteFallingBomb(pattern),
            BossBulletPatternType.SplitShot => ExecuteSplitShot(pattern),
            BossBulletPatternType.DebrisThrow => ExecuteDebrisThrow(pattern),
            BossBulletPatternType.LaserSweep => ExecuteLaserSweep(pattern),
            BossBulletPatternType.ShockwaveRing => ExecuteShockwaveRing(pattern),
            BossBulletPatternType.InterruptibleHeavyShot => ExecuteInterruptibleHeavyShot(pattern),
            BossBulletPatternType.DebrisSalvo => ExecuteDebrisSalvo(pattern),
            BossBulletPatternType.AcceleratingSweepBeam => ExecuteAcceleratingSweepBeam(pattern),
            BossBulletPatternType.TrackingResidualBeam => ExecuteTrackingResidualBeam(pattern),
            BossBulletPatternType.DebrisFragmentScatter => ExecuteDebrisFragmentScatter(pattern),
            _ => ExecuteFanSpread(pattern),
        };

        attackCooldownRemaining = ResolveCooldown(pattern.cooldownMultiplier);
        preserveActiveTelegraphUntilPatternEnds = false;
        activePatternRoutine = null;
    }

    private IEnumerator ExecuteFanSpread(BossBulletPatternDefinition pattern)
    {
        float projectileSpeed = ResolvePrimarySpeed(pattern);
        float projectileDamage = ResolvePrimaryDamage(pattern);

        for (int burstIndex = 0; burstIndex < Mathf.Max(1, pattern.burstCount); burstIndex++)
        {
            Vector3 origin = attackController.CurrentFireOrigin;
            Vector3 target = playerCombatController.HitPoint;
            attackController.PlayQuickAttackAnimation();
            SpawnSpread(origin, target, Mathf.Max(1, pattern.projectileCount), pattern.spreadAngle, projectileSpeed, projectileDamage);

            if (burstIndex + 1 < Mathf.Max(1, pattern.burstCount))
            {
                yield return new WaitForSeconds(pattern.burstInterval);
            }
        }
    }

    private IEnumerator ExecuteAimedBurst(BossBulletPatternDefinition pattern)
    {
        Vector3 warningOrigin = attackController.CurrentFireOrigin;
        Vector3 warningTarget = playerCombatController.HitPoint;
        GameObject warning = CreateLineTelegraph(
            warningOrigin,
            warningTarget,
            warningLineThickness,
            new Color(1f, 0.08f, 0.08f, 0.3f));
        yield return new WaitForSeconds(pattern.telegraphDuration);
        DestroyTelegraph(warning);

        float projectileSpeed = ResolvePrimarySpeed(pattern);
        float projectileDamage = ResolvePrimaryDamage(pattern);
        float shotInterval = pattern.burstInterval > 0f ? pattern.burstInterval : aimedBurstShotInterval;

        for (int shotIndex = 0; shotIndex < Mathf.Max(1, pattern.projectileCount); shotIndex++)
        {
            Vector3 origin = attackController.CurrentFireOrigin;
            Vector3 target = playerCombatController.HitPoint;
            attackController.PlayQuickAttackAnimation();
            attackController.SpawnProjectile(origin, target - origin, projectileSpeed, projectileDamage, spawnCosmeticBurst: false);

            if (shotIndex + 1 < Mathf.Max(1, pattern.projectileCount))
            {
                yield return new WaitForSeconds(shotInterval);
            }
        }
    }

    private IEnumerator ExecuteSpiralRing(BossBulletPatternDefinition pattern)
    {
        Vector3 origin = attackController.CurrentBossCenter;
        int projectileCount = Mathf.Max(4, pattern.projectileCount);
        float angleStep = 360f / projectileCount;
        float startAngle = spiralRotationDegrees;
        float projectileSpeed = ResolvePrimarySpeed(pattern);
        float projectileDamage = ResolvePrimaryDamage(pattern);

        attackController.PlayHeavyAttackAnimation();

        for (int i = 0; i < projectileCount; i++)
        {
            float angle = startAngle + angleStep * i;
            Vector3 direction = Quaternion.Euler(0f, angle, 0f) * Vector3.forward;
            attackController.SpawnProjectile(origin, direction, projectileSpeed, projectileDamage, spawnCosmeticBurst: false);
        }

        spiralRotationDegrees = Mathf.Repeat(spiralRotationDegrees + pattern.ringRotationStep, 360f);
        yield break;
    }

    private IEnumerator ExecuteFallingBomb(BossBulletPatternDefinition pattern)
    {
        Vector3 target = playerCombatController.HitPoint;
        attackController.PlayHeavyAttackAnimation();
        GameObject warning = CreateLaneTelegraph(target, pattern);
        yield return new WaitForSeconds(pattern.telegraphDuration);

        float flashInterval = 0.1f;
        float elapsed = 0f;
        bool bright = false;
        while (elapsed < pattern.flashingDuration)
        {
            SetTelegraphColor(warning, bright ? new Color(1f, 0.15f, 0.15f, 0.75f) : new Color(1f, 0.92f, 0.2f, 0.45f));
            bright = !bright;
            elapsed += flashInterval;
            yield return new WaitForSeconds(flashInterval);
        }

        BoxCollider warningCollider = warning != null ? warning.GetComponent<BoxCollider>() : null;
        if (warningCollider != null && playerCombatController.CheckHit(warningCollider.transform.position, 0f, warningCollider))
        {
            playerCombatController.ApplyDamage(ResolvePrimaryDamage(pattern));
        }

        DestroyTelegraph(warning);
    }

    private IEnumerator ExecuteSplitShot(BossBulletPatternDefinition pattern)
    {
        Vector3 origin = attackController.CurrentFireOrigin;
        Vector3 target = playerCombatController.HitPoint;
        attackController.PlayQuickAttackAnimation();
        ProjectileController projectile = attackController.SpawnProjectile(
            origin,
            target - origin,
            ResolvePrimarySpeed(pattern),
            ResolvePrimaryDamage(pattern));

        if (projectile != null)
        {
            BossSplitProjectileRuntime splitRuntime = projectile.gameObject.AddComponent<BossSplitProjectileRuntime>();
            splitRuntime.Configure(
                attackController,
                Mathf.Max(3, pattern.secondaryProjectileCount),
                pattern.spreadAngle,
                pattern.splitDistance,
                ResolveSecondarySpeed(pattern),
                ResolveSecondaryDamage(pattern));
        }

        yield break;
    }

    private IEnumerator ExecuteDebrisThrow(BossBulletPatternDefinition pattern)
    {
        Vector3 origin = attackController.CurrentFireOrigin;
        Vector3 lockedTarget = playerCombatController.HitPoint;
        attackController.PlayHeavyAttackAnimation();

        GameObject warning = CreateLineTelegraph(
            origin,
            lockedTarget,
            Mathf.Max(warningLineThickness, pattern.warningWidth),
            new Color(1f, 0.68f, 0.08f, 0.38f));
        yield return new WaitForSeconds(pattern.telegraphDuration);
        DestroyTelegraph(warning);

        ProjectileController debris = attackController.SpawnProjectile(
            origin,
            lockedTarget - origin,
            ResolvePrimarySpeed(pattern),
            ResolvePrimaryDamage(pattern),
            "BossDebrisRuntime",
            pattern.projectileScale);

        if (debris != null && pattern.secondaryProjectileCount > 0)
        {
            BossSplitProjectileRuntime splitRuntime = debris.gameObject.AddComponent<BossSplitProjectileRuntime>();
            splitRuntime.Configure(
                attackController,
                Mathf.Max(3, pattern.secondaryProjectileCount),
                pattern.spreadAngle,
                pattern.splitDistance,
                ResolveSecondarySpeed(pattern),
                ResolveSecondaryDamage(pattern));
        }
    }

    private IEnumerator ExecuteLaserSweep(BossBulletPatternDefinition pattern)
    {
        Vector3 origin = attackController.CurrentFireOrigin;
        Vector3 centerDirection = ResolveSafeDirection(playerCombatController.HitPoint - origin);
        float radius = Mathf.Max(1f, ScaleAttackSize(pattern.hazardRadius));
        float width = Mathf.Max(0.1f, ScaleAttackSize(pattern.warningWidth));
        float halfSweepAngle = pattern.spreadAngle * 0.5f;

        attackController.PlayHeavyAttackAnimation();
        GameObject chargeWarning = CreateBeamTelegraph(
            "BossLaserChargeTelegraph",
            origin,
            centerDirection,
            Mathf.Max(warningLineThickness, width * 0.25f),
            radius,
            new Color(1f, 0.22f, 0.08f, 0.35f));
        yield return new WaitForSeconds(pattern.telegraphDuration);
        DestroyTelegraph(chargeWarning);

        GameObject beam = CreateBeamTelegraph(
            "BossLaserSweepRuntime",
            origin,
            Quaternion.AngleAxis(-halfSweepAngle, Vector3.up) * centerDirection,
            width,
            radius,
            new Color(1f, 0.04f, 0.02f, 0.72f));

        float elapsed = 0f;
        float duration = Mathf.Max(0.05f, pattern.activeDuration);
        while (elapsed < duration)
        {
            float t = Mathf.Clamp01(elapsed / duration);
            float angle = Mathf.Lerp(-halfSweepAngle, halfSweepAngle, t);
            Vector3 sweepOrigin = attackController.CurrentFireOrigin;
            Vector3 direction = ResolveSafeDirection(Quaternion.AngleAxis(angle, Vector3.up) * centerDirection);

            UpdateBeamTelegraph(beam, sweepOrigin, direction, width, radius);
            TryApplyLineHazardDamage(sweepOrigin, sweepOrigin + direction * radius, width * 0.5f, ResolvePrimaryDamage(pattern));

            elapsed += Time.deltaTime;
            yield return null;
        }

        DestroyTelegraph(beam);
    }

    private IEnumerator ExecuteShockwaveRing(BossBulletPatternDefinition pattern)
    {
        Vector3 center = ResolveShockwaveCenter();
        attackController.PlayHeavyAttackAnimation();

        GameObject chargeWarning = CreateTelegraphPrimitive(
            "BossShockwaveChargeTelegraph",
            PrimitiveType.Sphere,
            center,
            Quaternion.identity,
            Vector3.one * Mathf.Max(0.5f, ScaleAttackSize(pattern.warningWidth)),
            new Color(1f, 0.72f, 0.06f, 0.42f),
            false);
        yield return new WaitForSeconds(pattern.telegraphDuration);
        DestroyTelegraph(chargeWarning);

        int segmentCount = Mathf.Max(12, pattern.projectileCount);
        float thickness = Mathf.Max(0.1f, ScaleAttackSize(pattern.hazardThickness));
        List<GameObject> ringSegments = CreateShockwaveSegments(center, thickness, segmentCount);
        float elapsed = 0f;
        float duration = Mathf.Max(0.05f, pattern.activeDuration);
        float targetRadius = Mathf.Max(thickness, ScaleAttackSize(pattern.hazardRadius));
        float hitHeight = ScaleAttackSize(pattern.warningHeight);

        while (elapsed < duration)
        {
            float t = Mathf.Clamp01(elapsed / duration);
            float radius = Mathf.Lerp(thickness, targetRadius, t);
            center = ResolveShockwaveCenter();

            UpdateShockwaveSegments(ringSegments, center, radius, thickness);
            TryApplyShockwaveDamage(center, radius, thickness, hitHeight, ResolvePrimaryDamage(pattern));

            elapsed += Time.deltaTime;
            yield return null;
        }

        DestroyTelegraphs(ringSegments);
    }

    private IEnumerator ExecuteInterruptibleHeavyShot(BossBulletPatternDefinition pattern)
    {
        Vector3 lockedTarget = playerCombatController.HitPoint;
        float startingBossHealth = bossController != null ? bossController.CurrentHealth : 0f;

        attackController.PlayHeavyAttackAnimation();
        GameObject weakPointWarning = CreateTelegraphPrimitive(
            "BossInterruptWeakPoint",
            PrimitiveType.Sphere,
            attackController.CurrentBossCenter,
            Quaternion.identity,
            Vector3.one * Mathf.Max(0.5f, ScaleAttackSize(pattern.warningWidth)),
            new Color(0.14f, 0.92f, 1f, 0.48f),
            false);
        GameObject shotWarning = CreateLineTelegraph(
            attackController.CurrentFireOrigin,
            lockedTarget,
            Mathf.Max(warningLineThickness, pattern.warningWidth * 0.18f),
            new Color(1f, 0.1f, 0.04f, 0.44f));

        float elapsed = 0f;
        float chargeDuration = Mathf.Max(0.05f, pattern.telegraphDuration);
        while (elapsed < chargeDuration)
        {
            if (DidInterruptHeavyShot(startingBossHealth, pattern.interruptDamageThreshold))
            {
                SetTelegraphColor(weakPointWarning, new Color(0.08f, 1f, 0.42f, 0.62f));
                DestroyTelegraph(shotWarning);
                yield return new WaitForSeconds(Mathf.Max(0.05f, pattern.flashingDuration));
                DestroyTelegraph(weakPointWarning);
                yield break;
            }

            UpdateInterruptTelegraphs(weakPointWarning, shotWarning, lockedTarget, pattern);
            elapsed += Time.deltaTime;
            yield return null;
        }

        DestroyTelegraph(weakPointWarning);
        DestroyTelegraph(shotWarning);

        Vector3 origin = attackController.CurrentFireOrigin;
        attackController.SpawnProjectile(
            origin,
            lockedTarget - origin,
            ResolvePrimarySpeed(pattern),
            ResolvePrimaryDamage(pattern),
            "BossInterruptibleHeavyShotRuntime",
            pattern.projectileScale);
    }

    private IEnumerator ExecuteDebrisSalvo(BossBulletPatternDefinition pattern)
    {
        int shotCount = ResolveHealthScaledProjectileCount(pattern);
        float shotTelegraphDuration = Mathf.Max(0f, pattern.telegraphDuration);
        float shotInterval = Mathf.Max(0f, pattern.burstInterval);
        float projectileSpeed = ResolvePrimarySpeed(pattern);
        float projectileDamage = ResolvePrimaryDamage(pattern);
        float warningThickness = Mathf.Max(0.01f, warningLineThickness);

        attackController.PlayHeavyAttackAnimation();

        Vector3 firstOrigin = attackController.CurrentFireOrigin;
        Vector3 firstTarget = ResolvePlayerSizedRandomAimPoint(firstOrigin, pattern);
        GameObject shotWarning = null;
        if (shotTelegraphDuration > 0f)
        {
            shotWarning = CreateLineTelegraph(
                firstOrigin,
                firstTarget,
                warningThickness,
                new Color(1f, 0.62f, 0.08f, 0.34f),
                useMinimumThickness: false,
                scaleThickness: false);
            yield return new WaitForSeconds(shotTelegraphDuration);
        }

        DestroyTelegraph(shotWarning);

        for (int shotIndex = 0; shotIndex < shotCount; shotIndex++)
        {
            Vector3 origin = attackController.CurrentFireOrigin;
            Vector3 target = shotIndex == 0 ? firstTarget : ResolvePlayerSizedRandomAimPoint(origin, pattern);
            Vector3 shotDirection = ResolveSafeDirection(target - origin);
            float travelDistance = Mathf.Max(0.01f, Vector3.Distance(origin, target));
            float initialSpeed = projectileSpeed * Mathf.Max(0.01f, pattern.approachInitialSpeedMultiplier);
            float acceleration = ResolveAccelerationForTravelTime(
                travelDistance,
                initialSpeed,
                Mathf.Max(0.05f, pattern.approachFlightDuration));
            ProjectileController projectile = attackController.SpawnProjectile(
                origin,
                shotDirection,
                initialSpeed,
                projectileDamage,
                "BossDebrisSalvoRuntime",
                pattern.projectileScale,
                spawnCosmeticBurst: false);
            if (projectile != null)
            {
                BossProjectileApproachScaleRuntime scaleRuntime = projectile.gameObject.AddComponent<BossProjectileApproachScaleRuntime>();
                scaleRuntime.Configure(
                    projectile,
                    origin,
                    target,
                    attackController.ResolveProjectileScaleMultiplier(pattern.projectileScale),
                    pattern.approachStartScale,
                    pattern.approachEndScale,
                    shotDirection,
                    initialSpeed,
                    acceleration);
            }

            if (shotIndex + 1 < shotCount)
            {
                yield return new WaitForSeconds(shotInterval);
            }
        }
    }

    private IEnumerator ExecuteDebrisFragmentScatter(BossBulletPatternDefinition pattern)
    {
        EnsureDebrisFragmentBatches(
            Mathf.Max(1, Mathf.Min(pattern.projectileCount, pattern.secondaryProjectileCount)),
            Mathf.Max(1, Mathf.Max(pattern.projectileCount, pattern.secondaryProjectileCount)));

        int[] fragmentBatch = GetNextDebrisFragmentBatch();
        int fragmentCount = fragmentBatch != null ? fragmentBatch.Length : ResolveHealthScaledProjectileCount(pattern);
        if (fragmentCount <= 0)
        {
            yield break;
        }

        Transform selectedFirePoint = ResolveDebrisFragmentFirePoint();
        Vector3 warningOrigin = ResolveDebrisFragmentFireOrigin(selectedFirePoint);
        Vector3 warningTarget = playerCombatController != null ? playerCombatController.HitPoint : attackController.CurrentPlayerHitPoint;
        GameObject warning = null;
        if (pattern.telegraphDuration > 0f)
        {
            warning = CreateLineTelegraph(
                warningOrigin,
                warningTarget,
                Mathf.Max(0.01f, warningLineThickness),
                new Color(1f, 0.52f, 0.08f, 0.28f),
                useMinimumThickness: false,
                scaleThickness: false);
        }

        if (debrisFragmentStompCount > 0 && debrisFragmentStompDuration > 0f && debrisFragmentJumpHeightRatio > 0f)
        {
            yield return PerformDebrisFragmentStompWindup();
        }
        else if (pattern.telegraphDuration > 0f)
        {
            yield return new WaitForSeconds(pattern.telegraphDuration);
        }

        DestroyTelegraph(warning);
        attackController.PlayQuickAttackAnimation();

        Vector3 origin = ResolveDebrisFragmentFireOrigin(selectedFirePoint);
        Vector3 playerCenter = playerCombatController != null ? playerCombatController.HitPoint : attackController.CurrentPlayerHitPoint;
        float projectileSpeed = ResolvePrimarySpeed(pattern);
        float projectileDamage = ResolvePrimaryDamage(pattern);
        float playerRadius = playerCombatController != null ? playerCombatController.HitRadius : 1f;
        float clusterRadius = Mathf.Max(0.05f, playerRadius * Mathf.Max(0.05f, pattern.safeRadius));
        float scatterRadius = Mathf.Max(clusterRadius * 2.2f, playerRadius * Mathf.Max(0.1f, pattern.aimJitterPlayerScale));
        float scatteredMinRadius = Mathf.Min(scatterRadius * 0.92f, clusterRadius * 1.8f);
        int clusterCount = Mathf.Min(fragmentCount, Random.Range(DebrisFragmentClusterMinCount, DebrisFragmentClusterMaxCount + 1));
        Vector3 clusterCenter = ResolveRandomAimPointInPlayerPlane(origin, playerCenter, 0f, clusterRadius * 0.6f);
        float spawnInterval = Mathf.Max(0f, pattern.burstInterval);

        for (int i = 0; i < fragmentCount; i++)
        {
            bool clustered = i < clusterCount;
            Vector3 aimPoint = clustered
                ? ResolveRandomAimPointInPlayerPlane(origin, clusterCenter, 0f, clusterRadius * 0.45f)
                : ResolveRandomAimPointInPlayerPlane(origin, playerCenter, scatteredMinRadius, scatterRadius);
            Vector3 direction = ResolveSafeDirection(aimPoint - origin);

            ProjectileController projectile = attackController.SpawnProjectile(
                origin,
                direction,
                projectileSpeed,
                projectileDamage,
                "BossDebrisFragmentRuntime",
                pattern.projectileScale,
                spawnCosmeticBurst: false);

            if (projectile != null)
            {
                GameObject fragmentPrefab = ResolveDebrisFragmentPrefab(fragmentBatch, i);
                DebrisFragmentProjectileVisualRuntime visualRuntime =
                    projectile.gameObject.AddComponent<DebrisFragmentProjectileVisualRuntime>();
                visualRuntime.Configure(fragmentPrefab, Mathf.Max(0.02f, pattern.hazardThickness));
            }

            if (spawnInterval > 0f && i + 1 < fragmentCount)
            {
                yield return new WaitForSeconds(spawnInterval);
            }
        }
    }

    private IEnumerator ExecuteAcceleratingSweepBeam(BossBulletPatternDefinition pattern)
    {
        Vector3 origin = attackController.CurrentFireOrigin;
        Vector3 centerDirection = ResolveSafeDirection(playerCombatController.HitPoint - origin);
        float radius = Mathf.Max(1f, ScaleAttackSize(pattern.hazardRadius));
        float width = Mathf.Max(0.1f, ScaleAttackSize(pattern.warningWidth));
        float halfSweepAngle = Mathf.Max(0f, pattern.spreadAngle) * 0.5f;
        bool leftToRight = Random.value >= 0.5f;
        float startAngle = leftToRight ? -halfSweepAngle : halfSweepAngle;
        float endAngle = leftToRight ? halfSweepAngle : -halfSweepAngle;
        Vector3 startDirection = ResolveSafeDirection(Quaternion.AngleAxis(startAngle, Vector3.up) * centerDirection);
        Vector3 endDirection = ResolveSafeDirection(Quaternion.AngleAxis(endAngle, Vector3.up) * centerDirection);
        if (TryResolveScreenHorizontalSweepDirections(origin, leftToRight, out Vector3 screenStartDirection, out Vector3 screenEndDirection))
        {
            startDirection = screenStartDirection;
            endDirection = screenEndDirection;
        }

        attackController.PlayHeavyAttackAnimation();
        GameObject directionWarning = CreateBeamTelegraph(
            "BossAcceleratingSweepDirectionWarning",
            origin,
            startDirection,
            Mathf.Max(warningLineThickness, width * 0.32f),
            radius,
            new Color(1f, 0.58f, 0.08f, 0.38f));

        float chargeElapsed = 0f;
        float chargeDuration = Mathf.Max(0.05f, pattern.telegraphDuration);
        while (chargeElapsed < chargeDuration)
        {
            UpdateBeamTelegraph(
                directionWarning,
                attackController.CurrentFireOrigin,
                startDirection,
                Mathf.Max(warningLineThickness, width * 0.32f),
                radius);

            chargeElapsed += Time.deltaTime;
            yield return null;
        }

        DestroyTelegraph(directionWarning);

        GameObject beam = CreateBeamTelegraph(
            "BossAcceleratingSweepBeam",
            attackController.CurrentFireOrigin,
            startDirection,
            width,
            radius,
            new Color(1f, 0.08f, 0.03f, 0.72f));

        preserveActiveTelegraphUntilPatternEnds = true;
        yield return SweepAcceleratingBeamPhase(
            beam,
            startDirection,
            endDirection,
            Mathf.Max(0.05f, pattern.slowDuration),
            Mathf.Max(0.05f, pattern.fastDuration),
            0.2f,
            width,
            radius,
            ResolvePrimaryDamage(pattern));
        preserveActiveTelegraphUntilPatternEnds = false;

        DestroyTelegraph(beam);
    }

    private IEnumerator ExecuteTrackingResidualBeam(BossBulletPatternDefinition pattern)
    {
        Vector3 origin = attackController.CurrentFireOrigin;
        Vector3 trackedAimPoint = ResolveTrackingResidualBeamTargetPoint();
        Vector3 beamDirection = ResolveSafeDirection(trackedAimPoint - origin);
        float radius = Mathf.Max(1f, ScaleAttackSize(pattern.hazardRadius));
        float width = Mathf.Max(0.1f, ScaleAttackSize(pattern.warningWidth));
        float chargeRadius = Mathf.Max(0.45f, width * 0.95f);

        attackController.PlayHeavyAttackAnimation();
        GameObject chargeWarning = CreateTelegraphPrimitive(
            "BossTrackingResidualMouthCharge",
            PrimitiveType.Sphere,
            origin,
            Quaternion.identity,
            Vector3.one * chargeRadius,
            new Color(1f, 0.46f, 0.08f, 0.48f),
            false);

        float warmupElapsed = 0f;
        float warmupDuration = Mathf.Max(0.05f, pattern.fixedDuration);
        while (warmupElapsed < warmupDuration)
        {
            origin = attackController.CurrentFireOrigin;
            beamDirection = ResolveSafeDirection(trackedAimPoint - origin);
            UpdateMouthChargeTelegraph(chargeWarning, origin, chargeRadius, warmupElapsed / warmupDuration);

            warmupElapsed += Time.deltaTime;
            yield return null;
        }

        DestroyTelegraph(chargeWarning);

        GameObject beam = CreateBeamTelegraph(
            "BossTrackingResidualBeam",
            attackController.CurrentFireOrigin,
            beamDirection,
            width,
            radius,
            new Color(1f, 0.03f, 0.02f, 0.72f));

        float trackingElapsed = 0f;
        float damageTickElapsed = 0f;
        float trackingDuration = Mathf.Max(0.05f, pattern.trackingDuration);
        SetTelegraphColor(beam, new Color(1f, 0.08f, 0.03f, 0.66f));

        while (trackingElapsed < trackingDuration)
        {
            Vector3 trackingOrigin = attackController.CurrentFireOrigin;
            trackedAimPoint = MoveAimPointTowardPlayer(
                trackedAimPoint,
                pattern.beamActiveTrackingSpeedMultiplier);
            beamDirection = ResolveSafeDirection(trackedAimPoint - trackingOrigin);

            UpdateBeamTelegraph(beam, trackingOrigin, beamDirection, width, radius);
            TryApplyTrackingResidualBeamDamage(
                trackingOrigin,
                trackingOrigin + beamDirection * radius,
                ResolveTrackingResidualBeamDamageHalfWidth(width),
                ref damageTickElapsed);

            trackingElapsed += Time.deltaTime;
            yield return null;
        }

        DestroyTelegraph(beam);
    }

    private void SpawnSpread(Vector3 origin, Vector3 target, int projectileCount, float spreadAngle, float speed, float damage)
    {
        Vector3 forward = (target - origin).normalized;
        Quaternion centerRotation = Quaternion.LookRotation(forward, Vector3.up);
        float step = projectileCount > 1 ? spreadAngle / (projectileCount - 1) : 0f;
        float start = -spreadAngle * 0.5f;

        for (int i = 0; i < projectileCount; i++)
        {
            float angle = start + step * i;
            Vector3 direction = centerRotation * Quaternion.AngleAxis(angle, Vector3.up) * Vector3.forward;
            attackController.SpawnProjectile(origin, direction, speed, damage, spawnCosmeticBurst: false);
        }
    }

    private IEnumerator SweepAcceleratingBeamPhase(
        GameObject beam,
        Vector3 startDirection,
        Vector3 endDirection,
        float slowDuration,
        float fastDuration,
        float slowProgress,
        float width,
        float radius,
        float damage)
    {
        float elapsed = 0f;
        float clampedSlowDuration = Mathf.Max(0.05f, slowDuration);
        float clampedFastDuration = Mathf.Max(0.05f, fastDuration);
        float clampedSlowProgress = Mathf.Clamp(slowProgress, 0.05f, 0.95f);
        float totalDuration = clampedSlowDuration + clampedFastDuration;
        bool fastVisualApplied = false;

        while (elapsed < totalDuration)
        {
            float progress;
            float currentWidth = width;
            if (elapsed < clampedSlowDuration)
            {
                progress = Mathf.Lerp(0f, clampedSlowProgress, Mathf.Clamp01(elapsed / clampedSlowDuration));
            }
            else
            {
                if (!fastVisualApplied)
                {
                    SetTelegraphColor(beam, new Color(1f, 0.02f, 0.02f, 0.82f));
                    fastVisualApplied = true;
                }

                float fastT = Mathf.Clamp01((elapsed - clampedSlowDuration) / clampedFastDuration);
                float fastProgress = 1f - Mathf.Pow(1f - fastT, 3f);
                progress = Mathf.Lerp(clampedSlowProgress, 1f, fastProgress);
                currentWidth = width * Mathf.Lerp(1.08f, 1.18f, fastT);
            }

            Vector3 origin = attackController.CurrentFireOrigin;
            Vector3 direction = ResolveSafeDirection(Vector3.Slerp(startDirection, endDirection, progress));

            UpdateBeamTelegraph(beam, origin, direction, currentWidth, radius);
            TryApplyLineHazardDamage(origin, origin + direction * radius, currentWidth * 0.5f, damage);

            elapsed += Time.deltaTime;
            yield return null;
        }

        Vector3 finalOrigin = attackController.CurrentFireOrigin;
        Vector3 finalDirection = ResolveSafeDirection(endDirection);
        float finalWidth = width * 1.18f;
        UpdateBeamTelegraph(beam, finalOrigin, finalDirection, finalWidth, radius);
        TryApplyLineHazardDamage(finalOrigin, finalOrigin + finalDirection * radius, finalWidth * 0.5f, damage);
        yield return null;
    }

    private int ResolveHealthScaledProjectileCount(BossBulletPatternDefinition pattern)
    {
        int minCount = Mathf.Max(1, Mathf.Min(pattern.projectileCount, pattern.secondaryProjectileCount));
        int maxCount = Mathf.Max(minCount, Mathf.Max(pattern.projectileCount, pattern.secondaryProjectileCount));
        if (minCount == maxCount)
        {
            return maxCount;
        }

        float healthLost = bossController != null ? 1f - Mathf.Clamp01(bossController.HealthRatio) : 0f;
        int steps = maxCount - minCount + 1;
        int offset = Mathf.Clamp(Mathf.FloorToInt(healthLost * steps), 0, maxCount - minCount);
        return minCount + offset;
    }

    private Vector3 ResolvePlayerSizedRandomAimPoint(Vector3 origin, BossBulletPatternDefinition pattern)
    {
        Vector3 playerCenter = playerCombatController != null ? playerCombatController.HitPoint : attackController.CurrentPlayerHitPoint;
        float playerRadius = playerCombatController != null ? playerCombatController.HitRadius : 1f;
        float randomRadius = Mathf.Max(0f, playerRadius * Mathf.Max(0f, pattern.aimJitterPlayerScale));
        if (randomRadius <= 0.001f)
        {
            return playerCenter;
        }

        Vector3 shotDirection = ResolveSafeDirection(playerCenter - origin);
        Vector3 right = Vector3.Cross(Vector3.up, shotDirection);
        if (right.sqrMagnitude <= 0.0001f)
        {
            right = Vector3.right;
        }
        else
        {
            right.Normalize();
        }

        Vector3 up = Vector3.Cross(shotDirection, right);
        if (up.sqrMagnitude <= 0.0001f)
        {
            up = Vector3.up;
        }
        else
        {
            up.Normalize();
        }

        Vector2 randomOffset = Random.insideUnitCircle * randomRadius;
        return playerCenter + right * randomOffset.x + up * randomOffset.y;
    }

    private void EnsureDebrisFragmentBatches(int minCount, int maxCount)
    {
        ResolveDebrisFragmentCatalog();

        int sourceCount = debrisFragmentCatalog != null ? debrisFragmentCatalog.Count : 0;
        int clampedMin = Mathf.Max(1, Mathf.Min(minCount, maxCount));
        int clampedMax = Mathf.Max(clampedMin, Mathf.Max(minCount, maxCount));
        if (sourceCount > 0)
        {
            clampedMin = Mathf.Min(clampedMin, sourceCount);
            clampedMax = Mathf.Min(clampedMax, sourceCount);
        }

        if (debrisFragmentBatchIndices != null &&
            debrisFragmentBatchIndices.Length == DebrisFragmentBatchTableCount &&
            debrisFragmentBatchSourceCount == sourceCount &&
            debrisFragmentBatchMinCount == clampedMin &&
            debrisFragmentBatchMaxCount == clampedMax)
        {
            return;
        }

        debrisFragmentBatchSourceCount = sourceCount;
        debrisFragmentBatchMinCount = clampedMin;
        debrisFragmentBatchMaxCount = clampedMax;
        nextDebrisFragmentBatchIndex = 0;

        if (sourceCount <= 0)
        {
            debrisFragmentBatchIndices = null;
            return;
        }

        debrisFragmentBatchIndices = new int[DebrisFragmentBatchTableCount][];
        List<int> sourceIndices = new(sourceCount);
        for (int batchIndex = 0; batchIndex < DebrisFragmentBatchTableCount; batchIndex++)
        {
            sourceIndices.Clear();
            for (int sourceIndex = 0; sourceIndex < sourceCount; sourceIndex++)
            {
                sourceIndices.Add(sourceIndex);
            }

            int batchCount = Random.Range(clampedMin, clampedMax + 1);
            int[] batch = new int[batchCount];
            for (int i = 0; i < batchCount; i++)
            {
                int pickIndex = Random.Range(0, sourceIndices.Count);
                batch[i] = sourceIndices[pickIndex];
                sourceIndices.RemoveAt(pickIndex);
            }

            debrisFragmentBatchIndices[batchIndex] = batch;
        }
    }

    private void ResolveDebrisFragmentCatalog()
    {
        if (debrisFragmentCatalog != null)
        {
            return;
        }

        debrisFragmentCatalog = Resources.Load<DebrisFragmentCatalog>(DebrisFragmentCatalogResourcePath);
    }

    private int[] GetNextDebrisFragmentBatch()
    {
        if (debrisFragmentBatchIndices == null || debrisFragmentBatchIndices.Length == 0)
        {
            return null;
        }

        int batchIndex = nextDebrisFragmentBatchIndex % debrisFragmentBatchIndices.Length;
        nextDebrisFragmentBatchIndex = (nextDebrisFragmentBatchIndex + 1) % debrisFragmentBatchIndices.Length;
        return debrisFragmentBatchIndices[batchIndex];
    }

    private GameObject ResolveDebrisFragmentPrefab(int[] fragmentBatch, int index)
    {
        ResolveDebrisFragmentCatalog();
        if (debrisFragmentCatalog == null ||
            debrisFragmentCatalog.Count <= 0 ||
            fragmentBatch == null ||
            fragmentBatch.Length == 0)
        {
            return null;
        }

        int fragmentIndex = fragmentBatch[index % fragmentBatch.Length];
        return debrisFragmentCatalog.GetFragmentPrefab(fragmentIndex);
    }

    private Vector3 ResolveDebrisFragmentFireOrigin(Transform selectedFirePoint)
    {
        return selectedFirePoint != null ? selectedFirePoint.position : attackController.CurrentFireOrigin;
    }

    private Transform ResolveDebrisFragmentFirePoint()
    {
        CacheDebrisFragmentFirePoints();
        if (debrisFragmentFirePoints.Count <= 0)
        {
            return null;
        }

        int firePointIndex = Random.Range(0, debrisFragmentFirePoints.Count);
        return debrisFragmentFirePoints[firePointIndex];
    }

    private IEnumerator PerformDebrisFragmentStompWindup()
    {
        Transform jumpTransform = bossController != null ? bossController.transform : null;
        if (jumpTransform == null)
        {
            yield return new WaitForSeconds(Mathf.Max(0f, debrisFragmentStompDuration) * Mathf.Max(1, debrisFragmentStompCount));
            yield break;
        }

        float duration = Mathf.Max(0.05f, debrisFragmentStompDuration);
        float jumpHeight = ResolveDebrisFragmentJumpHeight();
        if (jumpHeight <= 0.001f)
        {
            yield return new WaitForSeconds(duration * Mathf.Max(1, debrisFragmentStompCount));
            yield break;
        }

        int stompCount = Mathf.Max(1, debrisFragmentStompCount);
        for (int stompIndex = 0; stompIndex < stompCount; stompIndex++)
        {
            yield return PerformSingleDebrisFragmentStomp(jumpTransform, duration, jumpHeight);
            TriggerDebrisFragmentStompShake();
        }
    }

    private IEnumerator PerformSingleDebrisFragmentStomp(Transform jumpTransform, float duration, float jumpHeight)
    {
        float gravity = 8f * jumpHeight / (duration * duration);
        float initialVelocity = 4f * jumpHeight / duration;
        float baseY = jumpTransform.position.y;
        float elapsed = 0f;
        activeDebrisJumpTransform = jumpTransform;
        activeDebrisJumpBaseY = baseY;
        hasActiveDebrisJump = true;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Min(elapsed, duration);
            float verticalOffset = initialVelocity * t - 0.5f * gravity * t * t;
            Vector3 position = jumpTransform.position;
            position.y = baseY + Mathf.Max(0f, verticalOffset);
            jumpTransform.position = position;
            yield return null;
        }

        Vector3 landedPosition = jumpTransform.position;
        landedPosition.y = baseY;
        jumpTransform.position = landedPosition;
        hasActiveDebrisJump = false;
        activeDebrisJumpTransform = null;
    }

    private float ResolveDebrisFragmentJumpHeight()
    {
        float bossHeight = ResolveBossVisualHeight();
        return Mathf.Max(0.05f, bossHeight * Mathf.Max(0f, debrisFragmentJumpHeightRatio));
    }

    private void TriggerDebrisFragmentStompShake()
    {
        if (debrisFragmentStompShakeDuration <= 0f || debrisFragmentStompShakeAmplitude <= 0f)
        {
            return;
        }

        Transform cameraTransform = ResolveDebrisFragmentShakeTransform();
        if (cameraTransform == null)
        {
            return;
        }

        CancelDebrisFragmentCameraShake();
        activeDebrisCameraShakeRoutine = StartCoroutine(ShakeDebrisFragmentCameraRoutine(
            cameraTransform,
            Mathf.Max(0.01f, debrisFragmentStompShakeDuration),
            Mathf.Max(0.001f, debrisFragmentStompShakeAmplitude)));
    }

    private IEnumerator ShakeDebrisFragmentCameraRoutine(Transform cameraTransform, float duration, float amplitude)
    {
        activeDebrisCameraShakeTransform = cameraTransform;
        activeDebrisCameraShakeBaseLocalPosition = cameraTransform.localPosition;
        float elapsed = 0f;

        while (elapsed < duration && cameraTransform != null)
        {
            elapsed += Time.deltaTime;
            float normalizedTime = Mathf.Clamp01(elapsed / duration);
            float strength = amplitude * (1f - normalizedTime);
            Vector2 offset = Random.insideUnitCircle * strength;
            cameraTransform.localPosition = activeDebrisCameraShakeBaseLocalPosition + new Vector3(offset.x, offset.y, 0f);
            yield return null;
        }

        if (cameraTransform != null)
        {
            cameraTransform.localPosition = activeDebrisCameraShakeBaseLocalPosition;
        }

        activeDebrisCameraShakeRoutine = null;
        activeDebrisCameraShakeTransform = null;
    }

    private Transform ResolveDebrisFragmentShakeTransform()
    {
        Camera mainCamera = Camera.main;
        if (mainCamera != null)
        {
            return mainCamera.transform;
        }

        ArenaCameraRig cameraRig = FindAnyObjectByType<ArenaCameraRig>();
        return cameraRig != null ? cameraRig.transform : null;
    }

    private void CancelDebrisFragmentCameraShake()
    {
        if (activeDebrisCameraShakeRoutine != null)
        {
            StopCoroutine(activeDebrisCameraShakeRoutine);
            activeDebrisCameraShakeRoutine = null;
        }

        if (activeDebrisCameraShakeTransform != null)
        {
            activeDebrisCameraShakeTransform.localPosition = activeDebrisCameraShakeBaseLocalPosition;
            activeDebrisCameraShakeTransform = null;
        }
    }

    private void ResetActiveDebrisJump()
    {
        if (!hasActiveDebrisJump || activeDebrisJumpTransform == null)
        {
            hasActiveDebrisJump = false;
            activeDebrisJumpTransform = null;
            return;
        }

        Vector3 position = activeDebrisJumpTransform.position;
        position.y = activeDebrisJumpBaseY;
        activeDebrisJumpTransform.position = position;
        hasActiveDebrisJump = false;
        activeDebrisJumpTransform = null;
    }

    private float ResolveBossVisualHeight()
    {
        Transform bossTransform = bossController != null ? bossController.transform : transform;
        Renderer[] renderers = bossTransform.GetComponentsInChildren<Renderer>();
        Bounds bounds = default;
        bool hasBounds = false;
        for (int i = 0; i < renderers.Length; i++)
        {
            if (!renderers[i].enabled)
            {
                continue;
            }

            if (!hasBounds)
            {
                bounds = renderers[i].bounds;
                hasBounds = true;
            }
            else
            {
                bounds.Encapsulate(renderers[i].bounds);
            }
        }

        if (!hasBounds)
        {
            return Mathf.Max(1f, bossTransform.lossyScale.y);
        }

        return Mathf.Max(0.1f, bounds.size.y);
    }

    private void CacheDebrisFragmentFirePoints()
    {
        for (int i = debrisFragmentFirePoints.Count - 1; i >= 0; i--)
        {
            if (debrisFragmentFirePoints[i] == null)
            {
                debrisFragmentFirePoints.RemoveAt(i);
            }
        }

        if (debrisFragmentFirePoints.Count > 0)
        {
            return;
        }

        TryAddDebrisFragmentFirePoint(FindDebrisFragmentFirePoint(DebrisFragmentFirePointName1));
        TryAddDebrisFragmentFirePoint(FindDebrisFragmentFirePoint(DebrisFragmentFirePointName2));
    }

    private void TryAddDebrisFragmentFirePoint(Transform firePoint)
    {
        if (firePoint == null || debrisFragmentFirePoints.Contains(firePoint))
        {
            return;
        }

        debrisFragmentFirePoints.Add(firePoint);
    }

    private Transform FindDebrisFragmentFirePoint(string firePointName)
    {
        if (bossController != null)
        {
            Transform childFirePoint = FindChildRecursive(bossController.transform, firePointName);
            if (childFirePoint != null)
            {
                return childFirePoint;
            }
        }

        GameObject firePointObject = GameObject.Find(firePointName);
        return firePointObject != null ? firePointObject.transform : null;
    }

    private static Transform FindChildRecursive(Transform root, string childName)
    {
        if (root == null)
        {
            return null;
        }

        for (int i = 0; i < root.childCount; i++)
        {
            Transform child = root.GetChild(i);
            if (child.name == childName)
            {
                return child;
            }

            Transform nestedChild = FindChildRecursive(child, childName);
            if (nestedChild != null)
            {
                return nestedChild;
            }
        }

        return null;
    }

    private Vector3 ResolveRandomAimPointInPlayerPlane(Vector3 origin, Vector3 center, float minRadius, float maxRadius)
    {
        float clampedMaxRadius = Mathf.Max(0f, maxRadius);
        float clampedMinRadius = Mathf.Clamp(minRadius, 0f, clampedMaxRadius);
        if (clampedMaxRadius <= 0.001f)
        {
            return center;
        }

        Vector3 shotDirection = ResolveSafeDirection(center - origin);
        Vector3 right = Vector3.Cross(Vector3.up, shotDirection);
        if (right.sqrMagnitude <= 0.0001f)
        {
            right = Vector3.right;
        }
        else
        {
            right.Normalize();
        }

        Vector3 up = Vector3.Cross(shotDirection, right);
        if (up.sqrMagnitude <= 0.0001f)
        {
            up = Vector3.up;
        }
        else
        {
            up.Normalize();
        }

        float angle = Random.Range(0f, Mathf.PI * 2f);
        float radius = Mathf.Lerp(clampedMinRadius, clampedMaxRadius, Mathf.Sqrt(Random.value));
        Vector2 offset = new(Mathf.Cos(angle), Mathf.Sin(angle));
        return center + right * offset.x * radius + up * offset.y * radius;
    }

    private static float ResolveAccelerationForTravelTime(float distance, float initialSpeed, float travelDuration)
    {
        float duration = Mathf.Max(0.05f, travelDuration);
        float requiredAcceleration = 2f * (Mathf.Max(0.01f, distance) - Mathf.Max(0f, initialSpeed) * duration) / (duration * duration);
        return Mathf.Max(0f, requiredAcceleration);
    }

    private Vector3 MoveAimPointTowardPlayer(Vector3 currentAimPoint, float playerSpeedMultiplier)
    {
        if (playerCombatController == null)
        {
            return currentAimPoint;
        }

        float trackingSpeed = ResolvePlayerTrackingSpeed(playerSpeedMultiplier);
        Vector3 playerPoint = ResolveTrackingResidualBeamTargetPoint();
        float snapDistance = Mathf.Max(0.05f, Mathf.Min(playerCombatController.HitRadius * 0.25f, ScaleAttackSize(0.2f)));
        if (Vector3.Distance(currentAimPoint, playerPoint) <= snapDistance)
        {
            return playerPoint;
        }

        return Vector3.MoveTowards(currentAimPoint, playerPoint, trackingSpeed * Time.deltaTime);
    }

    private Vector3 ResolveTrackingResidualBeamTargetPoint()
    {
        if (playerOrbitController != null)
        {
            return playerOrbitController.transform.position;
        }

        return playerCombatController != null ? playerCombatController.HitPoint : attackController.CurrentPlayerHitPoint;
    }

    private float ResolvePlayerTrackingSpeed(float playerSpeedMultiplier)
    {
        float playerSpeed = 8f;
        if (playerOrbitController != null)
        {
            playerSpeed = Mathf.Max(
                playerOrbitController.DebugStrafeSpeed,
                playerOrbitController.DebugAltitudeSpeed,
                playerOrbitController.DebugForwardSpeed,
                playerOrbitController.CurrentWorldVelocity.magnitude);
        }

        if (playerSpeed < 0.1f)
        {
            playerSpeed = 8f;
        }

        return playerSpeed * Mathf.Max(0.01f, playerSpeedMultiplier);
    }

    private float ResolveBeamDamageHalfWidth(float visualWidth)
    {
        float playerRadius = playerCombatController != null ? playerCombatController.HitRadius : 0f;
        return Mathf.Max(visualWidth * 0.5f, playerRadius * 0.65f);
    }

    private float ResolveTrackingResidualBeamDamageHalfWidth(float visualWidth)
    {
        float playerRadius = playerCombatController != null ? playerCombatController.HitRadius : 0f;
        return Mathf.Max(visualWidth * 0.5f, playerRadius);
    }

    private void UpdateMouthChargeTelegraph(GameObject chargeWarning, Vector3 origin, float baseRadius, float normalizedTime)
    {
        if (chargeWarning == null)
        {
            return;
        }

        float pulse = 1f + Mathf.Sin(Mathf.Clamp01(normalizedTime) * Mathf.PI * 4f) * 0.16f;
        chargeWarning.transform.position = origin;
        chargeWarning.transform.localScale = Vector3.one * Mathf.Max(0.1f, baseRadius * pulse);
    }

    private GameObject CreateLineTelegraph(
        Vector3 origin,
        Vector3 target,
        float thickness,
        Color color,
        bool useMinimumThickness = true,
        bool scaleThickness = true)
    {
        Vector3 direction = target - origin;
        float length = Mathf.Max(0.1f, direction.magnitude);
        Quaternion rotation = Quaternion.LookRotation(ResolveSafeDirection(direction), Vector3.up);
        Vector3 position = origin + direction * 0.5f;
        float resolvedThickness = scaleThickness ? ScaleAttackSize(thickness) : thickness;
        float scaledThickness = useMinimumThickness
            ? Mathf.Max(minimumTelegraphThickness, resolvedThickness)
            : Mathf.Max(0.01f, resolvedThickness);
        Vector3 scale = new(scaledThickness, scaledThickness, length);
        return CreateTelegraphPrimitive("BossAimTelegraph", PrimitiveType.Cube, position, rotation, scale, color, false);
    }

    private GameObject CreateBeamTelegraph(
        string objectName,
        Vector3 origin,
        Vector3 direction,
        float width,
        float length,
        Color color)
    {
        GameObject beam = CreateTelegraphPrimitive(
            objectName,
            PrimitiveType.Cube,
            origin,
            Quaternion.identity,
            Vector3.one,
            color,
            false);
        UpdateBeamTelegraph(beam, origin, direction, width, length);
        return beam;
    }

    private void UpdateBeamTelegraph(GameObject beam, Vector3 origin, Vector3 direction, float width, float length)
    {
        if (beam == null)
        {
            return;
        }

        Vector3 safeDirection = ResolveSafeDirection(direction);
        float clampedLength = Mathf.Max(0.1f, length);
        beam.transform.SetPositionAndRotation(
            origin + safeDirection * (clampedLength * 0.5f),
            Quaternion.LookRotation(safeDirection, Vector3.up));
        beam.transform.localScale = new Vector3(
            Mathf.Max(0.1f, width),
            Mathf.Max(0.1f, width),
            clampedLength);
    }

    private List<GameObject> CreateShockwaveSegments(Vector3 center, float thickness, int segmentCount)
    {
        List<GameObject> segments = new(segmentCount);
        for (int i = 0; i < segmentCount; i++)
        {
            GameObject segment = CreateTelegraphPrimitive(
                "BossShockwaveRingSegment",
                PrimitiveType.Cube,
                center,
                Quaternion.identity,
                Vector3.one,
                new Color(1f, 0.24f, 0.04f, 0.58f),
                false);
            segments.Add(segment);
        }

        UpdateShockwaveSegments(segments, center, thickness, thickness);
        return segments;
    }

    private void UpdateShockwaveSegments(List<GameObject> segments, Vector3 center, float radius, float thickness)
    {
        if (segments == null || segments.Count == 0)
        {
            return;
        }

        float clampedRadius = Mathf.Max(0.1f, radius);
        float clampedThickness = Mathf.Max(0.1f, thickness);
        float arcLength = Mathf.Max(clampedThickness, (Mathf.PI * 2f * clampedRadius / segments.Count) * 0.72f);

        for (int i = 0; i < segments.Count; i++)
        {
            GameObject segment = segments[i];
            if (segment == null)
            {
                continue;
            }

            float angle = (360f / segments.Count) * i;
            Vector3 radial = Quaternion.Euler(0f, angle, 0f) * Vector3.forward;
            Vector3 tangent = Quaternion.Euler(0f, 90f, 0f) * radial;
            segment.transform.SetPositionAndRotation(
                center + radial * clampedRadius,
                Quaternion.LookRotation(tangent, Vector3.up));
            segment.transform.localScale = new Vector3(clampedThickness, 0.08f, arcLength);
        }
    }

    private Vector3 ResolveShockwaveCenter()
    {
        Vector3 center = attackController.CurrentBossCenter;
        center.y = playerCombatController.HitPoint.y;
        return center;
    }

    private void TryApplyLineHazardDamage(Vector3 start, Vector3 end, float halfWidth, float damage)
    {
        Vector3 closestPoint = ClosestPointOnSegment(playerCombatController.HitPoint, start, end);
        if (playerCombatController.CheckHit(closestPoint, Mathf.Max(0f, halfWidth)))
        {
            playerCombatController.ApplyDamage(damage);
        }
    }

    private void TryApplyTrackingResidualBeamDamage(Vector3 start, Vector3 end, float halfWidth, ref float damageTickElapsed)
    {
        Vector3 closestPoint = ClosestPointOnSegment(playerCombatController.HitPoint, start, end);
        if (!playerCombatController.CheckHit(closestPoint, Mathf.Max(0f, halfWidth)))
        {
            damageTickElapsed = 0f;
            return;
        }

        damageTickElapsed += Time.deltaTime;
        while (damageTickElapsed >= TrackingResidualBeamDamageTickInterval)
        {
            if (!playerCombatController.ApplyContinuousDamage(TrackingResidualBeamDamagePerTick))
            {
                break;
            }

            damageTickElapsed -= TrackingResidualBeamDamageTickInterval;
        }
    }

    private void TryApplyShockwaveDamage(Vector3 center, float radius, float thickness, float height, float damage)
    {
        Vector3 playerPoint = playerCombatController.HitPoint;
        float halfHeight = Mathf.Max(0.1f, height) * 0.5f;
        if (Mathf.Abs(playerPoint.y - center.y) > halfHeight + playerCombatController.HitRadius)
        {
            return;
        }

        Vector3 flatOffset = new(playerPoint.x - center.x, 0f, playerPoint.z - center.z);
        if (flatOffset.sqrMagnitude < 0.001f)
        {
            flatOffset = Vector3.forward;
        }

        Vector3 closestPoint = center + flatOffset.normalized * Mathf.Max(0.1f, radius);
        closestPoint.y = playerPoint.y;
        if (playerCombatController.CheckHit(closestPoint, Mathf.Max(0f, thickness * 0.5f)))
        {
            playerCombatController.ApplyDamage(damage);
        }
    }

    private bool DidInterruptHeavyShot(float startingBossHealth, float interruptDamageThreshold)
    {
        if (bossController == null || interruptDamageThreshold <= 0f)
        {
            return false;
        }

        return startingBossHealth - bossController.CurrentHealth >= interruptDamageThreshold;
    }

    private void UpdateInterruptTelegraphs(
        GameObject weakPointWarning,
        GameObject shotWarning,
        Vector3 lockedTarget,
        BossBulletPatternDefinition pattern)
    {
        if (weakPointWarning != null)
        {
            weakPointWarning.transform.position = attackController.CurrentBossCenter;
        }

        UpdateBeamTelegraph(
            shotWarning,
            attackController.CurrentFireOrigin,
            lockedTarget - attackController.CurrentFireOrigin,
            ScaleAttackSize(Mathf.Max(warningLineThickness, pattern.warningWidth * 0.18f)),
            Mathf.Max(0.1f, Vector3.Distance(attackController.CurrentFireOrigin, lockedTarget)));
    }

    private GameObject CreateLaneTelegraph(Vector3 target, BossBulletPatternDefinition pattern)
    {
        Vector3 position = new(target.x, target.y, target.z);
        Vector3 scale = new(
            ScaleAttackSize(pattern.warningWidth),
            ScaleAttackSize(pattern.warningHeight),
            ScaleAttackSize(pattern.warningDepth));
        return CreateTelegraphPrimitive(
            "BossFallingBombTelegraph",
            PrimitiveType.Cube,
            position,
            Quaternion.identity,
            scale,
            new Color(1f, 0.85f, 0.15f, 0.35f),
            true);
    }

    private GameObject CreateTelegraphPrimitive(
        string objectName,
        PrimitiveType primitiveType,
        Vector3 position,
        Quaternion rotation,
        Vector3 scale,
        Color color,
        bool keepCollider)
    {
        GameObject telegraphObject = GameObject.CreatePrimitive(primitiveType);
        telegraphObject.name = objectName;
        telegraphObject.transform.SetPositionAndRotation(position, rotation);
        telegraphObject.transform.localScale = scale;

        Collider telegraphCollider = telegraphObject.GetComponent<Collider>();
        if (telegraphCollider != null)
        {
            if (!keepCollider)
            {
                Destroy(telegraphCollider);
            }
            else
            {
                telegraphCollider.enabled = true;
            }
        }

        Renderer telegraphRenderer = telegraphObject.GetComponent<Renderer>();
        if (telegraphRenderer != null)
        {
            telegraphRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            telegraphRenderer.receiveShadows = false;
            telegraphRenderer.material = CreateTelegraphMaterialInstance(color);
        }

        runtimeTelegraphs.Add(telegraphObject);
        return telegraphObject;
    }

    private Material CreateTelegraphMaterialInstance(Color color)
    {
        Material template = GetOrCreateTelegraphMaterialTemplate();
        if (template == null)
        {
            return null;
        }

        Material material = new(template) { color = color, hideFlags = HideFlags.HideAndDontSave };
        return material;
    }

    private Material GetOrCreateTelegraphMaterialTemplate()
    {
        if (telegraphMaterialTemplate != null)
        {
            return telegraphMaterialTemplate;
        }

        Shader shader = Shader.Find("Universal Render Pipeline/Unlit");
        shader ??= Shader.Find("Unlit/Color");
        shader ??= Shader.Find("Sprites/Default");
        if (shader == null)
        {
            return null;
        }

        telegraphMaterialTemplate = new Material(shader)
        {
            name = "RuntimeBossTelegraphMaterial",
            hideFlags = HideFlags.HideAndDontSave,
            color = new Color(1f, 0.8f, 0.2f, 0.35f),
        };

        if (telegraphMaterialTemplate.HasProperty("_Surface"))
        {
            telegraphMaterialTemplate.SetFloat("_Surface", 1f);
        }

        if (telegraphMaterialTemplate.HasProperty("_Blend"))
        {
            telegraphMaterialTemplate.SetFloat("_Blend", 0f);
        }

        if (telegraphMaterialTemplate.HasProperty("_SrcBlend"))
        {
            telegraphMaterialTemplate.SetFloat("_SrcBlend", 5f);
        }

        if (telegraphMaterialTemplate.HasProperty("_DstBlend"))
        {
            telegraphMaterialTemplate.SetFloat("_DstBlend", 10f);
        }

        if (telegraphMaterialTemplate.HasProperty("_ZWrite"))
        {
            telegraphMaterialTemplate.SetFloat("_ZWrite", 0f);
        }

        telegraphMaterialTemplate.renderQueue = 3000;
        return telegraphMaterialTemplate;
    }

    private void SetTelegraphColor(GameObject telegraphObject, Color color)
    {
        if (telegraphObject == null)
        {
            return;
        }

        Renderer telegraphRenderer = telegraphObject.GetComponent<Renderer>();
        if (telegraphRenderer == null || telegraphRenderer.material == null)
        {
            return;
        }

        telegraphRenderer.material.color = color;
    }

    private void DestroyTelegraph(GameObject telegraphObject)
    {
        if (telegraphObject == null)
        {
            return;
        }

        runtimeTelegraphs.Remove(telegraphObject);
        Destroy(telegraphObject);
    }

    private void CleanupTelegraphs()
    {
        for (int i = runtimeTelegraphs.Count - 1; i >= 0; i--)
        {
            GameObject telegraphObject = runtimeTelegraphs[i];
            if (telegraphObject != null)
            {
                Destroy(telegraphObject);
            }
        }

        runtimeTelegraphs.Clear();
    }

    private void DestroyTelegraphs(List<GameObject> telegraphs)
    {
        if (telegraphs == null)
        {
            return;
        }

        for (int i = telegraphs.Count - 1; i >= 0; i--)
        {
            DestroyTelegraph(telegraphs[i]);
        }

        telegraphs.Clear();
    }

    private void CancelActivePattern()
    {
        ResetActiveDebrisJump();
        CancelDebrisFragmentCameraShake();

        if (activePatternRoutine == null)
        {
            return;
        }

        StopCoroutine(activePatternRoutine);
        activePatternRoutine = null;
        preserveActiveTelegraphUntilPatternEnds = false;
    }

    private float ResolveCooldown(float patternCooldownMultiplier)
    {
        float baseCooldown = attackController != null ? attackController.CurrentAttackInterval : 1f;
        return Mathf.Max(0.1f, baseCooldown * Mathf.Max(0.1f, patternCooldownMultiplier));
    }

    private float ResolvePrimarySpeed(BossBulletPatternDefinition pattern)
    {
        return attackController.BaseProjectileSpeed * Mathf.Max(0.1f, pattern.speedMultiplier);
    }

    private float ResolveSecondarySpeed(BossBulletPatternDefinition pattern)
    {
        return attackController.BaseProjectileSpeed * Mathf.Max(0.1f, pattern.secondarySpeedMultiplier);
    }

    private float ResolvePrimaryDamage(BossBulletPatternDefinition pattern)
    {
        return attackController.BaseProjectileDamage * Mathf.Max(0.1f, pattern.damageMultiplier);
    }

    private float ResolveSecondaryDamage(BossBulletPatternDefinition pattern)
    {
        return attackController.BaseProjectileDamage * Mathf.Max(0.1f, pattern.secondaryDamageMultiplier);
    }

    private float ScaleAttackSize(float value)
    {
        return value * Mathf.Max(0.1f, attackSizeMultiplier);
    }

    private bool TryResolveScreenHorizontalSweepDirections(
        Vector3 origin,
        bool leftToRight,
        out Vector3 startDirection,
        out Vector3 endDirection)
    {
        startDirection = Vector3.forward;
        endDirection = Vector3.forward;

        Camera camera = Camera.main;
        if (camera == null || playerCombatController == null)
        {
            return false;
        }

        Vector3 playerPoint = playerCombatController.HitPoint;
        Vector3 playerViewportPoint = camera.WorldToViewportPoint(playerPoint);
        if (playerViewportPoint.z <= 0.05f)
        {
            return false;
        }

        float viewportY = Mathf.Clamp(playerViewportPoint.y, 0.08f, 0.92f);
        float viewportDepth = Mathf.Max(0.1f, playerViewportPoint.z);
        Vector3 leftPoint = camera.ViewportToWorldPoint(new Vector3(-0.08f, viewportY, viewportDepth));
        Vector3 rightPoint = camera.ViewportToWorldPoint(new Vector3(1.08f, viewportY, viewportDepth));
        Vector3 startPoint = leftToRight ? leftPoint : rightPoint;
        Vector3 endPoint = leftToRight ? rightPoint : leftPoint;

        startDirection = ResolveSafeDirection(startPoint - origin);
        endDirection = ResolveSafeDirection(endPoint - origin);
        return true;
    }

    private static Vector3 ResolveSafeDirection(Vector3 direction)
    {
        return direction.sqrMagnitude > 0.0001f ? direction.normalized : Vector3.forward;
    }

    private static Vector3 ClosestPointOnSegment(Vector3 point, Vector3 start, Vector3 end)
    {
        Vector3 segment = end - start;
        float segmentLengthSqr = segment.sqrMagnitude;
        if (segmentLengthSqr <= 0.0001f)
        {
            return start;
        }

        float t = Mathf.Clamp01(Vector3.Dot(point - start, segment) / segmentLengthSqr);
        return start + segment * t;
    }
}

public class BossSplitProjectileRuntime : MonoBehaviour
{
    private BossAttackController attackController;
    private ProjectileController projectileController;
    private Vector3 startPosition;
    private float splitDistance;
    private float splitAngle;
    private float childSpeed;
    private float childDamage;
    private int childProjectileCount;
    private bool hasSplit;

    public void Configure(
        BossAttackController owner,
        int childCount,
        float childSpreadAngle,
        float distanceBeforeSplit,
        float childProjectileSpeed,
        float childProjectileDamage)
    {
        attackController = owner;
        projectileController = GetComponent<ProjectileController>();
        startPosition = transform.position;
        childProjectileCount = Mathf.Max(1, childCount);
        splitAngle = childSpreadAngle;
        splitDistance = Mathf.Max(0.1f, distanceBeforeSplit);
        childSpeed = Mathf.Max(0.1f, childProjectileSpeed);
        childDamage = Mathf.Max(0.1f, childProjectileDamage);
    }

    private void Update()
    {
        if (hasSplit)
        {
            return;
        }

        if (attackController == null || projectileController == null)
        {
            Destroy(this);
            return;
        }

        if ((transform.position - startPosition).sqrMagnitude < splitDistance * splitDistance)
        {
            return;
        }

        hasSplit = true;
        Vector3 origin = transform.position;
        Quaternion centerRotation = Quaternion.LookRotation(transform.forward, Vector3.up);
        float step = childProjectileCount > 1 ? splitAngle / (childProjectileCount - 1) : 0f;
        float start = -splitAngle * 0.5f;

        for (int i = 0; i < childProjectileCount; i++)
        {
            float angle = start + step * i;
            Vector3 direction = centerRotation * Quaternion.AngleAxis(angle, Vector3.up) * Vector3.forward;
            attackController.SpawnProjectile(origin, direction, childSpeed, childDamage, "BossSplitChildRuntime", spawnCosmeticBurst: false);
        }

        Destroy(gameObject);
    }
}

public class BossProjectileApproachScaleRuntime : MonoBehaviour
{
    private ProjectileController projectileController;
    private Vector3 startPosition;
    private Vector3 targetPosition;
    private Vector3 fullScale;
    private float fullFallbackHitRadiusMultiplier;
    private float startScale;
    private float endScale;
    private float travelDistance;
    private Vector3 moveDirection;
    private float initialSpeed;
    private float acceleration;
    private float elapsed;

    public void Configure(
        ProjectileController projectile,
        Vector3 origin,
        Vector3 target,
        float resolvedFullScaleMultiplier,
        float startScaleMultiplier,
        float endScaleMultiplier,
        Vector3 direction,
        float initialProjectileSpeed,
        float projectileAcceleration)
    {
        projectileController = projectile;
        startPosition = origin;
        targetPosition = target;
        fullScale = transform.localScale;
        fullFallbackHitRadiusMultiplier = Mathf.Max(0.01f, resolvedFullScaleMultiplier);
        startScale = Mathf.Max(0.01f, startScaleMultiplier);
        endScale = Mathf.Max(startScale, endScaleMultiplier);
        travelDistance = Mathf.Max(0.01f, Vector3.Distance(startPosition, targetPosition));
        moveDirection = direction.sqrMagnitude > 0.0001f ? direction.normalized : transform.forward;
        initialSpeed = Mathf.Max(0f, initialProjectileSpeed);
        acceleration = Mathf.Max(0f, projectileAcceleration);
        elapsed = 0f;
        projectileController.SetVelocityForRuntime(moveDirection, initialSpeed);
        ApplyScale(startScale);
    }

    private void Update()
    {
        if (projectileController == null)
        {
            Destroy(this);
            return;
        }

        elapsed += Time.deltaTime;
        projectileController.SetVelocityForRuntime(moveDirection, initialSpeed + acceleration * elapsed);

        float remainingDistance = Vector3.Distance(transform.position, targetPosition);
        float progress = 1f - Mathf.Clamp01(remainingDistance / travelDistance);
        float easedProgress = Mathf.SmoothStep(0f, 1f, progress);
        ApplyScale(Mathf.Lerp(startScale, endScale, easedProgress));
    }

    private void ApplyScale(float scaleMultiplier)
    {
        transform.localScale = fullScale * Mathf.Max(0.01f, scaleMultiplier);
        if (projectileController != null)
        {
            projectileController.SetFallbackHitRadiusMultiplier(fullFallbackHitRadiusMultiplier * Mathf.Max(0.01f, scaleMultiplier));
        }
    }
}
