using System.Collections;
using UnityEngine;

public class PlayerSpecialAttackController : MonoBehaviour
{
    private const string TextureCatalogResourcePath = "Battle/SpecialAttack/SpecialAttackTextureCatalog";
    private const string SceneTopTextureResourcePath = "Battle/SpecialAttack/special_scene1";
    private const string SceneBottomTextureResourcePath = "Battle/SpecialAttack/special_scene2";
    private const int DefaultMissileCountPerSide = 15;

    [Header("Timing")]
    [SerializeField] private float cutInDuration = 0.4f;
    [SerializeField] private float missileSalvoDuration = 0.6f;
    [SerializeField] private float visualReturnDuration = 0.35f;
    [SerializeField] private float visualTurnTowardBossAngle = 60f;

    [Header("Missiles")]
    [SerializeField] private int missileCountPerSide = DefaultMissileCountPerSide;
    [SerializeField] private int missilesPerVolley = 4;
    [SerializeField] private float specialMissileDamage = 0f;

    [Header("Missile Strike Distribution")]
    [SerializeField] private float targetSpreadRadius = 1.6f;
    [SerializeField] private float targetSpreadVerticalScale = 1.25f;
    [SerializeField] private float targetSpreadDepth = 0.2f;

    [Header("Missile Strike Flight")]
    [SerializeField] private float fanOutDuration = 0.28f;
    [SerializeField] private float fanOutDistance = 5.5f;
    [SerializeField] private float fanOutHorizontal = 1f;
    [SerializeField] private float fanOutVertical = 0.65f;
    [SerializeField] private float arcDuration = 0.75f;
    [SerializeField] private float arcDurationJitter = 0.18f;
    [SerializeField] private float arcHorizontalRadius = 10f;
    [SerializeField] private float arcVerticalRadius = 7f;
    [SerializeField] private float terminalEntryDistance = 8f;

    [Header("Missile Strike Pool")]
    [SerializeField] private int missilePoolPrewarmCount = 40;

    [Header("Resources")]
    [SerializeField] private SpecialAttackTextureCatalog textureCatalog;

    private BattleController battleController;
    private BossController bossController;
    private BossAttackController bossAttackController;
    private BossBulletPatternController bossPatternController;
    private PlayerCombatController playerCombatController;
    private PlayerOrbitController playerOrbitController;
    private HUDPresenter hudPresenter;
    private BattleAimPointTargetingPresenter aimPointTargetingPresenter;
    private SpecialAttackOverlayPresenter overlayPresenter;
    private Coroutine activeRoutine;
    private Texture2D sceneTopTextureFallback;
    private Texture2D sceneBottomTextureFallback;
    private bool lastMissileSalvoCompleted;
    private int salvoSequence;
    private SpecialMissilePool missilePool;

    public bool IsActive => activeRoutine != null;
    public event System.Action SpecialMissileSalvoCompleted;

