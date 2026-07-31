using System;
using System.Collections.Generic;

/// <summary>
/// Builds a deterministic weighted target sequence without repeating a target inside
/// one cycle. When every candidate has been used, a newly randomized cycle begins.
/// </summary>
public static class LockOnTargetSelection
{
    public static int[] BuildWeightedRepeatedSequence(
        IReadOnlyList<float> weights,
        int requestedCount,
        int randomSeed)
    {
        if (weights == null || weights.Count == 0 || requestedCount <= 0)
        {
            return Array.Empty<int>();
        }

        int[] result = new int[requestedCount];
        List<int> remaining = new(weights.Count);
        Random random = new(randomSeed);

        for (int writeIndex = 0; writeIndex < requestedCount; writeIndex++)
        {
            if (remaining.Count == 0)
            {
                for (int candidateIndex = 0; candidateIndex < weights.Count; candidateIndex++)
                {
                    remaining.Add(candidateIndex);
                }
            }

            int selectedRemainingIndex = SelectWeightedIndex(weights, remaining, random);
            result[writeIndex] = remaining[selectedRemainingIndex];
            remaining.RemoveAt(selectedRemainingIndex);
        }

        return result;
    }

    private static int SelectWeightedIndex(
        IReadOnlyList<float> weights,
        IReadOnlyList<int> remaining,
        Random random)
    {
        double totalWeight = 0d;
        for (int i = 0; i < remaining.Count; i++)
        {
            totalWeight += SanitizeWeight(weights[remaining[i]]);
        }

        if (totalWeight <= double.Epsilon)
        {
            return random.Next(remaining.Count);
        }

        double selection = random.NextDouble() * totalWeight;
        double cumulative = 0d;
        for (int i = 0; i < remaining.Count; i++)
        {
            cumulative += SanitizeWeight(weights[remaining[i]]);
            if (selection < cumulative)
            {
                return i;
            }
        }

        return remaining.Count - 1;
    }

    private static double SanitizeWeight(float weight)
    {
        if (float.IsNaN(weight) || weight <= 0f)
        {
            return 0d;
        }

        return float.IsPositiveInfinity(weight) ? 1_000_000d : Math.Min(weight, 1_000_000f);
    }
}
