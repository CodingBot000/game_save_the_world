using UnityEngine;

[DefaultExecutionOrder(650)]
public class BattleEventDialogueHealthTrigger : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private BossController bossController;
    [SerializeField] private BattleEventDialogueView dialogueView;

    [Header("Thresholds")]
    [SerializeField, Range(0f, 1f)] private float angryThreshold = 2f / 3f;
    [SerializeField, Range(0f, 1f)] private float normalThreshold = 1f / 3f;
    [SerializeField, Min(0.1f)] private float showDuration = 3f;

    private bool angryShown;
    private bool normalShown;

    private void Awake()
    {
        ResolveReferences();
    }

    private void Reset()
    {
        ResolveReferences();
    }

    private void OnValidate()
    {
        ResolveReferences();
    }

    private void Start()
    {
        ResolveReferences();
    }

    private void Update()
    {
        ResolveReferences();

        if (bossController == null || dialogueView == null || !bossController.IsAlive)
        {
            return;
        }

        float healthRatio = Mathf.Clamp01(bossController.HealthRatio);

        if (!angryShown && healthRatio <= angryThreshold)
        {
            angryShown = true;
            dialogueView.ShowAngry(showDuration);
        }

        if (!normalShown && healthRatio <= normalThreshold)
        {
            normalShown = true;
            dialogueView.ShowNormal(showDuration);
        }
    }

    private void ResolveReferences()
    {
        if (bossController == null)
        {
            bossController = FindAnyObjectByType<BossController>();
        }

        if (dialogueView == null)
        {
            dialogueView = GetComponent<BattleEventDialogueView>();
        }

        if (dialogueView == null)
        {
            dialogueView = FindAnyObjectByType<BattleEventDialogueView>();
        }
    }
}
