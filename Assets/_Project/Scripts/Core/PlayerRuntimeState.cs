using System;
using UnityEngine;

public sealed class PlayerProgressState
{
    public string SelectedVehicleId;
    public int HullUpgradeLevel;
    public int ArmorUpgradeLevel;
    public int WeaponUpgradeLevel;
    public int MissileUpgradeLevel;
}

public struct PlayerRuntimeStats
{
    public float MaxHull;
    public float MaxArmor;
    public float RepairRate;
    public float RepairDelay;
    public float BrokenRecoverThreshold;
    public float HullDamageMultiplierWhenBroken;

    public float FireCooldown;
    public float ProjectileSpeed;
    public float ProjectileDamage;
    public float InvulnerabilityDuration;
    public float PlayerHitRadius;

    public float MissileCooldown;
    public float MissileDamage;
    public float MissileLaunchSpeed;
    public float MissileCruiseSpeed;
    public float MissileAcceleration;
    public float MissileTurnRate;
    public float MissileLockOnDelay;
    public float MissileStraightPhaseDuration;
    public float MissileStraightPhaseDistance;
    public float MissileTurnPhaseDuration;
    public float MissileBoostPhaseDuration;
    public float MissileLifetime;
    public float MissileHitRadius;

    public float StrafeSpeed;
    public float AltitudeSpeed;
    public float ForwardSpeed;
}

public static class PlayerRuntimeState
{
    private const string VehiclePlayerStateCatalogResourcePath = "Vehicles/VehiclePlayerStateCatalog";

    private static readonly PlayerProgressState progress = new();
    private static PlayerRuntimeStats currentStats;
    private static bool hasCalculatedStats;

    public static event Action StatsChanged;

    public static PlayerProgressState Progress => progress;
    public static PlayerRuntimeStats CurrentStats
    {
        get
        {
            if (!hasCalculatedStats)
            {
                Recalculate();
            }

            return currentStats;
        }
    }

    public static void SetSelectedVehicle(string vehicleId)
    {
        string nextVehicleId = string.IsNullOrWhiteSpace(vehicleId) ? string.Empty : vehicleId.Trim();
        if (progress.SelectedVehicleId == nextVehicleId)
        {
            return;
        }

        progress.SelectedVehicleId = nextVehicleId;
        Recalculate();
    }

    public static void SetUpgradeLevel(PlayerUpgradeType type, int level)
    {
        int clampedLevel = Mathf.Max(0, level);
        bool changed = type switch
        {
            PlayerUpgradeType.Hull => SetIfChanged(ref progress.HullUpgradeLevel, clampedLevel),
            PlayerUpgradeType.Armor => SetIfChanged(ref progress.ArmorUpgradeLevel, clampedLevel),
            PlayerUpgradeType.Weapon => SetIfChanged(ref progress.WeaponUpgradeLevel, clampedLevel),
            PlayerUpgradeType.Missile => SetIfChanged(ref progress.MissileUpgradeLevel, clampedLevel),
            _ => false
        };

        if (changed)
        {
            Recalculate();
        }
    }

    public static PlayerRuntimeStats ResolveStats()
    {
        Recalculate();
        return currentStats;
    }

    public static void Recalculate()
    {
        string selectedVehicleId = ResolveSelectedVehicleId();
        progress.SelectedVehicleId = selectedVehicleId;

        VehiclePlayerStateDefinition defensiveState = ResolveVehiclePlayerState(selectedVehicleId);
        currentStats = CreateDefaultBattleStats(defensiveState);

        // Upgrade formulas are intentionally conservative placeholders. They
        // centralize future Garage/Upgrade math without changing existing data.
        currentStats.MaxHull += progress.HullUpgradeLevel * 10f;
        currentStats.MaxArmor += progress.ArmorUpgradeLevel * 10f;
        currentStats.ProjectileDamage += progress.WeaponUpgradeLevel * 2f;
        currentStats.MissileDamage += progress.MissileUpgradeLevel * 10f;

        hasCalculatedStats = true;
        StatsChanged?.Invoke();
    }

    private static bool SetIfChanged(ref int target, int value)
    {
        if (target == value)
        {
            return false;
        }

        target = value;
        return true;
    }

    private static string ResolveSelectedVehicleId()
    {
        if (!string.IsNullOrWhiteSpace(progress.SelectedVehicleId))
        {
            return progress.SelectedVehicleId;
        }

        HelicopterSelectionState selectionState = HelicopterSelectionState.EnsureInitialized();
        VehicleDefinition selectedVehicle = selectionState != null ? selectionState.EnsureSelectedHelicopter() : null;
        return selectedVehicle != null ? selectedVehicle.Id : string.Empty;
    }

    private static VehiclePlayerStateDefinition ResolveVehiclePlayerState(string vehicleId)
    {
        VehiclePlayerStateCatalog catalog = Resources.Load<VehiclePlayerStateCatalog>(VehiclePlayerStateCatalogResourcePath);
        return catalog != null ? catalog.GetState(vehicleId) : null;
    }

    private static PlayerRuntimeStats CreateDefaultBattleStats(VehiclePlayerStateDefinition defensiveState)
    {
        return new PlayerRuntimeStats
        {
            MaxHull = defensiveState != null ? Mathf.Max(1f, defensiveState.HullHp) : 100f,
            MaxArmor = defensiveState != null ? Mathf.Max(0f, defensiveState.ArmorHp) : 120f,
            RepairRate = defensiveState != null ? Mathf.Max(0f, defensiveState.RepairRate) : 8f,
            RepairDelay = defensiveState != null ? Mathf.Max(0f, defensiveState.RepairDelay) : 2.5f,
            BrokenRecoverThreshold = defensiveState != null ? Mathf.Max(0f, defensiveState.BrokenRecoverThreshold) : 36f,
            HullDamageMultiplierWhenBroken = defensiveState != null ? Mathf.Max(0f, defensiveState.HullDamageMultiplierWhenBroken) : 1.25f,

            FireCooldown = 0.15f,
            ProjectileSpeed = 60f,
            ProjectileDamage = 25f,
            InvulnerabilityDuration = 1f,
            PlayerHitRadius = 1.4f,

            MissileCooldown = 2.6f,
            MissileDamage = 150f,
            MissileLaunchSpeed = 18f,
            MissileCruiseSpeed = 72f,
            MissileAcceleration = 130f,
            MissileTurnRate = 280f,
            MissileLockOnDelay = 0.2f,
            MissileStraightPhaseDuration = 0.2f,
            MissileStraightPhaseDistance = 1f,
            MissileTurnPhaseDuration = 0.4f,
            MissileBoostPhaseDuration = 0.6f,
            MissileLifetime = 6f,
            MissileHitRadius = 1.8f,

            StrafeSpeed = 8f,
            AltitudeSpeed = 8f,
            ForwardSpeed = 10f,
        };
    }
}

public enum PlayerUpgradeType
{
    Hull,
    Armor,
    Weapon,
    Missile,
}
