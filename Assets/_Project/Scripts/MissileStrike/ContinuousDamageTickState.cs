using System;
using System.Collections.Generic;

public sealed class ContinuousDamageTickState
{
    public ContinuousDamageTickState(string sourceId, float damageTickInterval)
    {
        if (string.IsNullOrWhiteSpace(sourceId))
        {
            throw new ArgumentException("A continuous damage source ID is required.", nameof(sourceId));
        }

        if (damageTickInterval <= 0f || float.IsNaN(damageTickInterval) ||
            float.IsInfinity(damageTickInterval))
        {
            throw new ArgumentOutOfRangeException(
                nameof(damageTickInterval),
                "The damage tick interval must be positive and finite.");
        }

        SourceId = sourceId;
        DamageTickInterval = damageTickInterval;
    }

    public string SourceId { get; }
    public float DamageTickInterval { get; }
    public float DamageTickElapsed { get; private set; }
    public bool IsRegistered { get; internal set; }

    public void AddElapsed(float deltaTime)
    {
        if (deltaTime <= 0f || float.IsNaN(deltaTime) || float.IsInfinity(deltaTime))
        {
            return;
        }

        DamageTickElapsed = Math.Max(0f, DamageTickElapsed + deltaTime);
    }

    public bool TryConsumeTick()
    {
        if (DamageTickElapsed < DamageTickInterval)
        {
            return false;
        }

        DamageTickElapsed = Math.Max(0f, DamageTickElapsed - DamageTickInterval);
        return true;
    }

    public void ResetElapsed()
    {
        DamageTickElapsed = 0f;
    }
}

public sealed class PlayerContinuousDamageTickRegistry
{
    private readonly HashSet<ContinuousDamageTickState> activeStates = new();

    public int ActiveCount => activeStates.Count;

    public bool Register(ContinuousDamageTickState state)
    {
        if (state == null || state.IsRegistered || !activeStates.Add(state))
        {
            return false;
        }

        state.IsRegistered = true;
        return true;
    }

    public bool Unregister(ContinuousDamageTickState state)
    {
        if (state == null || !activeStates.Remove(state))
        {
            return false;
        }

        state.IsRegistered = false;
        return true;
    }

    public void ResetAll()
    {
        foreach (ContinuousDamageTickState state in activeStates)
        {
            state?.ResetElapsed();
        }
    }

    public void Clear()
    {
        foreach (ContinuousDamageTickState state in activeStates)
        {
            if (state != null)
            {
                state.IsRegistered = false;
            }
        }

        activeStates.Clear();
    }
}
