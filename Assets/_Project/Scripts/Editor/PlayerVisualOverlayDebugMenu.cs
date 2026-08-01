using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;

public static class PlayerVisualOverlayDebugMenu
{
    private const string MenuRoot = "TitanDestroyer/Debug/Player Visual Overlay/";
    private const double EdgePhaseSeconds = 3.75;
    public const string ScreenshotPath = "/tmp/TitanDestroyer_PlayerVisualOverlay.png";
    private static readonly Key[] EdgeKeys = { Key.A, Key.D, Key.W, Key.S };
    private static readonly string[] EdgeLabels = { "left", "right", "top", "bottom" };
    private static EditorApplication.CallbackFunction pendingEdgeRun;
    private static EdgeRunContext edgeRun;

    [MenuItem(MenuRoot + "Log And Verify State", priority = 320)]
    private static void LogAndVerifyState()
    {
        PlayerOrbitController orbit = Object.FindAnyObjectByType<PlayerOrbitController>();
        if (orbit == null)
        {
            Debug.LogWarning("[PlayerVisualOverlayDebug] PlayerOrbitController was not found.");
            return;
        }

        PlayerVisualOverlayRenderer overlay = orbit.OriginalVisualOverlayRenderer;
        Camera baseCamera = overlay != null ? overlay.BaseCamera : null;
        Camera overlayCamera = overlay != null ? overlay.OverlayCamera : null;
        Transform visualRoot = overlay != null ? overlay.VisualRoot : null;
        PlayerCombatController combat = orbit.GetComponent<PlayerCombatController>();
        int legacyObjectCount = CountLegacyScreenVisualObjects();
        Rect visualRect = default;
        bool hasVisualRect = overlay != null && overlay.TryGetVisualViewportRect(out visualRect);
        Vector3 playerViewport = baseCamera != null
            ? baseCamera.WorldToViewportPoint(orbit.transform.position)
            : Vector3.zero;
        Vector3 visualCenterViewport = hasVisualRect
            ? new Vector3(visualRect.center.x, visualRect.center.y, playerViewport.z)
            : Vector3.zero;
        Vector3 leftLauncherViewport = baseCamera != null && combat != null && combat.MissileLauncherLeft != null
            ? baseCamera.WorldToViewportPoint(combat.MissileLauncherLeft.position)
            : Vector3.zero;
        Vector3 rightLauncherViewport = baseCamera != null && combat != null && combat.MissileLauncherRight != null
            ? baseCamera.WorldToViewportPoint(combat.MissileLauncherRight.position)
            : Vector3.zero;

        bool cameraPoseMatches = baseCamera != null && overlayCamera != null &&
                                 Vector3.Distance(baseCamera.transform.position, overlayCamera.transform.position) < 0.0001f &&
                                 Quaternion.Angle(baseCamera.transform.rotation, overlayCamera.transform.rotation) < 0.001f &&
                                 MatrixApproximately(baseCamera.projectionMatrix, overlayCamera.projectionMatrix);
        bool visualInsideViewport = hasVisualRect &&
                                    visualRect.xMin >= -0.001f && visualRect.yMin >= -0.001f &&
                                    visualRect.xMax <= 1.001f && visualRect.yMax <= 1.001f;
        bool visualCenteredOnPlayer = hasVisualRect &&
                                      Vector2.Distance(
                                          new Vector2(playerViewport.x, playerViewport.y),
                                          visualRect.center) <= 0.01f;
        Rect movementRect = orbit.DebugMovementViewportRect;
        bool movementCoversFullGameplayViewport =
            Mathf.Abs(movementRect.xMin) <= 0.001f &&
            Mathf.Abs(movementRect.yMin) <= 0.001f &&
            Mathf.Abs(movementRect.xMax - 1f) <= 0.001f &&
            Mathf.Abs(movementRect.yMax - 1f) <= 0.001f;
        bool verified = orbit.IsUsingOriginalVisualOverlay && overlay != null && overlay.IsConfigured &&
                        overlay.BaseCameraExcludesVisualLayer && overlay.OverlayClearsDepth &&
                        overlay.BaseRendererSupportsCameraStacking &&
                        overlay.OverlayRendererSupportsCameraStacking &&
                        overlay.IsInBaseCameraStack && cameraPoseMatches &&
                        overlay.VisualRendererCount > 0 &&
                        overlay.RendererColliderLayerConflictCount == 0 &&
                        legacyObjectCount == 0 && visualInsideViewport && visualCenteredOnPlayer &&
                        orbit.IsMovementViewportInitializedForDisplay &&
                        movementCoversFullGameplayViewport;

        StringBuilder summary = new();
        summary.Append("[PlayerVisualOverlayDebug] verified=").Append(verified)
            .Append(", configured=").Append(overlay != null && overlay.IsConfigured)
            .Append(", visualRoot=").Append(visualRoot != null ? visualRoot.name : "null")
            .Append(", renderers=").Append(overlay != null ? overlay.VisualRendererCount : 0)
            .Append(", rendererColliderConflicts=").Append(overlay != null ? overlay.RendererColliderLayerConflictCount : -1)
            .Append(", baseExcludesLayer=").Append(overlay != null && overlay.BaseCameraExcludesVisualLayer)
            .Append(", baseSupportsStack=").Append(overlay != null && overlay.BaseRendererSupportsCameraStacking)
            .Append(", overlaySupportsStack=").Append(overlay != null && overlay.OverlayRendererSupportsCameraStacking)
            .Append(", overlayClearsDepth=").Append(overlay != null && overlay.OverlayClearsDepth)
            .Append(", inStack=").Append(overlay != null && overlay.IsInBaseCameraStack)
            .Append(", cameraPoseMatches=").Append(cameraPoseMatches)
            .Append(", visualCenteredOnPlayer=").Append(visualCenteredOnPlayer)
            .Append(", legacyObjects=").Append(legacyObjectCount)
            .Append(", playerViewport=").Append(FormatVector(playerViewport))
            .Append(", visualCenterViewport=").Append(FormatVector(visualCenterViewport))
            .Append(", visualRect=").Append(hasVisualRect ? FormatRect(visualRect) : "unavailable")
            .Append(", movementInitialized=").Append(orbit.IsMovementViewportInitializedForDisplay)
            .Append(", gamePixels=").Append(orbit.InitializedGameplayPixelSize)
            .Append(", gameAspect=").Append(orbit.InitializedGameplayAspect.ToString("F3"))
            .Append(", fullGameplayViewport=").Append(movementCoversFullGameplayViewport)
            .Append(", movementRect=").Append(FormatRect(movementRect))
            .Append(", leftLauncherViewport=").Append(FormatVector(leftLauncherViewport))
            .Append(", rightLauncherViewport=").Append(FormatVector(rightLauncherViewport));
        Debug.Log(summary.ToString(), orbit);
    }

