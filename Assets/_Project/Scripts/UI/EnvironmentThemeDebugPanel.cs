using UnityEngine;
using UnityEngine.UI;
#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
#endif

[DefaultExecutionOrder(550)]
public class EnvironmentThemeDebugPanel : MonoBehaviour
{
    private const string PanelRootName = "GeneratedEnvironmentThemeDebug";

    [Header("Debug UI")]
    [SerializeField] private bool showDebugPanel = true;
    [SerializeField] private Vector2 panelAnchoredPosition = new(28f, 96f);

    [Header("Scene References")]
    [SerializeField] private Canvas targetCanvas;
    [SerializeField] private EnvironmentBackgroundController environmentController;
    [SerializeField] private MoonOrbitController worldRotationController;

    private GameObject panelRoot;
    private Text currentThemeText;
    private Button dayButton;
    private Button nightButton;
    private Button rainButton;
    private Button undeadButton;
    private Button rotateButton;
    private Image dayButtonImage;
    private Image nightButtonImage;
    private Image rainButtonImage;
    private Image undeadButtonImage;
    private Image rotateButtonImage;
    private Text undeadButtonLabel;
    private Text rotateButtonLabel;
    private bool uiBound;

    private void Awake()
    {
        ResolveReferences();
        EnsureUiReferences();
    }

    private void Reset()
    {
        ResolveReferences();
    }

    private void OnEnable()
    {
        ResolveReferences();
        EnsureUiReferences();
        RefreshUiState();
    }

    private void Start()
    {
        ResolveReferences();
        EnsureUiReferences();
        RefreshUiState();
    }

    private void OnValidate()
    {
        ResolveReferences();
    }

    private void Update()
    {
        ResolveReferences();
        EnsureUiReferences();
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

    public void ToggleUndead()
    {
        GameplayDebugFlags.Undead = !GameplayDebugFlags.Undead;
        if (GameplayDebugFlags.Undead)
        {
            PlayerCombatController playerCombat = FindAnyObjectByType<PlayerCombatController>();
            playerCombat?.RefillForDebug();
        }

        RefreshUiState();
    }

    public void ToggleRotate()
    {
        ResolveReferences();
        if (worldRotationController == null)
        {
            return;
        }

        SetRotateEnabled(!IsRotateEnabled());
        RefreshUiState();
    }

    public void SetDebugPanelVisible(bool visible)
    {
        showDebugPanel = visible;
        RefreshUiState();
    }

#if UNITY_EDITOR
    [ContextMenu("Rebuild Authored Environment Debug UI")]
    public void RebuildAuthoredUiForEditor()
    {
        if (Application.isPlaying)
        {
            return;
        }

        ResolveReferences();
        if (targetCanvas == null)
        {
            Debug.LogWarning("EnvironmentThemeDebugPanel could not find a Canvas to rebuild authored UI.", this);
            return;
        }

        Transform existingRoot = targetCanvas.transform.Find(PanelRootName);
        if (existingRoot != null)
        {
            DestroyImmediate(existingRoot.gameObject);
        }

        ClearUiReferences();
        BuildUi(targetCanvas.transform);
        EnsureUiReferences();
        RefreshUiState();

        EditorUtility.SetDirty(targetCanvas.gameObject);
        EditorUtility.SetDirty(gameObject);
        EditorSceneManager.MarkSceneDirty(gameObject.scene);
    }
#endif

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

        if (worldRotationController == null)
        {
            worldRotationController = FindAnyObjectByType<MoonOrbitController>();
        }
    }

