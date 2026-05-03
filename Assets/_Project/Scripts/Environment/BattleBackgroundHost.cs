using UnityEngine;
using UnityEngine.SceneManagement;

[DisallowMultipleComponent]
[DefaultExecutionOrder(150)]
public sealed class BattleBackgroundHost : MonoBehaviour
{
    private const string DefaultStageRotationSourceName = "StageVisualRoot";

    [Header("Implementation")]
    [SerializeField] private EnvironmentBackgroundController environmentBackground;
    [SerializeField] private bool autoFindEnvironmentBackground = true;

    [Header("Rotation Binding")]
    [SerializeField] private Transform stageRotationSource;
    [SerializeField] private bool autoFindStageRotationSource = true;
    [SerializeField] private string stageRotationSourceName = DefaultStageRotationSourceName;
    [SerializeField] private bool bindStageRotationToParallax = true;

    [Header("Theme")]
    [SerializeField] private EnvironmentThemeType defaultTheme = EnvironmentThemeType.Day;
    [SerializeField] private bool applyDefaultThemeOnEnable = true;

    public EnvironmentBackgroundController EnvironmentBackground => environmentBackground;

    private void Reset()
    {
        ResolveEnvironmentBackground();
        ResolveStageRotationSource();
        ApplyConfiguration();
    }

    private void Awake()
    {
        ResolveEnvironmentBackground();
        ResolveStageRotationSource();
    }

    private void OnEnable()
    {
        ResolveEnvironmentBackground();
        ResolveStageRotationSource();
        ApplyConfiguration();
    }

    private void OnValidate()
    {
        ResolveEnvironmentBackground();
        ResolveStageRotationSource();

        if (!Application.isPlaying)
        {
            ApplyConfiguration();
        }
    }

    public void BindStageRotationSource(Transform source)
    {
        stageRotationSource = source;
        ApplyConfiguration();
    }

    public void SetDefaultTheme(EnvironmentThemeType themeType, bool applyNow = true)
    {
        defaultTheme = themeType;

        if (applyNow)
        {
            ApplyConfiguration();
        }
    }

    private void ApplyConfiguration()
    {
        if (environmentBackground == null)
        {
            return;
        }

        environmentBackground.RefreshChildReferences();

        if (bindStageRotationToParallax && stageRotationSource != null)
        {
            environmentBackground.UseTransformYawReference(stageRotationSource);
        }

        if (applyDefaultThemeOnEnable)
        {
            environmentBackground.SetTheme(defaultTheme);
        }
    }

    private void ResolveEnvironmentBackground()
    {
        if (!autoFindEnvironmentBackground && environmentBackground != null)
        {
            return;
        }

        environmentBackground ??= GetComponent<EnvironmentBackgroundController>();
        environmentBackground ??= GetComponentInChildren<EnvironmentBackgroundController>(true);
    }

    private void ResolveStageRotationSource()
    {
        if (!autoFindStageRotationSource || stageRotationSource != null || string.IsNullOrWhiteSpace(stageRotationSourceName))
        {
            return;
        }

        stageRotationSource = FindSceneTransform(stageRotationSourceName);
    }

    private Transform FindSceneTransform(string objectName)
    {
        Scene scene = gameObject.scene;
        if (!scene.IsValid())
        {
            return null;
        }

        foreach (GameObject root in scene.GetRootGameObjects())
        {
            Transform[] descendants = root.GetComponentsInChildren<Transform>(true);
            for (int i = 0; i < descendants.Length; i++)
            {
                if (descendants[i].name == objectName)
                {
                    return descendants[i];
                }
            }
        }

        return null;
    }
}