    [MenuItem(MenuRoot + "Capture Game Screenshot", priority = 321)]
    private static void CaptureGameScreenshot()
    {
        if (!Application.isPlaying)
        {
            Debug.LogWarning("[PlayerVisualOverlayDebug] Enter Play Mode before capturing the Game view.");
            return;
        }

        ScreenCapture.CaptureScreenshot(ScreenshotPath, 1);
        Debug.Log($"[PlayerVisualOverlayDebug] Screenshot requested: {ScreenshotPath}");
    }

    [MenuItem(MenuRoot + "Run Screen Edge Bounds", priority = 322)]
    private static void RunScreenEdgeBounds()
    {
        AbortEdgeRun(logResult: false);
        PlayerOrbitController orbit = Object.FindAnyObjectByType<PlayerOrbitController>();
        PlayerVisualOverlayRenderer overlay = orbit != null ? orbit.OriginalVisualOverlayRenderer : null;
        Keyboard keyboard = Keyboard.current;
        if (!Application.isPlaying || orbit == null || overlay == null || !overlay.IsConfigured || keyboard == null)
        {
            Debug.LogWarning(
                $"[PlayerVisualOverlayDebug] Edge run unavailable. playing={Application.isPlaying}, " +
                $"orbit={orbit != null}, overlay={overlay != null && overlay.IsConfigured}, keyboard={keyboard != null}.");
            return;
        }

        edgeRun = new EdgeRunContext
        {
            Orbit = orbit,
            Overlay = overlay,
            Keyboard = keyboard,
            Phase = 0,
            PhaseStartedAt = EditorApplication.timeSinceStartup,
            StayedInsideScreen = true,
            PreviousUndead = GameplayDebugFlags.Undead,
        };
        GameplayDebugFlags.Undead = true;
        QueueEdgeKey(edgeRun);
        pendingEdgeRun = TickEdgeRun;
        EditorApplication.update += pendingEdgeRun;
        Debug.Log("[PlayerVisualOverlayDebug] Started left/right/top/bottom screen-edge run.");
    }

