using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

[DefaultExecutionOrder(1000)]
public class BattleDebugTuningApplier : MonoBehaviour
{
    public PlayerCombatController PlayerCombat { get; private set; }
    public PlayerOrbitController PlayerOrbit { get; private set; }
    public PlayerMovementBounds PlayerMovementBounds { get; private set; }
    public BossController Boss { get; private set; }
    public BossAttackController BossAttack { get; private set; }
    public BossBulletPatternController BossPatterns { get; private set; }

    private void OnEnable()
    {
        BattleDebugTuningState.ValueChanged += HandleValueChanged;
        BattleDebugTuningState.PatternValueChanged += HandlePatternValueChanged;
    }

    private IEnumerator Start()
    {
        // BattleController has execution order 200. Waiting one frame ensures its
        // Start configuration has copied selected vehicle and encounter values first.
        yield return null;
        ResolveTargets();
        ApplyInitialState();
    }

    private void OnDisable()
    {
        BattleDebugTuningState.ValueChanged -= HandleValueChanged;
        BattleDebugTuningState.PatternValueChanged -= HandlePatternValueChanged;
    }

    public void ResolveTargets()
    {
        PlayerCombat = PlayerCombat != null ? PlayerCombat : FindAnyObjectByType<PlayerCombatController>();
        PlayerOrbit = PlayerOrbit != null ? PlayerOrbit : FindAnyObjectByType<PlayerOrbitController>();
        PlayerMovementBounds = PlayerMovementBounds != null ? PlayerMovementBounds : FindAnyObjectByType<PlayerMovementBounds>();
        Boss = Boss != null ? Boss : FindAnyObjectByType<BossController>();
        BossAttack = BossAttack != null ? BossAttack : FindAnyObjectByType<BossAttackController>();
        BossPatterns = BossPatterns != null ? BossPatterns : FindAnyObjectByType<BossBulletPatternController>();
    }

    public void ApplyInitialState()
    {
        ResolveTargets();
        ApplyBasePlayerStats(refillDefense: true);
        ApplyAllOverrides(refillPlayerDefense: true);
    }

    public void ApplyBasePlayerStats(bool refillDefense)
    {
        if (PlayerCombat == null)
        {
            return;
        }

        PlayerRuntimeStats stats = PlayerRuntimeState.ResolveStats();
        PlayerCombat.ApplyRuntimeStats(stats, refillDefense);
        if (PlayerOrbit != null)
        {
            PlayerOrbit.SetMovementSpeedsForDebug(stats.StrafeSpeed, stats.AltitudeSpeed, stats.ForwardSpeed);
        }
    }

    public void RefillPlayerForDebug()
    {
        ResolveTargets();
        PlayerCombat?.RefillForDebug();
    }

    public void FullHealBossForDebug()
    {
        ResolveTargets();
        Boss?.FullHealForDebug();
    }

    public void ApplyAllOverrides(bool refillPlayerDefense)
    {
        ApplyDebugTogglesAndVisuals();
        ApplyPlayerAttack();
        ApplyPlayerMissile();
        ApplyPlayerDefense(refillPlayerDefense);
        ApplyPlayerMovement();
        ApplyBossHealth();
        ApplyBossAttack();
        ApplyBossPatternTiming();
        ApplyAllPatternOverrides();
    }

