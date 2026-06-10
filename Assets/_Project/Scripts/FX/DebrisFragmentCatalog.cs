using UnityEngine;

[CreateAssetMenu(fileName = "DebrisFragmentCatalog", menuName = "TitanDestroyer/VFX/Debris Fragment Catalog")]
public sealed class DebrisFragmentCatalog : ScriptableObject
{
    [SerializeField] private GameObject[] fragmentPrefabs = System.Array.Empty<GameObject>();

    public int Count => fragmentPrefabs != null ? fragmentPrefabs.Length : 0;

    public GameObject GetFragmentPrefab(int index)
    {
        if (fragmentPrefabs == null || fragmentPrefabs.Length == 0)
        {
            return null;
        }

        int wrappedIndex = Mathf.Abs(index) % fragmentPrefabs.Length;
        return fragmentPrefabs[wrappedIndex];
    }

#if UNITY_EDITOR
    public void SetFragmentPrefabs(GameObject[] prefabs)
    {
        fragmentPrefabs = prefabs ?? System.Array.Empty<GameObject>();
    }
#endif
}
