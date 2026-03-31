using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public class HelicopterGarageEntry
{
    [SerializeField] private string id;
    [SerializeField] private string displayName;
    [SerializeField] private string previewResourcePath;
    [SerializeField] private Color tint;

    public HelicopterGarageEntry(string id, string displayName, string previewResourcePath, Color tint)
    {
        this.id = id;
        this.displayName = displayName;
        this.previewResourcePath = previewResourcePath;
        this.tint = tint;
    }

    public string Id => id;
    public string DisplayName => displayName;
    public string PreviewResourcePath => previewResourcePath;
    public Color Tint => tint;
}

public class HelicopterSelectionState : MonoBehaviour
{
    private const string RuntimeObjectName = "HelicopterSelectionState";
    private static HelicopterSelectionState instance;

    [SerializeField] private List<HelicopterGarageEntry> ownedHelicopters = new List<HelicopterGarageEntry>();
    [SerializeField] private string selectedHelicopterId;

    public event Action SelectionChanged;

    public static HelicopterSelectionState Instance => EnsureInitialized();

    public IReadOnlyList<HelicopterGarageEntry> OwnedHelicopters => ownedHelicopters;

    public HelicopterGarageEntry SelectedHelicopter
    {
        get
        {
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

        instance = FindFirstObjectByType<HelicopterSelectionState>();
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

    public GameObject LoadPreviewPrefab(HelicopterGarageEntry helicopter)
    {
        if (helicopter == null || string.IsNullOrEmpty(helicopter.PreviewResourcePath))
        {
            return null;
        }

        return Resources.Load<GameObject>(helicopter.PreviewResourcePath);
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
        for (int i = 0; i < ownedHelicopters.Count; i++)
        {
            if (ownedHelicopters[i].Id == helicopterId)
            {
                return true;
            }
        }

        return false;
    }

    private void InitializeDefaultsIfNeeded()
    {
        if (NeedsCatalogRefresh())
        {
            ownedHelicopters.Clear();
            ownedHelicopters.Add(new HelicopterGarageEntry("helicopter_1", "Helicopter1", "Garage/HelicopterPrefabs/Helicopter1", new Color(0.87f, 0.29f, 0.25f)));
            ownedHelicopters.Add(new HelicopterGarageEntry("helicopter_2", "Helicopter2", "Garage/HelicopterPrefabs/Helicopter2", new Color(0.2f, 0.52f, 0.92f)));
            ownedHelicopters.Add(new HelicopterGarageEntry("helicopter_3", "Helicopter3", "Garage/HelicopterPrefabs/Helicopter3", new Color(0.27f, 0.72f, 0.46f)));
            ownedHelicopters.Add(new HelicopterGarageEntry("helicopter_4", "Helicopter4", "Garage/HelicopterPrefabs/Helicopter4", new Color(0.82f, 0.67f, 0.22f)));
            ownedHelicopters.Add(new HelicopterGarageEntry("helicopter_5", "Helicopter5", "Garage/HelicopterPrefabs/Helicopter5", new Color(0.88f, 0.9f, 0.96f)));
            ownedHelicopters.Add(new HelicopterGarageEntry("helicopter_6", "Helicopter6", "Garage/HelicopterPrefabs/Helicopter6", new Color(0.68f, 0.34f, 0.84f)));
            ownedHelicopters.Add(new HelicopterGarageEntry("helicopter_7", "Helicopter7", "Garage/HelicopterPrefabs/Helicopter7", new Color(0.17f, 0.8f, 0.78f)));
            ownedHelicopters.Add(new HelicopterGarageEntry("helicopter_8", "Helicopter8", "Garage/HelicopterPrefabs/Helicopter8", new Color(0.94f, 0.5f, 0.2f)));
        }

        if (!HasOwnedHelicopter(selectedHelicopterId))
        {
            selectedHelicopterId = ownedHelicopters[0].Id;
        }
    }

    private bool NeedsCatalogRefresh()
    {
        if (ownedHelicopters.Count != 8)
        {
            return true;
        }

        for (int i = 0; i < ownedHelicopters.Count; i++)
        {
            if (ownedHelicopters[i] == null || string.IsNullOrEmpty(ownedHelicopters[i].PreviewResourcePath))
            {
                return true;
            }

            if (!ownedHelicopters[i].PreviewResourcePath.StartsWith("Garage/HelicopterPrefabs/"))
            {
                return true;
            }
        }

        return false;
    }
}
