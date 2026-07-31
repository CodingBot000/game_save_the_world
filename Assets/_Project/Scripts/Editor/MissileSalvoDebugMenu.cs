using UnityEditor;
using UnityEngine;

public static class MissileSalvoDebugMenu
{
    private const string MenuRoot = "TitanDestroyer/Debug/Missile Salvo/";

    [MenuItem(MenuRoot + "Fire 5", priority = 220)]
    private static void FireFive() => Fire(5);

    [MenuItem(MenuRoot + "Fire 10", priority = 221)]
    private static void FireTen() => Fire(10);

    [MenuItem(MenuRoot + "Fire 15", priority = 222)]
    private static void FireFifteen() => Fire(15);

    [MenuItem(MenuRoot + "Fire 20", priority = 223)]
    private static void FireTwenty() => Fire(20);

    [MenuItem(MenuRoot + "Fire 30", priority = 224)]
    private static void FireThirty() => Fire(30);

    [MenuItem(MenuRoot + "Reject 31", priority = 225)]
    private static void RejectThirtyOne() => Fire(31);

    [MenuItem(MenuRoot + "Verify Cleanup Guard", priority = 226)]
    private static void VerifyCleanupGuard()
    {
        if (!Application.isPlaying)
        {
            Debug.LogWarning("[SalvoDebug] Enter Play Mode before verifying the cleanup guard.");
            return;
        }

        CartoonSmokePuff.ClearAllRuntimeSmokeObjects();
        SpecialMissilePool pool = Object.FindAnyObjectByType<SpecialMissilePool>();
        Debug.Log($"[SalvoDebug] Cleanup guard verified. {GetPoolSnapshot(pool)}");
    }

    // Retain the stage-one menu path while implementation notes and smoke scripts migrate.
    [MenuItem("TitanDestroyer/Debug/Fire 30-Missile Salvo", priority = 227)]
    private static void FireLegacyThirtyMissileSalvo() => Fire(30);

    private static void Fire(int missileCount)
    {
        if (!Application.isPlaying)
        {
            Debug.LogWarning("[SalvoDebug] Enter Play Mode before firing a test salvo.");
            return;
        }

        PlayerSpecialAttackController adapter =
            Object.FindAnyObjectByType<PlayerSpecialAttackController>();
        if (adapter == null)
        {
            Debug.LogError("[SalvoDebug] PlayerSpecialAttackController was not found.");
            return;
        }

        bool started = adapter.TryActivateForDebug(missileCount);
        SpecialMissilePool pool = Object.FindAnyObjectByType<SpecialMissilePool>();
        string poolSnapshot = GetPoolSnapshot(pool);

        if (!started)
        {
            Debug.LogWarning(
                $"[SalvoDebug] {missileCount}-missile salvo rejected: {adapter.LastAttemptReason}. {poolSnapshot}");
            return;
        }

        Debug.Log(
            $"[SalvoDebug] {missileCount}-missile salvo started through the shared launcher API. {poolSnapshot}");
    }

    private static string GetPoolSnapshot(SpecialMissilePool pool)
    {
        return pool == null
            ? "Pool=missing"
            : $"Pool created={pool.CreatedMissiles} total={pool.TotalMissiles} available={pool.AvailableMissiles} reserved={pool.ReservedMissiles} leased={pool.LeasedMissiles} valid={pool.HasValidMissileCounts}";
    }
}
