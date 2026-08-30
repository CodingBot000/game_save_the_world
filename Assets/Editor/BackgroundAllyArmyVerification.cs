#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class BackgroundAllyArmyVerification
{
    private const string BattleArenaScenePath = "Assets/Scenes/BattleArena.unity/BattleArena.unity";
    private static readonly string OutputFolder = Path.Combine("Logs", "BackgroundAllyArmy");
    private static bool running;
    private static bool enteredPlayMode;
    private static int exitCode;
    private static double playStartedAt;
    private static int phase;
    private static BackgroundAllyArmyController army;
    private static BossController boss;
    private static PlayerCombatController player;
    private static BossAttackController bossAttack;
    private static BossBulletPatternController bossPattern;
    private static BossLockOnTargetProvider targetProvider;
    private static float bossHealth;
    private static float playerHull;
    private static float playerArmor;
    private static int targetCount;
    private static Vector3[] initialPositions;
    private static bool previousEnterPlayModeOptionsEnabled;
    private static EnterPlayModeOptions previousEnterPlayModeOptions;
    private static readonly Dictionary<Transform, Vector3> previousFlightPositions = new();
    private static float maximumUpDeviation;
    private static float minimumForwardMovementDot;
    private static int flightAttitudeSamples;
    private static bool previousUndead;
    private static int gatlingFlashesAtBaseline;
    private static Transform crashingUnit;
    private static Vector3 crashStartPosition;
    private static double crashStartedAt;

    [MenuItem("Tools/Titan Destroyer/Verify Background Ally Air Army")]
    public static void RunInteractive()
    {
        Run();
    }

    public static void RunBatch()
    {
        Run();
    }

    private static void Run()
    {
        if (running)
        {
            return;
        }

        Directory.CreateDirectory(OutputFolder);
        File.WriteAllText(Path.Combine(OutputFolder, "runtime.txt"), "Background ally air verification started.\n");
        running = true;
        exitCode = 0;
        phase = 0;
        enteredPlayMode = false;
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
            return;
        }

        if (state == PlayModeStateChange.EnteredEditMode && running)
        {
            CleanupCallbacks();
            if (Application.isBatchMode)
            {
                EditorApplication.Exit(exitCode);
            }
        }
    }

    private static void Tick()
    {
        if (!running || !enteredPlayMode || !EditorApplication.isPlaying)
        {
            return;
        }

        try
        {
            double elapsed = EditorApplication.timeSinceStartup - playStartedAt;
            if (elapsed > 35d)
            {
                throw new TimeoutException("Background ally air verification timed out.");
            }

            if (phase >= 3 && phase <= 4)
            {
                TrackFlightAttitude();
            }

            if (phase == 0 && elapsed >= 0.1d)
            {
                if (!TryResolveAndFreezeCombat())
                {
                    return;
                }

                phase = 1;
                Append("Battle combat disabled and active projectiles cleared.");
                return;
            }

            if (phase == 1 && elapsed >= 2.5d)
            {
                CaptureCombatBaseline();
                VerifyInitialRuntime();
                CaptureCamera("patrol.png");
                CaptureArmyIsolation("army-isolated.png");
                gatlingFlashesAtBaseline = army.TotalGatlingFlashes;
                phase = 2;
                Append("Initial runtime PASS; waiting for patrol gatling flash.");
                return;
            }

            if (phase == 2 && army != null && army.TotalGatlingFlashes > gatlingFlashesAtBaseline)
            {
                BackgroundAllyUnitView[] views = UnityEngine.Object.FindObjectsByType<BackgroundAllyUnitView>(FindObjectsInactive.Exclude);
                if (!views.Any(view => view.IsMuzzleFlashVisible))
                {
                    return;
                }

                CaptureCamera("gatling.png");
                CaptureArmyIsolation("army-gatling-isolated.png");
                army.ForceCosmeticAirAttackForDebug();
                Require(army.ActiveCosmeticAttackCount == 1, "Forced cosmetic attack did not start.");
                phase = 3;
                Append($"Patrol gatling PASS; flashes={army.TotalGatlingFlashes}. Forced attack started.");
                return;
            }

            if (phase == 3 && army != null && army.TotalCosmeticShotsFired > 0)
            {
                VerifyCombatInvariants();
                CaptureCamera("attack.png");
                CaptureArmyIsolation("army-attack-isolated.png");
                phase = 4;
                Append($"Cosmetic tracer PASS; shots={army.TotalCosmeticShotsFired} activeTracers={army.ActiveTracerCount}.");
                return;
            }

            if (phase == 4 && army != null && army.ActiveCosmeticAttackCount == 0)
            {
                VerifyCombatInvariants();
                VerifyMovementAndReuse();
                CaptureCamera("rejoined.png");
                army.ForceRandomCrashForDebug();
                Require(army.ActiveCrashCount == 1 && army.ActiveCrashTransform != null, "Forced cosmetic crash did not start.");
                crashingUnit = army.ActiveCrashTransform;
                crashStartPosition = crashingUnit.position;
                crashStartedAt = EditorApplication.timeSinceStartup;
                phase = 5;
                Append("Flight attack/rejoin PASS; forced random crash started.");
                return;
            }

            if (phase == 5 && army != null && EditorApplication.timeSinceStartup - crashStartedAt >= 1d)
            {
                VerifyCombatInvariants();
                Require(army.ActiveCrashCount == 1 && army.TotalCrashesStarted == 1, "Crash concurrency or count is incorrect.");
                Require(crashingUnit != null && crashingUnit.position.y < crashStartPosition.y - 0.35f, "Crashing helicopter did not fall.");
                Require(army.ActiveCrashAccumulatedRotation > 120f, "Crashing helicopter did not accumulate enough self-rotation.");
                Require(army.ActiveCrashSmoke != null && army.ActiveCrashSmoke.isPlaying, "Crashing helicopter smoke is not playing.");
                CaptureCamera("crash.png");
                CaptureArmyIsolation("army-crash-isolated.png");
                Append(
                    $"PASS units={army.SpawnedAirUnitCount} gatling={army.TotalGatlingFlashes} shots={army.TotalCosmeticShotsFired} " +
                    $"crashes={army.TotalCrashesStarted} crashDrop={crashStartPosition.y - crashingUnit.position.y:F3} " +
                    $"crashRotation={army.ActiveCrashAccumulatedRotation:F2} " +
                    $"bossHp={boss.CurrentHealth:F1} player={player.CurrentHull:F1}/{player.CurrentArmor:F1} targets={targetProvider.ValidTargetCount}");
                Debug.Log("[BackgroundAllyArmy] PASS " + Path.GetFullPath(OutputFolder));
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
        army = UnityEngine.Object.FindAnyObjectByType<BackgroundAllyArmyController>();
        boss = UnityEngine.Object.FindAnyObjectByType<BossController>();
        player = UnityEngine.Object.FindAnyObjectByType<PlayerCombatController>();
        bossAttack = UnityEngine.Object.FindAnyObjectByType<BossAttackController>();
        bossPattern = UnityEngine.Object.FindAnyObjectByType<BossBulletPatternController>();
        targetProvider = UnityEngine.Object.FindAnyObjectByType<BossLockOnTargetProvider>();
        if (army == null || boss == null || player == null || targetProvider == null || army.SpawnedAirUnitCount == 0)
        {
            return false;
        }

        player.SetCombatEnabled(false);
        previousUndead = GameplayDebugFlags.Undead;
        GameplayDebugFlags.Undead = true;
        if (bossAttack != null)
        {
            bossAttack.enabled = false;
        }
        if (bossPattern != null)
        {
            bossPattern.enabled = false;
        }

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

    private static void CaptureCombatBaseline()
    {
        bossHealth = boss.CurrentHealth;
        playerHull = player.CurrentHull;
        playerArmor = player.CurrentArmor;
        targetCount = targetProvider.ValidTargetCount;
    }

    private static void VerifyInitialRuntime()
    {
        BackgroundAllyUnitView[] views = UnityEngine.Object.FindObjectsByType<BackgroundAllyUnitView>(FindObjectsInactive.Exclude);
        Require(army.SpawnedAirUnitCount == 4, $"Expected 4 background choppers, got {army.SpawnedAirUnitCount}.");
        Require(views.Length == 4, $"Expected 4 background chopper views, got {views.Length}.");
        Require(views.All(view => Mathf.Abs(view.transform.localScale.x - 1.35f) < 0.001f), "Background chopper scale is not the requested 1.35.");
        Require(army.ActiveCosmeticAttackCount == 0, "A cosmetic attack started before the forced verification run.");
        Require(Mathf.Abs(army.AttackMotionSpeedScale - 0.5f) < 0.001f, $"Attack motion speed scale is not 0.5: {army.AttackMotionSpeedScale:F3}.");
        Require(army.GetComponentsInChildren<Collider>(true).Length == 0, "Background ally army contains a Collider.");
        Require(army.GetComponentsInChildren<Rigidbody>(true).Length == 0, "Background ally army contains a Rigidbody.");

        MeshFilter[] modelFilters = views
            .Select(view => view.GetComponentsInChildren<MeshFilter>(true).FirstOrDefault(filter => filter.name == "Model"))
            .ToArray();
        Require(modelFilters.All(filter => filter != null && filter.sharedMesh != null), "A background chopper model mesh is missing.");
        Mesh sharedMesh = modelFilters[0].sharedMesh;
        Require(modelFilters.All(filter => filter.sharedMesh == sharedMesh), "Background choppers do not share one Mesh.");
        Require(sharedMesh.triangles.Length / 3 == 500, $"Background chopper mesh is not 500 tris: {sharedMesh.triangles.Length / 3}.");

        Material[] bodyMaterials = views
            .Select(view => view.CachedRenderers.FirstOrDefault(renderer => renderer != null && renderer.sharedMaterial != null && renderer.sharedMaterial.name == "BackgroundChopper_500")?.sharedMaterial)
            .ToArray();
        Require(bodyMaterials.All(material => material != null && material == bodyMaterials[0]), "Background choppers do not share one body Material.");
        Require(bodyMaterials[0].enableInstancing, "Background chopper Material does not enable GPU instancing.");

        initialPositions = views.Select(view => view.transform.position).ToArray();
        previousFlightPositions.Clear();
        for (int i = 0; i < views.Length; i++)
        {
            previousFlightPositions[views[i].transform] = views[i].transform.position;
        }
        maximumUpDeviation = 0f;
        minimumForwardMovementDot = 1f;
        flightAttitudeSamples = 0;
        Camera camera = Camera.main;
        for (int i = 0; i < views.Length; i++)
        {
            Vector3 viewport = camera != null ? camera.WorldToViewportPoint(views[i].transform.position) : Vector3.zero;
            Vector2 projectedSize = camera != null ? CalculateProjectedViewportSize(camera, views[i].CachedRenderers) : Vector2.zero;
            Append(
                $"unit={views[i].name} world={views[i].transform.position:F3} viewport={viewport:F3} " +
                $"projected={projectedSize.x * 1280f:F1}x{projectedSize.y * 720f:F1}px scale={views[i].transform.localScale.x:F3}");
        }
        Require(views.All(view => view.MainRotorBlur != null && view.TailRotorBlur != null && view.Muzzle != null), "Rotor blur or muzzle anchors are missing.");
        Require(views.All(view => view.CrashSmoke != null), "Crash smoke bindings are missing.");
        for (int i = 0; i < views.Length; i++)
        {
            Transform model = views[i].VisualRoot.Find("Model");
            Require(model != null, "Background chopper Model transform is missing.");
            float yaw = Mathf.Repeat(model.localEulerAngles.y, 360f);
            Require(Mathf.Abs(Mathf.DeltaAngle(yaw, 270f)) <= 0.1f, $"Background chopper visual forward offset is not reversed: yaw={yaw:F2}.");
            Vector3 muzzleDirection = (views[i].Muzzle.position - views[i].transform.position).normalized;
            Require(Vector3.Dot(muzzleDirection, views[i].transform.forward) > 0.8f, "Background chopper muzzle is not on its forward side.");
        }
        VerifyCombatInvariants();
    }

    private static void VerifyCombatInvariants()
    {
        Require(Mathf.Abs(boss.CurrentHealth - bossHealth) < 0.001f, "Cosmetic attack changed boss health.");
        Require(Mathf.Abs(player.CurrentHull - playerHull) < 0.001f, "Cosmetic attack changed player Hull.");
        Require(Mathf.Abs(player.CurrentArmor - playerArmor) < 0.001f, "Cosmetic attack changed player Armor.");
        Require(targetProvider.ValidTargetCount == targetCount, "Cosmetic attack changed lock-on target count.");
    }

    private static void VerifyMovementAndReuse()
    {
        BackgroundAllyUnitView[] views = UnityEngine.Object.FindObjectsByType<BackgroundAllyUnitView>(FindObjectsInactive.Exclude);
        Require(views.Length == initialPositions.Length, "Background chopper count changed during the run.");
        bool moved = false;
        for (int i = 0; i < views.Length; i++)
        {
            moved |= Vector3.Distance(initialPositions[i], views[i].transform.position) > 0.25f;
        }

        Require(moved, "Background choppers did not move during the verification run.");
        Require(army.ActiveTracerCount == 0, "Tracer pool did not release all active tracers.");
        Require(army.TotalCosmeticShotsFired >= 2 && army.TotalCosmeticShotsFired <= 4, "Cosmetic shot count left the configured 2-4 range.");
        Require(flightAttitudeSamples > 0, "No flight attitude samples were recorded.");
        Require(maximumUpDeviation <= 16f, $"A background chopper stood too vertically: {maximumUpDeviation:F2} degrees from world up.");
        Require(minimumForwardMovementDot >= 0.35f, $"A background chopper moved backwards relative to its nose: dot={minimumForwardMovementDot:F3}.");
        Append($"attitude maxUpDeviation={maximumUpDeviation:F3} minForwardMovementDot={minimumForwardMovementDot:F3} samples={flightAttitudeSamples}");
    }

    private static void TrackFlightAttitude()
    {
        BackgroundAllyUnitView[] views = UnityEngine.Object.FindObjectsByType<BackgroundAllyUnitView>(FindObjectsInactive.Exclude);
        for (int i = 0; i < views.Length; i++)
        {
            Transform unit = views[i].transform;
            maximumUpDeviation = Mathf.Max(maximumUpDeviation, Vector3.Angle(unit.up, Vector3.up));
            if (previousFlightPositions.TryGetValue(unit, out Vector3 previous))
            {
                Vector3 movement = unit.position - previous;
                Vector3 planarMovement = Vector3.ProjectOnPlane(movement, Vector3.up);
                Vector3 planarForward = Vector3.ProjectOnPlane(unit.forward, Vector3.up);
                if (planarMovement.sqrMagnitude > 0.000025f && planarForward.sqrMagnitude > 0.000001f)
                {
                    float dot = Vector3.Dot(planarMovement.normalized, planarForward.normalized);
                    minimumForwardMovementDot = Mathf.Min(minimumForwardMovementDot, dot);
                    flightAttitudeSamples++;
                }
            }

            previousFlightPositions[unit] = unit.position;
        }
    }

    private static void CaptureCamera(string fileName)
    {
        Camera camera = Camera.main;
        Require(camera != null, "Main camera is missing for background ally capture.");
        Renderer[] playerRenderers = player != null ? player.GetComponentsInChildren<Renderer>(true) : Array.Empty<Renderer>();
        bool[] playerRendererStates = playerRenderers.Select(renderer => renderer.enabled).ToArray();
        const int width = 1280;
        const int height = 720;
        RenderTexture renderTexture = new(width, height, 24, RenderTextureFormat.ARGB32);
        Texture2D texture = new(width, height, TextureFormat.RGB24, false);
        RenderTexture previousActive = RenderTexture.active;
        RenderTexture previousTarget = camera.targetTexture;
        try
        {
            for (int i = 0; i < playerRenderers.Length; i++)
            {
                playerRenderers[i].enabled = false;
            }

            camera.targetTexture = renderTexture;
            camera.Render();
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
            for (int i = 0; i < playerRenderers.Length; i++)
            {
                if (playerRenderers[i] != null)
                {
                    playerRenderers[i].enabled = playerRendererStates[i];
                }
            }
            UnityEngine.Object.DestroyImmediate(texture);
            UnityEngine.Object.DestroyImmediate(renderTexture);
        }
    }

    private static Vector2 CalculateProjectedViewportSize(Camera camera, Renderer[] renderers)
    {
        bool hasBounds = false;
        Bounds combined = default;
        for (int i = 0; i < renderers.Length; i++)
        {
            Renderer renderer = renderers[i];
            if (renderer == null || !renderer.enabled || renderer is ParticleSystemRenderer || renderer.name.Contains("Flash"))
            {
                continue;
            }

            if (!hasBounds)
            {
                combined = renderer.bounds;
                hasBounds = true;
            }
            else
            {
                combined.Encapsulate(renderer.bounds);
            }
        }

        if (!hasBounds)
        {
            return Vector2.zero;
        }

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

    private static void CaptureArmyIsolation(string fileName)
    {
        Renderer[] armyRenderers = army.GetComponentsInChildren<Renderer>(true);
        Renderer[] allRenderers = UnityEngine.Object.FindObjectsByType<Renderer>(FindObjectsInactive.Exclude);
        bool[] states = allRenderers.Select(renderer => renderer.enabled).ToArray();
        try
        {
            for (int i = 0; i < allRenderers.Length; i++)
            {
                allRenderers[i].enabled = armyRenderers.Contains(allRenderers[i]);
            }

            CaptureCamera(fileName);
        }
        finally
        {
            for (int i = 0; i < allRenderers.Length; i++)
            {
                if (allRenderers[i] != null)
                {
                    allRenderers[i].enabled = states[i];
                }
            }
        }
    }

    private static void Finish(int code)
    {
        exitCode = code;
        if (EditorApplication.isPlaying)
        {
            EditorApplication.ExitPlaymode();
            return;
        }

        CleanupCallbacks();
        if (Application.isBatchMode)
        {
            EditorApplication.Exit(exitCode);
        }
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
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }

    private static void Append(string line)
    {
        File.AppendAllText(Path.Combine(OutputFolder, "runtime.txt"), line + Environment.NewLine);
    }
}
#endif
