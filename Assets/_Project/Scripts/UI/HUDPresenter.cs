using System;
using UnityEngine;
using UnityEngine.UI;

public class HUDPresenter : MonoBehaviour
{
    public event Action RetryRequested;
    public event Action QuitRequested;

    private BossController bossController;
    private PlayerCombatController playerCombatController;
    private PlayerOrbitController playerOrbitController;

    private Image bossFillImage;
    private Text bossText;
    private Text playerText;
    private Text statusText;
    private Text hintText;
    private Button missileButton;
    private Image missileButtonImage;
    private Text missileButtonLabel;
    private GameObject missionFailedOverlay;
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

        UpdateMissileButtonState();
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

    private void UpdateMissileButtonState()
    {
        if (missileButton == null || missileButtonImage == null || missileButtonLabel == null)
        {
            return;
        }

        if (playerCombatController == null)
        {
            missileButton.interactable = false;
            missileButtonImage.color = new Color(0.28f, 0.28f, 0.32f, 0.9f);
            missileButtonLabel.text = "MISSILE";
            return;
        }

        bool hasLaunchers = playerCombatController.HasMissileLaunchers;
        bool systemAvailable = playerCombatController.MissileSystemAvailable;
        bool ready = playerCombatController.MissileReady;
        bool canLaunch = playerCombatController.MissileInputAvailable;
        missileButton.interactable = canLaunch;

        if (!hasLaunchers)
        {
            missileButtonImage.color = new Color(0.22f, 0.22f, 0.24f, 0.92f);
            missileButtonLabel.text = "MISSILE\nOFFLINE";
            return;
        }

        if (!systemAvailable)
        {
            missileButtonImage.color = new Color(0.22f, 0.22f, 0.24f, 0.92f);
            missileButtonLabel.text = "MISSILE\nLOCKED";
            return;
        }

        float cooldownRemaining = playerCombatController.MissileCooldownRemaining;
        missileButtonImage.color = ready
            ? new Color(0.82f, 0.38f, 0.16f, 0.96f)
            : new Color(0.38f, 0.34f, 0.32f, 0.94f);
        missileButtonLabel.text = ready
            ? "MISSILE\nREADY"
            : $"MISSILE\n{cooldownRemaining:0.0}s";
    }

    public void ShowMissionFailedOverlay()
    {
        if (missionFailedOverlay != null)
        {
            missionFailedOverlay.SetActive(true);
        }
    }

    public void HideMissionFailedOverlay()
    {
        if (missionFailedOverlay != null)
        {
            missionFailedOverlay.SetActive(false);
        }
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
        hintText.text = "Camera auto-orbit   A / D strafe   W / S up-down   Q / Z forward-back   Space / Left click fire   Missile button bottom-right   R restart";

        missileButton = CreateAnchoredButton(
            "MissileButton",
            hudRoot.transform,
            runtimeFont,
            "MISSILE\nREADY",
            new Vector2(1f, 0f),
            new Vector2(1f, 0f),
            new Vector2(1f, 0f),
            new Vector2(-28f, 28f),
            new Vector2(208f, 74f),
            new Color(0.82f, 0.38f, 0.16f, 0.96f),
            FireMissile,
            out missileButtonImage,
            out missileButtonLabel);

        missionFailedOverlay = FindOrCreateUiObject("MissionFailedOverlay", hudRoot.transform);
        Image overlayImage = missionFailedOverlay.GetComponent<Image>() ?? missionFailedOverlay.AddComponent<Image>();
        overlayImage.color = new Color(0.02f, 0.03f, 0.05f, 0.74f);
        RectTransform overlayRect = missionFailedOverlay.GetComponent<RectTransform>();
        overlayRect.anchorMin = Vector2.zero;
        overlayRect.anchorMax = Vector2.one;
        overlayRect.offsetMin = Vector2.zero;
        overlayRect.offsetMax = Vector2.zero;

        GameObject panel = FindOrCreateUiObject("Panel", missionFailedOverlay.transform);
        Image panelImage = panel.GetComponent<Image>() ?? panel.AddComponent<Image>();
        panelImage.color = new Color(0.1f, 0.12f, 0.18f, 0.96f);
        RectTransform panelRect = panel.GetComponent<RectTransform>();
        panelRect.anchorMin = new Vector2(0.5f, 0.5f);
        panelRect.anchorMax = new Vector2(0.5f, 0.5f);
        panelRect.pivot = new Vector2(0.5f, 0.5f);
        panelRect.sizeDelta = new Vector2(460f, 240f);
        panelRect.anchoredPosition = Vector2.zero;

        Text titleText = CreateText("Title", panel.transform, runtimeFont, TextAnchor.MiddleCenter, 34, Color.white);
        titleText.text = "Mission Failed";
        RectTransform titleRect = titleText.rectTransform;
        titleRect.anchorMin = new Vector2(0.5f, 1f);
        titleRect.anchorMax = new Vector2(0.5f, 1f);
        titleRect.pivot = new Vector2(0.5f, 1f);
        titleRect.sizeDelta = new Vector2(360f, 48f);
        titleRect.anchoredPosition = new Vector2(0f, -36f);

        Text bodyText = CreateText("Body", panel.transform, runtimeFont, TextAnchor.MiddleCenter, 18, new Color(0.82f, 0.86f, 0.92f));
        bodyText.text = "The titan remains active.\nRegroup and try the assault again.";
        RectTransform bodyRect = bodyText.rectTransform;
        bodyRect.anchorMin = new Vector2(0.5f, 0.5f);
        bodyRect.anchorMax = new Vector2(0.5f, 0.5f);
        bodyRect.pivot = new Vector2(0.5f, 0.5f);
        bodyRect.sizeDelta = new Vector2(360f, 56f);
        bodyRect.anchoredPosition = new Vector2(0f, 8f);

        CreateButton(
            "RetryButton",
            panel.transform,
            runtimeFont,
            "Retry",
            new Vector2(-92f, -72f),
            new Color(0.16f, 0.62f, 0.44f),
            HandleRetryButtonClicked);

        CreateButton(
            "QuitButton",
            panel.transform,
            runtimeFont,
            "Quit",
            new Vector2(92f, -72f),
            new Color(0.72f, 0.24f, 0.2f),
            HandleQuitButtonClicked);

        missionFailedOverlay.SetActive(false);

        uiBuilt = true;
    }

