using System.Collections;
using UnityEngine;

public class PlayerSpecialAttackController : MonoBehaviour
{
    private const string TextureCatalogResourcePath = "Battle/SpecialAttack/SpecialAttackTextureCatalog";
    private const string SceneTopTextureResourcePath = "Battle/SpecialAttack/special_scene1";
    private const string SceneBottomTextureResourcePath = "Battle/SpecialAttack/special_scene2";
    private const int DefaultMissileCountPerSide = 15;
    private static readonly Vector2[] MissileSideArcPattern =
    {
        new(1f, 0f),
        new(0.65f, 0.55f),
        new(0.65f, -0.55f),
        new(0f, 1f),
        new(0f, -1f),
    };

    [Header("Timing")]
    [SerializeField] private float cutInDuration = 0.4f;
    [SerializeField] private float missileSalvoDuration = 2f;
    [SerializeField] private float visualReturnDuration = 0.35f;
    [SerializeField] private float visualTurnTowardBossAngle = 60f;

    [Header("Missiles")]
    [SerializeField] private int missileCountPerSide = DefaultMissileCountPerSide;
    [SerializeField] private int missilesPerVolley = 2;
    [SerializeField, Range(0f, 1f)] private float missileSideArcMinScreenOffset = 0.2f;
    [SerializeField, Range(0f, 1.2f)] private float missileSideArcMaxScreenOffset = 0.5f;
    [SerializeField, Range(0f, 1f)] private float missileSideArcMaxVerticalScreenOffset = 0.4f;
    [SerializeField] private float missileSideArcDuration = 0.5f;
    [SerializeField] private float missileFallbackSpreadAngle = 70f;
    [SerializeField] private float specialMissileDamage = 0f;

    [Header("Resources")]
    [SerializeField] private SpecialAttackTextureCatalog textureCatalog;

    private BattleController battleController;
    private BossController bossController;
    private BossAttackController bossAttackController;
    private BossBulletPatternController bossPatternController;
    private PlayerCombatController playerCombatController;
    private PlayerOrbitController playerOrbitController;
    private HUDPresenter hudPresenter;
    private SpecialAttackOverlayPresenter overlayPresenter;
    private Coroutine activeRoutine;
    private Texture2D sceneTopTextureFallback;
    private Texture2D sceneBottomTextureFallback;

    public bool IsActive => activeRoutine != null;

    public void Configure(
        BattleController battle,
        BossController boss,
        BossAttackController bossAttack,
        PlayerCombatController playerCombat,
        PlayerOrbitController playerOrbit,
        ArenaCameraRig cameraRig,
        HUDPresenter hud)
    {
        battleController = battle;
        bossController = boss;
        bossAttackController = bossAttack;
        bossPatternController = bossAttackController != null
            ? bossAttackController.GetComponent<BossBulletPatternController>()
            : FindAnyObjectByType<BossBulletPatternController>();
        playerCombatController = playerCombat;
        playerOrbitController = playerOrbit;
        hudPresenter = hud;
        ResolveTextureCatalog();
    }

    public bool TryActivate()
    {
        if (!CanActivate())
        {
            return false;
        }

        activeRoutine = StartCoroutine(SpecialAttackRoutine());
        return true;
    }

    public bool CanActivate()
    {
        return string.IsNullOrEmpty(GetUnavailableReason());
    }

    public string GetUnavailableReason()
    {
        if (IsActive)
        {
            return "Special attack is already active.";
        }

        if (battleController == null || !battleController.IsBattleActive)
        {
            return "Special attack unavailable.";
        }

        if (playerCombatController == null || !playerCombatController.IsAlive)
        {
            return "Player destroyed.";
        }

        if (bossController == null || !bossController.IsAlive)
        {
            return "No special attack target.";
        }

        if (!HasAnyLauncher())
        {
            return "Special launcher offline.";
        }

        ResolveTextureCatalog();
        if (ResolveSceneTopTexture() == null || ResolveSceneBottomTexture() == null)
        {
            return "Special scene assets missing.";
        }

        return string.Empty;
    }