    private void HandleValueChanged(BattleTuningKey key)
    {
        ResolveTargets();
        switch (key)
        {
            case BattleTuningKey.Undead:
            case BattleTuningKey.ShowDamageHurtbox:
            case BattleTuningKey.ShowMovementBoundsGuide:
                ApplyDebugTogglesAndVisuals();
                break;

            case BattleTuningKey.PlayerFireCooldown:
            case BattleTuningKey.PlayerProjectileSpeed:
            case BattleTuningKey.PlayerProjectileDamage:
            case BattleTuningKey.PlayerInvulnerabilityDuration:
            case BattleTuningKey.PlayerHitRadius:
                ApplyPlayerAttack();
                break;

            case BattleTuningKey.PlayerMissileLaunchSpeed:
            case BattleTuningKey.PlayerMissileCruiseSpeed:
            case BattleTuningKey.PlayerMissileAcceleration:
            case BattleTuningKey.PlayerMissileTurnRate:
            case BattleTuningKey.PlayerMissileLockOnDelay:
            case BattleTuningKey.PlayerMissileStraightPhaseDuration:
            case BattleTuningKey.PlayerMissileStraightPhaseDistance:
            case BattleTuningKey.PlayerMissileTurnPhaseDuration:
            case BattleTuningKey.PlayerMissileBoostPhaseDuration:
            case BattleTuningKey.PlayerMissileLifetime:
            case BattleTuningKey.PlayerMissileHitRadius:
                ApplyPlayerMissile();
                break;

            case BattleTuningKey.PlayerMaxHull:
            case BattleTuningKey.PlayerMaxArmor:
            case BattleTuningKey.PlayerRepairRate:
            case BattleTuningKey.PlayerRepairDelay:
            case BattleTuningKey.PlayerBrokenRecoverThreshold:
            case BattleTuningKey.PlayerHullDamageMultiplierWhenBroken:
                ApplyPlayerDefense(refill: true);
                break;

            case BattleTuningKey.PlayerStrafeSpeed:
            case BattleTuningKey.PlayerAltitudeSpeed:
            case BattleTuningKey.PlayerForwardSpeed:
            case BattleTuningKey.MovementBoundsX:
            case BattleTuningKey.MovementBoundsY:
            case BattleTuningKey.MovementBoundsZ:
                ApplyPlayerMovement();
                break;

            case BattleTuningKey.BossMaxHealth:
            case BattleTuningKey.BossCurrentHealth:
            case BattleTuningKey.BossHitRadius:
            case BattleTuningKey.BossIdleBobAmplitude:
            case BattleTuningKey.BossIdleBobSpeed:
                ApplyBossHealth();
                break;

            case BattleTuningKey.BossBaseAttackInterval:
            case BattleTuningKey.BossEnragedAttackInterval:
            case BattleTuningKey.BossProjectileSpeed:
            case BattleTuningKey.BossProjectileDamage:
            case BattleTuningKey.BossProjectileScaleMultiplier:
                ApplyBossAttack();
                break;

            case BattleTuningKey.BossPatternStartupDelay:
            case BattleTuningKey.BossPatternAimedBurstShotInterval:
            case BattleTuningKey.BossPatternWarningLineThickness:
            case BattleTuningKey.BossPatternAttackSizeMultiplier:
            case BattleTuningKey.BossPatternMinimumTelegraphThickness:
                ApplyBossPatternTiming();
                break;
        }
    }

    private void HandlePatternValueChanged(int patternIndex, BossPatternTuningKey key)
    {
        ResolveTargets();
        ApplyPatternOverride(patternIndex, key);
    }

    private void ApplyDebugTogglesAndVisuals()
    {
        if (BattleDebugTuningState.TryGetBool(BattleTuningKey.Undead, out bool undead))
        {
            GameplayDebugFlags.Undead = undead;
            if (undead)
            {
                PlayerCombat?.RefillForDebug();
            }
        }

        if (PlayerCombat != null &&
            BattleDebugTuningState.TryGetBool(BattleTuningKey.ShowDamageHurtbox, out bool showDamageHurtbox))
        {
            PlayerCombat.SetDamageHurtboxDebugVisibleForDebug(showDamageHurtbox);
        }

        if (PlayerMovementBounds != null &&
            BattleDebugTuningState.TryGetBool(BattleTuningKey.ShowMovementBoundsGuide, out bool showMovementGuide))
        {
            PlayerMovementBounds.SetRuntimeGuideVisibleForDebug(showMovementGuide);
        }
    }

    private void ApplyPlayerAttack()
    {
        if (PlayerCombat == null)
        {
            return;
        }

        PlayerCombat.SetFireCooldownForDebug(ResolveFloat(BattleTuningKey.PlayerFireCooldown, PlayerCombat.DebugFireCooldown));
        PlayerCombat.SetProjectileSpeedForDebug(ResolveFloat(BattleTuningKey.PlayerProjectileSpeed, PlayerCombat.DebugProjectileSpeed));
        PlayerCombat.SetProjectileDamageForDebug(ResolveFloat(BattleTuningKey.PlayerProjectileDamage, PlayerCombat.DebugProjectileDamage));
        PlayerCombat.SetInvulnerabilityDurationForDebug(ResolveFloat(BattleTuningKey.PlayerInvulnerabilityDuration, PlayerCombat.DebugInvulnerabilityDuration));
        PlayerCombat.SetHitRadiusForDebug(ResolveFloat(BattleTuningKey.PlayerHitRadius, PlayerCombat.HitRadius));
    }

