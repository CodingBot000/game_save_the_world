using System;
using UnityEngine;

[Serializable]
public class SkyGradientThemeSettings
{
    public Color topColor = new(0.43f, 0.66f, 0.88f, 1f);
    public Color horizonColor = new(0.86f, 0.92f, 0.97f, 1f);
    public Color bottomColor = new(0.62f, 0.76f, 0.89f, 1f);

    [Range(-0.5f, 0.5f)] public float horizonOffset = -0.06f;
    [Range(0.01f, 1f)] public float horizonSoftness = 0.26f;
    [Min(0f)] public float rotationMultiplier = 0.08f;
}

[Serializable]
public class ParallaxLayerThemeSettings
{
    public bool enabled = true;
    public Color tint = Color.white;

    [Range(0f, 1f)] public float opacity = 0.5f;
    [Min(0f)] public float rotationMultiplier = 0.15f;
    public Vector2 scrollVelocity = new(0.002f, 0f);

    [Min(0.1f)] public float patternScale = 4f;
    [Range(0f, 1f)] public float coverage = 0.55f;
    [Range(0.01f, 0.8f)] public float softness = 0.16f;
    [Range(0f, 1f)] public float bandCenter = 0.65f;
    [Range(0.01f, 1f)] public float bandWidth = 0.2f;

    [Range(0f, 8f)] public float intensity = 1f;
    [Range(1f, 128f)] public float density = 48f;
    [Range(0f, 8f)] public float twinkleSpeed = 1.5f;
}

[Serializable]
public class RainThemeSettings
{
    public bool enabled;
    [Range(0f, 1f)] public float intensity = 0.75f;
    public Color tint = new(0.74f, 0.82f, 0.93f, 0.18f);
    [Min(0f)] public float emissionRate = 380f;
    [Min(0.1f)] public float fallSpeed = 45f;
    [Min(0.1f)] public float lifetime = 2.5f;
    [Min(0.01f)] public float minParticleSize = 0.03f;
    [Min(0.01f)] public float maxParticleSize = 0.05f;
    public Vector3 emitterOffset = new(0f, 24f, 0f);
    public Vector3 emitterSize = new(44f, 4f, 44f);
    [Range(0f, 4f)] public float horizontalDrift = 1.2f;
    [Range(0f, 4f)] public float depthDrift = 0.8f;
    [Range(0f, 2f)] public float noiseStrength = 0.18f;
    [Range(0.1f, 8f)] public float stretchLength = 2.6f;
    [Range(0f, 4f)] public float stretchVelocity = 0.42f;
    [Min(64)] public int maxParticles = 1200;
}

[CreateAssetMenu(
    fileName = "EnvironmentTheme",
    menuName = "TitanDestroyer/Environment Theme",
    order = 10)]
public class EnvironmentThemeData : ScriptableObject
{
    [Header("Identity")]
    public EnvironmentThemeType themeType = EnvironmentThemeType.Day;

    [Header("Sky")]
    public SkyGradientThemeSettings sky = new();

    [Header("Cloud Layers")]
    public ParallaxLayerThemeSettings farClouds = new();
    public ParallaxLayerThemeSettings midClouds = new();
    public ParallaxLayerThemeSettings weather = new();
    public ParallaxLayerThemeSettings stars = new();

    [Header("Lighting")]
    public Color directionalLightColor = Color.white;
    [Range(0f, 2f)] public float directionalLightIntensity = 1.2f;

    [Header("Ambient")]
    public Color ambientSkyColor = new(0.36f, 0.43f, 0.52f, 1f);
    public Color ambientEquatorColor = new(0.2f, 0.25f, 0.29f, 1f);
    public Color ambientGroundColor = new(0.08f, 0.09f, 0.1f, 1f);
    [Range(0f, 2f)] public float ambientIntensity = 1f;

    [Header("Fog")]
    public bool fogEnabled;
    public Color fogColor = new(0.63f, 0.72f, 0.81f, 1f);
    [Range(0f, 0.1f)] public float fogDensity = 0.01f;
    [Min(0f)] public float fogStartDistance = 40f;
    [Min(0f)] public float fogEndDistance = 130f;

    [Header("Weather")]
    public RainThemeSettings rain = new();
}
