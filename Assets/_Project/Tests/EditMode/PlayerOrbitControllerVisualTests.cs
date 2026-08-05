using System;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;

public class PlayerOrbitControllerVisualTests
{
    private const BindingFlags InstanceFlags =
        BindingFlags.Instance | BindingFlags.NonPublic;
    private const BindingFlags StaticFlags =
        BindingFlags.Static | BindingFlags.NonPublic;

    private GameObject cameraObject;
    private GameObject playerObject;
    private Keyboard testKeyboard;

    [TearDown]
    public void TearDown()
    {
        if (testKeyboard != null && testKeyboard.added)
        {
            InputSystem.RemoveDevice(testKeyboard);
        }

        if (playerObject != null)
        {
            UnityEngine.Object.DestroyImmediate(playerObject);
        }

        if (cameraObject != null)
        {
            UnityEngine.Object.DestroyImmediate(cameraObject);
        }
    }

    [Test]
    public void LateUpdate_ProcessesMovementInputWithoutBossReferences()
    {
        Type controllerType = Type.GetType("PlayerOrbitController, Assembly-CSharp");
        Assert.That(controllerType, Is.Not.Null);

        playerObject = new GameObject("TargetIndependentMovementInputTestPlayer");
        Component controller = playerObject.AddComponent(controllerType);
        MethodInfo configure = controllerType.GetMethod(
            "Configure",
            BindingFlags.Instance | BindingFlags.Public);
        MethodInfo lateUpdate = controllerType.GetMethod("LateUpdate", InstanceFlags);
        FieldInfo movementInput = controllerType.GetField("movementInput", InstanceFlags);
        Assert.That(configure, Is.Not.Null);
        Assert.That(lateUpdate, Is.Not.Null);
        Assert.That(movementInput, Is.Not.Null);

        configure.Invoke(controller, new object[] { null, null, null, null, null });
        testKeyboard = InputSystem.AddDevice<Keyboard>("TargetIndependentMovementTestKeyboard");
        InputSystem.QueueStateEvent(testKeyboard, new KeyboardState(Key.D));
        InputSystem.Update();

        lateUpdate.Invoke(controller, null);

        Vector2 actualInput = (Vector2)movementInput.GetValue(controller);
        Assert.That(actualInput.x, Is.EqualTo(1f).Within(0.0001f));
        Assert.That(actualInput.y, Is.EqualTo(0f).Within(0.0001f));
    }

    [Test]
    public void Configure_ReenablesMovementInputForNewBattleSession()
    {
        Type controllerType = Type.GetType("PlayerOrbitController, Assembly-CSharp");
        Assert.That(controllerType, Is.Not.Null);

        playerObject = new GameObject("MovementInputResetTestPlayer");
        Component controller = playerObject.AddComponent(controllerType);
        MethodInfo setInputEnabled = controllerType.GetMethod(
            "SetInputEnabled",
            BindingFlags.Instance | BindingFlags.Public);
        MethodInfo configure = controllerType.GetMethod(
            "Configure",
            BindingFlags.Instance | BindingFlags.Public);
        PropertyInfo debugInputEnabled = controllerType.GetProperty(
            "DebugInputEnabled",
            BindingFlags.Instance | BindingFlags.Public);
        Assert.That(setInputEnabled, Is.Not.Null);
        Assert.That(configure, Is.Not.Null);
        Assert.That(debugInputEnabled, Is.Not.Null);

        setInputEnabled.Invoke(controller, new object[] { false });
        Assert.That((bool)debugInputEnabled.GetValue(controller), Is.False);

        configure.Invoke(controller, new object[] { null, null, null, null, null });

        Assert.That((bool)debugInputEnabled.GetValue(controller), Is.True);
    }