    private void ApplyPlayerMissile()
    {
        if (PlayerCombat == null)
        {
            return;
        }

        PlayerCombat.SetMissileFlightTuningForDebug(
            ResolveFloat(BattleTuningKey.PlayerMissileLaunchSpeed, PlayerCombat.DebugMissileLaunchSpeed),
            ResolveFloat(BattleTuningKey.PlayerMissileCruiseSpeed, PlayerCombat.DebugMissileCruiseSpeed),
            ResolveFloat(BattleTuningKey.PlayerMissileAcceleration, PlayerCombat.DebugMissileAcceleration),
            ResolveFloat(BattleTuningKey.PlayerMissileTurnRate, PlayerCombat.DebugMissileTurnRate),
            ResolveFloat(BattleTuningKey.PlayerMissileLockOnDelay, PlayerCombat.DebugMissileLockOnDelay),
            ResolveFloat(BattleTuningKey.PlayerMissileStraightPhaseDuration, PlayerCombat.DebugMissileStraightPhaseDuration),
            ResolveFloat(BattleTuningKey.PlayerMissileStraightPhaseDistance, PlayerCombat.DebugMissileStraightPhaseDistance),
            ResolveFloat(BattleTuningKey.PlayerMissileTurnPhaseDuration, PlayerCombat.DebugMissileTurnPhaseDuration),
            ResolveFloat(BattleTuningKey.PlayerMissileBoostPhaseDuration, PlayerCombat.DebugMissileBoostPhaseDuration),
            ResolveFloat(BattleTuningKey.PlayerMissileLifetime, PlayerCombat.DebugMissileLifetime),
            ResolveFloat(BattleTuningKey.PlayerMissileHitRadius, PlayerCombat.DebugMissileHitRadius));
    }

    private void ApplyPlayerDefense(bool refill)
    {
        if (PlayerCombat == null)
        {
            return;
        }

        PlayerCombat.SetDefenseTuningForDebug(
            ResolveFloat(BattleTuningKey.PlayerMaxHull, PlayerCombat.MaxHull),
            ResolveFloat(BattleTuningKey.PlayerMaxArmor, PlayerCombat.MaxArmor),
            ResolveFloat(BattleTuningKey.PlayerRepairRate, PlayerCombat.DebugArmorRepairRate),
            ResolveFloat(BattleTuningKey.PlayerRepairDelay, PlayerCombat.DebugArmorRepairDelay),
            ResolveFloat(BattleTuningKey.PlayerBrokenRecoverThreshold, PlayerCombat.DebugBrokenRecoverThreshold),
            ResolveFloat(BattleTuningKey.PlayerHullDamageMultiplierWhenBroken, PlayerCombat.DebugHullDamageMultiplierWhenBroken),
            refill);
    }

    private void ApplyPlayerMovement()
    {
        if (PlayerOrbit != null)
        {
            PlayerOrbit.SetMovementSpeedsForDebug(
                ResolveFloat(BattleTuningKey.PlayerStrafeSpeed, PlayerOrbit.DebugStrafeSpeed),
                ResolveFloat(BattleTuningKey.PlayerAltitudeSpeed, PlayerOrbit.DebugAltitudeSpeed),
                ResolveFloat(BattleTuningKey.PlayerForwardSpeed, PlayerOrbit.DebugForwardSpeed));
        }

        if (PlayerMovementBounds != null)
        {
            Vector3 extents = PlayerMovementBounds.DebugHalfExtents;
            extents.x = ResolveFloat(BattleTuningKey.MovementBoundsX, extents.x);
            extents.y = ResolveFloat(BattleTuningKey.MovementBoundsY, extents.y);
            extents.z = ResolveFloat(BattleTuningKey.MovementBoundsZ, extents.z);
            PlayerMovementBounds.SetHalfExtentsForDebug(extents);
        }
    }

    private void ApplyBossHealth()
    {
        if (Boss == null)
        {
            return;
        }

        Boss.SetMaxHealthForDebug(ResolveFloat(BattleTuningKey.BossMaxHealth, Boss.MaxHealth), refill: false);
        Boss.SetCurrentHealthForDebug(ResolveFloat(BattleTuningKey.BossCurrentHealth, Boss.CurrentHealth));
        Boss.SetHitRadiusForDebug(ResolveFloat(BattleTuningKey.BossHitRadius, Boss.HitRadius));
        Boss.SetIdleBobForDebug(
            ResolveFloat(BattleTuningKey.BossIdleBobAmplitude, Boss.DebugIdleBobAmplitude),
            ResolveFloat(BattleTuningKey.BossIdleBobSpeed, Boss.DebugIdleBobSpeed));
    }

