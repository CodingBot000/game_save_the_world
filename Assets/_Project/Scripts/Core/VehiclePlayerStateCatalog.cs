using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public class VehiclePlayerStateDefinition
{
    [SerializeField] private string vehicleId;
    [SerializeField] private float hullHp = 100f;
    [SerializeField] private float armorHp = 120f;
    [SerializeField] private float repairRate = 8f;
    [SerializeField] private float repairDelay = 2.5f;
    [SerializeField] private float brokenRecoverThreshold = 36f;
    [SerializeField] private float hullDamageMultiplierWhenBroken = 1.25f;

    public string VehicleId => vehicleId;
    public float HullHp => hullHp;
    public float ArmorHp => armorHp;
    public float RepairRate => repairRate;
    public float RepairDelay => repairDelay;
    public float BrokenRecoverThreshold => brokenRecoverThreshold;
    public float HullDamageMultiplierWhenBroken => hullDamageMultiplierWhenBroken;

#if UNITY_EDITOR
    public void Set(
        string newVehicleId,
        float newHullHp,
        float newArmorHp,
        float newRepairRate,
        float newRepairDelay,
        float newBrokenRecoverThreshold,
        float newHullDamageMultiplierWhenBroken)
    {
        vehicleId = newVehicleId;
        hullHp = newHullHp;
        armorHp = newArmorHp;
        repairRate = newRepairRate;
        repairDelay = newRepairDelay;
        brokenRecoverThreshold = newBrokenRecoverThreshold;
        hullDamageMultiplierWhenBroken = newHullDamageMultiplierWhenBroken;
    }
#endif
}

[CreateAssetMenu(fileName = "VehiclePlayerStateCatalog", menuName = "Titan Destroyer/Vehicle Player State Catalog")]
public class VehiclePlayerStateCatalog : ScriptableObject
{
    [SerializeField] private VehiclePlayerStateDefinition fallbackState = new VehiclePlayerStateDefinition();
    [SerializeField] private List<VehiclePlayerStateDefinition> vehicles = new List<VehiclePlayerStateDefinition>();

    public VehiclePlayerStateDefinition FallbackState => fallbackState;
    public IReadOnlyList<VehiclePlayerStateDefinition> Vehicles => vehicles;

    public VehiclePlayerStateDefinition GetState(string vehicleId)
    {
        if (!string.IsNullOrWhiteSpace(vehicleId))
        {
            for (int i = 0; i < vehicles.Count; i++)
            {
                VehiclePlayerStateDefinition definition = vehicles[i];
                if (definition != null && definition.VehicleId == vehicleId)
                {
                    return definition;
                }
            }
        }

        return fallbackState;
    }

#if UNITY_EDITOR
    public void ReplaceStates(VehiclePlayerStateDefinition newFallbackState, List<VehiclePlayerStateDefinition> newStates)
    {
        fallbackState = newFallbackState ?? new VehiclePlayerStateDefinition();
        vehicles = newStates ?? new List<VehiclePlayerStateDefinition>();
    }
#endif
}