    private void HandleRetryButtonClicked()
    {
        RetryRequested?.Invoke();
    }

    private void FireMissile()
    {
        if (playerCombatController == null)
        {
            return;
        }

        if (playerCombatController.TryFireMissile())
        {
            SetStatusMessage("Missile away.");
            return;
        }

        SetStatusMessage(playerCombatController.GetMissileUnavailableReason());
    }

    private void HandleQuitButtonClicked()
    {
        QuitRequested?.Invoke();
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
        buttonRect.sizeDelta = new Vector2(150f, 50f);
        buttonRect.anchoredPosition = anchoredPosition;

        Text labelText = CreateText($"{name}Label", buttonObject.transform, font, TextAnchor.MiddleCenter, 20, Color.white);
        RectTransform labelRect = labelText.rectTransform;
        labelRect.anchorMin = Vector2.zero;
        labelRect.anchorMax = Vector2.one;
        labelRect.offsetMin = Vector2.zero;
        labelRect.offsetMax = Vector2.zero;
        labelText.text = label;

        return button;
    }

    private static Button CreateAnchoredButton(
        string name,
        Transform parent,
        Font font,
        string label,
        Vector2 anchorMin,
        Vector2 anchorMax,
        Vector2 pivot,
        Vector2 anchoredPosition,
        Vector2 size,
        Color color,
        UnityEngine.Events.UnityAction onClick,
        out Image buttonImage,
        out Text buttonLabel)
    {
        GameObject buttonObject = FindOrCreateUiObject(name, parent);
        buttonImage = buttonObject.GetComponent<Image>() ?? buttonObject.AddComponent<Image>();
        buttonImage.color = color;

        Button button = buttonObject.GetComponent<Button>() ?? buttonObject.AddComponent<Button>();
        button.targetGraphic = buttonImage;
        button.onClick.RemoveAllListeners();
        button.onClick.AddListener(onClick);

        RectTransform buttonRect = buttonObject.GetComponent<RectTransform>();
        buttonRect.anchorMin = anchorMin;
        buttonRect.anchorMax = anchorMax;
        buttonRect.pivot = pivot;
        buttonRect.sizeDelta = size;
        buttonRect.anchoredPosition = anchoredPosition;

        buttonLabel = CreateText($"{name}Label", buttonObject.transform, font, TextAnchor.MiddleCenter, 19, Color.white);
        buttonLabel.fontStyle = FontStyle.Bold;
        RectTransform labelRect = buttonLabel.rectTransform;
        labelRect.anchorMin = Vector2.zero;
        labelRect.anchorMax = Vector2.one;
        labelRect.offsetMin = Vector2.zero;
        labelRect.offsetMax = Vector2.zero;
        buttonLabel.text = label;

        return button;
    }
}
