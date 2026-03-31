using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

public class MenuPresenter : MonoBehaviour
{
    [SerializeField] private bool autoBuildUi = true;
    [SerializeField] private float inputLockDuration = 0.35f;

    private Canvas canvas;
    private bool uiBuilt;
    private float inputUnlockTime;

    private void Awake()
    {
        ResolveCanvas();
        TryBuildUi();
    }

    private void OnEnable()
    {
        ResolveCanvas();
        TryBuildUi();
        inputUnlockTime = Time.unscaledTime + inputLockDuration;
    }

    private void Start()
    {
        ResolveCanvas();
        TryBuildUi();
        inputUnlockTime = Time.unscaledTime + inputLockDuration;
    }

    private void Update()
    {
        if (Time.unscaledTime < inputUnlockTime)
        {
            return;
        }

        Keyboard keyboard = Keyboard.current;
        if (keyboard == null)
        {
            return;
        }

        if (keyboard.enterKey.wasPressedThisFrame || keyboard.numpadEnterKey.wasPressedThisFrame || keyboard.spaceKey.wasPressedThisFrame)
        {
            StartSingleBattle();
        }

        if (keyboard.mKey.wasPressedThisFrame)
        {
            StartMultiPlaceholderBattle();
        }
    }

    public void StartSingleBattle()
    {
        GameFlowController.LoadStageSelect(GameMode.Single);
    }

    public void StartMultiPlaceholderBattle()
    {
        GameFlowController.LoadStageSelect(GameMode.MultiPlaceholder);
    }

    public void OpenGarage()
    {
        GameFlowController.LoadGarage();
    }

    public void OpenCharacter()
    {
        GameFlowController.LoadCharacter();
    }

    public void OpenPlaceholder(string featureName)
    {
        Debug.Log($"{featureName} is not implemented yet.");
    }

    private void ResolveCanvas()
    {
        canvas = GetComponent<Canvas>();
        if (canvas == null)
        {
            canvas = GetComponentInParent<Canvas>();
        }
    }

    private void TryBuildUi()
    {
        if (uiBuilt || !autoBuildUi || canvas == null)
        {
            return;
        }

        BuildUi();
        uiBuilt = true;
    }

