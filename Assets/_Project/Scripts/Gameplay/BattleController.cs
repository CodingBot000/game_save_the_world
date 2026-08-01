using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

[DefaultExecutionOrder(200)]
public class BattleController : MonoBehaviour
{
    private const string PlayerVisualRootName = "PlayerVisualRoot";
    private const string DamageHurtboxName = "CrashObserver";
    private const float MinUniformVehicleScale = 0.01f;
    private static readonly Vector3 DefaultPlayerVehicleLocalPosition = new(0f, 1f, 0f);
    private static readonly Quaternion DefaultPlayerVehicleLocalRotation = Quaternion.Euler(270.01978f, 0f, 0f);
    private static readonly Vector3 DefaultPlayerVehicleLocalScale =
        PreserveScaleMagnitudeAsUniform(new Vector3(1.442044f, 0.6920179f, 0.396217f));

    [Header("Runtime References")]
    [SerializeField] private BossController bossController;
    [SerializeField] private BossAttackController bossAttackController;
    [SerializeField] private PlayerOrbitController playerOrbitController;
    [SerializeField] private PlayerCombatController playerCombatController;
    [SerializeField] private PlayerLockOnController playerLockOnController;
    [SerializeField] private PlayerMovementBounds playerMovementBounds;
    [SerializeField] private HUDPresenter hudPresenter;
    [SerializeField] private GameObject playerProjectileTemplate;
    [SerializeField] private GameObject bossProjectileTemplate;
    [SerializeField] private GameObject allyPlaceholder;
    [SerializeField] private GameObject backgroundRoot;
    [SerializeField] private BattleBackgroundHost backgroundHost;
    [SerializeField] private Transform stageVisualRoot;
    [SerializeField] private EnvironmentThemeDebugPanel environmentThemeDebugPanel;
    [SerializeField] private PlayerMoveGuide playerMoveGuide;
    [SerializeField] private BattleDamageNumberPresenter damageNumberPresenter;

    private bool battleActive = true;
    private bool awaitingDefeatChoice;
    private BossTestState bossTestState;
    private BossLockOnTargetProvider bossLockOnTargetProvider;
    private BossWeakPointDebugFlash bossWeakPointDebugFlash;
    private BossPhaseDebugHud bossPhaseDebugHud;

    public bool IsBattleActive => battleActive;
    public BossTestState BossTestState => bossTestState;
    public BossLockOnTargetProvider BossLockOnTargetProvider => bossLockOnTargetProvider;
    public PlayerLockOnController PlayerLockOnController => playerLockOnController;

    private void Awake()
    {
        CartoonSmokePuff.ClearAllRuntimeSmokeObjects();
    }

    private void Start()
    {
        CartoonSmokePuff.ClearAllRuntimeSmokeObjects();
        ResolveReferences();
        ApplySelectedPlayerVehicleVisual();
        ApplyModeConfiguration();
        PositionSceneActors();
        ConfigureBackground();
        WireRuntime();
    }

    private void Update()
    {
        if (!battleActive)
        {
            if (awaitingDefeatChoice)
            {
                return;
            }

            Keyboard keyboard = Keyboard.current;
            if (keyboard != null && keyboard.rKey.wasPressedThisFrame)
            {
                SceneManager.LoadScene(SceneManager.GetActiveScene().path);
            }

            return;
        }
    }

    private void LateUpdate()
    {
        if (!battleActive)
        {
            return;
        }

        if (bossController == null || playerCombatController == null)
        {
            return;
        }

        if (bossController.IsAlive && playerCombatController.IsAlive)
        {
            bossController.FaceTarget(playerCombatController.HitPoint);
        }
    }

    public bool TryHitBoss(
        Vector3 worldPoint,
        float hitRadius,
        float damage,
        Collider projectileCollider = null)
    {
        if (!battleActive || bossController == null || !bossController.IsAlive)
        {
            return false;
        }

        if (!bossController.CheckHit(worldPoint, hitRadius, projectileCollider))
        {
            return false;
        }

        return TryApplyBossHitDamage(worldPoint, damage);
    }

