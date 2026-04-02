using System.Collections;
using System.Text;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

[ExecuteAlways]
public class StageStepSelectScenePresenter : MonoBehaviour
{
    private const string StageIconResourceRoot = "WorldMap/stage_icons/";

    [SerializeField] private Canvas canvas;
    [SerializeField] private RectTransform pageViewport;
    [SerializeField] private RectTransform pageContainer;
    [SerializeField] private Button closeButton;
    [SerializeField] private Button previousButton;
    [SerializeField] private Button nextButton;
    [SerializeField] private Button[] difficultyButtons;
    [SerializeField] private float pageTransitionDuration = 0.3f;
    [SerializeField] private string exitSceneName = GameFlowController.MainMenuSceneName;

    private RectTransform[] pages = System.Array.Empty<RectTransform>();
    private int currentPageIndex;
    private int currentDifficultyIndex;
    private Coroutine pageTransitionCoroutine;
    private bool uiBound;

    private void Awake()
    {
        Initialize();
    }

    private void OnEnable()
    {
        Initialize();
    }

    private void Start()
    {
        Initialize();
        SnapToPage();
    }

    private void Update()
    {
        if (!Application.isPlaying)
        {
            return;
        }

        Keyboard keyboard = Keyboard.current;
        if (keyboard == null)
        {
            return;
        }

        if (keyboard.escapeKey.wasPressedThisFrame)
        {
            CloseScene();
            return;
        }

        if (keyboard.leftArrowKey.wasPressedThisFrame)
        {
            ShowPreviousPage();
        }
        else if (keyboard.rightArrowKey.wasPressedThisFrame)
        {
            ShowNextPage();
        }

        if (keyboard.digit1Key.wasPressedThisFrame || keyboard.numpad1Key.wasPressedThisFrame)
        {
            SetDifficulty(0);
        }
        else if (keyboard.digit2Key.wasPressedThisFrame || keyboard.numpad2Key.wasPressedThisFrame)
        {
            SetDifficulty(1);
        }
        else if (keyboard.digit3Key.wasPressedThisFrame || keyboard.numpad3Key.wasPressedThisFrame)
        {
            SetDifficulty(2);
        }
        else if (keyboard.digit4Key.wasPressedThisFrame || keyboard.numpad4Key.wasPressedThisFrame)
        {
            SetDifficulty(3);
        }
    }

    private void OnRectTransformDimensionsChange()
    {
        if (!isActiveAndEnabled)
        {
            return;
        }

        LayoutPages();
        SnapToPage();
    }

    private void Initialize()
    {
        EnsureRuntimeSupport();
        ResolveReferences();
        ApplySelectionState();
        BindUi();
        LayoutPages();
        UpdateArrowState();
        UpdateDifficultyState();
    }

    private void EnsureRuntimeSupport()
    {
        if (!Application.isPlaying)
        {
            return;
        }

        if (FindAnyObjectByType<EventSystem>() == null)
        {
            GameObject eventSystemObject = new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
            eventSystemObject.transform.SetParent(transform, false);
        }
    }

    private void ResolveReferences()
    {
        if (canvas == null)
        {
            canvas = GetComponentInChildren<Canvas>(true);
        }

        if (canvas == null)
        {
            return;
        }

        Transform rootPanel = canvas.transform.Find("StageStepSelectRootPanel");
        Transform frameBorder = rootPanel != null ? rootPanel.Find("FrameBorder") : null;
        Transform frame = frameBorder != null ? frameBorder.Find("Frame") : null;

        if (pageViewport == null)
        {
            pageViewport = frame != null ? frame.Find("Viewport") as RectTransform : null;
        }

        if (pageContainer == null && pageViewport != null)
        {
            pageContainer = pageViewport.Find("PageContainer") as RectTransform;
        }

        if (closeButton == null)
        {
            closeButton = frame != null ? frame.Find("CloseButton")?.GetComponent<Button>() : null;
        }

        if (previousButton == null)
        {
            previousButton = frame != null ? frame.Find("PreviousButton")?.GetComponent<Button>() : null;
        }

        if (nextButton == null)
        {
            nextButton = frame != null ? frame.Find("NextButton")?.GetComponent<Button>() : null;
        }

        if (difficultyButtons == null || difficultyButtons.Length == 0)
        {
            Transform difficultyPanel = frame != null ? frame.Find("DifficultyPanel") : null;
            if (difficultyPanel != null)
            {
                difficultyButtons = new Button[difficultyPanel.childCount];
                for (int i = 0; i < difficultyPanel.childCount; i++)
                {
                    difficultyButtons[i] = difficultyPanel.GetChild(i).GetComponent<Button>();
                }
            }
        }

        CachePages();
    }

