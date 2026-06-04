using System;
using UnityEngine;
using UnityEngine.UI;
#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
#endif

public class HUDPresenter : MonoBehaviour
{
    private const string HudRootName = "GeneratedHUD";
    private const string ControlHintText = "A / D left-right   W / S up-down   Space / Left click fire   Right click / Missile button fire missile   R restart";

    public event Action RetryRequested;
    public event Action QuitRequested;

    [SerializeField] private bool showDebugPanel = true;

    private BossController bossController;
    private PlayerCombatController playerCombatController;
    private PlayerOrbitController playerOrbitController;

    private GameObject hudRoot;
    private Image bossFillImage;
    private RectTransform bossFillRect;
    private Text bossText;
    private Image hullFillImage;
    private RectTransform hullFillRect;
    private Image armorFillImage;
    private RectTransform armorFillRect;
    private Text hullText;
    private Text armorText;
    private Text playerInfoText;
    private GameObject debugPanelRoot;
    private Text stageDebugText;
    private Text statusText;
    private Text hintText;
    private Button missileButton;
    private Image missileButtonImage;
    private Text missileButtonLabel;
    private GameObject missionFailedOverlay;
    private Button retryButton;
    private Button quitButton;
    private StageSelectionState stageSelectionState;
    private bool uiBuilt;
    private bool missingAuthoredUiWarningLogged;

    private string statusMessage = string.Empty;
    private float statusTimer;

    private void Awake()
    {
        EnsureUiReferences();
    }

    private void OnEnable()
    {
        EnsureUiReferences();
    }

    private void Start()
    {
        EnsureUiReferences();
    }

    private void Update()
    {
        EnsureUiReferences();

        if (bossController != null && bossFillImage != null)
        {
            SetBarFill(
                bossFillRect,
                bossController.MaxHealth > 0f
                    ? bossController.CurrentHealth / bossController.MaxHealth
                    : 0f);
        }

        if (bossController != null && bossText != null)
        {
            bossText.text = $"Boss HP  {Mathf.CeilToInt(bossController.CurrentHealth)} / {Mathf.CeilToInt(bossController.MaxHealth)}";
        }

        if (playerCombatController != null)
        {
            if (hullFillImage != null)
            {
                SetBarFill(
                    hullFillRect,
                    playerCombatController.MaxHull > 0f
                        ? playerCombatController.CurrentHull / playerCombatController.MaxHull
                        : 0f);
            }

            if (armorFillImage != null)
            {
                SetBarFill(
                    armorFillRect,
                    playerCombatController.MaxArmor > 0f
                        ? playerCombatController.CurrentArmor / playerCombatController.MaxArmor
                        : 0f);
            }

            if (hullText != null)
            {
                hullText.text = $"HP  {Mathf.CeilToInt(playerCombatController.CurrentHull)} / {Mathf.CeilToInt(playerCombatController.MaxHull)}";
            }

            if (armorText != null)
            {
                string armorState = playerCombatController.ArmorBroken ? "  BROKEN" : string.Empty;
                armorText.text = $"Armor  {Mathf.CeilToInt(playerCombatController.CurrentArmor)} / {Mathf.CeilToInt(playerCombatController.MaxArmor)}{armorState}";
            }
        }

        if (playerOrbitController != null && playerInfoText != null)
        {
            playerInfoText.text = $"Boss Range  {playerOrbitController.CurrentDistance:F1}";
        }

        UpdateStageDebugText();
        RefreshDebugPanelState();

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
        EnsureUiReferences();
        bossController = boss;
        playerCombatController = player;
        playerOrbitController = orbit;
        stageSelectionState = StageSelectionState.EnsureInitialized();
    }

    public void SetStatusMessage(string message)
    {
        statusMessage = message;
        statusTimer = 3f;
    }

    public void SetDebugPanelVisible(bool visible)
    {
        showDebugPanel = visible;
        RefreshDebugPanelState();
    }

    public void ShowMissionFailedOverlay()
    {
        EnsureUiReferences();
        if (missionFailedOverlay != null)
        {
            missionFailedOverlay.SetActive(true);
        }
    }

