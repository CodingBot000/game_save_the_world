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
        rootRect.anchorMin = Vector2.zero;
        rootRect.anchorMax = Vector2.one;
        rootRect.offsetMin = Vector2.zero;
        rootRect.offsetMax = Vector2.zero;

        GameObject backdrop = FindOrCreateUiObject("Backdrop", root.transform);
        Image backdropImage = backdrop.GetComponent<Image>() ?? backdrop.AddComponent<Image>();
        backdropImage.color = new Color(0.08f, 0.11f, 0.16f, 0.92f);
        RectTransform backdropRect = backdrop.GetComponent<RectTransform>();
        backdropRect.anchorMin = Vector2.zero;
        backdropRect.anchorMax = Vector2.one;
        backdropRect.offsetMin = Vector2.zero;
        backdropRect.offsetMax = Vector2.zero;

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

        Button singleButton = CreateButton(
            "SingleButton",
            root.transform,
            runtimeFont,
            "Game Start",
            new Vector2(0f, -300f),
            new Color(0.12f, 0.62f, 0.46f),
            StartSingleBattle);

        Button multiButton = CreateButton(
            "MultiButton",
            root.transform,
            runtimeFont,
            "Start Co-op Placeholder",
            new Vector2(0f, -390f),
            new Color(0.76f, 0.57f, 0.18f),
            StartMultiPlaceholderBattle);

        Text hint = CreateText("Hint", root.transform, runtimeFont, 18, FontStyle.Normal, TextAnchor.MiddleCenter, new Color(0.82f, 0.86f, 0.92f));
        hint.text = "Enter / Space: Stage Select    M: Co-op Stage Select";
        RectTransform hintRect = hint.rectTransform;
        hintRect.anchorMin = new Vector2(0.5f, 0f);
        hintRect.anchorMax = new Vector2(0.5f, 0f);
        hintRect.pivot = new Vector2(0.5f, 0f);
        hintRect.sizeDelta = new Vector2(720f, 28f);
        hintRect.anchoredPosition = new Vector2(0f, 60f);

        singleButton.navigation = Navigation.defaultNavigation;
        multiButton.navigation = Navigation.defaultNavigation;
    }

    private static Button CreateButton(
        string name,
        Transform parent,
        Font font,
        string label,
        Vector2 anchoredPosition,
        Color color,
        UnityEngine.Events.UnityAction onClick)
    {
        GameObject buttonObject = FindOrCreateUiObject(name, parent);
        Image image = buttonObject.GetComponent<Image>() ?? buttonObject.AddComponent<Image>();
        image.color = color;

        Button button = buttonObject.GetComponent<Button>() ?? buttonObject.AddComponent<Button>();
        button.targetGraphic = image;
        button.onClick.RemoveAllListeners();
        button.onClick.AddListener(onClick);

        RectTransform buttonRect = buttonObject.GetComponent<RectTransform>();
        buttonRect.anchorMin = new Vector2(0.5f, 1f);
        buttonRect.anchorMax = new Vector2(0.5f, 1f);
        buttonRect.pivot = new Vector2(0.5f, 1f);
        buttonRect.sizeDelta = new Vector2(360f, 58f);
        buttonRect.anchoredPosition = anchoredPosition;

        Text labelText = CreateText($"{name}Label", buttonObject.transform, font, 22, FontStyle.Bold, TextAnchor.MiddleCenter, Color.white);
        labelText.text = label;
        RectTransform labelRect = labelText.rectTransform;
        labelRect.anchorMin = Vector2.zero;
        labelRect.anchorMax = Vector2.one;
        labelRect.offsetMin = Vector2.zero;
        labelRect.offsetMax = Vector2.zero;

        return button;
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
        Transform existing = parent.Find(name);
        if (existing != null)
        {
            if (existing is RectTransform)
            {
                return existing.gameObject;
            }

            UnityEngine.Object.Destroy(existing.gameObject);
        }

        GameObject created = new GameObject(name, typeof(RectTransform));
        created.transform.SetParent(parent, false);
        return created;
    }
}
