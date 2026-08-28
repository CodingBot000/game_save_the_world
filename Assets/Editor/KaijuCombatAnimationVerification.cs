using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>Deterministic Play Mode regression checks on an isolated copy, never the live boss.</summary>
public static class KaijuCombatAnimationVerification
{
    private static Coroutine sweepVerification;

    [MenuItem("Tools/TitanDestroyer/Kaiju Combat/7. Verify active sweep death (ends Play encounter)")]
    public static void VerifyActiveSweepDeath()
    {
        Require(Application.isPlaying && sweepVerification == null, "Start a fresh BattleArena Play session, without another sweep test.");
        UnityEngine.Object.FindAnyObjectByType<BattleController>().StartCoroutine(VerifyActiveSweepDeathRoutine());
    }

    private static IEnumerator VerifyActiveSweepDeathRoutine()
    {
        var pattern = UnityEngine.Object.FindAnyObjectByType<BossBulletPatternController>();
        var driver = UnityEngine.Object.FindAnyObjectByType<KaijuBossAnimationDriver>();
        var boss = driver.GetComponentInParent<BossController>();
        const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
        typeof(BossBulletPatternController).GetMethod("CancelActivePattern", flags).Invoke(pattern, null);
        typeof(BossBulletPatternController).GetMethod("CleanupTelegraphs", flags).Invoke(pattern, null);
        typeof(BossBulletPatternController).GetField("attackCooldownRemaining", flags).SetValue(pattern, 600f);
        Require(pattern.TryRunSweepForDebug(true), "Death probe could not start.");
        float deadline = Time.realtimeSinceStartup + 10f;
        while (!pattern.HasSweepFrame || pattern.LastSweepFrame.Damage <= 0f)
        {
            Require(Time.realtimeSinceStartup < deadline, "Death probe did not reach an active beam.");
            yield return null;
        }
        boss.ApplyDamage(boss.MaxHealth * 2f);
        yield return null;
        Require(driver.IsDead && !driver.HasSweepAim && !pattern.HasSweepFrame && pattern.ActiveTelegraphCount == 0,
            "Death left a damaging beam or procedural pose.");
        Require(GameObject.Find("BossAcceleratingSweepBeam") == null, "Death left a beam visual.");
        Debug.Log("[SweepDeath] PASS: active beam/telegraph/aim removed; Death pose owns the skeleton.");
    }

    [MenuItem("Tools/TitanDestroyer/Kaiju Combat/6. Verify live sweep alignment (Play Mode)")]
    public static void VerifyLiveSweep()
    {
        Require(Application.isPlaying, "Run in BattleArena Play Mode.");
        Require(sweepVerification == null, "Sweep verification is already running.");
        BattleController battle = UnityEngine.Object.FindAnyObjectByType<BattleController>();
        Require(battle != null, "Battle missing.");
        sweepVerification = battle.StartCoroutine(VerifyLiveSweepRoutine());
    }