    public void HideMissionFailedOverlay()
    {
        EnsureUiReferences();
        if (missionFailedOverlay != null)
        {
            missionFailedOverlay.SetActive(false);
        }
    }

#if UNITY_EDITOR
    [ContextMenu("Rebuild Authored HUD UI")]
    public void RebuildAuthoredUiForEditor()
    {
        if (Application.isPlaying)
        {
            return;
        }

        Canvas canvas = ResolveCanvas();
        if (canvas == null)
        {
            Debug.LogWarning("HUDPresenter could not find a Canvas to rebuild authored UI.", this);
            return;
        }

        Transform existingRoot = canvas.transform.Find(HudRootName);
        if (existingRoot != null)
        {
            DestroyImmediate(existingRoot.gameObject);
        }

        ClearUiReferences();
        BuildHudUi(canvas.transform);
        ResolveAuthoredUiReferences(canvas.transform);
        WireUiEvents();

        if (missionFailedOverlay != null)
        {
            missionFailedOverlay.SetActive(false);
        }

        if (hintText != null)
        {
            hintText.text = ControlHintText;
        }

        RefreshDebugPanelState();
        uiBuilt = true;
        EditorUtility.SetDirty(canvas.gameObject);
        EditorUtility.SetDirty(gameObject);
        EditorSceneManager.MarkSceneDirty(gameObject.scene);
    }
#endif

    private void EnsureUiReferences()
    {
        if (uiBuilt)
        {
            return;
        }

        Canvas canvas = ResolveCanvas();
        if (canvas == null)
        {
            return;
        }

        bool foundAuthoredUi = ResolveAuthoredUiReferences(canvas.transform);
        if (!foundAuthoredUi)
        {
            if (Application.isPlaying && !missingAuthoredUiWarningLogged)
            {
                Debug.LogWarning(
                    "HUDPresenter could not find authored HUD UI under BattleCanvas/GeneratedHUD. " +
                    "Runtime fallback is intentionally disabled, so no HUD will be created automatically.",
                    this);
                missingAuthoredUiWarningLogged = true;
            }

            return;
        }

        missingAuthoredUiWarningLogged = false;
        WireUiEvents();
        if (missionFailedOverlay != null)
        {
            missionFailedOverlay.SetActive(false);
        }

        if (hintText != null)
        {
            hintText.text = ControlHintText;
        }

        RefreshDebugPanelState();
        uiBuilt = true;
    }

    private Canvas ResolveCanvas()
    {
        Canvas canvas = GetComponent<Canvas>();
        if (canvas == null)
        {
            canvas = GetComponentInParent<Canvas>();
        }

        return canvas;
    }

