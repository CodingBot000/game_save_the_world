using System;
using System.Collections.Generic;

public readonly struct LockOnSalvoStageCalculation
{
    public LockOnSalvoStageCalculation(
        int successfulLockCount,
        int missileCount,
        float totalBaseDamage,
        float baseDamagePerMissile)
    {
        SuccessfulLockCount = successfulLockCount;
        MissileCount = missileCount;
        TotalBaseDamage = totalBaseDamage;
        BaseDamagePerMissile = baseDamagePerMissile;
    }

    public int SuccessfulLockCount { get; }
    public int MissileCount { get; }
    public float TotalBaseDamage { get; }
    public float BaseDamagePerMissile { get; }
}

public static class LockOnSalvoRules
{
    public static bool TryCalculate(
        int successfulLockCount,
        IReadOnlyList<int> missileCounts,
        IReadOnlyList<float> totalDamages,
        out LockOnSalvoStageCalculation calculation,
        out string failureReason)
    {
        calculation = default;
        failureReason = ValidateConfiguration(missileCounts, totalDamages);
        if (!string.IsNullOrEmpty(failureReason))
        {
            return false;
        }

        if (successfulLockCount <= 0 || successfulLockCount > missileCounts.Count)
        {
            failureReason = "SuccessfulLockCountInvalid";
            return false;
        }

        int stageIndex = successfulLockCount - 1;
        int missileCount = missileCounts[stageIndex];
        float totalBaseDamage = totalDamages[stageIndex];
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
            totalBaseDamage,
            baseDamagePerMissile);
        failureReason = string.Empty;
        return true;
    }

    public static string ValidateConfiguration(
        IReadOnlyList<int> missileCounts,
        IReadOnlyList<float> totalDamages)
    {
        if (missileCounts == null || totalDamages == null ||
            missileCounts.Count == 0 || missileCounts.Count != totalDamages.Count)
        {
            return "LockOnSalvoStageConfigurationInvalid";
        }

        for (int i = 0; i < missileCounts.Count; i++)
        {
            if (missileCounts[i] <= 0)
            {
                return "MissileCountByStageInvalid";
            }

            if (!IsPositiveFinite(totalDamages[i]))
            {
                return "StageTotalDamageInvalid";
            }
        }

        return string.Empty;
    }

    private static bool IsPositiveFinite(float value)
    {
        return value > 0f && !float.IsNaN(value) && !float.IsInfinity(value);
    }
}