    [Test]
    public void FullSalvoFacingRotation_IsFormerCameraFacingPoseRotatedBy180Degrees()
    {
        Type controllerType = Type.GetType("PlayerOrbitController, Assembly-CSharp");
        Assert.That(controllerType, Is.Not.Null);

        cameraObject = new GameObject("FullSalvoFacingTestCamera");
        Camera camera = cameraObject.AddComponent<Camera>();
        camera.transform.rotation = Quaternion.Euler(12f, 37f, 5f);

        playerObject = new GameObject("FullSalvoFacingTestPlayer");
        Component controller = playerObject.AddComponent(controllerType);

        FieldInfo movementCameraField = controllerType.GetField("movementCamera", InstanceFlags);
        FieldInfo tuningOffsetField = controllerType.GetField(
            "cinematicFrontViewEulerOffset",
            InstanceFlags);
        MethodInfo resolveRotationMethod = controllerType.GetMethod(
            "ResolveCameraFacingDisplayRotation",
            InstanceFlags);
        Assert.That(movementCameraField, Is.Not.Null);
        Assert.That(tuningOffsetField, Is.Not.Null);
        Assert.That(resolveRotationMethod, Is.Not.Null);

        movementCameraField.SetValue(controller, camera);
        tuningOffsetField.SetValue(controller, Vector3.zero);

        Quaternion actual = (Quaternion)resolveRotationMethod.Invoke(controller, null);
        Quaternion formerCameraFacing = Quaternion.LookRotation(
            -camera.transform.forward,
            camera.transform.up);
        Quaternion expected =
            Quaternion.AngleAxis(180f, camera.transform.up) * formerCameraFacing;

        Assert.That(Quaternion.Angle(actual, expected), Is.LessThan(0.001f));
        Assert.That(
            Vector3.Angle(actual * Vector3.forward, camera.transform.forward),
            Is.LessThan(0.001f));
    }

    [Test]
    public void FullSalvoVisualTurn_UsesPointThreeSecondSmoothTimedInterpolation()
    {
        Type controllerType = Type.GetType("PlayerOrbitController, Assembly-CSharp");
        Assert.That(controllerType, Is.Not.Null);

        playerObject = new GameObject("FullSalvoTurnTimingTestPlayer");
        Component controller = playerObject.AddComponent(controllerType);
        FieldInfo durationField = controllerType.GetField(
            "fullSalvoVisualTurnDuration",
            InstanceFlags);
        MethodInfo evaluateRotationMethod = controllerType.GetMethod(
            "EvaluateFullSalvoVisualTurnRotation",
            StaticFlags);
        Assert.That(durationField, Is.Not.Null);
        Assert.That(evaluateRotationMethod, Is.Not.Null);

        float duration = (float)durationField.GetValue(controller);
        Assert.That(duration, Is.EqualTo(0.3f).Within(0.0001f));

        Quaternion start = Quaternion.identity;
        Quaternion target = Quaternion.AngleAxis(90f, Vector3.up);
        Quaternion atStart = EvaluateRotation(
            evaluateRotationMethod,
            start,
            target,
            0f,
            duration);
        Quaternion atQuarter = EvaluateRotation(
            evaluateRotationMethod,
            start,
            target,
            duration * 0.25f,
            duration);
        Quaternion atMidpoint = EvaluateRotation(
            evaluateRotationMethod,
            start,
            target,
            duration * 0.5f,
            duration);
        Quaternion atEnd = EvaluateRotation(
            evaluateRotationMethod,
            start,
            target,
            duration,
            duration);

        Assert.That(Quaternion.Angle(atStart, start), Is.LessThan(0.001f));
        Assert.That(Quaternion.Angle(start, atQuarter), Is.LessThan(22.5f));
        Assert.That(Quaternion.Angle(start, atMidpoint), Is.EqualTo(45f).Within(0.01f));
        Assert.That(Quaternion.Angle(atEnd, target), Is.LessThan(0.001f));
    }