    public void Configure(
        BattleController battle,
        BossController boss,
        BossAttackController bossAttack,
        PlayerCombatController playerCombat,
        PlayerOrbitController playerOrbit,
        ArenaCameraRig cameraRig,
        HUDPresenter hud,
        BattleAimPointTargetingPresenter targetingPresenter = null)
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
        aimPointTargetingPresenter = targetingPresenter;
        ResolveTextureCatalog();
        EnsureMissilePool();
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
            lastMissileSalvoCompleted = false;
            yield return LaunchMissileSalvo();
            if (lastMissileSalvoCompleted)
            {
                SpecialMissileSalvoCompleted?.Invoke();
            }

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
        EnsureMissilePool();
        int countPerSide = Mathf.Max(1, missileCountPerSide);
        int totalCount = countPerSide * 2;
        int combatAimPointCount = bossController != null
            ? bossController.GetCombatAimPointCount()
            : 0;
        int currentSalvoSequence = unchecked(++salvoSequence);
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
                LaunchSpecialMissile(
                    i,
                    totalCount,
                    combatAimPointCount,
                    currentSalvoSequence);
            }

            if (volleyEnd < totalCount)
            {
                yield return new WaitForSeconds(launchInterval);
            }
        }

        lastMissileSalvoCompleted = true;
    }

    private void LaunchSpecialMissile(
        int index,
        int totalCount,
        int combatAimPointCount,
        int currentSalvoSequence)
    {
        Transform launcher = SelectSpecialLauncher(index);
        if (launcher == null || playerCombatController == null || bossController == null)
        {
            return;
        }

        int distributionAnchorCount = Mathf.Max(1, combatAimPointCount);
        int anchorIndex = MissileStrikeDistribution.GetAnchorIndex(
            index,
            distributionAnchorCount,
            currentSalvoSequence);
        Transform targetAnchor = ResolveStrikeAnchor(anchorIndex, combatAimPointCount);
        int anchorOrdinal = MissileStrikeDistribution.GetAnchorOrdinal(index, distributionAnchorCount);
        int assignedMissileCount = MissileStrikeDistribution.GetAnchorAssignmentCount(
            anchorIndex,
            totalCount,
            distributionAnchorCount,
            currentSalvoSequence);
        Vector3 targetLocalOffset = MissileStrikeDistribution.GetLocalOffset(
            index,
            anchorIndex,
            anchorOrdinal,
            assignedMissileCount,
            currentSalvoSequence,
            targetSpreadRadius,
            targetSpreadVerticalScale,
            targetSpreadDepth);
        SpecialMissileStrikePath strikePath = CreateStrikePath(
            launcher,
            index,
            currentSalvoSequence,
            targetAnchor,
            targetLocalOffset);
        Vector3 launchDirection = strikePath.FanOutDirection;
        float boostAcceleration = Mathf.Max(
            playerCombatController.DebugMissileAcceleration,
            Mathf.Abs(playerCombatController.DebugMissileCruiseSpeed - playerCombatController.DebugMissileLaunchSpeed) /
            Mathf.Max(0.01f, playerCombatController.DebugMissileBoostPhaseDuration));

        SpecialHomingMissileController missile = missilePool != null ? missilePool.Get() : null;
        if (missile == null)
        {
            GameObject missileInstance = new("PlayerSpecialMissileRuntime");
            missile = missileInstance.AddComponent<SpecialHomingMissileController>();
        }

        missile.transform.position = launcher.position;
        missile.transform.rotation = Quaternion.LookRotation(launchDirection.normalized, Vector3.up);
        float criticalChance = playerCombatController != null
            ? playerCombatController.ResolveCurrentWeaponCriticalChance()
            : ResolveSpecialCriticalChance(targetAnchor);

        missile.Launch(
            battleController,
            targetAnchor,
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
            playerCombatController.DebugMissileTemplateLocalEulerAngles,
            criticalChance);
        missile.ConfigureStrikePath(strikePath);
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

    private SpecialMissileStrikePath CreateStrikePath(
        Transform launcher,
        int missileIndex,
        int currentSalvoSequence,
        Transform targetAnchor,
        Vector3 targetLocalOffset)
    {
        Vector3 baseDirection = playerCombatController != null
            ? playerCombatController.GetMissileLaunchDirectionForSpecial()
            : launcher.forward;
        if (baseDirection.sqrMagnitude < 0.001f)
        {
            baseDirection = launcher.forward.sqrMagnitude > 0.001f ? launcher.forward : Vector3.forward;
        }

        baseDirection.Normalize();
        Camera mainCamera = Camera.main;
        Vector3 cameraRight = mainCamera != null ? mainCamera.transform.right : launcher.right;
        Vector3 cameraUp = mainCamera != null ? mainCamera.transform.up : Vector3.up;
        Vector3 cameraForward = mainCamera != null ? mainCamera.transform.forward : baseDirection;
        float sideSign = missileIndex % 2 == 0 ? -1f : 1f;
        float outwardAmount = Mathf.Lerp(
            0.58f,
            1f,
            MissileStrikeDistribution.Hash01(currentSalvoSequence, missileIndex, 0x31A7));
        float verticalAmount = MissileStrikeDistribution.HashSigned(
            currentSalvoSequence,
            missileIndex,
            0x6E2B);
        Vector3 fanDirection =
            baseDirection * 0.45f +
            cameraRight * (sideSign * Mathf.Max(0f, fanOutHorizontal) * outwardAmount) +
            cameraUp * (verticalAmount * Mathf.Max(0f, fanOutVertical));
        if (fanDirection.sqrMagnitude < 0.001f)
        {
            fanDirection = baseDirection;
        }

        fanDirection.Normalize();
        Vector3 fanEndPosition = launcher.position + fanDirection * Mathf.Max(0f, fanOutDistance);
        Vector3 targetWorldPosition = targetAnchor != null
            ? targetAnchor.TransformPoint(targetLocalOffset)
            : bossController != null
                ? bossController.HitPoint
                : launcher.position + baseDirection * 25f;
        Vector3 approachVector = targetWorldPosition - fanEndPosition;
        Vector3 approachDirection = approachVector.sqrMagnitude > 0.001f
            ? approachVector.normalized
            : baseDirection;
        float entryDistance = Mathf.Min(
            Mathf.Max(0f, terminalEntryDistance),
            approachVector.magnitude * 0.55f);
        Vector3 terminalEntryPoint = targetWorldPosition - approachDirection * entryDistance;
        Vector3 arcMidPoint = Vector3.Lerp(fanEndPosition, terminalEntryPoint, 0.5f);
        float arcSideAmount = sideSign * Mathf.Lerp(
            0.55f,
            1f,
            MissileStrikeDistribution.Hash01(currentSalvoSequence, missileIndex, 0x19C3));
        float arcVerticalAmount = MissileStrikeDistribution.HashSigned(
            currentSalvoSequence,
            missileIndex,
            0x52D1);
        float arcDepthAmount = MissileStrikeDistribution.HashSigned(
            currentSalvoSequence,
            missileIndex,
            0x73A9);
        Vector3 arcControlPoint =
            arcMidPoint +
            cameraRight * (arcSideAmount * Mathf.Max(0f, arcHorizontalRadius)) +
            cameraUp * (arcVerticalAmount * Mathf.Max(0f, arcVerticalRadius)) +
            cameraForward * (arcDepthAmount * Mathf.Max(0f, arcHorizontalRadius) * 0.18f);
        float resolvedArcDuration = Mathf.Max(
            0.1f,
            arcDuration + MissileStrikeDistribution.HashSigned(
                currentSalvoSequence,
                missileIndex,
                0x4F1B) * Mathf.Abs(arcDurationJitter));

        return new SpecialMissileStrikePath
        {
            TargetAnchor = targetAnchor,
            TargetLocalOffset = targetLocalOffset,
            FanOutDirection = fanDirection,
            FanOutDuration = Mathf.Max(0.01f, fanOutDuration),
            FanOutDistance = Mathf.Max(0f, fanOutDistance),
            ArcControlPoint = arcControlPoint,
            TerminalEntryPoint = terminalEntryPoint,
            ArcDuration = resolvedArcDuration,
        };
    }

    private Transform ResolveStrikeAnchor(int anchorIndex, int combatAimPointCount)
    {
        if (bossController == null)
        {
            return null;
        }

        if (combatAimPointCount > 0)
        {
            Transform combatAimPoint = bossController.GetCombatAimPoint(anchorIndex);
            if (combatAimPoint != null)
            {
                return combatAimPoint;
            }
        }

        return bossController.AimPoint != null ? bossController.AimPoint : bossController.transform;
    }

    private SpecialMissilePool EnsureMissilePool()
    {
        if (missilePool == null)
        {
            missilePool = GetComponentInChildren<SpecialMissilePool>(true);
            if (missilePool == null)
            {
                missilePool = SpecialMissilePool.Create(transform);
            }
        }

        missilePool.Prewarm(Mathf.Max(0, missilePoolPrewarmCount));
        if (playerCombatController != null)
        {
            missilePool.PrewarmImpacts(
                playerCombatController.DebugMissileImpactEffectTemplate,
                Mathf.Max(1, missileCountPerSide * 2));
        }

        return missilePool;
    }

    private float ResolveSpecialCriticalChance(Transform targetTransform)
    {
        if (aimPointTargetingPresenter != null && aimPointTargetingPresenter.TryGetSelectedAimPoint(out Transform selectedAimPoint))
        {
            return aimPointTargetingPresenter.GetCriticalChanceForShot(targetTransform, selectedAimPoint == targetTransform);
        }

        return 0.05f;
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

    private void OnDestroy()
    {
        if (missilePool != null)
        {
            missilePool.Dispose();
            missilePool = null;
        }
    }
}
