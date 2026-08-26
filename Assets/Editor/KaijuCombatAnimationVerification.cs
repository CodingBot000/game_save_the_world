using System;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;

/// <summary>Deterministic Play Mode regression checks on an isolated copy, never the live boss.</summary>
public static class KaijuCombatAnimationVerification
{
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
            driver.Animator.Update(1f / 60f);
            lateUpdate.Invoke(driver, null);
        }
    }

    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}
