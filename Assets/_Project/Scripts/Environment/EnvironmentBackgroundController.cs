using UnityEngine;
using UnityEngine.Rendering;

[DefaultExecutionOrder(400)]
public class EnvironmentBackgroundController : MonoBehaviour
{
    // Temporary: keep battle background rotation disabled. FarClouds scroll can still run per layer.
    // Set this to false to restore EnvironmentBackgroundController-driven synced motion.
    private static readonly bool TemporarilyDisableBackgroundMotion = true;

    private enum RotationTrackingMode
    {
        CameraOrbitAroundPivot = 0,
        TransformYawDelta = 1,
        ManualDegreesPerSecond = 2
    }

    [Header("Theme Assets")]
    [SerializeField] private EnvironmentThemeData dayTheme;
    [SerializeField] private EnvironmentThemeData nightTheme;
    [SerializeField] private EnvironmentThemeData rainTheme;
    [SerializeField] private EnvironmentThemeType initialTheme = EnvironmentThemeType.Day;

    [Header("Rotation Sync")]
    [SerializeField] private bool motionEnabled = false;
    [SerializeField] private RotationTrackingMode rotationTrackingMode = RotationTrackingMode.CameraOrbitAroundPivot;
    [SerializeField] private Transform rotationReference;
    [SerializeField] private Transform rotationPivot;
    [SerializeField] private float manualDegreesPerSecond = 14f;
    [SerializeField] private bool resetLayerMotionOnThemeApply;

    [Header("Scene References")]
    [SerializeField] private bool autoAssignSceneReferences = true;
    [SerializeField] private ArenaCameraRig arenaCameraRig;
    [SerializeField] private Camera arenaCamera;
    [SerializeField] private Light directionalLight;
    [SerializeField] private ParticleSystem rainParticleSystem;
    [SerializeField] private BackgroundParallaxLayer[] layers;

    private EnvironmentThemeData activeTheme;
    private bool hasTrackedAngle;
    private float lastTrackedAngle;

    public EnvironmentThemeType ActiveThemeType => activeTheme != null ? activeTheme.themeType : initialTheme;

    public bool MotionEnabled
    {
        get => motionEnabled;
        set
        {
            if (motionEnabled == value)
            {
                return;
            }

            motionEnabled = value;
            ResetTrackedAngle();
        }
    }

    public void AssignThemeAssets(EnvironmentThemeData day, EnvironmentThemeData night, EnvironmentThemeData rain, EnvironmentThemeType defaultTheme)
    {
        dayTheme = day;
        nightTheme = night;
        rainTheme = rain;
        initialTheme = defaultTheme;
    }

    public void RefreshChildReferences()
    {
        layers = GetComponentsInChildren<BackgroundParallaxLayer>(true);
        if (rainParticleSystem == null)
        {
            rainParticleSystem = GetComponentInChildren<ParticleSystem>(true);
        }
    }

    public void ApplyTheme(EnvironmentThemeData theme)
    {
        ApplyTheme(theme, !Application.isPlaying);
    }

    public void SetTheme(EnvironmentThemeType themeType)
    {
        ApplyTheme(GetTheme(themeType));
    }

    public void SetDayTheme()
    {
        SetTheme(EnvironmentThemeType.Day);
    }

    public void SetNightTheme()
    {
        SetTheme(EnvironmentThemeType.Night);
    }

    public void SetRainTheme()
    {
        SetTheme(EnvironmentThemeType.Rain);
    }

    public void UseCameraOrbitReference(Transform reference, Transform pivot)
    {
        rotationTrackingMode = RotationTrackingMode.CameraOrbitAroundPivot;
        rotationReference = reference;
        rotationPivot = pivot;
        ResetTrackedAngle();
    }

    public void UseTransformYawReference(Transform reference)
    {
        rotationTrackingMode = RotationTrackingMode.TransformYawDelta;
        rotationReference = reference;
        rotationPivot = null;
        ResetTrackedAngle();
    }

    public void UseManualRotation(float degreesPerSecond)
    {
        rotationTrackingMode = RotationTrackingMode.ManualDegreesPerSecond;
        manualDegreesPerSecond = degreesPerSecond;
        rotationReference = null;
        rotationPivot = null;
        ResetTrackedAngle();
    }

    public void SyncWithWorldRotation(float worldRotationDelta)
    {
        if (layers == null)
        {
            return;
        }

        for (int i = 0; i < layers.Length; i++)
        {
            if (layers[i] != null)
            {
                layers[i].SyncWithWorldRotation(worldRotationDelta);
            }
        }
    }

    [ContextMenu("Set Day Theme")]
    private void SetDayThemeContext()
    {
        SetDayTheme();
    }

    [ContextMenu("Set Night Theme")]
    private void SetNightThemeContext()
    {
        SetNightTheme();
    }

    [ContextMenu("Set Rain Theme")]
    private void SetRainThemeContext()
    {
        SetRainTheme();
    }

    private void Reset()
    {
        AutoAssignReferences();
        RefreshChildReferences();
        ApplyTheme(GetTheme(initialTheme), true);
    }

