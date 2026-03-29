using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

public static class InstallBattleArenaBackground
{
    private const string BattleArenaScenePath = "Assets/Scenes/BattleArena.unity/BattleArena.unity";
    private const string PrefabPath = "Assets/Prefabs/Environment/BattleArenaBackgroundRoot.prefab";

    private const string SkyMaterialPath = "Assets/Materials/Environment/Background/SkyGradient.mat";
    private const string FarCloudMaterialPath = "Assets/Materials/Environment/Background/FarClouds.mat";
    private const string MidCloudMaterialPath = "Assets/Materials/Environment/Background/MidClouds.mat";
    private const string WeatherMaterialPath = "Assets/Materials/Environment/Background/WeatherClouds.mat";
    private const string StarsMaterialPath = "Assets/Materials/Environment/Background/NightStars.mat";
    private const string RainMaterialPath = "Assets/Materials/Environment/Background/RainParticles.mat";

    private const string DayThemePath = "Assets/ScriptableObjects/EnvironmentThemes/DayTheme.asset";
    private const string NightThemePath = "Assets/ScriptableObjects/EnvironmentThemes/NightTheme.asset";
    private const string RainThemePath = "Assets/ScriptableObjects/EnvironmentThemes/RainTheme.asset";

    [MenuItem("Tools/TitanDestroyer/Install BattleArena Background")]
    public static void Install()
    {
        EnsureFolders();

        EnvironmentThemeData dayTheme = LoadOrCreateTheme(DayThemePath, EnvironmentThemeType.Day, ConfigureDayTheme);
        EnvironmentThemeData nightTheme = LoadOrCreateTheme(NightThemePath, EnvironmentThemeType.Night, ConfigureNightTheme);
        EnvironmentThemeData rainTheme = LoadOrCreateTheme(RainThemePath, EnvironmentThemeType.Rain, ConfigureRainTheme);

        Material skyMaterial = LoadOrCreateMaterial(SkyMaterialPath, "TitanDestroyer/Environment/StylizedSkyGradient");
        Material farCloudMaterial = LoadOrCreateMaterial(FarCloudMaterialPath, "TitanDestroyer/Environment/StylizedCloudLayer");
        Material midCloudMaterial = LoadOrCreateMaterial(MidCloudMaterialPath, "TitanDestroyer/Environment/StylizedCloudLayer");
        Material weatherMaterial = LoadOrCreateMaterial(WeatherMaterialPath, "TitanDestroyer/Environment/StylizedCloudLayer");
        Material starsMaterial = LoadOrCreateMaterial(StarsMaterialPath, "TitanDestroyer/Environment/StylizedStars");
        Material rainMaterial = LoadOrCreateParticleMaterial(RainMaterialPath);

        GameObject prefabRoot = BuildBackgroundPrefabAsset(dayTheme, nightTheme, rainTheme, skyMaterial, farCloudMaterial, midCloudMaterial, weatherMaterial, starsMaterial, rainMaterial);
        if (prefabRoot == null)
        {
            Debug.LogError("Background prefab could not be created.");
            return;
        }

        Scene scene = EnsureBattleArenaScene();
        Transform battleArenaRoot = FindRoot(scene, "BattleArenaRoot");
        if (battleArenaRoot == null)
        {
            Debug.LogError("BattleArenaRoot was not found.");
            return;
        }

        GameObject existing = FindChild(battleArenaRoot, "BackgroundRoot")?.gameObject;
        if (existing != null)
        {
            UnityEngine.Object.DestroyImmediate(existing);
        }

        GameObject instance = PrefabUtility.InstantiatePrefab(prefabRoot, battleArenaRoot) as GameObject;
        if (instance == null)
        {
            Debug.LogError("Background prefab could not be instantiated.");
            return;
        }

        instance.name = "BackgroundRoot";
        instance.transform.SetSiblingIndex(0);
        instance.transform.localPosition = Vector3.zero;
        instance.transform.localRotation = Quaternion.identity;
        instance.transform.localScale = Vector3.one;

        EnvironmentBackgroundController controller = instance.GetComponent<EnvironmentBackgroundController>();
        controller.RefreshChildReferences();
        controller.ApplyTheme(dayTheme);

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Debug.Log("Installed BattleArena background system.");
    }