    private static IEnumerator VerifyLiveSweepRoutine()
    {
        var pattern = UnityEngine.Object.FindAnyObjectByType<BossBulletPatternController>();
        var driver = UnityEngine.Object.FindAnyObjectByType<KaijuBossAnimationDriver>();
        var player = UnityEngine.Object.FindAnyObjectByType<PlayerCombatController>();
        var orbit = UnityEngine.Object.FindAnyObjectByType<PlayerOrbitController>();
        Require(pattern != null && driver != null && player != null && orbit != null, "Battle runtime missing.");
        const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
        FieldInfo cooldown = typeof(BossBulletPatternController).GetField("attackCooldownRemaining", flags);
        MethodInfo cancel = typeof(BossBulletPatternController).GetMethod("CancelActivePattern", flags);
        MethodInfo cleanup = typeof(BossBulletPatternController).GetMethod("CleanupTelegraphs", flags);
        float previousCooldown = (float)cooldown.GetValue(pattern);
        float previousCaptureDelta = Time.captureDeltaTime;
        bool previousInput = orbit.DebugInputEnabled;
        bool previousOrbitEnabled = orbit.enabled;
        Camera camera = Camera.main;
        float previousAspect = camera.aspect;
        Vector3 previousPlayerPosition = orbit.transform.position;
        Vector3 playerViewport = camera.WorldToViewportPoint(player.HitPoint);
        var boss = driver.GetComponentInParent<BossController>();
        Quaternion previousBossRotation = boss.transform.rotation;
        bool previousCombatEnabled = (bool)typeof(PlayerCombatController).GetField("combatEnabled", flags).GetValue(player);
        string folder = Path.Combine(Path.GetTempPath(), "TitanDestroyerSweepLive-" + DateTime.UtcNow.ToString("yyyyMMdd-HHmmssfff"));
        Directory.CreateDirectory(folder);
        var report = new System.Text.StringBuilder();
        Action<BossBulletPatternController.SweepFrame> observer = null;
        try
        {
            cancel.Invoke(pattern, null);
            cleanup.Invoke(pattern, null);
            orbit.SetInputEnabled(false);
            orbit.enabled = false; // Position the hit point deterministically, without movement clamping.
            player.SetCombatEnabled(false);
            var cases = new System.Collections.Generic.List<(int fps, float aspect, Vector2 position)>();
            foreach (int rate in new[] { 30, 60, 120 }) cases.Add((rate, previousAspect, new Vector2(0.5f, 0.5f)));
            foreach (float aspect in new[] { 16f / 9f, 19.5f / 9f, 4f / 3f })
            foreach (Vector2 point in new[] { new Vector2(0.5f, 0.5f), new Vector2(0.08f, 0.5f), new Vector2(0.92f, 0.5f), new Vector2(0.5f, 0.08f), new Vector2(0.5f, 0.92f) })
                cases.Add((60, aspect, point));
            foreach (Vector2 corner in new[] { new Vector2(0.08f, 0.08f), new Vector2(0.08f, 0.92f), new Vector2(0.92f, 0.08f), new Vector2(0.92f, 0.92f) })
                cases.Add((60, previousAspect, corner));
            int caseIndex = 0;
            foreach (var scenario in cases)
            foreach (bool leftToRight in new[] { true, false })
            {
                int fps = scenario.fps;
                Time.captureDeltaTime = 1f / fps;
                cooldown.SetValue(pattern, 600f);
                driver.CancelAction();
                driver.BeginPattern();
                boss.transform.rotation = previousBossRotation;
                camera.aspect = scenario.aspect;
                Vector3 targetPoint = camera.ViewportToWorldPoint(new Vector3(scenario.position.x, scenario.position.y, playerViewport.z));
                orbit.transform.position += targetPoint - player.HitPoint;
                yield return null;
                player.RefillForDebug();
                float maxAngle = 0f, maxOrigin = 0f, maxVisualDirection = 0f, maxCorrection = 0f;
                float firstWarningTime = -1f, firstDamageTime = -1f, lastDamageTime = -1f;
                float firstViewportX = float.NaN, finalViewportX = float.NaN;
                Plane playerPlane = new Plane(camera.transform.forward, player.HitPoint);
                float maxProgressError = 0f, finalProgress = 0f;
                int damagingFrames = 0, samples = 0, lastFrame = -1;
                var captured = new System.Collections.Generic.HashSet<int>();
                float started = Time.time;
                observer = frame =>
                {
                    if (frame.Damage <= 0f)
                    {
                        if (firstWarningTime < 0f) firstWarningTime = Time.time;
                        return;
                    }
                    damagingFrames++;
                    if (frame.Frame == lastFrame) throw new InvalidOperationException("Duplicate sweep frame.");
                    lastFrame = frame.Frame;
                    if (firstDamageTime < 0f) firstDamageTime = Time.time;
                    lastDamageTime = Time.time;
                    maxAngle = Mathf.Max(maxAngle, Vector3.Angle(driver.Mouth.forward, frame.Direction));
                    maxCorrection = Mathf.Max(maxCorrection, driver.SweepCorrectionAngle);
                    GameObject beam = GameObject.Find("BossAcceleratingSweepBeam");
                    Require(beam != null, "Active beam visual missing.");
                    Vector3 near = beam.transform.position - beam.transform.forward * (beam.transform.localScale.z * 0.5f);
                    maxOrigin = Mathf.Max(maxOrigin, Vector3.Distance(near, driver.Mouth.position));
                    maxVisualDirection = Mathf.Max(maxVisualDirection, Vector3.Angle(beam.transform.forward, frame.Direction));
                    finalProgress = pattern.LastSweepProgress;
                    if (playerPlane.Raycast(new Ray(frame.Origin, frame.Direction), out float distance))
                    {
                        float screenX = camera.WorldToViewportPoint(frame.Origin + frame.Direction * distance).x;
                        if (float.IsNaN(firstViewportX)) firstViewportX = screenX;
                        finalViewportX = screenX;
                    }
                    float expected = BossBulletPatternController.EvaluateSweepProgress(Time.time - firstDamageTime, 0.3f, 0.2f);
                    maxProgressError = Mathf.Max(maxProgressError, Mathf.Abs(expected - finalProgress));
                    Require(Mathf.Abs(frame.Damage - 18f) < 0.001f && Mathf.Abs(frame.Length - 70f) < 0.001f, "Sweep balance changed.");
                    int bucket = finalProgress >= 0.99f ? 2 : finalProgress >= 0.2f ? 1 : 0;
                    if (caseIndex < 6 && fps == 60 && captured.Add(bucket))
                        ScreenCapture.CaptureScreenshot(Path.Combine(folder, $"{fps}-{(leftToRight ? "ltr" : "rtl")}-{bucket}.png"));
                    samples++;
                };
                pattern.SweepFrameApplied += observer;
                Require(pattern.TryRunSweepForDebug(leftToRight), "Could not start deterministic sweep.");
                float deadline = Time.realtimeSinceStartup + 30f;
                while (pattern.IsPatternRunning)
                {
                    Require(Time.realtimeSinceStartup < deadline, "Sweep runtime stalled.");
                    yield return null;
                }
                pattern.SweepFrameApplied -= observer;
                observer = null;
                string line = $"case={caseIndex++} fps={fps} aspect={scenario.aspect:F3} player={scenario.position} ltr={leftToRight} samples={samples} requestToWarning={firstWarningTime - started:F5} windup={firstDamageTime - firstWarningTime:F5} active={lastDamageTime - firstDamageTime:F5} angle={maxAngle:F5} origin={maxOrigin:F6} visualDirection={maxVisualDirection:F5} correction={maxCorrection:F3} screenX={firstViewportX:F3}->{finalViewportX:F3} progressError={maxProgressError:F6} final={finalProgress:F3} aimRemaining={driver.HasSweepAim} visuals={pattern.ActiveTelegraphCount}";
                report.AppendLine(line);
                File.WriteAllText(Path.Combine(folder, "runtime.txt"), report.ToString());
                Debug.Log("[SweepLive] " + line);
                Require(damagingFrames > 0 && maxAngle <= 2f && maxOrigin <= 0.02f && maxVisualDirection <= 0.1f, "Sweep alignment exceeded tolerance.");
                Require(firstWarningTime >= 0f && Mathf.Abs(firstDamageTime - firstWarningTime - 0.8f) <= 1f / fps + 0.003f, "Visible sweep windup drifted.");
                Require(Mathf.Abs(lastDamageTime - firstDamageTime - 0.5f) <= 1f / fps + 0.003f, "Sweep duration drifted.");
                Require(maxProgressError < 0.001f && finalProgress >= 0.999f, "Sweep progress drifted.");
                Require(leftToRight ? firstViewportX <= 0f && finalViewportX >= 1f : firstViewportX >= 1f && finalViewportX <= 0f, "Sweep no longer covers both viewport edges.");
                Require(!driver.HasSweepAim && !pattern.HasSweepFrame && pattern.ActiveTelegraphCount == 0, "Sweep did not recover cleanly.");
            }

            foreach (bool duringActive in new[] { false, true })
            {
                cooldown.SetValue(pattern, 600f);
                driver.CancelAction();
                Require(pattern.TryRunSweepForDebug(true), "Cancel probe could not start.");
                float started = Time.time;
                while (Time.time - started < (duringActive ? 0.9f : 0.2f)) yield return null;
                pattern.SetCinematicPaused(true);
                Require(!pattern.HasSweepFrame && !driver.HasSweepAim && pattern.ActiveTelegraphCount == 0, "Cinematic cancellation left a live beam.");
                pattern.SetCinematicPaused(false);
                report.AppendLine("cinematicCancelActive=" + duringActive + " passed");
            }
            cooldown.SetValue(pattern, 600f);
            driver.CancelAction();
            Require(pattern.TryRunSweepForDebug(false), "Disable probe could not start.");
            float disableStart = Time.time;
            while (Time.time - disableStart < 0.9f) yield return null;
            pattern.enabled = false;
            Require(!pattern.HasSweepFrame && !driver.HasSweepAim && pattern.ActiveTelegraphCount == 0, "Disable left a live sweep.");
            pattern.enabled = true;
            report.AppendLine("disableActive passed");
            File.WriteAllText(Path.Combine(folder, "runtime.txt"), report.ToString());
            Debug.Log("[SweepLive] PASS " + folder);
        }
        finally
        {
            if (observer != null) pattern.SweepFrameApplied -= observer;
            cancel.Invoke(pattern, null);
            cleanup.Invoke(pattern, null);
            cooldown.SetValue(pattern, previousCooldown);
            Time.captureDeltaTime = previousCaptureDelta;
            camera.aspect = previousAspect;
            orbit.transform.position = previousPlayerPosition;
            boss.transform.rotation = previousBossRotation;
            orbit.enabled = previousOrbitEnabled;
            orbit.SetInputEnabled(previousInput);
            player.SetCombatEnabled(previousCombatEnabled);
            sweepVerification = null;
        }
    }

