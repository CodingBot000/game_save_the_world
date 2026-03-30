using System.Collections;
using UnityEngine;
using UnityEngine.UI;

public class StageSelectPresenter : MonoBehaviour
{
    [SerializeField] private bool autoBuildUi = true;
    [SerializeField] private string stageEarthName = "StageEarth";
    [SerializeField] private float zoomDuration = 0.7f;
    [SerializeField] private float overlayFadeDuration = 0.3f;
    [SerializeField] private float redAlertDuration = 1f;
    [SerializeField] private float zoomDistanceOffset = 1.15f;

    private Canvas canvas;
    private Camera stageCamera;
    private Transform stageEarth;
    private StageEarthInteractionController earthInteractionController;
    private Image blackoutOverlay;
    private Text redAlertText;
    private bool uiBuilt;
    private bool startRequested;

    private void Awake()
    {
        ResolveReferences();
        TryBuildUi();
    }

    private void OnEnable()
    {
        ResolveReferences();
        TryBuildUi();
    }

    private void Start()
    {
        ResolveReferences();
        TryBuildUi();
    }

    private void ResolveReferences()
    {
        canvas = GetComponent<Canvas>();
        if (canvas == null)
        {
            canvas = GetComponentInChildren<Canvas>(true);
        }

        if (canvas == null)
        {
            GameObject canvasObject = new GameObject("StageSelectCanvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvasObject.transform.SetParent(transform, false);

            canvas = canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;

            CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;
        }

        if (stageEarth == null && !string.IsNullOrWhiteSpace(stageEarthName))
        {
            GameObject earthObject = GameObject.Find(stageEarthName);
            stageEarth = earthObject != null ? earthObject.transform : null;
        }

        if (stageCamera == null)
        {
            stageCamera = Camera.main;
        }

        if (stageCamera == null)
        {
            stageCamera = FindAnyObjectByType<Camera>();
        }

        StageEarthInteractionController nextController =
            stageEarth != null ? stageEarth.GetComponent<StageEarthInteractionController>() : null;

        if (nextController == earthInteractionController)
        {
            return;
        }

        if (earthInteractionController != null)
        {
            earthInteractionController.Selected -= HandleStageSelected;
        }

        earthInteractionController = nextController;

        if (earthInteractionController != null)
        {
            earthInteractionController.Selected += HandleStageSelected;
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

        GameObject root = FindOrCreateUiObject("GeneratedStageSelect", canvas.transform);
        RectTransform rootRect = root.GetComponent<RectTransform>();
        rootRect.anchorMin = Vector2.zero;
        rootRect.anchorMax = Vector2.one;
        rootRect.offsetMin = Vector2.zero;
        rootRect.offsetMax = Vector2.zero;

        Text title = CreateText("Title", root.transform, runtimeFont, 40, FontStyle.Bold, TextAnchor.MiddleCenter, Color.white);
        title.text = "Stage Select";
        RectTransform titleRect = title.rectTransform;
        titleRect.anchorMin = new Vector2(0.5f, 1f);
        titleRect.anchorMax = new Vector2(0.5f, 1f);
        titleRect.pivot = new Vector2(0.5f, 1f);
        titleRect.sizeDelta = new Vector2(720f, 56f);
        titleRect.anchoredPosition = new Vector2(0f, -96f);

        Text subtitle = CreateText("Subtitle", root.transform, runtimeFont, 20, FontStyle.Normal, TextAnchor.MiddleCenter, new Color(0.84f, 0.9f, 0.96f));
        subtitle.text = "Select the globe to deploy.";
        RectTransform subtitleRect = subtitle.rectTransform;
        subtitleRect.anchorMin = new Vector2(0.5f, 1f);
        subtitleRect.anchorMax = new Vector2(0.5f, 1f);
        subtitleRect.pivot = new Vector2(0.5f, 1f);
        subtitleRect.sizeDelta = new Vector2(720f, 32f);
        subtitleRect.anchoredPosition = new Vector2(0f, -146f);

        Text hint = CreateText("Hint", root.transform, runtimeFont, 18, FontStyle.Normal, TextAnchor.MiddleCenter, new Color(0.76f, 0.84f, 0.92f));
        hint.text = "Mouse click or touch anywhere on Earth to start";
        RectTransform hintRect = hint.rectTransform;
        hintRect.anchorMin = new Vector2(0.5f, 0f);
        hintRect.anchorMax = new Vector2(0.5f, 0f);
        hintRect.pivot = new Vector2(0.5f, 0f);
        hintRect.sizeDelta = new Vector2(900f, 28f);
        hintRect.anchoredPosition = new Vector2(0f, 52f);

        blackoutOverlay = CreateImage("BlackoutOverlay", root.transform, new Color(0f, 0f, 0f, 0f));
        RectTransform overlayRect = blackoutOverlay.rectTransform;
        overlayRect.anchorMin = Vector2.zero;
        overlayRect.anchorMax = Vector2.one;
        overlayRect.offsetMin = Vector2.zero;
        overlayRect.offsetMax = Vector2.zero;
        blackoutOverlay.raycastTarget = false;

        redAlertText = CreateText("RedAlert", root.transform, runtimeFont, 120, FontStyle.Bold, TextAnchor.MiddleCenter, new Color(1f, 0.12f, 0.12f, 0f));
        redAlertText.text = "RED ALERT";
        redAlertText.raycastTarget = false;

        Outline outline = redAlertText.GetComponent<Outline>() ?? redAlertText.gameObject.AddComponent<Outline>();
        outline.effectColor = new Color(0.28f, 0f, 0f, 0.95f);
        outline.effectDistance = new Vector2(6f, -6f);

        Shadow shadow = redAlertText.GetComponent<Shadow>() ?? redAlertText.gameObject.AddComponent<Shadow>();
        shadow.effectColor = new Color(1f, 0.22f, 0.22f, 0.55f);
        shadow.effectDistance = new Vector2(0f, 0f);

        RectTransform alertRect = redAlertText.rectTransform;
        alertRect.anchorMin = new Vector2(0.5f, 0.5f);
        alertRect.anchorMax = new Vector2(0.5f, 0.5f);
        alertRect.pivot = new Vector2(0.5f, 0.5f);
        alertRect.sizeDelta = new Vector2(1200f, 180f);
        alertRect.anchoredPosition = Vector2.zero;
        alertRect.localScale = Vector3.one * 0.94f;
    }

    private void HandleStageSelected(Vector3 selectedPoint)
    {
        if (startRequested)
        {
            return;
        }

        startRequested = true;
        if (earthInteractionController != null)
        {
            earthInteractionController.enabled = false;
        }

        StartCoroutine(PlayStageStartSequence(selectedPoint));
    }

    private void OnDestroy()
    {
        if (earthInteractionController != null)
        {
            earthInteractionController.Selected -= HandleStageSelected;
        }
    }

    private IEnumerator PlayStageStartSequence(Vector3 selectedPoint)
    {
        ResolveReferences();

        if (stageCamera != null)
        {
            yield return AnimateCameraZoom(selectedPoint);
        }

        yield return FadeToBlack();
        yield return PlayRedAlert();
        GameFlowController.StartBattle(GameFlowController.CurrentMode);
    }

    private IEnumerator AnimateCameraZoom(Vector3 selectedPoint)
    {
        Transform cameraTransform = stageCamera.transform;
        Vector3 startPosition = cameraTransform.position;
        Quaternion startRotation = cameraTransform.rotation;
        Vector3 approachDirection = (selectedPoint - startPosition).normalized;
        Vector3 targetPosition = selectedPoint - approachDirection * zoomDistanceOffset;
        Quaternion targetRotation = Quaternion.LookRotation(selectedPoint - targetPosition, Vector3.up);

        float elapsed = 0f;
        while (elapsed < zoomDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / zoomDuration);
            float eased = EaseInCubic(t);

            cameraTransform.position = Vector3.LerpUnclamped(startPosition, targetPosition, eased);
            cameraTransform.rotation = Quaternion.SlerpUnclamped(startRotation, targetRotation, eased);
            yield return null;
        }

        cameraTransform.position = targetPosition;
        cameraTransform.rotation = targetRotation;
    }

    private IEnumerator FadeToBlack()
    {
        if (blackoutOverlay == null)
        {
            yield break;
        }

        float elapsed = 0f;
        Color color = blackoutOverlay.color;
        while (elapsed < overlayFadeDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            color.a = Mathf.Clamp01(elapsed / overlayFadeDuration);
            blackoutOverlay.color = color;
            yield return null;
        }

        color.a = 1f;
        blackoutOverlay.color = color;
    }

    private IEnumerator PlayRedAlert()
    {
        if (redAlertText == null)
        {
            yield return new WaitForSecondsRealtime(redAlertDuration);
            yield break;
        }

        float elapsed = 0f;
        Color baseColor = new Color(1f, 0.12f, 0.12f, 1f);
        while (elapsed < redAlertDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / redAlertDuration);
            float flicker = 0.35f + 0.65f * Mathf.Abs(Mathf.Sin(t * Mathf.PI * 8f));
            float pulse = 1f + 0.08f * Mathf.Sin(t * Mathf.PI * 4f);

            redAlertText.color = new Color(baseColor.r, baseColor.g, baseColor.b, flicker);
            redAlertText.rectTransform.localScale = Vector3.one * pulse;
            yield return null;
        }

        redAlertText.color = new Color(baseColor.r, baseColor.g, baseColor.b, 1f);
        redAlertText.rectTransform.localScale = Vector3.one;
    }

    private static float EaseInCubic(float t)
    {
        return t * t * t;
    }

    private static Image CreateImage(string name, Transform parent, Color color)
    {
        GameObject imageObject = FindOrCreateUiObject(name, parent);
        Image image = imageObject.GetComponent<Image>() ?? imageObject.AddComponent<Image>();
        image.color = color;
        return image;
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
