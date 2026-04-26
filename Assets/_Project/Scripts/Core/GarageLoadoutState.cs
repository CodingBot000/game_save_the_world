using System;
using UnityEngine;

public class GarageLoadoutState : MonoBehaviour
{
    private const string RuntimeObjectName = "GarageLoadoutState";
    private const string PrimaryWeaponPrefsKey = "GarageLoadout.PrimaryWeapon";
    private const string SecondaryWeapon1PrefsKey = "GarageLoadout.SecondaryWeapon1";
    private const string SecondaryWeapon2PrefsKey = "GarageLoadout.SecondaryWeapon2";
    private const string ArmorPrefsKey = "GarageLoadout.Armor";

    private static GarageLoadoutState instance;

    [SerializeField] private string primaryWeaponId = "gatling_mk1";
    [SerializeField] private string secondaryWeapon1Id = "rocket_pod_a";
    [SerializeField] private string secondaryWeapon2Id = "missile_pylon_a";
    [SerializeField] private string armorId = "light_armor";

    public event Action LoadoutChanged;

    public static GarageLoadoutState Instance => EnsureInitialized();

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Bootstrap()
    {
        EnsureInitialized();
    }

    public static GarageLoadoutState EnsureInitialized()
    {
        if (instance != null)
        {
            return instance;
        }

        instance = FindAnyObjectByType<GarageLoadoutState>();
        if (instance != null)
        {
            instance.LoadPersistedSelections();
            return instance;
        }

        GameObject runtimeObject = new GameObject(RuntimeObjectName);
        instance = runtimeObject.AddComponent<GarageLoadoutState>();
        instance.LoadPersistedSelections();
        DontDestroyOnLoad(runtimeObject);
        return instance;
    }

    public string GetSelection(GarageLoadoutSlotType slotType)
    {
        switch (slotType)
        {
            case GarageLoadoutSlotType.PrimaryWeapon:
                return primaryWeaponId;
            case GarageLoadoutSlotType.SecondaryWeapon1:
                return secondaryWeapon1Id;
            case GarageLoadoutSlotType.SecondaryWeapon2:
                return secondaryWeapon2Id;
            case GarageLoadoutSlotType.Armor:
                return armorId;
            default:
                return string.Empty;
        }
    }

    public void SetSelection(GarageLoadoutSlotType slotType, string itemId)
    {
        if (string.IsNullOrWhiteSpace(itemId) || GetSelection(slotType) == itemId)
        {
            return;
        }

        switch (slotType)
        {
            case GarageLoadoutSlotType.PrimaryWeapon:
                primaryWeaponId = itemId;
                PlayerPrefs.SetString(PrimaryWeaponPrefsKey, itemId);
                break;
            case GarageLoadoutSlotType.SecondaryWeapon1:
                secondaryWeapon1Id = itemId;
                PlayerPrefs.SetString(SecondaryWeapon1PrefsKey, itemId);
                break;
            case GarageLoadoutSlotType.SecondaryWeapon2:
                secondaryWeapon2Id = itemId;
                PlayerPrefs.SetString(SecondaryWeapon2PrefsKey, itemId);
                break;
            case GarageLoadoutSlotType.Armor:
                armorId = itemId;
                PlayerPrefs.SetString(ArmorPrefsKey, itemId);
                break;
        }

        PlayerPrefs.Save();
        LoadoutChanged?.Invoke();
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
        LoadPersistedSelections();
    }

    private void LoadPersistedSelections()
    {
        primaryWeaponId = PlayerPrefs.GetString(PrimaryWeaponPrefsKey, primaryWeaponId);
        secondaryWeapon1Id = PlayerPrefs.GetString(SecondaryWeapon1PrefsKey, secondaryWeapon1Id);
        secondaryWeapon2Id = PlayerPrefs.GetString(SecondaryWeapon2PrefsKey, secondaryWeapon2Id);
        armorId = PlayerPrefs.GetString(ArmorPrefsKey, armorId);
    }
}