    [MenuItem("Tools/TitanDestroyer/Kaiju Combat/4. Capture sweep baseline")]
    public static void CaptureSweepBaseline() => CaptureSweep(false);

    [MenuItem("Tools/TitanDestroyer/Kaiju Combat/5. Capture aligned sweep")]
    public static void CaptureAlignedSweep() => CaptureSweep(true);

    private static void CaptureSweep(bool aligned)
    {
        var source = UnityEngine.Object.FindObjectsByType<KaijuBossAnimationDriver>(FindObjectsSortMode.None)
            .FirstOrDefault(d => d.gameObject.scene.path == KaijuCombatAnimationBuilder.ScenePath);
        Require(source != null && source.Mouth != null, "Open BattleArena with its Kaiju mouth socket.");
        Camera reference = Camera.main;
        Require(reference != null, "Main camera missing.");
        var player = UnityEngine.Object.FindAnyObjectByType<PlayerCombatController>();
        Require(player != null, "Player missing.");
        string folder = Path.Combine(Path.GetTempPath(), "TitanDestroyerSweepReview-" + DateTime.UtcNow.ToString("yyyyMMdd-HHmmssfff"));
        Directory.CreateDirectory(folder);
        Scene preview = EditorSceneManager.NewPreviewScene();
        GameObject copy = UnityEngine.Object.Instantiate(source.GetComponentInParent<BossController>().gameObject);
        copy.name = "SweepReview_Temporary";
        SceneManager.MoveGameObjectToScene(copy, preview);
        try
        {
            foreach (MonoBehaviour b in copy.GetComponentsInChildren<MonoBehaviour>()) b.enabled = false;
            foreach (Collider c in copy.GetComponentsInChildren<Collider>()) c.enabled = false;
            var driver = copy.GetComponentInChildren<KaijuBossAnimationDriver>();
            if (aligned)
            {
                driver.enabled = true;
                copy.GetComponent<BossController>().SetCurrentHealthForDebug(2000f);
            }
            Animator animator = driver.Animator;
            animator.enabled = false;
            animator.fireEvents = false;
            AnimationClip idle = SweepClip("BasicIdle");
            idle.SampleAnimation(animator.gameObject, 0f);
            Transform mouth = driver.Mouth;
            Vector3 initialOrigin = mouth.position;
            Vector3 vp = reference.WorldToViewportPoint(player.HitPoint);
            float depth = Mathf.Max(0.1f, vp.z);
            float height = Mathf.Clamp(vp.y, 0.08f, 0.92f);
            Vector3 left = (reference.ViewportToWorldPoint(new Vector3(-0.08f, height, depth)) - initialOrigin).normalized;
            Vector3 right = (reference.ViewportToWorldPoint(new Vector3(1.08f, height, depth)) - initialOrigin).normalized;
            var report = new System.Text.StringBuilder();
            report.AppendLine($"aligned={aligned}; playMode={Application.isPlaying}; aspect={reference.aspect}; mouthLocal={mouth.localPosition:F6}; socketRotation={mouth.localEulerAngles:F3}");
            report.AppendLine($"origin={initialOrigin:F4}; player={player.HitPoint:F4}; left={left:F4}; right={right:F4}");
            foreach (bool ltr in new[] { true, false })
            {
                bool clipLeftToRight = aligned
                    ? KaijuBossAnimationDriver.SelectSweepClipLeftToRight(ltr ? left : right, ltr ? right : left) : ltr;
                AnimationClip clip = SweepClip(clipLeftToRight ? "Attack_BeamLeftToR" : "Attack_BeamRightToL");
                foreach (float elapsed in new[] { 0f, 0.15f, 0.3f, 0.4f, 0.5f })
                {
                    int ticket = -1;
                    if (aligned)
                    {
                        driver.CancelAction();
                        ticket = driver.BeginBeam(clipLeftToRight, 0.8f, 0.5f);
                        Require(driver.TryBeginSweepAim(ticket), "Sweep pose binding failed.");
                    }
                    float time = KaijuBossAnimationDriver.BeamCueTime + elapsed * 3.2f;
                    idle.SampleAnimation(animator.gameObject, 0f);
                    clip.SampleAnimation(animator.gameObject, time);
                    float p = elapsed < 0.3f ? 0.2f * elapsed / 0.3f : Mathf.Lerp(0.2f, 1f, 1f - Mathf.Pow(1f - Mathf.Clamp01((elapsed - 0.3f) / 0.2f), 3f));
                    Vector3 beam = Vector3.Slerp(ltr ? left : right, ltr ? right : left, p).normalized;
                    if (aligned) Require(driver.TryApplySweepAim(ticket, beam, 1f), "Sweep pose correction failed.");
                    Vector3 radial = (mouth.position - mouth.parent.position).normalized;
                    report.AppendLine($"{clip.name} elapsed={elapsed:F2} clip={time:F3} progress={p:F3} rawForward={mouth.forward:F4} radial={radial:F4} beam={beam:F4} rawError={Vector3.Angle(mouth.forward, beam):F3} radialError={Vector3.Angle(radial, beam):F3}");
                    // Camera.Render can reuse skinned geometry within one Editor frame.
                    // Bake each sampled pose so the preview really shows the current bones.
                    var skins = copy.GetComponentsInChildren<SkinnedMeshRenderer>();
                    var baked = skins.Select(skin =>
                    {
                        var mesh = new Mesh();
                        skin.BakeMesh(mesh);
                        var visual = new GameObject("BakedPose");
                        visual.transform.SetParent(skin.transform, false);
                        visual.AddComponent<MeshFilter>().sharedMesh = mesh;
                        visual.AddComponent<MeshRenderer>().sharedMaterials = skin.sharedMaterials;
                        skin.enabled = false;
                        return visual;
                    }).ToArray();
                    var rays = new GameObject("ReviewRays");
                    SceneManager.MoveGameObjectToScene(rays, preview);
                    AddReviewRay(rays.transform, mouth.position, mouth.forward, Color.red, 7f);
                    AddReviewRay(rays.transform, mouth.position, radial, Color.green, 7f);
                    AddReviewRay(rays.transform, mouth.position, beam, Color.cyan, 20f);
                    string tag = (ltr ? "left-to-right" : "right-to-left") + "-" + Mathf.RoundToInt(elapsed * 100f);
                    CaptureReview(preview, reference, Path.Combine(folder, tag + ".png"), null);
                    CaptureReview(preview, reference, Path.Combine(folder, tag + "-mouth.png"), mouth.position);
                    foreach (Renderer renderer in rays.GetComponentsInChildren<Renderer>())
                        UnityEngine.Object.DestroyImmediate(renderer.sharedMaterial);
                    UnityEngine.Object.DestroyImmediate(rays);
                    foreach (GameObject visual in baked)
                    {
                        UnityEngine.Object.DestroyImmediate(visual.GetComponent<MeshFilter>().sharedMesh);
                        UnityEngine.Object.DestroyImmediate(visual);
                    }
                    foreach (SkinnedMeshRenderer skin in skins) skin.enabled = true;
                }
            }
            File.WriteAllText(Path.Combine(folder, "baseline.txt"), report.ToString());
            Debug.Log("[SweepReview] " + folder + "\n" + report);
        }
        finally { EditorSceneManager.ClosePreviewScene(preview); }
    }

