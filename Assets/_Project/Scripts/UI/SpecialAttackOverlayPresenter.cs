using System.Collections;
using UnityEngine;
using UnityEngine.UI;

public class SpecialAttackOverlayPresenter : MonoBehaviour
{
    private const string OverlayCanvasName = "SpecialAttackOverlayCanvas";
    private const string OverlayRootName = "SpecialAttackOverlay";
    private const int OverlaySortingOrder = 10000;
    private const float ImageSlideDuration = 0.3f;
    private const float BottomImageDelay = 0.3f;

    private Canvas overlayCanvas;
    private CanvasGroup canvasGroup;
    private SpecialAttackDiagonalImage topImage;
    private SpecialAttackDiagonalImage bottomImage;

    public IEnumerator Play(Canvas canvas, Texture2D topTexture, Texture2D bottomTexture, float duration)
    {
        EnsureOverlay(canvas);
        if (canvasGroup == null)
        {
            yield break;
        }

        topImage.Configure(topTexture, SpecialAttackDiagonalImage.DiagonalHalf.Upper);
        bottomImage.Configure(bottomTexture, SpecialAttackDiagonalImage.DiagonalHalf.Lower);

        canvasGroup.transform.SetAsLastSibling();
        canvasGroup.alpha = 1f;
        canvasGroup.blocksRaycasts = true;
        canvasGroup.interactable = false;
        canvasGroup.gameObject.SetActive(true);
        Canvas.ForceUpdateCanvases();

        yield return PlaySlideInSequence(Mathf.Max(0f, duration));

        canvasGroup.alpha = 0f;
        canvasGroup.blocksRaycasts = false;
        canvasGroup.gameObject.SetActive(false);
    }

    private void EnsureOverlay(Canvas canvas)
    {
        overlayCanvas = EnsureOverlayCanvas(canvas);

        Transform existing = overlayCanvas.transform.Find(OverlayRootName);
        GameObject overlayObject = existing != null ? existing.gameObject : new GameObject(OverlayRootName, typeof(RectTransform));
        overlayObject.transform.SetParent(overlayCanvas.transform, false);
        overlayObject.transform.SetAsLastSibling();

        RectTransform overlayRect = overlayObject.GetComponent<RectTransform>();
        overlayRect.anchorMin = Vector2.zero;
        overlayRect.anchorMax = Vector2.one;
        overlayRect.offsetMin = Vector2.zero;
        overlayRect.offsetMax = Vector2.zero;
        overlayRect.pivot = new Vector2(0.5f, 0.5f);

        canvasGroup = overlayObject.GetComponent<CanvasGroup>();
        if (canvasGroup == null)
        {
            canvasGroup = overlayObject.AddComponent<CanvasGroup>();
        }

        topImage = EnsureHalfImage(overlayObject.transform, "TopScene", SpecialAttackDiagonalImage.DiagonalHalf.Upper);
        bottomImage = EnsureHalfImage(overlayObject.transform, "BottomScene", SpecialAttackDiagonalImage.DiagonalHalf.Lower);
        overlayObject.SetActive(false);
    }

    private static Canvas EnsureOverlayCanvas(Canvas sourceCanvas)
    {
        GameObject canvasObject = GameObject.Find(OverlayCanvasName);
        if (canvasObject == null)
        {
            canvasObject = new GameObject(OverlayCanvasName, typeof(RectTransform));
        }

        Canvas canvas = canvasObject.GetComponent<Canvas>();
        if (canvas == null)
        {
            canvas = canvasObject.AddComponent<Canvas>();
        }

        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.overrideSorting = true;
        canvas.sortingOrder = OverlaySortingOrder;

        CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
        if (scaler == null)
        {
            scaler = canvasObject.AddComponent<CanvasScaler>();
        }

        CanvasScaler sourceScaler = sourceCanvas != null ? sourceCanvas.GetComponent<CanvasScaler>() : null;
        if (sourceScaler != null)
        {
            scaler.uiScaleMode = sourceScaler.uiScaleMode;
            scaler.referenceResolution = sourceScaler.referenceResolution;
            scaler.screenMatchMode = sourceScaler.screenMatchMode;
            scaler.matchWidthOrHeight = sourceScaler.matchWidthOrHeight;
            scaler.referencePixelsPerUnit = sourceScaler.referencePixelsPerUnit;
        }
        else
        {
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0f;
        }

        RectTransform canvasRect = canvasObject.GetComponent<RectTransform>();
        canvasRect.anchorMin = Vector2.zero;
        canvasRect.anchorMax = Vector2.one;
        canvasRect.offsetMin = Vector2.zero;
        canvasRect.offsetMax = Vector2.zero;
        canvasRect.pivot = new Vector2(0.5f, 0.5f);

        return canvas;
    }

