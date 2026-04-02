using System;
using System.Collections.Generic;
using UnityEngine;

public class HelicopterSelectionState : MonoBehaviour
{
    private const string VehicleCatalogResourcePath = "Vehicles/VehicleCatalog";
    private const string RuntimeObjectName = "HelicopterSelectionState";
    private static HelicopterSelectionState instance;

    [SerializeField] private List<string> ownedHelicopterIds = new List<string>();
    [SerializeField] private string selectedHelicopterId;

    private readonly List<VehicleDefinition> ownedHelicopters = new List<VehicleDefinition>();
    private VehicleCatalog vehicleCatalog;

    public event Action SelectionChanged;

    public static HelicopterSelectionState Instance => EnsureInitialized();

    public VehicleCatalog Catalog => LoadCatalog();

    public IReadOnlyList<VehicleDefinition> OwnedHelicopters
    {
        get
        {
            RefreshOwnedHelicopters();
            return ownedHelicopters;
        }
    }

    public VehicleDefinition SelectedHelicopter
    {
        get
        {
            RefreshOwnedHelicopters();
            if (ownedHelicopters.Count == 0)
            {
                return null;
            }

            for (int i = 0; i < ownedHelicopters.Count; i++)
            {
                if (ownedHelicopters[i].Id == selectedHelicopterId)
                {
                    return ownedHelicopters[i];
                }
            }

            return ownedHelicopters[0];
        }
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Bootstrap()
    {
        EnsureInitialized();
    }

    public static HelicopterSelectionState EnsureInitialized()
    {
        if (instance != null)
        {
            return instance;
        }

        instance = FindAnyObjectByType<HelicopterSelectionState>();
        if (instance != null)
        {
            instance.InitializeDefaultsIfNeeded();
            return instance;
        }

        GameObject runtimeObject = new GameObject(RuntimeObjectName);
        instance = runtimeObject.AddComponent<HelicopterSelectionState>();
        instance.InitializeDefaultsIfNeeded();
        DontDestroyOnLoad(runtimeObject);
        return instance;
    }

    public void SelectHelicopter(string helicopterId)
    {
        if (string.IsNullOrEmpty(helicopterId) || !HasOwnedHelicopter(helicopterId) || selectedHelicopterId == helicopterId)
        {
            return;
        }

        selectedHelicopterId = helicopterId;
        SelectionChanged?.Invoke();
    }

    public VehicleDefinition EnsureSelectedHelicopter()
    {
        InitializeDefaultsIfNeeded();
        RefreshOwnedHelicopters();
        if (ownedHelicopters.Count == 0)
        {
            selectedHelicopterId = string.Empty;
            return null;
        }

        if (!HasOwnedHelicopter(selectedHelicopterId))
        {
            selectedHelicopterId = ownedHelicopters[0].Id;
        }

        return SelectedHelicopter;
    }

    private void Awake()
    {
        if (instance != null && instance != this)
        {
            Destroy(gameObject);
            return;
        }

        instance = this;
        DontDestroyOnLoad(gameObject);
        InitializeDefaultsIfNeeded();
    }

    private bool HasOwnedHelicopter(string helicopterId)
    {
        for (int i = 0; i < ownedHelicopterIds.Count; i++)
        {
            if (ownedHelicopterIds[i] == helicopterId)
            {
                return true;
            }
        }

        return false;
    }

    private void InitializeDefaultsIfNeeded()
    {
        VehicleCatalog catalog = LoadCatalog();
        if (catalog == null || catalog.Helicopters.Count == 0)
        {
            ownedHelicopterIds.Clear();
            ownedHelicopters.Clear();
            selectedHelicopterId = string.Empty;
            return;
        }

        if (NeedsCatalogRefresh())
        {
            ownedHelicopterIds.Clear();
            for (int i = 0; i < catalog.Helicopters.Count; i++)
            {
                VehicleDefinition helicopter = catalog.Helicopters[i];
                if (helicopter != null && !string.IsNullOrEmpty(helicopter.Id))
                {
                    ownedHelicopterIds.Add(helicopter.Id);
                }
            }
        }

        RefreshOwnedHelicopters();
        if (!HasOwnedHelicopter(selectedHelicopterId))
        {
            selectedHelicopterId = ownedHelicopters.Count > 0 ? ownedHelicopters[0].Id : string.Empty;
        }
    }

    private bool NeedsCatalogRefresh()
    {
        VehicleCatalog catalog = LoadCatalog();
        if (catalog == null || catalog.Helicopters.Count == 0)
        {
            return false;
        }

        if (ownedHelicopterIds.Count != catalog.Helicopters.Count)
        {
            return true;
        }

        for (int i = 0; i < ownedHelicopterIds.Count; i++)
        {
            VehicleDefinition helicopter = catalog.GetHelicopter(ownedHelicopterIds[i]);
            if (helicopter == null)
            {
                return true;
            }
        }

        return false;
    }

    private VehicleCatalog LoadCatalog()
    {
        if (vehicleCatalog == null)
        {
            vehicleCatalog = Resources.Load<VehicleCatalog>(VehicleCatalogResourcePath);
        }

        return vehicleCatalog;
    }

    private void RefreshOwnedHelicopters()
    {
        ownedHelicopters.Clear();
        VehicleCatalog catalog = LoadCatalog();
        if (catalog == null)
        {
            return;
        }

        for (int i = 0; i < ownedHelicopterIds.Count; i++)
        {
            VehicleDefinition helicopter = catalog.GetHelicopter(ownedHelicopterIds[i]);
            if (helicopter != null)
            {
                ownedHelicopters.Add(helicopter);
            }
        }
    }
}