    private void ApplyBossAttack()
    {
        if (BossAttack == null)
        {
            return;
        }

        BossAttack.SetAttackTimingForDebug(
            ResolveFloat(BattleTuningKey.BossBaseAttackInterval, BossAttack.DebugBaseAttackInterval),
            ResolveFloat(BattleTuningKey.BossEnragedAttackInterval, BossAttack.DebugEnragedAttackInterval));
        BossAttack.SetProjectileTuningForDebug(
            ResolveFloat(BattleTuningKey.BossProjectileSpeed, BossAttack.BaseProjectileSpeed),
            ResolveFloat(BattleTuningKey.BossProjectileDamage, BossAttack.BaseProjectileDamage));
        BossAttack.SetProjectileScaleMultiplierForDebug(
            ResolveFloat(BattleTuningKey.BossProjectileScaleMultiplier, BossAttack.DebugProjectileScaleMultiplier));
    }

    private void ApplyBossPatternTiming()
    {
        if (BossPatterns == null)
        {
            return;
        }

        BossPatterns.SetTimingForDebug(
            ResolveFloat(BattleTuningKey.BossPatternStartupDelay, BossPatterns.DebugStartupDelay),
            ResolveFloat(BattleTuningKey.BossPatternAimedBurstShotInterval, BossPatterns.DebugAimedBurstShotInterval),
            ResolveFloat(BattleTuningKey.BossPatternWarningLineThickness, BossPatterns.DebugWarningLineThickness),
            ResolveFloat(BattleTuningKey.BossPatternAttackSizeMultiplier, BossPatterns.DebugAttackSizeMultiplier),
            ResolveFloat(BattleTuningKey.BossPatternMinimumTelegraphThickness, BossPatterns.DebugMinimumTelegraphThickness));
    }

    private void ApplyAllPatternOverrides()
    {
        if (BossPatterns == null)
        {
            return;
        }

        int patternCount = BossPatterns.DebugPatternSequence.Count;
        for (int patternIndex = 0; patternIndex < patternCount; patternIndex++)
        {
            foreach (BossPatternTuningKey key in System.Enum.GetValues(typeof(BossPatternTuningKey)))
            {
                ApplyPatternOverride(patternIndex, key);
            }
        }
    }

    private void ApplyPatternOverride(int patternIndex, BossPatternTuningKey key)
    {
        if (BossPatterns == null)
        {
            return;
        }

        if (BattleDebugTuningState.TryGetPatternBool(patternIndex, key, out bool boolValue))
        {
            if (key == BossPatternTuningKey.Enabled)
            {
                BossPatterns.SetPatternEnabledForDebug(patternIndex, boolValue);
            }

            return;
        }

        if (BattleDebugTuningState.TryGetPatternInt(patternIndex, key, out int intValue))
        {
            BossPatterns.SetPatternIntForDebug(patternIndex, key, intValue);
            return;
        }

        if (BattleDebugTuningState.TryGetPatternFloat(patternIndex, key, out float floatValue))
        {
            BossPatterns.SetPatternFloatForDebug(patternIndex, key, floatValue);
        }
    }

    private static float ResolveFloat(BattleTuningKey key, float fallback)
    {
        return BattleDebugTuningState.TryGetFloat(key, out float value) ? value : fallback;
    }
}

public static class BattleDebugSceneBootstrap
{
    private const string RuntimeObjectName = "__BattleDebugRuntime";
    private static bool registered;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Register()
    {
        if (registered)
        {
            return;
        }

        registered = true;
        SceneManager.sceneLoaded += HandleSceneLoaded;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void InstallForInitialScene()
    {
        InstallForScene(SceneManager.GetActiveScene());
    }

    private static void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        InstallForScene(scene);
    }

    private static void InstallForScene(Scene scene)
    {
        if (!ShouldInstallDebugRuntime() || !scene.IsValid() || scene.name != GameFlowController.BattleSceneName)
        {
            return;
        }

        BattleDebugTuningApplier[] existingAppliers =
            Object.FindObjectsByType<BattleDebugTuningApplier>(FindObjectsInactive.Include);
        for (int i = 0; i < existingAppliers.Length; i++)
        {
            if (existingAppliers[i] != null && existingAppliers[i].gameObject.scene == scene)
            {
                return;
            }
        }

        GameObject runtimeObject = new(RuntimeObjectName);
        SceneManager.MoveGameObjectToScene(runtimeObject, scene);
        runtimeObject.AddComponent<BattleDebugTuningApplier>();
        runtimeObject.AddComponent<BattleDebugPanel>();
    }

    private static bool ShouldInstallDebugRuntime()
    {
        return Application.isEditor || Debug.isDebugBuild;
    }
}
