using System.Globalization;
using UnityEngine;
using UnityEngine.UI;

public class BattleDamageNumberPresenter : MonoBehaviour
{
    private const string CatalogResourcePath = "Battle/DamageNumbers/DamageNumberSpriteCatalog";
    private const string RootName = "DamageNumberRoot";

    [SerializeField] private Canvas targetCanvas;
    [SerializeField] private DamageNumberSpriteCatalog spriteCatalog;
    [SerializeField] private float digitHeight = 42f;
    [SerializeField] private float digitSpacing = -2f;
    [SerializeField] private float riseDistance = 64f;
    [SerializeField] private float lifetime = 0.85f;
    [SerializeField] private Vector2 startScreenOffset = new(0f, 8f);
    [SerializeField] private Vector2 randomScreenJitter = new(8f, 4f);

    private RectTransform damageRoot;
    private bool missingCatalogWarningLogged;

    public void Configure(Canvas canvas)
    {
        if (canvas != null)
        {
            targetCanvas = canvas;
        }

        EnsureRoot();
    }

    public void ShowDamage(Vector3 worldPosition, float damage, bool critical = false)
    {
        if (damage <= 0f)
        {
            return;
        }

        EnsureRoot();
        EnsureCatalog();
        if (damageRoot == null || spriteCatalog == null)
        {
            return;
        }

        Camera worldCamera = Camera.main;
        if (worldCamera == null)
        {
            worldCamera = FindAnyObjectByType<Camera>();
        }

        if (worldCamera == null)
        {
            return;
        }

        Vector3 screenPosition = worldCamera.WorldToScreenPoint(worldPosition);
        if (screenPosition.z <= 0.01f)
        {
            return;
        }

        Camera canvasCamera = targetCanvas != null && targetCanvas.renderMode != RenderMode.ScreenSpaceOverlay
            ? targetCanvas.worldCamera
            : null;
        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(
                damageRoot,
                screenPosition,
                canvasCamera,
                out Vector2 anchoredPosition))
        {
            return;
        }

        Vector2 jitter = new(
            Random.Range(-randomScreenJitter.x, randomScreenJitter.x),
            Random.Range(-randomScreenJitter.y, randomScreenJitter.y));
        anchoredPosition += startScreenOffset + jitter;

        string damageText = FormatDamage(damage);
        RectTransform numberRoot = BuildNumber(damageText, critical);
        if (numberRoot == null)
        {
            return;
        }

