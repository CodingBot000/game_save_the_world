using System;
using System.Collections;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

public sealed class StageBossLoadingPlayModeTests
{
    private const string BattleSceneName = "BattleArena";

    [UnityTest]
    public IEnumerator Stage1Selection_LoadsOnlyKaiju()
    {
        SetStageSelection("stage_01_tokyo", "Tokyo");
        yield return SceneManager.LoadSceneAsync(BattleSceneName, LoadSceneMode.Single);
        yield return null;
        yield return null;

        Component[] bosses = FindSceneComponents("BossController, Assembly-CSharp");
        Assert.That(bosses, Has.Length.EqualTo(1));
        Assert.That(bosses[0].name, Is.EqualTo("Kaiju"));
    }

    [UnityTest]
    public IEnumerator Stage2Selection_LoadsOnlySaratanAndPlaysIdle()
    {
        SetStageSelection("stage_02_seoul", "Seoul");
        yield return SceneManager.LoadSceneAsync(BattleSceneName, LoadSceneMode.Single);
        yield return null;
        yield return null;

        Component[] bosses = FindSceneComponents("BossController, Assembly-CSharp");
        Assert.That(bosses, Has.Length.EqualTo(1));
        Component boss = bosses[0];
        Assert.That(boss.name, Is.EqualTo("Saratan"));

        Animator animator = boss.GetComponentInChildren<Animator>(true);
        Assert.That(animator, Is.Not.Null);
        Assert.That(animator.runtimeAnimatorController, Is.Not.Null);

        Component attack = boss.GetComponent(RequireType("BossAttackController, Assembly-CSharp"));
        Assert.That(attack, Is.Not.Null);
        Assert.That(((Behaviour)attack).enabled, Is.True);

        Type patternType = RequireType("BossBulletPatternController, Assembly-CSharp");
        Component patterns = boss.GetComponent(patternType);
        Assert.That(patterns, Is.Not.Null);
        Assert.That(((Behaviour)patterns).enabled, Is.True);

        yield return new WaitForSeconds(0.25f);
        AnimatorStateInfo state = animator.GetCurrentAnimatorStateInfo(0);
        Assert.That(state.IsName("BasicIdle"), Is.True);
        Assert.That(state.normalizedTime, Is.GreaterThan(0f));

        Type patternEnumType = RequireType("BossBulletPatternType, Assembly-CSharp");
        object debrisPattern = Enum.Parse(patternEnumType, "DebrisFragmentScatter");
        MethodInfo runPattern = patternType.GetMethod("TryRunPatternForDebug", BindingFlags.Public | BindingFlags.Instance);
        Assert.That(runPattern, Is.Not.Null);
        Assert.That((bool)runPattern.Invoke(patterns, new[] { debrisPattern }), Is.True);
        yield return null;

        PropertyInfo isPatternRunning = patternType.GetProperty("IsPatternRunning", BindingFlags.Public | BindingFlags.Instance);
        Assert.That(isPatternRunning, Is.Not.Null);
        Assert.That((bool)isPatternRunning.GetValue(patterns), Is.True);
    }

    private static void SetStageSelection(string stageId, string stageName)
    {
        Type stateType = RequireType("StageSelectionState, Assembly-CSharp");
        MethodInfo ensureInitialized = stateType.GetMethod(
            "EnsureInitialized",
            BindingFlags.Public | BindingFlags.Static);
        Assert.That(ensureInitialized, Is.Not.Null);
        object state = ensureInitialized.Invoke(null, null);
        MethodInfo setSelection = stateType.GetMethod(
            "SetSelection",
            BindingFlags.Public | BindingFlags.Instance,
            null,
            new[] { typeof(string), typeof(string), typeof(int) },
            null);
        Assert.That(setSelection, Is.Not.Null);
        setSelection.Invoke(state, new object[] { stageId, stageName, 0 });
    }

    private static Component[] FindSceneComponents(string assemblyQualifiedTypeName)
    {
        Type type = RequireType(assemblyQualifiedTypeName);
        Scene activeScene = SceneManager.GetActiveScene();
        return Resources.FindObjectsOfTypeAll(type)
            .OfType<Component>()
            .Where(component => component.gameObject.scene == activeScene)
            .ToArray();
    }

    private static Type RequireType(string assemblyQualifiedName)
    {
        Type type = Type.GetType(assemblyQualifiedName);
        Assert.That(type, Is.Not.Null, $"Could not resolve {assemblyQualifiedName}");
        return type;
    }
}
