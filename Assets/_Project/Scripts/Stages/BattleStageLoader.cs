using UnityEngine;
using UnityEngine.SceneManagement;

[DisallowMultipleComponent]
[DefaultExecutionOrder(100)]
public sealed class BattleStageLoader : MonoBehaviour
{
    [SerializeField] private StageCatalog stageCatalog;
    [SerializeField] private Transform bossSpawnPoint;
    [SerializeField] private Transform environmentSpawnPoint;

    private GameObject activeBossObject;
    private GameObject activeEnvironmentObject;

    public StageDefinition ActiveStageDefinition { get; private set; }
    public BossDefinition ActiveBossDefinition => ActiveStageDefinition != null
        ? ActiveStageDefinition.BossDefinition
        : null;
    public GameObject ActiveBossObject => activeBossObject;
    public GameObject ActiveEnvironmentObject => activeEnvironmentObject;

    private void Awake()
    {
        LoadSelectedStage();
    }

    public void LoadSelectedStage()
    {
        if (stageCatalog == null)
        {
            Debug.LogError("BattleStageLoader requires a StageCatalog.", this);
            enabled = false;
            return;
        }

        if (!stageCatalog.Validate(out string catalogError))
        {
            Debug.LogError(catalogError, stageCatalog);
            enabled = false;
            return;
        }

        string selectedStageId = StageSelectionState.Instance.SelectedStageId;
        ActiveStageDefinition = stageCatalog.Resolve(selectedStageId, out bool usedFallback);
        if (ActiveStageDefinition == null)
        {
            Debug.LogError("Stage catalog could not resolve a stage or fallback.", stageCatalog);
            enabled = false;
            return;
        }

        if (usedFallback && !string.IsNullOrWhiteSpace(selectedStageId))
        {
            Debug.LogWarning(
                $"Unknown stage id '{selectedStageId}'. Loading default stage '{ActiveStageDefinition.StageId}'.",
                this);
        }

        ClearSpawnedContent();
        activeBossObject = Instantiate(
            ActiveBossDefinition.BossPrefab,
            bossSpawnPoint != null ? bossSpawnPoint : transform);
        activeBossObject.name = ActiveBossDefinition.BossPrefab.name;

        activeEnvironmentObject = Instantiate(
            ActiveStageDefinition.EnvironmentPrefab,
            environmentSpawnPoint != null ? environmentSpawnPoint : transform);
        activeEnvironmentObject.name = ActiveStageDefinition.EnvironmentPrefab.name;

        BossController[] bosses = FindSceneBosses();
        if (bosses.Length != 1)
        {
            Debug.LogError(
                $"Stage '{ActiveStageDefinition.StageId}' must contain exactly one BossController, found {bosses.Length}.",
                this);
            enabled = false;
            return;
        }

        BattleBackgroundHost backgroundHost = FindSceneComponent<BattleBackgroundHost>();
        backgroundHost?.SetDefaultTheme(ActiveStageDefinition.EnvironmentTheme, false);

        Debug.Log(
            $"Loaded stage '{ActiveStageDefinition.StageId}' with boss '{ActiveBossDefinition.BossId}'.",
            this);
    }

    private void ClearSpawnedContent()
    {
        DestroySpawnedObject(activeBossObject);
        DestroySpawnedObject(activeEnvironmentObject);
        activeBossObject = null;
        activeEnvironmentObject = null;
    }

    private static void DestroySpawnedObject(GameObject target)
    {
        if (target == null)
        {
            return;
        }

        if (Application.isPlaying)
        {
            Destroy(target);
        }
        else
        {
            DestroyImmediate(target);
        }
    }

    private BossController[] FindSceneBosses()
    {
        Scene scene = gameObject.scene;
        if (!scene.IsValid())
        {
            return System.Array.Empty<BossController>();
        }

        System.Collections.Generic.List<BossController> result = new();
        foreach (GameObject root in scene.GetRootGameObjects())
        {
            result.AddRange(root.GetComponentsInChildren<BossController>(true));
        }

        return result.ToArray();
    }

    private T FindSceneComponent<T>() where T : Component
    {
        Scene scene = gameObject.scene;
        if (!scene.IsValid())
        {
            return null;
        }

        foreach (GameObject root in scene.GetRootGameObjects())
        {
            T component = root.GetComponentInChildren<T>(true);
            if (component != null)
            {
                return component;
            }
        }

        return null;
    }
}