    [Test]
    public void FullSalvoVisualReturn_UsesPointThreeSecondSmoothTimedInterpolation()
    {
        Type controllerType = Type.GetType("PlayerOrbitController, Assembly-CSharp");
        Assert.That(controllerType, Is.Not.Null);

        playerObject = new GameObject("FullSalvoReturnTimingTestPlayer");
        Component controller = playerObject.AddComponent(controllerType);
        FieldInfo durationField = controllerType.GetField(
            "fullSalvoVisualReturnDuration",
            InstanceFlags);
        MethodInfo evaluateRotationMethod = controllerType.GetMethod(
            "EvaluateFullSalvoVisualReturnRotation",
            StaticFlags);
        Assert.That(durationField, Is.Not.Null);
        Assert.That(evaluateRotationMethod, Is.Not.Null);

        float duration = (float)durationField.GetValue(controller);
        Assert.That(duration, Is.EqualTo(0.3f).Within(0.0001f));

        Quaternion fullSalvoPose = Quaternion.AngleAxis(180f, Vector3.up);
        Quaternion normalSidePose = Quaternion.AngleAxis(90f, Vector3.up);
        Quaternion atStart = EvaluateRotation(
            evaluateRotationMethod,
            fullSalvoPose,
            normalSidePose,
            0f,
            duration);
        Quaternion atQuarter = EvaluateRotation(
            evaluateRotationMethod,
            fullSalvoPose,
            normalSidePose,
            duration * 0.25f,
            duration);
        Quaternion atMidpoint = EvaluateRotation(
            evaluateRotationMethod,
            fullSalvoPose,
            normalSidePose,
            duration * 0.5f,
            duration);
        Quaternion atEnd = EvaluateRotation(
            evaluateRotationMethod,
            fullSalvoPose,
            normalSidePose,
            duration,
            duration);

        Assert.That(Quaternion.Angle(atStart, fullSalvoPose), Is.LessThan(0.001f));
        Assert.That(Quaternion.Angle(fullSalvoPose, atQuarter), Is.LessThan(22.5f));
        Assert.That(
            Quaternion.Angle(fullSalvoPose, atMidpoint),
            Is.EqualTo(45f).Within(0.01f));
        Assert.That(Quaternion.Angle(atEnd, normalSidePose), Is.LessThan(0.001f));
    }

    [Test]
    public void PlayerOverlayCentering_IgnoresDynamicVfxRenderers()
    {
        Type overlayType = Type.GetType("PlayerVisualOverlayRenderer, Assembly-CSharp");
        Assert.That(overlayType, Is.Not.Null);

        MethodInfo canDefineVisualCenter = overlayType.GetMethod(
            "CanDefineVisualCenter",
            StaticFlags);
        Assert.That(canDefineVisualCenter, Is.Not.Null);

        playerObject = new GameObject("PlayerOverlayCenteringRendererTest");
        MeshRenderer helicopterMesh = playerObject.AddComponent<MeshRenderer>();

        GameObject particleObject = new("SidewinderExhaustParticle");
        particleObject.transform.SetParent(playerObject.transform, false);
        particleObject.AddComponent<ParticleSystem>();
        ParticleSystemRenderer particleRenderer =
            particleObject.GetComponent<ParticleSystemRenderer>();

        GameObject trailObject = new("SidewinderExhaustTrail");
        trailObject.transform.SetParent(playerObject.transform, false);
        TrailRenderer trailRenderer = trailObject.AddComponent<TrailRenderer>();

        GameObject lineObject = new("RuntimeSpeedLine");
        lineObject.transform.SetParent(playerObject.transform, false);
        LineRenderer lineRenderer = lineObject.AddComponent<LineRenderer>();

        Assert.That(
            (bool)canDefineVisualCenter.Invoke(null, new object[] { helicopterMesh }),
            Is.True);
        Assert.That(
            (bool)canDefineVisualCenter.Invoke(null, new object[] { particleRenderer }),
            Is.False);
        Assert.That(
            (bool)canDefineVisualCenter.Invoke(null, new object[] { trailRenderer }),
            Is.False);
        Assert.That(
            (bool)canDefineVisualCenter.Invoke(null, new object[] { lineRenderer }),
            Is.False);
    }

