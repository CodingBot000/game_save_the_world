using UnityEngine;
using UnityEngine.Rendering;

[ExecuteAlways]
public class PlayerMovementBounds : MonoBehaviour
{
    private const string RuntimeGuideRootName = "_RuntimeMovementBoundsGuide";

    [SerializeField] private Vector3 localCenter = Vector3.zero;
    [SerializeField] private Vector3 halfExtents = new(2.4f, 1.15f, 0.9f);
    [SerializeField] private bool showGizmo = true;
    [SerializeField] private bool showRuntimeGuide = false;
    [SerializeField] private Color runtimeGuideColor = new(0.12f, 1f, 0.38f, 0.95f);
    [SerializeField] private float runtimeGuideLineWidth = 0.045f;

    private static readonly int ZWriteId = Shader.PropertyToID("_ZWrite");
    private static readonly int ZTestId = Shader.PropertyToID("_ZTest");
    private static Material sharedRuntimeGuideMaterial;

    private Transform runtimeGuideRoot;
    private LineRenderer[] runtimeGuideEdges;

    private static readonly int[,] EdgeIndices =
    {
        { 0, 1 },
        { 1, 2 },
        { 2, 3 },
        { 3, 0 },
        { 4, 5 },
        { 5, 6 },
        { 6, 7 },
        { 7, 4 },
        { 0, 4 },
        { 1, 5 },
        { 2, 6 },
        { 3, 7 }
    };

    public Vector3 ClampWorldPosition(Vector3 worldPosition)
    {
        Vector3 localPosition = transform.InverseTransformPoint(worldPosition) - localCenter;
        Vector3 clampedLocalOffset = new(
            Mathf.Clamp(localPosition.x, -Mathf.Abs(halfExtents.x), Mathf.Abs(halfExtents.x)),
            Mathf.Clamp(localPosition.y, -Mathf.Abs(halfExtents.y), Mathf.Abs(halfExtents.y)),
            Mathf.Clamp(localPosition.z, -Mathf.Abs(halfExtents.z), Mathf.Abs(halfExtents.z)));
        return transform.TransformPoint(localCenter + clampedLocalOffset);
    }

    public void GetAxes(out Vector3 right, out Vector3 up, out Vector3 forward)
    {
        right = Vector3.ProjectOnPlane(transform.right, Vector3.up);
        if (right.sqrMagnitude <= 0.0001f)
        {
            right = Vector3.right;
        }
        else
        {
            right.Normalize();
        }

        forward = Vector3.ProjectOnPlane(transform.forward, Vector3.up);
        if (forward.sqrMagnitude <= 0.0001f)
        {
            forward = Vector3.forward;
        }
        else
        {
            forward.Normalize();
        }

        up = Vector3.up;
    }

    private void OnEnable()
    {
        UpdateRuntimeGuide();
    }

    private void LateUpdate()
    {
        UpdateRuntimeGuide();
    }

    private void OnDisable()
    {
        SetRuntimeGuideEnabled(false);
    }

    private void OnDrawGizmos()
    {
        if (!showGizmo)
        {
            return;
        }

        Matrix4x4 previousMatrix = Gizmos.matrix;
        Color previousColor = Gizmos.color;
        Gizmos.matrix = transform.localToWorldMatrix;
        Gizmos.color = new Color(0.12f, 1f, 0.38f, 0.45f);
        Gizmos.DrawWireCube(localCenter, halfExtents * 2f);
        Gizmos.color = new Color(0.12f, 1f, 0.38f, 0.08f);
        Gizmos.DrawCube(localCenter, halfExtents * 2f);
        Gizmos.matrix = previousMatrix;
        Gizmos.color = previousColor;
    }

    private void OnValidate()
    {
        runtimeGuideLineWidth = Mathf.Max(0.001f, runtimeGuideLineWidth);
    }

    private void UpdateRuntimeGuide()
    {
        if (!Application.isPlaying || !showRuntimeGuide)
        {
            SetRuntimeGuideEnabled(false);
            return;
        }

        EnsureRuntimeGuide();

        Vector3[] corners = GetWorldCorners();
        Color color = runtimeGuideColor;
        float lineWidth = Mathf.Max(0.001f, runtimeGuideLineWidth);

        for (int i = 0; i < runtimeGuideEdges.Length; i++)
        {
            LineRenderer edge = runtimeGuideEdges[i];
            if (edge == null)
            {
                continue;
            }

            int startIndex = EdgeIndices[i, 0];
            int endIndex = EdgeIndices[i, 1];
            edge.enabled = true;
            edge.startColor = color;
            edge.endColor = color;
            edge.startWidth = lineWidth;
            edge.endWidth = lineWidth;
            edge.SetPosition(0, corners[startIndex]);
            edge.SetPosition(1, corners[endIndex]);
        }
    }

