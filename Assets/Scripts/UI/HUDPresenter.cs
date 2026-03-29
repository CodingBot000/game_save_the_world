using UnityEngine;
using UnityEngine.UI;

public class HUDPresenter : MonoBehaviour
{
    private BossController bossController;
    private PlayerCombatController playerCombatController;
    private PlayerOrbitController playerOrbitController;

    private Image bossFillImage;
    private Text bossText;
    private Text playerText;
    private Text statusText;
    private Text hintText;
    private bool uiBuilt;

    private string statusMessage = string.Empty;
    private float statusTimer;

    private void Awake()
    {
        EnsureRuntimeUi();
    }

    private void OnEnable()
    {
        EnsureRuntimeUi();
    }

    private void Start()
    {
        EnsureRuntimeUi();
    }

    private void Update()
    {
        if (bossController != null && bossFillImage != null)
        {
            bossFillImage.fillAmount = bossController.MaxHealth > 0f
                ? bossController.CurrentHealth / bossController.MaxHealth
                : 0f;
        }

        if (bossController != null && bossText != null)
        {
            bossText.text = $"Boss HP  {Mathf.CeilToInt(bossController.CurrentHealth)} / {Mathf.CeilToInt(bossController.MaxHealth)}";
        }

        if (playerCombatController != null && playerOrbitController != null && playerText != null)
        {
            playerText.text =
                $"Player HP  {Mathf.CeilToInt(playerCombatController.CurrentHealth)} / {Mathf.CeilToInt(playerCombatController.MaxHealth)}\n" +
                $"Boss Range  {playerOrbitController.CurrentDistance:F1}";
        }

        if (statusTimer > 0f)
        {
            statusTimer -= Time.deltaTime;
        }
        else if (bossController != null && playerCombatController != null)
        {
            statusMessage = bossController.IsAlive && playerCombatController.IsAlive
                ? "Battle active"
                : statusMessage;
        }

        if (statusText != null)
        {
            statusText.text = statusMessage;
        }
    }

    public void Configure(BossController boss, PlayerCombatController player, PlayerOrbitController orbit)
    {
        bossController = boss;
        playerCombatController = player;
        playerOrbitController = orbit;
    }

    public void SetStatusMessage(string message)
    {
        statusMessage = message;
        statusTimer = 3f;
    }

    private void EnsureRuntimeUi()
    {
        if (uiBuilt)
        {
            return;
        }

        Canvas canvas = GetComponent<Canvas>();
        if (canvas == null)
        {
            canvas = GetComponentInParent<Canvas>();
        }

        if (canvas == null)
        {
            return;
        }

        Font runtimeFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

        GameObject hudRoot = FindOrCreateUiObject("GeneratedHUD", canvas.transform);
        RectTransform hudRootRect = hudRoot.GetComponent<RectTransform>();
        hudRootRect.anchorMin = Vector2.zero;
        hudRootRect.anchorMax = Vector2.one;
        hudRootRect.offsetMin = Vector2.zero;
        hudRootRect.offsetMax = Vector2.zero;

        GameObject barBackground = FindOrCreateUiObject("BossBarBackground", hudRoot.transform);
        Image backgroundImage = barBackground.GetComponent<Image>() ?? barBackground.AddComponent<Image>();
        backgroundImage.color = new Color(0.08f, 0.12f, 0.18f, 0.85f);
        RectTransform backgroundRect = barBackground.GetComponent<RectTransform>();
        backgroundRect.anchorMin = new Vector2(0.5f, 1f);
        backgroundRect.anchorMax = new Vector2(0.5f, 1f);
        backgroundRect.pivot = new Vector2(0.5f, 1f);
        backgroundRect.sizeDelta = new Vector2(560f, 28f);
        backgroundRect.anchoredPosition = new Vector2(0f, -28f);

        GameObject barFill = FindOrCreateUiObject("BossBarFill", barBackground.transform);
        bossFillImage = barFill.GetComponent<Image>() ?? barFill.AddComponent<Image>();
        bossFillImage.color = new Color(0.85f, 0.28f, 0.28f, 1f);
        bossFillImage.type = Image.Type.Filled;
        bossFillImage.fillMethod = Image.FillMethod.Horizontal;
        bossFillImage.fillOrigin = 0;
        RectTransform fillRect = barFill.GetComponent<RectTransform>();
        fillRect.anchorMin = Vector2.zero;
        fillRect.anchorMax = Vector2.one;
        fillRect.offsetMin = new Vector2(4f, 4f);
        fillRect.offsetMax = new Vector2(-4f, -4f);

        bossText = CreateText("BossLabel", hudRoot.transform, runtimeFont, TextAnchor.MiddleCenter, 20, Color.white);
        RectTransform bossTextRect = bossText.rectTransform;
        bossTextRect.anchorMin = new Vector2(0.5f, 1f);
        bossTextRect.anchorMax = new Vector2(0.5f, 1f);
        bossTextRect.pivot = new Vector2(0.5f, 1f);
        bossTextRect.sizeDelta = new Vector2(620f, 32f);
        bossTextRect.anchoredPosition = new Vector2(0f, -62f);

        playerText = CreateText("PlayerLabel", hudRoot.transform, runtimeFont, TextAnchor.UpperLeft, 18, Color.white);
        RectTransform playerRect = playerText.rectTransform;
        playerRect.anchorMin = new Vector2(0f, 1f);
        playerRect.anchorMax = new Vector2(0f, 1f);
        playerRect.pivot = new Vector2(0f, 1f);
        playerRect.sizeDelta = new Vector2(380f, 64f);
        playerRect.anchoredPosition = new Vector2(24f, -24f);

        statusText = CreateText("StatusLabel", hudRoot.transform, runtimeFont, TextAnchor.MiddleCenter, 22, new Color(1f, 0.88f, 0.62f));
        RectTransform statusRect = statusText.rectTransform;
        statusRect.anchorMin = new Vector2(0.5f, 1f);
        statusRect.anchorMax = new Vector2(0.5f, 1f);
        statusRect.pivot = new Vector2(0.5f, 1f);
        statusRect.sizeDelta = new Vector2(720f, 40f);
        statusRect.anchoredPosition = new Vector2(0f, -98f);

        hintText = CreateText("HintLabel", hudRoot.transform, runtimeFont, TextAnchor.LowerCenter, 18, new Color(0.78f, 0.86f, 0.96f));
        RectTransform hintRect = hintText.rectTransform;
        hintRect.anchorMin = new Vector2(0.5f, 0f);
        hintRect.anchorMax = new Vector2(0.5f, 0f);
        hintRect.pivot = new Vector2(0.5f, 0f);
        hintRect.sizeDelta = new Vector2(960f, 32f);
        hintRect.anchoredPosition = new Vector2(0f, 18f);
        hintText.text = "Camera auto-orbit   A / D strafe   W / S up-down   Q / Z forward-back   Space / Left click fire   R restart";

        uiBuilt = true;
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

    private static Text CreateText(string name, Transform parent, Font font, TextAnchor alignment, int fontSize, Color color)
    {
        GameObject textObject = FindOrCreateUiObject(name, parent);
        Text text = textObject.GetComponent<Text>() ?? textObject.AddComponent<Text>();
        text.font = font;
        text.alignment = alignment;
        text.fontSize = fontSize;
        text.color = color;
        text.horizontalOverflow = HorizontalWrapMode.Overflow;
        text.verticalOverflow = VerticalWrapMode.Overflow;
        return text;
    }
}