    public bool TryHitBoss(
        Vector3 previousWorldPoint,
        Vector3 worldPoint,
        float hitRadius,
        float damage,
        Collider projectileCollider = null)
    {
        if (!battleActive || bossController == null || !bossController.IsAlive)
        {
            return false;
        }

        if (!bossController.CheckHit(previousWorldPoint, worldPoint, hitRadius, projectileCollider))
        {
            return false;
        }

        return TryApplyBossHitDamage(worldPoint, damage);
    }

    private bool TryApplyBossHitDamage(Vector3 worldPoint, float damage)
    {
        float appliedDamage = Mathf.Max(0f, damage);
        bool damageApplied = appliedDamage <= 0f || bossController.ApplyDamage(appliedDamage);
        if (damageApplied && appliedDamage > 0f)
        {
            bossLockOnTargetProvider?.MarkNearestTargetRecentlyAttacked(worldPoint);
            EnsureDamageNumberPresenter();
            damageNumberPresenter?.ShowDamage(worldPoint, appliedDamage, critical: false);
        }

        return damageApplied;
    }

    public bool TryHitPlayer(Vector3 worldPoint, float hitRadius, float damage, Collider projectileCollider = null)
    {
        if (!battleActive || playerCombatController == null || !playerCombatController.IsAlive)
        {
            return false;
        }

        if (!playerCombatController.CheckHit(worldPoint, hitRadius, projectileCollider))
        {
            return false;
        }

        return damage <= 0f || playerCombatController.ApplyDamage(damage);
    }

    public bool TryHitPlayer(
        Vector3 previousWorldPoint,
        Vector3 worldPoint,
        float hitRadius,
        float damage,
        Collider projectileCollider = null)
    {
        if (!battleActive || playerCombatController == null || !playerCombatController.IsAlive)
        {
            return false;
        }

        if (!playerCombatController.CheckHit(previousWorldPoint, worldPoint, hitRadius, projectileCollider))
        {
            return false;
        }

        return damage <= 0f || playerCombatController.ApplyDamage(damage);
    }

    private void ResolveReferences()
    {
        bossController ??= FindSceneComponent<BossController>();
        bossAttackController ??= FindSceneComponent<BossAttackController>();
        playerOrbitController ??= FindSceneComponent<PlayerOrbitController>();
        playerCombatController ??= FindSceneComponent<PlayerCombatController>();
        playerLockOnController ??= FindSceneComponent<PlayerLockOnController>();
        playerMovementBounds ??= FindSceneComponent<PlayerMovementBounds>();
        hudPresenter ??= FindSceneComponent<HUDPresenter>();
        playerProjectileTemplate ??= FindSceneObject("PlayerProjectileTemplate");
        bossProjectileTemplate ??= FindSceneObject("BossProjectileTemplate");
        allyPlaceholder ??= FindSceneObject("AllyPlaceholder");
        backgroundRoot ??= FindSceneObject("BackgroundRoot");
        backgroundHost ??= backgroundRoot != null ? backgroundRoot.GetComponent<BattleBackgroundHost>() : FindSceneComponent<BattleBackgroundHost>();
        stageVisualRoot ??= FindSceneTransform("StageVisualRoot");
        environmentThemeDebugPanel ??= FindSceneComponent<EnvironmentThemeDebugPanel>();
        playerMoveGuide ??= FindSceneComponent<PlayerMoveGuide>();
        damageNumberPresenter ??= FindSceneComponent<BattleDamageNumberPresenter>();

        if (playerLockOnController == null)
        {
            playerLockOnController = gameObject.AddComponent<PlayerLockOnController>();
        }

        EnsureBossLockOnTestSystems();

        EnsureDamageNumberPresenter();

    }

    private void ApplyModeConfiguration()
    {
        if (playerMoveGuide != null)
        {
            playerMoveGuide.gameObject.SetActive(false);
        }

        if (bossController != null)
        {
            float health = GameFlowController.CurrentMode == GameMode.MultiPlaceholder ? 2800f : 2000f;
            bossController.ConfigureEncounter(health);
        }

        if (allyPlaceholder != null)
        {
            bool showAlly = GameFlowController.CurrentMode == GameMode.MultiPlaceholder;
            allyPlaceholder.SetActive(showAlly);
        }
    }

