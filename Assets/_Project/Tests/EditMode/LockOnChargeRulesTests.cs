using NUnit.Framework;

public class LockOnChargeRulesTests
{
    private static readonly float[] Thresholds = { 3.00f, 3.40f, 3.90f, 4.45f, 5.15f };

    [TestCase(0f, 0)]
    [TestCase(2.999f, 0)]
    [TestCase(3.00f, 1)]
    [TestCase(3.40f, 2)]
    [TestCase(3.90f, 3)]
    [TestCase(4.45f, 4)]
    [TestCase(5.15f, 5)]
    [TestCase(20f, 5)]
    public void GetReachedStage_UsesConfiguredThresholds(float elapsed, int expected)
    {
        Assert.That(LockOnChargeRules.GetReachedStage(elapsed, Thresholds), Is.EqualTo(expected));
    }

    [Test]
    public void GetNextStageProgress_ResetsWithinEachStageBand()
    {
        Assert.That(LockOnChargeRules.GetNextStageProgress(0f, Thresholds), Is.EqualTo(0f));
        Assert.That(LockOnChargeRules.GetNextStageProgress(1.50f, Thresholds), Is.EqualTo(0.5f).Within(0.001f));
        Assert.That(LockOnChargeRules.GetNextStageProgress(3.00f, Thresholds), Is.EqualTo(0f).Within(0.001f));
        Assert.That(LockOnChargeRules.GetNextStageProgress(3.20f, Thresholds), Is.EqualTo(0.5f).Within(0.001f));
        Assert.That(LockOnChargeRules.GetNextStageProgress(5.15f, Thresholds), Is.EqualTo(1f));
    }

    [Test]
    public void InvalidInputs_ReturnSafeDefaults()
    {
        Assert.That(LockOnChargeRules.GetReachedStage(-1f, Thresholds), Is.Zero);
        Assert.That(LockOnChargeRules.GetReachedStage(float.NaN, Thresholds), Is.Zero);
        Assert.That(LockOnChargeRules.GetReachedStage(1f, null), Is.Zero);
        Assert.That(LockOnChargeRules.GetNextStageProgress(1f, null), Is.Zero);
    }

    [Test]
    public void AreStrictlyIncreasing_RejectsInvalidOrRepeatedThresholds()
    {
        Assert.That(LockOnChargeRules.AreStrictlyIncreasing(Thresholds), Is.True);
        Assert.That(LockOnChargeRules.AreStrictlyIncreasing(new[] { 3f, 3f }), Is.False);
        Assert.That(LockOnChargeRules.AreStrictlyIncreasing(new[] { 3f, float.NaN }), Is.False);
        Assert.That(LockOnChargeRules.AreStrictlyIncreasing(new float[0]), Is.False);
    }
}
