using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public abstract class OverlayScenePresenterBase : MonoBehaviour
{
    [SerializeField] private bool autoBuildUi = true;

    private Canvas canvas;
    private bool uiBuilt;

    protected abstract string SceneTitle { get; }
    protected abstract string SceneDescription { get; }
    protected abstract Color AccentColor { get; }

    private void Awake()
    {
        ResolveCanvas();
        TryBuildUi();
    }

    private void OnEnable()
    {
        ResolveCanvas();
        TryBuildUi();
    }

    private void Update()
    {
        Keyboard keyboard = Keyboard.current;
        if (keyboard != null && keyboard.escapeKey.wasPressedThisFrame)
        {
            CloseScene();
        }
    }

    private void ResolveCanvas()
    {
        canvas = GetComponent<Canvas>();
        if (canvas == null)
        {
            canvas = GetComponentInParent<Canvas>();
        }

        if (canvas == null)
        {
            GameObject canvasObject = new GameObject(
                "OverlayCanvas",
                typeof(RectTransform),
                typeof(Canvas),
                typeof(CanvasScaler),
                typeof(GraphicRaycaster));
            canvasObject.transform.SetParent(transform, false);
            canvas = canvasObject.GetComponent<Canvas>();
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

        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.overrideSorting = true;
        canvas.sortingOrder = 100;

        if (canvas.GetComponent<GraphicRaycaster>() == null)
        {
            canvas.gameObject.AddComponent<GraphicRaycaster>();
        }

        CanvasScaler scaler = canvas.GetComponent<CanvasScaler>();
        if (scaler == null)
        {
            scaler = canvas.gameObject.AddComponent<CanvasScaler>();
        }

        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = 0.5f;

        GameObject root = SimpleUiFactory.FindOrCreateUiObject("GeneratedOverlayRoot", canvas.transform);
        SimpleUiFactory.StretchFull(root.GetComponent<RectTransform>());

        Image dimmer = SimpleUiFactory.CreateImage("Dimmer", root.transform, new Color(0.02f, 0.04f, 0.08f, 0.78f));
        SimpleUiFactory.StretchFull(dimmer.rectTransform);

        Image panel = SimpleUiFactory.CreateImage("Panel", root.transform, new Color(0.08f, 0.13f, 0.19f, 0.96f));
        SimpleUiFactory.StretchFull(panel.rectTransform);

        Image panelAccent = SimpleUiFactory.CreateImage("PanelAccent", panel.transform, AccentColor);
        SimpleUiFactory.SetAnchoredLayout(
            panelAccent.rectTransform,
            new Vector2(0f, 1f),
            new Vector2(1f, 1f),
            new Vector2(0.5f, 1f),
            new Vector2(0f, 10f),
            Vector2.zero);

        Button closeButton = SimpleUiFactory.CreateButton(
            "CloseButton",
            root.transform,
            runtimeFont,
            "X",
            new Color(0.68f, 0.18f, 0.18f, 1f),
            CloseScene,
            24);
        SimpleUiFactory.SetAnchoredLayout(
            closeButton.GetComponent<RectTransform>(),
            new Vector2(0f, 1f),
            new Vector2(0f, 1f),
            new Vector2(0f, 1f),
            new Vector2(72f, 72f),
            new Vector2(44f, -44f));

        Text title = SimpleUiFactory.CreateText("Title", panel.transform, runtimeFont, 40, FontStyle.Bold, TextAnchor.MiddleLeft, Color.white);
        title.text = SceneTitle;
        SimpleUiFactory.SetAnchoredLayout(
            title.rectTransform,
            new Vector2(0f, 1f),
            new Vector2(1f, 1f),
            new Vector2(0f, 1f),
            new Vector2(-140f, 56f),
            new Vector2(56f, -72f));

        Text description = SimpleUiFactory.CreateText("Description", panel.transform, runtimeFont, 22, FontStyle.Normal, TextAnchor.UpperLeft, new Color(0.84f, 0.9f, 0.96f));
        description.text = SceneDescription;
        SimpleUiFactory.SetAnchoredLayout(
            description.rectTransform,
            new Vector2(0f, 1f),
            new Vector2(1f, 1f),
            new Vector2(0f, 1f),
            new Vector2(-112f, 92f),
            new Vector2(56f, -140f));

        Image contentCard = SimpleUiFactory.CreateImage("ContentCard", panel.transform, new Color(0.11f, 0.17f, 0.24f, 1f));
        SimpleUiFactory.SetAnchoredLayout(
            contentCard.rectTransform,
            new Vector2(0f, 0f),
            new Vector2(1f, 1f),
            new Vector2(0.5f, 0.5f),
            new Vector2(-112f, -280f),
            new Vector2(0f, -24f));

        Text contentLabel = SimpleUiFactory.CreateText("ContentLabel", contentCard.transform, runtimeFont, 28, FontStyle.Bold, TextAnchor.MiddleCenter, new Color(0.9f, 0.95f, 1f));
        contentLabel.text = $"{SceneTitle}\nPlaceholder";
        SimpleUiFactory.StretchFull(contentLabel.rectTransform);

        Text footerHint = SimpleUiFactory.CreateText("FooterHint", panel.transform, runtimeFont, 18, FontStyle.Normal, TextAnchor.MiddleRight, new Color(0.73f, 0.8f, 0.88f));
        footerHint.text = "Press Esc or click X to close";
        SimpleUiFactory.SetAnchoredLayout(
            footerHint.rectTransform,
            new Vector2(1f, 0f),
            new Vector2(1f, 0f),
            new Vector2(1f, 0f),
            new Vector2(320f, 26f),
            new Vector2(-56f, 40f));
    }

    private void CloseScene()
    {
        SceneManager.UnloadSceneAsync(gameObject.scene);
    }
}