    private static void TickEdgeRun()
    {
        if (edgeRun == null || !Application.isPlaying || edgeRun.Orbit == null || edgeRun.Overlay == null)
        {
            AbortEdgeRun(logResult: true);
            return;
        }

        if (edgeRun.Overlay.TryGetVisualViewportRect(out Rect visualRect))
        {
            edgeRun.StayedInsideScreen &=
                visualRect.xMin >= -0.002f && visualRect.yMin >= -0.002f &&
                visualRect.xMax <= 1.002f && visualRect.yMax <= 1.002f;
        }
        else
        {
            edgeRun.StayedInsideScreen = false;
        }

        if (EditorApplication.timeSinceStartup - edgeRun.PhaseStartedAt < EdgePhaseSeconds)
        {
            return;
        }

        Camera camera = edgeRun.Overlay.BaseCamera;
        Vector3 playerViewport = camera != null
            ? camera.WorldToViewportPoint(edgeRun.Orbit.transform.position)
            : Vector3.zero;
        Rect movementRect = edgeRun.Orbit.DebugMovementViewportRect;
        bool reachedEdge = edgeRun.Phase switch
        {
            0 => playerViewport.x <= movementRect.xMin + 0.015f,
            1 => playerViewport.x >= movementRect.xMax - 0.015f,
            2 => playerViewport.y >= movementRect.yMax - 0.015f,
            3 => playerViewport.y <= movementRect.yMin + 0.015f,
            _ => false,
        };
        edgeRun.ReachedEdges[edgeRun.Phase] = reachedEdge;
        edgeRun.EdgeViewports[edgeRun.Phase] = playerViewport;
        edgeRun.Phase++;
        if (edgeRun.Phase >= EdgeKeys.Length)
        {
            AbortEdgeRun(logResult: true);
            return;
        }

        edgeRun.PhaseStartedAt = EditorApplication.timeSinceStartup;
        QueueEdgeKey(edgeRun);
    }

    private static void QueueEdgeKey(EdgeRunContext context)
    {
        InputSystem.QueueStateEvent(context.Keyboard, new KeyboardState(EdgeKeys[context.Phase]));
    }

    private static void AbortEdgeRun(bool logResult)
    {
        if (pendingEdgeRun != null)
        {
            EditorApplication.update -= pendingEdgeRun;
            pendingEdgeRun = null;
        }

        if (edgeRun == null)
        {
            return;
        }

        if (edgeRun.Keyboard != null)
        {
            InputSystem.QueueStateEvent(edgeRun.Keyboard, new KeyboardState());
        }

        GameplayDebugFlags.Undead = edgeRun.PreviousUndead;
        if (logResult)
        {
            bool reachedAllEdges = true;
            StringBuilder details = new();
            for (int i = 0; i < EdgeLabels.Length; i++)
            {
                reachedAllEdges &= edgeRun.ReachedEdges[i];
                if (i > 0)
                {
                    details.Append(", ");
                }

                details.Append(EdgeLabels[i]).Append('=')
                    .Append(edgeRun.ReachedEdges[i]).Append('@')
                    .Append(FormatVector(edgeRun.EdgeViewports[i]));
            }

            Rect movementRect = edgeRun.Orbit.DebugMovementViewportRect;
            bool fullGameplayViewport =
                Mathf.Abs(movementRect.xMin) <= 0.001f &&
                Mathf.Abs(movementRect.yMin) <= 0.001f &&
                Mathf.Abs(movementRect.xMax - 1f) <= 0.001f &&
                Mathf.Abs(movementRect.yMax - 1f) <= 0.001f;
            Debug.Log(
                $"[PlayerVisualOverlayDebug] edgeRun verified={reachedAllEdges && fullGameplayViewport}, " +
                $"fullGameplayViewport={fullGameplayViewport}, " +
                $"visualStayedInsideScreen={edgeRun.StayedInsideScreen}, {details}");
        }

        edgeRun = null;
    }

    private static int CountLegacyScreenVisualObjects()
    {
        int count = 0;
        GameObject[] objects = Resources.FindObjectsOfTypeAll<GameObject>();
        for (int i = 0; i < objects.Length; i++)
        {
            GameObject target = objects[i];
            if (target == null)
            {
                continue;
            }

            string objectName = target.name;
            if (objectName == "PlayerScreenSpaceVisualRenderCamera" ||
                objectName == "PlayerScreenSpaceVisualRoot" ||
                objectName == "PlayerScreenSpaceVisualCanvas" ||
                objectName == "PlayerScreenSpaceVisualImage" ||
                objectName.EndsWith("_ScreenVisual"))
            {
                count++;
            }
        }

        return count;
    }

    private static bool MatrixApproximately(Matrix4x4 left, Matrix4x4 right)
    {
        for (int i = 0; i < 16; i++)
        {
            if (Mathf.Abs(left[i] - right[i]) > 0.0001f)
            {
                return false;
            }
        }

        return true;
    }

    private static string FormatVector(Vector3 value) =>
        $"({value.x:F3},{value.y:F3},{value.z:F3})";

    private static string FormatRect(Rect value) =>
        $"({value.xMin:F3},{value.yMin:F3})-({value.xMax:F3},{value.yMax:F3})";

    private sealed class EdgeRunContext
    {
        public PlayerOrbitController Orbit;
        public PlayerVisualOverlayRenderer Overlay;
        public Keyboard Keyboard;
        public int Phase;
        public double PhaseStartedAt;
        public bool StayedInsideScreen;
        public bool PreviousUndead;
        public readonly bool[] ReachedEdges = new bool[4];
        public readonly Vector3[] EdgeViewports = new Vector3[4];
    }
}
