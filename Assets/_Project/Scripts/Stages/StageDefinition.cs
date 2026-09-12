using UnityEngine;

[CreateAssetMenu(menuName = "Titan Slayer/Stages/Stage Definition", fileName = "StageDefinition")]
public sealed class StageDefinition : ScriptableObject
{
    [SerializeField] private string stageId;
    [SerializeField] private string displayName;
    [SerializeField] private BossDefinition bossDefinition;
    [SerializeField] private GameObject environmentPrefab;
    [SerializeField] private EnvironmentThemeType environmentTheme = EnvironmentThemeType.Day;

    public string StageId => stageId;
    public string DisplayName => displayName;
    public BossDefinition BossDefinition => bossDefinition;
    public GameObject EnvironmentPrefab => environmentPrefab;
    public EnvironmentThemeType EnvironmentTheme => environmentTheme;

    public bool IsValid =>
        !string.IsNullOrWhiteSpace(stageId) &&
        bossDefinition != null &&
        bossDefinition.IsValid &&
        environmentPrefab != null;
}
