using System;
using System.Reflection;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public class EnvironmentBackgroundLightingTests
{
    private const BindingFlags InstanceFlags = BindingFlags.Instance | BindingFlags.NonPublic;

    private Scene environmentScene;
    private Scene otherScene;
    private Light originalSun;
    private Component controller;
    private FieldInfo lightField;
    private MethodInfo assignReferences;

    [SetUp]
    public void SetUp()
    {
        originalSun = RenderSettings.sun;
        // Preview scenes also work when the test runner owns an unsaved, untitled scene.
        environmentScene = EditorSceneManager.NewPreviewScene();
        otherScene = EditorSceneManager.NewPreviewScene();

        Type controllerType = Type.GetType("EnvironmentBackgroundController, Assembly-CSharp");
        Assert.That(controllerType, Is.Not.Null);
        GameObject root = new("EnvironmentLightingTest");
        root.SetActive(false);
        SceneManager.MoveGameObjectToScene(root, environmentScene);
        controller = root.AddComponent(controllerType);
        lightField = controllerType.GetField("directionalLight", InstanceFlags);
        assignReferences = controllerType.GetMethod("AutoAssignReferences", InstanceFlags);
        Assert.That(lightField, Is.Not.Null);
        Assert.That(assignReferences, Is.Not.Null);
    }

    [TearDown]
    public void TearDown()
    {
        RenderSettings.sun = originalSun;
        if (environmentScene.IsValid())
        {
            EditorSceneManager.ClosePreviewScene(environmentScene);
        }
        if (otherScene.IsValid())
        {
            EditorSceneManager.ClosePreviewScene(otherScene);
        }
    }

    [TestCase(false)]
    [TestCase(true)]
    public void AutoAssign_PrefersShadowCastingEnvironmentLightOverShadowlessSun(bool fillCreatedFirst)
    {
        Light fill = null;
        if (fillCreatedFirst)
        {
            fill = CreateLight(environmentScene, LightShadows.None, 10f);
        }
        Light environmentLight = CreateLight(environmentScene, LightShadows.Hard, 1f);
        fill ??= CreateLight(environmentScene, LightShadows.None, 10f);
        RenderSettings.sun = fill;

        assignReferences.Invoke(controller, null);

        Assert.That(lightField.GetValue(controller), Is.SameAs(environmentLight));
    }

    [Test]
    public void AutoAssign_ReplacesLightFromAnotherScene()
    {
        Light foreignLight = CreateLight(otherScene, LightShadows.Soft, 20f);
        Light environmentLight = CreateLight(environmentScene, LightShadows.Hard, 1f);
        RenderSettings.sun = foreignLight;
        lightField.SetValue(controller, foreignLight);

        assignReferences.Invoke(controller, null);

        Assert.That(lightField.GetValue(controller), Is.SameAs(environmentLight));
    }

    [Test]
    public void AutoAssign_PreservesExplicitSameSceneAssignment()
    {
        Light assigned = CreateLight(environmentScene, LightShadows.None, 1f);
        CreateLight(environmentScene, LightShadows.Hard, 10f);
        lightField.SetValue(controller, assigned);

        assignReferences.Invoke(controller, null);

        Assert.That(lightField.GetValue(controller), Is.SameAs(assigned));
    }

    [Test]
    public void AutoAssign_ExcludesInactiveDisabledAndNonDirectionalLights()
    {
        Light inactive = CreateLight(environmentScene, LightShadows.Hard, 10f);
        inactive.gameObject.SetActive(false);
        Light disabled = CreateLight(environmentScene, LightShadows.Hard, 10f);
        disabled.enabled = false;
        Light point = CreateLight(environmentScene, LightShadows.Hard, 10f);
        point.type = LightType.Point;
        Light fallback = CreateLight(environmentScene, LightShadows.None, 1f);

        assignReferences.Invoke(controller, null);

        Assert.That(lightField.GetValue(controller), Is.SameAs(fallback));
    }

    [Test]
    public void AutoAssign_RespectsSameSceneSunAmongShadowCastingLights()
    {
        CreateLight(environmentScene, LightShadows.Hard, 10f);
        Light sun = CreateLight(environmentScene, LightShadows.Soft, 1f);
        RenderSettings.sun = sun;

        assignReferences.Invoke(controller, null);

        Assert.That(lightField.GetValue(controller), Is.SameAs(sun));
    }

    [Test]
    public void AutoAssign_DoesNotUseForeignLightWhenSceneHasNoDirectionalLight()
    {
        RenderSettings.sun = CreateLight(otherScene, LightShadows.Soft, 10f);

        assignReferences.Invoke(controller, null);

        Assert.That(lightField.GetValue(controller), Is.Null);
    }

    private static Light CreateLight(Scene scene, LightShadows shadows, float intensity)
    {
        GameObject lightObject = new("LightingTestLight");
        SceneManager.MoveGameObjectToScene(lightObject, scene);
        Light light = lightObject.AddComponent<Light>();
        light.type = LightType.Directional;
        light.shadows = shadows;
        light.intensity = intensity;
        return light;
    }
}
