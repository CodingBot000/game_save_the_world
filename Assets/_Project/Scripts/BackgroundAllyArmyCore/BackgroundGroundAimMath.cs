using UnityEngine;

public static class BackgroundGroundAimMath
{
    private const float MinimumDirectionSqrMagnitude = 0.000001f;

    public static bool TryCalculateYawRotation(
        Quaternion restRotation,
        Vector3 restAimDirectionInParent,
        Vector3 yawAxisInParent,
        Vector3 targetDirectionInParent,
        out Quaternion targetRotation)
    {
        targetRotation = restRotation;
        if (yawAxisInParent.sqrMagnitude <= MinimumDirectionSqrMagnitude)
        {
            return false;
        }

        yawAxisInParent.Normalize();
        Vector3 restPlanarDirection = Vector3.ProjectOnPlane(restAimDirectionInParent, yawAxisInParent);
        Vector3 targetPlanarDirection = Vector3.ProjectOnPlane(targetDirectionInParent, yawAxisInParent);
        if (restPlanarDirection.sqrMagnitude <= MinimumDirectionSqrMagnitude
            || targetPlanarDirection.sqrMagnitude <= MinimumDirectionSqrMagnitude)
        {
            return false;
        }

        float yawDegrees = Vector3.SignedAngle(
            restPlanarDirection.normalized,
            targetPlanarDirection.normalized,
            yawAxisInParent);
        targetRotation = Quaternion.AngleAxis(yawDegrees, yawAxisInParent) * restRotation;
        return true;
    }

    public static bool TryCalculatePitchRotation(
        Quaternion restRotation,
        Vector3 restAimDirectionInParent,
        Vector3 upInParent,
        Vector3 targetDirectionInParent,
        float maximumPitchUpDegrees,
        float maximumPitchDownDegrees,
        out Quaternion targetRotation)
    {
        targetRotation = restRotation;
        if (upInParent.sqrMagnitude <= MinimumDirectionSqrMagnitude
            || targetDirectionInParent.sqrMagnitude <= MinimumDirectionSqrMagnitude)
        {
            return false;
        }

        upInParent.Normalize();
        Vector3 restPlanarDirection = Vector3.ProjectOnPlane(restAimDirectionInParent, upInParent);
        Vector3 pitchAxisInParent = Vector3.Cross(upInParent, restPlanarDirection);
        if (pitchAxisInParent.sqrMagnitude <= MinimumDirectionSqrMagnitude)
        {
            return false;
        }

        pitchAxisInParent.Normalize();
        Vector3 targetInPitchPlane = Vector3.ProjectOnPlane(targetDirectionInParent, pitchAxisInParent);
        if (targetInPitchPlane.sqrMagnitude <= MinimumDirectionSqrMagnitude)
        {
            return false;
        }

        float pitchDegrees = Vector3.SignedAngle(
            restAimDirectionInParent,
            targetInPitchPlane.normalized,
            pitchAxisInParent);
        pitchDegrees = Mathf.Clamp(
            pitchDegrees,
            -Mathf.Abs(maximumPitchUpDegrees),
            Mathf.Abs(maximumPitchDownDegrees));
        targetRotation = Quaternion.AngleAxis(pitchDegrees, pitchAxisInParent) * restRotation;
        return true;
    }
}
