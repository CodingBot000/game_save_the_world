using System;
using UnityEngine;
using UnityEngine.UI;
#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
#endif

public class HUDPresenter : MonoBehaviour
{
    private const float ShootErrorDisplayDuration = 3f;
    private const string HudRootName = "GeneratedHUD";
    private const string MusicObjectName = "BattleArenaMusic";
    private const string PlayerStatusBaseSpritePath = "Assets/Art/UI/Battle/HUD/player_hp_armor_base.png";
    private const string PlayerHullFillSpritePath = "Assets/Art/UI/Battle/HUD/player_hp_fill.png";
    private const string PlayerArmorFillSpritePath = "Assets/Art/UI/Battle/HUD/player_armor_fill.png";
    private const string BossBaseSpritePath = "Assets/Art/UI/Battle/HUD/boss_hp_base.png";
    private const string BossFillSpritePath = "Assets/Art/UI/Battle/HUD/boss_hp_fill.png";
    private const string ControlHintText = "A / D left-right   W / S up-down   Space / Left click fire   Hold Right click / LOCK ON, release to attack   R restart";
    private static readonly Vector2 MissileButtonAnchoredPosition = new(-196f, 28f);
    private static readonly Vector2 MissileButtonSize = new(208f, 74f);
    private static readonly Vector2 SpecialButtonAnchoredPosition = new(-28f, 28f);
    private static readonly Vector2 SpecialButtonSize = new(156f, 74f);
    private static readonly Vector2 PlayerStatusBaseSourceSize = new(592f, 232f);
    private static readonly Vector2 PlayerStatusBaseDisplaySize = new(414f, 162.2f);
    private static readonly Vector2 PlayerHullFillSourceOffset = new(118f, 17f);
    private static readonly Vector2 PlayerHullFillSourceSize = new(422f, 71f);
    private static readonly Vector2 PlayerArmorFillSourceOffset = new(153f, 60f);
    private static readonly Vector2 PlayerArmorFillSourceSize = new(362f, 77f);
    private static readonly Vector2 BossBaseSourceSize = new(780f, 107f);
    private static readonly Vector2 BossBaseDisplaySize = new(620f, 85f);
    private static readonly Vector2 BossFillSourceOffset = new(52f, 31f);
    private static readonly Vector2 BossFillSourceSize = new(673f, 38f);

    public event Action RetryRequested;
    public event Action QuitRequested;

    [SerializeField] private bool showDebugPanel = true;
    [SerializeField] private AudioClip battleMusicClip;
    [SerializeField, Range(0f, 1f)] private float battleMusicVolume = 0.7f;

    private BossController bossController;
    private PlayerCombatController playerCombatController;
    private PlayerOrbitController playerOrbitController;
    private PlayerSpecialAttackController specialAttackController;
    private PlayerLockOnController lockOnController;
    private LockOnHudPresenter lockOnHudPresenter;

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
    private Button specialButton;
    private Image specialButtonImage;
    private Text specialButtonLabel;
    private Button musicButton;
    private Image musicButtonImage;
    private Text musicButtonLabel;
    private AudioSource battleMusicSource;
    private GameObject missionFailedOverlay;
    private Button retryButton;
    private Button quitButton;
    private StageSelectionState stageSelectionState;
    private bool uiBuilt;
    private bool missingAuthoredUiWarningLogged;

    private string statusMessage = string.Empty;
    private float statusTimer;
    private bool musicEnabled;
    private bool shootErrorVisible;
    private float shootErrorRemaining;
    private string statusMessageBeforeShootError = string.Empty;
    private float statusTimerBeforeShootError;
    private int statusFontSizeBeforeShootError;
    private FontStyle statusFontStyleBeforeShootError;
    private Color statusColorBeforeShootError;
    private bool statusRaycastBeforeShootError;
    private Outline shootErrorOutline;
    private bool shootErrorOutlineAdded;
    private bool shootErrorOutlineEnabledBefore;
    private Color shootErrorOutlineColorBefore;
    private Vector2 shootErrorOutlineDistanceBefore;

    public Canvas RuntimeCanvas => ResolveCanvas();

