using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

[DefaultExecutionOrder(200)]
public class BattleController : MonoBehaviour
{
    [Header("Runtime References")]
    [SerializeField] private BossController bossController;
    [SerializeField] private BossAttackController bossAttackController;
    [SerializeField] private PlayerOrbitController playerOrbitController;
    [SerializeField] private PlayerCombatController playerCombatController;
    [SerializeField] private ArenaCameraRig arenaCameraRig;
    [SerializeField] private HUDPresenter hudPresenter;
    [SerializeField] private GameObject playerProjectileTemplate;
    [SerializeField] private GameObject bossProjectileTemplate;
    [SerializeField] private GameObject allyPlaceholder;

    private bool battleActive = true;
    private bool awaitingDefeatChoice;

    public bool IsBattleActive => battleActive;

    private void Start()
    {
        ResolveReferences();
        ApplyModeConfiguration();
        PositionSceneActors();
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

    public bool TryHitBoss(Vector3 worldPoint, float hitRadius, float damage)
    {
        if (!battleActive || bossController == null || !bossController.IsAlive)
        {
            return false;
        }

        float distance = Vector3.Distance(worldPoint, bossController.HitPoint);
        if (distance > hitRadius + bossController.HitRadius)
        {
            return false;
        }

        return bossController.ApplyDamage(damage);
    }

    public bool TryHitPlayer(Vector3 worldPoint, float hitRadius, float damage)
    {
        if (!battleActive || playerCombatController == null || !playerCombatController.IsAlive)
        {
            return false;
        }

        float distance = Vector3.Distance(worldPoint, playerCombatController.HitPoint);
        if (distance > hitRadius + playerCombatController.HitRadius)
        {
            return false;
        }

        return playerCombatController.ApplyDamage(damage);
    }

    private void ResolveReferences()
    {
        bossController ??= FindSceneComponent<BossController>();
        bossAttackController ??= FindSceneComponent<BossAttackController>();
        playerOrbitController ??= FindSceneComponent<PlayerOrbitController>();
        playerCombatController ??= FindSceneComponent<PlayerCombatController>();
        arenaCameraRig ??= FindSceneComponent<ArenaCameraRig>();
        hudPresenter ??= FindSceneComponent<HUDPresenter>();
        playerProjectileTemplate ??= FindSceneObject("PlayerProjectileTemplate");
        bossProjectileTemplate ??= FindSceneObject("BossProjectileTemplate");
        allyPlaceholder ??= FindSceneObject("AllyPlaceholder");
    }

    private void ApplyModeConfiguration()
    {
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

    private void PositionSceneActors()
    {
        if (arenaCameraRig != null && bossController != null)
        {
            arenaCameraRig.Configure(bossController.OrbitCenter, bossController.AimPoint);
        }

        if (playerOrbitController != null && bossController != null && arenaCameraRig != null)
        {
            playerOrbitController.Configure(bossController.OrbitCenter, bossController.AimPoint, arenaCameraRig);
            playerOrbitController.AdoptScenePlacement(playerOrbitController.transform.position);
        }

        if (bossController != null)
        {
            bossController.CaptureBasePose();

            if (playerOrbitController != null)
            {
                bossController.AdoptSceneRotation(playerOrbitController.transform.position);
            }
        }
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
        }

        if (bossAttackController != null)
        {
            bossAttackController.Configure(this, bossController, playerCombatController, bossProjectileTemplate);
        }

        if (hudPresenter != null && bossController != null && playerCombatController != null && playerOrbitController != null)
        {
            hudPresenter.Configure(bossController, playerCombatController, playerOrbitController);
            hudPresenter.RetryRequested += HandleRetryRequested;
            hudPresenter.QuitRequested += HandleQuitRequested;
            string modeLabel = GameFlowController.CurrentMode == GameMode.MultiPlaceholder
                ? "Co-op placeholder mode"
                : "Single battle mode";
            hudPresenter.SetStatusMessage($"{modeLabel}. Camera auto-orbits. A/D strafe. W/S altitude. Q/Z forward-back.");
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