    private static AnimationClip SweepClip(string suffix) => AssetDatabase.LoadAssetAtPath<AnimationClip>(
        "Assets/Animation/Invader/Clips/Kaiju_" + suffix + ".anim");

    private static void AddReviewRay(Transform root, Vector3 origin, Vector3 direction, Color color, float length)
    {
        var go = new GameObject("Axis");
        go.transform.SetParent(root, false);
        var line = go.AddComponent<LineRenderer>();
        line.sharedMaterial = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
        line.sharedMaterial.SetColor("_BaseColor", color);
        line.startWidth = line.endWidth = 0.1f;
        line.positionCount = 2;
        line.SetPosition(0, origin);
        line.SetPosition(1, origin + direction * length);
    }

    private static void CaptureReview(Scene preview, Camera reference, string file, Vector3? mouthPoint)
    {
        var go = new GameObject("ReviewCamera");
        SceneManager.MoveGameObjectToScene(go, preview);
        Camera camera = go.AddComponent<Camera>();
        camera.CopyFrom(reference);
        camera.enabled = false;
        camera.scene = preview;
        camera.transform.SetPositionAndRotation(reference.transform.position, reference.transform.rotation);
        camera.clearFlags = CameraClearFlags.SolidColor;
        camera.backgroundColor = new Color(0.12f, 0.14f, 0.18f);
        camera.aspect = 16f / 9f;
        if (mouthPoint.HasValue)
        {
            Vector3 towardCamera = (reference.transform.position - mouthPoint.Value).normalized;
            camera.transform.position = mouthPoint.Value + towardCamera * 17f + reference.transform.right * 6f;
            camera.transform.LookAt(mouthPoint.Value);
            camera.fieldOfView = 40f;
        }
        var lightGo = new GameObject("ReviewLight");
        SceneManager.MoveGameObjectToScene(lightGo, preview);
        Light light = lightGo.AddComponent<Light>();
        light.type = LightType.Directional;
        light.intensity = 2f;
        light.transform.rotation = reference.transform.rotation;
        var rt = new RenderTexture(1280, 720, 24);
        var tex = new Texture2D(1280, 720, TextureFormat.RGB24, false);
        RenderTexture previous = RenderTexture.active;
        try
        {
            camera.targetTexture = rt;
            camera.Render();
            RenderTexture.active = rt;
            tex.ReadPixels(new Rect(0, 0, 1280, 720), 0, 0);
            tex.Apply();
            File.WriteAllBytes(file, tex.EncodeToPNG());
        }
        finally
        {
            RenderTexture.active = previous;
            camera.targetTexture = null;
            UnityEngine.Object.DestroyImmediate(tex);
            UnityEngine.Object.DestroyImmediate(rt);
            UnityEngine.Object.DestroyImmediate(lightGo);
            UnityEngine.Object.DestroyImmediate(go);
        }
    }

