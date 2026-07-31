using System.Collections.Generic;
using NUnit.Framework;

public class LockOnTargetSelectionTests
{
    [Test]
    public void SameSeed_ProducesSameSequence()
    {
        float[] weights = { 1f, 5f, 2f, 8f, 3f };

        int[] first = LockOnTargetSelection.BuildWeightedRepeatedSequence(weights, 18, 7301);
        int[] second = LockOnTargetSelection.BuildWeightedRepeatedSequence(weights, 18, 7301);

        Assert.That(second, Is.EqualTo(first));
    }

    [Test]
    public void EachCycle_UsesEveryCandidateBeforeRepeating()
    {
        float[] weights = { 1f, 1f, 1f, 1f, 1f };
        int[] sequence = LockOnTargetSelection.BuildWeightedRepeatedSequence(weights, 12, 41);

        AssertCycleIsUnique(sequence, 0, 5);
        AssertCycleIsUnique(sequence, 5, 5);
        AssertCycleIsUnique(sequence, 10, 2);
    }

    [Test]
    public void ZeroAndNegativeWeights_StillProduceCompleteUniformCycles()
    {
        float[] weights = { 0f, -10f, float.NaN };
        int[] sequence = LockOnTargetSelection.BuildWeightedRepeatedSequence(weights, 6, 9);

        AssertCycleIsUnique(sequence, 0, 3);
        AssertCycleIsUnique(sequence, 3, 3);
    }

    [Test]
    public void SingleCandidate_RepeatsForEveryRequestedSlot()
    {
        int[] sequence = LockOnTargetSelection.BuildWeightedRepeatedSequence(
            new[] { 25f },
            5,
            1);

        Assert.That(sequence, Is.EqualTo(new[] { 0, 0, 0, 0, 0 }));
    }

    [Test]
    public void EmptyInputOrNonPositiveCount_ReturnsEmptySequence()
    {
        Assert.That(
            LockOnTargetSelection.BuildWeightedRepeatedSequence(new float[0], 5, 1),
            Is.Empty);
        Assert.That(
            LockOnTargetSelection.BuildWeightedRepeatedSequence(new[] { 1f }, 0, 1),
            Is.Empty);
        Assert.That(
            LockOnTargetSelection.BuildWeightedRepeatedSequence(null, 5, 1),
            Is.Empty);
    }

    private static void AssertCycleIsUnique(
        IReadOnlyList<int> sequence,
        int startIndex,
        int count)
    {
        HashSet<int> seen = new();
        for (int i = 0; i < count; i++)
        {
            Assert.That(seen.Add(sequence[startIndex + i]), Is.True);
        }
    }
}
