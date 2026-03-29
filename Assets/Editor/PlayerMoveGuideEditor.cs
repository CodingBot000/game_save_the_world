using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(PlayerMoveGuide))]
public class PlayerMoveGuideEditor : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();

        PlayerMoveGuide guide = (PlayerMoveGuide)target;
        if (guide.TargetCamera == null)
        {
            EditorGUILayout.HelpBox("Place PlayerMoveGuide under a Camera to preview and edit movement bounds.", MessageType.Info);
        }
        else
        {
            EditorGUILayout.HelpBox("Drag the corner handles in Scene view to resize the rectangle. Drag the arrow handles to adjust near, preview, and far depth.", MessageType.None);
        }
    }

    private void OnSceneGUI()
    {
        PlayerMoveGuide guide = (PlayerMoveGuide)target;
        if (!TryDrawGuideOverlay(guide, drawHandles: true))
        {
            return;
        }
    }

    internal static bool TryDrawGuideOverlay(PlayerMoveGuide guide, bool drawHandles)
    {
        Camera targetCamera = guide.TargetCamera;
        if (targetCamera == null)
        {
            return false;
        }

        Rect viewportRect = guide.ViewportRect;
        float minimumDepth = guide.MinimumDepth;
        float maximumDepth = guide.MaximumDepth;
        float previewDepth = guide.PreviewDepth;

        Vector3[] nearCorners = GetWorldCorners(targetCamera, viewportRect, minimumDepth);
        Vector3[] farCorners = GetWorldCorners(targetCamera, viewportRect, maximumDepth);
        Vector3[] previewCorners = GetWorldCorners(targetCamera, viewportRect, previewDepth);

        DrawRectLoop(nearCorners, new Color(guide.GuideColor.r, guide.GuideColor.g, guide.GuideColor.b, 0.9f), 3f);
        DrawRectLoop(farCorners, new Color(guide.GuideColor.r, guide.GuideColor.g, guide.GuideColor.b, 0.35f), 2f);
        DrawRectLoop(previewCorners, guide.GuideColor, 4f);

        Handles.color = new Color(guide.GuideColor.r, guide.GuideColor.g, guide.GuideColor.b, 0.35f);
        for (int i = 0; i < 4; i++)
        {
            Handles.DrawLine(nearCorners[i], farCorners[i]);
        }

        Vector3 centerPoint = targetCamera.ViewportToWorldPoint(new Vector3(viewportRect.center.x, viewportRect.center.y, previewDepth));
        Handles.color = guide.GuideColor;
        Handles.Label(centerPoint, "PlayerMoveGuide");

        if (drawHandles)
        {
            DrawCornerHandles(guide, targetCamera, previewCorners, previewDepth);
            DrawDepthHandles(guide, targetCamera, viewportRect, minimumDepth, previewDepth, maximumDepth);
        }

        return true;
    }

    private static void DrawCornerHandles(PlayerMoveGuide guide, Camera targetCamera, Vector3[] previewCorners, float previewDepth)
    {
        Vector3[] movedCorners = new Vector3[previewCorners.Length];
        bool changed = false;

        for (int i = 0; i < previewCorners.Length; i++)
        {
            float handleSize = HandleUtility.GetHandleSize(previewCorners[i]) * 0.08f;
            EditorGUI.BeginChangeCheck();
            Vector3 moved = Handles.FreeMoveHandle(
                previewCorners[i],
                handleSize,
                Vector3.zero,
                Handles.DotHandleCap);

            if (EditorGUI.EndChangeCheck())
            {
                movedCorners[i] = moved;
                changed = true;
            }
            else
            {
                movedCorners[i] = previewCorners[i];
            }
        }

        if (!changed)
        {
            return;
        }

        Undo.RecordObject(guide, "Adjust Player Move Guide Rect");

        float minViewportX = float.PositiveInfinity;
        float maxViewportX = float.NegativeInfinity;
        float minViewportY = float.PositiveInfinity;
        float maxViewportY = float.NegativeInfinity;

        for (int i = 0; i < movedCorners.Length; i++)
        {
            Vector3 viewport = targetCamera.WorldToViewportPoint(movedCorners[i]);
            minViewportX = Mathf.Min(minViewportX, viewport.x);
            maxViewportX = Mathf.Max(maxViewportX, viewport.x);
            minViewportY = Mathf.Min(minViewportY, viewport.y);
            maxViewportY = Mathf.Max(maxViewportY, viewport.y);
        }

        guide.SetViewportRect(Rect.MinMaxRect(minViewportX, minViewportY, maxViewportX, maxViewportY));
        guide.SetPreviewDepth(previewDepth);
        EditorUtility.SetDirty(guide);
    }

    private static void DrawDepthHandles(PlayerMoveGuide guide, Camera targetCamera, Rect viewportRect, float minimumDepth, float previewDepth, float maximumDepth)
    {
        Vector3 direction = targetCamera.transform.forward;
        float centerX = viewportRect.center.x;
        float centerY = viewportRect.center.y;

        Vector3 nearPoint = targetCamera.ViewportToWorldPoint(new Vector3(centerX, centerY, minimumDepth));
        Vector3 previewPoint = targetCamera.ViewportToWorldPoint(new Vector3(centerX, centerY, previewDepth));
        Vector3 farPoint = targetCamera.ViewportToWorldPoint(new Vector3(centerX, centerY, maximumDepth));

        float nearSize = HandleUtility.GetHandleSize(nearPoint) * 0.18f;
        float previewSize = HandleUtility.GetHandleSize(previewPoint) * 0.18f;
        float farSize = HandleUtility.GetHandleSize(farPoint) * 0.18f;

        Handles.color = new Color(1f, 0.9f, 0.2f, 0.95f);
        Handles.Label(previewPoint, "Preview Depth");
        Handles.Label(nearPoint, "Min Depth");
        Handles.Label(farPoint, "Max Depth");

        EditorGUI.BeginChangeCheck();
        Vector3 movedNear = Handles.Slider(nearPoint, direction, nearSize, Handles.ArrowHandleCap, 0f);
        Vector3 movedPreview = Handles.Slider(previewPoint, direction, previewSize, Handles.ArrowHandleCap, 0f);
        Vector3 movedFar = Handles.Slider(farPoint, direction, farSize, Handles.ArrowHandleCap, 0f);

        if (!EditorGUI.EndChangeCheck())
        {
            return;
        }

        float newMinDepth = Vector3.Dot(movedNear - targetCamera.transform.position, direction);
        float newPreviewDepth = Vector3.Dot(movedPreview - targetCamera.transform.position, direction);
        float newMaxDepth = Vector3.Dot(movedFar - targetCamera.transform.position, direction);

        Undo.RecordObject(guide, "Adjust Player Move Guide Depth");
        guide.SetDepthRange(newMinDepth, newMaxDepth);
        guide.SetPreviewDepth(newPreviewDepth);
        EditorUtility.SetDirty(guide);
    }

    private static void DrawRectLoop(Vector3[] corners, Color color, float thickness)
    {
        Handles.color = color;
        for (int i = 0; i < corners.Length; i++)
        {
            Handles.DrawAAPolyLine(thickness, corners[i], corners[(i + 1) % corners.Length]);
        }
    }

    private static Vector3[] GetWorldCorners(Camera targetCamera, Rect viewportRect, float depth)
    {
        return new[]
        {
            targetCamera.ViewportToWorldPoint(new Vector3(viewportRect.xMin, viewportRect.yMin, depth)),
            targetCamera.ViewportToWorldPoint(new Vector3(viewportRect.xMin, viewportRect.yMax, depth)),
            targetCamera.ViewportToWorldPoint(new Vector3(viewportRect.xMax, viewportRect.yMax, depth)),
            targetCamera.ViewportToWorldPoint(new Vector3(viewportRect.xMax, viewportRect.yMin, depth))
        };
    }
}

[InitializeOnLoad]
public static class PlayerMoveGuideSceneOverlay
{
    static PlayerMoveGuideSceneOverlay()
    {
        SceneView.duringSceneGui += DrawAllGuides;
    }

    private static void DrawAllGuides(SceneView sceneView)
    {
        PlayerMoveGuide[] guides = Object.FindObjectsByType<PlayerMoveGuide>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        for (int i = 0; i < guides.Length; i++)
        {
            PlayerMoveGuide guide = guides[i];
            if (guide == null || EditorUtility.IsPersistent(guide))
            {
                continue;
            }

            if (Selection.activeGameObject == guide.gameObject)
            {
                continue;
            }

            PlayerMoveGuideEditor.TryDrawGuideOverlay(guide, drawHandles: false);
        }
    }
}