    private void EnsureBossLockOnTestSystems()
    {
        if (bossController == null)
        {
            return;
        }

        bossTestState = bossController.GetComponent<BossTestState>() ??
                        bossController.gameObject.AddComponent<BossTestState>();
        bossLockOnTargetProvider = bossController.GetComponent<BossLockOnTargetProvider>() ??
                                   bossController.gameObject.AddComponent<BossLockOnTargetProvider>();
        bossWeakPointDebugFlash = bossController.GetComponent<BossWeakPointDebugFlash>() ??
                                  bossController.gameObject.AddComponent<BossWeakPointDebugFlash>();

        if (hudPresenter != null)
        {
            bossPhaseDebugHud = hudPresenter.GetComponent<BossPhaseDebugHud>() ??
                                hudPresenter.gameObject.AddComponent<BossPhaseDebugHud>();
        }
    }

    private void EnsureDamageNumberPresenter()
    {
        if (damageNumberPresenter != null)
        {
            return;
        }

        Canvas damageCanvas = hudPresenter != null ? hudPresenter.RuntimeCanvas : FindSceneComponent<Canvas>();
        if (damageCanvas == null)
        {
            return;
        }

        damageNumberPresenter = damageCanvas.GetComponent<BattleDamageNumberPresenter>();
        if (damageNumberPresenter == null)
        {
            damageNumberPresenter = damageCanvas.gameObject.AddComponent<BattleDamageNumberPresenter>();
        }

        damageNumberPresenter.Configure(damageCanvas);
    }

    private void ApplySelectedPlayerVehicleVisual()
    {
        if (playerOrbitController == null)
        {
            return;
        }

        HelicopterSelectionState selectionState = HelicopterSelectionState.EnsureInitialized();
        VehicleDefinition selectedVehicle = selectionState.EnsureSelectedHelicopter();
        if (selectedVehicle == null || selectedVehicle.Prefab == null)
        {
            return;
        }

        Transform playerVisualRoot = playerOrbitController.transform.Find(PlayerVisualRootName);
        if (playerVisualRoot == null)
        {
            GameObject rootObject = new GameObject(PlayerVisualRootName);
            playerVisualRoot = rootObject.transform;
            playerVisualRoot.SetParent(playerOrbitController.transform, false);
            playerVisualRoot.localPosition = Vector3.zero;
            playerVisualRoot.localRotation = Quaternion.identity;
            playerVisualRoot.localScale = Vector3.one;
        }

        Vector3 localPosition = DefaultPlayerVehicleLocalPosition;
        Quaternion localRotation = DefaultPlayerVehicleLocalRotation;
        Vector3 localScale = DefaultPlayerVehicleLocalScale;
        Transform preservedDamageHurtbox = PreserveDamageHurtbox(playerVisualRoot);

        Transform templateVisual = FindVehicleVisualTemplate(playerVisualRoot);
        if (templateVisual != null)
        {
            localPosition = templateVisual.localPosition;
            localRotation = templateVisual.localRotation;
            localScale = PreserveScaleMagnitudeAsUniform(templateVisual.localScale);
        }

        for (int i = playerVisualRoot.childCount - 1; i >= 0; i--)
        {
            DestroyImmediate(playerVisualRoot.GetChild(i).gameObject);
        }

        GameObject vehicleInstance = Instantiate(selectedVehicle.Prefab, playerVisualRoot);
        vehicleInstance.name = selectedVehicle.Prefab.name;
        vehicleInstance.transform.localPosition = localPosition;
        vehicleInstance.transform.localRotation = localRotation;
        vehicleInstance.transform.localScale = PreserveScaleMagnitudeAsUniform(localScale);
        RestoreDamageHurtbox(vehicleInstance.transform, preservedDamageHurtbox);

        playerOrbitController.RefreshVisualBindings();
        if (playerCombatController != null)
        {
            playerCombatController.RefreshVisualBindings();
        }
    }

