using UnityEngine;
using UnityEngine.UI;

[DefaultExecutionOrder(550)]
public class EnvironmentThemeDebugPanel : MonoBehaviour
{
    [Header("Debug UI")]
    [SerializeField] private bool autoBuildUi = true;
    [SerializeField] private bool showDebugPanel = true;
    [SerializeField] private Vector2 panelAnchoredPosition = new(-24f, -24f);

    [Header("Scene References")]
    [SerializeField] private Canvas targetCanvas;
    [SerializeField] private EnvironmentBackgroundController environmentController;

    private GameObject panelRoot;
    private Text currentThemeText;
    private Image dayButtonImage;
    private Image nightButtonImage;
    private Image rainButtonImage;
    private bool uiBuilt;

    private void Awake()
    {
        ResolveReferences();
        TryBuildUi();
    }

    private void Reset()
    {
        ResolveReferences();
    }

    private void OnEnable()
    {
        ResolveReferences();
        TryBuildUi();
        RefreshUiState();
    }

    private void Start()
    {
        ResolveReferences();
        TryBuildUi();
        RefreshUiState();
    }

    private void OnValidate()
    {
        ResolveReferences();
    }

    private void Update()
    {
        ResolveReferences();
        RefreshUiState();
    }

    public void SetDayTheme()
    {
        if (environmentController == null)
        {
            return;
        }

        environmentController.SetDayTheme();
        RefreshUiState();
    }

    public void SetNightTheme()
    {
        if (environmentController == null)
        {
            return;
        }

        environmentController.SetNightTheme();
        RefreshUiState();
    }

    public void SetRainTheme()
    {
        if (environmentController == null)
        {
            return;
        }

        environmentController.SetRainTheme();
        RefreshUiState();
    }

    private void ResolveReferences()
    {
        if (targetCanvas == null)
        {
            targetCanvas = GetComponent<Canvas>();
        }

        if (targetCanvas == null)
        {
            targetCanvas = GetComponentInParent<Canvas>();
        }

        if (environmentController == null)
        {
            environmentController = FindAnyObjectByType<EnvironmentBackgroundController>();
        }
    }

    private void TryBuildUi()
    {
        if (!showDebugPanel)
        {
            if (panelRoot != null)
            {
                panelRoot.SetActive(false);
            }

            return;
        }

        if (uiBuilt || !autoBuildUi || targetCanvas == null)
        {
            return;
        }

        BuildUi();
        uiBuilt = true;
    }

    private void BuildUi()
    {
        Font runtimeFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

        panelRoot = FindOrCreateUiObject("GeneratedEnvironmentThemeDebug", targetCanvas.transform);
        RectTransform panelRect = panelRoot.GetComponent<RectTransform>();
        panelRect.anchorMin = new Vector2(1f, 1f);
        panelRect.anchorMax = new Vector2(1f, 1f);
        panelRect.pivot = new Vector2(1f, 1f);
        panelRect.sizeDelta = new Vector2(308f, 132f);
        panelRect.anchoredPosition = panelAnchoredPosition;

        Image panelBackground = panelRoot.GetComponent<Image>() ?? panelRoot.AddComponent<Image>();
        panelBackground.color = new Color(0.07f, 0.10f, 0.15f, 0.84f);

        Text titleText = CreateText(
            "TitleLabel",
            panelRoot.transform,
            runtimeFont,
            18,
            FontStyle.Bold,
            TextAnchor.UpperLeft,
            new Color(0.94f, 0.97f, 1f));
        titleText.text = "Environment Theme";
        RectTransform titleRect = titleText.rectTransform;
        titleRect.anchorMin = new Vector2(0f, 1f);
        titleRect.anchorMax = new Vector2(1f, 1f);
        titleRect.pivot = new Vector2(0f, 1f);
        titleRect.offsetMin = new Vector2(14f, -34f);
        titleRect.offsetMax = new Vector2(-14f, -10f);

        currentThemeText = CreateText(
            "CurrentThemeLabel",
            panelRoot.transform,
            runtimeFont,
            16,
            FontStyle.Normal,
            TextAnchor.UpperLeft,
            new Color(0.76f, 0.84f, 0.93f));
        RectTransform currentThemeRect = currentThemeText.rectTransform;
        currentThemeRect.anchorMin = new Vector2(0f, 1f);
        currentThemeRect.anchorMax = new Vector2(1f, 1f);
        currentThemeRect.pivot = new Vector2(0f, 1f);
        currentThemeRect.offsetMin = new Vector2(14f, -60f);
        currentThemeRect.offsetMax = new Vector2(-14f, -36f);

        CreateButton(
            "DayButton",
            panelRoot.transform,
            runtimeFont,
            "Day",
            new Vector2(56f, -90f),
            new Color(0.24f, 0.60f, 0.80f),
            SetDayTheme,
            out dayButtonImage);

        CreateButton(
            "NightButton",
            panelRoot.transform,
            runtimeFont,
            "Night",
            new Vector2(154f, -90f),
            new Color(0.22f, 0.26f, 0.42f),
            SetNightTheme,
            out nightButtonImage);

        CreateButton(
            "RainButton",
            panelRoot.transform,
            runtimeFont,
            "Rain",
            new Vector2(252f, -90f),
            new Color(0.28f, 0.42f, 0.52f),
            SetRainTheme,
            out rainButtonImage);
    }

