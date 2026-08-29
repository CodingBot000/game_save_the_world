#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

public static class BackgroundAllyArmyBuilder
{
    private const string BattleArenaScenePath = "Assets/Scenes/BattleArena.unity/BattleArena.unity";
    private const string ArtRoot = "Assets/_Project/Art/Environment/BackgroundAllyArmy";
    private const string AirRoot = ArtRoot + "/Air";
    private const string ModelPath = AirRoot + "/Models/small_chopper_bg_optimized_500.fbx";
    private const string BaseColorPath = AirRoot + "/Textures/small_chopper_bg_basecolor_256.png";
    private const string NormalPath = AirRoot + "/Textures/small_chopper_bg_normal_128.png";
    private const string MaskPath = AirRoot + "/Textures/small_chopper_bg_unity_metallic_smoothness_128.png";
    private const string MaterialFolder = ArtRoot + "/Materials";
    private const string ChopperMaterialPath = MaterialFolder + "/BackgroundChopper_500.mat";
    private const string RotorMaterialPath = MaterialFolder + "/BackgroundRotorBlur.mat";
    private const string TracerMaterialPath = MaterialFolder + "/BackgroundAirTracer.mat";
    private const string SmokeMaterialPath = MaterialFolder + "/BackgroundCrashSmoke.mat";
    private const string VfxFolder = ArtRoot + "/VFX";
    private const string RotorTexturePath = VfxFolder + "/background_rotor_blur_64.png";
    private const string SmokeTexturePath = VfxFolder + "/background_smoke_soft_64.png";
    private const string PrefabFolder = "Assets/Prefabs/Environment/BackgroundAllyArmy";
    private const string ChopperPrefabPath = PrefabFolder + "/BackgroundChopper_500.prefab";
    private const string BattleArenaRootName = "BattleArenaRoot";
    private const string ArmyRootName = "AmbientAllyArmyRoot";