    private static GameObject BuildBackgroundPrefabAsset(
        EnvironmentThemeData dayTheme,
        EnvironmentThemeData nightTheme,
        EnvironmentThemeData rainTheme,
        Material skyMaterial,
        Material farCloudMaterial,
        Material midCloudMaterial,
        Material weatherMaterial,
        Material starsMaterial,
        Material rainMaterial)
    {
        GameObject root = new("BackgroundRoot");
        try
        {
            EnvironmentBackgroundController controller = root.AddComponent<EnvironmentBackgroundController>();
            controller.AssignThemeAssets(dayTheme, nightTheme, rainTheme, EnvironmentThemeType.Day);

            CreateShellLayer(root.transform, "Sky Layer", "SkyShell", BackgroundLayerKind.Sky, skyMaterial, new Vector3(240f, 210f, 240f));
            CreateShellLayer(root.transform, "Far Clouds Layer", "FarCloudShell", BackgroundLayerKind.FarClouds, farCloudMaterial, new Vector3(190f, 122f, 190f));
            CreateShellLayer(root.transform, "Mid Clouds Layer", "MidCloudShell", BackgroundLayerKind.MidClouds, midCloudMaterial, new Vector3(168f, 106f, 168f));
            CreateShellLayer(root.transform, "Weather Layer", "WeatherShell", BackgroundLayerKind.Weather, weatherMaterial, new Vector3(178f, 98f, 178f));
            CreateShellLayer(root.transform, "Optional Stars Layer", "StarShell", BackgroundLayerKind.Stars, starsMaterial, new Vector3(208f, 208f, 208f));
            CreateRainLayer(root.transform, rainMaterial);

            controller.RefreshChildReferences();
            controller.ApplyTheme(dayTheme);

            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
            return prefab;
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(root);
        }
    }

    private static void CreateShellLayer(
        Transform parent,
        string layerName,
        string meshName,
        BackgroundLayerKind layerKind,
        Material material,
        Vector3 shellScale)
    {
        GameObject layerRoot = new(layerName);
        layerRoot.transform.SetParent(parent, false);
        layerRoot.transform.localPosition = Vector3.zero;
        layerRoot.transform.localRotation = Quaternion.identity;
        layerRoot.transform.localScale = Vector3.one;

        GameObject shell = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        shell.name = meshName;
        shell.transform.SetParent(layerRoot.transform, false);
        shell.transform.localPosition = Vector3.zero;
        shell.transform.localRotation = Quaternion.identity;
        shell.transform.localScale = shellScale;

        Collider collider = shell.GetComponent<Collider>();
        if (collider != null)
        {
            UnityEngine.Object.DestroyImmediate(collider);
        }

        Renderer renderer = shell.GetComponent<Renderer>();
        if (renderer != null)
        {
            renderer.sharedMaterial = material;
            ConfigureBackgroundRenderer(renderer);
        }

        BackgroundParallaxLayer layer = layerRoot.AddComponent<BackgroundParallaxLayer>();
        layer.Configure(layerKind, renderer, layerRoot.transform);
    }

