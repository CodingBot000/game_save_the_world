using System;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

public sealed class StageContentIntegrationTests
{
    private const string ContentRoot = "Assets/_Project/Content";
    private const string Stage1Root = ContentRoot + "/Bosses/Stage01_Kaiju";
    private const string Stage2Root = ContentRoot + "/Bosses/Stage02_Saratan";
    private const string CatalogPath = ContentRoot + "/Stages/StageCatalog.asset";
    private const string Stage1PrefabPath = Stage1Root + "/Prefabs/Boss_Stage01_Kaiju.prefab";
    private const string Stage2PrefabPath = Stage2Root + "/Runtime/Prefabs/Boss_Stage02_Saratan.prefab";

    private static Type BossControllerType => RequireType("BossController, Assembly-CSharp");
    private static Type BossAttackControllerType => RequireType("BossAttackController, Assembly-CSharp");

    [TestCase("stage_01_tokyo", "boss_stage01_kaiju", false)]
    [TestCase("stage_02_seoul", "boss_stage02_saratan", false)]
    [TestCase("", "boss_stage01_kaiju", true)]
    [TestCase("unknown_stage", "boss_stage01_kaiju", true)]
    public void Catalog_ResolvesStageAndFallback(
        string stageId,
        string expectedBossId,
        bool expectedFallback)
    {
        ScriptableObject catalog = AssetDatabase.LoadAssetAtPath<ScriptableObject>(CatalogPath);
        Assert.That(catalog, Is.Not.Null);

        MethodInfo resolve = catalog.GetType().GetMethod("Resolve", BindingFlags.Public | BindingFlags.Instance);
        Assert.That(resolve, Is.Not.Null);
        object[] arguments = { stageId, false };
        object stage = resolve.Invoke(catalog, arguments);

        Assert.That(stage, Is.Not.Null);
        Assert.That((bool)arguments[1], Is.EqualTo(expectedFallback));
        object boss = stage.GetType().GetProperty("BossDefinition")?.GetValue(stage);
        Assert.That(boss, Is.Not.Null);
        string bossId = boss.GetType().GetProperty("BossId")?.GetValue(boss) as string;
        Assert.That(bossId, Is.EqualTo(expectedBossId));
    }

    [Test]
    public void BossPrefabs_AreDistinctAndContainOneBossController()
    {
        GameObject stage1 = AssetDatabase.LoadAssetAtPath<GameObject>(Stage1PrefabPath);
        GameObject stage2 = AssetDatabase.LoadAssetAtPath<GameObject>(Stage2PrefabPath);
        Assert.That(stage1, Is.Not.Null);
        Assert.That(stage2, Is.Not.Null);
        Assert.That(AssetDatabase.AssetPathToGUID(Stage1PrefabPath),
            Is.Not.EqualTo(AssetDatabase.AssetPathToGUID(Stage2PrefabPath)));
        Assert.That(stage1.GetComponentsInChildren(BossControllerType, true), Has.Length.EqualTo(1));
        Assert.That(stage2.GetComponentsInChildren(BossControllerType, true), Has.Length.EqualTo(1));
    }

    [Test]
    public void Stage2Prefab_UsesIdleAnimatorAndDisabledAttackController()
    {
        GameObject stage2 = AssetDatabase.LoadAssetAtPath<GameObject>(Stage2PrefabPath);
        Assert.That(stage2, Is.Not.Null);
        Component attack = stage2.GetComponent(BossAttackControllerType);
        Assert.That(attack, Is.Not.Null);
        Assert.That(((Behaviour)attack).enabled, Is.False);

        Animator animator = stage2.GetComponentInChildren<Animator>(true);
        Assert.That(animator, Is.Not.Null);
        Assert.That(animator.runtimeAnimatorController, Is.Not.Null);
        AnimationClip idle = animator.runtimeAnimatorController.animationClips
            .Single(clip => clip.name.Contains("BasicIdle", StringComparison.Ordinal));
        Assert.That(AnimationUtility.GetAnimationClipSettings(idle).loopTime, Is.True);
    }

    [Test]
    public void Stage2Content_DoesNotReferenceStage1OrImportedSourceAssets()
    {
        string[] dependencies = AssetDatabase.GetDependencies(Stage2Root, true);
        string[] forbidden = dependencies.Where(path =>
                path.StartsWith(Stage1Root, StringComparison.Ordinal) ||
                path.StartsWith("Assets/Animation/Invader/", StringComparison.Ordinal) ||
                path.StartsWith("Assets/Invader/", StringComparison.Ordinal) ||
                path.StartsWith("Assets/Materials/Invader/", StringComparison.Ordinal) ||
                path.StartsWith("Assets/Textures/Invader/", StringComparison.Ordinal) ||
                path.Equals("Assets/Prefabs/Characters/BossPlaceholder.prefab", StringComparison.Ordinal))
            .ToArray();

        Assert.That(forbidden, Is.Empty, string.Join("\n", forbidden));
    }

    [Test]
    public void Stage2Materials_UseSupportedShaderAndOwnTextures()
    {
        string[] materialGuids = AssetDatabase.FindAssets("t:Material", new[] { Stage2Root + "/Art" });
        Assert.That(materialGuids, Is.Not.Empty);

        foreach (string guid in materialGuids)
        {
            string materialPath = AssetDatabase.GUIDToAssetPath(guid);
            Material material = AssetDatabase.LoadAssetAtPath<Material>(materialPath);
            Assert.That(material.shader, Is.Not.Null, materialPath);
            Assert.That(material.shader.isSupported, Is.True, materialPath);
            Assert.That(material.shader.name, Is.EqualTo("Universal Render Pipeline/Lit"), materialPath);

            foreach (string propertyName in material.GetTexturePropertyNames())
            {
                Texture texture = material.GetTexture(propertyName);
                if (texture == null)
                {
                    continue;
                }

                string texturePath = AssetDatabase.GetAssetPath(texture);
                Assert.That(texturePath, Does.StartWith(Stage2Root + "/Art/"),
                    $"{materialPath} property {propertyName} references {texturePath}");
            }
        }
    }

    [Test]
    public void StageDefinitions_UseSeparateEnvironmentPrefabs()
    {
        string stage1Environment = ContentRoot + "/Stages/Stage01/Environment/Stage01Environment.prefab";
        string stage2Environment = ContentRoot + "/Stages/Stage02/Environment/Stage02Environment.prefab";
        Assert.That(AssetDatabase.LoadAssetAtPath<GameObject>(stage1Environment), Is.Not.Null);
        Assert.That(AssetDatabase.LoadAssetAtPath<GameObject>(stage2Environment), Is.Not.Null);
        Assert.That(AssetDatabase.AssetPathToGUID(stage1Environment),
            Is.Not.EqualTo(AssetDatabase.AssetPathToGUID(stage2Environment)));
    }

    private static Type RequireType(string assemblyQualifiedName)
    {
        Type type = Type.GetType(assemblyQualifiedName);
        Assert.That(type, Is.Not.Null, $"Could not resolve {assemblyQualifiedName}");
        return type;
    }
}
