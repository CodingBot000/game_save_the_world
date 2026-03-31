using UnityEngine;

public class CharacterScenePresenter : OverlayScenePresenterBase
{
    protected override string SceneTitle => "Character";

    protected override string SceneDescription =>
        "Character scene placeholder opened on top of MainMenu with additive loading.";

    protected override Color AccentColor => new Color(0.7f, 0.41f, 0.16f, 1f);
}
