using NUnit.Framework;

public class LockOnSalvoRulesTests
{
    private static readonly int[] MissileCounts = { 5, 10, 15, 20, 30 };
    private static readonly float[] TotalDamages = { 9f, 20f, 35f, 60f, 100f };

    [TestCase(1, 5, 9f, 1.8f)]
    [TestCase(2, 10, 20f, 2f)]
    [TestCase(3, 15, 35f, 2.333333f)]
    [TestCase(4, 20, 60f, 3f)]
    [TestCase(5, 30, 100f, 3.333333f)]
    public void TryCalculate_UsesConfiguredStageTotalDamage(
        int successfulLocks,
        int expectedMissiles,
        float expectedTotalDamage,
        float expectedDamagePerMissile)
    {
        bool calculated = LockOnSalvoRules.TryCalculate(
            successfulLocks,
            MissileCounts,
            TotalDamages,
            out LockOnSalvoStageCalculation result,
            out string reason);

        Assert.That(calculated, Is.True, reason);
        Assert.That(result.MissileCount, Is.EqualTo(expectedMissiles));
        Assert.That(result.TotalBaseDamage, Is.EqualTo(expectedTotalDamage).Within(0.0001f));
        Assert.That(result.BaseDamagePerMissile, Is.EqualTo(expectedDamagePerMissile).Within(0.0001f));
        Assert.That(
            result.BaseDamagePerMissile * result.MissileCount,
            Is.EqualTo(expectedTotalDamage).Within(0.001f));
    }

    [TestCase(0, "SuccessfulLockCountInvalid")]
    [TestCase(6, "SuccessfulLockCountInvalid")]
    public void TryCalculate_RejectsUnsupportedSuccessfulLockCount(int lockCount, string expectedReason)
    {
        Assert.That(
            LockOnSalvoRules.TryCalculate(
                lockCount,
                MissileCounts,
                TotalDamages,
                out _,
                out string reason),
            Is.False);
        Assert.That(reason, Is.EqualTo(expectedReason));
    }

    [TestCase(0f)]
    [TestCase(-1f)]
    [TestCase(float.NaN)]
    public void TryCalculate_RejectsInvalidStageTotalDamage(float totalDamage)
    {
        Assert.That(
            LockOnSalvoRules.TryCalculate(
                5,
                new[] { 30 },
                new[] { totalDamage },
                out _,
                out string reason),
            Is.False);
        Assert.That(reason, Is.EqualTo("StageTotalDamageInvalid"));
    }

    [Test]
    public void ValidateConfiguration_RejectsMismatchedArraysAndNonPositiveValues()
    {
        Assert.That(
            LockOnSalvoRules.ValidateConfiguration(MissileCounts, new[] { 1f }),
            Is.EqualTo("LockOnSalvoStageConfigurationInvalid"));
        Assert.That(
            LockOnSalvoRules.ValidateConfiguration(new[] { 5, 0 }, new[] { 9f, 20f }),
            Is.EqualTo("MissileCountByStageInvalid"));
        Assert.That(
            LockOnSalvoRules.ValidateConfiguration(new[] { 5 }, new[] { 0f }),
            Is.EqualTo("StageTotalDamageInvalid"));
    }
}