    private static SpecialAttackDiagonalImage EnsureHalfImage(
        Transform parent,
        string name,
        SpecialAttackDiagonalImage.DiagonalHalf half)
    {
        Transform existing = parent.Find(name);
        GameObject imageObject = existing != null ? existing.gameObject : new GameObject(name, typeof(RectTransform));
        imageObject.transform.SetParent(parent, false);

        RectTransform imageRect = imageObject.GetComponent<RectTransform>();
        imageRect.anchorMin = Vector2.zero;
        imageRect.anchorMax = Vector2.one;
        imageRect.offsetMin = Vector2.zero;
        imageRect.offsetMax = Vector2.zero;
        imageRect.pivot = new Vector2(0.5f, 0.5f);

        SpecialAttackDiagonalImage image = imageObject.GetComponent<SpecialAttackDiagonalImage>();
        if (image == null)
        {
            image = imageObject.AddComponent<SpecialAttackDiagonalImage>();
        }

        image.raycastTarget = false;
        image.color = Color.white;
        image.Configure(null, half);
        return image;
    }

    private IEnumerator PlaySlideInSequence(float holdDuration)
    {
        RectTransform overlayRect = canvasGroup.transform as RectTransform;
        RectTransform topRect = topImage.rectTransform;
        RectTransform bottomRect = bottomImage.rectTransform;
        Vector2 slideDistance = ResolveSlideDistance(overlayRect);
        Vector2 topStartOffset = new(-slideDistance.x, -slideDistance.y);
        Vector2 bottomStartOffset = new(slideDistance.x, slideDistance.y);

        SetImageOffset(topRect, topStartOffset);
        SetImageOffset(bottomRect, bottomStartOffset);

        float elapsed = 0f;
        float sequenceDuration = BottomImageDelay + ImageSlideDuration;
        while (elapsed < sequenceDuration)
        {
            elapsed += Time.deltaTime;

            float topProgress = Mathf.Clamp01(elapsed / ImageSlideDuration);
            SetImageOffset(topRect, Vector2.Lerp(topStartOffset, Vector2.zero, SmoothProgress(topProgress)));

            float bottomProgress = Mathf.Clamp01((elapsed - BottomImageDelay) / ImageSlideDuration);
            SetImageOffset(bottomRect, Vector2.Lerp(bottomStartOffset, Vector2.zero, SmoothProgress(bottomProgress)));

            yield return null;
        }

        SetImageOffset(topRect, Vector2.zero);
        SetImageOffset(bottomRect, Vector2.zero);
        yield return new WaitForSeconds(holdDuration);
    }

    private static Vector2 ResolveSlideDistance(RectTransform overlayRect)
    {
        Vector2 size = overlayRect != null ? overlayRect.rect.size : new Vector2(Screen.width, Screen.height);
        if (size.x <= 1f)
        {
            size.x = Screen.width;
        }

        if (size.y <= 1f)
        {
            size.y = Screen.height;
        }

        return size * 1.1f;
    }

    private static float SmoothProgress(float progress)
    {
        return Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(progress));
    }

    private static void SetImageOffset(RectTransform imageRect, Vector2 offset)
    {
        if (imageRect == null)
        {
            return;
        }

        imageRect.offsetMin = offset;
        imageRect.offsetMax = offset;
    }
}
