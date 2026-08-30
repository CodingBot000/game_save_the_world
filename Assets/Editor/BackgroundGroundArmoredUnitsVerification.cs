#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class BackgroundGroundArmoredUnitsVerification
{
    private const string BattleArenaScenePath = "Assets/Scenes/BattleArena.unity/BattleArena.unity";
    private static readonly string OutputFolder = Path.Combine("Logs", "BackgroundGroundArmoredUnits");
    private static bool running;
    private static bool enteredPlayMode;
    private static int exitCode;
    private static int phase;
    private static double playStartedAt;
    private static BackgroundGroundArmoredUnitsRuntime ground;
    private static BackgroundAllyArmyController air;
    private static BossController boss;
    private static PlayerCombatController player;
    private static BossLockOnTargetProvider targetProvider;
    private static float bossHealth;
    private static float playerHull;
    private static float playerArmor;
    private static int targetCount;
    private static Vector3[] initialPositions;
    private static bool previousUndead;
    private static bool previousEnterPlayModeOptionsEnabled;
    private static EnterPlayModeOptions previousEnterPlayModeOptions;

    [MenuItem("Tools/Titan Destroyer/Verify Background Ground Armored Units")]
    public static void RunInteractive() => Run();

    public static void RunBatch() => Run();

    private static void Run()
    {
        if (running) return;
        Directory.CreateDirectory(OutputFolder);
        File.WriteAllText(Path.Combine(OutputFolder, "runtime.txt"), "Background ground armored unit verification started.\n");
        running = true;
        enteredPlayMode = false;
        exitCode = 0;
        phase = 0;
        previousEnterPlayModeOptionsEnabled = EditorSettings.enterPlayModeOptionsEnabled;
        previousEnterPlayModeOptions = EditorSettings.enterPlayModeOptions;
        EditorSettings.enterPlayModeOptionsEnabled = true;
        EditorSettings.enterPlayModeOptions = EnterPlayModeOptions.DisableDomainReload;
        EditorSceneManager.OpenScene(BattleArenaScenePath, OpenSceneMode.Single);
        EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
        EditorApplication.update += Tick;
        EditorApplication.EnterPlaymode();
    }

    private static void OnPlayModeStateChanged(PlayModeStateChange state)
    {
        if (state == PlayModeStateChange.EnteredPlayMode)
        {
            enteredPlayMode = true;
            playStartedAt = EditorApplication.timeSinceStartup;
        }
        else if (state == PlayModeStateChange.EnteredEditMode && running)
        {
            CleanupCallbacks();
            if (Application.isBatchMode) EditorApplication.Exit(exitCode);
        }
    }

    private static void Tick()
    {
        if (!running || !enteredPlayMode || !EditorApplication.isPlaying) return;
        try
        {
            double elapsed = EditorApplication.timeSinceStartup - playStartedAt;
            if (elapsed > 40d)
            {
                throw new TimeoutException(
                    $"Background ground armored unit verification timed out at phase={phase}, shots={ground?.TotalCosmeticShots ?? 0}, " +
                    $"muzzleFlashes={ground?.TotalMuzzleFlashes ?? 0}, visibleFlashes={ground?.VisibleMuzzleFlashCount ?? 0}.");
            }

            if (phase == 0 && elapsed >= 0.1d)
            {
                if (!TryResolveAndFreezeCombat()) return;
                phase = 1;
                return;
            }

            if (phase == 1 && elapsed >= 1.5d)
            {
                CaptureBaseline();
                VerifyInitialRuntime();
                CaptureCamera("patrol.png", isolateGround: false);
                CaptureCamera("ground-isolated.png", isolateGround: true);
                ground.ForceGroundPrimaryAttackForDebug();
                Require(ground.ActivePrimaryAttackCount == 1, "Forced ground primary attack did not start.");
                ground.ForceGroundMuzzleFlashForDebug();
                Require(ground.TotalMuzzleFlashes > 0, "Forced cosmetic shot did not create a muzzle flash event.");
                Require(ground.VisibleMuzzleFlashCount > 0, "Forced cosmetic shot did not enable a muzzle flash renderer.");
                CaptureCamera("attack.png", isolateGround: false);
                CaptureCamera("ground-attack-isolated.png", isolateGround: true);
                phase = 2;
                Append($"Initial runtime PASS; forced primary attack started with visible muzzle flash count={ground.VisibleMuzzleFlashCount}.");
                return;
            }

            if (phase == 2 && ground.TotalMuzzleFlashes > 0 && ground.VisibleMuzzleFlashCount == 0)
            {
                VerifyCombatInvariants();
                phase = 3;
                Append($"Timed muzzle flash PASS shots={ground.TotalCosmeticShots} muzzleFlashes={ground.TotalMuzzleFlashes} visibleFlashes={ground.VisibleMuzzleFlashCount} activeVfx={ground.ActiveVfxCount}.");
                return;
            }

            if (phase == 3 && ground.ActivePrimaryAttackCount == 0)
            {
                VerifyCombatInvariants();
                VerifyMovementAndGrounding();
                Append(
                    $"PASS units={ground.SpawnedUnitCount} primary={ground.TotalPrimaryAttacks} shots={ground.TotalCosmeticShots} muzzleFlashes={ground.TotalMuzzleFlashes} " +
                    $"bossHp={boss.CurrentHealth:F1} player={player.CurrentHull:F1}/{player.CurrentArmor:F1} targets={targetProvider.ValidTargetCount}");
                Debug.Log("[BackgroundGroundArmoredUnits] PASS " + Path.GetFullPath(OutputFolder));
                Finish(0);
            }
        }
        catch (Exception exception)
        {
            Append("FAIL " + exception);
            Debug.LogException(exception);
            Finish(1);
        }
    }

    private static bool TryResolveAndFreezeCombat()
    {
        ground = UnityEngine.Object.FindAnyObjectByType<BackgroundGroundArmoredUnitsRuntime>();
        air = UnityEngine.Object.FindAnyObjectByType<BackgroundAllyArmyController>();
        boss = UnityEngine.Object.FindAnyObjectByType<BossController>();
        player = UnityEngine.Object.FindAnyObjectByType<PlayerCombatController>();
        targetProvider = UnityEngine.Object.FindAnyObjectByType<BossLockOnTargetProvider>();
        if (ground == null || boss == null || player == null || targetProvider == null || ground.SpawnedUnitCount == 0) return false;

        player.SetCombatEnabled(false);
        previousUndead = GameplayDebugFlags.Undead;
        GameplayDebugFlags.Undead = true;
        if (air != null) air.enabled = false;
        BossAttackController bossAttack = UnityEngine.Object.FindAnyObjectByType<BossAttackController>();
        BossBulletPatternController bossPattern = UnityEngine.Object.FindAnyObjectByType<BossBulletPatternController>();
        if (bossAttack != null) bossAttack.enabled = false;
        if (bossPattern != null) bossPattern.enabled = false;
        ProjectileController[] activeProjectiles = UnityEngine.Object.FindObjectsByType<ProjectileController>(FindObjectsInactive.Exclude);
        for (int i = 0; i < activeProjectiles.Length; i++)
        {
            if (activeProjectiles[i] != null && activeProjectiles[i].gameObject.scene.IsValid())
            {
                UnityEngine.Object.Destroy(activeProjectiles[i].gameObject);
            }
        }
        return true;
    }

    private static void CaptureBaseline()
    {
        bossHealth = boss.CurrentHealth;
        playerHull = player.CurrentHull;
        playerArmor = player.CurrentArmor;
        targetCount = targetProvider.ValidTargetCount;
    }

    private static void VerifyInitialRuntime()
    {
        BackgroundGroundUnitView[] views = UnityEngine.Object.FindObjectsByType<BackgroundGroundUnitView>(FindObjectsInactive.Exclude);
        Require(ground.SpawnedUnitCount == 8, $"Expected 8 ground vehicles, got {ground.SpawnedUnitCount}.");
        Require(views.Length == 8, $"Expected 8 ground unit views, got {views.Length}.");
        int gatlingCount = views.Count(view => view.name.Contains("Gatling"));
        int mortarCount = views.Count(view => view.name.Contains("Mortar"));
        int tankCount = views.Length - gatlingCount - mortarCount;
        Require(tankCount == 3, $"Expected three tanks, got {tankCount}.");
        Require(gatlingCount == 3, $"Expected three gatling carriers, got {gatlingCount}.");
        Require(mortarCount == 2, $"Expected two mortar carriers, got {mortarCount}.");
        Require(ground.GetComponentsInChildren<Collider>(true).Length == 0, "Ground army contains a Collider.");
        Require(ground.GetComponentsInChildren<Rigidbody>(true).Length == 0, "Ground army contains a Rigidbody.");
        Require(views.All(view => view.TurretYaw != null && view.WeaponPitch != null && view.Muzzle != null), "A weapon pivot or muzzle is missing.");

        for (int i = 0; i < views.Length; i++)
        {
            Transform model = views[i].VisualRoot.Find("Model");
            Require(model != null, $"{views[i].name} Model transform is missing.");
            int triangles = model.GetComponentsInChildren<MeshFilter>(true)
                .Where(filter => filter.sharedMesh != null
                                 && (filter.name == "Body" || filter.name == "TurretMesh" || filter.name == "BarrelMesh"))
                .Sum(filter => filter.sharedMesh.triangles.Length / 3);
            int expected = views[i].name.Contains("Gatling") ? 779 : views[i].name.Contains("Mortar") ? 820 : 800;
            Require(triangles == expected, $"{views[i].name} expected {expected} tris, got {triangles}.");

            Vector3 local = ground.StageVisualRoot.InverseTransformPoint(views[i].transform.position);
            Require(local.y >= 0.08f && local.y <= 0.25f, $"{views[i].name} is not grounded in StageVisualRoot local space: y={local.y:F3}.");
            Vector3 muzzleDirection = (views[i].Muzzle.position - views[i].transform.position).normalized;
            Require(Vector3.Dot(muzzleDirection, views[i].transform.forward) > 0.25f, $"{views[i].name} muzzle is not on the forward side.");

            Vector2 projected = CalculateProjectedSize(Camera.main, views[i].CachedRenderers);
            Require(projected.x * 1280f >= 12f && projected.x * 1280f <= 110f,
                $"{views[i].name} projected width is outside the background-unit range: {projected.x * 1280f:F1}px.");
            Append($"unit={views[i].name} stageLocal={local:F3} projected={projected.x * 1280f:F1}x{projected.y * 720f:F1}px tris={triangles}");
        }

        Material bodyMaterial = views[0].CachedRenderers
            .First(renderer => renderer != null && renderer.sharedMaterial != null && renderer.sharedMaterial.name == "BackgroundGroundVehicles")
            .sharedMaterial;
        Require(bodyMaterial.enableInstancing, "Ground vehicle material does not enable GPU instancing.");
        Require(views.All(view => view.CachedRenderers.Any(renderer => renderer != null && renderer.sharedMaterial == bodyMaterial)), "Ground vehicles do not share one body material.");
        initialPositions = views.Select(view => view.transform.position).ToArray();
        VerifyCombatInvariants();
    }

    private static void VerifyMovementAndGrounding()
    {
        BackgroundGroundUnitView[] views = UnityEngine.Object.FindObjectsByType<BackgroundGroundUnitView>(FindObjectsInactive.Exclude);
        Require(views.Length == initialPositions.Length, "Ground vehicle count changed during verification.");
        Require(views.Where((view, index) => Vector3.Distance(view.transform.position, initialPositions[index]) > 0.2f).Count() >= 6,
            "Fewer than six ground vehicles moved along their routes.");
        for (int i = 0; i < views.Length; i++)
        {
            float localY = ground.StageVisualRoot.InverseTransformPoint(views[i].transform.position).y;
            Require(localY >= 0.08f && localY <= 0.25f, $"{views[i].name} left the stage-local ground plane: y={localY:F3}.");
        }
    }

    private static void VerifyCombatInvariants()
    {
        Require(Mathf.Abs(boss.CurrentHealth - bossHealth) < 0.001f, "Ground cosmetic attack changed boss health.");
        Require(Mathf.Abs(player.CurrentHull - playerHull) < 0.001f, "Ground cosmetic attack changed player Hull.");
        Require(Mathf.Abs(player.CurrentArmor - playerArmor) < 0.001f, "Ground cosmetic attack changed player Armor.");
        Require(targetProvider.ValidTargetCount == targetCount, "Ground cosmetic attack changed lock-on target count.");
    }

    private static Vector2 CalculateProjectedSize(Camera camera, Renderer[] renderers)
    {
        bool hasBounds = false;
        Bounds combined = default;
        for (int i = 0; i < renderers.Length; i++)
        {
            Renderer renderer = renderers[i];
            if (renderer == null || !renderer.enabled || renderer.name.Contains("Flash")) continue;
            if (!hasBounds) { combined = renderer.bounds; hasBounds = true; }
            else combined.Encapsulate(renderer.bounds);
        }

        if (!hasBounds || camera == null) return Vector2.zero;
        Vector3 center = combined.center;
        Vector3 extents = combined.extents;
        float minX = float.PositiveInfinity;
        float minY = float.PositiveInfinity;
        float maxX = float.NegativeInfinity;
        float maxY = float.NegativeInfinity;
        for (int x = -1; x <= 1; x += 2)
        for (int y = -1; y <= 1; y += 2)
        for (int z = -1; z <= 1; z += 2)
        {
            Vector3 viewport = camera.WorldToViewportPoint(center + Vector3.Scale(extents, new Vector3(x, y, z)));
            minX = Mathf.Min(minX, viewport.x);
            minY = Mathf.Min(minY, viewport.y);
            maxX = Mathf.Max(maxX, viewport.x);
            maxY = Mathf.Max(maxY, viewport.y);
        }

        return new Vector2(Mathf.Max(0f, maxX - minX), Mathf.Max(0f, maxY - minY));
    }

    private static void CaptureCamera(string fileName, bool isolateGround)
    {
        Camera camera = Camera.main;
        Require(camera != null, "Main camera is missing.");
        Renderer[] all = UnityEngine.Object.FindObjectsByType<Renderer>(FindObjectsInactive.Exclude);
        Renderer[] groundRenderers = ground.GetComponentsInChildren<Renderer>(true);
        bool[] states = all.Select(renderer => renderer.enabled).ToArray();
        Renderer[] playerRenderers = player.GetComponentsInChildren<Renderer>(true);
        const int width = 1280;
        const int height = 720;
        RenderTexture renderTexture = new(width, height, 24, RenderTextureFormat.ARGB32);
        Texture2D texture = new(width, height, TextureFormat.RGB24, false);
        RenderTexture previousActive = RenderTexture.active;
        RenderTexture previousTarget = camera.targetTexture;
        try
        {
            for (int i = 0; i < all.Length; i++)
            {
                if (isolateGround) all[i].enabled = groundRenderers.Contains(all[i]);
                else if (playerRenderers.Contains(all[i])) all[i].enabled = false;
            }

            camera.targetTexture = renderTexture;
            camera.Render();
            RenderTexture.active = renderTexture;
            texture.ReadPixels(new Rect(0, 0, width, height), 0, 0);
            texture.Apply(false, false);
            File.WriteAllBytes(Path.Combine(OutputFolder, fileName), texture.EncodeToPNG());
        }
        finally
        {
            camera.targetTexture = previousTarget;
            RenderTexture.active = previousActive;
            for (int i = 0; i < all.Length; i++) if (all[i] != null) all[i].enabled = states[i];
            UnityEngine.Object.DestroyImmediate(texture);
            UnityEngine.Object.DestroyImmediate(renderTexture);
        }
    }

    private static void Finish(int code)
    {
        exitCode = code;
        if (EditorApplication.isPlaying) { EditorApplication.ExitPlaymode(); return; }
        CleanupCallbacks();
        if (Application.isBatchMode) EditorApplication.Exit(exitCode);
    }

    private static void CleanupCallbacks()
    {
        EditorApplication.update -= Tick;
        EditorApplication.playModeStateChanged -= OnPlayModeStateChanged;
        EditorSettings.enterPlayModeOptionsEnabled = previousEnterPlayModeOptionsEnabled;
        EditorSettings.enterPlayModeOptions = previousEnterPlayModeOptions;
        GameplayDebugFlags.Undead = previousUndead;
        running = false;
        enteredPlayMode = false;
    }

    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }

    private static void Append(string line)
    {
        File.AppendAllText(Path.Combine(OutputFolder, "runtime.txt"), line + Environment.NewLine);
    }
}
#endif
