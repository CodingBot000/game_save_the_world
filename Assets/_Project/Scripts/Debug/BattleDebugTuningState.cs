using System;
using System.Collections.Generic;
using UnityEngine;

public enum BattleTuningKey
{
    IgnoreMissileCooldown,
    Undead,
    ShowDamageHurtbox,
    ShowMovementBoundsGuide,

    PlayerFireCooldown,
    PlayerProjectileSpeed,
    PlayerProjectileDamage,
    PlayerInvulnerabilityDuration,
    PlayerHitRadius,

    PlayerMissileCooldown,
    PlayerMissileDamage,
    PlayerMissileLaunchSpeed,
    PlayerMissileCruiseSpeed,
    PlayerMissileAcceleration,
    PlayerMissileTurnRate,
    PlayerMissileLockOnDelay,
    PlayerMissileStraightPhaseDuration,
    PlayerMissileStraightPhaseDistance,
    PlayerMissileTurnPhaseDuration,
    PlayerMissileBoostPhaseDuration,
    PlayerMissileLifetime,
    PlayerMissileHitRadius,

    PlayerMaxHull,
    PlayerMaxArmor,
    PlayerRepairRate,
    PlayerRepairDelay,
    PlayerBrokenRecoverThreshold,
    PlayerHullDamageMultiplierWhenBroken,

    PlayerStrafeSpeed,
    PlayerAltitudeSpeed,
    PlayerForwardSpeed,
    MovementBoundsX,
    MovementBoundsY,
    MovementBoundsZ,

    BossMaxHealth,
    BossCurrentHealth,
    BossHitRadius,
    BossIdleBobAmplitude,
    BossIdleBobSpeed,

    BossBaseAttackInterval,
    BossEnragedAttackInterval,
    BossProjectileSpeed,
    BossProjectileDamage,

    BossPatternStartupDelay,
    BossPatternAimedBurstShotInterval,
    BossPatternWarningLineThickness,
}

public enum BossPatternTuningKey
{
    Enabled,
    MinHealthRatio,
    MaxHealthRatio,
    CooldownMultiplier,
    ProjectileCount,
    SecondaryProjectileCount,
    BurstCount,
    BurstInterval,
    SpreadAngle,
    SpeedMultiplier,
    SecondarySpeedMultiplier,
    DamageMultiplier,
    SecondaryDamageMultiplier,
    RingRotationStep,
    TelegraphDuration,
    FlashingDuration,
    WarningWidth,
    WarningHeight,
    WarningDepth,
    OverheadHeight,
    SplitDistance,
}

public static class BattleDebugTuningState
{
    private static readonly Dictionary<BattleTuningKey, float> floatOverrides = new();
    private static readonly Dictionary<BattleTuningKey, int> intOverrides = new();
    private static readonly Dictionary<BattleTuningKey, bool> boolOverrides = new();
    private static readonly Dictionary<int, Dictionary<BossPatternTuningKey, float>> patternFloatOverrides = new();
    private static readonly Dictionary<int, Dictionary<BossPatternTuningKey, int>> patternIntOverrides = new();
    private static readonly Dictionary<int, Dictionary<BossPatternTuningKey, bool>> patternBoolOverrides = new();

    public static event Action OverridesChanged;
    public static event Action<BattleTuningKey> ValueChanged;
    public static event Action<int, BossPatternTuningKey> PatternValueChanged;

    public static bool HasOverrides =>
        floatOverrides.Count > 0 ||
        intOverrides.Count > 0 ||
        boolOverrides.Count > 0 ||
        patternFloatOverrides.Count > 0 ||
        patternIntOverrides.Count > 0 ||
        patternBoolOverrides.Count > 0;

    public static void SetFloat(BattleTuningKey key, float value)
    {
        floatOverrides[key] = Mathf.Max(0f, value);
        ValueChanged?.Invoke(key);
        OverridesChanged?.Invoke();
    }

    public static void SetInt(BattleTuningKey key, int value)
    {
        intOverrides[key] = Mathf.Max(0, value);
        ValueChanged?.Invoke(key);
        OverridesChanged?.Invoke();
    }

    public static void SetBool(BattleTuningKey key, bool value)
    {
        boolOverrides[key] = value;
        ValueChanged?.Invoke(key);
        OverridesChanged?.Invoke();
    }

    public static bool TryGetFloat(BattleTuningKey key, out float value)
    {
        return floatOverrides.TryGetValue(key, out value);
    }

    public static bool TryGetInt(BattleTuningKey key, out int value)
    {
        return intOverrides.TryGetValue(key, out value);
    }

    public static bool TryGetBool(BattleTuningKey key, out bool value)
    {
        return boolOverrides.TryGetValue(key, out value);
    }

    public static void SetPatternFloat(int patternIndex, BossPatternTuningKey key, float value)
    {
        Dictionary<BossPatternTuningKey, float> overrides = GetPatternDictionary(patternFloatOverrides, patternIndex);
        overrides[key] = Mathf.Max(0f, value);
        PatternValueChanged?.Invoke(patternIndex, key);
        OverridesChanged?.Invoke();
    }

    public static void SetPatternInt(int patternIndex, BossPatternTuningKey key, int value)
    {
        Dictionary<BossPatternTuningKey, int> overrides = GetPatternDictionary(patternIntOverrides, patternIndex);
        overrides[key] = Mathf.Max(0, value);
        PatternValueChanged?.Invoke(patternIndex, key);
        OverridesChanged?.Invoke();
    }

    public static void SetPatternBool(int patternIndex, BossPatternTuningKey key, bool value)
    {
        Dictionary<BossPatternTuningKey, bool> overrides = GetPatternDictionary(patternBoolOverrides, patternIndex);
        overrides[key] = value;
        PatternValueChanged?.Invoke(patternIndex, key);
        OverridesChanged?.Invoke();
    }

    public static bool TryGetPatternFloat(int patternIndex, BossPatternTuningKey key, out float value)
    {
        value = 0f;
        return patternFloatOverrides.TryGetValue(patternIndex, out Dictionary<BossPatternTuningKey, float> overrides) &&
               overrides.TryGetValue(key, out value);
    }

    public static bool TryGetPatternInt(int patternIndex, BossPatternTuningKey key, out int value)
    {
        value = 0;
        return patternIntOverrides.TryGetValue(patternIndex, out Dictionary<BossPatternTuningKey, int> overrides) &&
               overrides.TryGetValue(key, out value);
    }

    public static bool TryGetPatternBool(int patternIndex, BossPatternTuningKey key, out bool value)
    {
        value = false;
        return patternBoolOverrides.TryGetValue(patternIndex, out Dictionary<BossPatternTuningKey, bool> overrides) &&
               overrides.TryGetValue(key, out value);
    }

    public static void ClearOverrides()
    {
        floatOverrides.Clear();
        intOverrides.Clear();
        boolOverrides.Clear();
        patternFloatOverrides.Clear();
        patternIntOverrides.Clear();
        patternBoolOverrides.Clear();
        OverridesChanged?.Invoke();
    }

    private static Dictionary<TKey, TValue> GetPatternDictionary<TKey, TValue>(
        Dictionary<int, Dictionary<TKey, TValue>> source,
        int patternIndex)
    {
        int clampedIndex = Mathf.Max(0, patternIndex);
        if (!source.TryGetValue(clampedIndex, out Dictionary<TKey, TValue> overrides))
        {
            overrides = new Dictionary<TKey, TValue>();
            source[clampedIndex] = overrides;
        }

        return overrides;
    }
}
