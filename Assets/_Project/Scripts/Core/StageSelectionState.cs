using System;
using UnityEngine;

public enum StageDifficulty
{
    Easy = 0,
    Normal = 1,
    Expert = 2,
    Nightmare = 3
}

public class StageSelectionState : MonoBehaviour
{
    private const string RuntimeObjectName = "StageSelectionState";
    private static StageSelectionState instance;

    [SerializeField] private string selectedStageId;
    [SerializeField] private string selectedStageName;
    [SerializeField] private StageDifficulty selectedDifficulty = StageDifficulty.Easy;

    public event Action SelectionChanged;

    public static StageSelectionState Instance => EnsureInitialized();
    public string SelectedStageId => selectedStageId;
    public string SelectedStageName => selectedStageName;
    public StageDifficulty SelectedDifficulty => selectedDifficulty;
    public int SelectedDifficultyNumber => (int)selectedDifficulty + 1;
    public string SelectedDifficultyName => GetDifficultyName(selectedDifficulty);

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Bootstrap()
    {
        EnsureInitialized();
    }

    public static StageSelectionState EnsureInitialized()
    {
        if (instance != null)
        {
            return instance;
        }

        instance = FindAnyObjectByType<StageSelectionState>();
        if (instance != null)
        {
            return instance;
        }

        GameObject runtimeObject = new GameObject(RuntimeObjectName);
        instance = runtimeObject.AddComponent<StageSelectionState>();
        DontDestroyOnLoad(runtimeObject);
        return instance;
    }

    public void SetSelection(string stageId, string stageName, int difficultyIndex)
    {
        SetSelection(stageId, stageName, FromDifficultyIndex(difficultyIndex));
    }

    public void SetSelection(string stageId, string stageName, StageDifficulty difficulty)
    {
        string nextStageId = string.IsNullOrWhiteSpace(stageId) ? string.Empty : stageId.Trim();
        string nextStageName = string.IsNullOrWhiteSpace(stageName) ? string.Empty : stageName.Trim();
        StageDifficulty nextDifficulty = ClampDifficulty(difficulty);

        if (selectedStageId == nextStageId &&
            selectedStageName == nextStageName &&
            selectedDifficulty == nextDifficulty)
        {
            return;
        }

        selectedStageId = nextStageId;
        selectedStageName = nextStageName;
        selectedDifficulty = nextDifficulty;
        SelectionChanged?.Invoke();
    }

    public static StageDifficulty FromDifficultyIndex(int difficultyIndex)
    {
        return ClampDifficulty((StageDifficulty)Mathf.Clamp(difficultyIndex, 0, 3));
    }

    public static StageDifficulty ClampDifficulty(StageDifficulty difficulty)
    {
        return (StageDifficulty)Mathf.Clamp((int)difficulty, 0, 3);
    }

    public static string GetDifficultyName(StageDifficulty difficulty)
    {
        return ClampDifficulty(difficulty) switch
        {
            StageDifficulty.Easy => "Easy",
            StageDifficulty.Normal => "Normal",
            StageDifficulty.Expert => "Expert",
            StageDifficulty.Nightmare => "Nightmare",
            _ => "Easy"
        };
    }

    private void Awake()
    {
        if (instance != null && instance != this)
        {
            Destroy(gameObject);
            return;
        }

        instance = this;
        DontDestroyOnLoad(gameObject);
    }
}