    private void BindUi()
    {
        if (canvas == null || pageViewport == null || pageContainer == null)
        {
            return;
        }

        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.overrideSorting = true;
        canvas.sortingOrder = 100;

        CanvasScaler scaler = canvas.GetComponent<CanvasScaler>() ?? canvas.gameObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = 0.5f;

        GraphicRaycaster raycaster = canvas.GetComponent<GraphicRaycaster>();
        if (raycaster == null)
        {
            canvas.gameObject.AddComponent<GraphicRaycaster>();
        }

        if (closeButton != null)
        {
            closeButton.onClick.RemoveAllListeners();
            closeButton.onClick.AddListener(CloseScene);
        }

        if (previousButton != null)
        {
            previousButton.onClick.RemoveAllListeners();
            previousButton.onClick.AddListener(ShowPreviousPage);
        }

        if (nextButton != null)
        {
            nextButton.onClick.RemoveAllListeners();
            nextButton.onClick.AddListener(ShowNextPage);
        }

        if (difficultyButtons != null)
        {
            for (int i = 0; i < difficultyButtons.Length; i++)
            {
                Button button = difficultyButtons[i];
                if (button == null)
                {
                    continue;
                }

                int capturedIndex = i;
                button.onClick.RemoveAllListeners();
                button.onClick.AddListener(() => SetDifficulty(capturedIndex));
            }
        }

        BindStagePreviewButtons();
        uiBound = true;
    }

    private void CachePages()
    {
        if (pageContainer == null)
        {
            pages = System.Array.Empty<RectTransform>();
            return;
        }

        int childCount = pageContainer.childCount;
        pages = new RectTransform[childCount];
        for (int i = 0; i < childCount; i++)
        {
            pages[i] = pageContainer.GetChild(i) as RectTransform;
        }

        currentPageIndex = Mathf.Clamp(currentPageIndex, 0, Mathf.Max(0, childCount - 1));
    }

    private void ApplySelectionState()
    {
        if (!Application.isPlaying)
        {
            return;
        }

        StageSelectionState selectionState = StageSelectionState.EnsureInitialized();
        if (selectionState == null)
        {
            return;
        }

        if (!string.IsNullOrWhiteSpace(selectionState.SelectedStageId))
        {
            int matchingPageIndex = FindPageIndex(selectionState.SelectedStageId, selectionState.SelectedStageName);
            if (matchingPageIndex >= 0)
            {
                currentPageIndex = matchingPageIndex;
            }
        }

        int maxDifficultyIndex = difficultyButtons != null && difficultyButtons.Length > 0
            ? difficultyButtons.Length - 1
            : 3;
        currentDifficultyIndex = Mathf.Clamp((int)selectionState.SelectedDifficulty, 0, maxDifficultyIndex);
    }

    private void BindStagePreviewButtons()
    {
        CachePages();

        for (int i = 0; i < pages.Length; i++)
        {
            if (pages[i] == null)
            {
                continue;
            }

            Button previewButton = pages[i].Find("StagePreviewButton")?.GetComponent<Button>();
            if (previewButton == null)
            {
                continue;
            }

            previewButton.onClick.RemoveAllListeners();
            previewButton.onClick.AddListener(OpenBattleScene);
        }

        ApplyStagePreviewImages();
    }

