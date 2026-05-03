using UnityEngine;

[ExecuteAlways]
[DefaultExecutionOrder(500)]
public class BackgroundParallaxLayer : MonoBehaviour
{
    // Temporary: hard-stop only synced parallax rotation driven by the stage/camera.
    // Set this to false to restore synced parallax layer rotation.
    // Cloud pattern rotation is handled independently in LateUpdate and is not affected by this flag.
    private static readonly bool TemporarilyDisableSyncedLayerRotation = true;
    private static readonly bool TemporarilyAllowFarCloudScroll = true;
    private const float TemporaryFarCloudScrollSpeedScale = 0.5f;

    private static readonly int TopColorId = Shader.PropertyToID("_TopColor");
    private static readonly int HorizonColorId = Shader.PropertyToID("_HorizonColor");
    private static readonly int BottomColorId = Shader.PropertyToID("_BottomColor");
    private static readonly int HorizonOffsetId = Shader.PropertyToID("_HorizonOffset");
    private static readonly int HorizonSoftnessId = Shader.PropertyToID("_HorizonSoftness");
    private static readonly int TintId = Shader.PropertyToID("_Tint");
    private static readonly int OpacityId = Shader.PropertyToID("_Opacity");
    private static readonly int ScrollOffsetId = Shader.PropertyToID("_ScrollOffset");
    private static readonly int PatternScaleId = Shader.PropertyToID("_PatternScale");
    private static readonly int CoverageId = Shader.PropertyToID("_Coverage");
    private static readonly int SoftnessId = Shader.PropertyToID("_Softness");
    private static readonly int BandCenterId = Shader.PropertyToID("_BandCenter");
    private static readonly int BandWidthId = Shader.PropertyToID("_BandWidth");
    private static readonly int IntensityId = Shader.PropertyToID("_Intensity");
    private static readonly int DensityId = Shader.PropertyToID("_Density");
    private static readonly int TwinkleSpeedId = Shader.PropertyToID("_TwinkleSpeed");

    [SerializeField] private BackgroundLayerKind layerKind = BackgroundLayerKind.Sky;
    [SerializeField] private Transform rotationRoot;
    [SerializeField] private Renderer targetRenderer;
    [SerializeField] private float farCloudPatternDegreesPerSecond = 4f;
    [SerializeField] private float midCloudPatternDegreesPerSecond = 6f;

    private Material runtimeMaterial;
    private Quaternion baseLocalRotation = Quaternion.identity;
    private bool hasBaseRotation;
    private float accumulatedRotationY;
    private Vector2 accumulatedScroll;
    private float currentRotationMultiplier;
    private Vector2 currentScrollVelocity;

    public BackgroundLayerKind LayerKind => layerKind;

    public void Configure(BackgroundLayerKind kind, Renderer renderer, Transform root = null)
    {
        layerKind = kind;
        targetRenderer = renderer;
        rotationRoot = root != null ? root : transform;
        CacheReferences();
    }

    public void ResetMotion(bool resetScroll = true)
    {
        CacheReferences();
        accumulatedRotationY = 0f;

        if (rotationRoot != null)
        {
            rotationRoot.localRotation = baseLocalRotation;
        }

        if (!resetScroll)
        {
            return;
        }

        accumulatedScroll = Vector2.zero;
        ApplyScrollOffset(GetWritableMaterial(UseSharedMaterial()));
    }

    public void ApplySkySettings(SkyGradientThemeSettings settings, bool useSharedMaterial)
    {
        CacheReferences();
        currentRotationMultiplier = settings != null ? settings.rotationMultiplier : 0f;
        currentScrollVelocity = Vector2.zero;

        if (targetRenderer == null)
        {
            return;
        }

        targetRenderer.enabled = true;

        Material material = GetWritableMaterial(useSharedMaterial);
        if (material == null)
        {
            return;
        }

        SetColorIfPresent(material, TopColorId, settings.topColor);
        SetColorIfPresent(material, HorizonColorId, settings.horizonColor);
        SetColorIfPresent(material, BottomColorId, settings.bottomColor);
        SetFloatIfPresent(material, HorizonOffsetId, settings.horizonOffset);
        SetFloatIfPresent(material, HorizonSoftnessId, settings.horizonSoftness);
    }

    public void ApplyLayerSettings(ParallaxLayerThemeSettings settings, bool useSharedMaterial)
    {
        CacheReferences();
        currentRotationMultiplier = settings != null ? settings.rotationMultiplier : 0f;
        currentScrollVelocity = settings != null ? settings.scrollVelocity : Vector2.zero;

        if (targetRenderer == null)
        {
            return;
        }

        targetRenderer.enabled = settings != null && settings.enabled && settings.opacity > 0.001f;

        Material material = GetWritableMaterial(useSharedMaterial);
        if (material == null || settings == null)
        {
            return;
        }

        SetColorIfPresent(material, TintId, settings.tint);
        SetFloatIfPresent(material, OpacityId, settings.opacity);
        SetFloatIfPresent(material, PatternScaleId, settings.patternScale);
        SetFloatIfPresent(material, CoverageId, settings.coverage);
        SetFloatIfPresent(material, SoftnessId, settings.softness);
        SetFloatIfPresent(material, BandCenterId, settings.bandCenter);
        SetFloatIfPresent(material, BandWidthId, settings.bandWidth);
        SetFloatIfPresent(material, IntensityId, settings.intensity);
        SetFloatIfPresent(material, DensityId, settings.density);
        SetFloatIfPresent(material, TwinkleSpeedId, settings.twinkleSpeed);
        ApplyScrollOffset(material);
    }

