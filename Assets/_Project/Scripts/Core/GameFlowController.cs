using UnityEngine.SceneManagement;

public static class GameFlowController
{
    private const string VehicleSelectScenePath = "Assets/Scenes/VehicleSelectScene.unity";
    private const string CharacterScenePath = "Assets/Scenes/CharacterScene.unity";

    public const string BootSceneName = "Boot";
    public const string IntroSceneName = "IntroScene";
    public const string MainMenuSceneName = "MainMenu";
    public const string StageSelectSceneName = "StageSelectScene";
    public const string StageStepSelectSceneName = "StageStepSelectScene";
    public const string BattleSceneName = "BattleArena";
    public const string VehicleSelectSceneName = "VehicleSelectScene";
    public const string GarageSceneName = VehicleSelectSceneName;
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
        LoadVehicleSelect();
    }

    public static void LoadVehicleSelect()
    {
        LoadAdditiveScene(VehicleSelectScenePath);
    }

    public static void LoadCharacter()
    {
        LoadAdditiveScene(CharacterScenePath);
    }

    public static void CloseGarage()
    {
        CloseVehicleSelect();
    }

    public static void CloseVehicleSelect()
    {
        CloseScene(VehicleSelectScenePath);
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