    private static void CreateRainLayer(Transform parent, Material rainMaterial)
    {
        GameObject rainRoot = new("Optional Rain FX Root");
        rainRoot.transform.SetParent(parent, false);
        rainRoot.transform.localPosition = Vector3.zero;
        rainRoot.transform.localRotation = Quaternion.identity;
        rainRoot.transform.localScale = Vector3.one;

        ParticleSystem particleSystem = rainRoot.AddComponent<ParticleSystem>();
        ParticleSystemRenderer renderer = rainRoot.GetComponent<ParticleSystemRenderer>();

        ParticleSystem.MainModule main = particleSystem.main;
        main.loop = true;
        main.playOnAwake = true;
        main.simulationSpace = ParticleSystemSimulationSpace.Local;
        main.startLifetime = 2.5f;
        main.startSpeed = 45f;
        main.startSize = new ParticleSystem.MinMaxCurve(0.03f, 0.05f);
        main.startColor = new Color(0.74f, 0.82f, 0.93f, 0.24f);
        main.maxParticles = 1200;

        ParticleSystem.EmissionModule emission = particleSystem.emission;
        emission.enabled = false;
        emission.rateOverTime = 0f;

        ParticleSystem.ShapeModule shape = particleSystem.shape;
        shape.enabled = true;
        shape.shapeType = ParticleSystemShapeType.Box;
        shape.position = new Vector3(0f, 24f, 0f);
        shape.scale = new Vector3(44f, 4f, 44f);

        ParticleSystem.VelocityOverLifetimeModule velocity = particleSystem.velocityOverLifetime;
        velocity.enabled = true;
        velocity.space = ParticleSystemSimulationSpace.Local;
        velocity.x = new ParticleSystem.MinMaxCurve(1.2f);
        velocity.y = new ParticleSystem.MinMaxCurve(-41.85f);
        velocity.z = new ParticleSystem.MinMaxCurve(0.8f);

        ParticleSystem.NoiseModule noise = particleSystem.noise;
        noise.enabled = true;
        noise.strength = 0.18f;
        noise.frequency = 0.18f;
        noise.scrollSpeed = 0.15f;

        renderer.sharedMaterial = rainMaterial;
        renderer.renderMode = ParticleSystemRenderMode.Stretch;
        renderer.alignment = ParticleSystemRenderSpace.View;
        renderer.lengthScale = 2.6f;
        renderer.velocityScale = 0.42f;
        renderer.cameraVelocityScale = 0f;
        renderer.sortMode = ParticleSystemSortMode.Distance;
        renderer.shadowCastingMode = ShadowCastingMode.Off;
        renderer.receiveShadows = false;
        renderer.lightProbeUsage = LightProbeUsage.Off;
        renderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
    }

    private static void ConfigureBackgroundRenderer(Renderer renderer)
    {
        renderer.shadowCastingMode = ShadowCastingMode.Off;
        renderer.receiveShadows = false;
        renderer.lightProbeUsage = LightProbeUsage.Off;
        renderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
        renderer.motionVectorGenerationMode = MotionVectorGenerationMode.ForceNoMotion;
    }

    private static EnvironmentThemeData LoadOrCreateTheme(string assetPath, EnvironmentThemeType themeType, Action<EnvironmentThemeData> configure)
    {
        EnvironmentThemeData theme = AssetDatabase.LoadAssetAtPath<EnvironmentThemeData>(assetPath);
        if (theme == null)
        {
            theme = ScriptableObject.CreateInstance<EnvironmentThemeData>();
            theme.themeType = themeType;
            configure(theme);
            AssetDatabase.CreateAsset(theme, assetPath);
        }

        EditorUtility.SetDirty(theme);
        return theme;
    }

    private static Material LoadOrCreateMaterial(string assetPath, string shaderName)
    {
        Material material = AssetDatabase.LoadAssetAtPath<Material>(assetPath);
        Shader shader = Shader.Find(shaderName);
        if (shader == null)
        {
            throw new InvalidOperationException($"Shader not found: {shaderName}");
        }

        if (material == null)
        {
            material = new Material(shader);
            AssetDatabase.CreateAsset(material, assetPath);
        }
        else
        {
            material.shader = shader;
        }

        EditorUtility.SetDirty(material);
        return material;
    }

    private static Material LoadOrCreateParticleMaterial(string assetPath)
    {
        Material material = AssetDatabase.LoadAssetAtPath<Material>(assetPath);
        Shader shader = Shader.Find("Universal Render Pipeline/Particles/Unlit");
        if (shader == null)
        {
            shader = Shader.Find("Particles/Standard Unlit");
        }

        if (shader == null)
        {
            throw new InvalidOperationException("A particle shader compatible with the current project could not be found.");
        }

        if (material == null)
        {
            material = new Material(shader);
            AssetDatabase.CreateAsset(material, assetPath);
        }
        else
        {
            material.shader = shader;
        }

        if (material.HasProperty("_Surface"))
        {
            material.SetFloat("_Surface", 1f);
        }

        if (material.HasProperty("_Blend"))
        {
            material.SetFloat("_Blend", 0f);
        }

        if (material.HasProperty("_BaseColor"))
        {
            material.SetColor("_BaseColor", new Color(0.74f, 0.82f, 0.93f, 0.16f));
        }
        else if (material.HasProperty("_Color"))
        {
            material.SetColor("_Color", new Color(0.74f, 0.82f, 0.93f, 0.16f));
        }

        EditorUtility.SetDirty(material);
        return material;
    }

