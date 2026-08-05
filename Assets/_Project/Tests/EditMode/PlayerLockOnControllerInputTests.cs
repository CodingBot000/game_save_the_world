using System;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

public class PlayerLockOnControllerInputTests
{
    private const BindingFlags InstanceFlags =
        BindingFlags.Instance | BindingFlags.NonPublic;

    private GameObject controllerObject;

    [TearDown]
    public void TearDown()
    {
        if (controllerObject != null)
        {
            UnityEngine.Object.DestroyImmediate(controllerObject);
        }
    }

    [Test]
    public void TemporaryOverrides_DefaultToTimedInputAndOriginalStageProfiles()
    {
        Type controllerType = Type.GetType("PlayerLockOnController, Assembly-CSharp");
        Type inputSourceType = Type.GetType("LockOnInputSource, Assembly-CSharp");
        Assert.That(controllerType, Is.Not.Null);
        Assert.That(inputSourceType, Is.Not.Null);

        controllerObject = new GameObject("LockOnInputOverrideTest");
        Component controller = controllerObject.AddComponent(controllerType);
        MethodInfo shouldApplyMethod = controllerType.GetMethod(
            "ShouldApplyMouseRightFullChargeOverride",
            InstanceFlags);
        MethodInfo resolveProfileMethod = controllerType.GetMethod(
            "ResolveSalvoProfileLockCount",
            InstanceFlags);
        PropertyInfo mountedIgnitionDuration = controllerType.GetProperty(
            "FullSalvoMountedSidewinderIgnitionDuration");
        Assert.That(shouldApplyMethod, Is.Not.Null);
        Assert.That(resolveProfileMethod, Is.Not.Null);
        Assert.That(mountedIgnitionDuration, Is.Not.Null);
        Assert.That(
            (float)mountedIgnitionDuration.GetValue(controller),
            Is.EqualTo(1f).Within(0.0001f));

        object mouseRight = Enum.Parse(inputSourceType, "MouseRight");
        object mobileHud = Enum.Parse(inputSourceType, "MobileHud");
        object debug = Enum.Parse(inputSourceType, "Debug");

        Assert.That(
            shouldApplyMethod.Invoke(controller, new[] { mouseRight }),
            Is.False);
        Assert.That(
            shouldApplyMethod.Invoke(controller, new[] { mobileHud }),
            Is.False);
        Assert.That(
            shouldApplyMethod.Invoke(controller, new[] { debug }),
            Is.False);

        Assert.That(ReadPublicProperty<bool>(controller, "ForceFullChargeOnMouseRightForTesting"),
            Is.False);
        Assert.That(ReadPublicProperty<bool>(controller, "PromoteThreeOrMoreLocksToFullSalvoForTesting"),
            Is.False);
        Assert.That(ResolveProfile(resolveProfileMethod, controller, 1), Is.EqualTo(1));
        Assert.That(ResolveProfile(resolveProfileMethod, controller, 2), Is.EqualTo(2));
        Assert.That(ResolveProfile(resolveProfileMethod, controller, 3), Is.EqualTo(3));
        Assert.That(ResolveProfile(resolveProfileMethod, controller, 4), Is.EqualTo(4));
        Assert.That(ResolveProfile(resolveProfileMethod, controller, 5), Is.EqualTo(5));
    }

    [Test]
    public void PromotionOverride_CanStillBeEnabledExplicitlyForDebugging()
    {
        Type controllerType = Type.GetType("PlayerLockOnController, Assembly-CSharp");
        Assert.That(controllerType, Is.Not.Null);

        controllerObject = new GameObject("LockOnPromotionRestoreTest");
        Component controller = controllerObject.AddComponent(controllerType);
        FieldInfo promotionField = controllerType.GetField(
            "promoteThreeOrMoreLocksToFullSalvoForTesting",
            InstanceFlags);
        MethodInfo resolveProfileMethod = controllerType.GetMethod(
            "ResolveSalvoProfileLockCount",
            InstanceFlags);
        Assert.That(promotionField, Is.Not.Null);
        Assert.That(resolveProfileMethod, Is.Not.Null);

        promotionField.SetValue(controller, true);

        Assert.That(ResolveProfile(resolveProfileMethod, controller, 1), Is.EqualTo(1));
        Assert.That(ResolveProfile(resolveProfileMethod, controller, 2), Is.EqualTo(2));
        Assert.That(ResolveProfile(resolveProfileMethod, controller, 3), Is.EqualTo(5));
        Assert.That(ResolveProfile(resolveProfileMethod, controller, 4), Is.EqualTo(5));
        Assert.That(ResolveProfile(resolveProfileMethod, controller, 5), Is.EqualTo(5));
    }

    private static int ResolveProfile(
        MethodInfo resolveProfileMethod,
        Component controller,
        int successfulLocks)
    {
        return (int)resolveProfileMethod.Invoke(controller, new object[] { successfulLocks });
    }

    private static T ReadPublicProperty<T>(Component component, string propertyName)
    {
        PropertyInfo property = component.GetType().GetProperty(propertyName);
        Assert.That(property, Is.Not.Null);
        return (T)property.GetValue(component);
    }
}
