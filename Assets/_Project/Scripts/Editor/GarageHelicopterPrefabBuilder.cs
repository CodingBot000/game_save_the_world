using UnityEditor;
using UnityEngine;

public static class GarageHelicopterPrefabBuilder
{
    private const string SourceModelPath = "Assets/TestMap/testmap fbx/Airtest.fbx";
    private const string TargetRootFolder = "Assets/Resources/Garage";
    private const string TargetFolder = "Assets/Resources/Garage/HelicopterPrefabs";

    [MenuItem("Tools/Titan Destroyer/Rebuild Garage Helicopter Prefabs")]
    public static void Rebuild()
    {
        GameObject sourceModel = AssetDatabase.LoadAssetAtPath<GameObject>(SourceModelPath);
        if (sourceModel == null)
        {
            Debug.LogError($"Garage helicopter builder could not load source model at '{SourceModelPath}'.");
            return;
        }

        EnsureFolderExists(TargetRootFolder);
        EnsureFolderExists(TargetFolder);

        for (int i = 1; i <= 8; i++)
        {
            GameObject instance = PrefabUtility.InstantiatePrefab(sourceModel) as GameObject;
            if (instance == null)
            {
                instance = Object.Instantiate(sourceModel);
            }

            instance.name = $"Helicopter{i}";
            instance.transform.position = Vector3.zero;
            instance.transform.rotation = Quaternion.identity;
            instance.transform.localScale = Vector3.one;

            string prefabPath = $"{TargetFolder}/Helicopter{i}.prefab";
            PrefabUtility.SaveAsPrefabAsset(instance, prefabPath);
            Object.DestroyImmediate(instance);
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("Garage helicopter prefabs rebuilt.");
    }

    private static void EnsureFolderExists(string assetPath)
    {
        if (AssetDatabase.IsValidFolder(assetPath))
        {
            return;
        }

        string[] parts = assetPath.Split('/');
        string current = parts[0];
        for (int i = 1; i < parts.Length; i++)
        {
            string next = $"{current}/{parts[i]}";
            if (!AssetDatabase.IsValidFolder(next))
            {
                AssetDatabase.CreateFolder(current, parts[i]);
            }

            current = next;
        }
    }
}