    private void EnsureRuntimeGuide()
    {
        if (runtimeGuideRoot == null)
        {
            Transform existing = transform.Find(RuntimeGuideRootName);
            if (existing != null)
            {
                runtimeGuideRoot = existing;
            }
            else
            {
                GameObject root = new(RuntimeGuideRootName);
                root.hideFlags = HideFlags.HideInHierarchy | HideFlags.DontSaveInEditor | HideFlags.DontSaveInBuild;
                runtimeGuideRoot = root.transform;
                runtimeGuideRoot.SetParent(transform, false);
            }
        }

        if (runtimeGuideEdges == null || runtimeGuideEdges.Length != EdgeIndices.GetLength(0))
        {
            runtimeGuideEdges = new LineRenderer[EdgeIndices.GetLength(0)];
        }

        for (int i = 0; i < runtimeGuideEdges.Length; i++)
        {
            runtimeGuideEdges[i] ??= CreateRuntimeGuideEdge(i);
        }
    }

    private LineRenderer CreateRuntimeGuideEdge(int index)
    {
        Transform existing = runtimeGuideRoot.Find($"Edge_{index:00}");
        GameObject edgeObject;
        if (existing != null)
        {
            edgeObject = existing.gameObject;
        }
        else
        {
            edgeObject = new GameObject($"Edge_{index:00}");
            edgeObject.hideFlags = HideFlags.HideInHierarchy | HideFlags.DontSaveInEditor | HideFlags.DontSaveInBuild;
            edgeObject.transform.SetParent(runtimeGuideRoot, false);
        }

        LineRenderer line = edgeObject.GetComponent<LineRenderer>();
        if (line == null)
        {
            line = edgeObject.AddComponent<LineRenderer>();
        }

        line.sharedMaterial = GetRuntimeGuideMaterial();
        line.useWorldSpace = true;
        line.loop = false;
        line.positionCount = 2;
        line.alignment = LineAlignment.View;
        line.textureMode = LineTextureMode.Stretch;
        line.shadowCastingMode = ShadowCastingMode.Off;
        line.receiveShadows = false;
        line.motionVectorGenerationMode = MotionVectorGenerationMode.ForceNoMotion;
        line.widthMultiplier = 1f;
        line.numCapVertices = 4;
        line.startColor = runtimeGuideColor;
        line.endColor = runtimeGuideColor;
        line.startWidth = runtimeGuideLineWidth;
        line.endWidth = runtimeGuideLineWidth;
        return line;
    }

    private void SetRuntimeGuideEnabled(bool enabled)
    {
        if (runtimeGuideEdges == null)
        {
            return;
        }

        for (int i = 0; i < runtimeGuideEdges.Length; i++)
        {
            if (runtimeGuideEdges[i] != null)
            {
                runtimeGuideEdges[i].enabled = enabled;
            }
        }
    }

    private Vector3[] GetWorldCorners()
    {
        Vector3 extents = new(Mathf.Abs(halfExtents.x), Mathf.Abs(halfExtents.y), Mathf.Abs(halfExtents.z));
        Vector3 min = localCenter - extents;
        Vector3 max = localCenter + extents;

        return new[]
        {
            transform.TransformPoint(new Vector3(min.x, min.y, min.z)),
            transform.TransformPoint(new Vector3(min.x, min.y, max.z)),
            transform.TransformPoint(new Vector3(max.x, min.y, max.z)),
            transform.TransformPoint(new Vector3(max.x, min.y, min.z)),
            transform.TransformPoint(new Vector3(min.x, max.y, min.z)),
            transform.TransformPoint(new Vector3(min.x, max.y, max.z)),
            transform.TransformPoint(new Vector3(max.x, max.y, max.z)),
            transform.TransformPoint(new Vector3(max.x, max.y, min.z))
        };
    }

    private static Material GetRuntimeGuideMaterial()
    {
        if (sharedRuntimeGuideMaterial != null)
        {
            return sharedRuntimeGuideMaterial;
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

        sharedRuntimeGuideMaterial = new Material(shader)
        {
            hideFlags = HideFlags.HideAndDontSave,
            renderQueue = (int)RenderQueue.Overlay
        };

        if (sharedRuntimeGuideMaterial.HasProperty(ZWriteId))
        {
            sharedRuntimeGuideMaterial.SetFloat(ZWriteId, 0f);
        }

        if (sharedRuntimeGuideMaterial.HasProperty(ZTestId))
        {
            sharedRuntimeGuideMaterial.SetFloat(ZTestId, (float)CompareFunction.Always);
        }

        if (sharedRuntimeGuideMaterial.HasProperty("_Color"))
        {
            sharedRuntimeGuideMaterial.SetColor("_Color", Color.white);
        }

        if (sharedRuntimeGuideMaterial.HasProperty("_BaseColor"))
        {
            sharedRuntimeGuideMaterial.SetColor("_BaseColor", Color.white);
        }

        return sharedRuntimeGuideMaterial;
    }
}