    private void BuildUi()
    {
        Font runtimeFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        GameObject root = FindOrCreateUiObject("GeneratedMainMenu", canvas.transform);
        RectTransform rootRect = root.GetComponent<RectTransform>();
        SimpleUiFactory.StretchFull(rootRect);

        GameObject backdrop = FindOrCreateUiObject("Backdrop", root.transform);
        Image backdropImage = backdrop.GetComponent<Image>() ?? backdrop.AddComponent<Image>();
        backdropImage.color = new Color(0.08f, 0.11f, 0.16f, 0.92f);
        RectTransform backdropRect = backdrop.GetComponent<RectTransform>();
        SimpleUiFactory.StretchFull(backdropRect);

        Image rightTopPanel = SimpleUiFactory.CreateImage("RightTopPanel", root.transform, new Color(0.07f, 0.1f, 0.15f, 0.68f));
        SimpleUiFactory.SetAnchoredLayout(
            rightTopPanel.rectTransform,
            new Vector2(1f, 1f),
            new Vector2(1f, 1f),
            new Vector2(1f, 1f),
            new Vector2(330f, 320f),
            new Vector2(-36f, -36f));

        Text title = CreateText("Title", root.transform, runtimeFont, 42, FontStyle.Bold, TextAnchor.MiddleCenter, Color.white);
        title.text = "Titan Destroyer";
        RectTransform titleRect = title.rectTransform;
        titleRect.anchorMin = new Vector2(0.5f, 1f);
        titleRect.anchorMax = new Vector2(0.5f, 1f);
        titleRect.pivot = new Vector2(0.5f, 1f);
        titleRect.sizeDelta = new Vector2(900f, 64f);
        titleRect.anchoredPosition = new Vector2(0f, -120f);

        Text subtitle = CreateText("Subtitle", root.transform, runtimeFont, 20, FontStyle.Normal, TextAnchor.MiddleCenter, new Color(0.8f, 0.88f, 0.95f));
        subtitle.text = "Orbit the titan, keep distance, and burn down the boss core.";
        RectTransform subtitleRect = subtitle.rectTransform;
        subtitleRect.anchorMin = new Vector2(0.5f, 1f);
        subtitleRect.anchorMax = new Vector2(0.5f, 1f);
        subtitleRect.pivot = new Vector2(0.5f, 1f);
        subtitleRect.sizeDelta = new Vector2(960f, 32f);
        subtitleRect.anchoredPosition = new Vector2(0f, -180f);

        List<Button> buttons = new List<Button>();

        Button singleButton = CreateButton(
            "SingleButton",
            root.transform,
            runtimeFont,
            "Game Start",
            new Color(0.12f, 0.62f, 0.46f),
            StartSingleBattle);
        SimpleUiFactory.SetAnchoredLayout(
            singleButton.GetComponent<RectTransform>(),
            new Vector2(0.5f, 0.5f),
            new Vector2(0.5f, 0.5f),
            new Vector2(0.5f, 0.5f),
            new Vector2(420f, 72f),
            new Vector2(0f, 0f));
        buttons.Add(singleButton);

        Button garageButton = CreateButton(
            "GarageButton",
            root.transform,
            runtimeFont,
            "Garage",
            new Color(0.17f, 0.55f, 0.92f),
            OpenGarage);
        SimpleUiFactory.SetAnchoredLayout(
            garageButton.GetComponent<RectTransform>(),
            new Vector2(1f, 0f),
            new Vector2(1f, 0f),
            new Vector2(1f, 0f),
            new Vector2(240f, 56f),
            new Vector2(-44f, 120f));
        buttons.Add(garageButton);

        Button characterButton = CreateButton(
            "CharacterButton",
            root.transform,
            runtimeFont,
            "Pilot",
            new Color(0.7f, 0.41f, 0.16f),
            OpenCharacter);
        SimpleUiFactory.SetAnchoredLayout(
            characterButton.GetComponent<RectTransform>(),
            new Vector2(1f, 0f),
            new Vector2(1f, 0f),
            new Vector2(1f, 0f),
            new Vector2(240f, 56f),
            new Vector2(-44f, 50f));
        buttons.Add(characterButton);

        Button recordButton = CreateButton(
            "RecordButton",
            root.transform,
            runtimeFont,
            "Record",
            new Color(0.24f, 0.34f, 0.5f),
            () => OpenPlaceholder("Record"));
        SimpleUiFactory.SetAnchoredLayout(
            recordButton.GetComponent<RectTransform>(),
            new Vector2(0f, 0f),
            new Vector2(0f, 0f),
            new Vector2(0f, 0f),
            new Vector2(240f, 56f),
            new Vector2(44f, 120f));
        buttons.Add(recordButton);

        Button leaderBoardButton = CreateButton(
            "LeaderBoardButton",
            root.transform,
            runtimeFont,
            "LeaderBoard",
            new Color(0.31f, 0.32f, 0.52f),
            () => OpenPlaceholder("LeaderBoard"));
        SimpleUiFactory.SetAnchoredLayout(
            leaderBoardButton.GetComponent<RectTransform>(),
            new Vector2(0f, 0f),
            new Vector2(0f, 0f),
            new Vector2(0f, 0f),
            new Vector2(240f, 56f),
            new Vector2(44f, 50f));
        buttons.Add(leaderBoardButton);

        Button fuelButton = CreateButton(
            "FuelButton",
            rightTopPanel.transform,
            runtimeFont,
            "Fuel: 20",
            new Color(0.2f, 0.46f, 0.34f),
            () => OpenPlaceholder("Fuel"));
        SimpleUiFactory.SetAnchoredLayout(
            fuelButton.GetComponent<RectTransform>(),
            new Vector2(0.5f, 1f),
            new Vector2(0.5f, 1f),
            new Vector2(0.5f, 1f),
            new Vector2(270f, 52f),
            new Vector2(0f, -50f));
        buttons.Add(fuelButton);

        Button currencyButton = CreateButton(
            "CurrencyButton",
            rightTopPanel.transform,
            runtimeFont,
            "Gold / Premium: 3k",
            new Color(0.55f, 0.45f, 0.14f),
            () => OpenPlaceholder("Currency"),
            18);
        SimpleUiFactory.SetAnchoredLayout(
            currencyButton.GetComponent<RectTransform>(),
            new Vector2(0.5f, 1f),
            new Vector2(0.5f, 1f),
            new Vector2(0.5f, 1f),
            new Vector2(270f, 52f),
            new Vector2(0f, -114f));
        buttons.Add(currencyButton);

        Button alertButton = CreateButton(
            "AlertButton",
            rightTopPanel.transform,
            runtimeFont,
            "Alerts",
            new Color(0.6f, 0.23f, 0.18f),
            () => OpenPlaceholder("Alerts"));
        SimpleUiFactory.SetAnchoredLayout(
            alertButton.GetComponent<RectTransform>(),
            new Vector2(0.5f, 1f),
            new Vector2(0.5f, 1f),
            new Vector2(0.5f, 1f),
            new Vector2(270f, 52f),
            new Vector2(0f, -178f));
        buttons.Add(alertButton);

        Button settingsButton = CreateButton(
            "SettingsButton",
            rightTopPanel.transform,
            runtimeFont,
            "Settings",
            new Color(0.22f, 0.29f, 0.4f),
            () => OpenPlaceholder("Settings"));
        SimpleUiFactory.SetAnchoredLayout(
            settingsButton.GetComponent<RectTransform>(),
            new Vector2(0.5f, 1f),
            new Vector2(0.5f, 1f),
            new Vector2(0.5f, 1f),
            new Vector2(270f, 52f),
            new Vector2(0f, -242f));
        buttons.Add(settingsButton);

        Text hint = CreateText("Hint", root.transform, runtimeFont, 18, FontStyle.Normal, TextAnchor.MiddleCenter, new Color(0.82f, 0.86f, 0.92f));
        hint.text = "Enter / Space: Stage Select";
        RectTransform hintRect = hint.rectTransform;
        hintRect.anchorMin = new Vector2(0.5f, 0f);
        hintRect.anchorMax = new Vector2(0.5f, 0f);
        hintRect.pivot = new Vector2(0.5f, 0f);
        hintRect.sizeDelta = new Vector2(720f, 28f);
        hintRect.anchoredPosition = new Vector2(0f, 60f);

        foreach (Button button in buttons)
        {
            button.navigation = Navigation.defaultNavigation;
        }
    }

    private static Button CreateButton(
        string name,
        Transform parent,
        Font font,
        string label,
        Color color,
        UnityEngine.Events.UnityAction onClick,
        int fontSize = 22)
    {
        return SimpleUiFactory.CreateButton(name, parent, font, label, color, onClick, fontSize);
    }

    private static Text CreateText(
        string name,
        Transform parent,
        Font font,
        int fontSize,
        FontStyle fontStyle,
        TextAnchor alignment,
        Color color)
    {
        GameObject textObject = FindOrCreateUiObject(name, parent);
        Text text = textObject.GetComponent<Text>() ?? textObject.AddComponent<Text>();
        text.font = font;
        text.fontSize = fontSize;
        text.fontStyle = fontStyle;
        text.alignment = alignment;
        text.color = color;
        text.horizontalOverflow = HorizontalWrapMode.Overflow;
        text.verticalOverflow = VerticalWrapMode.Overflow;
        return text;
    }

    private static GameObject FindOrCreateUiObject(string name, Transform parent)
    {
        return SimpleUiFactory.FindOrCreateUiObject(name, parent);
    }
}