    private static void EnsureFolders()
    {
        EnsureFolder("Assets/Materials/Environment/Background");
        EnsureFolder("Assets/Prefabs/Environment");
        EnsureFolder("Assets/ScriptableObjects/EnvironmentThemes");
    }

    private static void EnsureFolder(string assetPath)
    {
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

        for (int i = 0; i < root.childCount; i++)
        {
            Transform child = root.GetChild(i);
            if (child.name == name)
            {
                return child;
            }
        }

        return null;
    }

    private static void ConfigureDayTheme(EnvironmentThemeData theme)
    {
        theme.sky.topColor = new Color(0.54f, 0.74f, 0.94f, 1f);
        theme.sky.horizonColor = new Color(0.95f, 0.98f, 1f, 1f);
        theme.sky.bottomColor = new Color(0.76f, 0.87f, 0.97f, 1f);
        theme.sky.horizonOffset = -0.08f;
        theme.sky.horizonSoftness = 0.22f;
        theme.sky.rotationMultiplier = 0.08f;

        theme.farClouds.enabled = true;
        theme.farClouds.tint = new Color(0.93f, 0.96f, 0.99f, 1f);
        theme.farClouds.opacity = 0.38f;
        theme.farClouds.rotationMultiplier = 0.15f;
        theme.farClouds.scrollVelocity = new Vector2(0.0018f, 0.0001f);
        theme.farClouds.patternScale = 4.2f;
        theme.farClouds.coverage = 0.54f;
        theme.farClouds.softness = 0.15f;
        theme.farClouds.bandCenter = 0.73f;
        theme.farClouds.bandWidth = 0.12f;
        theme.farClouds.intensity = 1f;

        theme.midClouds.enabled = true;
        theme.midClouds.tint = new Color(0.96f, 0.98f, 1f, 1f);
        theme.midClouds.opacity = 0.6f;
        theme.midClouds.rotationMultiplier = 0.32f;
        theme.midClouds.scrollVelocity = new Vector2(0.0038f, 0.00025f);
        theme.midClouds.patternScale = 5.8f;
        theme.midClouds.coverage = 0.5f;
        theme.midClouds.softness = 0.13f;
        theme.midClouds.bandCenter = 0.62f;
        theme.midClouds.bandWidth = 0.18f;
        theme.midClouds.intensity = 1f;

        theme.weather.enabled = false;
        theme.weather.tint = new Color(0.64f, 0.72f, 0.81f, 1f);
        theme.weather.opacity = 0f;
        theme.weather.rotationMultiplier = 0.2f;
        theme.weather.scrollVelocity = new Vector2(0.0012f, 0f);
        theme.weather.patternScale = 3.4f;
        theme.weather.coverage = 0.5f;
        theme.weather.softness = 0.18f;
        theme.weather.bandCenter = 0.58f;
        theme.weather.bandWidth = 0.24f;
        theme.weather.intensity = 1f;

        theme.stars.enabled = false;
        theme.stars.opacity = 0f;
        theme.stars.rotationMultiplier = 0.1f;
        theme.stars.density = 76f;
        theme.stars.intensity = 1.2f;
        theme.stars.twinkleSpeed = 1.6f;
        theme.stars.bandCenter = 0.34f;
        theme.stars.bandWidth = 0.22f;

        theme.directionalLightColor = new Color(1f, 0.985f, 0.95f, 1f);
        theme.directionalLightIntensity = 1.32f;
        theme.ambientSkyColor = new Color(0.5f, 0.61f, 0.73f, 1f);
        theme.ambientEquatorColor = new Color(0.28f, 0.34f, 0.4f, 1f);
        theme.ambientGroundColor = new Color(0.09f, 0.1f, 0.1f, 1f);
        theme.ambientIntensity = 1.08f;
        theme.fogEnabled = false;
        theme.fogColor = new Color(0.63f, 0.72f, 0.82f, 1f);
        theme.fogDensity = 0.004f;
        theme.fogStartDistance = 55f;
        theme.fogEndDistance = 145f;
        theme.rain.enabled = false;
        theme.rain.intensity = 0f;
    }