    [MenuItem("Tools/TitanDestroyer/Kaiju Combat/3. Verify runtime poses and events (Play Mode)")]
    public static void VerifyRuntime()
    {
        Require(EditorApplication.isPlaying, "Run in BattleArena Play Mode.");
        var source = UnityEngine.Object.FindObjectsByType<KaijuBossAnimationDriver>(FindObjectsSortMode.None)
            .FirstOrDefault(d => d.gameObject.scene.path == KaijuCombatAnimationBuilder.ScenePath);
        Require(source != null, "No combat Kaiju in the current scene.");
        GameObject copy = UnityEngine.Object.Instantiate(source.GetComponentInParent<BossController>().gameObject);
        copy.name = "KaijuCombatVerification_Temporary";
        try
        {
            var driver = copy.GetComponentInChildren<KaijuBossAnimationDriver>();
            foreach (MonoBehaviour behaviour in copy.GetComponentsInChildren<MonoBehaviour>())
                if (behaviour != driver) behaviour.enabled = false;
            foreach (Collider collider in copy.GetComponentsInChildren<Collider>()) collider.enabled = false;
            var boss = copy.GetComponent<BossController>();
            boss.SetCurrentHealthForDebug(boss.MaxHealth);
            Animator animator = driver.Animator;
            animator.Rebind();
            animator.Update(0f);
            driver.CancelAction();

            foreach (float angle in new[] { -45f, -22.5f, 0f, 22.5f, 45f })
            {
                animator.SetFloat("TargetAngle", angle);
                int before = driver.ReleasedCueCount;
                int ticket = driver.BeginFiring();
                Advance(driver, 0.2f);
                Require(!driver.WasReleased(ticket), "Projectile released before frame 9.");
                Advance(driver, 0.2f);
                Require(driver.WasReleased(ticket) && driver.ReleasedCueCount == before + 1,
                    "Missing or duplicate blend-tree firing event at angle " + angle);
                Advance(driver, 1f);
                Require(!driver.IsBusy, "Firing did not recover.");
            }

            VerifyMaskedLegs(driver);
            foreach (string trigger in new[] { "Attack1", "Attack2" })
            {
                driver.CancelAction();
                int before = driver.ReleasedCueCount;
                animator.SetTrigger(trigger);
                Advance(driver, 0.15f);
                Require(driver.IsBusy, "Legacy trigger did not enter an action: " + trigger);
                Advance(driver, 3.5f);
                Require(!driver.IsBusy && driver.ReleasedCueCount == before + 1, "Legacy trigger did not release/recover: " + trigger);
            }
            foreach (bool leftToRight in new[] { true, false })
            {
                int ticket = driver.BeginBeam(leftToRight, 0.8f, 0.5f);
                Require(Mathf.Approximately(animator.GetLayerWeight(1), 0f), "Upper body overriding beam.");
                Advance(driver, 0.5f);
                Require(!driver.WasReleased(ticket), "Beam released before windup.");
                Advance(driver, 0.4f);
                Require(driver.WasReleased(ticket), "Beam start event missing.");
                Advance(driver, 1.5f);
                Require(!driver.IsBusy && Mathf.Approximately(animator.GetLayerWeight(1), 1f), "Beam recovery failed.");
            }

            int tail = driver.BeginTail();
            Advance(driver, 1.4f);
            Require(!driver.WasReleased(tail), "Tail released before impact.");
            Advance(driver, 0.3f);
            Require(driver.WasReleased(tail), "Tail impact event missing.");
            Advance(driver, 1.5f);
            Require(!driver.IsBusy, "Tail recovery failed.");

            foreach (float degrees in new[] { 60f, -75f })
            {
                Quaternion start = boss.transform.rotation;
                driver.BeginTurn(degrees);
                Advance(driver, 0.12f);
                Require(Quaternion.Angle(start, boss.transform.rotation) < 0.01f, "Turn moved during preparation.");
                Advance(driver, 0.5f);
                Require(driver.IsTurning && Quaternion.Angle(start, boss.transform.rotation) > 5f, "Airborne rotation missing.");
                Advance(driver, 0.5f);
                Quaternion expected = Quaternion.AngleAxis(degrees, Vector3.up) * start;
                Require(!driver.IsTurning && Quaternion.Angle(expected, boss.transform.rotation) < 0.05f, "Turn angle/landing incorrect.");
                Advance(driver, 0.3f);
                Require(!driver.IsBusy, "Turn recovery failed.");
            }

            int sustained = driver.BeginFiring(0.3f, true);
            Advance(driver, 0.5f);
            Require(driver.WasReleased(sustained), "Tracking-beam cue missing.");
            float held = animator.GetCurrentAnimatorStateInfo(1).normalizedTime;
            Advance(driver, 1f);
            Require(Mathf.Abs(held - animator.GetCurrentAnimatorStateInfo(1).normalizedTime) < 0.001f, "Sustained firing pose is not held.");
            driver.ReleaseSustainedFiring();
            Advance(driver, 1f);
            Require(!driver.IsBusy, "Sustained firing did not recover.");

            int cancelled = driver.BeginFiring();
            driver.CancelAction();
            Advance(driver, 1f);
            Require(!driver.WasReleased(cancelled), "Cancelled attack still released.");
            int paused = driver.BeginFiring();
            driver.SetCinematicPaused(true);
            Advance(driver, 1f);
            Require(!driver.WasReleased(paused), "Paused attack released.");
            driver.SetCinematicPaused(false);
            Advance(driver, 0.5f);
            Require(driver.WasReleased(paused), "Resume failed.");

            driver.BeginBeam(true, 0.8f, 1f);
            boss.ApplyDamage(boss.MaxHealth * 2f);
            Require(driver.IsDead && driver.BeginFiring() == -1, "Death did not block attacks.");
            Advance(driver, 5f);
            Require(animator.GetCurrentAnimatorStateInfo(0).IsName("Base Layer.Death") &&
                Mathf.Approximately(animator.GetLayerWeight(1), 0f), "Death was interrupted or overridden.");
            Transform pelvis = animator.transform.Find("Root/Pelvis");
            Vector3 finalPosition = pelvis.position;
            Advance(driver, 1f);
            Require(Vector3.Distance(finalPosition, pelvis.position) < 0.001f, "Death final pose not held.");
            Debug.Log("Kaiju combat runtime PASS: 5 aim angles/deduplicated firing, masked legs, Attack1/Attack2, 2 beams, tail, +/- turns, sustain, cancel, pause/resume, death hold.");
        }
        finally { UnityEngine.Object.DestroyImmediate(copy); }
    }

