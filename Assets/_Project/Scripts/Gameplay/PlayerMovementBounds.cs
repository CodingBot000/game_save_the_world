using UnityEngine;

[ExecuteAlways]
public class PlayerMovementBounds : MonoBehaviour
{
    [SerializeField] private Vector3 localCenter = Vector3.zero;
    [SerializeField] private Vector3 halfExtents = new(2.4f, 1.15f, 0.9f);
    [SerializeField] private bool showGizmo = true;

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
}
