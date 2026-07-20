using NUnit.Framework;
using UnityEngine;

public class MissileStrikeDistributionTests
{
    [Test]
    public void ThirtyMissilesAcrossFiveAnchors_AssignsSixToEachAnchor()
    {
        int[] counts = CountAssignments(30, 5, 7);

        CollectionAssert.AreEqual(new[] { 6, 6, 6, 6, 6 }, counts);
    }

    [Test]
    public void UnevenMissileCount_AssignmentDifferenceNeverExceedsOne()
    {
        int[] counts = CountAssignments(31, 5, 19);

        Assert.That(Mathf.Max(counts) - Mathf.Min(counts), Is.LessThanOrEqualTo(1));
    }

    [Test]
    public void SameSeed_ProducesSameOffsets()
    {
        Vector3 first = CreateOffset(11, 2, 2, 6, 41);
        Vector3 second = CreateOffset(11, 2, 2, 6, 41);

        Assert.That(first, Is.EqualTo(second));
    }

    [Test]
    public void DifferentOrdinals_DoNotRepeatOffsets()
    {
        Vector3[] offsets = new Vector3[6];
        for (int i = 0; i < offsets.Length; i++)
        {
            offsets[i] = CreateOffset(i * 5, 0, i, offsets.Length, 3);
        }

        for (int i = 0; i < offsets.Length; i++)
        {
            for (int j = i + 1; j < offsets.Length; j++)
            {
                Assert.That(Vector3.Distance(offsets[i], offsets[j]), Is.GreaterThan(0.001f));
            }
        }
    }

    [Test]
    public void LocalOffsets_StayInsideConfiguredEllipseAndDepth()
    {
        const float radius = 1.6f;
        const float verticalScale = 1.25f;
        const float depth = 0.2f;

        for (int i = 0; i < 30; i++)
        {
            Vector3 offset = MissileStrikeDistribution.GetLocalOffset(
                i,
                i % 5,
                i / 5,
                6,
                9,
                radius,
                verticalScale,
                depth);
            float normalizedEllipseRadius = Mathf.Sqrt(
                offset.x * offset.x / (radius * radius) +
                offset.y * offset.y / (radius * radius * verticalScale * verticalScale));

            Assert.That(normalizedEllipseRadius, Is.LessThanOrEqualTo(1.0001f));
            Assert.That(Mathf.Abs(offset.z), Is.LessThanOrEqualTo(depth));
        }
    }

    [Test]
    public void NoAnchors_ReturnsFallbackIndex()
    {
        Assert.That(MissileStrikeDistribution.GetAnchorIndex(0, 0, 1), Is.EqualTo(-1));
    }

    private static int[] CountAssignments(int missileCount, int anchorCount, int seed)
    {
        int[] counts = new int[anchorCount];
        for (int i = 0; i < missileCount; i++)
        {
            counts[MissileStrikeDistribution.GetAnchorIndex(i, anchorCount, seed)]++;
        }

        return counts;
    }

    private static Vector3 CreateOffset(
        int missileIndex,
        int anchorIndex,
        int ordinal,
        int assignedCount,
        int seed)
    {
        return MissileStrikeDistribution.GetLocalOffset(
            missileIndex,
            anchorIndex,
            ordinal,
            assignedCount,
            seed,
            1.6f,
            1.25f,
            0.2f);
    }
}
