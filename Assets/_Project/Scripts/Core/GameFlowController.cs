using UnityEngine.SceneManagement;

public static class GameFlowController
{
    public const string BootSceneName = "Boot";
    public const string IntroSceneName = "IntroScene";
    public const string MainMenuSceneName = "MainMenu";
    public const string StageSelectSceneName = "StageSelectScene";
    public const string BattleSceneName = "BattleArena";

    public static GameMode CurrentMode { get; private set; } = GameMode.Single;

    public static void SetMode(GameMode mode)
    {
        CurrentMode = mode;
    }

    public static void LoadMainMenu()
    {
        SceneManager.LoadScene(MainMenuSceneName);
    }

    public static void LoadIntro()
    {
        SceneManager.LoadScene(IntroSceneName);
    }

    public static void LoadStageSelect(GameMode mode)
    {
        CurrentMode = mode;
        SceneManager.LoadScene(StageSelectSceneName);
    }

    public static void StartBattle(GameMode mode)
    {
        CurrentMode = mode;
        SceneManager.LoadScene(BattleSceneName);
    }
}