    private static Transform FindVehicleVisualTemplate(Transform playerVisualRoot)
    {
        if (playerVisualRoot == null)
        {
            return null;
        }

        for (int i = 0; i < playerVisualRoot.childCount; i++)
        {
            Transform child = playerVisualRoot.GetChild(i);
            if (child == null || child.name == DamageHurtboxName)
            {
                continue;
            }

            return child;
        }

        return null;
    }

    private static Vector3 PreserveScaleMagnitudeAsUniform(Vector3 scale)
    {
        float x = Mathf.Abs(scale.x);
        float y = Mathf.Abs(scale.y);
        float z = Mathf.Abs(scale.z);
        float uniformScale = Mathf.Sqrt((x * x + y * y + z * z) / 3f);

        if (uniformScale <= MinUniformVehicleScale)
        {
            uniformScale = Mathf.Max(x, y, z, MinUniformVehicleScale);
        }

        return Vector3.one * uniformScale;
    }

    private static Transform PreserveDamageHurtbox(Transform playerVisualRoot)
    {
        if (playerVisualRoot == null)
        {
            return null;
        }

        Transform damageHurtbox = FindDeepChild(playerVisualRoot, DamageHurtboxName);
        if (damageHurtbox == null)
        {
            return null;
        }

        damageHurtbox.SetParent(playerVisualRoot.parent, true);
        return damageHurtbox;
    }

    private static void RestoreDamageHurtbox(Transform vehicleVisualRoot, Transform damageHurtbox)
    {
        if (vehicleVisualRoot == null || damageHurtbox == null)
        {
            return;
        }

        damageHurtbox.SetParent(vehicleVisualRoot, true);
    }

    private static Transform FindDeepChild(Transform root, string targetName)
    {
        if (root == null || string.IsNullOrWhiteSpace(targetName))
        {
            return null;
        }

        for (int i = 0; i < root.childCount; i++)
        {
            Transform child = root.GetChild(i);
            if (child.name == targetName)
            {
                return child;
            }

            Transform nested = FindDeepChild(child, targetName);
            if (nested != null)
            {
                return nested;
            }
        }

        return null;
    }

    private void PositionSceneActors()
    {
        if (playerOrbitController != null && bossController != null)
        {
            playerOrbitController.Configure(
                bossController.OrbitCenter,
                bossController.AimPoint,
                playerMovementBounds,
                playerMoveGuide,
                playerLockOnController);
            playerOrbitController.AdoptScenePlacement(playerOrbitController.transform.position);
        }

        if (bossController != null)
        {
            bossController.CaptureBasePose();
        }
    }

    private void ConfigureBackground()
    {
        if (backgroundHost == null)
        {
            return;
        }

        // BattleController only provides the stage rotation source.
        // The background host owns the concrete background implementation.
        backgroundHost.BindStageRotationSource(stageVisualRoot);
    }

    private void WireRuntime()
    {
        if (playerCombatController != null)
        {
            playerCombatController.Configure(this, bossController, playerProjectileTemplate);
            playerCombatController.Died += HandlePlayerDied;
        }

        if (bossController != null)
        {
            bossController.Died += HandleBossDied;

            bossLockOnTargetProvider?.Configure(
                bossController,
                bossTestState,
                Camera.main);
            Transform bossVisualRoot = FindDeepChild(bossController.transform, "BossVisualRoot");
            if (bossVisualRoot == null)
            {
                Debug.LogWarning(
                    "BossWeakPointDebugFlash could not find BossVisualRoot; flash targets are left empty.",
                    bossController);
            }

            bossWeakPointDebugFlash?.Configure(bossTestState, bossVisualRoot);
            bossPhaseDebugHud?.Configure(
                bossTestState,
                hudPresenter != null ? hudPresenter.RuntimeCanvas : FindSceneComponent<Canvas>());
        }

        if (bossAttackController != null)
        {
            bossAttackController.Configure(this, bossController, playerCombatController, bossProjectileTemplate, playerOrbitController);
        }

        playerLockOnController?.Configure(
            this,
            playerCombatController,
            bossLockOnTargetProvider,
            GetComponent<PlayerMissileSalvoLauncher>(),
            hudPresenter);

        if (damageNumberPresenter != null)
        {
            damageNumberPresenter.Configure(hudPresenter != null ? hudPresenter.RuntimeCanvas : null);
        }

        if (hudPresenter != null && bossController != null && playerCombatController != null && playerOrbitController != null)
        {
            hudPresenter.Configure(bossController, playerCombatController, playerOrbitController);
            hudPresenter.ConfigureLockOnController(playerLockOnController);
            hudPresenter.RetryRequested += HandleRetryRequested;
            hudPresenter.QuitRequested += HandleQuitRequested;
            string modeLabel = GameFlowController.CurrentMode == GameMode.MultiPlaceholder
                ? "Co-op placeholder mode"
                : "Single battle mode";
            hudPresenter.SetStatusMessage($"{modeLabel}. A/D left-right. W/S up-down.");
        }
    }

