using System;
using System.Collections;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

public class PlayerLockOnFacingIntegrationTests
{
    private const BindingFlags InstanceFlags =
        BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
    private const string BattleScene = "Assets/Scenes/BattleArena.unity/BattleArena.unity";

    [UnityTearDown]
    public IEnumerator TearDown()
    {
        if (Application.isPlaying)
        {
            yield return new ExitPlayMode();
        }
    }

    [UnityTest]
    public IEnumerator BattleArena_AllLockStagesFaceFront_OnlyFiveLaunchSidewinders()
    {
        yield return new EnterPlayMode();
        // Create event-handler closures after the Play Mode domain reload.
        yield return VerifyLockStagesInBattleArena();
    }

    private static IEnumerator VerifyLockStagesInBattleArena()
    {
        AsyncOperation loading = EditorSceneManager.LoadSceneAsyncInPlayMode(
            BattleScene, new LoadSceneParameters(LoadSceneMode.Single));
        // The Edit Mode runner does not wait on runtime YieldInstructions.
        while (!loading.isDone)
        {
            yield return null;
        }

        Type lockOnType = ResolveType("PlayerLockOnController");
        double readyDeadline = UnityEditor.EditorApplication.timeSinceStartup + 30d;
        while (UnityEngine.Object.FindObjectsByType(lockOnType).Length == 0)
        {
            Assert.That(UnityEditor.EditorApplication.timeSinceStartup, Is.LessThan(readyDeadline),
                "BattleController.Start must initialize the lock-on runtime.");
            yield return null;
        }

        Component lockOn = FindComponent("PlayerLockOnController");
        Component orbit = FindComponent("PlayerOrbitController");
        Component launcher = FindComponent("PlayerMissileSalvoLauncher");
        Component boss = FindComponent("BossController");
        MonoBehaviour bossAttack = (MonoBehaviour)FindComponent("BossAttackController");
        bossAttack.enabled = false;
        bossAttack.StopAllCoroutines();
        Invoke(boss, "SetMaxHealthForDebug", 100000f, true);
        Component mounted = Read<Component>(lockOn, "MountedSidewinderCosmeticController");
        Assert.That(mounted, Is.Not.Null);
        Assert.That(Read<int>(mounted, "ResolvedMountedSidewinderCount"), Is.EqualTo(2));

        object input = Enum.Parse(ResolveType("LockOnInputSource"), "MobileHud");
        int commonStarts = 0;
        int fullStarts = 0;
        bool frontPoseActiveBeforeFirstWave = false;
        Action<int> onCommonStart = _ => commonStarts++;
        Action<int> onFullStart = _ => fullStarts++;
        Action<int, string> onLauncherStart = (_, source) =>
        {
            if (source == "LockOn")
            {
                frontPoseActiveBeforeFirstWave = Read<bool>(orbit, "IsFullSalvoFrontViewActive");
            }
        };
        Subscribe(lockOn, "OnLockOnSalvoStarting", onCommonStart);
        Subscribe(lockOn, "OnFullSalvoStarting", onFullStart);
        Subscribe(launcher, "SalvoStarted", onLauncherStart);

        // Releasing before the first successful lock must not turn or launch anything.
        Assert.That((bool)Invoke(lockOn, "TryBeginCharging", input), Is.True);
        Assert.That((bool)Invoke(lockOn, "TryReleaseCharging", input), Is.False);
        Assert.That(commonStarts, Is.Zero);
        Assert.That(fullStarts, Is.Zero);
        Assert.That(Read<bool>(orbit, "IsFullSalvoFrontViewActive"), Is.False);

        int[] expectedMissiles = { 5, 10, 15, 20, 30 };
        float[] expectedDamage = { 9f, 20f, 35f, 60f, 100f };
        for (int stage = 1; stage <= 5; stage++)
        {
            // Debug charging skips real charge time; let the previous missiles
            // return to the pool before requesting the next complete volley.
            yield return WaitForMissilePool(launcher);
            Invoke(lockOn, "AdvanceReuseWaitForDebug", 10f);
            Assert.That((bool)Invoke(lockOn, "TryBeginCharging", input), Is.True, $"stage {stage}");
            float threshold = (float)Invoke(lockOn, "GetCumulativeChargeTimeForStage", stage);
            Invoke(lockOn, "AdvanceChargeForDebug", threshold + 0.01f);
            Assert.That(Read<int>(lockOn, "SuccessfulLockCount"), Is.EqualTo(stage));

            Quaternion originalPose = Read<Quaternion>(orbit, "DebugCurrentVisualDisplayRotation");
            Quaternion frontPose = (Quaternion)Invoke(orbit, "ResolveCameraFacingDisplayRotation");
            frontPoseActiveBeforeFirstWave = false;
            bool released = (bool)Invoke(lockOn, "TryReleaseCharging", input);
            Assert.That(released, Is.True, $"stage {stage}: {Read<string>(lockOn, "LastSalvoFailureReason")}");
            Assert.That(commonStarts, Is.EqualTo(stage));
            Assert.That(fullStarts, Is.EqualTo(stage == 5 ? 1 : 0));
            Assert.That(frontPoseActiveBeforeFirstWave, Is.True, $"stage {stage}: turn before launch");
            Assert.That(Read<bool>(orbit, "IsFullSalvoFrontViewActive"), Is.True);
            Assert.That(Read<int>(lockOn, "LastRequestedMissileCount"), Is.EqualTo(expectedMissiles[stage - 1]));
            Assert.That(Read<float>(lockOn, "LastBaseDamageBudget"), Is.EqualTo(expectedDamage[stage - 1]));

            yield return WaitForGameSeconds(0.4f);
            Assert.That(Read<float>(orbit, "FullSalvoVisualTurnProgress"), Is.EqualTo(1f).Within(0.001f));
            Assert.That(Quaternion.Angle(
                Read<Quaternion>(orbit, "DebugCurrentVisualDisplayRotation"), frontPose), Is.LessThan(0.1f));
            if (stage < 5)
            {
                Assert.That(Read<bool>(mounted, "IsWaitingForVisualTurn"), Is.False);
                Assert.That(Read<bool>(mounted, "IsIgniting"), Is.False);
                Assert.That(Read<int>(mounted, "ActiveExhaustCount"), Is.Zero);
                Assert.That(Read<int>(mounted, "LastDetachedSidewinderCount"), Is.Zero);
            }

            yield return WaitForGameSeconds(2.8f);
            Assert.That(Read<int>(lockOn, "LastFiredMissileCount"), Is.EqualTo(expectedMissiles[stage - 1]));
            Assert.That(Read<bool>(orbit, "IsFullSalvoFrontViewActive"), Is.False);
            Assert.That(Read<bool>(orbit, "IsFullSalvoVisualReturning"), Is.False);
            Assert.That(Quaternion.Angle(
                Read<Quaternion>(orbit, "DebugCurrentVisualDisplayRotation"), originalPose), Is.LessThan(0.1f));
            Assert.That(Read<int>(mounted, "LastDetachedSidewinderCount"), Is.EqualTo(stage == 5 ? 2 : 0));
            if (stage == 5)
            {
                Assert.That(Read<bool>(mounted, "LastIgnitionStartedAfterVisualTurn"), Is.True);
            }
            Debug.Log($"[LockOnFacingTest] Stage {stage}: front pose, missile budget, Sidewinders and pose restoration passed.");
        }

        // Cancel a partial salvo after the turn begins but before its first missile.
        // Both the facing override and its completion notification must be cleaned up.
        yield return WaitForMissilePool(launcher);
        Invoke(lockOn, "AdvanceReuseWaitForDebug", 10f);
        Assert.That((bool)Invoke(lockOn, "TryBeginCharging", input), Is.True);
        Invoke(lockOn, "AdvanceChargeForDebug", 1.01f);
        int canceledFinishes = 0;
        Action<int, bool> onFinished = (_, canceled) => { if (canceled) canceledFinishes++; };
        Action<int, string> cancelBeforeLaunch = (_, source) =>
        {
            if (source == "LockOn") ((Behaviour)launcher).enabled = false;
        };
        Subscribe(lockOn, "OnLockOnSalvoFinished", onFinished);
        Subscribe(launcher, "SalvoStarted", cancelBeforeLaunch);
        Assert.That((bool)Invoke(lockOn, "TryReleaseCharging", input), Is.False);
        Assert.That(canceledFinishes, Is.EqualTo(1));
        Assert.That(Read<int>(lockOn, "LastFiredMissileCount"), Is.Zero);
        Assert.That(Read<bool>(orbit, "IsFullSalvoFrontViewActive"), Is.False);
        Assert.That(Read<bool>(orbit, "IsFullSalvoVisualTurning"), Is.False);
        Assert.That(fullStarts, Is.EqualTo(1));
    }

