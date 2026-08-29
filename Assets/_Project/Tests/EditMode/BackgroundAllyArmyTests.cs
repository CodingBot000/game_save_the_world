using NUnit.Framework;
using UnityEngine;

public class BackgroundAllyArmyTests
{
    [Test]
    public void OrbitPositionUsesCameraPlaneAxes()
    {
        Vector3 center = new(3f, 4f, 5f);
        Vector3 right = new(0f, 0f, 1f);
        Vector3 up = Vector3.up;

        Vector3 atZero = BackgroundAllyArmyMath.EvaluateOrbitPosition(center, right, up, 10f, 4f, 0f);
        Vector3 atQuarter = BackgroundAllyArmyMath.EvaluateOrbitPosition(center, right, up, 10f, 4f, Mathf.PI * 0.5f);

        Assert.That(Vector3.Distance(atZero, center + right * 10f), Is.LessThan(0.0001f));
        Assert.That(Vector3.Distance(atQuarter, center + up * 4f), Is.LessThan(0.0001f));
    }

    [Test]
    public void OrbitTangentReversesWithDirectionSign()
    {
        Vector3 forward = BackgroundAllyArmyMath.EvaluateOrbitTangent(
            Vector3.right,
            Vector3.up,
            10f,
            4f,
            Mathf.PI * 0.25f,
            1f);
        Vector3 reverse = BackgroundAllyArmyMath.EvaluateOrbitTangent(
            Vector3.right,
            Vector3.up,
            10f,
            4f,
            Mathf.PI * 0.25f,
            -1f);

        Assert.That(Vector3.Dot(forward, reverse), Is.LessThan(-0.9999f));
        Assert.That(forward.magnitude, Is.EqualTo(1f).Within(0.0001f));
    }

    [Test]
    public void FormationWingTargetsAreSymmetric()
    {
        Vector3 leader = new(1f, 2f, 3f);
        Vector3 forward = Vector3.forward;
        Vector3 radial = Vector3.right;

        Vector3 left = BackgroundAllyArmyMath.EvaluateFormationTarget(leader, forward, radial, 1.5f, 0.8f, -1);
        Vector3 right = BackgroundAllyArmyMath.EvaluateFormationTarget(leader, forward, radial, 1.5f, 0.8f, 1);

        Vector3 midpoint = (left + right) * 0.5f;
        Assert.That(Vector3.Distance(midpoint, leader - forward * 1.5f), Is.LessThan(0.0001f));
        Assert.That(Vector3.Distance(left, right), Is.EqualTo(1.6f).Within(0.0001f));
    }

    [Test]
    public void ResolveNearestOrbitAngleRecoversRepresentativeAngles()
    {
        Vector3 center = new(-2f, 3f, 7f);
        float[] angles = { 0f, 0.37f, Mathf.PI * 0.5f, Mathf.PI, Mathf.PI * 1.73f };
        for (int i = 0; i < angles.Length; i++)
        {
            Vector3 position = BackgroundAllyArmyMath.EvaluateOrbitPosition(
                center,
                Vector3.right,
                Vector3.up,
                11f,
                5f,
                angles[i]);
            float resolved = BackgroundAllyArmyMath.ResolveNearestOrbitAngle(
                position,
                center,
                Vector3.right,
                Vector3.up,
                11f,
                5f);

            Assert.That(Mathf.Abs(Mathf.DeltaAngle(angles[i] * Mathf.Rad2Deg, resolved * Mathf.Rad2Deg)), Is.LessThan(0.001f));
        }
    }

    [Test]
    public void SmoothingHelpersClampAndRemainMonotonic()
    {
        Assert.That(BackgroundAllyArmyMath.Smooth01(-1f), Is.EqualTo(0f));
        Assert.That(BackgroundAllyArmyMath.Smooth01(2f), Is.EqualTo(1f));
        Assert.That(BackgroundAllyArmyMath.Smooth01(0.25f), Is.LessThan(BackgroundAllyArmyMath.Smooth01(0.75f)));
        Assert.That(BackgroundAllyArmyMath.ExponentialSmoothingFactor(6f, 0f), Is.EqualTo(0f));
        Assert.That(BackgroundAllyArmyMath.ExponentialSmoothingFactor(6f, 0.25f), Is.InRange(0f, 1f));
    }

    [Test]
    public void FlightRotationKeepsSteepMovementNearLevelAndForward()
    {
        Vector3 desired = new Vector3(0.2f, 1f, 0.5f).normalized;
        Quaternion rotation = BackgroundAllyArmyMath.EvaluateConstrainedFlightRotation(
            desired,
            Vector3.forward,
            Vector3.up,
            7f,
            8f);

        Vector3 visualForward = rotation * Vector3.forward;
        Vector3 visualUp = rotation * Vector3.up;
        Vector3 planarDesired = Vector3.ProjectOnPlane(desired, Vector3.up).normalized;
        Assert.That(Vector3.Dot(visualForward, planarDesired), Is.GreaterThan(0.98f));
        Assert.That(Vector3.Angle(visualUp, Vector3.up), Is.LessThan(12f));
    }

    [Test]
    public void FlightRotationUsesFallbackWhenMovementIsVertical()
    {
        Quaternion rotation = BackgroundAllyArmyMath.EvaluateConstrainedFlightRotation(
            Vector3.up,
            Vector3.left,
            Vector3.up,
            7f,
            0f);

        Vector3 planarForward = Vector3.ProjectOnPlane(rotation * Vector3.forward, Vector3.up).normalized;
        Assert.That(Vector3.Dot(planarForward, Vector3.left), Is.GreaterThan(0.999f));
        Assert.That(Vector3.Angle(rotation * Vector3.up, Vector3.up), Is.LessThan(7.1f));
    }
}
