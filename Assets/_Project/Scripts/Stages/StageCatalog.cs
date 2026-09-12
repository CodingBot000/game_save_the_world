using System;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(menuName = "Titan Slayer/Stages/Stage Catalog", fileName = "StageCatalog")]
public sealed class StageCatalog : ScriptableObject
{
    [SerializeField] private StageDefinition defaultStage;
    [SerializeField] private List<StageDefinition> stages = new();

    public StageDefinition DefaultStage => defaultStage;
    public IReadOnlyList<StageDefinition> Stages => stages;

    public StageDefinition Resolve(string stageId, out bool usedFallback)
    {
        if (!string.IsNullOrWhiteSpace(stageId))
        {
            string normalizedId = stageId.Trim();
            for (int i = 0; i < stages.Count; i++)
            {
                StageDefinition candidate = stages[i];
                if (candidate != null &&
                    string.Equals(candidate.StageId, normalizedId, StringComparison.Ordinal))
                {
                    usedFallback = false;
                    return candidate;
                }
            }
        }

        usedFallback = true;
        return defaultStage;
    }

    public bool Validate(out string error)
    {
        if (defaultStage == null || !defaultStage.IsValid)
        {
            error = "Stage catalog has no valid default stage.";
            return false;
        }

        HashSet<string> ids = new(StringComparer.Ordinal);
        for (int i = 0; i < stages.Count; i++)
        {
            StageDefinition stage = stages[i];
            if (stage == null || !stage.IsValid)
            {
                error = $"Stage catalog entry {i} is missing or invalid.";
                return false;
            }

            if (!ids.Add(stage.StageId))
            {
                error = $"Stage catalog contains duplicate stage id '{stage.StageId}'.";
                return false;
            }
        }

        error = string.Empty;
        return true;
    }
}