    private static void VerifyMaskedLegs(KaijuBossAnimationDriver driver)
    {
        Animator a = driver.Animator;
        driver.CancelAction();
        a.SetFloat("TargetAngle", -45f);
        a.Play("Base Layer.BasicIdle", 0, 0.37f);
        a.Play("UpperBody.AimIdle", 1, 0.37f);
        a.SetLayerWeight(1, 0f);
        a.Update(0f);
        Transform foot = a.transform.Find("Root/Pelvis/Thigh L/Calf L/Cannon L/Foot L");
        Transform spine = a.transform.Find("Root/Pelvis/Spine 01");
        Require(foot != null, "Missing foot path.");
        Quaternion footRotation = foot.rotation;
        Quaternion spineRotation = spine.rotation;
        a.SetLayerWeight(1, 1f);
        a.Update(0f);
        Require(Quaternion.Angle(footRotation, foot.rotation) < 0.05f, "Upper-body mask changes the legs.");
        Require(Quaternion.Angle(spineRotation, spine.rotation) > 0.1f, "Upper-body mask does not affect the spine.");
    }

    private static void Advance(KaijuBossAnimationDriver driver, float seconds)
    {
        MethodInfo lateUpdate = typeof(KaijuBossAnimationDriver).GetMethod("LateUpdate", BindingFlags.Instance | BindingFlags.NonPublic);
        int frames = Mathf.CeilToInt(seconds * 60f);
        for (int i = 0; i < frames; i++)
        {
            typeof(KaijuBossAnimationDriver).GetMethod("RestoreSweepPose", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(driver, null);
            driver.Animator.Update(1f / 60f);
            lateUpdate.Invoke(driver, null);
        }
    }

    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}
