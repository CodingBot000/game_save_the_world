using UnityEngine;

[CreateAssetMenu(menuName = "Titan Slayer/Stages/Boss Definition", fileName = "BossDefinition")]
public sealed class BossDefinition : ScriptableObject
{
    [SerializeField] private string bossId;
    [SerializeField] private string displayName;
    [SerializeField] private GameObject bossPrefab;
    [SerializeField, Min(1f)] private float maxHealth = 2000f;

    public string BossId => bossId;
    public string DisplayName => displayName;
    public GameObject BossPrefab => bossPrefab;
    public float MaxHealth => Mathf.Max(1f, maxHealth);

    public bool IsValid => !string.IsNullOrWhiteSpace(bossId) && bossPrefab != null;
}