    private void ApplyStagePreviewImages()
    {
        for (int i = 0; i < pages.Length; i++)
        {
            RectTransform page = pages[i];
            if (page == null)
            {
                continue;
            }

            string stageTitle = page.Find("StageNameLabel")?.GetComponent<Text>()?.text;
            Button previewButton = page.Find("StagePreviewButton")?.GetComponent<Button>();
            if (previewButton == null)
            {
                continue;
            }

            RawImage previewImage = GetOrCreatePreviewImage(previewButton.transform);
            Texture2D texture = LoadStagePreviewTexture(stageTitle);
            AspectRatioFitter aspectFitter = previewImage.GetComponent<AspectRatioFitter>();

            if (texture == null)
            {
                previewImage.texture = null;
                previewImage.color = new Color(1f, 1f, 1f, 0f);
                continue;
            }

            previewImage.texture = texture;
            previewImage.color = Color.white;
            if (aspectFitter != null)
            {
                aspectFitter.aspectRatio = Mathf.Max(0.01f, texture.width / (float)texture.height);
            }
        }
    }

    private RawImage GetOrCreatePreviewImage(Transform previewButtonTransform)
    {
        Transform existing = previewButtonTransform.Find("PreviewImage");
        RawImage image;

        if (existing != null)
        {
            image = existing.GetComponent<RawImage>() ?? existing.gameObject.AddComponent<RawImage>();
        }
        else
        {
            GameObject imageObject = new GameObject("PreviewImage", typeof(RectTransform), typeof(RawImage), typeof(AspectRatioFitter));
            imageObject.transform.SetParent(previewButtonTransform, false);
            image = imageObject.GetComponent<RawImage>();

            RectTransform imageRect = image.rectTransform;
            imageRect.anchorMin = Vector2.zero;
            imageRect.anchorMax = Vector2.one;
            imageRect.offsetMin = new Vector2(18f, 18f);
            imageRect.offsetMax = new Vector2(-18f, -18f);
        }

        AspectRatioFitter fitter = image.GetComponent<AspectRatioFitter>() ?? image.gameObject.AddComponent<AspectRatioFitter>();
        fitter.aspectMode = AspectRatioFitter.AspectMode.FitInParent;
        fitter.aspectRatio = 1f;
        image.raycastTarget = false;
        return image;
    }

    private Texture2D LoadStagePreviewTexture(string stageTitle)
    {
        string resourceKey = GetStageIconResourceKey(stageTitle);
        if (string.IsNullOrWhiteSpace(resourceKey))
        {
            return null;
        }

        return Resources.Load<Texture2D>(StageIconResourceRoot + resourceKey);
    }

    private static string GetStageIconResourceKey(string stageTitle)
    {
        if (string.IsNullOrWhiteSpace(stageTitle))
        {
            return null;
        }

        string normalizedTitle = stageTitle.Trim().ToLowerInvariant().Replace(" ", string.Empty);
        return normalizedTitle switch
        {
            "hollywood" => "usa",
            _ => normalizedTitle
        };
    }

    private void LayoutPages()
    {
        if (pageViewport == null || pageContainer == null)
        {
            return;
        }

        CachePages();
        if (pages.Length == 0)
        {
            return;
        }

        float pageWidth = pageViewport.rect.width;
        if (pageWidth <= 0f)
        {
            return;
        }

        pageContainer.anchorMin = new Vector2(0f, 0f);
        pageContainer.anchorMax = new Vector2(0f, 1f);
        pageContainer.pivot = new Vector2(0f, 0.5f);
        pageContainer.sizeDelta = new Vector2(pageWidth * pages.Length, 0f);

        for (int i = 0; i < pages.Length; i++)
        {
            RectTransform page = pages[i];
            if (page == null)
            {
                continue;
            }

            page.anchorMin = new Vector2(0f, 0f);
            page.anchorMax = new Vector2(0f, 1f);
            page.pivot = new Vector2(0f, 0.5f);
            page.sizeDelta = new Vector2(pageWidth, 0f);
            page.anchoredPosition = new Vector2(pageWidth * i, 0f);
        }
    }

