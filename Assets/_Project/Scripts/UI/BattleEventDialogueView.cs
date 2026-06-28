using UnityEngine;
using UnityEngine.UI;

public class BattleEventDialogueView : MonoBehaviour
{
    public enum DialogueMood
    {
        Normal,
        Angry
    }

    [Header("UI References")]
    [SerializeField] private CanvasGroup canvasGroup;
    [SerializeField] private Image bubbleBackground;
    [SerializeField] private Image frameImage;
    [SerializeField] private Image playerShoutImage;

    [Header("Animation Frames")]
    [SerializeField] private Sprite[] normalFrames;
    [SerializeField] private Sprite[] angryFrames;
    [SerializeField, Min(1f)] private float framesPerSecond = 8f;
    [SerializeField] private bool hideOnAwake = true;
    [SerializeField] private bool usePlayerShoutImage = true;
    [SerializeField, Min(0f)] private float playerShoutBounceScale = 0.08f;
    [SerializeField, Min(0.01f)] private float playerShoutBounceFrequency = 2.5f;

    private Sprite[] activeFrames;
    private float visibleRemaining;
    private float frameTimer;
    private float playerShoutBounceTimer;
    private int frameIndex;
    private bool isVisible;

    private void Awake()
    {
        ResolveReferences();

        if (hideOnAwake)
        {
            HideImmediate();
        }
    }

    private void Reset()
    {
        ResolveReferences();
    }

    private void OnValidate()
    {
        ResolveReferences();
    }

    private void Update()
    {
        if (!isVisible)
        {
            return;
        }

        visibleRemaining -= Time.deltaTime;
        if (visibleRemaining <= 0f)
        {
            HideImmediate();
            return;
        }

        AdvanceFrame(Time.deltaTime);
        UpdatePlayerShoutBounce(Time.deltaTime);
    }

    public void ShowNormal(float duration)
    {
        Show(DialogueMood.Normal, duration);
    }

    public void ShowAngry(float duration)
    {
        Show(DialogueMood.Angry, duration);
    }

    public void Show(DialogueMood mood, float duration)
    {
        ResolveReferences();

        bool showingPlayerShout = usePlayerShoutImage && playerShoutImage != null;
        activeFrames = showingPlayerShout ? null : mood == DialogueMood.Angry ? angryFrames : normalFrames;
        visibleRemaining = Mathf.Max(0.01f, duration);
        frameTimer = 0f;
        playerShoutBounceTimer = 0f;
        frameIndex = 0;
        isVisible = true;

        if (canvasGroup != null)
        {
            canvasGroup.alpha = 1f;
            canvasGroup.interactable = false;
            canvasGroup.blocksRaycasts = false;
        }

        SetLegacyDialogueActive(!showingPlayerShout);
        SetPlayerShoutVisible(showingPlayerShout);

        if (!showingPlayerShout)
        {
            ApplyCurrentFrame();
        }
    }

    public void HideImmediate()
    {
        isVisible = false;
        visibleRemaining = 0f;
        frameTimer = 0f;
        playerShoutBounceTimer = 0f;
        frameIndex = 0;

        if (canvasGroup != null)
        {
            canvasGroup.alpha = 0f;
            canvasGroup.interactable = false;
            canvasGroup.blocksRaycasts = false;
        }

        if (frameImage != null)
        {
            frameImage.enabled = false;
        }

        SetLegacyDialogueActive(false);
        SetPlayerShoutVisible(false);
        ResetPlayerShoutScale();
    }

    private void AdvanceFrame(float deltaTime)
    {
        if (activeFrames == null || activeFrames.Length <= 1 || frameImage == null)
        {
            return;
        }

        frameTimer += deltaTime;
        float frameDuration = 1f / Mathf.Max(1f, framesPerSecond);

        while (frameTimer >= frameDuration)
        {
            frameTimer -= frameDuration;
            frameIndex = (frameIndex + 1) % activeFrames.Length;
            ApplyCurrentFrame();
        }
    }

    private void ApplyCurrentFrame()
    {
        if (frameImage == null)
        {
            return;
        }

        if (activeFrames == null || activeFrames.Length == 0)
        {
            frameImage.enabled = false;
            return;
        }

        Sprite sprite = activeFrames[Mathf.Clamp(frameIndex, 0, activeFrames.Length - 1)];
        frameImage.sprite = sprite;
        frameImage.preserveAspect = true;
        frameImage.enabled = sprite != null;
    }

    private void SetLegacyDialogueActive(bool active)
    {
        if (bubbleBackground != null)
        {
            bubbleBackground.enabled = active;
            bubbleBackground.gameObject.SetActive(active);
        }

        if (frameImage != null)
        {
            Transform frameRoot = frameImage.transform.parent;
            if (frameRoot != null)
            {
                frameRoot.gameObject.SetActive(active);
            }

            if (!active)
            {
                frameImage.enabled = false;
            }
        }
    }

    private void SetPlayerShoutVisible(bool visible)
    {
        if (playerShoutImage == null)
        {
            return;
        }

        ApplyPlayerShoutRectForTopRightPivot();
        if (visible)
        {
            ResetPlayerShoutScale();
        }

        playerShoutImage.enabled = visible;
        playerShoutImage.gameObject.SetActive(visible);
    }

    private void UpdatePlayerShoutBounce(float deltaTime)
    {
        if (!usePlayerShoutImage || playerShoutImage == null || !playerShoutImage.gameObject.activeSelf)
        {
            return;
        }

        playerShoutBounceTimer += deltaTime;
        float wave = Mathf.Sin(playerShoutBounceTimer * Mathf.PI * 2f * Mathf.Max(0.01f, playerShoutBounceFrequency));
        float bounceT = 0.5f + wave * 0.5f;
        float scale = 1f + Mathf.SmoothStep(0f, 1f, bounceT) * Mathf.Max(0f, playerShoutBounceScale);
        playerShoutImage.rectTransform.localScale = Vector3.one * scale;
    }

    private void ResetPlayerShoutScale()
    {
        if (playerShoutImage != null)
        {
            playerShoutImage.rectTransform.localScale = Vector3.one;
        }
    }

    private void ApplyPlayerShoutRectForTopRightPivot()
    {
        if (playerShoutImage == null)
        {
            return;
        }

        RectTransform rect = playerShoutImage.rectTransform;
        rect.anchorMin = Vector2.one;
        rect.anchorMax = Vector2.one;
        rect.pivot = Vector2.one;
        rect.anchoredPosition = Vector2.zero;
    }

    private void ResolveReferences()
    {
        if (canvasGroup == null)
        {
            canvasGroup = GetComponent<CanvasGroup>();
        }

        if (bubbleBackground == null)
        {
            Transform foundBubble = transform.Find("BubbleBackground");
            if (foundBubble != null)
            {
                bubbleBackground = foundBubble.GetComponent<Image>();
            }
        }

        if (frameImage == null)
        {
            Transform foundFrame = transform.Find("FrameAnimationRoot/FrameImage");
            if (foundFrame != null)
            {
                frameImage = foundFrame.GetComponent<Image>();
            }
        }

        if (playerShoutImage == null)
        {
            Transform foundPlayerShout = transform.Find("PlayerShoutImage");
            if (foundPlayerShout != null)
            {
                playerShoutImage = foundPlayerShout.GetComponent<Image>();
            }
        }
    }
}
