using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

internal static class BuildSaratanStageIntegration
{
    private const string ScenePath = "Assets/Scenes/BattleArena.unity/BattleArena.unity";
    private const string ImportedRoot = "Assets/SaratanExport/Stage02_Saratan";
    private const string BossContentRoot = "Assets/_Project/Content/Bosses";
    private const string Stage1Root = BossContentRoot + "/Stage01_Kaiju";
    private const string Stage2Root = BossContentRoot + "/Stage02_Saratan";
    private const string StagesRoot = "Assets/_Project/Content/Stages";
    private const string Stage1PrefabPath = Stage1Root + "/Runtime/Prefabs/Boss_Stage01_Kaiju.prefab";
    private const string Stage2PrefabPath = Stage2Root + "/Runtime/Prefabs/Boss_Stage02_Saratan.prefab";
    private const string Stage1EnvironmentPath = StagesRoot + "/Stage01/Environment/Stage01Environment.prefab";
    private const string Stage2EnvironmentPath = StagesRoot + "/Stage02/Environment/Stage02Environment.prefab";
    private const string AnimatorControllerPath = Stage2Root + "/Runtime/Animation/Controllers/Saratan.controller";
    private const string SaratanAttackDataFolder = Stage2Root + "/Runtime/Attack/Data";
    private const string SharedDebrisCatalogPath = "Assets/_Project/Resources/VFX/MonsterDebrisFragmentCatalog.asset";
    private const string SaratanDebrisCatalogPath = SaratanAttackDataFolder + "/SaratanDebrisFragmentCatalog.asset";
    private const string RigBModelPath = Stage2Root + "/Art/RigB/Models/Saratan_RigB_Model.fbx";
    private const string RigBIdlePath = Stage2Root + "/Art/RigB/Animations/Saratan_RigB_BasicIdle.anim";
    private const string RigBFiringFrontPath = Stage2Root + "/Art/RigB/Animations/Saratan_RigB_Attack_FiringFront.anim";
    private const string RigBBreathFrontPath = Stage2Root + "/Art/RigB/Animations/Saratan_RigB_Attack_BreathFront.anim";
    private const string SaratanMouthBonePath = "Root/Pelvis/Spine 01/Neck_01/Head/Jaw Under";

    public static void Run()
    {
        MoveImportedContent();
        FixSaratanMaterials();
        EnsureFolder(Stage1Root + "/Runtime/Prefabs");
        EnsureFolder(Stage1Root + "/Data");
        EnsureFolder(Stage2Root + "/Runtime/Animation/Controllers");
        EnsureFolder(Stage2Root + "/Runtime/Animation/Masks");
        EnsureFolder(SaratanAttackDataFolder);
        EnsureFolder(Stage2Root + "/Runtime/Prefabs");
        EnsureFolder(Stage2Root + "/Data");
        EnsureFolder(StagesRoot + "/Stage01/Environment");
        EnsureFolder(StagesRoot + "/Stage02/Environment");

        AnimatorController animatorController = CreateSaratanAnimatorController();
        DebrisFragmentCatalog saratanDebrisCatalog = CreateSaratanDebrisCatalog();
        GameObject stage1Environment = CreateEnvironmentPrefab(Stage1EnvironmentPath, "Stage01Environment");
        GameObject stage2Environment = CreateEnvironmentPrefab(Stage2EnvironmentPath, "Stage02Environment");

        Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        GameObject existingBoss = FindSceneObject(scene, "BossPlaceholder");
        if (existingBoss == null)
        {
            throw new InvalidOperationException("BattleArena does not contain the expected BossPlaceholder instance.");
        }

        Transform bossParent = existingBoss.transform.parent;
        Vector3 bossLocalPosition = existingBoss.transform.localPosition;
        Quaternion bossLocalRotation = existingBoss.transform.localRotation;
        Vector3 bossLocalScale = existingBoss.transform.localScale;

        GameObject stage1Prefab = CreateStage1Prefab(existingBoss);
        GameObject stage2Prefab = CreateStage2Prefab(
            stage1Prefab,
            animatorController,
            saratanDebrisCatalog,
            bossLocalPosition,
            bossLocalRotation,
            bossLocalScale);

        BossDefinition stage1BossDefinition = CreateBossDefinition(
            Stage1Root + "/Data/KaijuBossDefinition.asset",
            "boss_stage01_kaiju",
            "Kaiju",
            stage1Prefab,
            2000f);
        BossDefinition stage2BossDefinition = CreateBossDefinition(
            Stage2Root + "/Data/SaratanBossDefinition.asset",
            "boss_stage02_saratan",
            "Saratan",
            stage2Prefab,
            2000f);

        StageDefinition stage1Definition = CreateStageDefinition(
            StagesRoot + "/Stage01/Stage01Definition.asset",
            "stage_01_tokyo",
            "Tokyo",
            stage1BossDefinition,
            stage1Environment,
            EnvironmentThemeType.Day);
        StageDefinition stage2Definition = CreateStageDefinition(
            StagesRoot + "/Stage02/Stage02Definition.asset",
            "stage_02_seoul",
            "Seoul",
            stage2BossDefinition,
            stage2Environment,
            EnvironmentThemeType.Day);
        StageCatalog catalog = CreateStageCatalog(
            StagesRoot + "/StageCatalog.asset",
            stage1Definition,
            stage2Definition);

        GameObject bossSpawnPoint = ReplaceSceneBossWithSpawnPoint(existingBoss, bossParent);
        GameObject environmentSpawnPoint = EnsureSceneObject(scene, "EnvironmentSpawnPoint", bossParent);
        environmentSpawnPoint.transform.SetLocalPositionAndRotation(Vector3.zero, Quaternion.identity);
        environmentSpawnPoint.transform.localScale = Vector3.one;

        GameObject systems = FindSceneObject(scene, "Systems");
        if (systems == null)
        {
            throw new InvalidOperationException("BattleArena does not contain the Systems object.");
        }

        BattleStageLoader loader = systems.GetComponent<BattleStageLoader>();
        if (loader == null)
        {
            loader = systems.AddComponent<BattleStageLoader>();
        }
        SetObjectReference(loader, "stageCatalog", catalog);
        SetObjectReference(loader, "bossSpawnPoint", bossSpawnPoint.transform);
        SetObjectReference(loader, "environmentSpawnPoint", environmentSpawnPoint.transform);

        BattleController battleController = systems.GetComponent<BattleController>();
        if (battleController == null)
        {
            throw new InvalidOperationException("Systems does not contain BattleController.");
        }

        SetObjectReference(battleController, "bossController", null);
        SetObjectReference(battleController, "bossAttackController", null);
        SetObjectReference(battleController, "stageLoader", loader);

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);