    private void ShowPreviousPage()
    {
        GoToPage(currentPageIndex - 1, true);
    }

    private void ShowNextPage()
    {
        GoToPage(currentPageIndex + 1, true);
    }

    private void GoToPage(int targetIndex, bool animate)
    {
        if (!uiBound || pages.Length == 0)
        {
            return;
        }

        int clampedIndex = Mathf.Clamp(targetIndex, 0, pages.Length - 1);
        if (clampedIndex == currentPageIndex && pageTransitionCoroutine == null)
        {
            UpdateArrowState();
            return;
        }

        currentPageIndex = clampedIndex;
        SyncSelectionState();
        float targetX = GetPageOffset(currentPageIndex);

        if (pageTransitionCoroutine != null)
        {
            StopCoroutine(pageTransitionCoroutine);
            pageTransitionCoroutine = null;
        }

        if (!animate || !Application.isPlaying)
        {
            SetContainerPosition(targetX);
            UpdateArrowState();
            return;
        }

        UpdateArrowState();
        pageTransitionCoroutine = StartCoroutine(AnimateToPage(targetX));
    }

    private IEnumerator AnimateToPage(float targetX)
    {
        float duration = Mathf.Max(0.01f, pageTransitionDuration);
        float elapsed = 0f;
        float startX = pageContainer.anchoredPosition.x;

        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            float eased = Mathf.SmoothStep(0f, 1f, t);
            SetContainerPosition(Mathf.Lerp(startX, targetX, eased));
            yield return null;
        }