    [Test]
    public void MovementProjection_UsesStableOverlayCameraOnlyDuringWorldShake()
    {
        Type controllerType = Type.GetType("PlayerOrbitController, Assembly-CSharp");
        Type overlayType = Type.GetType("PlayerVisualOverlayRenderer, Assembly-CSharp");
        Assert.That(controllerType, Is.Not.Null);
        Assert.That(overlayType, Is.Not.Null);

        cameraObject = new GameObject("MovementProjectionBaseCamera");
        Camera baseCamera = cameraObject.AddComponent<Camera>();
        playerObject = new GameObject("MovementProjectionTestPlayer");
        Component controller = playerObject.AddComponent(controllerType);
        Component overlay = playerObject.AddComponent(overlayType);
        GameObject stableCameraObject = new("MovementProjectionStableOverlayCamera");
        stableCameraObject.transform.SetParent(playerObject.transform, false);
        Camera stableCamera = stableCameraObject.AddComponent<Camera>();

        FieldInfo movementCameraField = controllerType.GetField(
            "movementCamera",
            InstanceFlags);
        FieldInfo playerOverlayField = controllerType.GetField(
            "playerVisualOverlayRenderer",
            InstanceFlags);
        MethodInfo resolveProjectionCamera = controllerType.GetMethod(
            "ResolveMovementProjectionCamera",
            InstanceFlags);
        FieldInfo overlayCameraField = overlayType.GetField(
            "overlayCamera",
            InstanceFlags);
        FieldInfo stableProjectionActiveField = overlayType.GetField(
            "stableOverlayProjectionActive",
            InstanceFlags);
        Assert.That(movementCameraField, Is.Not.Null);
        Assert.That(playerOverlayField, Is.Not.Null);
        Assert.That(resolveProjectionCamera, Is.Not.Null);
        Assert.That(overlayCameraField, Is.Not.Null);
        Assert.That(stableProjectionActiveField, Is.Not.Null);

        movementCameraField.SetValue(controller, baseCamera);
        playerOverlayField.SetValue(controller, overlay);
        overlayCameraField.SetValue(overlay, stableCamera);

        stableProjectionActiveField.SetValue(overlay, false);
        Assert.That(resolveProjectionCamera.Invoke(controller, null), Is.SameAs(baseCamera));

        stableProjectionActiveField.SetValue(overlay, true);
        Assert.That(resolveProjectionCamera.Invoke(controller, null), Is.SameAs(stableCamera));
    }

    [Test]
    public void PlayerOverlayCentering_ExcludesRegisteredDetachableAttachmentHierarchy()
    {
        Type overlayType = Type.GetType("PlayerVisualOverlayRenderer, Assembly-CSharp");
        Assert.That(overlayType, Is.Not.Null);

        playerObject = new GameObject("DetachableAttachmentCenteringTest");
        Component overlay = playerObject.AddComponent(overlayType);
        GameObject sidewinder = new("Sidewinder");
        sidewinder.transform.SetParent(playerObject.transform, false);
        GameObject sidewinderMesh = new("SidewinderMesh");
        sidewinderMesh.transform.SetParent(sidewinder.transform, false);

        MethodInfo registerIgnoredRoot = overlayType.GetMethod(
            "RegisterCenteringIgnoredRoot",
            BindingFlags.Instance | BindingFlags.Public);
        MethodInfo isUnderIgnoredRoot = overlayType.GetMethod(
            "IsUnderCenteringIgnoredRoot",
            InstanceFlags);
        Assert.That(registerIgnoredRoot, Is.Not.Null);
        Assert.That(isUnderIgnoredRoot, Is.Not.Null);

        Assert.That(
            (bool)isUnderIgnoredRoot.Invoke(
                overlay,
                new object[] { sidewinderMesh.transform }),
            Is.False);

        registerIgnoredRoot.Invoke(overlay, new object[] { sidewinder.transform });

        Assert.That(
            (bool)isUnderIgnoredRoot.Invoke(
                overlay,
                new object[] { sidewinderMesh.transform }),
            Is.True);
    }