        numberRoot.anchoredPosition = anchoredPosition;
        FloatingDamageNumberRuntime runtime = numberRoot.gameObject.AddComponent<FloatingDamageNumberRuntime>();
        runtime.Play(numberRoot, numberRoot.GetComponent<CanvasGroup>(), anchoredPosition, riseDistance, lifetime);
    }

    private RectTransform BuildNumber(string damageText, bool critical)
    {
        GameObject numberObject = new("DamageNumber");
        RectTransform numberRect = numberObject.AddComponent<RectTransform>();
        numberRect.SetParent(damageRoot, false);
        numberRect.anchorMin = new Vector2(0.5f, 0.5f);
        numberRect.anchorMax = new Vector2(0.5f, 0.5f);
        numberRect.pivot = new Vector2(0.5f, 0.5f);
        numberObject.AddComponent<CanvasGroup>();

        float[] widths = new float[damageText.Length];
        float totalWidth = 0f;
        for (int i = 0; i < damageText.Length; i++)
        {
            if (!char.IsDigit(damageText[i]))
            {
                continue;
            }

            Sprite sprite = spriteCatalog.GetDigitSprite(damageText[i] - '0', critical);
            if (sprite == null)
            {
                Destroy(numberObject);
                return null;
            }

            float scale = digitHeight / Mathf.Max(1f, sprite.rect.height);
            float width = sprite.rect.width * scale;
            widths[i] = width;
            totalWidth += width;
        }

        totalWidth += Mathf.Max(0, damageText.Length - 1) * digitSpacing;
        numberRect.sizeDelta = new Vector2(Mathf.Max(1f, totalWidth), digitHeight);

        float cursor = -totalWidth * 0.5f;
        for (int i = 0; i < damageText.Length; i++)
        {
            if (!char.IsDigit(damageText[i]))
            {
                continue;
            }

            Sprite sprite = spriteCatalog.GetDigitSprite(damageText[i] - '0', critical);
            float width = widths[i];

            GameObject digitObject = new($"Digit_{damageText[i]}");
            RectTransform digitRect = digitObject.AddComponent<RectTransform>();
            digitRect.SetParent(numberRect, false);
            digitRect.anchorMin = new Vector2(0.5f, 0.5f);
            digitRect.anchorMax = new Vector2(0.5f, 0.5f);
            digitRect.pivot = new Vector2(0.5f, 0.5f);
            digitRect.sizeDelta = new Vector2(width, digitHeight);
            digitRect.anchoredPosition = new Vector2(cursor + width * 0.5f, 0f);

            Image image = digitObject.AddComponent<Image>();
            image.sprite = sprite;
            image.preserveAspect = true;
            image.raycastTarget = false;

            cursor += width + digitSpacing;
        }

        numberRect.SetAsLastSibling();
        return numberRect;
    }

    private void EnsureRoot()
    {
        if (damageRoot != null)
        {
            return;
        }

        if (targetCanvas == null)
        {
            targetCanvas = GetComponent<Canvas>();
        }

        if (targetCanvas == null)
        {
            targetCanvas = GetComponentInParent<Canvas>();
        }

        if (targetCanvas == null)
        {
            targetCanvas = FindAnyObjectByType<Canvas>();
        }

        if (targetCanvas == null)
        {
            return;
        }

        Transform existingRoot = targetCanvas.transform.Find(RootName);
        if (existingRoot != null)
        {
            damageRoot = existingRoot as RectTransform;
            return;
        }

        GameObject rootObject = new(RootName);
        damageRoot = rootObject.AddComponent<RectTransform>();
        damageRoot.SetParent(targetCanvas.transform, false);
        damageRoot.anchorMin = Vector2.zero;
        damageRoot.anchorMax = Vector2.one;
        damageRoot.offsetMin = Vector2.zero;
        damageRoot.offsetMax = Vector2.zero;
        damageRoot.pivot = new Vector2(0.5f, 0.5f);
        rootObject.AddComponent<CanvasRenderer>();
        damageRoot.SetAsLastSibling();
    }

    private void EnsureCatalog()
    {
        if (spriteCatalog != null)
        {
            return;
        }

        spriteCatalog = Resources.Load<DamageNumberSpriteCatalog>(CatalogResourcePath);
        if (spriteCatalog == null && !missingCatalogWarningLogged)
        {
            Debug.LogWarning($"Damage number sprite catalog was not found at Resources/{CatalogResourcePath}.", this);
            missingCatalogWarningLogged = true;
        }
    }

    private static string FormatDamage(float damage)
    {
        return Mathf.Max(0, Mathf.RoundToInt(damage)).ToString(CultureInfo.InvariantCulture);
    }
}

public class FloatingDamageNumberRuntime : MonoBehaviour
{
    private RectTransform rectTransform;
    private CanvasGroup canvasGroup;
    private Vector2 startPosition;
    private float riseDistance;
    private float lifetime;
    private float elapsed;

    public void Play(
        RectTransform targetRectTransform,
        CanvasGroup targetCanvasGroup,
        Vector2 anchoredPosition,
        float upwardDistance,
        float duration)
    {
        rectTransform = targetRectTransform;
        canvasGroup = targetCanvasGroup;
        startPosition = anchoredPosition;
        riseDistance = Mathf.Max(0f, upwardDistance);
        lifetime = Mathf.Max(0.05f, duration);
        elapsed = 0f;

        if (canvasGroup != null)
        {
            canvasGroup.alpha = 1f;
            canvasGroup.interactable = false;
            canvasGroup.blocksRaycasts = false;
        }
    }

    private void Update()
    {
        if (rectTransform == null)
        {
            Destroy(gameObject);
            return;
        }

        elapsed += Time.deltaTime;
        float t = Mathf.Clamp01(elapsed / lifetime);
        float riseT = 1f - Mathf.Pow(1f - t, 2f);
        rectTransform.anchoredPosition = startPosition + Vector2.up * (riseDistance * riseT);

        if (canvasGroup != null)
        {
            canvasGroup.alpha = 1f - Mathf.SmoothStep(0f, 1f, t);
        }

        if (t >= 1f)
        {
            Destroy(gameObject);
        }
    }
}
