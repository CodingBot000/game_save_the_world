using System;
using System.Collections.Generic;

public readonly struct LockOnSalvoStageCalculation
{
    public LockOnSalvoStageCalculation(
        int successfulLockCount,
        int missileCount,
        float gatlingBaseDamage,
        float stageDamageRatio,
        float totalBaseDamage,
        float baseDamagePerMissile)
    {
        SuccessfulLockCount = successfulLockCount;
        MissileCount = missileCount;
        GatlingBaseDamage = gatlingBaseDamage;
        StageDamageRatio = stageDamageRatio;
        TotalBaseDamage = totalBaseDamage;
        BaseDamagePerMissile = baseDamagePerMissile;
    }

    public int SuccessfulLockCount { get; }
    public int MissileCount { get; }
    public float GatlingBaseDamage { get; }
    public float StageDamageRatio { get; }
    public float TotalBaseDamage { get; }
    public float BaseDamagePerMissile { get; }
}

public static class LockOnSalvoRules
{
    public static bool TryCalculate(
        int successfulLockCount,
        float gatlingBaseDamage,
        float fullSalvoGatlingDamageMultiplier,
        IReadOnlyList<int> missileCounts,
        IReadOnlyList<float> stageDamageRatios,
        out LockOnSalvoStageCalculation calculation,
        out string failureReason)
    {
        calculation = default;
        failureReason = ValidateConfiguration(
            missileCounts,
            stageDamageRatios,
            fullSalvoGatlingDamageMultiplier);
        if (!string.IsNullOrEmpty(failureReason))
        {
            return false;
        }

        if (successfulLockCount <= 0 || successfulLockCount > missileCounts.Count)
        {
            failureReason = "SuccessfulLockCountInvalid";
            return false;
        }

        if (!IsPositiveFinite(gatlingBaseDamage))
        {
            failureReason = "InvalidGatlingBaseDamage";
            return false;
        }

        int stageIndex = successfulLockCount - 1;
        int missileCount = missileCounts[stageIndex];
        float stageDamageRatio = stageDamageRatios[stageIndex];
        float totalBaseDamage = gatlingBaseDamage *
                                fullSalvoGatlingDamageMultiplier *
                                stageDamageRatio;
        float baseDamagePerMissile = totalBaseDamage / missileCount;
        if (!IsPositiveFinite(totalBaseDamage) ||
            !IsPositiveFinite(baseDamagePerMissile))
        {
            failureReason = "CalculatedSalvoDamageInvalid";
            return false;
        }

        calculation = new LockOnSalvoStageCalculation(
            successfulLockCount,
            missileCount,
            gatlingBaseDamage,
            stageDamageRatio,
            totalBaseDamage,
            baseDamagePerMissile);
        failureReason = string.Empty;
        return true;
    }

    public static string ValidateConfiguration(
        IReadOnlyList<int> missileCounts,
        IReadOnlyList<float> stageDamageRatios,
        float fullSalvoGatlingDamageMultiplier)
    {
        if (missileCounts == null || stageDamageRatios == null ||
            missileCounts.Count == 0 || missileCounts.Count != stageDamageRatios.Count)
        {
            return "LockOnSalvoStageConfigurationInvalid";
        }

        if (!IsPositiveFinite(fullSalvoGatlingDamageMultiplier))
        {
            return "FullSalvoDamageMultiplierInvalid";
        }

        for (int i = 0; i < missileCounts.Count; i++)
        {
            if (missileCounts[i] <= 0)
            {
                return "MissileCountByStageInvalid";
            }

            if (!IsPositiveFinite(stageDamageRatios[i]))
            {
                return "StageDamageRatioInvalid";
            }
        }

        return string.Empty;
    }

    private static bool IsPositiveFinite(float value)
    {
        return value > 0f && !float.IsNaN(value) && !float.IsInfinity(value);
    }
}