    private void Awake()
    {
        AutoAssignReferences();
        RefreshChildReferences();
    }

    private void OnEnable()
    {
        AutoAssignReferences();
        RefreshChildReferences();
        ApplyTheme(GetTheme(initialTheme), !Application.isPlaying);
        if (TemporarilyDisableBackgroundMotion)
        {
            ResetLayerMotion();
        }

        ResetTrackedAngle();
    }

    private void LateUpdate()
    {
        if (!Application.isPlaying)
        {
            return;
        }

        if (TemporarilyDisableBackgroundMotion)
        {
            ResetTrackedAngle();
            TickLayerScroll(Time.deltaTime);
            return;
        }

        if (!motionEnabled)
        {
            ResetTrackedAngle();
            return;
        }

        float worldRotationDelta = GetWorldRotationDelta();
        if (Mathf.Abs(worldRotationDelta) > Mathf.Epsilon)
        {
            SyncWithWorldRotation(worldRotationDelta);
        }

        TickLayerScroll(Time.deltaTime);
    }

    private void OnValidate()
    {
        AutoAssignReferences();
        RefreshChildReferences();
        ApplyTheme(GetTheme(initialTheme), true);
        ResetTrackedAngle();
    }

    private void AutoAssignReferences()
    {
        if (!autoAssignSceneReferences)
        {
            return;
        }

        if (arenaCameraRig == null)
        {
            arenaCameraRig = FindAnyObjectByType<ArenaCameraRig>();
        }

        if (arenaCamera == null && arenaCameraRig != null)
        {
            arenaCamera = arenaCameraRig.GetComponent<Camera>();
        }

        if (arenaCamera == null)
        {
            arenaCamera = Camera.main;
        }

        if (directionalLight == null)
        {
            Light[] sceneLights = FindObjectsByType<Light>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
            for (int i = 0; i < sceneLights.Length; i++)
            {
                if (sceneLights[i] != null && sceneLights[i].type == LightType.Directional)
                {
                    directionalLight = sceneLights[i];
                    break;
                }
            }
        }

        if (rotationReference == null)
        {
            rotationReference = arenaCameraRig != null ? arenaCameraRig.transform : arenaCamera != null ? arenaCamera.transform : null;
        }

        if (rotationPivot == null)
        {
            BossController bossController = FindAnyObjectByType<BossController>();
            rotationPivot = bossController != null ? bossController.transform : null;
        }
    }

    private EnvironmentThemeData GetTheme(EnvironmentThemeType themeType)
    {
        return themeType switch
        {
            EnvironmentThemeType.Night => nightTheme != null ? nightTheme : dayTheme,
            EnvironmentThemeType.Rain => rainTheme != null ? rainTheme : dayTheme,
            _ => dayTheme
        };
    }

    private void ApplyTheme(EnvironmentThemeData theme, bool useSharedMaterials)
    {
        if (theme == null)
        {
            return;
        }

        activeTheme = theme;

        if (resetLayerMotionOnThemeApply && layers != null)
        {
            for (int i = 0; i < layers.Length; i++)
            {
                if (layers[i] != null)
                {
                    layers[i].ResetMotion();
                }
            }
        }

        for (int i = 0; i < layers.Length; i++)
        {
            ApplyThemeToLayer(layers[i], theme, useSharedMaterials);
        }

        ApplyLighting(theme);
        ApplyRain(theme, useSharedMaterials);
    }

    private void ApplyThemeToLayer(BackgroundParallaxLayer layer, EnvironmentThemeData theme, bool useSharedMaterials)
    {
        if (layer == null || theme == null)
        {
            return;
        }

        switch (layer.LayerKind)
        {
            case BackgroundLayerKind.Sky:
                layer.ApplySkySettings(theme.sky, useSharedMaterials);
                break;
            case BackgroundLayerKind.FarClouds:
                layer.ApplyLayerSettings(theme.farClouds, useSharedMaterials);
                break;
            case BackgroundLayerKind.MidClouds:
                layer.ApplyLayerSettings(theme.midClouds, useSharedMaterials);
                break;
            case BackgroundLayerKind.Weather:
                layer.ApplyLayerSettings(theme.weather, useSharedMaterials);
                break;
            case BackgroundLayerKind.Stars:
                layer.ApplyLayerSettings(theme.stars, useSharedMaterials);
                break;
        }
    }

    private void ApplyLighting(EnvironmentThemeData theme)
    {
        if (directionalLight != null)
        {
            directionalLight.color = theme.directionalLightColor;
            directionalLight.intensity = theme.directionalLightIntensity;
            RenderSettings.sun = directionalLight;
        }

        RenderSettings.ambientMode = AmbientMode.Trilight;
        RenderSettings.ambientSkyColor = theme.ambientSkyColor;
        RenderSettings.ambientEquatorColor = theme.ambientEquatorColor;
        RenderSettings.ambientGroundColor = theme.ambientGroundColor;
        RenderSettings.ambientIntensity = theme.ambientIntensity;

        RenderSettings.fog = theme.fogEnabled;
        RenderSettings.fogColor = theme.fogColor;
        RenderSettings.fogDensity = theme.fogDensity;
        RenderSettings.fogMode = FogMode.Linear;
        RenderSettings.fogStartDistance = theme.fogStartDistance;
        RenderSettings.fogEndDistance = theme.fogEndDistance;
    }

