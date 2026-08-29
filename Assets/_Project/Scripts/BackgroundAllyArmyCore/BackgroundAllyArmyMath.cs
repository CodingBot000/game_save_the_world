using UnityEngine;

public static class BackgroundAllyArmyMath
{
    public const float TwoPi = Mathf.PI * 2f;

    public static Vector3 EvaluateOrbitPosition(
        Vector3 center,
        Vector3 cameraRight,
        Vector3 cameraUp,
        float radiusX,
        float radiusY,
        float angleRadians)
    {
        return center
               + cameraRight.normalized * (Mathf.Cos(angleRadians) * Mathf.Max(0f, radiusX))
               + cameraUp.normalized * (Mathf.Sin(angleRadians) * Mathf.Max(0f, radiusY));
    }

    public static Vector3 EvaluateOrbitTangent(
        Vector3 cameraRight,
        Vector3 cameraUp,
        float radiusX,
        float radiusY,
        float angleRadians,
        float directionSign)
    {
        Vector3 tangent = cameraRight.normalized * (-Mathf.Sin(angleRadians) * Mathf.Max(0f, radiusX))
                          + cameraUp.normalized * (Mathf.Cos(angleRadians) * Mathf.Max(0f, radiusY));
        tangent *= directionSign < 0f ? -1f : 1f;
        return tangent.sqrMagnitude > 0.000001f ? tangent.normalized : cameraRight.normalized;
    }

    public static Vector3 EvaluateFormationTarget(
        Vector3 leaderPosition,
        Vector3 leaderForward,
        Vector3 radialDirection,
        float trailDistance,
        float lateralDistance,
        int wingSide)
    {
        Vector3 forward = leaderForward.sqrMagnitude > 0.000001f ? leaderForward.normalized : Vector3.forward;
        Vector3 radial = radialDirection.sqrMagnitude > 0.000001f ? radialDirection.normalized : Vector3.right;
        float side = wingSide < 0 ? -1f : wingSide > 0 ? 1f : 0f;
        return leaderPosition
               - forward * Mathf.Max(0f, trailDistance)
               + radial * (Mathf.Max(0f, lateralDistance) * side);
    }

    public static float ResolveNearestOrbitAngle(
        Vector3 worldPosition,
        Vector3 center,
        Vector3 cameraRight,
        Vector3 cameraUp,
        float radiusX,
        float radiusY)
    {
        Vector3 offset = worldPosition - center;
        float x = Vector3.Dot(offset, cameraRight.normalized) / Mathf.Max(0.0001f, Mathf.Abs(radiusX));
        float y = Vector3.Dot(offset, cameraUp.normalized) / Mathf.Max(0.0001f, Mathf.Abs(radiusY));
        return NormalizeRadians(Mathf.Atan2(y, x));
    }

    public static float ExponentialSmoothingFactor(float speed, float deltaTime)
    {
        if (speed <= 0f || deltaTime <= 0f)
        {
            return 0f;
        }

        return 1f - Mathf.Exp(-speed * deltaTime);
    }

    public static Quaternion EvaluateConstrainedFlightRotation(
        Vector3 desiredDirection,
        Vector3 fallbackForward,
        Vector3 worldUp,
        float maximumPitchDegrees,
        float bankDegrees)
    {
        Vector3 up = worldUp.sqrMagnitude > 0.000001f ? worldUp.normalized : Vector3.up;
        Vector3 rawForward = desiredDirection.sqrMagnitude > 0.000001f
            ? desiredDirection.normalized
            : fallbackForward.sqrMagnitude > 0.000001f
                ? fallbackForward.normalized
                : Vector3.forward;
        Vector3 planarForward = Vector3.ProjectOnPlane(rawForward, up);
        if (planarForward.sqrMagnitude <= 0.000001f)
        {
            planarForward = Vector3.ProjectOnPlane(fallbackForward, up);
        }

        if (planarForward.sqrMagnitude <= 0.000001f)
        {
            planarForward = Vector3.Cross(up, Vector3.right);
            if (planarForward.sqrMagnitude <= 0.000001f)
            {
                planarForward = Vector3.Cross(up, Vector3.forward);
            }
        }

        planarForward.Normalize();
        float verticalFactor = Mathf.Clamp(Vector3.Dot(rawForward, up), -1f, 1f);
        float pitch = -verticalFactor * Mathf.Max(0f, maximumPitchDegrees);
        return Quaternion.LookRotation(planarForward, up) * Quaternion.Euler(pitch, 0f, bankDegrees);
    }

    public static float Smooth01(float value)
    {
        float t = Mathf.Clamp01(value);
        return t * t * (3f - 2f * t);
    }

    public static float NormalizeRadians(float value)
    {
        value %= TwoPi;
        return value < 0f ? value + TwoPi : value;
    }
}
