using UnityEngine;
using UnityEngine.Rendering;

[ExecuteAlways]
public class PlayerMoveGuide : MonoBehaviour
{
    private const string VisualRootName = "_GuideVisuals";

    [SerializeField] private float minViewportX = 0.12f;
    [SerializeField] private float maxViewportX = 0.88f;
    [SerializeField] private float minViewportY = 0.16f;
    [SerializeField] private float maxViewportY = 0.84f;
    [SerializeField] private float minCameraDepth = 6f;
    [SerializeField] private float maxCameraDepth = 18f;
    [SerializeField] private float previewDepth = 12f;
    [SerializeField] private Color guideColor = new Color(0.12f, 1f, 0.38f, 0.95f);
    [SerializeField] private bool showInGameView = true;
    [SerializeField] private bool showDepthVolumeInGame = true;
    [SerializeField] private float previewLineWidth = 0.045f;
    [SerializeField] private float depthLineWidth = 0.02f;

    private static Material sharedLineMaterial;

    private Transform visualRoot;
    private LineRenderer previewRenderer;
    private LineRenderer nearRenderer;
    private LineRenderer farRenderer;
    private LineRenderer[] depthRenderers;

    public Camera TargetCamera => GetComponentInParent<Camera>();
    public Rect ViewportRect => Rect.MinMaxRect(
        Mathf.Min(minViewportX, maxViewportX),
        Mathf.Min(minViewportY, maxViewportY),
        Mathf.Max(minViewportX, maxViewportX),
        Mathf.Max(minViewportY, maxViewportY));
    public float MinimumDepth => Mathf.Min(minCameraDepth, maxCameraDepth);
    public float MaximumDepth => Mathf.Max(minCameraDepth, maxCameraDepth);
    public float PreviewDepth => Mathf.Clamp(previewDepth, MinimumDepth, MaximumDepth);
    public Color GuideColor => guideColor;
    public bool ShowInGameView => showInGameView;

    public void GetMovementBounds(out Rect viewportRect, out float minimumDepth, out float maximumDepth)
    {
        viewportRect = ViewportRect;
        minimumDepth = MinimumDepth;
        maximumDepth = MaximumDepth;
    }

    public void SetViewportRect(Rect rect)
    {
        minViewportX = rect.xMin;
        maxViewportX = rect.xMax;
        minViewportY = rect.yMin;
        maxViewportY = rect.yMax;
        ClampValues();
    }

    public void SetDepthRange(float minimumDepth, float maximumDepth)
    {
        minCameraDepth = minimumDepth;
        maxCameraDepth = maximumDepth;
        ClampValues();
    }

    public void SetPreviewDepth(float depth)
    {
        previewDepth = depth;
        ClampValues();
    }

    private void OnDrawGizmos()
    {
        Camera targetCamera = TargetCamera;
        if (targetCamera == null)
        {
            return;
        }

        GetMovementBounds(out Rect viewportRect, out float minimumDepth, out float maximumDepth);
        float drawDepth = PreviewDepth;

        Color nearColor = guideColor;
        nearColor.a = 0.9f;
        Color farColor = guideColor;
        farColor.a = 0.35f;

        Vector3[] nearCorners = GetWorldCorners(targetCamera, viewportRect, minimumDepth);
        Vector3[] farCorners = GetWorldCorners(targetCamera, viewportRect, maximumDepth);
        Vector3[] previewCorners = GetWorldCorners(targetCamera, viewportRect, drawDepth);

        DrawLoop(nearCorners, nearColor);
        DrawLoop(farCorners, farColor);
        DrawLoop(previewCorners, guideColor);

        Gizmos.color = farColor;
        for (int i = 0; i < nearCorners.Length; i++)
        {
            Gizmos.DrawLine(nearCorners[i], farCorners[i]);
        }
    }

    private void OnEnable()
    {
        EnsureRuntimeVisuals();
        UpdateRuntimeVisuals();
    }