    private IEnumerator SpecialAttackRoutine()
    {
        SetPlayerInputPaused(true);
        SetBossPaused(true);
        ClearBossProjectiles();

        hudPresenter?.SetStatusMessage("Special attack.");

        try
        {
            SetSpecialAttackVisualPose();

            yield return PlayCutInOverlay();
            yield return LaunchMissileSalvo();

            if (playerOrbitController != null)
            {
                yield return playerOrbitController.ClearCinematicVisualOverrideSmooth(visualReturnDuration);
            }
        }
        finally
        {
            playerOrbitController?.ClearCinematicVisualOverride();
            SetBossPaused(false);
            SetPlayerInputPaused(false);
            activeRoutine = null;
        }
    }

    private IEnumerator PlayCutInOverlay()
    {
        Canvas canvas = hudPresenter != null ? hudPresenter.RuntimeCanvas : null;
        if (canvas == null && hudPresenter != null)
        {
            canvas = hudPresenter.GetComponentInParent<Canvas>();
        }

        if (canvas == null)
        {
            canvas = FindAnyObjectByType<Canvas>();
        }

        Texture2D topTexture = ResolveSceneTopTexture();
        Texture2D bottomTexture = ResolveSceneBottomTexture();
        if (topTexture == null || bottomTexture == null)
        {
            yield return new WaitForSeconds(Mathf.Max(0f, cutInDuration));
            yield break;
        }

        if (overlayPresenter == null)
        {
            overlayPresenter = gameObject.GetComponent<SpecialAttackOverlayPresenter>();
            if (overlayPresenter == null)
            {
                overlayPresenter = gameObject.AddComponent<SpecialAttackOverlayPresenter>();
            }
        }

        IEnumerator overlayRoutine = overlayPresenter.Play(
            canvas,
            topTexture,
            bottomTexture,
            cutInDuration);

        while (true)
        {
            object current;
            try
            {
                if (!overlayRoutine.MoveNext())
                {
                    break;
                }

                current = overlayRoutine.Current;
            }
            catch (System.Exception exception)
            {
                Debug.LogException(exception, this);
                break;
            }

            yield return current;
        }
    }

    private IEnumerator LaunchMissileSalvo()
    {
        int countPerSide = Mathf.Max(1, missileCountPerSide);
        int totalCount = countPerSide * 2;
        int volleySize = Mathf.Max(1, missilesPerVolley);
        int volleyCount = Mathf.CeilToInt(totalCount / (float)volleySize);
        float launchInterval = volleyCount > 1
            ? Mathf.Max(0f, missileSalvoDuration) / (volleyCount - 1)
            : 0f;

        for (int volleyStart = 0; volleyStart < totalCount; volleyStart += volleySize)
        {
            if (bossController == null || !bossController.IsAlive || battleController == null || !battleController.IsBattleActive)
            {
                yield break;
            }

            int volleyEnd = Mathf.Min(volleyStart + volleySize, totalCount);
            for (int i = volleyStart; i < volleyEnd; i++)
            {
                LaunchSpecialMissile(i, countPerSide);
            }

            if (volleyEnd < totalCount)
            {
                yield return new WaitForSeconds(launchInterval);
            }
        }
    }

    private void LaunchSpecialMissile(int index, int countPerSide)
    {
        Transform launcher = SelectSpecialLauncher(index);
        if (launcher == null || playerCombatController == null || bossController == null)
        {
            return;
        }

        int totalCount = Mathf.Max(1, countPerSide * 2);
        Vector3 launchDirection = ResolveSpecialMissileLaunchDirection(
            launcher,
            index,
            countPerSide,
            totalCount,
            out bool hasSideArc,
            out Vector3 sideArcControlPosition,
            out Vector3 sideArcEndPosition);
        float boostAcceleration = Mathf.Max(
            playerCombatController.DebugMissileAcceleration,
            Mathf.Abs(playerCombatController.DebugMissileCruiseSpeed - playerCombatController.DebugMissileLaunchSpeed) /
            Mathf.Max(0.01f, playerCombatController.DebugMissileBoostPhaseDuration));

        GameObject missileInstance = new("PlayerSpecialMissileRuntime");
        missileInstance.transform.position = launcher.position;
        missileInstance.transform.rotation = Quaternion.LookRotation(launchDirection.normalized, Vector3.up);

        SpecialHomingMissileController missile = missileInstance.AddComponent<SpecialHomingMissileController>();
        missile.Launch(
            battleController,
            bossController.AimPoint,
            ProjectileTeam.Player,
            launchDirection,
            playerCombatController.DebugMissileLaunchSpeed,
            playerCombatController.DebugMissileCruiseSpeed,
            boostAcceleration,
            playerCombatController.DebugMissileTurnRate,
            playerCombatController.DebugMissileLockOnDelay,
            playerCombatController.DebugMissileStraightPhaseDuration,
            playerCombatController.DebugMissileStraightPhaseDistance,
            playerCombatController.DebugMissileTurnPhaseDuration,
            playerCombatController.DebugMissileBoostPhaseDuration,
            playerCombatController.DebugMissileLifetime,
            specialMissileDamage,
            playerCombatController.DebugMissileHitRadius,
            playerCombatController.DebugMissileVisualTemplate,
            playerCombatController.DebugMissileSmokeTemplate,
            playerCombatController.DebugMissileImpactEffectTemplate,
            playerCombatController.DebugMissileVisualTexture,
            playerCombatController.DebugMissileSmokeTexture,
            playerCombatController.DebugMissileVisualScale,
            playerCombatController.DebugMissileSmokeScale,
            playerCombatController.DebugMissileImpactEffectScale,
            playerCombatController.DebugMissileUseTemplateOriginalMaterials,
            playerCombatController.DebugMissileTemplateTint,
            playerCombatController.DebugMissileTemplateLocalEulerAngles);

        if (hasSideArc)
        {
            missile.ConfigureSideArc(
                sideArcControlPosition,
                sideArcEndPosition,
                missileSideArcDuration);
        }
    }

