using System;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

public class MountedSidewinderCosmeticTests
{
    private GameObject root;

    [TearDown]
    public void TearDown()
    {
        if (root != null)
        {
            UnityEngine.Object.DestroyImmediate(root);
        }
    }

    [Test]
    public void Defaults_UseOneSecondIgnitionThenVisibleSlowToFastFlightAndZeroDamage()
    {
        root = new GameObject("Player");
        Type controllerType = ResolveControllerType();
        Component controller = root.AddComponent(controllerType);

        Assert.That(ReadProperty<float>(controller, "IgnitionDuration"),
            Is.EqualTo(1f).Within(0.0001f));
        Assert.That(ReadProperty<float>(controller, "MaximumFlightDuration"),
            Is.EqualTo(6f).Within(0.0001f));
        Assert.That(ReadProperty<float>(controller, "SlowFlightDuration"),
            Is.EqualTo(0.5f).Within(0.0001f));
        Assert.That(ReadProperty<float>(controller, "CosmeticLaunchSpeed"),
            Is.EqualTo(5f).Within(0.0001f));
        Assert.That(ReadProperty<float>(controller, "CosmeticCruiseSpeed"),
            Is.EqualTo(35f).Within(0.0001f));
        Assert.That(ReadProperty<float>(controller, "CosmeticAcceleration"),
            Is.EqualTo(20f).Within(0.0001f));
        Assert.That(ReadProperty<float>(controller, "DamagePerCosmeticMissile"), Is.Zero);
    }

    [Test]
    public void FlightSpeed_HoldsSlowForHalfSecondThenAcceleratesToCruiseSpeed()
    {
        Type controllerType = ResolveControllerType();
        MethodInfo evaluateSpeed = controllerType.GetMethod(
            "EvaluateCosmeticFlightSpeed",
            BindingFlags.Public | BindingFlags.Static);
        Assert.That(evaluateSpeed, Is.Not.Null);

        Assert.That(EvaluateSpeed(evaluateSpeed, 0f), Is.EqualTo(5f).Within(0.0001f));
        Assert.That(EvaluateSpeed(evaluateSpeed, 0.5f), Is.EqualTo(5f).Within(0.0001f));
        Assert.That(EvaluateSpeed(evaluateSpeed, 1f), Is.EqualTo(15f).Within(0.0001f));
        Assert.That(EvaluateSpeed(evaluateSpeed, 2f), Is.EqualTo(35f).Within(0.0001f));
        Assert.That(EvaluateSpeed(evaluateSpeed, 3f), Is.EqualTo(35f).Within(0.0001f));
    }

    [Test]
    public void RefreshBindings_ResolvesOnlyTheTwoOuterMountedSidewinders()
    {
        root = new GameObject("Player");
        Transform visualRoot = CreateChild(root.transform, "PlayerVisualRoot");
        CreateMountedSidewinder(visualRoot, "WeponePylon_L_03");
        CreateMountedSidewinder(visualRoot, "WeponePylon_R_03");
        CreateChild(visualRoot, "RocketPod19rds").gameObject.AddComponent<MeshRenderer>();
        CreateChild(visualRoot, "AGM_Black").gameObject.AddComponent<MeshRenderer>();

        Type controllerType = ResolveControllerType();
        Component controller = root.AddComponent(controllerType);
        MethodInfo configure = controllerType.GetMethod("Configure");
        MethodInfo refreshBindings = controllerType.GetMethod("RefreshBindingsForDebug");
        Assert.That(configure, Is.Not.Null);
        Assert.That(refreshBindings, Is.Not.Null);
        configure.Invoke(controller, new object[] { null, null, null });

        Assert.That((bool)refreshBindings.Invoke(controller, null), Is.True);
        Assert.That(ReadProperty<int>(controller, "ResolvedMountedSidewinderCount"),
            Is.EqualTo(2));
        Assert.That(ReadProperty<string>(controller, "LastBindingFailure"), Is.Empty);
    }

    [Test]
    public void SegmentReachesTarget_DetectsFastProjectilePassingThroughTargetRadius()
    {
        Type controllerType = ResolveControllerType();
        MethodInfo segmentReachesTarget = controllerType.GetMethod(
            "SegmentReachesTarget",
            BindingFlags.Public | BindingFlags.Static);
        Assert.That(segmentReachesTarget, Is.Not.Null);

        Assert.That(InvokeSegmentTest(
            segmentReachesTarget,
            Vector3.zero,
            Vector3.forward * 10f,
            Vector3.forward * 5f,
            0.25f), Is.True);
        Assert.That(InvokeSegmentTest(
            segmentReachesTarget,
            Vector3.zero,
            Vector3.forward * 10f,
            new Vector3(2f, 0f, 5f),
            0.25f), Is.False);
    }

    private static void CreateMountedSidewinder(Transform visualRoot, string pylonName)
    {
        Transform pylon = CreateChild(visualRoot, pylonName);
        Transform sidewinder = CreateChild(pylon, "Sidewinder");
        Transform boneRoot = CreateChild(sidewinder, "Root_Sidewinder");
        Transform nozzle = CreateChild(boneRoot, "FX_Nozzle");
        nozzle.localPosition = Vector3.up * 0.4f;
        CreateChild(sidewinder, "Sidewinder").gameObject.AddComponent<MeshRenderer>();
    }

    private static Transform CreateChild(Transform parent, string name)
    {
        GameObject child = new(name);
        child.transform.SetParent(parent, false);
        return child.transform;
    }

    private static Type ResolveControllerType()
    {
        Type controllerType = Type.GetType(
            "MountedSidewinderCosmeticController, Assembly-CSharp");
        Assert.That(controllerType, Is.Not.Null);
        return controllerType;
    }

    private static T ReadProperty<T>(Component component, string propertyName)
    {
        PropertyInfo property = component.GetType().GetProperty(propertyName);
        Assert.That(property, Is.Not.Null);
        return (T)property.GetValue(component);
    }

    private static bool InvokeSegmentTest(
        MethodInfo method,
        Vector3 start,
        Vector3 end,
        Vector3 target,
        float radius)
    {
        return (bool)method.Invoke(null, new object[] { start, end, target, radius });
    }

    private static float EvaluateSpeed(MethodInfo method, float elapsed)
    {
        return (float)method.Invoke(
            null,
            new object[] { elapsed, 5f, 35f, 0.5f, 20f });
    }
}
