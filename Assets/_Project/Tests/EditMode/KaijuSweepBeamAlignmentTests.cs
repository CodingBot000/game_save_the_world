using System;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

public class KaijuSweepBeamAlignmentTests
{
    private const BindingFlags Flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static;
    private static Type DriverType => Type.GetType("KaijuBossAnimationDriver, Assembly-CSharp", true);
    private static Type PatternType => Type.GetType("BossBulletPatternController, Assembly-CSharp", true);

    [TestCase(0f, 0f)]
    [TestCase(0.15f, 0.1f)]
    [TestCase(0.3f, 0.2f)]
    [TestCase(0.4f, 0.9f)]
    [TestCase(0.5f, 1f)]
    [TestCase(3f, 1f)]
    public void SweepProgress_PreservesSlowFastTiming(float time, float expected)
    {
        Assert.That(Progress(time, 0.3f, 0.2f), Is.EqualTo(expected).Within(0.00001f));
    }

    [TestCase(30)]
    [TestCase(60)]
    [TestCase(120)]
    public void SweepProgress_IsMonotoneAtEachFrameRateAndHandlesMinimumDuration(int fps)
    {
        float previous = 0f;
        for (int frame = 0; frame <= fps; frame++)
        {
            float actual = Progress((float)frame / fps, 0.3f, 0.2f);
            Assert.That(actual, Is.InRange(previous, 1f));
            previous = actual;
        }
        Assert.That(Progress(0.05f, 0f, 0f), Is.EqualTo(0.2f).Within(0.00001f));
        Assert.That(Progress(0.1f, 0f, 0f), Is.EqualTo(1f).Within(0.00001f));
    }

    [TestCase(0f)]
    [TestCase(90f)]
    [TestCase(-90f)]
    [TestCase(180f)]
    public void ClipSelection_UsesAngularPathRatherThanCameraSideNames(float yaw)
    {
        Quaternion rotation = Quaternion.Euler(0f, yaw, 0f);
        Vector3 left = rotation * new Vector3(-0.4f, 0.5f, -1f).normalized;
        Vector3 right = rotation * new Vector3(0.4f, 0.5f, -1f).normalized;
        MethodInfo select = DriverType.GetMethod("SelectSweepClipLeftToRight", Flags);
        Assert.That((bool)select.Invoke(null, new object[] { left, right }), Is.False);
        Assert.That((bool)select.Invoke(null, new object[] { right, left }), Is.True);
        Vector3 fallbackStart = Quaternion.Euler(0f, -46f, 0f) * Vector3.forward;
        Vector3 fallbackEnd = Quaternion.Euler(0f, 46f, 0f) * Vector3.forward;
        Assert.That((bool)select.Invoke(null, new object[] { fallbackStart, fallbackEnd }), Is.True);
    }

