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

    [Header("Animation Frames")]
    [SerializeField] private Sprite[] normalFrames;
    [SerializeField] private Sprite[] angryFrames;
    [SerializeField, Min(1f)] private float framesPerSecond = 8f;
    [SerializeField] private bool hideOnAwake = true;

    private Sprite[] activeFrames;
    private float visibleRemaining;
    private float frameTimer;
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

        activeFrames = mood == DialogueMood.Angry ? angryFrames : normalFrames;
        visibleRemaining = Mathf.Max(0.01f, duration);
        frameTimer = 0f;
        frameIndex = 0;
        isVisible = true;

        if (canvasGroup != null)
        {
            canvasGroup.alpha = 1f;
            canvasGroup.interactable = false;
            canvasGroup.blocksRaycasts = false;
        }

        if (bubbleBackground != null)
        {
            bubbleBackground.enabled = true;
        }

        ApplyCurrentFrame();
    }

    public void HideImmediate()
    {
        isVisible = false;
        visibleRemaining = 0f;
        frameTimer = 0f;
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
    }
}
