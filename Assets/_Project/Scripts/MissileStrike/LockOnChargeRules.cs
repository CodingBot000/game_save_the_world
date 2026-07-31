using System;
using System.Collections.Generic;

public static class LockOnChargeRules
{
    public static int GetReachedStage(
        float elapsedSeconds,
        IReadOnlyList<float> stageChargeTimes)
    {
        if (stageChargeTimes == null || stageChargeTimes.Count == 0 ||
            float.IsNaN(elapsedSeconds) || elapsedSeconds < 0f)
        {
            return 0;
        }

        int reachedStage = 0;
        for (int i = 0; i < stageChargeTimes.Count; i++)
        {
            float threshold = SanitizeThreshold(stageChargeTimes[i]);
            if (elapsedSeconds < threshold)
            {
                break;
            }

            reachedStage = i + 1;
        }

        return reachedStage;
    }

    public static float GetNextStageProgress(
        float elapsedSeconds,
        IReadOnlyList<float> stageChargeTimes)
    {
        if (stageChargeTimes == null || stageChargeTimes.Count == 0)
        {
            return 0f;
        }

        int reachedStage = GetReachedStage(elapsedSeconds, stageChargeTimes);
        if (reachedStage >= stageChargeTimes.Count)
        {
            return 1f;
        }

        float start = reachedStage > 0
            ? SanitizeThreshold(stageChargeTimes[reachedStage - 1])
            : 0f;
        float end = SanitizeThreshold(stageChargeTimes[reachedStage]);
        float duration = Math.Max(0.0001f, end - start);
        return Clamp01((Math.Max(0f, elapsedSeconds) - start) / duration);
    }

    public static bool AreStrictlyIncreasing(IReadOnlyList<float> stageChargeTimes)
    {
        if (stageChargeTimes == null || stageChargeTimes.Count == 0)
        {
            return false;
        }

        float previous = 0f;
        for (int i = 0; i < stageChargeTimes.Count; i++)
        {
            float current = stageChargeTimes[i];
            if (float.IsNaN(current) || float.IsInfinity(current) || current <= previous)
            {
                return false;
            }

            previous = current;
        }

        return true;
    }

    private static float SanitizeThreshold(float threshold)
    {
        return float.IsNaN(threshold) || float.IsNegativeInfinity(threshold)
            ? float.PositiveInfinity
            : Math.Max(0f, threshold);
    }

    private static float Clamp01(float value)
    {
        return Math.Max(0f, Math.Min(1f, value));
    }
}