    private void SetSpecialAttackVisualPose()
    {
        if (playerOrbitController == null || bossController == null)
        {
            return;
        }

        playerOrbitController.SetCinematicVisualTurnToward(
            bossController.HitPoint,
            visualTurnTowardBossAngle);
    }

    private Vector3 ResolveSpecialMissileLaunchDirection(
        Transform launcher,
        int index,
        int countPerSide,
        int totalCount,
        out bool hasSideArc,
        out Vector3 sideArcControlPosition,
        out Vector3 sideArcEndPosition)
    {
        hasSideArc = false;
        sideArcControlPosition = launcher != null ? launcher.position : Vector3.zero;
        sideArcEndPosition = sideArcControlPosition;

        Camera mainCamera = Camera.main;
        if (mainCamera != null &&
            TryResolveSideArc(
                mainCamera,
                launcher,
                index,
                countPerSide,
                out sideArcControlPosition,
                out sideArcEndPosition,
                out Vector3 sideArcDirection))
        {
            hasSideArc = true;
            return sideArcDirection;
        }

        Vector3 baseDirection = playerCombatController.GetMissileLaunchDirectionForSpecial();
        float normalizedIndex = totalCount <= 1 ? 0.5f : index / (totalCount - 1f);
        float spreadOffset = Mathf.Lerp(
            -missileFallbackSpreadAngle * 0.5f,
            missileFallbackSpreadAngle * 0.5f,
            normalizedIndex);
        return Quaternion.AngleAxis(spreadOffset, Vector3.up) * baseDirection;
    }

