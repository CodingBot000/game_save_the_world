using UnityEngine;
using UnityEngine.UI;

public class BattleEventBroadcastView : MonoBehaviour
{
    public enum BroadcastMood
    {
        Normal,
        Surprise
    }

    [Header("UI References")]
    [SerializeField] private CanvasGroup canvasGroup;
    [SerializeField] private Image reporterImage;

    [Header("Animation Frames")]
    [SerializeField] private Sprite[] normalFrames;
    [SerializeField] private Sprite[] surpriseFrames;
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
        Show(BroadcastMood.Normal, duration);
    }

    public void ShowSurprise(float duration)
    {
        Show(BroadcastMood.Surprise, duration);
    }

    public void Show(BroadcastMood mood, float duration)
    {
        ResolveReferences();

        activeFrames = mood == BroadcastMood.Surprise ? surpriseFrames : normalFrames;
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

        if (reporterImage != null)
        {
            reporterImage.enabled = false;
        }
    }

    private void AdvanceFrame(float deltaTime)
    {
        if (activeFrames == null || activeFrames.Length <= 1 || reporterImage == null)
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
        if (reporterImage == null)
        {
            return;
        }

        if (activeFrames == null || activeFrames.Length == 0)
        {
            reporterImage.enabled = false;
            return;
        }

        Sprite sprite = activeFrames[Mathf.Clamp(frameIndex, 0, activeFrames.Length - 1)];
        reporterImage.sprite = sprite;
        reporterImage.preserveAspect = true;
        reporterImage.enabled = sprite != null;
    }

    private void ResolveReferences()
    {
        if (canvasGroup == null)
        {
            canvasGroup = GetComponent<CanvasGroup>();
        }

        if (reporterImage == null)
        {
            Transform foundFrame = transform.Find("ReporterFrameRoot/ReporterImage");
            if (foundFrame != null)
            {
                reporterImage = foundFrame.GetComponent<Image>();
            }
        }
    }
}
