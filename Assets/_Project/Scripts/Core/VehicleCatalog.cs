using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public class VehicleDefinition
{
    [SerializeField] private string id;
    [SerializeField] private string displayName;
    [SerializeField] private GameObject prefab;
    [SerializeField] private Texture2D thumbnail;

    public string Id => id;
    public string DisplayName => displayName;
    public GameObject Prefab => prefab;
    public Texture2D Thumbnail => thumbnail;

#if UNITY_EDITOR
    public void Set(string newId, string newDisplayName, GameObject newPrefab, Texture2D newThumbnail)
    {
        id = newId;
        displayName = newDisplayName;
        prefab = newPrefab;
        thumbnail = newThumbnail;
    }
#endif
}

[CreateAssetMenu(fileName = "VehicleCatalog", menuName = "Titan Destroyer/Vehicle Catalog")]
public class VehicleCatalog : ScriptableObject
{
    [SerializeField] private List<VehicleDefinition> helicopters = new List<VehicleDefinition>();

    public IReadOnlyList<VehicleDefinition> Helicopters => helicopters;

    public VehicleDefinition GetHelicopter(string id)
    {
        if (string.IsNullOrEmpty(id))
        {
            return null;
        }

        for (int i = 0; i < helicopters.Count; i++)
        {
            if (helicopters[i] != null && helicopters[i].Id == id)
            {
                return helicopters[i];
            }
        }

        return null;
    }

#if UNITY_EDITOR
    public void ReplaceHelicopters(List<VehicleDefinition> definitions)
    {
        helicopters = definitions ?? new List<VehicleDefinition>();
    }
#endif
}