    public void SyncWithWorldRotation(float worldRotationDelta)
    {
        if (TemporarilyDisableSyncedLayerRotation)
        {
            return;
        }

        CacheReferences();
        if (rotationRoot == null || Mathf.Abs(worldRotationDelta) <= Mathf.Epsilon)
        {
            return;
        }

        accumulatedRotationY += worldRotationDelta * currentRotationMultiplier;
        rotationRoot.localRotation = baseLocalRotation * Quaternion.Euler(0f, accumulatedRotationY, 0f);
    }

    public void Tick(float deltaTime, bool useSharedMaterial)
    {
        Vector2 scrollVelocity = currentScrollVelocity;
        if (TemporarilyDisableSyncedLayerRotation)
        {
            if (!TemporarilyAllowFarCloudScroll || layerKind != BackgroundLayerKind.FarClouds)
            {
                return;
            }

            scrollVelocity *= TemporaryFarCloudScrollSpeedScale;
        }

        if (deltaTime <= 0f || scrollVelocity.sqrMagnitude <= 0.000001f)
        {
            return;
        }

        Material material = GetWritableMaterial(useSharedMaterial);
        if (material == null)
        {
            return;
        }

        accumulatedScroll += scrollVelocity * deltaTime;
        ApplyScrollOffset(material);
    }

    private void LateUpdate()
    {
        if (!Application.isPlaying)
        {
            return;
        }

        float patternSpeed = GetAlwaysCloudPatternSpeed();
        if (Mathf.Abs(patternSpeed) <= Mathf.Epsilon)
        {
            return;
        }

        ScrollCloudPattern(Time.unscaledDeltaTime, patternSpeed);
    }

    private float GetAlwaysCloudPatternSpeed()
    {
        return layerKind switch
        {
            BackgroundLayerKind.FarClouds => farCloudPatternDegreesPerSecond,
            BackgroundLayerKind.MidClouds => midCloudPatternDegreesPerSecond,
            _ => 0f
        };
    }

    private void ScrollCloudPattern(float deltaTime, float degreesPerSecond)
    {
        if (deltaTime <= 0f)
        {
            return;
        }

        Material material = GetWritableMaterial(false);
        if (material == null)
        {
            return;
        }

        accumulatedScroll.x += degreesPerSecond / 360f * deltaTime;
        ApplyScrollOffset(material);
    }

    private void Awake()
    {
        CacheReferences();
    }

    private void OnEnable()
    {
        CacheReferences();
    }

    private void OnValidate()
    {
        CacheReferences();
    }

    private void OnDestroy()
    {
        if (!Application.isPlaying || runtimeMaterial == null)
        {
            return;
        }

        Destroy(runtimeMaterial);
    }

    private void CacheReferences()
    {
        if (rotationRoot == null)
        {
            rotationRoot = transform;
        }

        if (targetRenderer == null)
        {
            targetRenderer = GetComponentInChildren<Renderer>(true);
        }

        if (!hasBaseRotation && rotationRoot != null)
        {
            baseLocalRotation = rotationRoot.localRotation;
            hasBaseRotation = true;
        }
    }

    private bool UseSharedMaterial()
    {
        return !Application.isPlaying;
    }

    private Material GetWritableMaterial(bool useSharedMaterial)
    {
        if (targetRenderer == null)
        {
            return null;
        }

        if (useSharedMaterial)
        {
            return targetRenderer.sharedMaterial;
        }

        if (runtimeMaterial == null)
        {
            runtimeMaterial = targetRenderer.material;
        }

        return runtimeMaterial;
    }

    private void ApplyScrollOffset(Material material)
    {
        SetVectorIfPresent(material, ScrollOffsetId, accumulatedScroll);
    }

    private static void SetColorIfPresent(Material material, int propertyId, Color value)
    {
        if (material.HasProperty(propertyId))
        {
            material.SetColor(propertyId, value);
        }
    }

    private static void SetFloatIfPresent(Material material, int propertyId, float value)
    {
        if (material.HasProperty(propertyId))
        {
            material.SetFloat(propertyId, value);
        }
    }

    private static void SetVectorIfPresent(Material material, int propertyId, Vector2 value)
    {
        if (material.HasProperty(propertyId))
        {
            material.SetVector(propertyId, value);
        }
    }
}