    [MenuItem("Tools/Titan Destroyer/Rebuild Background Ally Air Army")]
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
        Debug.Log("Background ally air army assets and BattleArena scene bindings rebuilt.");
    }

    public static void RebuildBattleArenaForBatch()
    {
        if (!BuildAssets())
        {
            return;
        }

        Scene scene = EditorSceneManager.OpenScene(BattleArenaScenePath, OpenSceneMode.Single);
        if (BuildScene(scene))
        {
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();
        }
    }

    private static bool BuildAssets()
    {
        EnsureFolder(MaterialFolder);
        EnsureFolder(VfxFolder);
        EnsureFolder(PrefabFolder);
        AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);

        if (!ConfigureModelImporter())
        {
            return false;
        }

        ConfigureTextureImporter(BaseColorPath, TextureImporterType.Default, true, 256, alpha: false);
        ConfigureTextureImporter(NormalPath, TextureImporterType.NormalMap, false, 128, alpha: false);
        ConfigureTextureImporter(MaskPath, TextureImporterType.Default, false, 128, alpha: true);
        CreateRotorBlurTexture();
        ConfigureTextureImporter(RotorTexturePath, TextureImporterType.Default, true, 64, alpha: true);
        CreateSmokeTexture();
        ConfigureTextureImporter(SmokeTexturePath, TextureImporterType.Default, true, 64, alpha: true);

        Material chopperMaterial = BuildChopperMaterial();
        Material rotorMaterial = BuildTransparentUnlitMaterial(
            RotorMaterialPath,
            AssetDatabase.LoadAssetAtPath<Texture2D>(RotorTexturePath),
            new Color(0.74f, 0.84f, 0.9f, 0.48f),
            additive: false);
        Material tracerMaterial = BuildTransparentUnlitMaterial(
            TracerMaterialPath,
            null,
            new Color(1f, 0.58f, 0.18f, 0.78f),
            additive: true);
        Material smokeMaterial = BuildTransparentUnlitMaterial(
            SmokeMaterialPath,
            AssetDatabase.LoadAssetAtPath<Texture2D>(SmokeTexturePath),
            new Color(0.12f, 0.13f, 0.14f, 0.78f),
            additive: false);

        if (chopperMaterial == null || rotorMaterial == null || tracerMaterial == null || smokeMaterial == null)
        {
            Debug.LogError("Background ally army material creation failed.");
            return false;
        }

        return BuildChopperPrefab(chopperMaterial, rotorMaterial, tracerMaterial, smokeMaterial);
    }

    private static bool ConfigureModelImporter()
    {
        ModelImporter importer = AssetImporter.GetAtPath(ModelPath) as ModelImporter;
        if (importer == null)
        {
            AssetDatabase.ImportAsset(ModelPath, ImportAssetOptions.ForceSynchronousImport | ImportAssetOptions.ForceUpdate);
            importer = AssetImporter.GetAtPath(ModelPath) as ModelImporter;
        }

        if (importer == null)
        {
            Debug.LogError($"Background chopper model was not found at {ModelPath}.");
            return false;
        }

        importer.importAnimation = false;
        importer.importCameras = false;
        importer.importLights = false;
        importer.importBlendShapes = false;
        importer.materialImportMode = ModelImporterMaterialImportMode.None;
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
            Debug.LogError($"Background ally texture was not found at {path}.");
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

    private static Material BuildChopperMaterial()
    {
        Shader shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
        if (shader == null)
        {
            return null;
        }

        Material material = LoadOrCreateMaterial(ChopperMaterialPath, shader);
        Texture2D baseColor = AssetDatabase.LoadAssetAtPath<Texture2D>(BaseColorPath);
        Texture2D normal = AssetDatabase.LoadAssetAtPath<Texture2D>(NormalPath);
        Texture2D mask = AssetDatabase.LoadAssetAtPath<Texture2D>(MaskPath);
        material.name = "BackgroundChopper_500";
        material.enableInstancing = true;
        material.doubleSidedGI = true;

        SetTexture(material, "_BaseMap", "_MainTex", baseColor);
        if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", Color.white);
        if (material.HasProperty("_Color")) material.SetColor("_Color", Color.white);

        if (normal != null && material.HasProperty("_BumpMap"))
        {
            material.SetTexture("_BumpMap", normal);
            material.SetFloat("_BumpScale", 0.72f);
            material.EnableKeyword("_NORMALMAP");
        }

        if (mask != null && material.HasProperty("_MetallicGlossMap"))
        {
            material.SetTexture("_MetallicGlossMap", mask);
            material.SetFloat("_Metallic", 1f);
            material.SetFloat("_Smoothness", 1f);
            material.EnableKeyword("_METALLICSPECGLOSSMAP");
        }

        if (material.HasProperty("_EmissionColor"))
        {
            material.SetColor("_EmissionColor", new Color(0.11f, 0.13f, 0.15f, 1f));
            material.EnableKeyword("_EMISSION");
        }

        if (material.HasProperty("_Cull")) material.SetFloat("_Cull", (float)CullMode.Off);
        if (material.HasProperty("_Surface")) material.SetFloat("_Surface", 0f);
        if (material.HasProperty("_ZWrite")) material.SetFloat("_ZWrite", 1f);
        material.renderQueue = -1;
        EditorUtility.SetDirty(material);
        return material;
    }

    private static Material BuildTransparentUnlitMaterial(
        string assetPath,
        Texture texture,
        Color color,
        bool additive)
    {
        Shader shader = Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Unlit/Transparent");
        if (shader == null)
        {
            return null;
        }

        Material material = LoadOrCreateMaterial(assetPath, shader);
        material.name = Path.GetFileNameWithoutExtension(assetPath);
        material.enableInstancing = true;
        SetTexture(material, "_BaseMap", "_MainTex", texture);
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

    private static void SetTexture(Material material, string primaryProperty, string fallbackProperty, Texture texture)
    {
        if (material == null)
        {
            return;
        }

        if (material.HasProperty(primaryProperty))
        {
            material.SetTexture(primaryProperty, texture);
        }
        else if (material.HasProperty(fallbackProperty))
        {
            material.SetTexture(fallbackProperty, texture);
        }
    }

    private static bool BuildChopperPrefab(
        Material chopperMaterial,
        Material rotorMaterial,
        Material muzzleFlashMaterial,
        Material smokeMaterial)
    {
        GameObject modelAsset = AssetDatabase.LoadAssetAtPath<GameObject>(ModelPath);
        if (modelAsset == null)
        {
            Debug.LogError($"Background chopper model asset could not be loaded at {ModelPath}.");
            return false;
        }

        GameObject root = new("BackgroundChopper_500");
        GameObject visualRootObject = new("VisualRoot");
        visualRootObject.transform.SetParent(root.transform, false);
        Transform visualRoot = visualRootObject.transform;

        GameObject modelInstance = PrefabUtility.InstantiatePrefab(modelAsset, visualRoot) as GameObject;
        if (modelInstance == null)
        {
            modelInstance = Object.Instantiate(modelAsset, visualRoot);
        }

        modelInstance.name = "Model";
        modelInstance.transform.localPosition = Vector3.zero;
        modelInstance.transform.localRotation = Quaternion.Euler(0f, -90f, 0f);
        modelInstance.transform.localScale = Vector3.one;
        AssignSharedMaterialAndRendererSettings(modelInstance, chopperMaterial);

        Transform muzzle = CreateAnchor("Muzzle", visualRoot, new Vector3(0f, -0.02f, 0.96f));
        Transform mainRotor = CreateRotorQuad(
            "MainRotorBlur",
            visualRoot,
            new Vector3(0f, 0.42f, -0.03f),
            Quaternion.Euler(90f, 0f, 0f),
            new Vector3(1.58f, 1.58f, 1f),
            rotorMaterial);
        Transform tailRotor = CreateRotorQuad(
            "TailRotorBlur",
            visualRoot,
            new Vector3(0f, 0.2f, -0.86f),
            Quaternion.identity,
            new Vector3(0.31f, 0.31f, 1f),
            rotorMaterial);
        Renderer[] muzzleFlashRenderers = CreateMuzzleFlash(muzzle, muzzleFlashMaterial);
        ParticleSystem crashSmoke = CreateCrashSmoke(visualRoot, smokeMaterial);

        BackgroundAllyUnitView view = root.AddComponent<BackgroundAllyUnitView>();
        view.ConfigureForEditor(
            visualRoot,
            muzzle,
            mainRotor,
            tailRotor,
            muzzleFlashRenderers,
            crashSmoke);

        PrefabUtility.SaveAsPrefabAsset(root, ChopperPrefabPath);
        Object.DestroyImmediate(root);
        return AssetDatabase.LoadAssetAtPath<GameObject>(ChopperPrefabPath) != null;
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
                for (int materialIndex = 0; materialIndex < materials.Length; materialIndex++)
                {
                    materials[materialIndex] = material;
                }

                renderer.sharedMaterials = materials;
            }

            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            renderer.lightProbeUsage = LightProbeUsage.Off;
            renderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
            renderer.motionVectorGenerationMode = MotionVectorGenerationMode.Camera;
        }
    }

    private static Transform CreateAnchor(string name, Transform parent, Vector3 localPosition)
    {
        GameObject anchor = new(name);
        anchor.transform.SetParent(parent, false);
        anchor.transform.localPosition = localPosition;
        return anchor.transform;
    }

    private static Transform CreateRotorQuad(
        string name,
        Transform parent,
        Vector3 localPosition,
        Quaternion localRotation,
        Vector3 localScale,
        Material material)
    {
        GameObject quad = GameObject.CreatePrimitive(PrimitiveType.Quad);
        quad.name = name;
        quad.transform.SetParent(parent, false);
        quad.transform.localPosition = localPosition;
        quad.transform.localRotation = localRotation;
        quad.transform.localScale = localScale;
        Collider collider = quad.GetComponent<Collider>();
        if (collider != null)
        {
            Object.DestroyImmediate(collider);
        }

        MeshRenderer renderer = quad.GetComponent<MeshRenderer>();
        renderer.sharedMaterial = material;
        renderer.shadowCastingMode = ShadowCastingMode.Off;
        renderer.receiveShadows = false;
        renderer.lightProbeUsage = LightProbeUsage.Off;
        renderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
        return quad.transform;
    }

    private static Renderer[] CreateMuzzleFlash(Transform muzzle, Material material)
    {
        GameObject root = new("MuzzleFlash");
        root.transform.SetParent(muzzle, false);
        root.transform.localPosition = new Vector3(0f, 0f, 0.1f);

        MeshRenderer horizontal = CreateEffectQuad(
            "FlashHorizontal",
            root.transform,
            Quaternion.Euler(90f, 0f, 0f),
            new Vector3(0.11f, 0.32f, 1f),
            material);
        MeshRenderer vertical = CreateEffectQuad(
            "FlashVertical",
            root.transform,
            Quaternion.Euler(0f, 90f, 0f),
            new Vector3(0.32f, 0.11f, 1f),
            material);
        horizontal.enabled = false;
        vertical.enabled = false;
        return new Renderer[] { horizontal, vertical };
    }

    private static MeshRenderer CreateEffectQuad(
        string name,
        Transform parent,
        Quaternion localRotation,
        Vector3 localScale,
        Material material)
    {
        GameObject quad = GameObject.CreatePrimitive(PrimitiveType.Quad);
        quad.name = name;
        quad.transform.SetParent(parent, false);
        quad.transform.localPosition = Vector3.zero;
        quad.transform.localRotation = localRotation;
        quad.transform.localScale = localScale;
        Collider collider = quad.GetComponent<Collider>();
        if (collider != null)
        {
            Object.DestroyImmediate(collider);
        }

        MeshRenderer renderer = quad.GetComponent<MeshRenderer>();
        renderer.sharedMaterial = material;
        renderer.shadowCastingMode = ShadowCastingMode.Off;
        renderer.receiveShadows = false;
        renderer.lightProbeUsage = LightProbeUsage.Off;
        renderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
        return renderer;
    }

    private static ParticleSystem CreateCrashSmoke(Transform parent, Material material)
    {
        GameObject smokeObject = new("CrashSmoke");
        smokeObject.transform.SetParent(parent, false);
        smokeObject.transform.localPosition = new Vector3(0f, 0.08f, -0.12f);
        ParticleSystem smoke = smokeObject.AddComponent<ParticleSystem>();
        ParticleSystem.MainModule main = smoke.main;
        main.loop = true;
        main.duration = 4f;
        main.startLifetime = new ParticleSystem.MinMaxCurve(1.2f, 2f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(0.18f, 0.48f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.22f, 0.5f);
        main.startColor = new ParticleSystem.MinMaxGradient(
            new Color(0.12f, 0.13f, 0.14f, 0.62f),
            new Color(0.3f, 0.31f, 0.32f, 0.82f));
        main.maxParticles = 60;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.playOnAwake = false;

        ParticleSystem.EmissionModule emission = smoke.emission;
        emission.rateOverTime = 20f;
        ParticleSystem.ShapeModule shape = smoke.shape;
        shape.shapeType = ParticleSystemShapeType.Sphere;
        shape.radius = 0.055f;

        ParticleSystem.ColorOverLifetimeModule colorOverLifetime = smoke.colorOverLifetime;
        colorOverLifetime.enabled = true;
        Gradient gradient = new();
        gradient.SetKeys(
            new[]
            {
                new GradientColorKey(new Color(0.2f, 0.21f, 0.22f), 0f),
                new GradientColorKey(new Color(0.38f, 0.39f, 0.4f), 1f),
            },
            new[]
            {
                new GradientAlphaKey(0f, 0f),
                new GradientAlphaKey(0.78f, 0.12f),
                new GradientAlphaKey(0f, 1f),
            });
        colorOverLifetime.color = gradient;

        ParticleSystemRenderer renderer = smoke.GetComponent<ParticleSystemRenderer>();
        renderer.renderMode = ParticleSystemRenderMode.Billboard;
        renderer.sharedMaterial = material;
        renderer.shadowCastingMode = ShadowCastingMode.Off;
        renderer.receiveShadows = false;
        smoke.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        return smoke;
    }

    private static bool BuildScene(Scene scene)
    {
        if (!scene.IsValid())
        {
            Debug.LogError("Background ally army scene build failed because no valid scene is loaded.");
            return false;
        }

        Transform battleRoot = FindSceneTransform(scene, BattleArenaRootName);
        if (battleRoot == null)
        {
            Debug.LogError($"Background ally army scene build failed because {BattleArenaRootName} was not found.");
            return false;
        }

        Transform armyRoot = battleRoot.Find(ArmyRootName);
        if (armyRoot == null)
        {
            GameObject armyObject = new(ArmyRootName);
            armyRoot = armyObject.transform;
            armyRoot.SetParent(battleRoot, false);
        }

        Transform airRoot = EnsureSceneChild(armyRoot, "AirRoot");
        Transform vfxRoot = EnsureSceneChild(armyRoot, "CosmeticVfxRoot");
        BackgroundAllyArmyController controller = armyRoot.GetComponent<BackgroundAllyArmyController>();
        if (controller == null)
        {
            controller = armyRoot.gameObject.AddComponent<BackgroundAllyArmyController>();
        }

        GameObject chopperPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(ChopperPrefabPath);
        Material tracerMaterial = AssetDatabase.LoadAssetAtPath<Material>(TracerMaterialPath);
        controller.ConfigureAssetsForEditor(chopperPrefab, tracerMaterial, airRoot, vfxRoot);
        controller.ApplyAuthoredDefaultsForEditorIfNeeded();
        EditorUtility.SetDirty(controller);

        BattleController battleController = FindSceneComponent<BattleController>(scene);
        if (battleController != null)
        {
            SerializedObject serializedBattle = new(battleController);
            SerializedProperty allyProperty = serializedBattle.FindProperty("backgroundAllyArmy");
            if (allyProperty != null)
            {
                allyProperty.objectReferenceValue = controller;
                serializedBattle.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(battleController);
            }
        }

        return true;
    }

    private static Transform EnsureSceneChild(Transform parent, string name)
    {
        Transform child = parent.Find(name);
        if (child != null)
        {
            return child;
        }

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
                if (descendants[j].name == name)
                {
                    return descendants[j];
                }
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
            if (component != null)
            {
                return component;
            }
        }

        return null;
    }

    private static void CreateRotorBlurTexture()
    {
        const int size = 64;
        Texture2D texture = new(size, size, TextureFormat.RGBA32, false, false);
        Color[] pixels = new Color[size * size];
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float nx = ((x + 0.5f) / size - 0.5f) * 2f;
                float ny = ((y + 0.5f) / size - 0.5f) * 2f;
                float radius = Mathf.Sqrt(nx * nx + ny * ny);
                float angle = Mathf.Atan2(ny, nx);
                float outer = 1f - Mathf.SmoothStep(0.82f, 1f, radius);
                float inner = Mathf.SmoothStep(0.04f, 0.14f, radius);
                float blade = Mathf.Pow(Mathf.Abs(Mathf.Cos(angle * 4f)), 12f);
                float alpha = outer * inner * Mathf.Lerp(0.06f, 0.36f, blade);
                pixels[y * size + x] = new Color(0.78f, 0.86f, 0.92f, alpha);
            }
        }

        texture.SetPixels(pixels);
        texture.Apply(false, false);
        File.WriteAllBytes(RotorTexturePath, texture.EncodeToPNG());
        Object.DestroyImmediate(texture);
        AssetDatabase.ImportAsset(RotorTexturePath, ImportAssetOptions.ForceSynchronousImport | ImportAssetOptions.ForceUpdate);
    }

    private static void CreateSmokeTexture()
    {
        const int size = 64;
        Texture2D texture = new(size, size, TextureFormat.RGBA32, false, false);
        Color[] pixels = new Color[size * size];
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float nx = ((x + 0.5f) / size - 0.5f) * 2f;
                float ny = ((y + 0.5f) / size - 0.5f) * 2f;
                float radius = Mathf.Sqrt(nx * nx + ny * ny);
                float soft = 1f - Mathf.SmoothStep(0.15f, 1f, radius);
                float noise = Mathf.PerlinNoise(x * 0.12f + 3.1f, y * 0.12f + 7.4f);
                float alpha = soft * Mathf.Lerp(0.55f, 1f, noise);
                pixels[y * size + x] = new Color(0.72f, 0.74f, 0.76f, alpha);
            }
        }

        texture.SetPixels(pixels);
        texture.Apply(false, false);
        File.WriteAllBytes(SmokeTexturePath, texture.EncodeToPNG());
        Object.DestroyImmediate(texture);
        AssetDatabase.ImportAsset(SmokeTexturePath, ImportAssetOptions.ForceSynchronousImport | ImportAssetOptions.ForceUpdate);
    }

    private static void EnsureFolder(string assetPath)
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
#endif
