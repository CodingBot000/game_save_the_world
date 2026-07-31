using UnityEditor;
using UnityEngine;

public static class MissileSalvoDebugMenu
{
    private const string MenuPath = "TitanDestroyer/Debug/Fire 30-Missile Salvo";

    [MenuItem(MenuPath, priority = 220)]
    private static void FireLegacyThirtyMissileSalvo()
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

        if (!adapter.TryActivate())
        {
            Debug.LogError($"[SalvoDebug] 30-missile salvo rejected: {adapter.GetUnavailableReason()}");
            return;
        }

        Debug.Log("[SalvoDebug] 30-missile salvo started through the shared launcher API.");
    }

    [MenuItem(MenuPath, validate = true)]
    private static bool ValidateFireLegacyThirtyMissileSalvo()
    {
        return Application.isPlaying;
    }
}