    private static IEnumerator WaitForMissilePool(Component launcher)
    {
        double deadline = UnityEditor.EditorApplication.timeSinceStartup + 60d;
        while (Read<int>(launcher, "PoolLeasedMissiles") > 0)
        {
            Assert.That(UnityEditor.EditorApplication.timeSinceStartup, Is.LessThan(deadline),
                "Previous missiles must return to the pool before testing another salvo.");
            yield return null;
        }
    }

    private static IEnumerator WaitForGameSeconds(float duration)
    {
        float until = Time.time + duration;
        double deadline = UnityEditor.EditorApplication.timeSinceStartup + 30d;
        while (Time.time < until)
        {
            Assert.That(UnityEditor.EditorApplication.timeSinceStartup, Is.LessThan(deadline),
                "BattleArena runtime must continue advancing during the integration test.");
            yield return null;
        }
    }

    private static Type ResolveType(string name)
    {
        Type type = Type.GetType(name + ", Assembly-CSharp");
        Assert.That(type, Is.Not.Null, name);
        return type;
    }

    private static Component FindComponent(string name)
    {
        Component component = UnityEngine.Object.FindObjectsByType(
            ResolveType(name)).OfType<Component>().FirstOrDefault();
        Assert.That(component, Is.Not.Null, name);
        return component;
    }

    private static object Invoke(Component component, string name, params object[] arguments)
    {
        MethodInfo method = component.GetType().GetMethod(name, InstanceFlags);
        Assert.That(method, Is.Not.Null, name);
        return method.Invoke(component, arguments);
    }

    private static T Read<T>(Component component, string name)
    {
        PropertyInfo property = component.GetType().GetProperty(name, InstanceFlags);
        Assert.That(property, Is.Not.Null, name);
        return (T)property.GetValue(component);
    }

    private static void Subscribe(Component component, string name, Delegate callback)
    {
        EventInfo eventInfo = component.GetType().GetEvent(name);
        Assert.That(eventInfo, Is.Not.Null, name);
        eventInfo.AddEventHandler(component, callback);
    }
}
