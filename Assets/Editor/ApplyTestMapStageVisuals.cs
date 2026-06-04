using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class ApplyTestMapStageVisuals
{
    private const string BattleArenaScenePath = "Assets/Scenes/BattleArena.unity/BattleArena.unity";
    private const string StageVisualAssetPath = "Assets/TestMap/testmap fbx/testmap.fbx";
    private const string BossVisualAssetPath = "Assets/TestMap/testmap fbx/Kaijutest.fbx";
    private const string VehicleCatalogAssetPath = "Assets/Resources/Vehicles/VehicleCatalog.asset";
    private const string VehiclePrefabFolder = "Assets/Resources/Vehicles/Helicopters/Prefabs";
    private const string BossVisualMaterialPath = "Assets/Materials/StageVisuals/BossVisual_TestMap_Black.mat";

    private const string StageVisualRootName = "StageVisualRoot";
    private const string BossVisualRootName = "BossVisualRoot";
    private const string PlayerVisualRootName = "PlayerVisualRoot";
    private const string AllyVisualRootName = "AllyVisualRoot";
    private const string StageVisualInstanceName = "StageVisual_TestMap";

    private static readonly Vector3 StageLocalPosition = new(0f, 2f, 0f);
    private static readonly Vector3 StageLocalEulerAngles = Vector3.zero;
    private static readonly Vector3 StageLocalScale = new(2f, 2f, 2f);

    private static readonly Vector3 BossLocalPosition = Vector3.zero;
    private static readonly Vector3 BossLocalEulerAngles = new(270f, 0f, 0f);
    private static readonly Vector3 BossLocalScale = Vector3.one;

    private static readonly Vector3 PlayerLocalPosition = new(0f, 1f, 0f);
    private static readonly Vector3 PlayerLocalEulerAngles = new(270.01978f, 0f, 0f);
    private static readonly Vector3 PlayerLocalScale = new Vector3(1.442044f, 0.6920179f, 0.396217f);

    private static readonly Vector3 AllyLocalPosition = new(0f, 1f, 0f);
    private static readonly Vector3 AllyLocalEulerAngles = new(270.01978f, 0f, 0f);
    private static readonly Vector3 AllyLocalScale = new Vector3(1.1f, 0.55f, 0.32f);

    private const float PlayerOrbitHeight = 12f;
    private static readonly Color BossVisualColor = new(0.06f, 0.06f, 0.06f, 1f);

    [MenuItem("Tools/TitanDestroyer/Apply TestMap Stage Visuals")]
    public static void Apply()
    {
        Scene scene = EnsureBattleArenaScene();
        if (!scene.IsValid())
        {
            Debug.LogError("BattleArena scene could not be loaded.");
            return;
        }

        Transform battleArenaRoot = FindRoot(scene, "BattleArenaRoot");
        if (battleArenaRoot == null)
        {
            Debug.LogError("BattleArenaRoot was not found.");
            return;
        }

        Transform environment = FindChild(battleArenaRoot, "Environment");
        Transform characters = FindChild(battleArenaRoot, "Characters");
        Transform spawnPoints = FindChild(battleArenaRoot, "SpawnPoints");
        Transform bossRoot = FindChild(characters, "BossPlaceholder");
        Transform playerRoot = FindChild(characters, "PlayerPlaceholder");
        Transform allyRoot = FindChild(characters, "AllyPlaceholder");

        if (environment == null || characters == null || spawnPoints == null || bossRoot == null || playerRoot == null)
        {
            Debug.LogError("BattleArena scene is missing one or more required hierarchy nodes.");
            return;
        }

        GameObject stageVisualAsset = AssetDatabase.LoadAssetAtPath<GameObject>(StageVisualAssetPath);
        GameObject bossVisualAsset = AssetDatabase.LoadAssetAtPath<GameObject>(BossVisualAssetPath);
        GameObject playerVisualAsset = LoadDefaultVehiclePrefab();
        if (stageVisualAsset == null || bossVisualAsset == null || playerVisualAsset == null)
        {
            Debug.LogError("One or more BattleArena visual assets could not be loaded.");
            return;
        }

        Transform stageVisualRoot = EnsureChild(environment, StageVisualRootName);
        Transform bossVisualRoot = EnsureChild(bossRoot, BossVisualRootName);
        Transform playerVisualRoot = EnsureChild(playerRoot, PlayerVisualRootName);
        Transform allyVisualRoot = allyRoot != null ? EnsureChild(allyRoot, AllyVisualRootName) : null;
        LocalTransformSnapshot stageVisualTransform = CaptureLocalTransform(
            FindChild(stageVisualRoot, StageVisualInstanceName),
            StageLocalPosition,
            StageLocalEulerAngles,
            StageLocalScale);

        ResetLocal(stageVisualRoot, Vector3.zero, Vector3.zero, Vector3.one);
        ResetLocal(bossVisualRoot, Vector3.zero, Vector3.zero, Vector3.one);
        ResetLocal(playerVisualRoot, Vector3.zero, Vector3.zero, Vector3.one);
        if (allyVisualRoot != null)
        {
            ResetLocal(allyVisualRoot, Vector3.zero, Vector3.zero, Vector3.one);
        }

        ClearChildren(stageVisualRoot);
        ClearChildren(bossVisualRoot);
        ClearChildren(playerVisualRoot);
        if (allyVisualRoot != null)
        {
            ClearChildren(allyVisualRoot);
        }

        GameObject stageVisual = InstantiateMountedVisual(
            stageVisualAsset,
            stageVisualRoot,
            StageVisualInstanceName,
            StageLocalPosition,
            StageLocalEulerAngles,
            StageLocalScale);
        stageVisualTransform.ApplyTo(stageVisual.transform);
        GameObject bossVisual = InstantiateMountedVisual(bossVisualAsset, bossVisualRoot, "BossVisual_TestMap", BossLocalPosition, BossLocalEulerAngles, BossLocalScale);
        InstantiateMountedVisual(playerVisualAsset, playerVisualRoot, "PlayerVisual_Vehicle", PlayerLocalPosition, PlayerLocalEulerAngles, PlayerLocalScale);
        if (allyVisualRoot != null)
        {
            InstantiateMountedVisual(playerVisualAsset, allyVisualRoot, "AllyVisual_Vehicle", AllyLocalPosition, AllyLocalEulerAngles, AllyLocalScale);
        }

        ApplySharedMaterial(bossVisual, GetOrCreateMaterial(BossVisualMaterialPath, BossVisualColor));

        RemoveRenderersOutsideMount(environment, StageVisualRootName);
        RemoveRenderersOutsideMount(bossRoot, BossVisualRootName);
        RemoveRenderersOutsideMount(playerRoot, PlayerVisualRootName);
        if (allyRoot != null)
        {
            RemoveRenderersOutsideMount(allyRoot, AllyVisualRootName);
        }

        RemoveRenderersRecursively(spawnPoints);
        SetOrbitHeight(playerRoot, PlayerOrbitHeight);

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Debug.Log("Applied TestMap stage visuals to BattleArena.");
    }

    private static GameObject LoadDefaultVehiclePrefab()
    {
        VehicleCatalog catalog = AssetDatabase.LoadAssetAtPath<VehicleCatalog>(VehicleCatalogAssetPath);
        if (catalog != null && catalog.Helicopters.Count > 0 && catalog.Helicopters[0] != null && catalog.Helicopters[0].Prefab != null)
        {
            return catalog.Helicopters[0].Prefab;
        }

        string[] prefabGuids = AssetDatabase.FindAssets("t:Prefab", new[] { VehiclePrefabFolder });
        if (prefabGuids.Length == 0)
        {
            return null;
        }

        System.Array.Sort(prefabGuids, (left, right) =>
        {
            string leftPath = AssetDatabase.GUIDToAssetPath(left);
            string rightPath = AssetDatabase.GUIDToAssetPath(right);
            return string.CompareOrdinal(leftPath, rightPath);
        });

        return AssetDatabase.LoadAssetAtPath<GameObject>(AssetDatabase.GUIDToAssetPath(prefabGuids[0]));
    }

    private static Scene EnsureBattleArenaScene()
    {
        Scene activeScene = SceneManager.GetActiveScene();
        if (activeScene.path == BattleArenaScenePath)
        {
            return activeScene;
        }

        return EditorSceneManager.OpenScene(BattleArenaScenePath, OpenSceneMode.Single);
    }

    private static Transform FindRoot(Scene scene, string name)
    {
        foreach (GameObject rootObject in scene.GetRootGameObjects())
        {
            if (rootObject.name == name)
            {
                return rootObject.transform;
            }
        }

        return null;
    }

    private static Transform FindChild(Transform root, string name)
    {
        if (root == null)
        {
            return null;
        }

        foreach (Transform child in root.GetComponentsInChildren<Transform>(true))
        {
            if (child.name == name)
            {
                return child;
            }
        }

        return null;
    }

    private static Transform EnsureChild(Transform parent, string name)
    {
        Transform existing = parent.Find(name);
        if (existing != null)
        {
            return existing;
        }

        GameObject child = new(name);
        Undo.RegisterCreatedObjectUndo(child, $"Create {name}");
        child.transform.SetParent(parent, false);
        child.transform.localPosition = Vector3.zero;
        child.transform.localRotation = Quaternion.identity;
        child.transform.localScale = Vector3.one;
        return child.transform;
    }

    private static void ClearChildren(Transform root)
    {
        List<GameObject> children = new();
        for (int i = 0; i < root.childCount; i++)
        {
            children.Add(root.GetChild(i).gameObject);
        }

        foreach (GameObject child in children)
        {
            Undo.DestroyObjectImmediate(child);
        }
    }

    private static GameObject InstantiateMountedVisual(
        GameObject sourceAsset,
        Transform parent,
        string instanceName,
        Vector3 localPosition,
        Vector3 localEulerAngles,
        Vector3 localScale)
    {
        GameObject instance = PrefabUtility.InstantiatePrefab(sourceAsset, parent) as GameObject;
        if (instance == null)
        {
            throw new System.InvalidOperationException($"Failed to instantiate visual asset: {sourceAsset.name}");
        }

        Undo.RegisterCreatedObjectUndo(instance, $"Instantiate {instanceName}");
        instance.name = instanceName;
        ResetLocal(instance.transform, localPosition, localEulerAngles, localScale);
        return instance;
    }

    private static LocalTransformSnapshot CaptureLocalTransform(Transform target, Vector3 fallbackPosition, Vector3 fallbackEulerAngles, Vector3 fallbackScale)
    {
        if (target == null)
        {
            return new LocalTransformSnapshot(fallbackPosition, Quaternion.Euler(fallbackEulerAngles), fallbackScale);
        }

        return new LocalTransformSnapshot(target.localPosition, target.localRotation, target.localScale);
    }

    private static void ResetLocal(Transform target, Vector3 localPosition, Vector3 localEulerAngles, Vector3 localScale)
    {
        target.localPosition = localPosition;
        target.localRotation = Quaternion.Euler(localEulerAngles);
        target.localScale = localScale;
    }

    private readonly struct LocalTransformSnapshot
    {
        private readonly Vector3 localPosition;
        private readonly Quaternion localRotation;
        private readonly Vector3 localScale;

        public LocalTransformSnapshot(Vector3 localPosition, Quaternion localRotation, Vector3 localScale)
        {
            this.localPosition = localPosition;
            this.localRotation = localRotation;
            this.localScale = localScale;
        }

        public void ApplyTo(Transform target)
        {
            target.localPosition = localPosition;
            target.localRotation = localRotation;
            target.localScale = localScale;
        }
    }

    private static Material GetOrCreateMaterial(string materialPath, Color color)
    {
        Material existing = AssetDatabase.LoadAssetAtPath<Material>(materialPath);
        if (existing != null)
        {
            ConfigureSolidColorMaterial(existing, color);
            return existing;
        }

        string directory = System.IO.Path.GetDirectoryName(materialPath);
        if (!AssetDatabase.IsValidFolder(directory))
        {
            string parent = "Assets/Materials";
            if (!AssetDatabase.IsValidFolder(parent))
            {
                AssetDatabase.CreateFolder("Assets", "Materials");
            }

            AssetDatabase.CreateFolder(parent, "StageVisuals");
        }

        Shader shader = Shader.Find("Universal Render Pipeline/Lit");
        if (shader == null)
        {
            shader = Shader.Find("Standard");
        }

        Material material = new(shader);
        ConfigureSolidColorMaterial(material, color);
        AssetDatabase.CreateAsset(material, materialPath);
        return material;
    }

    private static void ConfigureSolidColorMaterial(Material material, Color color)
    {
        if (material.HasProperty("_BaseColor"))
        {
            material.SetColor("_BaseColor", color);
        }

        if (material.HasProperty("_Color"))
        {
            material.SetColor("_Color", color);
        }

        material.enableInstancing = true;
        EditorUtility.SetDirty(material);
    }

    private static void ApplySharedMaterial(GameObject root, Material material)
    {
        foreach (Renderer renderer in root.GetComponentsInChildren<Renderer>(true))
        {
            Undo.RecordObject(renderer, "Assign player visual material");
            renderer.sharedMaterial = material;
            EditorUtility.SetDirty(renderer);
        }
    }

    private static void RemoveRenderersOutsideMount(Transform root, string keepMountName)
    {
        Transform keepMount = root.Find(keepMountName);
        List<Renderer> renderersToRemove = new();

        foreach (Renderer renderer in root.GetComponentsInChildren<Renderer>(true))
        {
            if (keepMount != null && renderer.transform.IsChildOf(keepMount))
            {
                continue;
            }

            renderersToRemove.Add(renderer);
        }

        foreach (Renderer renderer in renderersToRemove)
        {
            Undo.DestroyObjectImmediate(renderer);
        }
    }

    private static void RemoveRenderersRecursively(Transform root)
    {
        List<Renderer> renderersToRemove = new(root.GetComponentsInChildren<Renderer>(true));
        foreach (Renderer renderer in renderersToRemove)
        {
            Undo.DestroyObjectImmediate(renderer);
        }
    }

    private static void SetOrbitHeight(Transform playerRoot, float orbitHeight)
    {
        PlayerOrbitController orbitController = playerRoot.GetComponent<PlayerOrbitController>();
        if (orbitController == null)
        {
            Debug.LogWarning("PlayerOrbitController was not found on PlayerPlaceholder.");
            return;
        }

        SerializedObject serializedObject = new(orbitController);
        SerializedProperty orbitHeightProperty = serializedObject.FindProperty("orbitHeight");
        if (orbitHeightProperty == null)
        {
            Debug.LogWarning("orbitHeight serialized property was not found.");
            return;
        }

        orbitHeightProperty.floatValue = orbitHeight;
        serializedObject.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(orbitController);
    }
}
