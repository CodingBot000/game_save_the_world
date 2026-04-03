#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

public static class VehiclePlayerStateCatalogBuilder
{
    private const string VehicleCatalogAssetPath = "Assets/Resources/Vehicles/VehicleCatalog.asset";
    private const string PlayerStateCatalogAssetPath = "Assets/Resources/Vehicles/VehiclePlayerStateCatalog.asset";

    private const float DefaultHullHp = 100f;
    private const float DefaultArmorHp = 120f;
    private const float DefaultRepairRate = 8f;
    private const float DefaultRepairDelay = 2.5f;
    private const float DefaultBrokenRecoverThreshold = 36f;
    private const float DefaultHullDamageMultiplierWhenBroken = 1.25f;

    [MenuItem("Tools/Titan Destroyer/Sync Vehicle Player State Catalog")]
    public static void SyncCatalog()
    {
        VehicleCatalog vehicleCatalog = AssetDatabase.LoadAssetAtPath<VehicleCatalog>(VehicleCatalogAssetPath);
        if (vehicleCatalog == null)
        {
            Debug.LogError($"Could not load VehicleCatalog at '{VehicleCatalogAssetPath}'.");
            return;
        }

        VehiclePlayerStateCatalog playerStateCatalog =
            AssetDatabase.LoadAssetAtPath<VehiclePlayerStateCatalog>(PlayerStateCatalogAssetPath);

        if (playerStateCatalog == null)
        {
            playerStateCatalog = ScriptableObject.CreateInstance<VehiclePlayerStateCatalog>();
            AssetDatabase.CreateAsset(playerStateCatalog, PlayerStateCatalogAssetPath);
        }

        Dictionary<string, VehiclePlayerStateDefinition> existingDefinitions = new Dictionary<string, VehiclePlayerStateDefinition>();
        for (int i = 0; i < playerStateCatalog.Vehicles.Count; i++)
        {
            VehiclePlayerStateDefinition definition = playerStateCatalog.Vehicles[i];
            if (definition == null || string.IsNullOrWhiteSpace(definition.VehicleId))
            {
                continue;
            }

            existingDefinitions[definition.VehicleId] = definition;
        }

        VehiclePlayerStateDefinition fallbackState = CreateOrCopyDefault(null);
        VehiclePlayerStateDefinition existingFallback = playerStateCatalog.FallbackState;
        if (existingFallback != null)
        {
            fallbackState.Set(
                string.Empty,
                existingFallback.HullHp,
                existingFallback.ArmorHp,
                existingFallback.RepairRate,
                existingFallback.RepairDelay,
                existingFallback.BrokenRecoverThreshold,
                existingFallback.HullDamageMultiplierWhenBroken);
        }

        List<VehiclePlayerStateDefinition> states = new List<VehiclePlayerStateDefinition>();
        for (int i = 0; i < vehicleCatalog.Helicopters.Count; i++)
        {
            VehicleDefinition vehicle = vehicleCatalog.Helicopters[i];
            if (vehicle == null || string.IsNullOrWhiteSpace(vehicle.Id))
            {
                continue;
            }

            if (existingDefinitions.TryGetValue(vehicle.Id, out VehiclePlayerStateDefinition existing))
            {
                states.Add(CreateCopy(
                    vehicle.Id,
                    existing.HullHp,
                    existing.ArmorHp,
                    existing.RepairRate,
                    existing.RepairDelay,
                    existing.BrokenRecoverThreshold,
                    existing.HullDamageMultiplierWhenBroken));
            }
            else
            {
                states.Add(CreateOrCopyDefault(vehicle.Id));
            }
        }

        playerStateCatalog.ReplaceStates(fallbackState, states);
        EditorUtility.SetDirty(playerStateCatalog);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("Vehicle player state catalog synced.");
    }

    private static VehiclePlayerStateDefinition CreateOrCopyDefault(string vehicleId)
    {
        return CreateCopy(
            vehicleId,
            DefaultHullHp,
            DefaultArmorHp,
            DefaultRepairRate,
            DefaultRepairDelay,
            DefaultBrokenRecoverThreshold,
            DefaultHullDamageMultiplierWhenBroken);
    }

    private static VehiclePlayerStateDefinition CreateCopy(
        string vehicleId,
        float hullHp,
        float armorHp,
        float repairRate,
        float repairDelay,
        float brokenRecoverThreshold,
        float hullDamageMultiplierWhenBroken)
    {
        VehiclePlayerStateDefinition definition = new VehiclePlayerStateDefinition();
        definition.Set(
            vehicleId,
            hullHp,
            armorHp,
            repairRate,
            repairDelay,
            brokenRecoverThreshold,
            hullDamageMultiplierWhenBroken);
        return definition;
    }
}
#endif