    private void LateUpdate()
    {
        UpdateRuntimeVisuals();
    }

    private void OnDisable()
    {
        SetRuntimeVisualsEnabled(false);
    }

    private static void DrawLoop(Vector3[] corners, Color color)
    {
        Gizmos.color = color;
        for (int i = 0; i < corners.Length; i++)
        {
            Gizmos.DrawLine(corners[i], corners[(i + 1) % corners.Length]);
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

    private void OnValidate()
    {
        ClampValues();
        EnsureRuntimeVisuals();
        UpdateRuntimeVisuals();
    }

    private void ClampValues()
    {
        minViewportX = Mathf.Clamp01(minViewportX);
        maxViewportX = Mathf.Clamp01(maxViewportX);
        minViewportY = Mathf.Clamp01(minViewportY);
        maxViewportY = Mathf.Clamp01(maxViewportY);

        if (maxViewportX < minViewportX)
        {
            (minViewportX, maxViewportX) = (maxViewportX, minViewportX);
        }

        if (maxViewportY < minViewportY)
        {
            (minViewportY, maxViewportY) = (maxViewportY, minViewportY);
        }

        minCameraDepth = Mathf.Max(0.01f, minCameraDepth);
        maxCameraDepth = Mathf.Max(0.01f, maxCameraDepth);
        if (maxCameraDepth < minCameraDepth)
        {
            (minCameraDepth, maxCameraDepth) = (maxCameraDepth, minCameraDepth);
        }

        previewDepth = Mathf.Clamp(previewDepth, minCameraDepth, maxCameraDepth);
    }

    private void EnsureRuntimeVisuals()
    {
        if (visualRoot == null)
        {
            Transform existing = transform.Find(VisualRootName);
            if (existing != null)
            {
                visualRoot = existing;
            }
            else
            {
                GameObject root = new GameObject(VisualRootName);
                root.hideFlags = HideFlags.HideInHierarchy | HideFlags.DontSaveInEditor | HideFlags.DontSaveInBuild;
                visualRoot = root.transform;
                visualRoot.SetParent(transform, false);
            }
        }

        previewRenderer ??= CreateLineRenderer("PreviewRect", true, guideColor, previewLineWidth);
        nearRenderer ??= CreateLineRenderer("NearRect", true, new Color(guideColor.r, guideColor.g, guideColor.b, 0.8f), depthLineWidth);
        farRenderer ??= CreateLineRenderer("FarRect", true, new Color(guideColor.r, guideColor.g, guideColor.b, 0.35f), depthLineWidth);

        if (depthRenderers == null || depthRenderers.Length != 4)
        {
            depthRenderers = new LineRenderer[4];
        }

        for (int i = 0; i < depthRenderers.Length; i++)
        {
            depthRenderers[i] ??= CreateLineRenderer($"DepthRail_{i}", false, new Color(guideColor.r, guideColor.g, guideColor.b, 0.3f), depthLineWidth);
        }
    }

    private void UpdateRuntimeVisuals()
    {
        if (!showInGameView)
        {
            SetRuntimeVisualsEnabled(false);
            return;
        }

        Camera targetCamera = TargetCamera;
        if (targetCamera == null)
        {
            SetRuntimeVisualsEnabled(false);
            return;
        }

        EnsureRuntimeVisuals();

        Rect viewportRect = ViewportRect;
        Vector3[] nearCorners = GetWorldCorners(targetCamera, viewportRect, MinimumDepth);
        Vector3[] farCorners = GetWorldCorners(targetCamera, viewportRect, MaximumDepth);
        Vector3[] previewCorners = GetWorldCorners(targetCamera, viewportRect, PreviewDepth);

        ApplyLoop(previewRenderer, previewCorners, guideColor, previewLineWidth);
        ApplyLoop(nearRenderer, nearCorners, new Color(guideColor.r, guideColor.g, guideColor.b, 0.8f), depthLineWidth);
        ApplyLoop(farRenderer, farCorners, new Color(guideColor.r, guideColor.g, guideColor.b, 0.35f), depthLineWidth);

        bool showDepthRails = showDepthVolumeInGame;
        nearRenderer.enabled = showDepthRails;
        farRenderer.enabled = showDepthRails;

        for (int i = 0; i < depthRenderers.Length; i++)
        {
            LineRenderer rail = depthRenderers[i];
            if (rail == null)
            {
                continue;
            }

            rail.enabled = showDepthRails;
            if (!showDepthRails)
            {
                continue;
            }

            rail.positionCount = 2;
            rail.startWidth = depthLineWidth;
            rail.endWidth = depthLineWidth;
            rail.startColor = new Color(guideColor.r, guideColor.g, guideColor.b, 0.3f);
            rail.endColor = new Color(guideColor.r, guideColor.g, guideColor.b, 0.3f);
            rail.SetPosition(0, nearCorners[i]);
            rail.SetPosition(1, farCorners[i]);
        }

        previewRenderer.enabled = true;
    }

    private void SetRuntimeVisualsEnabled(bool enabled)
    {
        if (previewRenderer != null)
        {
            previewRenderer.enabled = enabled;
        }

        if (nearRenderer != null)
        {
            nearRenderer.enabled = enabled;
        }

        if (farRenderer != null)
        {
            farRenderer.enabled = enabled;
        }

        if (depthRenderers == null)
        {
            return;
        }

        for (int i = 0; i < depthRenderers.Length; i++)
        {
            if (depthRenderers[i] != null)
            {
                depthRenderers[i].enabled = enabled;
            }
        }
    }

    private LineRenderer CreateLineRenderer(string name, bool loop, Color color, float width)
    {
        Transform existing = visualRoot.Find(name);
        GameObject lineObject;
        if (existing != null)
        {
            lineObject = existing.gameObject;
        }
        else
        {
            lineObject = new GameObject(name);
            lineObject.hideFlags = HideFlags.HideInHierarchy | HideFlags.DontSaveInEditor | HideFlags.DontSaveInBuild;
            lineObject.transform.SetParent(visualRoot, false);
        }

        LineRenderer line = lineObject.GetComponent<LineRenderer>();
        if (line == null)
        {
            line = lineObject.AddComponent<LineRenderer>();
        }

        line.sharedMaterial = GetSharedLineMaterial();
        line.useWorldSpace = true;
        line.loop = loop;
        line.alignment = LineAlignment.View;
        line.textureMode = LineTextureMode.Stretch;
        line.shadowCastingMode = ShadowCastingMode.Off;
        line.receiveShadows = false;
        line.motionVectorGenerationMode = MotionVectorGenerationMode.ForceNoMotion;
        line.widthMultiplier = 1f;
        line.numCapVertices = 4;
        line.numCornerVertices = 4;
        line.startColor = color;
        line.endColor = color;
        line.startWidth = width;
        line.endWidth = width;
        return line;
    }

    private void ApplyLoop(LineRenderer line, Vector3[] corners, Color color, float width)
    {
        line.positionCount = corners.Length;
        line.startColor = color;
        line.endColor = color;
        line.startWidth = width;
        line.endWidth = width;
        for (int i = 0; i < corners.Length; i++)
        {
            line.SetPosition(i, corners[i]);
        }
    }

    private static Material GetSharedLineMaterial()
    {
        if (sharedLineMaterial != null)
        {
            return sharedLineMaterial;
        }

        Shader shader = Shader.Find("Sprites/Default");
        if (shader == null)
        {
            shader = Shader.Find("Universal Render Pipeline/Unlit");
        }

        if (shader == null)
        {
            shader = Shader.Find("Hidden/Internal-Colored");
        }

        sharedLineMaterial = new Material(shader)
        {
            hideFlags = HideFlags.HideAndDontSave
        };
        return sharedLineMaterial;
    }
}