    private static void ConfigureNightTheme(EnvironmentThemeData theme)
    {
        theme.sky.topColor = new Color(0.05f, 0.08f, 0.18f, 1f);
        theme.sky.horizonColor = new Color(0.18f, 0.22f, 0.34f, 1f);
        theme.sky.bottomColor = new Color(0.02f, 0.03f, 0.08f, 1f);
        theme.sky.horizonOffset = -0.03f;
        theme.sky.horizonSoftness = 0.28f;
        theme.sky.rotationMultiplier = 0.06f;

        theme.farClouds.enabled = true;
        theme.farClouds.tint = new Color(0.25f, 0.31f, 0.42f, 1f);
        theme.farClouds.opacity = 0.24f;
        theme.farClouds.rotationMultiplier = 0.12f;
        theme.farClouds.scrollVelocity = new Vector2(0.0011f, 0.00006f);
        theme.farClouds.patternScale = 4.6f;
        theme.farClouds.coverage = 0.58f;
        theme.farClouds.softness = 0.18f;
        theme.farClouds.bandCenter = 0.71f;
        theme.farClouds.bandWidth = 0.15f;
        theme.farClouds.intensity = 0.82f;

        theme.midClouds.enabled = true;
        theme.midClouds.tint = new Color(0.33f, 0.39f, 0.52f, 1f);
        theme.midClouds.opacity = 0.34f;
        theme.midClouds.rotationMultiplier = 0.26f;
        theme.midClouds.scrollVelocity = new Vector2(0.0026f, 0.00012f);
        theme.midClouds.patternScale = 5.8f;
        theme.midClouds.coverage = 0.56f;
        theme.midClouds.softness = 0.16f;
        theme.midClouds.bandCenter = 0.61f;
        theme.midClouds.bandWidth = 0.2f;
        theme.midClouds.intensity = 0.9f;

        theme.weather.enabled = false;
        theme.weather.tint = new Color(0.17f, 0.21f, 0.3f, 1f);
        theme.weather.opacity = 0.08f;
        theme.weather.rotationMultiplier = 0.18f;
        theme.weather.scrollVelocity = new Vector2(0.001f, 0f);
        theme.weather.patternScale = 3.2f;
        theme.weather.coverage = 0.48f;
        theme.weather.softness = 0.2f;
        theme.weather.bandCenter = 0.56f;
        theme.weather.bandWidth = 0.26f;
        theme.weather.intensity = 0.72f;

        theme.stars.enabled = true;
        theme.stars.tint = new Color(0.78f, 0.86f, 1f, 1f);
        theme.stars.opacity = 0.88f;
        theme.stars.rotationMultiplier = 0.1f;
        theme.stars.scrollVelocity = new Vector2(0.0004f, 0f);
        theme.stars.intensity = 1.35f;
        theme.stars.density = 78f;
        theme.stars.twinkleSpeed = 1.9f;
        theme.stars.bandCenter = 0.34f;
        theme.stars.bandWidth = 0.2f;

        theme.directionalLightColor = new Color(0.47f, 0.56f, 0.72f, 1f);
        theme.directionalLightIntensity = 0.45f;
        theme.ambientSkyColor = new Color(0.09f, 0.12f, 0.19f, 1f);
        theme.ambientEquatorColor = new Color(0.05f, 0.07f, 0.1f, 1f);
        theme.ambientGroundColor = new Color(0.02f, 0.025f, 0.03f, 1f);
        theme.ambientIntensity = 0.75f;
        theme.fogEnabled = true;
        theme.fogColor = new Color(0.08f, 0.11f, 0.17f, 1f);
        theme.fogDensity = 0.007f;
        theme.fogStartDistance = 42f;
        theme.fogEndDistance = 118f;
        theme.rain.enabled = false;
        theme.rain.intensity = 0f;
    }

