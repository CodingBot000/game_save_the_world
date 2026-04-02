using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;

public static class GarageHelicopterPrefabBuilder
{
    private const string SourceFolder = "Assets/10_Voxels_Helicopters_Pack/Prefabs";
    private const string SourceTexturePath = "Assets/10_Voxels_Helicopters_Pack/Textures/Helicopters_Texture.png";
    private const string TargetRootFolder = "Assets/Resources/Vehicles";
    private const string TargetTextureFolder = "Assets/Resources/Vehicles/Helicopters/Textures";
    private const string TargetMaterialFolder = "Assets/Resources/Vehicles/Helicopters/Materials";
    private const string TargetFolder = "Assets/Resources/Vehicles/Helicopters/Prefabs";
    private const string TargetTexturePath = "Assets/Resources/Vehicles/Helicopters/Textures/Helicopters_Texture.png";
    private const string TargetMaterialPath = "Assets/Resources/Vehicles/Helicopters/Materials/HelicopterRuntimeMaterial.mat";
    private const float RuntimeScaleMultiplier = 0.1f;
    [MenuItem("Tools/Titan Destroyer/Rebuild Vehicle Helicopter Prefabs")]
    public static void Rebuild()
    {
        EnsureFolderExists(TargetRootFolder);
        EnsureFolderExists("Assets/Resources/Vehicles/Helicopters");
        EnsureFolderExists(TargetTextureFolder);
        EnsureFolderExists(TargetMaterialFolder);
        EnsureFolderExists(TargetFolder);
        CopySourceTexture();

        AssetDatabase.Refresh();
        Material runtimeMaterial = EnsureRuntimeMaterial();
        if (runtimeMaterial == null)
        {
            Debug.LogError("Vehicle helicopter builder could not create a runtime material.");
            return;
        }

        DeleteExistingPrefabs();

        foreach (string sourcePath in GetSourcePrefabPaths())
        {
            GameObject sourcePrefab = AssetDatabase.LoadAssetAtPath<GameObject>(sourcePath);
            if (sourcePrefab == null)
            {
                Debug.LogError($"Vehicle helicopter builder could not load source prefab at '{sourcePath}'.");
                continue;
            }

            int index = ExtractIndex(Path.GetFileNameWithoutExtension(sourcePath));
            string displayName = $"Helicopter{index}";

            GameObject root = new GameObject(displayName);
            GameObject visualMount = new GameObject("VehicleModel");
            visualMount.transform.SetParent(root.transform, false);

            GameObject instance = PrefabUtility.InstantiatePrefab(sourcePrefab, visualMount.transform) as GameObject;
            if (instance == null)
            {
                instance = Object.Instantiate(sourcePrefab, visualMount.transform);
            }

            instance.name = sourcePrefab.name;
            instance.transform.localPosition = Vector3.zero;
            instance.transform.localRotation = Quaternion.identity;
            instance.transform.localScale = sourcePrefab.transform.localScale * RuntimeScaleMultiplier;
            PrefabUtility.UnpackPrefabInstance(instance, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
            AssignSharedMaterial(instance, runtimeMaterial);

            string prefabPath = $"{TargetFolder}/{displayName}.prefab";
            PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
            Object.DestroyImmediate(root);
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("Vehicle helicopter prefabs rebuilt.");
    }

    private static void CopySourceTexture()
    {
        if (File.Exists(TargetTexturePath))
        {
            AssetDatabase.DeleteAsset(TargetTexturePath);
        }

        FileUtil.CopyFileOrDirectory(SourceTexturePath, TargetTexturePath);
    }

    private static Material EnsureRuntimeMaterial()
    {
        Texture2D runtimeTexture = AssetDatabase.LoadAssetAtPath<Texture2D>(TargetTexturePath);
        if (runtimeTexture == null)
        {
            return null;
        }

        Material material = AssetDatabase.LoadAssetAtPath<Material>(TargetMaterialPath);
        if (material == null)
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Unlit")
                ?? Shader.Find("Universal Render Pipeline/Lit")
                ?? Shader.Find("Standard");
            if (shader == null)
            {
                return null;
            }

            material = new Material(shader);
            AssetDatabase.CreateAsset(material, TargetMaterialPath);
        }

        material.name = "HelicopterRuntimeMaterial";
        if (material.HasProperty("_BaseMap"))
        {
            material.SetTexture("_BaseMap", runtimeTexture);
        }

        if (material.HasProperty("_MainTex"))
        {
            material.SetTexture("_MainTex", runtimeTexture);
        }

        if (material.HasProperty("_BaseColor"))
        {
            material.SetColor("_BaseColor", Color.white);
        }

        if (material.HasProperty("_Color"))
        {
            material.SetColor("_Color", Color.white);
        }

        if (material.HasProperty("_Surface"))
        {
            material.SetFloat("_Surface", 0f);
        }

        if (material.HasProperty("_Cull"))
        {
            material.SetFloat("_Cull", 2f);
        }

        EditorUtility.SetDirty(material);
        return material;
    }

    private static void AssignSharedMaterial(GameObject root, Material material)
    {
        Renderer[] renderers = root.GetComponentsInChildren<Renderer>(true);
        for (int i = 0; i < renderers.Length; i++)
        {
            Material[] materials = renderers[i].sharedMaterials;
            if (materials == null || materials.Length == 0)
            {
                renderers[i].sharedMaterial = material;
                continue;
            }

            for (int materialIndex = 0; materialIndex < materials.Length; materialIndex++)
            {
                materials[materialIndex] = material;
            }

            renderers[i].sharedMaterials = materials;
        }
    }

    private static IEnumerable<string> GetSourcePrefabPaths()
    {
        string[] guids = AssetDatabase.FindAssets("t:Prefab", new[] { SourceFolder });
        return guids
            .Select(AssetDatabase.GUIDToAssetPath)
            .Where(path => path.EndsWith("_body.prefab"))
            .OrderBy(path => ExtractIndex(Path.GetFileNameWithoutExtension(path)));
    }

    private static void DeleteExistingPrefabs()
    {
        string[] existingPrefabGuids = AssetDatabase.FindAssets("t:Prefab", new[] { TargetFolder });
        for (int i = 0; i < existingPrefabGuids.Length; i++)
        {
            string assetPath = AssetDatabase.GUIDToAssetPath(existingPrefabGuids[i]);
            AssetDatabase.DeleteAsset(assetPath);
        }
    }

    private static int ExtractIndex(string name)
    {
        Match match = Regex.Match(name, @"(\d+)");
        return match.Success && int.TryParse(match.Value, out int index) ? index : int.MaxValue;
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
