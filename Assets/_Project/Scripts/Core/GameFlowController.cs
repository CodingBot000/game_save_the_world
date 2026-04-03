using UnityEngine.SceneManagement;

public static class GameFlowController
{
    private const string GarageScenePath = "Assets/Scenes/GarageScene.unity";
    private const string CharacterScenePath = "Assets/Scenes/CharacterScene.unity";

    public const string BootSceneName = "Boot";
    public const string IntroSceneName = "IntroScene";
    public const string MainMenuSceneName = "MainMenu";
    public const string StageSelectSceneName = "StageSelectScene";
    public const string StageStepSelectSceneName = "StageStepSelectScene";
    public const string BattleSceneName = "BattleArena";
    public const string GarageSceneName = "GarageScene";
    public const string CharacterSceneName = "CharacterScene";

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

    public static void LoadStageStepSelect(GameMode mode)
    {
        CurrentMode = mode;
        SceneManager.LoadScene(StageStepSelectSceneName);
    }

    public static void StartBattle(GameMode mode)
    {
        CurrentMode = mode;
        SceneManager.LoadScene(BattleSceneName);
    }

    public static void LoadGarage()
    {
        LoadAdditiveScene(GarageScenePath);
    }

    public static void LoadCharacter()
    {
        LoadAdditiveScene(CharacterScenePath);
    }

    public static void CloseGarage()
    {
        CloseScene(GarageScenePath);
    }

    public static void CloseCharacter()
    {
        CloseScene(CharacterScenePath);
    }

    private static void LoadAdditiveScene(string scenePath)
    {
        if (SceneManager.GetSceneByPath(scenePath).isLoaded)
        {
            return;
        }

        SceneManager.LoadScene(scenePath, LoadSceneMode.Additive);
    }

    private static void CloseScene(string scenePath)
    {
        Scene scene = SceneManager.GetSceneByPath(scenePath);
        if (!scene.isLoaded)
        {
            return;
        }

        SceneManager.UnloadSceneAsync(scene);
    }
}