    [TestCase(true, 20)]
    [TestCase(true, 44)]
    [TestCase(true, 68)]
    [TestCase(false, 20)]
    [TestCase(false, 44)]
    [TestCase(false, 68)]
    public void ActualRig_AlignsWithoutMovingRootOrLegs_AndRestoresWithoutAccumulation(bool leftToRight, int frame)
    {
        GameObject root = new GameObject("SweepAlignmentTest");
        try
        {
            Component boss = root.AddComponent(Type.GetType("BossController, Assembly-CSharp", true));
            Invoke(boss, "SetCurrentHealthForDebug", 2000f);
            GameObject model = UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Invader/Kaiju_001.fbx"), root.transform);
            model.transform.localRotation = Quaternion.identity;
            Animator animator = model.GetComponent<Animator>();
            if (animator == null) animator = model.AddComponent<Animator>();
            animator.runtimeAnimatorController = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>("Assets/Animation/Invader/KaijuCombat.controller");
            animator.fireEvents = false;
            animator.Rebind();
            animator.Update(0f);
            Component driver = model.AddComponent(DriverType);
            Transform head = model.transform.Find("Root/Pelvis/Spine 01/Spine 02/Neck_01/Neck_02/Head");
            Assert.That(head, Is.Not.Null);
            Transform mouth = new GameObject("KaijuMouthSocket").transform;
            mouth.SetParent(head, false);
            mouth.localPosition = new Vector3(-0.24138662f, 1.1754575f, -0.0063846977f);
            mouth.localRotation = Quaternion.FromToRotation(Vector3.forward, mouth.localPosition.normalized);
            Invoke(driver, "Configure", boss, mouth, null);
            Transform[] bones = { head.parent.parent, head.parent, head };
            Transform foot = model.transform.Find("Root/Pelvis/Thigh L/Calf L/Cannon L/Foot L");
            AnimationClip clip = AssetDatabase.LoadAssetAtPath<AnimationClip>("Assets/Animation/Invader/Clips/Kaiju_Attack_Beam" + (leftToRight ? "LeftToR" : "RightToL") + ".anim");
            foreach (float yaw in new[] { -90f, -45f, 0f, 45f, 90f })
            {
                Invoke(driver, "CancelAction");
                root.transform.rotation = Quaternion.Euler(0f, yaw, 0f);
                int ticket = (int)Invoke(driver, "BeginBeam", leftToRight, 0.8f, 0.5f);
                Assert.That((bool)Invoke(driver, "TryBeginSweepAim", ticket), Is.True);
                clip.SampleAnimation(model, frame / 30f);
                Quaternion[] basePoses = Array.ConvertAll(bones, t => t.localRotation);
                Quaternion rootRotation = root.transform.rotation;
                Vector3 footPosition = foot.position;
                Vector3 target = new Vector3(0.2f, 0.45f, 1f).normalized;
                Assert.That((bool)Invoke(driver, "TryApplySweepAim", ticket, Vector3.zero, 1f), Is.False);
                Assert.That((bool)Invoke(driver, "TryApplySweepAim", ticket, target, float.NaN), Is.False);
                Assert.That((bool)Invoke(driver, "TryApplySweepAim", ticket, target, 1f), Is.True);
                Assert.That(Vector3.Angle(mouth.forward, target), Is.LessThan(0.2f));
                Assert.That(Quaternion.Angle(bones[0].localRotation, basePoses[0]), Is.GreaterThan(0.01f));
                Quaternion corrected = head.rotation;
                Assert.That((bool)Invoke(driver, "TryApplySweepAim", ticket, target, 1f), Is.True);
                Assert.That(Quaternion.Angle(head.rotation, corrected), Is.LessThan(0.1f), "Correction must not accumulate.");
                Assert.That(Vector3.Distance(foot.position, footPosition), Is.LessThan(0.0001f));
                Assert.That(Quaternion.Angle(root.transform.rotation, rootRotation), Is.LessThan(0.01f));
                Invoke(driver, "RestoreSweepPose");
                for (int i = 0; i < bones.Length; i++)
                    Assert.That(Quaternion.Angle(bones[i].localRotation, basePoses[i]), Is.LessThan(0.1f));
                Invoke(driver, "CancelAction");
                Assert.That((bool)Invoke(driver, "TryApplySweepAim", ticket, target, 1f), Is.False, "Stale attack ticket.");
            }

            int missing = (int)Invoke(driver, "BeginBeam", true, 0.8f, 0.5f);
            Invoke(driver, "Configure", boss, null, null);
            Assert.That((bool)Invoke(driver, "TryBeginSweepAim", missing), Is.False);
        }
        finally { UnityEngine.Object.DestroyImmediate(root); }
    }

    private static float Progress(float time, float slow, float fast) =>
        (float)PatternType.GetMethod("EvaluateSweepProgress", Flags).Invoke(null, new object[] { time, slow, fast, 0.2f });

    private static object Invoke(Component target, string method, params object[] arguments) =>
        target.GetType().GetMethod(method, Flags).Invoke(target, arguments);
}