    [Test]
    public void PlayerOverlayCameraPose_RemainsFixedWhileWorldCameraShakes()
    {
        Type overlayType = Type.GetType("PlayerVisualOverlayRenderer, Assembly-CSharp");
        Assert.That(overlayType, Is.Not.Null);

        cameraObject = new GameObject("StablePoseBaseCamera");
        Camera baseCamera = cameraObject.AddComponent<Camera>();
        baseCamera.transform.SetPositionAndRotation(
            new Vector3(4f, 5f, 6f),
            Quaternion.Euler(10f, 20f, 30f));
        playerObject = new GameObject("StablePoseOverlayOwner");
        Component overlay = playerObject.AddComponent(overlayType);
        GameObject overlayCameraObject = new("StablePoseOverlayCamera");
        overlayCameraObject.transform.SetParent(playerObject.transform, false);
        Camera overlayCamera = overlayCameraObject.AddComponent<Camera>();

        Vector3 stablePosition = new(1f, 2f, 3f);
        Quaternion stableRotation = Quaternion.Euler(2f, 4f, 6f);
        FieldInfo baseCameraField = overlayType.GetField("baseCamera", InstanceFlags);
        FieldInfo overlayCameraField = overlayType.GetField("overlayCamera", InstanceFlags);
        FieldInfo stableActiveField = overlayType.GetField(
            "stableOverlayProjectionActive",
            InstanceFlags);
        FieldInfo stablePositionField = overlayType.GetField(
            "stableOverlayCameraPosition",
            InstanceFlags);
        FieldInfo stableRotationField = overlayType.GetField(
            "stableOverlayCameraRotation",
            InstanceFlags);
        MethodInfo syncPose = overlayType.GetMethod("SyncOverlayCameraPose", InstanceFlags);
        Assert.That(baseCameraField, Is.Not.Null);
        Assert.That(overlayCameraField, Is.Not.Null);
        Assert.That(stableActiveField, Is.Not.Null);
        Assert.That(stablePositionField, Is.Not.Null);
        Assert.That(stableRotationField, Is.Not.Null);
        Assert.That(syncPose, Is.Not.Null);

        baseCameraField.SetValue(overlay, baseCamera);
        overlayCameraField.SetValue(overlay, overlayCamera);
        stablePositionField.SetValue(overlay, stablePosition);
        stableRotationField.SetValue(overlay, stableRotation);
        stableActiveField.SetValue(overlay, true);
        syncPose.Invoke(overlay, null);

        Assert.That(Vector3.Distance(overlayCamera.transform.position, stablePosition), Is.LessThan(0.0001f));
        Assert.That(Quaternion.Angle(overlayCamera.transform.rotation, stableRotation), Is.LessThan(0.001f));

        stableActiveField.SetValue(overlay, false);
        syncPose.Invoke(overlay, null);

        Assert.That(Vector3.Distance(overlayCamera.transform.position, baseCamera.transform.position), Is.LessThan(0.0001f));
        Assert.That(Quaternion.Angle(overlayCamera.transform.rotation, baseCamera.transform.rotation), Is.LessThan(0.001f));
    }

    [Test]
    public void FullSalvoCameraShake_UsesTemporaryLargeVisibilityTestMultiplier()
    {
        Type feedbackType = Type.GetType("LockOnCombatFeedback, Assembly-CSharp");
        Assert.That(feedbackType, Is.Not.Null);

        FieldInfo amplitudeField = feedbackType.GetField(
            "FullSalvoCameraShakeAmplitude",
            BindingFlags.Static | BindingFlags.NonPublic);
        FieldInfo multiplierField = feedbackType.GetField(
            "TemporaryCameraShakeVisibilityTestMultiplier",
            BindingFlags.Static | BindingFlags.NonPublic);
        Assert.That(amplitudeField, Is.Not.Null);
        Assert.That(multiplierField, Is.Not.Null);

        float amplitude = (float)amplitudeField.GetRawConstantValue();
        float visibilityTestMultiplier = (float)multiplierField.GetRawConstantValue();
        Assert.That(amplitude, Is.EqualTo(0.0075f).Within(0.00001f));
        Assert.That(visibilityTestMultiplier, Is.EqualTo(8f));

        const float fullHdWidth = 1920f;
        const float fullHdHeight = 1080f;
        float effectiveAmplitude = amplitude * visibilityTestMultiplier;
        float maximumHorizontalPixels = effectiveAmplitude * fullHdWidth * 0.5f;
        float maximumVerticalPixels = effectiveAmplitude * fullHdHeight * 0.5f;

        Assert.That(effectiveAmplitude, Is.EqualTo(0.06f).Within(0.00001f));
        Assert.That(maximumHorizontalPixels, Is.GreaterThanOrEqualTo(57f));
        Assert.That(maximumVerticalPixels, Is.GreaterThanOrEqualTo(32f));
        Assert.That(maximumHorizontalPixels, Is.LessThan(60f));
    }

    private static Quaternion EvaluateRotation(
        MethodInfo method,
        Quaternion start,
        Quaternion target,
        float elapsed,
        float duration)
    {
        return (Quaternion)method.Invoke(
            null,
            new object[] { start, target, elapsed, duration });
    }
}