    private bool TryResolveSideArc(
        Camera mainCamera,
        Transform launcher,
        int index,
        int countPerSide,
        out Vector3 controlPosition,
        out Vector3 endPosition,
        out Vector3 initialDirection)
    {
        controlPosition = launcher != null ? launcher.position : Vector3.zero;
        endPosition = controlPosition;
        initialDirection = Vector3.zero;
        if (mainCamera == null || launcher == null)
        {
            return false;
        }

        Vector3 launcherViewportPoint = mainCamera.WorldToViewportPoint(launcher.position);
        float launcherDepth = launcherViewportPoint.z;
        if (launcherDepth <= mainCamera.nearClipPlane + 0.5f)
        {
            launcherDepth = Mathf.Max(
                mainCamera.nearClipPlane + 5f,
                Vector3.Dot(launcher.position - mainCamera.transform.position, mainCamera.transform.forward));
        }

        Vector3 targetPoint = bossController != null && bossController.AimPoint != null
            ? bossController.AimPoint.position
            : launcher.position + playerCombatController.GetMissileLaunchDirectionForSpecial() * 25f;
        Vector3 targetViewportPoint = mainCamera.WorldToViewportPoint(targetPoint);
        float targetDepth = targetViewportPoint.z > mainCamera.nearClipPlane + 0.5f
            ? targetViewportPoint.z
            : launcherDepth + 25f;

        int sideIndex = Mathf.Clamp(index / 2, 0, Mathf.Max(0, countPerSide - 1));
        Vector2 patternPoint = MissileSideArcPattern[sideIndex % MissileSideArcPattern.Length];
        float minOffset = Mathf.Clamp01(missileSideArcMinScreenOffset);
        float maxOffset = Mathf.Max(minOffset, missileSideArcMaxScreenOffset);
        float sideOffset = Mathf.Lerp(minOffset, maxOffset, Mathf.Clamp01(patternPoint.x));
        float verticalOffset = Mathf.Clamp(
            patternPoint.y,
            -1f,
            1f) * Mathf.Clamp01(missileSideArcMaxVerticalScreenOffset);
        float sideSign = index % 2 == 0 ? -1f : 1f;

        float endDepth = Mathf.Lerp(launcherDepth, targetDepth, 0.18f);
        float controlDepth = Mathf.Lerp(launcherDepth, targetDepth, 0.08f);
        Vector3 endViewportPoint = new(
            launcherViewportPoint.x + sideSign * sideOffset,
            launcherViewportPoint.y + verticalOffset,
            endDepth);
        Vector3 controlViewportPoint = new(
            launcherViewportPoint.x + sideSign * sideOffset * 1.15f,
            launcherViewportPoint.y + verticalOffset * 0.8f,
            controlDepth);

        controlPosition = mainCamera.ViewportToWorldPoint(controlViewportPoint);
        endPosition = mainCamera.ViewportToWorldPoint(endViewportPoint);

        Vector3 resolvedDirection = controlPosition - launcher.position;
        if (resolvedDirection.sqrMagnitude < 0.001f)
        {
            return false;
        }

        initialDirection = resolvedDirection.normalized;
        return true;
    }

    private Transform SelectSpecialLauncher(int index)
    {
        Transform left = playerCombatController != null ? playerCombatController.MissileLauncherLeft : null;
        Transform right = playerCombatController != null ? playerCombatController.MissileLauncherRight : null;

        if (left != null && right != null)
        {
            return index % 2 == 0 ? left : right;
        }

        return left != null ? left : right;
    }

    private bool HasAnyLauncher()
    {
        return playerCombatController != null &&
               (playerCombatController.MissileLauncherLeft != null || playerCombatController.MissileLauncherRight != null);
    }

    private void SetPlayerInputPaused(bool paused)
    {
        if (!paused &&
            (battleController == null ||
             !battleController.IsBattleActive ||
             playerCombatController == null ||
             !playerCombatController.IsAlive))
        {
            playerOrbitController?.SetInputEnabled(false);
            playerCombatController?.SetCombatEnabled(false);
            return;
        }

        playerOrbitController?.SetInputEnabled(!paused);
        playerCombatController?.SetCombatEnabled(!paused);
    }

    private void SetBossPaused(bool paused)
    {
        bossController?.SetCinematicPaused(paused);
        bossAttackController?.SetCinematicPaused(paused);
        bossPatternController?.SetCinematicPaused(paused);
    }

    private void ClearBossProjectiles()
    {
        ProjectileController[] projectiles = FindObjectsByType<ProjectileController>(FindObjectsSortMode.None);
        foreach (ProjectileController projectile in projectiles)
        {
            if (projectile != null && projectile.Team == ProjectileTeam.Boss)
            {
                Destroy(projectile.gameObject);
            }
        }
    }

    private void ResolveTextureCatalog()
    {
        if (textureCatalog == null)
        {
            textureCatalog = Resources.Load<SpecialAttackTextureCatalog>(TextureCatalogResourcePath);
        }
    }

    private Texture2D ResolveSceneTopTexture()
    {
        if (sceneTopTextureFallback == null)
        {
            sceneTopTextureFallback = Resources.Load<Texture2D>(SceneTopTextureResourcePath);
        }

        if (sceneTopTextureFallback != null)
        {
            return sceneTopTextureFallback;
        }

        ResolveTextureCatalog();
        return textureCatalog != null ? textureCatalog.SceneTopTexture : null;
    }

    private Texture2D ResolveSceneBottomTexture()
    {
        if (sceneBottomTextureFallback == null)
        {
            sceneBottomTextureFallback = Resources.Load<Texture2D>(SceneBottomTextureResourcePath);
        }

        if (sceneBottomTextureFallback != null)
        {
            return sceneBottomTextureFallback;
        }

        ResolveTextureCatalog();
        return textureCatalog != null ? textureCatalog.SceneBottomTexture : null;
    }
}