        UpdateManifestFinalPaths();
        ValidateIntegration(stage1Prefab, stage2Prefab, catalog);
        AssetDatabase.SaveAssets();
        Debug.Log("SARATAN_INTEGRATION_BUILD_SUCCESS");
    }

    public static void FixSaratanMaterials()
    {
        Shader shader = Shader.Find("Universal Render Pipeline/Lit");
        if (shader == null || !shader.isSupported)
        {
            throw new InvalidOperationException("The supported URP Lit shader could not be resolved.");
        }

        ConvertMaterial(
            Stage2Root + "/Art/RigA/Materials/Saratan_RigA_Body.mat",
            Stage2Root + "/Art/RigA/Textures/Saratan_RigA_Body.png",
            null,
            shader);
        ConvertMaterial(
            Stage2Root + "/Art/RigA/Materials/Saratan_RigA_Eye.mat",
            Stage2Root + "/Art/RigA/Textures/Saratan_RigA_Eye.png",
            null,
            shader);
        ConvertMaterial(
            Stage2Root + "/Art/RigA/Materials/Saratan_RigA_HeadSail.mat",
            Stage2Root + "/Art/RigA/Textures/Saratan_RigA_HeadSail.png",
            null,
            shader);
        ConvertMaterial(
            Stage2Root + "/Art/RigB/Materials/Saratan_RigB_Body.mat",
            Stage2Root + "/Art/RigB/Textures/Saratan_RigB_Body.png",
            Stage2Root + "/Art/RigB/Textures/Saratan_RigB_Body_Emission.png",
            shader);
        ConvertMaterial(
            Stage2Root + "/Art/RigB/Materials/Saratan_RigB_Eye.mat",
            Stage2Root + "/Art/RigB/Textures/Saratan_RigB_Eye.png",
            Stage2Root + "/Art/RigB/Textures/Saratan_RigB_Eye_Emission.png",
            shader);

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
        Debug.Log("SARATAN_MATERIAL_FIX_SUCCESS shader=Universal Render Pipeline/Lit");
    }

    [MenuItem("Tools/Titan Destroyer/Stage Content/Match Saratan Scale To Stage 1")]
    public static void MatchSaratanScaleToStage1()
    {
        float targetHeight = CalculatePrefabVisualHeight(Stage1PrefabPath);
        GameObject stage2Root = PrefabUtility.LoadPrefabContents(Stage2PrefabPath);

        try
        {
            Transform rootTransform = stage2Root.transform;
            Vector3 rootPosition = rootTransform.localPosition;
            Quaternion rootRotation = rootTransform.localRotation;
            Vector3 rootScale = rootTransform.localScale;
            rootTransform.SetLocalPositionAndRotation(Vector3.zero, Quaternion.identity);
            rootTransform.localScale = Vector3.one;

            Transform visualRoot = rootTransform.Find("BossVisualRoot");
            Transform model = visualRoot != null ? visualRoot.Find("SaratanVisual") : null;
            if (visualRoot == null || model == null)
            {
                throw new InvalidOperationException("Stage 2 prefab is missing BossVisualRoot/SaratanVisual.");
            }

            Bounds currentBounds = CalculateRendererBounds(visualRoot.gameObject);
            if (currentBounds.size.y <= 0.001f)
            {
                throw new InvalidOperationException("Stage 2 Saratan visual has no measurable renderer height.");
            }

            float scaleRatio = targetHeight / currentBounds.size.y;
            model.localScale *= scaleRatio;

            Bounds scaledBounds = CalculateRendererBounds(visualRoot.gameObject);
            Vector3 worldOffset = new(
                -scaledBounds.center.x,
                -scaledBounds.min.y,
                -scaledBounds.center.z);
            model.localPosition += visualRoot.InverseTransformVector(worldOffset);

            Bounds finalBounds = CalculateRendererBounds(visualRoot.gameObject);
            UpdateStage2TargetGeometry(stage2Root, finalBounds);

            rootTransform.SetLocalPositionAndRotation(rootPosition, rootRotation);
            rootTransform.localScale = rootScale;
            PrefabUtility.SaveAsPrefabAsset(stage2Root, Stage2PrefabPath);
            AssetDatabase.SaveAssets();

            Debug.Log(
                $"SARATAN_SCALE_MATCH_SUCCESS oldHeight={currentBounds.size.y:F3} " +
                $"targetHeight={targetHeight:F3} finalHeight={finalBounds.size.y:F3} ratio={scaleRatio:F4}");
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(stage2Root);
        }
    }

    [MenuItem("Tools/Titan Destroyer/Stage Content/Apply Temporary Saratan Attack Pattern")]
    public static void ApplyTemporarySaratanAttackPattern()
    {
        EnsureFolder(Stage2Root + "/Runtime/Animation/Controllers");
        EnsureFolder(SaratanAttackDataFolder);

        AnimatorController animatorController = CreateSaratanAnimatorController();
        DebrisFragmentCatalog saratanDebrisCatalog = CreateSaratanDebrisCatalog();
        GameObject stage1Root = PrefabUtility.LoadPrefabContents(Stage1PrefabPath);
        GameObject stage2Root = PrefabUtility.LoadPrefabContents(Stage2PrefabPath);

        try
        {
            ConfigureTemporaryStage2Attack(
                stage1Root,
                stage2Root,
                animatorController,
                saratanDebrisCatalog);
            PrefabUtility.SaveAsPrefabAsset(stage2Root, Stage2PrefabPath);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);

            ValidateStage2AttackIsolation(
                AssetDatabase.LoadAssetAtPath<GameObject>(Stage2PrefabPath));
            Debug.Log("SARATAN_TEMPORARY_ATTACK_PATTERN_SUCCESS");
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(stage2Root);
            PrefabUtility.UnloadPrefabContents(stage1Root);
        }
    }

    public static void CaptureStage2Preview()
    {
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(Stage2PrefabPath);
        if (prefab == null)
        {
            throw new InvalidOperationException($"Stage 2 prefab is missing: {Stage2PrefabPath}");
        }

        GameObject boss = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
        boss.transform.position = Vector3.zero;
        Bounds bounds = CalculateRendererBounds(boss);

        GameObject lightObject = new("PreviewLight");
        Light previewLight = lightObject.AddComponent<Light>();
        previewLight.type = LightType.Directional;
        previewLight.intensity = 1.4f;
        lightObject.transform.rotation = Quaternion.Euler(38f, -32f, 0f);

        GameObject cameraObject = new("PreviewCamera");
        Camera camera = cameraObject.AddComponent<Camera>();
        camera.clearFlags = CameraClearFlags.SolidColor;
        camera.backgroundColor = new Color(0.07f, 0.09f, 0.12f, 1f);
        camera.fieldOfView = 35f;
        Vector3 viewDirection = new Vector3(0.7f, 0.25f, -1f).normalized;
        float distance = Mathf.Max(bounds.size.magnitude * 1.15f, 8f);
        camera.transform.position = bounds.center - viewDirection * distance;
        camera.transform.LookAt(bounds.center + Vector3.up * bounds.extents.y * 0.08f);

        RenderTexture target = new(1024, 768, 24, RenderTextureFormat.ARGB32);
        camera.targetTexture = target;
        camera.Render();
        RenderTexture previous = RenderTexture.active;
        RenderTexture.active = target;
        Texture2D image = new(1024, 768, TextureFormat.RGBA32, false);
        image.ReadPixels(new Rect(0f, 0f, 1024f, 768f), 0, 0);
        image.Apply();

        string outputPath = "/Users/switch/Development/Game/Unity/TitanSlayer/tmp/SaratanStage2PrefabPreview.png";
        File.WriteAllBytes(outputPath, image.EncodeToPNG());
        RenderTexture.active = previous;
        camera.targetTexture = null;
        UnityEngine.Object.DestroyImmediate(image);
        UnityEngine.Object.DestroyImmediate(target);
        UnityEngine.Object.DestroyImmediate(cameraObject);
        UnityEngine.Object.DestroyImmediate(lightObject);
        UnityEngine.Object.DestroyImmediate(boss);
        Debug.Log($"SARATAN_PREVIEW_CAPTURE_SUCCESS path={outputPath} bounds={bounds}");
    }

    private static void MoveImportedContent()
    {
        if (AssetDatabase.IsValidFolder(Stage2Root))
        {
            return;
        }

        if (!AssetDatabase.IsValidFolder(ImportedRoot))
        {
            throw new InvalidOperationException($"Isolated import root is missing: {ImportedRoot}");
        }

        EnsureFolder(BossContentRoot);
        string error = AssetDatabase.MoveAsset(ImportedRoot, Stage2Root);
        if (!string.IsNullOrEmpty(error))
        {
            throw new InvalidOperationException($"Could not move isolated Saratan content: {error}");
        }

        if (AssetDatabase.IsValidFolder("Assets/SaratanExport"))
        {
            AssetDatabase.DeleteAsset("Assets/SaratanExport");
        }

        AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
    }

    private static void ConvertMaterial(
        string materialPath,
        string baseTexturePath,
        string emissionTexturePath,
        Shader shader)
    {
        Material material = AssetDatabase.LoadAssetAtPath<Material>(materialPath);
        Texture baseTexture = AssetDatabase.LoadAssetAtPath<Texture>(baseTexturePath);
        Texture emissionTexture = string.IsNullOrWhiteSpace(emissionTexturePath)
            ? null
            : AssetDatabase.LoadAssetAtPath<Texture>(emissionTexturePath);
        if (material == null || baseTexture == null)
        {
            throw new InvalidOperationException(
                $"Could not migrate Saratan material '{materialPath}' with texture '{baseTexturePath}'.");
        }

        foreach (string propertyName in material.GetTexturePropertyNames())
        {
            material.SetTexture(propertyName, null);
        }

        material.shader = shader;
        material.SetTexture("_BaseMap", baseTexture);
        material.SetColor("_BaseColor", Color.white);
        material.SetFloat("_Smoothness", 0.25f);
        material.SetFloat("_Metallic", 0f);

        if (emissionTexture != null)
        {
            material.EnableKeyword("_EMISSION");
            material.SetTexture("_EmissionMap", emissionTexture);
            material.SetColor("_EmissionColor", Color.white * 1.5f);
            material.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
        }
        else
        {
            material.DisableKeyword("_EMISSION");
            material.SetTexture("_EmissionMap", null);
            material.SetColor("_EmissionColor", Color.black);
        }

        EditorUtility.SetDirty(material);
    }

    private static AnimatorController CreateSaratanAnimatorController()
    {
        AnimatorController controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(AnimatorControllerPath);
        if (controller == null)
        {
            controller = AnimatorController.CreateAnimatorControllerAtPath(AnimatorControllerPath);
        }

        AnimationClip idle = AssetDatabase.LoadAssetAtPath<AnimationClip>(RigBIdlePath);
        AnimationClip firing = AssetDatabase.LoadAssetAtPath<AnimationClip>(RigBFiringFrontPath);
        AnimationClip breath = AssetDatabase.LoadAssetAtPath<AnimationClip>(RigBBreathFrontPath);
        if (idle == null || firing == null || breath == null)
        {
            throw new InvalidOperationException("One or more Saratan Rig B combat clips are missing.");
        }

        SetClipLoop(idle, true);
        SetClipLoop(firing, false);
        SetClipLoop(breath, false);
        EnsureTriggerParameter(controller, "Attack1");
        EnsureTriggerParameter(controller, "Attack2");

        AnimatorStateMachine stateMachine = controller.layers[0].stateMachine;
        ClearStateMachine(stateMachine);
        AnimatorState idleState = CreateAnimatorState(stateMachine, "BasicIdle", idle, new Vector3(220f, 0f, 0f));
        AnimatorState firingState = CreateAnimatorState(stateMachine, "Attack1_FiringFront", firing, new Vector3(220f, 90f, 0f));
        AnimatorState breathState = CreateAnimatorState(stateMachine, "Attack2_BreathFront", breath, new Vector3(220f, 180f, 0f));
        stateMachine.defaultState = idleState;
        AddAttackTransition(stateMachine, firingState, "Attack1");
        AddAttackTransition(stateMachine, breathState, "Attack2");
        AddReturnToIdleTransition(firingState, idleState);
        AddReturnToIdleTransition(breathState, idleState);

        EditorUtility.SetDirty(controller);
        return controller;
    }

    private static DebrisFragmentCatalog CreateSaratanDebrisCatalog()
    {
        DebrisFragmentCatalog source = AssetDatabase.LoadAssetAtPath<DebrisFragmentCatalog>(SharedDebrisCatalogPath);
        if (source == null)
        {
            throw new InvalidOperationException($"Shared debris catalog is missing: {SharedDebrisCatalogPath}");
        }

        DebrisFragmentCatalog target = AssetDatabase.LoadAssetAtPath<DebrisFragmentCatalog>(SaratanDebrisCatalogPath);
        if (target == null)
        {
            target = ScriptableObject.CreateInstance<DebrisFragmentCatalog>();
            target.name = "SaratanDebrisFragmentCatalog";
            AssetDatabase.CreateAsset(target, SaratanDebrisCatalogPath);
        }

        EditorUtility.CopySerialized(source, target);
        target.name = "SaratanDebrisFragmentCatalog";
        EditorUtility.SetDirty(target);
        return target;
    }

    private static GameObject CreateEnvironmentPrefab(string path, string name)
    {
        GameObject root = new(name);
        GameObject sharedEnvironmentAnchor = new("SharedCurrentBattleEnvironment");
        sharedEnvironmentAnchor.transform.SetParent(root.transform, false);
        GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, path);
        UnityEngine.Object.DestroyImmediate(root);
        return prefab;
    }

    private static GameObject CreateStage1Prefab(GameObject existingBoss)
    {
        GameObject copy = UnityEngine.Object.Instantiate(existingBoss);
        copy.name = "Boss_Stage01_Kaiju";
        copy.transform.SetParent(null, true);
        if (PrefabUtility.IsPartOfPrefabInstance(copy))
        {
            PrefabUtility.UnpackPrefabInstance(copy, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
        }

        GameObject prefab = PrefabUtility.SaveAsPrefabAsset(copy, Stage1PrefabPath);
        UnityEngine.Object.DestroyImmediate(copy);
        if (prefab == null)
        {
            throw new InvalidOperationException("Could not create the Stage 1 Kaiju prefab.");
        }

        return prefab;
    }

    private static GameObject CreateStage2Prefab(
        GameObject stage1Prefab,
        RuntimeAnimatorController animatorController,
        DebrisFragmentCatalog saratanDebrisCatalog,
        Vector3 rootPosition,
        Quaternion rootRotation,
        Vector3 rootScale)
    {
        GameObject modelAsset = AssetDatabase.LoadAssetAtPath<GameObject>(RigBModelPath);
        if (modelAsset == null)
        {
            throw new InvalidOperationException($"Rig B model is missing: {RigBModelPath}");
        }

        GameObject root = new("Boss_Stage02_Saratan");
        root.AddComponent<BossController>();
        root.AddComponent<BossAttackController>();

        GameObject visualRoot = new("BossVisualRoot");
        visualRoot.transform.SetParent(root.transform, false);

        GameObject model = (GameObject)PrefabUtility.InstantiatePrefab(modelAsset);
        model.name = "SaratanVisual";
        model.transform.SetParent(visualRoot.transform, false);
        model.transform.SetLocalPositionAndRotation(Vector3.zero, Quaternion.identity);
        model.transform.localScale = Vector3.one;

        Bounds initialBounds = CalculateRendererBounds(model);
        float targetHeight = CalculatePrefabVisualHeight(Stage1PrefabPath);
        float normalizationScale = initialBounds.size.y > 0.001f
            ? targetHeight / initialBounds.size.y
            : 1f;
        model.transform.localScale = Vector3.one * normalizationScale;
        Bounds normalizedBounds = CalculateRendererBounds(model);
        model.transform.localPosition += new Vector3(
            -normalizedBounds.center.x,
            -normalizedBounds.min.y,
            -normalizedBounds.center.z);

        Animator animator = model.GetComponent<Animator>();
        if (animator == null)
        {
            animator = model.AddComponent<Animator>();
        }
        animator.runtimeAnimatorController = animatorController;
        animator.applyRootMotion = false;

        Bounds finalBounds = CalculateRendererBounds(model);
        new GameObject("AimPoint").transform.SetParent(root.transform, false);
        GameObject hurtbox = new("BossHurtbox");
        hurtbox.transform.SetParent(root.transform, false);
        hurtbox.AddComponent<BoxCollider>();
        UpdateStage2TargetGeometry(root, finalBounds);
        ConfigureTemporaryStage2Attack(
            stage1Prefab,
            root,
            animatorController,
            saratanDebrisCatalog);

        root.transform.localPosition = rootPosition;
        root.transform.localRotation = rootRotation;
        root.transform.localScale = rootScale;

        GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, Stage2PrefabPath);
        UnityEngine.Object.DestroyImmediate(root);
        if (prefab == null)
        {
            throw new InvalidOperationException("Could not create the Stage 2 Saratan prefab.");
        }

        Debug.Log(
            $"SARATAN_BOUNDS source={initialBounds.size} normalized={finalBounds.size} scale={normalizationScale:F4}");
        return prefab;
    }

    private static BossDefinition CreateBossDefinition(
        string path,
        string bossId,
        string displayName,
        GameObject prefab,
        float maxHealth)
    {
        AssetDatabase.DeleteAsset(path);
        BossDefinition definition = ScriptableObject.CreateInstance<BossDefinition>();
        AssetDatabase.CreateAsset(definition, path);
        SerializedObject serialized = new(definition);
        serialized.FindProperty("bossId").stringValue = bossId;
        serialized.FindProperty("displayName").stringValue = displayName;
        serialized.FindProperty("bossPrefab").objectReferenceValue = prefab;
        serialized.FindProperty("maxHealth").floatValue = maxHealth;
        serialized.ApplyModifiedPropertiesWithoutUndo();
        return definition;
    }

    private static StageDefinition CreateStageDefinition(
        string path,
        string stageId,
        string displayName,
        BossDefinition boss,
        GameObject environment,
        EnvironmentThemeType theme)
    {
        AssetDatabase.DeleteAsset(path);
        StageDefinition definition = ScriptableObject.CreateInstance<StageDefinition>();
        AssetDatabase.CreateAsset(definition, path);
        SerializedObject serialized = new(definition);
        serialized.FindProperty("stageId").stringValue = stageId;
        serialized.FindProperty("displayName").stringValue = displayName;
        serialized.FindProperty("bossDefinition").objectReferenceValue = boss;
        serialized.FindProperty("environmentPrefab").objectReferenceValue = environment;
        serialized.FindProperty("environmentTheme").enumValueIndex = (int)theme;
        serialized.ApplyModifiedPropertiesWithoutUndo();
        return definition;
    }

    private static StageCatalog CreateStageCatalog(
        string path,
        StageDefinition stage1,
        StageDefinition stage2)
    {
        AssetDatabase.DeleteAsset(path);
        StageCatalog catalog = ScriptableObject.CreateInstance<StageCatalog>();
        AssetDatabase.CreateAsset(catalog, path);
        SerializedObject serialized = new(catalog);
        serialized.FindProperty("defaultStage").objectReferenceValue = stage1;
        SerializedProperty stages = serialized.FindProperty("stages");
        stages.arraySize = 2;
        stages.GetArrayElementAtIndex(0).objectReferenceValue = stage1;
        stages.GetArrayElementAtIndex(1).objectReferenceValue = stage2;
        serialized.ApplyModifiedPropertiesWithoutUndo();
        return catalog;
    }

    private static GameObject ReplaceSceneBossWithSpawnPoint(GameObject existingBoss, Transform parent)
    {
        GameObject spawnPoint = EnsureSceneObject(existingBoss.scene, "BossSpawnPoint", parent);
        spawnPoint.transform.SetLocalPositionAndRotation(Vector3.zero, Quaternion.identity);
        spawnPoint.transform.localScale = Vector3.one;
        UnityEngine.Object.DestroyImmediate(existingBoss);
        return spawnPoint;
    }

    private static GameObject EnsureSceneObject(Scene scene, string name, Transform parent)
    {
        GameObject existing = FindSceneObject(scene, name);
        if (existing != null)
        {
            existing.transform.SetParent(parent, false);
            return existing;
        }

        GameObject created = new(name);
        SceneManager.MoveGameObjectToScene(created, scene);
        created.transform.SetParent(parent, false);
        return created;
    }

    private static Bounds CalculateRendererBounds(GameObject root)
    {
        Renderer[] renderers = root.GetComponentsInChildren<Renderer>(true);
        if (renderers.Length == 0)
        {
            return new Bounds(Vector3.zero, Vector3.one);
        }

        Bounds bounds = renderers[0].bounds;
        for (int i = 1; i < renderers.Length; i++)
        {
            bounds.Encapsulate(renderers[i].bounds);
        }

        return bounds;
    }

    private static void ConfigureTemporaryStage2Attack(
        GameObject stage1Root,
        GameObject stage2Root,
        RuntimeAnimatorController animatorController,
        DebrisFragmentCatalog saratanDebrisCatalog)
    {
        BossAttackController sourceAttack = stage1Root.GetComponent<BossAttackController>();
        BossBulletPatternController sourcePatterns = stage1Root.GetComponent<BossBulletPatternController>();
        if (sourceAttack == null || sourcePatterns == null)
        {
            throw new InvalidOperationException("Stage 1 prefab does not contain the expected attack components.");
        }

        BossAttackController targetAttack = stage2Root.GetComponent<BossAttackController>();
        if (targetAttack == null)
        {
            targetAttack = stage2Root.AddComponent<BossAttackController>();
        }

        BossBulletPatternController targetPatterns = stage2Root.GetComponent<BossBulletPatternController>();
        if (targetPatterns == null)
        {
            targetPatterns = stage2Root.AddComponent<BossBulletPatternController>();
        }

        // Copy values into components owned by the Stage 2 prefab. No component or
        // pattern asset from Stage 1 is referenced after this one-time migration.
        EditorUtility.CopySerialized(sourceAttack, targetAttack);
        EditorUtility.CopySerialized(sourcePatterns, targetPatterns);
        targetAttack.enabled = true;
        targetPatterns.enabled = true;

        Animator animator = stage2Root.GetComponentInChildren<Animator>(true);
        if (animator == null)
        {
            throw new InvalidOperationException("Stage 2 prefab does not contain an Animator.");
        }

        animator.runtimeAnimatorController = animatorController;
        animator.applyRootMotion = false;

        Transform mouthBone = animator.transform.Find(SaratanMouthBonePath);
        if (mouthBone == null)
        {
            throw new InvalidOperationException($"Saratan mouth bone is missing: {SaratanMouthBonePath}");
        }

        Transform mouthFirePoint = EnsureNamedChild(stage2Root, mouthBone, "SaratanMouthFirePoint");
        mouthFirePoint.SetLocalPositionAndRotation(Vector3.zero, Quaternion.identity);
        mouthFirePoint.localScale = Vector3.one;

        Bounds visualBounds = CalculateVisualBoundsWithNormalizedRoot(stage2Root);
        Transform debrisPoint1 = EnsureNamedChild(stage2Root, stage2Root.transform, "BossFootDebrisFirePoint1");
        Transform debrisPoint2 = EnsureNamedChild(stage2Root, stage2Root.transform, "BossFootDebrisFirePoint2");
        float debrisX = Mathf.Max(0.5f, visualBounds.extents.x * 0.35f);
        float debrisY = visualBounds.min.y + visualBounds.size.y * 0.08f;
        debrisPoint1.localPosition = new Vector3(-debrisX, debrisY, visualBounds.center.z);
        debrisPoint2.localPosition = new Vector3(debrisX, debrisY, visualBounds.center.z);
        debrisPoint1.localRotation = debrisPoint2.localRotation = Quaternion.identity;
        debrisPoint1.localScale = debrisPoint2.localScale = Vector3.one;

        SetObjectReference(targetAttack, "firePoint", mouthFirePoint);
        SetObjectReference(targetAttack, "bulletPatternController", targetPatterns);
        SetObjectReference(targetAttack, "bossAnimator", animator);
        SetObjectReference(targetPatterns, "debrisFragmentCatalog", saratanDebrisCatalog);
        EditorUtility.SetDirty(animator);
        EditorUtility.SetDirty(targetAttack);
        EditorUtility.SetDirty(targetPatterns);
    }

    private static Transform EnsureNamedChild(GameObject searchRoot, Transform parent, string name)
    {
        Transform child = searchRoot.GetComponentsInChildren<Transform>(true)
            .FirstOrDefault(candidate => candidate.name == name);
        if (child == null)
        {
            child = new GameObject(name).transform;
        }

        child.SetParent(parent, false);
        return child;
    }

    private static Bounds CalculateVisualBoundsWithNormalizedRoot(GameObject prefabRoot)
    {
        Transform rootTransform = prefabRoot.transform;
        Vector3 rootPosition = rootTransform.localPosition;
        Quaternion rootRotation = rootTransform.localRotation;
        Vector3 rootScale = rootTransform.localScale;
        rootTransform.SetLocalPositionAndRotation(Vector3.zero, Quaternion.identity);
        rootTransform.localScale = Vector3.one;

        Transform visualRoot = rootTransform.Find("BossVisualRoot");
        Bounds bounds = CalculateRendererBounds(visualRoot != null ? visualRoot.gameObject : prefabRoot);

        rootTransform.SetLocalPositionAndRotation(rootPosition, rootRotation);
        rootTransform.localScale = rootScale;
        return bounds;
    }

    private static float CalculatePrefabVisualHeight(string prefabPath)
    {
        GameObject prefabRoot = PrefabUtility.LoadPrefabContents(prefabPath);
        try
        {
            return CalculateVisualBoundsWithNormalizedRoot(prefabRoot).size.y;
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(prefabRoot);
        }
    }

    private static void UpdateStage2TargetGeometry(GameObject root, Bounds visualBounds)
    {
        Transform aimPoint = root.transform.Find("AimPoint");
        Transform hurtbox = root.transform.Find("BossHurtbox");
        if (aimPoint == null || hurtbox == null)
        {
            throw new InvalidOperationException("Stage 2 prefab is missing AimPoint or BossHurtbox.");
        }

        aimPoint.localPosition = new Vector3(0f, Mathf.Max(1f, visualBounds.size.y * 0.7f), 0f);
        hurtbox.localPosition = new Vector3(0f, visualBounds.size.y * 0.5f, 0f);

        BoxCollider collider = hurtbox.GetComponent<BoxCollider>();
        if (collider == null)
        {
            collider = hurtbox.gameObject.AddComponent<BoxCollider>();
        }

        collider.isTrigger = true;
        collider.size = new Vector3(
            Mathf.Max(1f, visualBounds.size.x * 0.9f),
            Mathf.Max(1f, visualBounds.size.y),
            Mathf.Max(1f, visualBounds.size.z * 0.9f));
    }

    private static AnimatorState CreateAnimatorState(
        AnimatorStateMachine stateMachine,
        string name,
        AnimationClip clip,
        Vector3 position)
    {
        AnimatorState state = stateMachine.AddState(name, position);
        state.motion = clip;
        state.writeDefaultValues = false;
        return state;
    }

    private static void EnsureTriggerParameter(AnimatorController controller, string parameterName)
    {
        AnimatorControllerParameter existing = controller.parameters
            .FirstOrDefault(parameter => parameter.name == parameterName);
        if (existing != null && existing.type == AnimatorControllerParameterType.Trigger)
        {
            return;
        }

        if (existing != null)
        {
            controller.RemoveParameter(existing);
        }

        controller.AddParameter(parameterName, AnimatorControllerParameterType.Trigger);
    }

    private static void ClearStateMachine(AnimatorStateMachine stateMachine)
    {
        foreach (AnimatorStateTransition transition in stateMachine.anyStateTransitions.ToArray())
        {
            stateMachine.RemoveAnyStateTransition(transition);
        }

        foreach (ChildAnimatorState childState in stateMachine.states.ToArray())
        {
            stateMachine.RemoveState(childState.state);
        }

        foreach (ChildAnimatorStateMachine childStateMachine in stateMachine.stateMachines.ToArray())
        {
            stateMachine.RemoveStateMachine(childStateMachine.stateMachine);
        }
    }

    private static void AddAttackTransition(
        AnimatorStateMachine stateMachine,
        AnimatorState targetState,
        string triggerName)
    {
        AnimatorStateTransition transition = stateMachine.AddAnyStateTransition(targetState);
        transition.hasExitTime = false;
        transition.duration = 0.05f;
        transition.hasFixedDuration = true;
        transition.canTransitionToSelf = true;
        transition.AddCondition(AnimatorConditionMode.If, 0f, triggerName);
    }

    private static void AddReturnToIdleTransition(AnimatorState attackState, AnimatorState idleState)
    {
        AnimatorStateTransition transition = attackState.AddTransition(idleState);
        transition.hasExitTime = true;
        transition.exitTime = 0.95f;
        transition.duration = 0.1f;
        transition.hasFixedDuration = true;
    }

    private static void SetClipLoop(AnimationClip clip, bool loopTime)
    {
        AnimationClipSettings settings = AnimationUtility.GetAnimationClipSettings(clip);
        if (settings.loopTime == loopTime)
        {
            return;
        }

        settings.loopTime = loopTime;
        AnimationUtility.SetAnimationClipSettings(clip, settings);
        EditorUtility.SetDirty(clip);
    }

    private static void SetObjectReference(UnityEngine.Object target, string propertyName, UnityEngine.Object value)
    {
        SerializedObject serialized = new(target);
        SerializedProperty property = serialized.FindProperty(propertyName);
        if (property == null)
        {
            throw new InvalidOperationException($"{target.GetType().Name} has no serialized field '{propertyName}'.");
        }

        property.objectReferenceValue = value;
        serialized.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void UpdateManifestFinalPaths()
    {
        string assetPath = Stage2Root + "/import-manifest.json";
        string absolutePath = Path.Combine(Directory.GetParent(Application.dataPath)!.FullName, assetPath);
        if (!File.Exists(absolutePath))
        {
            throw new InvalidOperationException("Saratan import manifest is missing after the move.");
        }

        string contents = File.ReadAllText(absolutePath)
            .Replace(ImportedRoot, Stage2Root, StringComparison.Ordinal);
        File.WriteAllText(absolutePath, contents);
        AssetDatabase.ImportAsset(assetPath, ImportAssetOptions.ForceSynchronousImport);
    }

    private static void ValidateIntegration(
        GameObject stage1Prefab,
        GameObject stage2Prefab,
        StageCatalog catalog)
    {
        if (!catalog.Validate(out string catalogError))
        {
            throw new InvalidOperationException(catalogError);
        }

        if (stage1Prefab.GetComponentsInChildren<BossController>(true).Length != 1 ||
            stage2Prefab.GetComponentsInChildren<BossController>(true).Length != 1)
        {
            throw new InvalidOperationException("Each boss prefab must contain exactly one BossController.");
        }

        ValidateStage2AttackIsolation(stage2Prefab);

        string[] forbiddenDependencies = AssetDatabase.GetDependencies(Stage2Root, true)
            .Where(path =>
                path.StartsWith(Stage1Root, StringComparison.Ordinal) ||
                path.StartsWith("Assets/Animation/Invader/", StringComparison.Ordinal) ||
                path.StartsWith("Assets/Invader/", StringComparison.Ordinal) ||
                path.StartsWith("Assets/Materials/Invader/", StringComparison.Ordinal) ||
                path.StartsWith("Assets/Textures/Invader/", StringComparison.Ordinal) ||
                path.Equals("Assets/Prefabs/Characters/BossPlaceholder.prefab", StringComparison.Ordinal))
            .OrderBy(path => path, StringComparer.Ordinal)
            .ToArray();
        if (forbiddenDependencies.Length > 0)
        {
            throw new InvalidOperationException(
                "Stage 2 content references Stage 1 or package source assets:\n" +
                string.Join("\n", forbiddenDependencies));
        }
    }

    private static void ValidateStage2AttackIsolation(GameObject stage2Prefab)
    {
        BossAttackController stage2Attack = stage2Prefab.GetComponent<BossAttackController>();
        BossBulletPatternController stage2Patterns = stage2Prefab.GetComponent<BossBulletPatternController>();
        if (stage2Attack == null || !stage2Attack.enabled || stage2Patterns == null || !stage2Patterns.enabled)
        {
            throw new InvalidOperationException("Stage 2 temporary attack components must exist and be enabled.");
        }

        if (stage2Prefab.GetComponentInChildren<KaijuBossAnimationDriver>(true) != null)
        {
            throw new InvalidOperationException("Stage 2 must not use the Kaiju-specific animation driver.");
        }

        SerializedObject patterns = new(stage2Patterns);
        UnityEngine.Object debrisCatalog = patterns.FindProperty("debrisFragmentCatalog").objectReferenceValue;
        if (AssetDatabase.GetAssetPath(debrisCatalog) != SaratanDebrisCatalogPath)
        {
            throw new InvalidOperationException("Stage 2 must use its own debris catalog asset.");
        }

        string[] forbiddenDependencies = AssetDatabase.GetDependencies(Stage2PrefabPath, true)
            .Where(path => path.StartsWith(Stage1Root, StringComparison.Ordinal))
            .ToArray();
        if (forbiddenDependencies.Length > 0)
        {
            throw new InvalidOperationException(
                "Stage 2 attack setup references Stage 1 assets:\n" +
                string.Join("\n", forbiddenDependencies));
        }
    }

    private static GameObject FindSceneObject(Scene scene, string name)
    {
        foreach (GameObject root in scene.GetRootGameObjects())
        {
            Transform match = root.GetComponentsInChildren<Transform>(true)
                .FirstOrDefault(candidate => candidate.name == name);
            if (match != null)
            {
                return match.gameObject;
            }
        }

        return null;
    }

    private static void EnsureFolder(string folder)
    {
        if (AssetDatabase.IsValidFolder(folder))
        {
            return;
        }

        string[] parts = folder.Split('/');
        string current = parts[0];
        for (int i = 1; i < parts.Length; i++)
        {
            string next = current + "/" + parts[i];
            if (!AssetDatabase.IsValidFolder(next))
            {
                AssetDatabase.CreateFolder(current, parts[i]);
            }

            current = next;
        }
    }
}