    private void ApplyRain(EnvironmentThemeData theme, bool useSharedMaterials)
    {
        if (rainParticleSystem == null)
        {
            return;
        }

        RainThemeSettings rain = theme.rain;
        ParticleSystem.MainModule main = rainParticleSystem.main;
        ParticleSystem.EmissionModule emission = rainParticleSystem.emission;
        ParticleSystem.ShapeModule shape = rainParticleSystem.shape;
        ParticleSystem.VelocityOverLifetimeModule velocity = rainParticleSystem.velocityOverLifetime;
        ParticleSystem.NoiseModule noise = rainParticleSystem.noise;

        main.startSpeed = rain.fallSpeed;
        main.startLifetime = rain.lifetime;
        main.startSize = new ParticleSystem.MinMaxCurve(rain.minParticleSize, rain.maxParticleSize);
        main.maxParticles = rain.maxParticles;

        Color rainColor = rain.tint;
        rainColor.a *= rain.intensity;
        main.startColor = rainColor;

        shape.position = rain.emitterOffset;
        shape.scale = rain.emitterSize;

        velocity.enabled = true;
        velocity.space = ParticleSystemSimulationSpace.Local;
        velocity.x = new ParticleSystem.MinMaxCurve(rain.horizontalDrift);
        velocity.y = new ParticleSystem.MinMaxCurve(-rain.fallSpeed * 0.93f);
        velocity.z = new ParticleSystem.MinMaxCurve(rain.depthDrift);

        noise.enabled = rain.noiseStrength > 0.001f;
        noise.strength = rain.noiseStrength;
        noise.frequency = 0.18f;
        noise.scrollSpeed = 0.15f;

        emission.enabled = rain.enabled && rain.intensity > 0.01f;
        emission.rateOverTime = rain.enabled ? rain.emissionRate * rain.intensity : 0f;

        ParticleSystemRenderer renderer = rainParticleSystem.GetComponent<ParticleSystemRenderer>();
        if (renderer != null)
        {
            renderer.enabled = emission.enabled;
            renderer.renderMode = ParticleSystemRenderMode.Stretch;
            renderer.lengthScale = rain.stretchLength;
            renderer.velocityScale = rain.stretchVelocity;
            renderer.cameraVelocityScale = 0f;
            renderer.sortMode = ParticleSystemSortMode.Distance;

            Material material = useSharedMaterials ? renderer.sharedMaterial : renderer.material;
            if (material != null)
            {
                if (material.HasProperty("_BaseColor"))
                {
                    material.SetColor("_BaseColor", rainColor);
                }
                else if (material.HasProperty("_Color"))
                {
                    material.SetColor("_Color", rainColor);
                }
            }
        }

        if (!Application.isPlaying)
        {
            return;
        }

        if (emission.enabled)
        {
            rainParticleSystem.Play(true);
        }
        else
        {
            rainParticleSystem.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        }
    }

    private float GetWorldRotationDelta()
    {
        if (rotationTrackingMode == RotationTrackingMode.ManualDegreesPerSecond)
        {
            return manualDegreesPerSecond * Time.deltaTime;
        }

        float currentAngle = GetTrackedAngle();
        if (!hasTrackedAngle)
        {
            lastTrackedAngle = currentAngle;
            hasTrackedAngle = true;
            return 0f;
        }

        float delta = Mathf.DeltaAngle(lastTrackedAngle, currentAngle);
        lastTrackedAngle = currentAngle;
        return delta;
    }

    private float GetTrackedAngle()
    {
        if (rotationTrackingMode == RotationTrackingMode.TransformYawDelta)
        {
            return rotationReference != null ? rotationReference.eulerAngles.y : 0f;
        }

        if (rotationReference == null || rotationPivot == null)
        {
            return 0f;
        }

        Vector3 flatDirection = rotationReference.position - rotationPivot.position;
        flatDirection.y = 0f;
        if (flatDirection.sqrMagnitude < 0.0001f)
        {
            return 0f;
        }

        return Mathf.Atan2(flatDirection.x, flatDirection.z) * Mathf.Rad2Deg;
    }

    private void ResetTrackedAngle()
    {
        hasTrackedAngle = false;
        lastTrackedAngle = 0f;
    }

    private void ResetLayerMotion()
    {
        if (layers == null)
        {
            return;
        }

        for (int i = 0; i < layers.Length; i++)
        {
            if (layers[i] != null)
            {
                layers[i].ResetMotion();
            }
        }
    }

    private void TickLayerScroll(float deltaTime)
    {
        if (layers == null)
        {
            return;
        }

        for (int i = 0; i < layers.Length; i++)
        {
            if (layers[i] != null)
            {
                layers[i].Tick(deltaTime, false);
            }
        }
    }
}