    private void Awake()
    {
        RestoreRuntimeAudioOutput();
        ResolveBattleMusicSource();
        SetMusicEnabled(true, updateStatus: false);
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
                bossFillImage,
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
                    hullFillImage,
                    hullFillRect,
                    playerCombatController.MaxHull > 0f
                        ? playerCombatController.CurrentHull / playerCombatController.MaxHull
                        : 0f);
            }

            if (armorFillImage != null)
            {
                SetBarFill(
                    armorFillImage,
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

        if (shootErrorVisible)
        {
            shootErrorRemaining = Mathf.Max(0f, shootErrorRemaining - Time.deltaTime);
            if (shootErrorRemaining <= 0f)
            {
                ClearShootError();
            }
        }

        if (!shootErrorVisible && statusTimer > 0f)
        {
            statusTimer -= Time.deltaTime;
        }
        else if (!shootErrorVisible && bossController != null && playerCombatController != null)
        {
            statusMessage = bossController.IsAlive && playerCombatController.IsAlive
                ? "Battle active"
                : statusMessage;
        }

        if (statusText != null)
        {
            statusText.text = statusMessage;
        }

        if (lockOnController == null)
        {
            UpdateMissileButtonState();
            UpdateSpecialButtonState();
        }
        UpdateMusicButtonState();
    }

    public void Configure(
        BossController boss,
        PlayerCombatController player,
        PlayerOrbitController orbit,
        PlayerSpecialAttackController specialAttack = null)
    {
        EnsureUiReferences();
        bossController = boss;
        playerCombatController = player;
        playerOrbitController = orbit;
        specialAttackController = specialAttack;
        stageSelectionState = StageSelectionState.EnsureInitialized();
    }

    public void ConfigureLockOnController(PlayerLockOnController controller)
    {
        EnsureUiReferences();
        lockOnController = controller;
        if (lockOnController == null)
        {
            return;
        }

        if (missileButton != null)
        {
            missileButton.onClick.RemoveListener(FireMissile);
        }

        if (specialButton != null)
        {
            specialButton.onClick.RemoveListener(FireSpecial);
        }

        lockOnHudPresenter = GetComponent<LockOnHudPresenter>() ??
                             gameObject.AddComponent<LockOnHudPresenter>();
        lockOnHudPresenter.Configure(
            lockOnController,
            RuntimeCanvas,
            missileButton,
            missileButtonImage,
            missileButtonLabel,
            specialButton != null ? specialButton.gameObject : null);
        if (hintText != null)
        {
            hintText.text = ControlHintText;
        }
    }

    public void SetStatusMessage(string message)
    {
        ClearShootError(restorePreviousStatus: false);
        statusMessage = message;
        statusTimer = 3f;
    }

    public void ShowShootError(string reason)
    {
        EnsureUiReferences();
        if (statusText == null)
        {
            return;
        }

        if (!shootErrorVisible)
        {
            statusMessageBeforeShootError = statusMessage;
            statusTimerBeforeShootError = statusTimer;
            statusFontSizeBeforeShootError = statusText.fontSize;
            statusFontStyleBeforeShootError = statusText.fontStyle;
            statusColorBeforeShootError = statusText.color;
            statusRaycastBeforeShootError = statusText.raycastTarget;
            shootErrorOutline = statusText.GetComponent<Outline>();
            if (shootErrorOutline == null)
            {
                shootErrorOutline = statusText.gameObject.AddComponent<Outline>();
                shootErrorOutlineAdded = true;
            }
            else
            {
                shootErrorOutlineAdded = false;
            }

            shootErrorOutlineEnabledBefore = shootErrorOutline.enabled;
            shootErrorOutlineColorBefore = shootErrorOutline.effectColor;
            shootErrorOutlineDistanceBefore = shootErrorOutline.effectDistance;
        }

        shootErrorVisible = true;
        shootErrorRemaining = ShootErrorDisplayDuration;
        statusMessage = "SHOOT ERROR";
        statusTimer = 0f;
        ApplyShootErrorStyle();
    }

    public void ClearShootError()
    {
        ClearShootError(restorePreviousStatus: true);
    }

    public bool IsShootErrorVisible => shootErrorVisible;
    public string DebugStatusText => statusText != null ? statusText.text : string.Empty;
    public int DebugStatusFontSize => statusText != null ? statusText.fontSize : 0;

    private void ClearShootError(bool restorePreviousStatus)
    {
        if (!shootErrorVisible)
        {
            return;
        }

        shootErrorVisible = false;
        shootErrorRemaining = 0f;
        if (statusText != null)
        {
            statusText.fontSize = statusFontSizeBeforeShootError;
            statusText.fontStyle = statusFontStyleBeforeShootError;
            statusText.color = statusColorBeforeShootError;
            statusText.raycastTarget = statusRaycastBeforeShootError;
        }

        if (shootErrorOutline != null)
        {
            shootErrorOutline.enabled = shootErrorOutlineAdded
                ? false
                : shootErrorOutlineEnabledBefore;
            shootErrorOutline.effectColor = shootErrorOutlineColorBefore;
            shootErrorOutline.effectDistance = shootErrorOutlineDistanceBefore;
        }

        if (restorePreviousStatus)
        {
            statusMessage = statusMessageBeforeShootError;
            statusTimer = statusTimerBeforeShootError;
        }

        shootErrorOutline = null;
        shootErrorOutlineAdded = false;
    }

    private void ApplyShootErrorStyle()
    {
        if (statusText == null)
        {
            return;
        }

        statusText.text = "SHOOT ERROR";
        statusText.fontSize = 32;
        statusText.fontStyle = FontStyle.Bold;
        statusText.color = new Color32(255, 48, 48, 255);
        statusText.raycastTarget = false;
        if (shootErrorOutline != null)
        {
            shootErrorOutline.enabled = true;
            shootErrorOutline.effectColor = Color.black;
            shootErrorOutline.effectDistance = new Vector2(2f, -2f);
            shootErrorOutline.useGraphicAlpha = true;
        }
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
        EnsureSpecialButton(hudRoot.transform);
        EnsureMusicButton(hudRoot.transform);
        WireUiEvents();
        UpdateSpecialButtonState();
        UpdateMusicButtonState();
        if (missionFailedOverlay != null)
        {
            missionFailedOverlay.transform.SetAsLastSibling();
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
        ConfigureFillImageForRuntime(bossFillImage);
        bossText = FindUiComponent<Text>(hudRootTransform, "BossBarBackground/BossLabel") ??
                   FindUiComponent<Text>(hudRootTransform, "BossLabel");
        ConfigureGaugeLabel(bossText, TextAnchor.MiddleCenter, 18);

        hullFillImage = FindUiComponent<Image>(hudRootTransform, "PlayerStatusRoot/HullBar/HullBarFillArea/HullBarFill");
        hullFillRect = hullFillImage != null ? hullFillImage.rectTransform : null;
        ConfigureFillImageForRuntime(hullFillImage);
        hullText = FindUiComponent<Text>(hudRootTransform, "PlayerStatusRoot/HullBar/HullBarLabel");
        ConfigureGaugeLabel(hullText, TextAnchor.MiddleCenter, 16);

        armorFillImage = FindUiComponent<Image>(hudRootTransform, "PlayerStatusRoot/ArmorBar/ArmorBarFillArea/ArmorBarFill");
        armorFillRect = armorFillImage != null ? armorFillImage.rectTransform : null;
        ConfigureFillImageForRuntime(armorFillImage);
        armorText = FindUiComponent<Text>(hudRootTransform, "PlayerStatusRoot/ArmorBar/ArmorBarLabel");
        ConfigureGaugeLabel(armorText, TextAnchor.MiddleCenter, 16);

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
        specialButton = FindUiComponent<Button>(hudRootTransform, "SpecialButton");
        specialButtonImage = FindUiComponent<Image>(hudRootTransform, "SpecialButton");
        specialButtonLabel = FindUiComponent<Text>(hudRootTransform, "SpecialButton/SpecialButtonLabel");
        musicButton = FindUiComponent<Button>(hudRootTransform, "MusicButton");
        musicButtonImage = FindUiComponent<Image>(hudRootTransform, "MusicButton");
        musicButtonLabel = FindUiComponent<Text>(hudRootTransform, "MusicButton/MusicButtonLabel");

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
            if (lockOnController == null)
            {
                missileButton.onClick.AddListener(FireMissile);
            }
        }

        if (specialButton != null)
        {
            specialButton.onClick.RemoveListener(FireSpecial);
            if (lockOnController == null)
            {
                specialButton.onClick.AddListener(FireSpecial);
            }
        }

        if (musicButton != null)
        {
            musicButton.onClick.RemoveListener(ToggleMusic);
            musicButton.onClick.AddListener(ToggleMusic);
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
        ClearShootError();
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
        specialButton = null;
        specialButtonImage = null;
        specialButtonLabel = null;
        musicButton = null;
        musicButtonImage = null;
        musicButtonLabel = null;
        missionFailedOverlay = null;
        retryButton = null;
        quitButton = null;
    }

    private void OnDisable()
    {
        ClearShootError();
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

    private void EnsureSpecialButton(Transform hudRootTransform)
    {
        if (hudRootTransform == null)
        {
            return;
        }

        if (missileButton != null)
        {
            ApplyAnchoredButtonLayout(
                missileButton.GetComponent<RectTransform>(),
                new Vector2(1f, 0f),
                new Vector2(1f, 0f),
                new Vector2(1f, 0f),
                MissileButtonAnchoredPosition,
                MissileButtonSize);
        }

        Font runtimeFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        if (specialButton == null || specialButtonImage == null || specialButtonLabel == null)
        {
            specialButton = CreateAnchoredButton(
                "SpecialButton",
                hudRootTransform,
                runtimeFont,
                "Special",
                new Vector2(1f, 0f),
                new Vector2(1f, 0f),
                new Vector2(1f, 0f),
                SpecialButtonAnchoredPosition,
                SpecialButtonSize,
                new Color(0.48f, 0.22f, 0.72f, 0.96f),
                FireSpecial,
                out specialButtonImage,
                out specialButtonLabel);
        }
        else
        {
            ApplyAnchoredButtonLayout(
                specialButton.GetComponent<RectTransform>(),
                new Vector2(1f, 0f),
                new Vector2(1f, 0f),
                new Vector2(1f, 0f),
                SpecialButtonAnchoredPosition,
                SpecialButtonSize);
        }

        specialButton.transform.SetAsLastSibling();
    }

    private void UpdateSpecialButtonState()
    {
        if (specialButton == null || specialButtonImage == null || specialButtonLabel == null)
        {
            return;
        }

        if (specialAttackController == null)
        {
            specialButton.interactable = false;
            specialButtonImage.color = new Color(0.28f, 0.24f, 0.32f, 0.9f);
            specialButtonLabel.text = "Special";
            return;
        }

        if (specialAttackController.IsActive)
        {
            specialButton.interactable = false;
            specialButtonImage.color = new Color(0.36f, 0.22f, 0.42f, 0.92f);
            specialButtonLabel.text = "Special\nACTIVE";
            return;
        }

        bool canActivate = specialAttackController.CanActivate();
        specialButton.interactable = canActivate;
        specialButtonImage.color = canActivate
            ? new Color(0.58f, 0.2f, 0.78f, 0.96f)
            : new Color(0.26f, 0.22f, 0.28f, 0.92f);
        specialButtonLabel.text = canActivate ? "Special" : "Special\nLOCKED";
    }

    private void EnsureMusicButton(Transform hudRootTransform)
    {
        if (hudRootTransform == null)
        {
            return;
        }

        if (musicButton != null && musicButtonImage != null && musicButtonLabel != null)
        {
            musicButton.transform.SetAsLastSibling();
            return;
        }

        Font runtimeFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        musicButton = CreateAnchoredButton(
            "MusicButton",
            hudRootTransform,
            runtimeFont,
            "MUSIC\nOFF",
            new Vector2(0f, 0f),
            new Vector2(0f, 0f),
            new Vector2(0f, 0f),
            new Vector2(28f, 28f),
            new Vector2(148f, 56f),
            new Color(0.20f, 0.22f, 0.26f, 0.92f),
            ToggleMusic,
            out musicButtonImage,
            out musicButtonLabel);
        musicButton.transform.SetAsLastSibling();
    }

    private void UpdateMusicButtonState()
    {
        if (musicButton == null || musicButtonImage == null || musicButtonLabel == null)
        {
            return;
        }

        musicButton.interactable = battleMusicSource != null;
        musicButtonImage.color = musicEnabled
            ? new Color(0.18f, 0.48f, 0.44f, 0.96f)
            : new Color(0.20f, 0.22f, 0.26f, 0.92f);
        musicButtonLabel.text = musicEnabled ? "MUSIC\nON" : "MUSIC\nOFF";
    }

    private void BuildHudUi(Transform canvasTransform)
    {
        // Editor rebuild path only.
        // Runtime fallback is disabled so the source of truth stays explicit in the scene canvas.
        Font runtimeFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        Sprite playerStatusBaseSprite = null;
        Sprite playerHullFillSprite = null;
        Sprite playerArmorFillSprite = null;
        Sprite bossBaseSprite = null;
        Sprite bossFillSprite = null;

#if UNITY_EDITOR
        playerStatusBaseSprite = LoadHudSprite(PlayerStatusBaseSpritePath);
        playerHullFillSprite = LoadHudSprite(PlayerHullFillSpritePath);
        playerArmorFillSprite = LoadHudSprite(PlayerArmorFillSpritePath);
        bossBaseSprite = LoadHudSprite(BossBaseSpritePath);
        bossFillSprite = LoadHudSprite(BossFillSpritePath);
#endif

        GameObject runtimeHudRoot = FindOrCreateUiObject(HudRootName, canvasTransform);
        RectTransform hudRootRect = runtimeHudRoot.GetComponent<RectTransform>();
        hudRootRect.anchorMin = Vector2.zero;
        hudRootRect.anchorMax = Vector2.one;
        hudRootRect.offsetMin = Vector2.zero;
        hudRootRect.offsetMax = Vector2.zero;

        GameObject barBackground = FindOrCreateUiObject("BossBarBackground", runtimeHudRoot.transform);
        Image backgroundImage = barBackground.GetComponent<Image>() ?? barBackground.AddComponent<Image>();
        ConfigureHudImage(backgroundImage, bossBaseSprite, new Color(0.08f, 0.12f, 0.18f, 0.85f), false);
        RectTransform backgroundRect = barBackground.GetComponent<RectTransform>();
        backgroundRect.anchorMin = new Vector2(0.5f, 1f);
        backgroundRect.anchorMax = new Vector2(0.5f, 1f);
        backgroundRect.pivot = new Vector2(0.5f, 1f);
        backgroundRect.sizeDelta = BossBaseDisplaySize;
        backgroundRect.anchoredPosition = new Vector2(0f, -18f);

        float bossScale = BossBaseDisplaySize.x / BossBaseSourceSize.x;
        GameObject bossFillArea = FindOrCreateUiObject("BossBarFillArea", barBackground.transform);
        RectTransform bossFillAreaRect = bossFillArea.GetComponent<RectTransform>();
        SetTopLeftLayout(
            bossFillAreaRect,
            BossFillSourceOffset * bossScale,
            BossFillSourceSize * bossScale);

        GameObject barFill = FindOrCreateUiObject("BossBarFill", bossFillArea.transform);
        Image createdBossFillImage = barFill.GetComponent<Image>() ?? barFill.AddComponent<Image>();
        ConfigureHudImage(createdBossFillImage, bossFillSprite, new Color(0.85f, 0.28f, 0.28f, 1f), true);
        RectTransform createdBossFillRect = barFill.GetComponent<RectTransform>();
        createdBossFillRect.anchorMin = Vector2.zero;
        createdBossFillRect.anchorMax = Vector2.one;
        createdBossFillRect.offsetMin = Vector2.zero;
        createdBossFillRect.offsetMax = Vector2.zero;

        Text createdBossText = CreateText("BossLabel", barBackground.transform, runtimeFont, TextAnchor.MiddleCenter, 18, Color.white);
        ConfigureGaugeLabel(createdBossText, TextAnchor.MiddleCenter, 18);
        createdBossText.text = "Boss HP -- / --";
        RectTransform bossTextRect = createdBossText.rectTransform;
        SetTopLeftLayout(
            bossTextRect,
            BossFillSourceOffset * bossScale,
            BossFillSourceSize * bossScale);
        createdBossText.transform.SetAsLastSibling();

        GameObject playerStatusRoot = FindOrCreateUiObject("PlayerStatusRoot", runtimeHudRoot.transform);
        RectTransform playerStatusRect = playerStatusRoot.GetComponent<RectTransform>();
        playerStatusRect.anchorMin = new Vector2(0f, 1f);
        playerStatusRect.anchorMax = new Vector2(0f, 1f);
        playerStatusRect.pivot = new Vector2(0f, 1f);
        playerStatusRect.sizeDelta = new Vector2(PlayerStatusBaseDisplaySize.x, PlayerStatusBaseDisplaySize.y + 28f);
        playerStatusRect.anchoredPosition = new Vector2(24f, -24f);

        GameObject playerStatusBase = FindOrCreateUiObject("PlayerStatusBase", playerStatusRoot.transform);
        Image playerStatusBaseImage = playerStatusBase.GetComponent<Image>() ?? playerStatusBase.AddComponent<Image>();
        ConfigureHudImage(playerStatusBaseImage, playerStatusBaseSprite, new Color(0.08f, 0.12f, 0.18f, 0.9f), false);
        RectTransform playerStatusBaseRect = playerStatusBase.GetComponent<RectTransform>();
        SetTopLeftLayout(playerStatusBaseRect, Vector2.zero, PlayerStatusBaseDisplaySize);

        float playerScale = PlayerStatusBaseDisplaySize.x / PlayerStatusBaseSourceSize.x;
        CreateSpriteStatusBar(
            "HullBar",
            playerStatusRoot.transform,
            runtimeFont,
            PlayerHullFillSourceOffset * playerScale,
            PlayerHullFillSourceSize * playerScale,
            playerHullFillSprite,
            new Color(0.86f, 0.2f, 0.2f, 1f),
            out _,
            out _,
            out _);

        CreateSpriteStatusBar(
            "ArmorBar",
            playerStatusRoot.transform,
            runtimeFont,
            PlayerArmorFillSourceOffset * playerScale,
            PlayerArmorFillSourceSize * playerScale,
            playerArmorFillSprite,
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
        playerInfoRect.anchoredPosition = new Vector2(0f, -166f);

        Text createdStatusText = CreateText("StatusLabel", runtimeHudRoot.transform, runtimeFont, TextAnchor.MiddleCenter, 22, new Color(1f, 0.88f, 0.62f));
        RectTransform statusRect = createdStatusText.rectTransform;
        statusRect.anchorMin = new Vector2(0.5f, 1f);
        statusRect.anchorMax = new Vector2(0.5f, 1f);
        statusRect.pivot = new Vector2(0.5f, 1f);
        statusRect.sizeDelta = new Vector2(720f, 40f);
        statusRect.anchoredPosition = new Vector2(0f, -120f);

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
            MissileButtonAnchoredPosition,
            MissileButtonSize,
            new Color(0.82f, 0.38f, 0.16f, 0.96f),
            FireMissile,
            out _,
            out _);

        CreateAnchoredButton(
            "SpecialButton",
            runtimeHudRoot.transform,
            runtimeFont,
            "Special",
            new Vector2(1f, 0f),
            new Vector2(1f, 0f),
            new Vector2(1f, 0f),
            SpecialButtonAnchoredPosition,
            SpecialButtonSize,
            new Color(0.58f, 0.2f, 0.78f, 0.96f),
            FireSpecial,
            out _,
            out _);

        CreateAnchoredButton(
            "MusicButton",
            runtimeHudRoot.transform,
            runtimeFont,
            "MUSIC\nOFF",
            new Vector2(0f, 0f),
            new Vector2(0f, 0f),
            new Vector2(0f, 0f),
            new Vector2(28f, 28f),
            new Vector2(148f, 56f),
            new Color(0.20f, 0.22f, 0.26f, 0.92f),
            ToggleMusic,
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

    private void FireSpecial()
    {
        if (specialAttackController == null)
        {
            SetStatusMessage("Special attack unavailable.");
            return;
        }

        if (specialAttackController.TryActivate())
        {
            SetStatusMessage("Special attack.");
            return;
        }

        SetStatusMessage(specialAttackController.GetUnavailableReason());
    }

    private void ToggleMusic()
    {
        SetMusicEnabled(!musicEnabled, updateStatus: true);
    }

    private void SetMusicEnabled(bool enabled, bool updateStatus)
    {
        RestoreRuntimeAudioOutput();
        ResolveBattleMusicSource();
        musicEnabled = enabled && battleMusicSource != null && battleMusicSource.clip != null;
        if (battleMusicSource != null)
        {
            battleMusicSource.mute = !musicEnabled;
            if (musicEnabled)
            {
                if (!battleMusicSource.isPlaying)
                {
                    battleMusicSource.Play();
                }
            }
            else
            {
                battleMusicSource.Stop();
            }
        }

        UpdateMusicButtonState();
        if (updateStatus)
        {
            SetStatusMessage(musicEnabled ? "Music on." : "Music off.");
        }
    }

    private void ResolveBattleMusicSource()
    {
        if (battleMusicSource != null)
        {
            ConfigureBattleMusicSource();
            return;
        }

        GameObject musicObject = GameObject.Find(MusicObjectName);
        if (musicObject == null && battleMusicClip != null)
        {
            musicObject = new GameObject(MusicObjectName);
        }

        if (musicObject != null)
        {
            battleMusicSource = musicObject.GetComponent<AudioSource>() ?? musicObject.AddComponent<AudioSource>();
            ConfigureBattleMusicSource();
        }
    }

    private void ConfigureBattleMusicSource()
    {
        if (battleMusicSource == null)
        {
            return;
        }

        if (battleMusicSource.clip == null && battleMusicClip != null)
        {
            battleMusicSource.clip = battleMusicClip;
        }

        RuntimeAudioOutputGuard.PrimeClip(battleMusicSource.clip);
        battleMusicSource.playOnAwake = false;
        battleMusicSource.loop = true;
        RuntimeAudioOutputGuard.ConfigureAlwaysAudible2D(battleMusicSource, battleMusicVolume);
    }

    private static void RestoreRuntimeAudioOutput()
    {
        RuntimeAudioOutputGuard.Restore();
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

    private static void ConfigureGaugeLabel(Text text, TextAnchor alignment, int maxFontSize)
    {
        if (text == null)
        {
            return;
        }

        text.alignment = alignment;
        text.fontSize = maxFontSize;
        text.fontStyle = FontStyle.Bold;
        text.color = Color.white;
        text.raycastTarget = false;
        text.resizeTextForBestFit = true;
        text.resizeTextMinSize = 10;
        text.resizeTextMaxSize = maxFontSize;
        text.horizontalOverflow = HorizontalWrapMode.Wrap;
        text.verticalOverflow = VerticalWrapMode.Truncate;

        Outline outline = text.GetComponent<Outline>() ?? text.gameObject.AddComponent<Outline>();
        outline.effectColor = new Color(0f, 0f, 0f, 0.9f);
        outline.effectDistance = new Vector2(1.4f, -1.4f);
        outline.useGraphicAlpha = true;
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

        ApplyAnchoredButtonLayout(
            buttonObject.GetComponent<RectTransform>(),
            anchorMin,
            anchorMax,
            pivot,
            anchoredPosition,
            size);

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

    private static void ApplyAnchoredButtonLayout(
        RectTransform buttonRect,
        Vector2 anchorMin,
        Vector2 anchorMax,
        Vector2 pivot,
        Vector2 anchoredPosition,
        Vector2 size)
    {
        if (buttonRect == null)
        {
            return;
        }

        buttonRect.anchorMin = anchorMin;
        buttonRect.anchorMax = anchorMax;
        buttonRect.pivot = pivot;
        buttonRect.sizeDelta = size;
        buttonRect.anchoredPosition = anchoredPosition;
    }

#if UNITY_EDITOR
    private static Sprite LoadHudSprite(string path)
    {
        Sprite sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
        if (sprite == null)
        {
            Debug.LogWarning($"HUD sprite was not found at {path}.");
        }

        return sprite;
    }
#endif

    private static void ConfigureHudImage(Image image, Sprite sprite, Color fallbackColor, bool filled)
    {
        if (image == null)
        {
            return;
        }

        image.sprite = sprite;
        image.color = sprite != null ? Color.white : fallbackColor;
        image.raycastTarget = false;
        image.preserveAspect = false;

        if (filled)
        {
            ConfigureFillImageForRuntime(image);
            return;
        }

        image.type = Image.Type.Simple;
        image.fillAmount = 1f;
    }

    private static void ConfigureFillImageForRuntime(Image image)
    {
        if (image == null)
        {
            return;
        }

        image.raycastTarget = false;
        if (image.sprite == null)
        {
            image.type = Image.Type.Simple;
            image.fillAmount = 1f;
            return;
        }

        image.type = Image.Type.Filled;
        image.fillMethod = Image.FillMethod.Horizontal;
        image.fillOrigin = (int)Image.OriginHorizontal.Left;
        image.fillClockwise = true;
        image.fillAmount = 1f;
    }

    private static void SetTopLeftLayout(RectTransform rect, Vector2 offsetFromTopLeft, Vector2 size)
    {
        if (rect == null)
        {
            return;
        }

        rect.anchorMin = new Vector2(0f, 1f);
        rect.anchorMax = new Vector2(0f, 1f);
        rect.pivot = new Vector2(0f, 1f);
        rect.anchoredPosition = new Vector2(offsetFromTopLeft.x, -offsetFromTopLeft.y);
        rect.sizeDelta = size;
    }

    private static void SetBarFill(Image fillImage, RectTransform fillRect, float ratio)
    {
        float clampedRatio = Mathf.Clamp01(ratio);
        if (fillImage != null && fillImage.sprite != null)
        {
            if (fillImage.type != Image.Type.Filled || fillImage.fillMethod != Image.FillMethod.Horizontal)
            {
                ConfigureFillImageForRuntime(fillImage);
            }

            fillImage.fillAmount = clampedRatio;
            if (fillRect != null)
            {
                fillRect.anchorMin = Vector2.zero;
                fillRect.anchorMax = Vector2.one;
                fillRect.offsetMin = Vector2.zero;
                fillRect.offsetMax = Vector2.zero;
            }

            return;
        }

        if (fillRect == null)
        {
            return;
        }

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

    private static void CreateSpriteStatusBar(
        string name,
        Transform parent,
        Font font,
        Vector2 topLeftOffset,
        Vector2 size,
        Sprite fillSprite,
        Color fallbackFillColor,
        out Image fillImage,
        out RectTransform fillRect,
        out Text overlayText)
    {
        GameObject rootObject = FindOrCreateUiObject(name, parent);
        Image backgroundImage = rootObject.GetComponent<Image>() ?? rootObject.AddComponent<Image>();
        backgroundImage.color = Color.clear;
        backgroundImage.raycastTarget = false;

        RectTransform rootRect = rootObject.GetComponent<RectTransform>();
        SetTopLeftLayout(rootRect, topLeftOffset, size);

        GameObject fillAreaObject = FindOrCreateUiObject($"{name}FillArea", rootObject.transform);
        RectTransform fillAreaRect = fillAreaObject.GetComponent<RectTransform>();
        fillAreaRect.anchorMin = Vector2.zero;
        fillAreaRect.anchorMax = Vector2.one;
        fillAreaRect.offsetMin = Vector2.zero;
        fillAreaRect.offsetMax = Vector2.zero;

        GameObject fillObject = FindOrCreateUiObject($"{name}Fill", fillAreaObject.transform);
        fillImage = fillObject.GetComponent<Image>() ?? fillObject.AddComponent<Image>();
        ConfigureHudImage(fillImage, fillSprite, fallbackFillColor, true);
        fillRect = fillObject.GetComponent<RectTransform>();
        fillRect.anchorMin = Vector2.zero;
        fillRect.anchorMax = Vector2.one;
        fillRect.offsetMin = Vector2.zero;
        fillRect.offsetMax = Vector2.zero;

        overlayText = CreateText($"{name}Label", rootObject.transform, font, TextAnchor.MiddleCenter, 16, Color.white);
        ConfigureGaugeLabel(overlayText, TextAnchor.MiddleCenter, 16);
        overlayText.text = name == "ArmorBar" ? "Armor -- / --" : "HP -- / --";
        RectTransform labelRect = overlayText.rectTransform;
        labelRect.anchorMin = Vector2.zero;
        labelRect.anchorMax = Vector2.one;
        labelRect.offsetMin = new Vector2(6f, 0f);
        labelRect.offsetMax = new Vector2(-6f, 0f);
        overlayText.transform.SetAsLastSibling();
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
