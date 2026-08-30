#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

public static class BackgroundGroundArmoredUnitsBuilder
{
    private const string BattleArenaScenePath = "Assets/Scenes/BattleArena.unity/BattleArena.unity";
    private const string GroundArtRoot = "Assets/_Project/Art/Environment/BackgroundAllyArmy/Ground";
    private const string ModelFolder = GroundArtRoot + "/Models";
    private const string TextureFolder = GroundArtRoot + "/Textures";
    private const string MaterialFolder = GroundArtRoot + "/Materials";
    private const string TankModelPath = ModelFolder + "/background_tank_tracked_rounded_optimized_800.fbx";
    private const string GatlingModelPath = ModelFolder + "/background_tank_wheeled_gatling_optimized_800.fbx";
    private const string MortarModelPath = ModelFolder + "/background_tank_tracked_cannon_optimized_800.fbx";
    private const string BaseColorPath = TextureFolder + "/background_ground_vehicles_basecolor_256.png";
    private const string NormalPath = TextureFolder + "/background_ground_vehicles_normal_128.png";
    private const string MaskPath = TextureFolder + "/background_ground_vehicles_unity_metallic_smoothness_128.png";
    private const string VehicleMaterialPath = MaterialFolder + "/BackgroundGroundVehicles.mat";
    private const string TracerMaterialPath = MaterialFolder + "/BackgroundGroundTracer.mat";
    private const string MortarTrailMaterialPath = MaterialFolder + "/BackgroundGroundMortarTrail.mat";
    private const string ExplosionMaterialPath = MaterialFolder + "/BackgroundGroundExplosion.mat";
    private const string PrefabFolder = "Assets/Prefabs/Environment/BackgroundAllyArmy/Ground";
    private const string TankPrefabPath = PrefabFolder + "/BackgroundTank_800.prefab";
    private const string GatlingPrefabPath = PrefabFolder + "/BackgroundGatlingCarrier_800.prefab";
    private const string MortarPrefabPath = PrefabFolder + "/BackgroundMortarCarrier_820.prefab";
    private const string BattleArenaRootName = "BattleArenaRoot";
    private const string ArmyRootName = "AmbientAllyArmyRoot";
    private const string GroundRootName = "GroundArmoredUnits";

    [MenuItem("Tools/Titan Destroyer/Rebuild Background Ground Armored Units")]
    public static void RebuildLoadedBattleArena()
    {
        if (!BuildAssets())
        {
            return;
        }

        Scene scene = SceneManager.GetActiveScene();
        if (!scene.IsValid() || scene.path != BattleArenaScenePath)
        {
            scene = EditorSceneManager.OpenScene(BattleArenaScenePath, OpenSceneMode.Single);
        }

        if (!BuildScene(scene))
        {
            return;
        }

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("Background ground armored unit assets and BattleArena scene bindings rebuilt.");
    }

    public static void RebuildBattleArenaForBatch()
    {
        if (!BuildAssets())
        {
            throw new System.InvalidOperationException("Ground armored unit asset build failed.");
        }

        Scene scene = EditorSceneManager.OpenScene(BattleArenaScenePath, OpenSceneMode.Single);
        if (!BuildScene(scene))
        {
            throw new System.InvalidOperationException("Ground armored unit scene build failed.");
        }

        EditorSceneManager.SaveScene(scene);
        AssetDatabase.SaveAssets();
        Debug.Log("BACKGROUND_GROUND_BUILD_OK");
    }

    private static bool BuildAssets()
    {
        EnsureFolder(MaterialFolder);
        EnsureFolder(PrefabFolder);
        AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);

        if (!ConfigureModelImporter(TankModelPath)
            || !ConfigureModelImporter(GatlingModelPath)
            || !ConfigureModelImporter(MortarModelPath))
        {
            return false;
        }

        ConfigureTextureImporter(BaseColorPath, TextureImporterType.Default, true, 256, alpha: false);
        ConfigureTextureImporter(NormalPath, TextureImporterType.NormalMap, false, 128, alpha: false);
        ConfigureTextureImporter(MaskPath, TextureImporterType.Default, false, 128, alpha: false);