    private bool ResolveAuthoredUiReferences(Transform canvasTransform)
    {
        if (canvasTransform == null)
        {
            return false;
        }

        Transform hudRootTransform = canvasTransform.Find(HudRootName);
        if (hudRootTransform == null)
        {
            return false;
        }

        hudRoot = hudRootTransform.gameObject;
        bossFillImage = FindUiComponent<Image>(hudRootTransform, "BossBarBackground/BossBarFillArea/BossBarFill");
        bossFillRect = bossFillImage != null ? bossFillImage.rectTransform : null;
        bossText = FindUiComponent<Text>(hudRootTransform, "BossLabel");

        hullFillImage = FindUiComponent<Image>(hudRootTransform, "PlayerStatusRoot/HullBar/HullBarFillArea/HullBarFill");
        hullFillRect = hullFillImage != null ? hullFillImage.rectTransform : null;
        hullText = FindUiComponent<Text>(hudRootTransform, "PlayerStatusRoot/HullBar/HullBarLabel");

        armorFillImage = FindUiComponent<Image>(hudRootTransform, "PlayerStatusRoot/ArmorBar/ArmorBarFillArea/ArmorBarFill");
        armorFillRect = armorFillImage != null ? armorFillImage.rectTransform : null;
        armorText = FindUiComponent<Text>(hudRootTransform, "PlayerStatusRoot/ArmorBar/ArmorBarLabel");

        playerInfoText = FindUiComponent<Text>(hudRootTransform, "PlayerStatusRoot/PlayerInfoLabel");
        statusText = FindUiComponent<Text>(hudRootTransform, "StatusLabel");
        hintText = FindUiComponent<Text>(hudRootTransform, "HintLabel");
        debugPanelRoot = FindUiTransform(hudRootTransform, "DebugPanelRoot")?.gameObject;
        stageDebugText = FindUiComponent<Text>(hudRootTransform, "DebugPanelRoot/StageDebugLabel");
        if (stageDebugText == null)
        {
            stageDebugText = FindUiComponent<Text>(hudRootTransform, "StageDebugLabel");
            if (stageDebugText != null)
            {
                debugPanelRoot = stageDebugText.transform.parent.gameObject;
            }
        }

        missileButton = FindUiComponent<Button>(hudRootTransform, "MissileButton");
        missileButtonImage = FindUiComponent<Image>(hudRootTransform, "MissileButton");
        missileButtonLabel = FindUiComponent<Text>(hudRootTransform, "MissileButton/MissileButtonLabel");

        missionFailedOverlay = FindUiTransform(hudRootTransform, "MissionFailedOverlay")?.gameObject;
        retryButton = FindUiComponent<Button>(hudRootTransform, "MissionFailedOverlay/Panel/RetryButton");
        quitButton = FindUiComponent<Button>(hudRootTransform, "MissionFailedOverlay/Panel/QuitButton");

        return bossFillImage != null &&
               bossText != null &&
               hullFillImage != null &&
               armorFillImage != null &&
               playerInfoText != null &&
               statusText != null &&
               hintText != null &&
               stageDebugText != null &&
               missileButton != null &&
               missileButtonLabel != null &&
               missionFailedOverlay != null &&
               retryButton != null &&
               quitButton != null;
    }

    private void WireUiEvents()
    {
        if (missileButton != null)
        {
            missileButton.onClick.RemoveListener(FireMissile);
            missileButton.onClick.AddListener(FireMissile);
        }

        if (retryButton != null)
        {
            retryButton.onClick.RemoveListener(HandleRetryButtonClicked);
            retryButton.onClick.AddListener(HandleRetryButtonClicked);
        }

        if (quitButton != null)
        {
            quitButton.onClick.RemoveListener(HandleQuitButtonClicked);
            quitButton.onClick.AddListener(HandleQuitButtonClicked);
        }
    }

