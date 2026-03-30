using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

public class IntroPresenter : MonoBehaviour
{
    [SerializeField] private bool autoBuildUi = true;

    private Canvas canvas;
    private bool uiBuilt;
    private bool transitionRequested;
    private bool runtimeInitialized;

    private void Awake()
    {
        if (!Application.isPlaying)
        {
            return;
        }

        InitializeRuntime();
    }

    private void OnEnable()
    {
        if (!Application.isPlaying)
        {
            return;
        }

        InitializeRuntime();
    }

    private void Start()
    {
        if (!Application.isPlaying)
        {
            return;
        }

        InitializeRuntime();
    }

    private void Update()
    {
        if (!Application.isPlaying || !runtimeInitialized || transitionRequested)
        {
            return;
        }

        if (WasContinuePressed())
        {
            ContinueToMainMenu();
        }
    }

    public void ContinueToMainMenu()
    {
        if (transitionRequested)
        {
            return;
        }

        transitionRequested = true;
        GameFlowController.LoadMainMenu();
    }

    private void InitializeRuntime()
    {
        if (runtimeInitialized)
        {
            return;
        }

        EnsureEventSystem();
        ResolveCanvas();
        TryBuildUi();
        runtimeInitialized = true;
    }

    private bool WasContinuePressed()
    {
        Keyboard keyboard = Keyboard.current;
        if (keyboard != null)
        {
            if (keyboard.anyKey.wasPressedThisFrame || keyboard.enterKey.wasPressedThisFrame || keyboard.spaceKey.wasPressedThisFrame)
            {
                return true;
            }
        }

        Mouse mouse = Mouse.current;
        if (mouse != null && (mouse.leftButton.wasPressedThisFrame || mouse.rightButton.wasPressedThisFrame))
        {
            return true;
        }

        return false;
    }

    private void ResolveCanvas()
    {
        canvas = GetComponent<Canvas>();
        if (canvas == null)
        {
            canvas = GetComponentInChildren<Canvas>(true);
        }

        if (canvas != null)
        {
            return;
        }

        GameObject canvasObject = new GameObject("IntroCanvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        canvasObject.transform.SetParent(transform, false);

        canvas = canvasObject.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;

        CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = 0.5f;
    }

    private void EnsureEventSystem()
    {
        if (FindAnyObjectByType<EventSystem>() != null)
        {
            return;
        }

        GameObject eventSystemObject = new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
        eventSystemObject.transform.SetParent(transform, false);
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

        GameObject root = FindOrCreateUiObject("GeneratedIntro", canvas.transform);
        RectTransform rootRect = root.GetComponent<RectTransform>();
        rootRect.anchorMin = Vector2.zero;
        rootRect.anchorMax = Vector2.one;
        rootRect.offsetMin = Vector2.zero;
        rootRect.offsetMax = Vector2.zero;

        GameObject backdrop = FindOrCreateUiObject("Backdrop", root.transform);
        Image backdropImage = backdrop.GetComponent<Image>() ?? backdrop.AddComponent<Image>();
        backdropImage.color = new Color(0.03f, 0.05f, 0.09f, 0.96f);
        RectTransform backdropRect = backdrop.GetComponent<RectTransform>();
        backdropRect.anchorMin = Vector2.zero;
        backdropRect.anchorMax = Vector2.one;
        backdropRect.offsetMin = Vector2.zero;
        backdropRect.offsetMax = Vector2.zero;

        GameObject bandObject = FindOrCreateUiObject("AccentBand", root.transform);
        Image bandImage = bandObject.GetComponent<Image>() ?? bandObject.AddComponent<Image>();
        bandImage.color = new Color(0.78f, 0.18f, 0.12f, 0.9f);
        RectTransform bandRect = bandImage.rectTransform;
        bandRect.anchorMin = new Vector2(0.5f, 0.5f);
        bandRect.anchorMax = new Vector2(0.5f, 0.5f);
        bandRect.pivot = new Vector2(0.5f, 0.5f);
        bandRect.sizeDelta = new Vector2(680f, 6f);
        bandRect.anchoredPosition = new Vector2(0f, 12f);

        Text title = CreateText("Title", root.transform, runtimeFont, 56, FontStyle.Bold, TextAnchor.MiddleCenter, Color.white);
        title.text = "TITAN DESTROYER";
        RectTransform titleRect = title.rectTransform;
        titleRect.anchorMin = new Vector2(0.5f, 0.5f);
        titleRect.anchorMax = new Vector2(0.5f, 0.5f);
        titleRect.pivot = new Vector2(0.5f, 0.5f);
        titleRect.sizeDelta = new Vector2(1200f, 80f);
        titleRect.anchoredPosition = new Vector2(0f, 96f);

        Text subtitle = CreateText("Subtitle", root.transform, runtimeFont, 22, FontStyle.Normal, TextAnchor.MiddleCenter, new Color(0.83f, 0.88f, 0.93f));
        subtitle.text = "Approach vector locked. Prepare for titan engagement.";
        RectTransform subtitleRect = subtitle.rectTransform;
        subtitleRect.anchorMin = new Vector2(0.5f, 0.5f);
        subtitleRect.anchorMax = new Vector2(0.5f, 0.5f);
        subtitleRect.pivot = new Vector2(0.5f, 0.5f);
        subtitleRect.sizeDelta = new Vector2(1100f, 40f);
        subtitleRect.anchoredPosition = new Vector2(0f, 38f);

        Button continueButton = CreateButton(
            "ContinueButton",
            root.transform,
            runtimeFont,
            "Continue",
            new Vector2(0f, -74f),
            new Color(0.82f, 0.24f, 0.16f),
            ContinueToMainMenu);

        Text hint = CreateText("Hint", root.transform, runtimeFont, 18, FontStyle.Normal, TextAnchor.MiddleCenter, new Color(0.78f, 0.83f, 0.9f));
        hint.text = "Press any key or click to continue";
        RectTransform hintRect = hint.rectTransform;
        hintRect.anchorMin = new Vector2(0.5f, 0.5f);
        hintRect.anchorMax = new Vector2(0.5f, 0.5f);
        hintRect.pivot = new Vector2(0.5f, 0.5f);
        hintRect.sizeDelta = new Vector2(720f, 28f);
        hintRect.anchoredPosition = new Vector2(0f, -148f);

        continueButton.navigation = Navigation.defaultNavigation;
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
        buttonRect.anchorMin = new Vector2(0.5f, 0.5f);
        buttonRect.anchorMax = new Vector2(0.5f, 0.5f);
        buttonRect.pivot = new Vector2(0.5f, 0.5f);
        buttonRect.sizeDelta = new Vector2(280f, 56f);
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

            Object.Destroy(existing.gameObject);
        }

        GameObject created = new GameObject(name, typeof(RectTransform));
        created.transform.SetParent(parent, false);
        return created;
    }
}