    private void EnsureUiReferences()
    {
        if (uiBound || targetCanvas == null)
        {
            return;
        }

        Transform panelTransform = targetCanvas.transform.Find(PanelRootName);
        if (panelTransform == null)
        {
            return;
        }

        if (panelTransform.Find("RotateButton/RotateButtonLabel") == null)
        {
            BuildUi(targetCanvas.transform);
            panelTransform = targetCanvas.transform.Find(PanelRootName);
        }

        panelRoot = panelTransform.gameObject;
        currentThemeText = FindUiComponent<Text>(panelTransform, "CurrentThemeLabel");

        dayButton = FindUiComponent<Button>(panelTransform, "DayButton");
        dayButtonImage = FindUiComponent<Image>(panelTransform, "DayButton");
        nightButton = FindUiComponent<Button>(panelTransform, "NightButton");
        nightButtonImage = FindUiComponent<Image>(panelTransform, "NightButton");
        rainButton = FindUiComponent<Button>(panelTransform, "RainButton");
        rainButtonImage = FindUiComponent<Image>(panelTransform, "RainButton");
        undeadButton = FindUiComponent<Button>(panelTransform, "UndeadButton");
        undeadButtonImage = FindUiComponent<Image>(panelTransform, "UndeadButton");
        undeadButtonLabel = FindUiComponent<Text>(panelTransform, "UndeadButton/UndeadButtonLabel");
        rotateButton = FindUiComponent<Button>(panelTransform, "RotateButton");
        rotateButtonImage = FindUiComponent<Image>(panelTransform, "RotateButton");
        rotateButtonLabel = FindUiComponent<Text>(panelTransform, "RotateButton/RotateButtonLabel");

        if (dayButton != null)
        {
            dayButton.onClick.RemoveListener(SetDayTheme);
            dayButton.onClick.AddListener(SetDayTheme);
        }

        if (nightButton != null)
        {
            nightButton.onClick.RemoveListener(SetNightTheme);
            nightButton.onClick.AddListener(SetNightTheme);
        }

        if (rainButton != null)
        {
            rainButton.onClick.RemoveListener(SetRainTheme);
            rainButton.onClick.AddListener(SetRainTheme);
        }

        if (undeadButton != null)
        {
            undeadButton.onClick.RemoveListener(ToggleUndead);
            undeadButton.onClick.AddListener(ToggleUndead);
        }

        if (rotateButton != null)
        {
            rotateButton.onClick.RemoveListener(ToggleRotate);
            rotateButton.onClick.AddListener(ToggleRotate);
        }

        uiBound =
            panelRoot != null &&
            currentThemeText != null &&
            dayButton != null &&
            nightButton != null &&
            rainButton != null &&
            undeadButton != null &&
            undeadButtonLabel != null &&
            rotateButton != null &&
            rotateButtonLabel != null;
    }

    private void ClearUiReferences()
    {
        panelRoot = null;
        currentThemeText = null;
        dayButton = null;
        nightButton = null;
        rainButton = null;
        undeadButton = null;
        rotateButton = null;
        dayButtonImage = null;
        nightButtonImage = null;
        rainButtonImage = null;
        undeadButtonImage = null;
        rotateButtonImage = null;
        undeadButtonLabel = null;
        rotateButtonLabel = null;
        uiBound = false;
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

        if (environmentController != null)
        {
            ApplyButtonState(dayButtonImage, environmentController.ActiveThemeType == EnvironmentThemeType.Day, new Color(0.24f, 0.60f, 0.80f));
            ApplyButtonState(nightButtonImage, environmentController.ActiveThemeType == EnvironmentThemeType.Night, new Color(0.22f, 0.26f, 0.42f));
            ApplyButtonState(rainButtonImage, environmentController.ActiveThemeType == EnvironmentThemeType.Rain, new Color(0.28f, 0.42f, 0.52f));
        }

        if (undeadButtonImage != null)
        {
            ApplyButtonState(undeadButtonImage, GameplayDebugFlags.Undead, new Color(0.34f, 0.18f, 0.18f));
        }

        if (undeadButtonLabel != null)
        {
            undeadButtonLabel.text = GameplayDebugFlags.Undead ? "Undead ON" : "Undead OFF";
        }

        bool rotateEnabled = IsRotateEnabled();
        if (rotateButtonImage != null)
        {
            ApplyButtonState(rotateButtonImage, rotateEnabled, new Color(0.18f, 0.34f, 0.48f));
        }

        if (rotateButtonLabel != null)
        {
            rotateButtonLabel.text = rotateEnabled ? "Rotate ON" : "Rotate OFF";
        }
    }

    private bool IsRotateEnabled()
    {
        return worldRotationController != null && worldRotationController.OrbitEnabled;
    }

    private void SetRotateEnabled(bool enabled)
    {
        if (worldRotationController != null)
        {
            worldRotationController.OrbitEnabled = enabled;
        }
    }