    private void ClearUiReferences()
    {
        uiBuilt = false;
        hudRoot = null;
        bossFillImage = null;
        bossFillRect = null;
        bossText = null;
        hullFillImage = null;
        hullFillRect = null;
        armorFillImage = null;
        armorFillRect = null;
        hullText = null;
        armorText = null;
        playerInfoText = null;
        debugPanelRoot = null;
        stageDebugText = null;
        statusText = null;
        hintText = null;
        missileButton = null;
        missileButtonImage = null;
        missileButtonLabel = null;
        missionFailedOverlay = null;
        retryButton = null;
        quitButton = null;
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

    private void BuildHudUi(Transform canvasTransform)
    {
        // Editor rebuild path only.
        // Runtime fallback is disabled so the source of truth stays explicit in the scene canvas.
        Font runtimeFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

        GameObject runtimeHudRoot = FindOrCreateUiObject(HudRootName, canvasTransform);
        RectTransform hudRootRect = runtimeHudRoot.GetComponent<RectTransform>();
        hudRootRect.anchorMin = Vector2.zero;
        hudRootRect.anchorMax = Vector2.one;
        hudRootRect.offsetMin = Vector2.zero;
        hudRootRect.offsetMax = Vector2.zero;

        GameObject barBackground = FindOrCreateUiObject("BossBarBackground", runtimeHudRoot.transform);
        Image backgroundImage = barBackground.GetComponent<Image>() ?? barBackground.AddComponent<Image>();
        backgroundImage.color = new Color(0.08f, 0.12f, 0.18f, 0.85f);
        RectTransform backgroundRect = barBackground.GetComponent<RectTransform>();
        backgroundRect.anchorMin = new Vector2(0.5f, 1f);
        backgroundRect.anchorMax = new Vector2(0.5f, 1f);
        backgroundRect.pivot = new Vector2(0.5f, 1f);
        backgroundRect.sizeDelta = new Vector2(560f, 28f);
        backgroundRect.anchoredPosition = new Vector2(0f, -28f);

        GameObject bossFillArea = FindOrCreateUiObject("BossBarFillArea", barBackground.transform);
        RectTransform bossFillAreaRect = bossFillArea.GetComponent<RectTransform>();
        bossFillAreaRect.anchorMin = Vector2.zero;
        bossFillAreaRect.anchorMax = Vector2.one;
        bossFillAreaRect.offsetMin = new Vector2(4f, 4f);
        bossFillAreaRect.offsetMax = new Vector2(-4f, -4f);

        GameObject barFill = FindOrCreateUiObject("BossBarFill", bossFillArea.transform);
        Image createdBossFillImage = barFill.GetComponent<Image>() ?? barFill.AddComponent<Image>();
        createdBossFillImage.color = new Color(0.85f, 0.28f, 0.28f, 1f);
        RectTransform createdBossFillRect = barFill.GetComponent<RectTransform>();
        createdBossFillRect.anchorMin = Vector2.zero;
        createdBossFillRect.anchorMax = Vector2.one;
        createdBossFillRect.offsetMin = Vector2.zero;
        createdBossFillRect.offsetMax = Vector2.zero;

        Text createdBossText = CreateText("BossLabel", runtimeHudRoot.transform, runtimeFont, TextAnchor.MiddleCenter, 20, Color.white);
        RectTransform bossTextRect = createdBossText.rectTransform;
        bossTextRect.anchorMin = new Vector2(0.5f, 1f);
        bossTextRect.anchorMax = new Vector2(0.5f, 1f);
        bossTextRect.pivot = new Vector2(0.5f, 1f);
        bossTextRect.sizeDelta = new Vector2(620f, 32f);
        bossTextRect.anchoredPosition = new Vector2(0f, -62f);

        GameObject playerStatusRoot = FindOrCreateUiObject("PlayerStatusRoot", runtimeHudRoot.transform);
        RectTransform playerStatusRect = playerStatusRoot.GetComponent<RectTransform>();
        playerStatusRect.anchorMin = new Vector2(0f, 1f);
        playerStatusRect.anchorMax = new Vector2(0f, 1f);
        playerStatusRect.pivot = new Vector2(0f, 1f);
        playerStatusRect.sizeDelta = new Vector2(420f, 124f);
        playerStatusRect.anchoredPosition = new Vector2(24f, -24f);

        CreateStatusBar(
            "HullBar",
            playerStatusRoot.transform,
            runtimeFont,
            new Vector2(0f, 1f),
            new Vector2(0f, 1f),
            new Vector2(0f, 1f),
            new Vector2(0f, 0f),
            new Vector2(360f, 28f),
            new Color(0.22f, 0.07f, 0.07f, 0.9f),
            new Color(0.86f, 0.2f, 0.2f, 1f),
            out _,
            out _,
            out _);

        CreateStatusBar(
            "ArmorBar",
            playerStatusRoot.transform,
            runtimeFont,
            new Vector2(0f, 1f),
            new Vector2(0f, 1f),
            new Vector2(0f, 1f),
            new Vector2(0f, -36f),
            new Vector2(360f, 28f),
            new Color(0.05f, 0.1f, 0.18f, 0.9f),
            new Color(0.2f, 0.5f, 0.94f, 1f),
            out _,
            out _,
            out _);

        Text createdPlayerInfoText = CreateText("PlayerInfoLabel", playerStatusRoot.transform, runtimeFont, TextAnchor.UpperLeft, 18, Color.white);
        RectTransform playerInfoRect = createdPlayerInfoText.rectTransform;
        playerInfoRect.anchorMin = new Vector2(0f, 1f);
        playerInfoRect.anchorMax = new Vector2(0f, 1f);
        playerInfoRect.pivot = new Vector2(0f, 1f);
        playerInfoRect.sizeDelta = new Vector2(360f, 28f);
        playerInfoRect.anchoredPosition = new Vector2(0f, -76f);

        Text createdStatusText = CreateText("StatusLabel", runtimeHudRoot.transform, runtimeFont, TextAnchor.MiddleCenter, 22, new Color(1f, 0.88f, 0.62f));
        RectTransform statusRect = createdStatusText.rectTransform;
        statusRect.anchorMin = new Vector2(0.5f, 1f);
        statusRect.anchorMax = new Vector2(0.5f, 1f);
        statusRect.pivot = new Vector2(0.5f, 1f);
        statusRect.sizeDelta = new Vector2(720f, 40f);
        statusRect.anchoredPosition = new Vector2(0f, -98f);

        Text createdHintText = CreateText("HintLabel", runtimeHudRoot.transform, runtimeFont, TextAnchor.LowerCenter, 18, new Color(0.78f, 0.86f, 0.96f));
        RectTransform hintRect = createdHintText.rectTransform;
        hintRect.anchorMin = new Vector2(0.5f, 0f);
        hintRect.anchorMax = new Vector2(0.5f, 0f);
        hintRect.pivot = new Vector2(0.5f, 0f);
        hintRect.sizeDelta = new Vector2(960f, 32f);
        hintRect.anchoredPosition = new Vector2(0f, 18f);
        createdHintText.text = ControlHintText;

        GameObject createdDebugPanelRoot = FindOrCreateUiObject("DebugPanelRoot", runtimeHudRoot.transform);
        Image createdDebugPanelImage = createdDebugPanelRoot.GetComponent<Image>() ?? createdDebugPanelRoot.AddComponent<Image>();
        createdDebugPanelImage.color = new Color(0.05f, 0.08f, 0.12f, 0.52f);
        RectTransform debugPanelRect = createdDebugPanelRoot.GetComponent<RectTransform>();
        debugPanelRect.anchorMin = new Vector2(0f, 0f);
        debugPanelRect.anchorMax = new Vector2(0f, 0f);
        debugPanelRect.pivot = new Vector2(0f, 0f);
        debugPanelRect.sizeDelta = new Vector2(740f, 132f);
        debugPanelRect.anchoredPosition = new Vector2(16f, 16f);

        Text createdStageDebugText = CreateText("StageDebugLabel", createdDebugPanelRoot.transform, runtimeFont, TextAnchor.LowerLeft, 16, new Color(0.88f, 0.93f, 0.98f, 0.96f));
        RectTransform stageDebugRect = createdStageDebugText.rectTransform;
        stageDebugRect.anchorMin = Vector2.zero;
        stageDebugRect.anchorMax = Vector2.one;
        stageDebugRect.pivot = new Vector2(0f, 0f);
        stageDebugRect.offsetMin = new Vector2(12f, 10f);
        stageDebugRect.offsetMax = new Vector2(-12f, -10f);

        CreateAnchoredButton(
            "MissileButton",
            runtimeHudRoot.transform,
            runtimeFont,
            "MISSILE\nREADY",
            new Vector2(1f, 0f),
            new Vector2(1f, 0f),
            new Vector2(1f, 0f),
            new Vector2(-28f, 28f),
            new Vector2(208f, 74f),
            new Color(0.82f, 0.38f, 0.16f, 0.96f),
            FireMissile,
            out _,
            out _);

        GameObject createdMissionFailedOverlay = FindOrCreateUiObject("MissionFailedOverlay", runtimeHudRoot.transform);
        Image overlayImage = createdMissionFailedOverlay.GetComponent<Image>() ?? createdMissionFailedOverlay.AddComponent<Image>();
        overlayImage.color = new Color(0.02f, 0.03f, 0.05f, 0.74f);
        RectTransform overlayRect = createdMissionFailedOverlay.GetComponent<RectTransform>();
        overlayRect.anchorMin = Vector2.zero;
        overlayRect.anchorMax = Vector2.one;
        overlayRect.offsetMin = Vector2.zero;
        overlayRect.offsetMax = Vector2.zero;

        GameObject panel = FindOrCreateUiObject("Panel", createdMissionFailedOverlay.transform);
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

        createdMissionFailedOverlay.SetActive(false);
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

    private static Transform FindUiTransform(Transform root, string relativePath)
    {
        return root != null ? root.Find(relativePath) : null;
    }

    private static T FindUiComponent<T>(Transform root, string relativePath) where T : Component
    {
        Transform target = FindUiTransform(root, relativePath);
        return target != null ? target.GetComponent<T>() : null;
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

            if (Application.isPlaying)
            {
                Destroy(existing.gameObject);
            }
            else
            {
                DestroyImmediate(existing.gameObject);
            }
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

    private static void SetBarFill(RectTransform fillRect, float ratio)
    {
        if (fillRect == null)
        {
            return;
        }

        float clampedRatio = Mathf.Clamp01(ratio);
        fillRect.anchorMin = Vector2.zero;
        fillRect.anchorMax = new Vector2(clampedRatio, 1f);
        fillRect.offsetMin = Vector2.zero;
        fillRect.offsetMax = Vector2.zero;
    }

    private void RefreshDebugPanelState()
    {
        if (debugPanelRoot != null)
        {
            debugPanelRoot.SetActive(showDebugPanel);
        }
    }

    private void UpdateStageDebugText()
    {
        if (stageDebugText == null)
        {
            return;
        }

        stageSelectionState ??= StageSelectionState.EnsureInitialized();

        string stageId = !string.IsNullOrWhiteSpace(stageSelectionState.SelectedStageId)
            ? stageSelectionState.SelectedStageId
            : "(unset)";
        string stageName = !string.IsNullOrWhiteSpace(stageSelectionState.SelectedStageName)
            ? stageSelectionState.SelectedStageName
            : "(unset)";
        string difficultyLabel = $"{stageSelectionState.SelectedDifficultyName} ({stageSelectionState.SelectedDifficultyNumber})";
        string hitDebug = playerCombatController != null
            ? playerCombatController.LastHitDebugSummary
            : "LastHit: player unavailable";

        stageDebugText.text =
            $"SelectedStageId: {stageId}\n" +
            $"SelectedStageName: {stageName}\n" +
            $"SelectedDifficulty: {difficultyLabel}\n" +
            hitDebug;
    }

    private static void CreateStatusBar(
        string name,
        Transform parent,
        Font font,
        Vector2 anchorMin,
        Vector2 anchorMax,
        Vector2 pivot,
        Vector2 anchoredPosition,
        Vector2 size,
        Color backgroundColor,
        Color fillColor,
        out Image fillImage,
        out RectTransform fillRect,
        out Text overlayText)
    {
        GameObject rootObject = FindOrCreateUiObject(name, parent);
        Image backgroundImage = rootObject.GetComponent<Image>() ?? rootObject.AddComponent<Image>();
        backgroundImage.color = backgroundColor;

        RectTransform rootRect = rootObject.GetComponent<RectTransform>();
        rootRect.anchorMin = anchorMin;
        rootRect.anchorMax = anchorMax;
        rootRect.pivot = pivot;
        rootRect.anchoredPosition = anchoredPosition;
        rootRect.sizeDelta = size;

        GameObject fillAreaObject = FindOrCreateUiObject($"{name}FillArea", rootObject.transform);
        RectTransform fillAreaRect = fillAreaObject.GetComponent<RectTransform>();
        fillAreaRect.anchorMin = Vector2.zero;
        fillAreaRect.anchorMax = Vector2.one;
        fillAreaRect.offsetMin = new Vector2(3f, 3f);
        fillAreaRect.offsetMax = new Vector2(-3f, -3f);

        GameObject fillObject = FindOrCreateUiObject($"{name}Fill", fillAreaObject.transform);
        fillImage = fillObject.GetComponent<Image>() ?? fillObject.AddComponent<Image>();
        fillImage.color = fillColor;
        fillRect = fillObject.GetComponent<RectTransform>();
        fillRect.anchorMin = Vector2.zero;
        fillRect.anchorMax = Vector2.one;
        fillRect.offsetMin = Vector2.zero;
        fillRect.offsetMax = Vector2.zero;

        overlayText = CreateText($"{name}Label", rootObject.transform, font, TextAnchor.MiddleCenter, 16, Color.white);
        RectTransform labelRect = overlayText.rectTransform;
        labelRect.anchorMin = Vector2.zero;
        labelRect.anchorMax = Vector2.one;
        labelRect.offsetMin = new Vector2(10f, 0f);
        labelRect.offsetMax = new Vector2(-10f, 0f);
        overlayText.fontStyle = FontStyle.Bold;
    }
}
