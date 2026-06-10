using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

public static class MonsterDebrisFragmentCatalogBuilder
{
    private const string SourceFolder = "Assets/_Project/Art/VFX/MonsterDebrisParticles";
    private const string CatalogFolder = "Assets/_Project/Resources/VFX";
    private const string CatalogPath = CatalogFolder + "/MonsterDebrisFragmentCatalog.asset";

    [MenuItem("TitanDestroyer/VFX/Rebuild Monster Debris Fragment Catalog")]
    public static void RebuildCatalog()
    {
        string[] guids = AssetDatabase.FindAssets("t:GameObject", new[] { SourceFolder });
        List<GameObject> fragments = new();
        for (int i = 0; i < guids.Length; i++)
        {
            string path = AssetDatabase.GUIDToAssetPath(guids[i]);
            if (!path.EndsWith(".fbx", System.StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            GameObject fragment = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (fragment != null)
            {
                fragments.Add(fragment);
            }
        }

        fragments.Sort((left, right) => string.CompareOrdinal(left.name, right.name));
        EnsureFolder(CatalogFolder);

        DebrisFragmentCatalog catalog = AssetDatabase.LoadAssetAtPath<DebrisFragmentCatalog>(CatalogPath);
        if (catalog == null)
        {
            catalog = ScriptableObject.CreateInstance<DebrisFragmentCatalog>();
            AssetDatabase.CreateAsset(catalog, CatalogPath);
        }

        catalog.SetFragmentPrefabs(fragments.ToArray());
        EditorUtility.SetDirty(catalog);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Debug.Log($"Rebuilt debris fragment catalog with {fragments.Count} fragment prefab(s): {CatalogPath}");
    }

    private static void EnsureFolder(string folderPath)
    {
        string[] parts = folderPath.Split('/');
        string currentPath = parts[0];
        for (int i = 1; i < parts.Length; i++)
        {
            string nextPath = currentPath + "/" + parts[i];
            if (!AssetDatabase.IsValidFolder(nextPath))
            {
                AssetDatabase.CreateFolder(currentPath, parts[i]);
            }

            currentPath = nextPath;
        }
    }
}
