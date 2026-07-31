using NUnit.Framework;

public class LockOnChargeRulesTests
{
    private static readonly float[] Thresholds = { 0.35f, 0.75f, 1.25f, 1.80f, 2.50f };

    [TestCase(0f, 0)]
    [TestCase(0.349f, 0)]
    [TestCase(0.35f, 1)]
    [TestCase(0.75f, 2)]
    [TestCase(1.25f, 3)]
    [TestCase(1.80f, 4)]
    [TestCase(2.50f, 5)]
    [TestCase(20f, 5)]
    public void GetReachedStage_UsesConfiguredThresholds(float elapsed, int expected)
    {
        Assert.That(LockOnChargeRules.GetReachedStage(elapsed, Thresholds), Is.EqualTo(expected));
    }

    [Test]
    public void GetNextStageProgress_ResetsWithinEachStageBand()
    {
        Assert.That(LockOnChargeRules.GetNextStageProgress(0f, Thresholds), Is.EqualTo(0f));
        Assert.That(LockOnChargeRules.GetNextStageProgress(0.35f, Thresholds), Is.EqualTo(0f).Within(0.001f));
        Assert.That(LockOnChargeRules.GetNextStageProgress(0.55f, Thresholds), Is.EqualTo(0.5f).Within(0.001f));
        Assert.That(LockOnChargeRules.GetNextStageProgress(2.50f, Thresholds), Is.EqualTo(1f));
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
        Assert.That(LockOnChargeRules.AreStrictlyIncreasing(new[] { 0.35f, 0.35f }), Is.False);
        Assert.That(LockOnChargeRules.AreStrictlyIncreasing(new[] { 0.35f, float.NaN }), Is.False);
        Assert.That(LockOnChargeRules.AreStrictlyIncreasing(new float[0]), Is.False);
    }
}