    private void HandleBossDied()
    {
        battleActive = false;
        awaitingDefeatChoice = false;

        if (bossAttackController != null)
        {
            bossAttackController.enabled = false;
        }

        if (playerOrbitController != null)
        {
            playerOrbitController.SetInputEnabled(false);
        }

        if (playerCombatController != null)
        {
            playerCombatController.SetCombatEnabled(false);
        }

        if (hudPresenter != null)
        {
            hudPresenter.HideMissionFailedOverlay();
            hudPresenter.SetStatusMessage("Boss defeated. Press R to restart.");
        }
    }

    private void HandlePlayerDied()
    {
        if (GameplayDebugFlags.Undead)
        {
            if (playerCombatController != null)
            {
                playerCombatController.RefillForDebug();
                playerCombatController.SetCombatEnabled(true);
            }

            if (playerOrbitController != null)
            {
                playerOrbitController.SetInputEnabled(true);
            }

            if (bossAttackController != null)
            {
                bossAttackController.enabled = true;
            }

            battleActive = true;
            awaitingDefeatChoice = false;

            if (hudPresenter != null)
            {
                hudPresenter.HideMissionFailedOverlay();
                hudPresenter.SetStatusMessage("Undead debug active. Damage ignored.");
            }

            return;
        }

        battleActive = false;
        awaitingDefeatChoice = true;

        if (bossAttackController != null)
        {
            bossAttackController.enabled = false;
        }

        if (playerOrbitController != null)
        {
            playerOrbitController.SetInputEnabled(false);
        }

        if (playerCombatController != null)
        {
            playerCombatController.SetCombatEnabled(false);
        }

        if (hudPresenter != null)
        {
            hudPresenter.SetStatusMessage("Mission failed.");
            hudPresenter.ShowMissionFailedOverlay();
        }
    }

    private void HandleRetryRequested()
    {
        SceneManager.LoadScene(SceneManager.GetActiveScene().path);
    }

    private void HandleQuitRequested()
    {
        GameFlowController.LoadMainMenu();
    }

    private Transform FindSceneTransform(string objectName)
    {
        GameObject found = FindSceneObject(objectName);
        return found != null ? found.transform : null;
    }

    private GameObject FindSceneObject(string objectName)
    {
        Scene scene = gameObject.scene;
        if (!scene.IsValid())
        {
            return null;
        }

        foreach (GameObject root in scene.GetRootGameObjects())
        {
            Transform[] descendants = root.GetComponentsInChildren<Transform>(true);
            foreach (Transform candidate in descendants)
            {
                if (candidate.name == objectName)
                {
                    return candidate.gameObject;
                }
            }
        }

        return null;
    }

    private T FindSceneComponent<T>() where T : Component
    {
        Scene scene = gameObject.scene;
        if (scene.IsValid())
        {
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                T component = root.GetComponentInChildren<T>(true);
                if (component != null)
                {
                    return component;
                }
            }
        }

        return Object.FindAnyObjectByType<T>();
    }

    private void OnDestroy()
    {
        if (bossController != null)
        {
            bossController.Died -= HandleBossDied;
        }

        if (playerCombatController != null)
        {
            playerCombatController.Died -= HandlePlayerDied;
        }

        if (hudPresenter != null)
        {
            hudPresenter.RetryRequested -= HandleRetryRequested;
            hudPresenter.QuitRequested -= HandleQuitRequested;
        }
    }
}