        SetContainerPosition(targetX);
        pageTransitionCoroutine = null;
        UpdateArrowState();
    }

    private void SnapToPage()
    {
        if (pages.Length == 0)
        {
            CachePages();
        }

        if (pages.Length == 0)
        {
            return;
        }

        SetContainerPosition(GetPageOffset(currentPageIndex));
        UpdateArrowState();
    }

    private float GetPageOffset(int pageIndex)
    {
        float pageWidth = pageViewport != null ? pageViewport.rect.width : 0f;
        return -pageWidth * pageIndex;
    }

    private void SetContainerPosition(float x)
    {
        if (pageContainer == null)
        {
            return;
        }

        Vector2 anchoredPosition = pageContainer.anchoredPosition;
        anchoredPosition.x = x;
        anchoredPosition.y = 0f;
        pageContainer.anchoredPosition = anchoredPosition;
    }

    private void UpdateArrowState()
    {
        int pageCount = pages != null ? pages.Length : 0;

        if (previousButton != null)
        {
            previousButton.gameObject.SetActive(pageCount > 1 && currentPageIndex > 0);
        }

        if (nextButton != null)
        {
            nextButton.gameObject.SetActive(pageCount > 1 && currentPageIndex < pageCount - 1);
        }
    }

    private void SetDifficulty(int difficultyIndex)
    {
        if (difficultyButtons == null || difficultyButtons.Length == 0)
        {
            return;
        }

        currentDifficultyIndex = Mathf.Clamp(difficultyIndex, 0, difficultyButtons.Length - 1);
        SyncSelectionState();
        UpdateDifficultyState();
    }

    private void UpdateDifficultyState()
    {
        if (difficultyButtons == null)
        {
            return;
        }

        for (int i = 0; i < difficultyButtons.Length; i++)
        {
            Button button = difficultyButtons[i];
            if (button == null)
            {
                continue;
            }

            bool isSelected = i == currentDifficultyIndex;
            Image image = button.GetComponent<Image>();
            if (image != null)
            {
                image.color = isSelected ? new Color(0.85f, 0.88f, 0.93f, 1f) : Color.white;
            }

            RectTransform buttonRect = button.transform as RectTransform;
            if (buttonRect != null)
            {
                buttonRect.localScale = isSelected ? Vector3.one * 1.03f : Vector3.one;
            }

            Text label = button.GetComponentInChildren<Text>(true);
            if (label != null)
            {
                label.color = isSelected ? new Color(0.06f, 0.08f, 0.11f, 1f) : new Color(0.15f, 0.15f, 0.15f, 1f);
            }
        }
    }

    private void OpenBattleScene()
    {
        SyncSelectionState();
        GameFlowController.StartBattle(GameFlowController.CurrentMode);
    }

    private void SyncSelectionState()
    {
        if (!Application.isPlaying || pages.Length == 0)
        {
            return;
        }

        StageSelectionState selectionState = StageSelectionState.EnsureInitialized();
        if (selectionState == null)
        {
            return;
        }

        selectionState.SetSelection(
            GetStageIdForPage(currentPageIndex),
            GetStageNameForPage(currentPageIndex),
            currentDifficultyIndex);
    }

    private void CloseScene()
    {
        if (SceneManager.sceneCount > 1)
        {
            SceneManager.UnloadSceneAsync(gameObject.scene);
            return;
        }

        if (!string.IsNullOrWhiteSpace(exitSceneName))
        {
            SceneManager.LoadScene(exitSceneName);
        }
    }

    private int FindPageIndex(string stageId, string stageName)
    {
        for (int i = 0; i < pages.Length; i++)
        {
            string pageStageId = GetStageIdForPage(i);
            if (!string.IsNullOrWhiteSpace(stageId) && pageStageId == stageId)
            {
                return i;
            }

            string pageStageName = GetStageNameForPage(i);
            if (!string.IsNullOrWhiteSpace(stageName) &&
                string.Equals(pageStageName, stageName, System.StringComparison.OrdinalIgnoreCase))
            {
                return i;
            }
        }

        return -1;
    }

    private string GetStageIdForPage(int pageIndex)
    {
        string stageName = GetStageNameForPage(pageIndex);
        string normalizedStageName = NormalizeStageKey(stageName);
        if (string.IsNullOrEmpty(normalizedStageName))
        {
            normalizedStageName = "stage";
        }

        return $"stage_{pageIndex + 1:00}_{normalizedStageName}";
    }

    private string GetStageNameForPage(int pageIndex)
    {
        if (pageIndex < 0 || pageIndex >= pages.Length || pages[pageIndex] == null)
        {
            return string.Empty;
        }

        Text stageNameLabel = pages[pageIndex].Find("StageNameLabel")?.GetComponent<Text>();
        return stageNameLabel != null ? stageNameLabel.text.Trim() : string.Empty;
    }

    private static string NormalizeStageKey(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        StringBuilder builder = new StringBuilder();
        bool lastWasSeparator = false;
        string lowered = value.Trim().ToLowerInvariant();

        for (int i = 0; i < lowered.Length; i++)
        {
            char character = lowered[i];
            if (char.IsLetterOrDigit(character))
            {
                builder.Append(character);
                lastWasSeparator = false;
            }
            else if (!lastWasSeparator && builder.Length > 0)
            {
                builder.Append('_');
                lastWasSeparator = true;
            }
        }

        return builder.ToString().Trim('_');
    }

#if UNITY_EDITOR
    public void SetReferences(
        Canvas newCanvas,
        RectTransform newPageViewport,
        RectTransform newPageContainer,
        Button newCloseButton,
        Button newPreviousButton,
        Button newNextButton,
        Button[] newDifficultyButtons)
    {
        canvas = newCanvas;
        pageViewport = newPageViewport;
        pageContainer = newPageContainer;
        closeButton = newCloseButton;
        previousButton = newPreviousButton;
        nextButton = newNextButton;
        difficultyButtons = newDifficultyButtons;
    }
#endif
}
