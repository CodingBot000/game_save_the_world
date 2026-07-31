using NUnit.Framework;

public class LockOnSalvoRulesTests
{
    private static readonly int[] MissileCounts = { 5, 10, 15, 20, 30 };
    private static readonly float[] DamageRatios = { 0.30f, 0.40f, 0.50f, 0.60f, 1f };

    [TestCase(1, 5, 75f, 15f)]
    [TestCase(2, 10, 100f, 10f)]
    [TestCase(3, 15, 125f, 8.333333f)]
    [TestCase(4, 20, 150f, 7.5f)]
    [TestCase(5, 30, 250f, 8.333333f)]
    public void TryCalculate_UsesGatlingBudgetAndConfiguredStageRatios(
        int successfulLocks,
        int expectedMissiles,
        float expectedTotalDamage,
        float expectedDamagePerMissile)
    {
        bool calculated = LockOnSalvoRules.TryCalculate(
            successfulLocks,
            gatlingBaseDamage: 25f,
            fullSalvoGatlingDamageMultiplier: 10f,
            MissileCounts,
            DamageRatios,
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
                25f,
                10f,
                MissileCounts,
                DamageRatios,
                out _,
                out string reason),
            Is.False);
        Assert.That(reason, Is.EqualTo(expectedReason));
    }

    [TestCase(0f)]
    [TestCase(-1f)]
    [TestCase(float.NaN)]
    public void TryCalculate_RejectsInvalidGatlingDamage(float gatlingDamage)
    {
        Assert.That(
            LockOnSalvoRules.TryCalculate(
                5,
                gatlingDamage,
                10f,
                MissileCounts,
                DamageRatios,
                out _,
                out string reason),
            Is.False);
        Assert.That(reason, Is.EqualTo("InvalidGatlingBaseDamage"));
    }

    [Test]
    public void ValidateConfiguration_RejectsMismatchedArraysAndNonPositiveValues()
    {
        Assert.That(
            LockOnSalvoRules.ValidateConfiguration(MissileCounts, new[] { 1f }, 10f),
            Is.EqualTo("LockOnSalvoStageConfigurationInvalid"));
        Assert.That(
            LockOnSalvoRules.ValidateConfiguration(new[] { 5, 0 }, new[] { 0.3f, 0.4f }, 10f),
            Is.EqualTo("MissileCountByStageInvalid"));
        Assert.That(
            LockOnSalvoRules.ValidateConfiguration(new[] { 5 }, new[] { 0f }, 10f),
            Is.EqualTo("StageDamageRatioInvalid"));
        Assert.That(
            LockOnSalvoRules.ValidateConfiguration(new[] { 5 }, new[] { 0.3f }, 0f),
            Is.EqualTo("FullSalvoDamageMultiplierInvalid"));
    }
}