    private void RefreshUiState()
    {
        if (panelRoot != null)
        {
            panelRoot.SetActive(showDebugPanel);
        }

        if (!showDebugPanel)
        {
            return;
        }

        if (currentThemeText != null)
        {
            currentThemeText.text = environmentController != null
                ? $"Current: {environmentController.ActiveThemeType}"
                : "Current: Missing Controller";
        }

        if (environmentController == null)
        {
            return;
        }

        ApplyButtonState(dayButtonImage, environmentController.ActiveThemeType == EnvironmentThemeType.Day, new Color(0.24f, 0.60f, 0.80f));
        ApplyButtonState(nightButtonImage, environmentController.ActiveThemeType == EnvironmentThemeType.Night, new Color(0.22f, 0.26f, 0.42f));
        ApplyButtonState(rainButtonImage, environmentController.ActiveThemeType == EnvironmentThemeType.Rain, new Color(0.28f, 0.42f, 0.52f));
    }

    private static void ApplyButtonState(Image image, bool isActive, Color baseColor)
    {
        if (image == null)
        {
            return;
        }

        image.color = isActive
            ? Color.Lerp(baseColor, Color.white, 0.22f)
            : baseColor;
    }

    private static void CreateButton(
        string name,
        Transform parent,
        Font font,
        string label,
        Vector2 anchoredPosition,
        Color color,
        UnityEngine.Events.UnityAction onClick,
        out Image buttonImage)
    {
        GameObject buttonObject = FindOrCreateUiObject(name, parent);
        buttonImage = buttonObject.GetComponent<Image>() ?? buttonObject.AddComponent<Image>();
        buttonImage.color = color;

        Button button = buttonObject.GetComponent<Button>() ?? buttonObject.AddComponent<Button>();
        button.targetGraphic = buttonImage;
        button.onClick.RemoveAllListeners();
        button.onClick.AddListener(onClick);

        RectTransform buttonRect = buttonObject.GetComponent<RectTransform>();
        buttonRect.anchorMin = new Vector2(0f, 1f);
        buttonRect.anchorMax = new Vector2(0f, 1f);
        buttonRect.pivot = new Vector2(0.5f, 0.5f);
        buttonRect.sizeDelta = new Vector2(86f, 34f);
        buttonRect.anchoredPosition = anchoredPosition;

        Text labelText = CreateText(
            $"{name}Label",
            buttonObject.transform,
            font,
            15,
            FontStyle.Bold,
            TextAnchor.MiddleCenter,
            Color.white);
        labelText.text = label;
        RectTransform labelRect = labelText.rectTransform;
        labelRect.anchorMin = Vector2.zero;
        labelRect.anchorMax = Vector2.one;
        labelRect.offsetMin = Vector2.zero;
        labelRect.offsetMax = Vector2.zero;
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

            Object.Destroy(existing.gameObject);
        }

        GameObject created = new(name, typeof(RectTransform));
        created.transform.SetParent(parent, false);
        return created;
    }
}