    private void BuildUi(Transform canvasTransform)
    {
        Font runtimeFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

        GameObject createdPanelRoot = FindOrCreateUiObject(PanelRootName, canvasTransform);
        RectTransform panelRect = createdPanelRoot.GetComponent<RectTransform>();
        panelRect.anchorMin = new Vector2(0f, 0f);
        panelRect.anchorMax = new Vector2(0f, 0f);
        panelRect.pivot = new Vector2(0f, 0f);
        panelRect.sizeDelta = new Vector2(420f, 176f);
        panelRect.anchoredPosition = panelAnchoredPosition;

        Image panelBackground = createdPanelRoot.GetComponent<Image>() ?? createdPanelRoot.AddComponent<Image>();
        panelBackground.color = new Color(0.07f, 0.10f, 0.15f, 0.84f);

        Text titleText = CreateText(
            "TitleLabel",
            createdPanelRoot.transform,
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

        Text createdCurrentThemeText = CreateText(
            "CurrentThemeLabel",
            createdPanelRoot.transform,
            runtimeFont,
            16,
            FontStyle.Normal,
            TextAnchor.UpperLeft,
            new Color(0.76f, 0.84f, 0.93f));
        RectTransform currentThemeRect = createdCurrentThemeText.rectTransform;
        currentThemeRect.anchorMin = new Vector2(0f, 1f);
        currentThemeRect.anchorMax = new Vector2(1f, 1f);
        currentThemeRect.pivot = new Vector2(0f, 1f);
        currentThemeRect.offsetMin = new Vector2(14f, -60f);
        currentThemeRect.offsetMax = new Vector2(-14f, -36f);

        CreateButton(
            "DayButton",
            createdPanelRoot.transform,
            runtimeFont,
            "Day",
            new Vector2(56f, -90f),
            new Vector2(86f, 34f),
            new Color(0.24f, 0.60f, 0.80f),
            SetDayTheme,
            out _,
            out _);

        CreateButton(
            "NightButton",
            createdPanelRoot.transform,
            runtimeFont,
            "Night",
            new Vector2(154f, -90f),
            new Vector2(86f, 34f),
            new Color(0.22f, 0.26f, 0.42f),
            SetNightTheme,
            out _,
            out _);

        CreateButton(
            "RainButton",
            createdPanelRoot.transform,
            runtimeFont,
            "Rain",
            new Vector2(252f, -90f),
            new Vector2(86f, 34f),
            new Color(0.28f, 0.42f, 0.52f),
            SetRainTheme,
            out _,
            out _);

        CreateButton(
            "UndeadButton",
            createdPanelRoot.transform,
            runtimeFont,
            "Undead OFF",
            new Vector2(154f, -132f),
            new Vector2(184f, 34f),
            new Color(0.34f, 0.18f, 0.18f),
            ToggleUndead,
            out _,
            out _);

        CreateButton(
            "RotateButton",
            createdPanelRoot.transform,
            runtimeFont,
            "Rotate OFF",
            new Vector2(334f, -132f),
            new Vector2(136f, 34f),
            new Color(0.18f, 0.34f, 0.48f),
            ToggleRotate,
            out _,
            out _);
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

    private static T FindUiComponent<T>(Transform root, string relativePath) where T : Component
    {
        Transform target = root != null ? root.Find(relativePath) : null;
        return target != null ? target.GetComponent<T>() : null;
    }

    private static void CreateButton(
        string name,
        Transform parent,
        Font font,
        string label,
        Vector2 anchoredPosition,
        Vector2 size,
        Color color,
        UnityEngine.Events.UnityAction onClick,
        out Image buttonImage,
        out Text labelText)
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
        buttonRect.sizeDelta = size;
        buttonRect.anchoredPosition = anchoredPosition;

        labelText = CreateText(
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

            if (Application.isPlaying)
            {
                Object.Destroy(existing.gameObject);
            }
            else
            {
                Object.DestroyImmediate(existing.gameObject);
            }
        }

        GameObject created = new(name, typeof(RectTransform));
        created.transform.SetParent(parent, false);
        return created;
    }
}
