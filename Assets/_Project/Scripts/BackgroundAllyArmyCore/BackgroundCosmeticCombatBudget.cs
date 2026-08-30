using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Shared visual-combat budget for background air and ground units.
/// It never calls gameplay damage APIs.
/// </summary>
[DisallowMultipleComponent]
public sealed class BackgroundCosmeticCombatBudget : MonoBehaviour
{
    [SerializeField, Range(1, 4)] private int maximumAmbientOwners = 2;

    private object primaryOwner;
    private readonly HashSet<object> ambientOwners = new();

    public bool HasPrimaryOwner => primaryOwner != null;
    public int AmbientOwnerCount => ambientOwners.Count;
    public int MaximumAmbientOwners => maximumAmbientOwners;

    public bool TryAcquirePrimary(object owner)
    {
        if (owner == null)
        {
            return false;
        }

        if (primaryOwner == owner)
        {
            return true;
        }

        if (primaryOwner != null)
        {
            return false;
        }

        primaryOwner = owner;
        return true;
    }

    public void ReleasePrimary(object owner)
    {
        if (owner != null && primaryOwner == owner)
        {
            primaryOwner = null;
        }
    }

    public bool TryAcquireAmbient(object owner)
    {
        if (owner == null)
        {
            return false;
        }

        if (ambientOwners.Contains(owner))
        {
            return true;
        }

        if (ambientOwners.Count >= Mathf.Max(1, maximumAmbientOwners))
        {
            return false;
        }

        ambientOwners.Add(owner);
        return true;
    }

    public void ReleaseAmbient(object owner)
    {
        if (owner != null)
        {
            ambientOwners.Remove(owner);
        }
    }

    public void ReleaseAll(object owner)
    {
        ReleasePrimary(owner);
        ReleaseAmbient(owner);
    }

    public void ResetBudget()
    {
        primaryOwner = null;
        ambientOwners.Clear();
    }

    private void OnDisable()
    {
        ResetBudget();
    }
}
