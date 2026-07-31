using NUnit.Framework;

public class ContinuousDamageTickStateTests
{
    [Test]
    public void ResetAll_ClearsEveryRegisteredAccumulator()
    {
        PlayerContinuousDamageTickRegistry registry = new();
        ContinuousDamageTickState first = new("beam.first", 0.2f);
        ContinuousDamageTickState second = new("beam.second", 0.5f);
        Assert.That(registry.Register(first), Is.True);
        Assert.That(registry.Register(second), Is.True);
        first.AddElapsed(0.19f);
        second.AddElapsed(0.49f);

        registry.ResetAll();

        Assert.That(first.DamageTickElapsed, Is.Zero);
        Assert.That(second.DamageTickElapsed, Is.Zero);
        Assert.That(registry.ActiveCount, Is.EqualTo(2));
    }

    [Test]
    public void TickMustAccumulateFullIntervalAgainAfterReset()
    {
        ContinuousDamageTickState state = new("beam", 0.2f);
        state.AddElapsed(0.19f);
        state.ResetElapsed();
        state.AddElapsed(0.19f);

        Assert.That(state.TryConsumeTick(), Is.False);
        state.AddElapsed(0.01f);
        Assert.That(state.TryConsumeTick(), Is.True);
        Assert.That(state.DamageTickElapsed, Is.EqualTo(0f).Within(0.0001f));
    }

    [Test]
    public void RegistryRejectsDuplicateRegistrationAndUnregistration()
    {
        PlayerContinuousDamageTickRegistry registry = new();
        ContinuousDamageTickState state = new("beam", 0.2f);

        Assert.That(registry.Register(state), Is.True);
        Assert.That(registry.Register(state), Is.False);
        Assert.That(registry.ActiveCount, Is.EqualTo(1));
        Assert.That(registry.Unregister(state), Is.True);
        Assert.That(registry.Unregister(state), Is.False);
        Assert.That(state.IsRegistered, Is.False);
        Assert.That(registry.ActiveCount, Is.Zero);
    }

    [Test]
    public void ClearMarksEveryStateUnregistered()
    {
        PlayerContinuousDamageTickRegistry registry = new();
        ContinuousDamageTickState state = new("beam", 0.2f);
        registry.Register(state);

        registry.Clear();

        Assert.That(registry.ActiveCount, Is.Zero);
        Assert.That(state.IsRegistered, Is.False);
    }

    [TestCase(0f)]
    [TestCase(-0.1f)]
    [TestCase(float.NaN)]
    [TestCase(float.PositiveInfinity)]
    public void ConstructorRejectsInvalidTickInterval(float interval)
    {
        Assert.That(
            () => new ContinuousDamageTickState("beam", interval),
            Throws.TypeOf<System.ArgumentOutOfRangeException>());
    }
}