        Material vehicleMaterial = BuildVehicleMaterial();
        Material tracerMaterial = BuildTransparentUnlitMaterial(
            TracerMaterialPath,
            new Color(1f, 0.48f, 0.12f, 0.64f),
            additive: true);
        Material mortarTrailMaterial = BuildTransparentUnlitMaterial(
            MortarTrailMaterialPath,
            new Color(1f, 0.38f, 0.08f, 0.5f),
            additive: true);
        Material explosionMaterial = BuildTransparentUnlitMaterial(
            ExplosionMaterialPath,
            new Color(1f, 0.28f, 0.05f, 0.46f),
            additive: true);

        if (vehicleMaterial == null || tracerMaterial == null || mortarTrailMaterial == null || explosionMaterial == null)
        {
            Debug.LogError("Background ground armored unit material creation failed.");
            return false;
        }

        return BuildVehiclePrefab(TankModelPath, TankPrefabPath, "BackgroundTank_800", vehicleMaterial, tracerMaterial)
               && BuildVehiclePrefab(GatlingModelPath, GatlingPrefabPath, "BackgroundGatlingCarrier_800", vehicleMaterial, tracerMaterial)
               && BuildVehiclePrefab(MortarModelPath, MortarPrefabPath, "BackgroundMortarCarrier_820", vehicleMaterial, tracerMaterial);
    }

    private static bool ConfigureModelImporter(string path)
    {
        ModelImporter importer = AssetImporter.GetAtPath(path) as ModelImporter;
        if (importer == null)
        {
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport | ImportAssetOptions.ForceUpdate);
            importer = AssetImporter.GetAtPath(path) as ModelImporter;
        }

        if (importer == null)
        {
            Debug.LogError($"Background ground model was not found at {path}.");
            return false;
        }

        importer.importAnimation = false;
        importer.importCameras = false;
        importer.importLights = false;
        importer.importBlendShapes = false;
        importer.materialImportMode = ModelImporterMaterialImportMode.None;
        importer.globalScale = 100f;
        importer.meshCompression = ModelImporterMeshCompression.Low;
        importer.isReadable = false;
        importer.optimizeMeshPolygons = true;
        importer.optimizeMeshVertices = true;
        importer.SaveAndReimport();
        return true;
    }

    private static void ConfigureTextureImporter(
        string path,
        TextureImporterType textureType,
        bool sRgb,
        int maxSize,
        bool alpha)
    {
        TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter;
        if (importer == null)
        {
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport | ImportAssetOptions.ForceUpdate);
            importer = AssetImporter.GetAtPath(path) as TextureImporter;
        }

        if (importer == null)
        {
            Debug.LogError($"Background ground texture was not found at {path}.");
            return;
        }

        importer.textureType = textureType;
        importer.sRGBTexture = sRgb;
        importer.maxTextureSize = maxSize;
        importer.mipmapEnabled = true;
        importer.alphaIsTransparency = alpha;
        importer.wrapMode = TextureWrapMode.Clamp;
        importer.textureCompression = TextureImporterCompression.CompressedHQ;
        importer.SaveAndReimport();
    }

    private static Material BuildVehicleMaterial()
    {
        Shader shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
        if (shader == null)
        {
            return null;
        }

        Material material = LoadOrCreateMaterial(VehicleMaterialPath, shader);
        material.name = "BackgroundGroundVehicles";
        material.enableInstancing = true;
        Texture2D baseColor = AssetDatabase.LoadAssetAtPath<Texture2D>(BaseColorPath);
        Texture2D normal = AssetDatabase.LoadAssetAtPath<Texture2D>(NormalPath);
        Texture2D mask = AssetDatabase.LoadAssetAtPath<Texture2D>(MaskPath);
        SetTexture(material, "_BaseMap", "_MainTex", baseColor);
        if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", new Color(0.78f, 0.82f, 0.76f, 1f));
        if (material.HasProperty("_Color")) material.SetColor("_Color", new Color(0.78f, 0.82f, 0.76f, 1f));

        if (normal != null && material.HasProperty("_BumpMap"))
        {
            material.SetTexture("_BumpMap", normal);
            material.SetFloat("_BumpScale", 0.68f);
            material.EnableKeyword("_NORMALMAP");
        }

        if (mask != null && material.HasProperty("_MetallicGlossMap"))
        {
            material.SetTexture("_MetallicGlossMap", mask);
            material.SetFloat("_Metallic", 1f);
            material.SetFloat("_Smoothness", 0.68f);
            material.EnableKeyword("_METALLICSPECGLOSSMAP");
        }

        if (material.HasProperty("_Cull")) material.SetFloat("_Cull", (float)CullMode.Back);
        if (material.HasProperty("_Surface")) material.SetFloat("_Surface", 0f);
        if (material.HasProperty("_ZWrite")) material.SetFloat("_ZWrite", 1f);
        material.renderQueue = -1;
        EditorUtility.SetDirty(material);
        return material;
    }

    private static Material BuildTransparentUnlitMaterial(string path, Color color, bool additive)
    {
        Shader shader = Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Unlit/Transparent");
        if (shader == null)
        {
            return null;
        }

        Material material = LoadOrCreateMaterial(path, shader);
        material.name = System.IO.Path.GetFileNameWithoutExtension(path);
        material.enableInstancing = true;
        if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", color);
        if (material.HasProperty("_Color")) material.SetColor("_Color", color);
        if (material.HasProperty("_Surface")) material.SetFloat("_Surface", 1f);
        if (material.HasProperty("_Blend")) material.SetFloat("_Blend", additive ? 2f : 0f);
        if (material.HasProperty("_SrcBlend")) material.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
        if (material.HasProperty("_DstBlend")) material.SetFloat("_DstBlend", additive ? (float)BlendMode.One : (float)BlendMode.OneMinusSrcAlpha);
        if (material.HasProperty("_ZWrite")) material.SetFloat("_ZWrite", 0f);
        if (material.HasProperty("_Cull")) material.SetFloat("_Cull", (float)CullMode.Off);
        material.SetOverrideTag("RenderType", "Transparent");
        material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        material.renderQueue = (int)RenderQueue.Transparent;
        EditorUtility.SetDirty(material);
        return material;
    }

    private static Material LoadOrCreateMaterial(string path, Shader shader)
    {
        Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (material != null)
        {
            material.shader = shader;
            return material;
        }

        material = new Material(shader);
        AssetDatabase.CreateAsset(material, path);
        return material;
    }

    private static void SetTexture(Material material, string primary, string fallback, Texture texture)
    {
        if (material.HasProperty(primary)) material.SetTexture(primary, texture);
        else if (material.HasProperty(fallback)) material.SetTexture(fallback, texture);
    }

    private static bool BuildVehiclePrefab(
        string modelPath,
        string prefabPath,
        string prefabName,
        Material vehicleMaterial,
        Material flashMaterial)
    {
        GameObject modelAsset = AssetDatabase.LoadAssetAtPath<GameObject>(modelPath);
        if (modelAsset == null)
        {
            Debug.LogError($"Background ground model could not be loaded at {modelPath}.");
            return false;
        }

        GameObject root = new(prefabName);
        GameObject groundPivotObject = new("GroundPivot");
        groundPivotObject.transform.SetParent(root.transform, false);
        GameObject visualRootObject = new("VisualRoot");
        visualRootObject.transform.SetParent(root.transform, false);

        GameObject modelInstance = PrefabUtility.InstantiatePrefab(modelAsset, visualRootObject.transform) as GameObject;
        if (modelInstance == null)
        {
            modelInstance = Object.Instantiate(modelAsset, visualRootObject.transform);
        }

        modelInstance.name = "Model";
        modelInstance.transform.SetLocalPositionAndRotation(Vector3.zero, Quaternion.identity);
        modelInstance.transform.localScale = Vector3.one;
        PrefabUtility.UnpackPrefabInstance(modelInstance, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
        AssignSharedMaterialAndRendererSettings(modelInstance, vehicleMaterial);

        Transform turret = FindDescendant(modelInstance.transform, "TurretYawPivot");
        Transform weapon = FindDescendant(modelInstance.transform, "BarrelPitchPivot");
        Transform muzzle = FindDescendant(modelInstance.transform, "Muzzle");
        if (turret == null || weapon == null || muzzle == null)
        {
            Debug.LogError($"{prefabName} is missing TurretYawPivot, BarrelPitchPivot, or Muzzle.");
            Object.DestroyImmediate(root);
            return false;
        }

        Renderer[] flashes = CreateMuzzleFlash(muzzle, flashMaterial, out Transform flashRoot);
        BackgroundGroundUnitView view = root.AddComponent<BackgroundGroundUnitView>();
        view.ConfigureForEditor(visualRootObject.transform, turret, weapon, muzzle, flashRoot, flashes);
        PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
        Object.DestroyImmediate(root);
        return AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath) != null;
    }

    private static void AssignSharedMaterialAndRendererSettings(GameObject root, Material material)
    {
        Renderer[] renderers = root.GetComponentsInChildren<Renderer>(true);
        for (int i = 0; i < renderers.Length; i++)
        {
            Renderer renderer = renderers[i];
            Material[] materials = renderer.sharedMaterials;
            if (materials == null || materials.Length == 0)
            {
                renderer.sharedMaterial = material;
            }
            else
            {
                for (int j = 0; j < materials.Length; j++) materials[j] = material;
                renderer.sharedMaterials = materials;
            }

            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            renderer.lightProbeUsage = LightProbeUsage.Off;
            renderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
            renderer.motionVectorGenerationMode = MotionVectorGenerationMode.ForceNoMotion;
        }
    }

    private static Renderer[] CreateMuzzleFlash(Transform muzzle, Material material, out Transform flashRoot)
    {
        GameObject root = new("MuzzleFlash");
        flashRoot = root.transform;
        flashRoot.SetParent(muzzle, false);
        flashRoot.localPosition = new Vector3(0f, 0f, 0.08f);
        MeshRenderer horizontal = CreateEffectQuad("FlashHorizontal", flashRoot, Quaternion.Euler(90f, 0f, 0f), new Vector3(0.12f, 0.34f, 1f), material);
        MeshRenderer vertical = CreateEffectQuad("FlashVertical", flashRoot, Quaternion.Euler(0f, 90f, 0f), new Vector3(0.34f, 0.12f, 1f), material);
        horizontal.enabled = false;
        vertical.enabled = false;
        return new Renderer[] { horizontal, vertical };
    }

    private static MeshRenderer CreateEffectQuad(string name, Transform parent, Quaternion rotation, Vector3 scale, Material material)
    {
        GameObject quad = GameObject.CreatePrimitive(PrimitiveType.Quad);
        quad.name = name;
        quad.transform.SetParent(parent, false);
        quad.transform.SetLocalPositionAndRotation(Vector3.zero, rotation);
        quad.transform.localScale = scale;
        Collider collider = quad.GetComponent<Collider>();
        if (collider != null) Object.DestroyImmediate(collider);
        MeshRenderer renderer = quad.GetComponent<MeshRenderer>();
        renderer.sharedMaterial = material;
        renderer.shadowCastingMode = ShadowCastingMode.Off;
        renderer.receiveShadows = false;
        renderer.lightProbeUsage = LightProbeUsage.Off;
        renderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
        return renderer;
    }

    private static bool BuildScene(Scene scene)
    {
        if (!scene.IsValid())
        {
            Debug.LogError("Ground armored unit scene build failed because no valid scene is loaded.");
            return false;
        }

        Transform battleRoot = FindSceneTransform(scene, BattleArenaRootName);
        Transform armyRoot = FindSceneTransform(scene, ArmyRootName);
        if (battleRoot == null || armyRoot == null)
        {
            Debug.LogError("Ground armored unit scene build failed because BattleArenaRoot or AmbientAllyArmyRoot was not found.");
            return false;
        }

        BackgroundCosmeticCombatBudget budget = armyRoot.GetComponent<BackgroundCosmeticCombatBudget>();
        if (budget == null)
        {
            budget = armyRoot.gameObject.AddComponent<BackgroundCosmeticCombatBudget>();
        }

        Transform groundRoot = EnsureSceneChild(armyRoot, GroundRootName);
        Transform unitsRoot = EnsureSceneChild(groundRoot, "GroundUnitsRoot");
        Transform vfxRoot = EnsureSceneChild(groundRoot, "GroundCosmeticVfxRoot");
        BackgroundGroundArmoredUnitsRuntime runtime = groundRoot.GetComponent<BackgroundGroundArmoredUnitsRuntime>();
        if (runtime == null)
        {
            runtime = groundRoot.gameObject.AddComponent<BackgroundGroundArmoredUnitsRuntime>();
        }

        runtime.ConfigureAssetsForEditor(
            AssetDatabase.LoadAssetAtPath<GameObject>(TankPrefabPath),
            AssetDatabase.LoadAssetAtPath<GameObject>(GatlingPrefabPath),
            AssetDatabase.LoadAssetAtPath<GameObject>(MortarPrefabPath),
            AssetDatabase.LoadAssetAtPath<Material>(TracerMaterialPath),
            AssetDatabase.LoadAssetAtPath<Material>(MortarTrailMaterialPath),
            AssetDatabase.LoadAssetAtPath<Material>(ExplosionMaterialPath),
            unitsRoot,
            vfxRoot);
        runtime.ApplyAuthoredDefaultsForEditorIfNeeded();
        EditorUtility.SetDirty(runtime);
        EditorUtility.SetDirty(budget);

        BattleController battleController = FindSceneComponent<BattleController>(scene);
        if (battleController != null)
        {
            SerializedObject serializedBattle = new(battleController);
            SerializedProperty groundProperty = serializedBattle.FindProperty("backgroundGroundArmoredUnits");
            SerializedProperty stageProperty = serializedBattle.FindProperty("stageVisualRoot");
            if (groundProperty != null)
            {
                groundProperty.objectReferenceValue = runtime;
            }

            if (stageProperty != null)
            {
                stageProperty.objectReferenceValue = FindSceneTransform(scene, "StageVisualRoot");
            }

            serializedBattle.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(battleController);
        }

        return true;
    }

    private static Transform FindDescendant(Transform root, string name)
    {
        Transform[] transforms = root.GetComponentsInChildren<Transform>(true);
        for (int i = 0; i < transforms.Length; i++)
        {
            if (transforms[i].name == name) return transforms[i];
        }

        return null;
    }

    private static Transform EnsureSceneChild(Transform parent, string name)
    {
        Transform child = parent.Find(name);
        if (child != null) return child;
        GameObject childObject = new(name);
        childObject.transform.SetParent(parent, false);
        return childObject.transform;
    }

    private static Transform FindSceneTransform(Scene scene, string name)
    {
        GameObject[] roots = scene.GetRootGameObjects();
        for (int i = 0; i < roots.Length; i++)
        {
            Transform[] descendants = roots[i].GetComponentsInChildren<Transform>(true);
            for (int j = 0; j < descendants.Length; j++)
            {
                if (descendants[j].name == name) return descendants[j];
            }
        }

        return null;
    }

    private static T FindSceneComponent<T>(Scene scene) where T : Component
    {
        GameObject[] roots = scene.GetRootGameObjects();
        for (int i = 0; i < roots.Length; i++)
        {
            T component = roots[i].GetComponentInChildren<T>(true);
            if (component != null) return component;
        }

        return null;
    }

    private static void EnsureFolder(string assetPath)
    {
        if (AssetDatabase.IsValidFolder(assetPath)) return;
        string[] parts = assetPath.Split('/');
        string current = parts[0];
        for (int i = 1; i < parts.Length; i++)
        {
            string next = $"{current}/{parts[i]}";
            if (!AssetDatabase.IsValidFolder(next)) AssetDatabase.CreateFolder(current, parts[i]);
            current = next;
        }
    }
}
#endif