    private static void ConfigureRainTheme(EnvironmentThemeData theme)
    {
        theme.sky.topColor = new Color(0.22f, 0.27f, 0.36f, 1f);
        theme.sky.horizonColor = new Color(0.43f, 0.48f, 0.57f, 1f);
        theme.sky.bottomColor = new Color(0.1f, 0.13f, 0.18f, 1f);
        theme.sky.horizonOffset = -0.04f;
        theme.sky.horizonSoftness = 0.3f;
        theme.sky.rotationMultiplier = 0.07f;

        theme.farClouds.enabled = true;
        theme.farClouds.tint = new Color(0.57f, 0.62f, 0.69f, 1f);
        theme.farClouds.opacity = 0.4f;
        theme.farClouds.rotationMultiplier = 0.15f;
        theme.farClouds.scrollVelocity = new Vector2(0.0023f, 0.0001f);
        theme.farClouds.patternScale = 4.8f;
        theme.farClouds.coverage = 0.56f;
        theme.farClouds.softness = 0.17f;
        theme.farClouds.bandCenter = 0.72f;
        theme.farClouds.bandWidth = 0.16f;
        theme.farClouds.intensity = 0.9f;

        theme.midClouds.enabled = true;
        theme.midClouds.tint = new Color(0.45f, 0.5f, 0.58f, 1f);
        theme.midClouds.opacity = 0.58f;
        theme.midClouds.rotationMultiplier = 0.32f;
        theme.midClouds.scrollVelocity = new Vector2(0.0048f, 0.0002f);
        theme.midClouds.patternScale = 6.3f;
        theme.midClouds.coverage = 0.49f;
        theme.midClouds.softness = 0.13f;
        theme.midClouds.bandCenter = 0.61f;
        theme.midClouds.bandWidth = 0.22f;
        theme.midClouds.intensity = 0.88f;

        theme.weather.enabled = true;
        theme.weather.tint = new Color(0.35f, 0.41f, 0.49f, 1f);
        theme.weather.opacity = 0.34f;
        theme.weather.rotationMultiplier = 0.22f;
        theme.weather.scrollVelocity = new Vector2(0.0018f, 0f);
        theme.weather.patternScale = 3.1f;
        theme.weather.coverage = 0.46f;
        theme.weather.softness = 0.19f;
        theme.weather.bandCenter = 0.56f;
        theme.weather.bandWidth = 0.28f;
        theme.weather.intensity = 0.86f;

        theme.stars.enabled = false;
        theme.stars.opacity = 0f;
        theme.stars.rotationMultiplier = 0.08f;
        theme.stars.density = 72f;
        theme.stars.intensity = 1f;
        theme.stars.twinkleSpeed = 1.4f;
        theme.stars.bandCenter = 0.34f;
        theme.stars.bandWidth = 0.18f;

        theme.directionalLightColor = new Color(0.73f, 0.79f, 0.87f, 1f);
        theme.directionalLightIntensity = 0.72f;
        theme.ambientSkyColor = new Color(0.18f, 0.23f, 0.3f, 1f);
        theme.ambientEquatorColor = new Color(0.11f, 0.14f, 0.18f, 1f);
        theme.ambientGroundColor = new Color(0.04f, 0.05f, 0.06f, 1f);
        theme.ambientIntensity = 0.82f;
        theme.fogEnabled = true;
        theme.fogColor = new Color(0.31f, 0.37f, 0.46f, 1f);
        theme.fogDensity = 0.012f;
        theme.fogStartDistance = 28f;
        theme.fogEndDistance = 92f;
        theme.rain.enabled = true;
        theme.rain.intensity = 0.82f;
        theme.rain.tint = new Color(0.78f, 0.86f, 0.95f, 0.28f);
        theme.rain.emissionRate = 560f;
        theme.rain.fallSpeed = 45f;
        theme.rain.lifetime = 2.25f;
        theme.rain.minParticleSize = 0.03f;
        theme.rain.maxParticleSize = 0.05f;
        theme.rain.emitterOffset = new Vector3(0f, 24f, 0f);
        theme.rain.emitterSize = new Vector3(44f, 4f, 44f);
        theme.rain.horizontalDrift = 1.2f;
        theme.rain.depthDrift = 0.8f;
        theme.rain.noiseStrength = 0.18f;
        theme.rain.stretchLength = 2.6f;
        theme.rain.stretchVelocity = 0.42f;
        theme.rain.maxParticles = 1200;
    }
}
