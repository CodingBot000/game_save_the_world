#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

public static class RepairAircraftMaterials
{
    private static readonly MaterialSpec[] MaterialSpecs =
    {
        new("Assets/Materials/Aircraft/Viper.mat", "Assets/Textures/Aircraft/Viper.png"),
        new("Assets/Materials/Aircraft/20mmGatlingGun_Viper.mat", "Assets/Textures/Aircraft/20mmGatlingGun.png"),
        new("Assets/Materials/Aircraft/AGM.mat", "Assets/Textures/Aircraft/AGM.png"),
        new("Assets/Materials/Aircraft/PylonAGM4rds.mat", "Assets/Textures/Aircraft/PylonAGM4rds.png"),
        new("Assets/Materials/Aircraft/RocketPod19rds.mat", "Assets/Textures/Aircraft/RocketPod19rds.png"),
        new("Assets/Materials/Aircraft/Sidewinder.mat", "Assets/Textures/Aircraft/Sidewinder.png"),
        new("Assets/Materials/Aircraft/pilot_test.mat", null, false, new Color(0.15f, 0.17f, 0.19f, 1f), 0.05f),
        new("Assets/Materials/Aircraft/ViperCockpitGlass.mat", null, true, new Color(0.5f, 0.58f, 0.64f, 0.2f), 0.82f),
    };

    [MenuItem("Tools/Titan Destroyer/Repair Aircraft Materials")]
    public static void Repair()
    {
        Shader litShader = Shader.Find("Universal Render Pipeline/Lit")
            ?? Shader.Find("Universal Render Pipeline/Simple Lit")
            ?? Shader.Find("Standard");

        if (litShader == null)
        {
            Debug.LogError("Could not find a supported Lit shader for aircraft materials.");
            return;
        }

        int repairedCount = 0;
        foreach (MaterialSpec spec in MaterialSpecs)
        {
            Material material = AssetDatabase.LoadAssetAtPath<Material>(spec.MaterialPath);
            if (material == null)
            {
                Debug.LogWarning($"Skipped missing material: {spec.MaterialPath}");
                continue;
            }

            Texture2D texture = null;
            if (spec.UseTexture && !string.IsNullOrWhiteSpace(spec.TexturePath))
            {
                texture = AssetDatabase.LoadAssetAtPath<Texture2D>(spec.TexturePath);
                if (texture == null)
                {
                    Debug.LogWarning($"Texture was not found for material '{spec.MaterialPath}': {spec.TexturePath}");
                }
            }

            material.shader = litShader;
            ApplyCommonSettings(material, texture, spec.BaseColor, spec.Smoothness);
            if (spec.Transparent)
            {
                ApplyTransparentSettings(material, spec.BaseColor, spec.Smoothness);
            }
            else
            {
                ApplyOpaqueSettings(material);
            }

            EditorUtility.SetDirty(material);
            repairedCount++;
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log($"Repaired {repairedCount} aircraft materials for URP.");
    }

    private static void ApplyCommonSettings(Material material, Texture2D texture, Color baseColor, float smoothness)
    {
        if (material.HasProperty("_BaseMap"))
        {
            material.SetTexture("_BaseMap", texture);
        }

        if (material.HasProperty("_MainTex"))
        {
            material.SetTexture("_MainTex", texture);
        }

        SetColorIfPresent(material, "_BaseColor", baseColor);
        SetColorIfPresent(material, "_Color", baseColor);
        SetFloatIfPresent(material, "_WorkflowMode", 1f);
        SetFloatIfPresent(material, "_Metallic", 0f);
        SetFloatIfPresent(material, "_Smoothness", smoothness);
        SetFloatIfPresent(material, "_BumpScale", 0f);
        SetFloatIfPresent(material, "_OcclusionStrength", 1f);
        SetFloatIfPresent(material, "_Cull", 2f);

        // Imported aircraft materials were authored for a different toon shader.
        // When we convert them to URP/Lit, these slots must be cleared too or the original
        // green pilot texture can remain visible after a shader repair/reimport cycle.
        if (texture == null)
        {
            SetTextureIfPresent(material, "_1st_ShadeMap", null);
            SetTextureIfPresent(material, "_2nd_ShadeMap", null);
            SetTextureIfPresent(material, "_HighColor_Tex", null);
            SetTextureIfPresent(material, "_BaseColorMap", null);
        }
    }

    private static void ApplyOpaqueSettings(Material material)
    {
        SetFloatIfPresent(material, "_Surface", 0f);
        SetFloatIfPresent(material, "_AlphaClip", 0f);
        SetFloatIfPresent(material, "_Blend", 0f);
        SetFloatIfPresent(material, "_SrcBlend", 1f);
        SetFloatIfPresent(material, "_DstBlend", 0f);
        SetFloatIfPresent(material, "_ZWrite", 1f);
        material.renderQueue = -1;
        material.SetOverrideTag("RenderType", "Opaque");
    }

    private static void ApplyTransparentSettings(Material material, Color glassColor, float smoothness)
    {
        SetColorIfPresent(material, "_BaseColor", glassColor);
        SetColorIfPresent(material, "_Color", glassColor);
        SetFloatIfPresent(material, "_Surface", 1f);
        SetFloatIfPresent(material, "_AlphaClip", 0f);
        SetFloatIfPresent(material, "_Blend", 0f);
        SetFloatIfPresent(material, "_SrcBlend", 5f);
        SetFloatIfPresent(material, "_DstBlend", 10f);
        SetFloatIfPresent(material, "_ZWrite", 0f);
        SetFloatIfPresent(material, "_Smoothness", smoothness);
        material.renderQueue = 3000;
        material.SetOverrideTag("RenderType", "Transparent");
    }

    private static void SetFloatIfPresent(Material material, string propertyName, float value)
    {
        if (material.HasProperty(propertyName))
        {
            material.SetFloat(propertyName, value);
        }
    }

    private static void SetColorIfPresent(Material material, string propertyName, Color value)
    {
        if (material.HasProperty(propertyName))
        {
            material.SetColor(propertyName, value);
        }
    }

    private static void SetTextureIfPresent(Material material, string propertyName, Texture value)
    {
        if (material.HasProperty(propertyName))
        {
            material.SetTexture(propertyName, value);
        }
    }

    private readonly struct MaterialSpec
    {
        public MaterialSpec(
            string materialPath,
            string texturePath,
            bool transparent = false,
            Color? baseColor = null,
            float smoothness = 0.15f)
        {
            MaterialPath = materialPath;
            TexturePath = texturePath;
            Transparent = transparent;
            UseTexture = !string.IsNullOrWhiteSpace(texturePath);
            BaseColor = baseColor ?? Color.white;
            Smoothness = smoothness;
        }

        public string MaterialPath { get; }
        public string TexturePath { get; }
        public bool Transparent { get; }
        public bool UseTexture { get; }
        public Color BaseColor { get; }
        public float Smoothness { get; }
    }
}
#endif
