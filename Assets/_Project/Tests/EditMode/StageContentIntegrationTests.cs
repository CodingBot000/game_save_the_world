using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

public sealed class StageContentIntegrationTests
{
    private const string ContentRoot = "Assets/_Project/Content";
    private const string KaijuRoot = ContentRoot + "/Bosses/Kaiju";
    private const string SaratanRoot = ContentRoot + "/Bosses/Saratan";
    private const string CatalogPath = ContentRoot + "/Stages/StageCatalog.asset";
    private const string KaijuPrefabPath = KaijuRoot + "/Runtime/Prefabs/Kaiju.prefab";
    private const string SaratanPrefabPath = SaratanRoot + "/Runtime/Prefabs/Saratan.prefab";

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
        GameObject kaiju = AssetDatabase.LoadAssetAtPath<GameObject>(KaijuPrefabPath);
        GameObject saratan = AssetDatabase.LoadAssetAtPath<GameObject>(SaratanPrefabPath);
        Assert.That(kaiju, Is.Not.Null);
        Assert.That(saratan, Is.Not.Null);
        Assert.That(kaiju.name, Is.EqualTo("Kaiju"));
        Assert.That(saratan.name, Is.EqualTo("Saratan"));
        Assert.That(AssetDatabase.AssetPathToGUID(KaijuPrefabPath),
            Is.Not.EqualTo(AssetDatabase.AssetPathToGUID(SaratanPrefabPath)));
        Assert.That(kaiju.GetComponentsInChildren(BossControllerType, true), Has.Length.EqualTo(1));
        Assert.That(saratan.GetComponentsInChildren(BossControllerType, true), Has.Length.EqualTo(1));
    }

    [Test]
    public void KaijuPrefab_UsesOwnedVisualAssets()
    {
        GameObject kaiju = AssetDatabase.LoadAssetAtPath<GameObject>(KaijuPrefabPath);
        Assert.That(kaiju, Is.Not.Null);

        Animator animator = kaiju.GetComponentInChildren<Animator>(true);
        Assert.That(animator, Is.Not.Null);
        Assert.That(animator.runtimeAnimatorController, Is.Not.Null);
        Assert.That(AssetDatabase.GetAssetPath(animator.runtimeAnimatorController),
            Does.StartWith(KaijuRoot + "/Runtime/Animation/"));

        foreach (AnimationClip clip in animator.runtimeAnimatorController.animationClips)
        {
            Assert.That(AssetDatabase.GetAssetPath(clip), Does.StartWith(KaijuRoot + "/Art/RigA/Animations/"),
                clip.name);
        }

        string[] dependencies = AssetDatabase.GetDependencies(KaijuPrefabPath, true);
        Assert.That(dependencies, Does.Contain(KaijuRoot + "/Art/RigA/Models/Kaiju_001.fbx"));
        Assert.That(dependencies.Any(path => path.EndsWith("_Combat.mat", StringComparison.Ordinal)), Is.False,
            string.Join("\n", dependencies));

        string[] legacyDependencies = dependencies.Where(IsLegacyKaijuPath).ToArray();
        Assert.That(legacyDependencies, Is.Empty, string.Join("\n", legacyDependencies));
    }

    [Test]
    public void KaijuPrefab_UsesUnitScaleForGameplayAndVisualHierarchy()
    {
        GameObject root = PrefabUtility.LoadPrefabContents(KaijuPrefabPath);
        try
        {
            Assert.That(root.transform.localScale, Is.EqualTo(Vector3.one), "Boss root");

            Transform visualRoot = root.transform.Find("BossVisualRoot");
            Transform visual = visualRoot != null ? visualRoot.Find("BossVisual_Kaiju") : null;
            Assert.That(visualRoot, Is.Not.Null);
            Assert.That(visual, Is.Not.Null);
            Assert.That(visualRoot.localScale, Is.EqualTo(Vector3.one), "BossVisualRoot");
            Assert.That(visual.localScale, Is.EqualTo(Vector3.one), "BossVisual_Kaiju");

            foreach (string childName in new[]
                     {
                         "AimPoint",
                         "AimPoint2",
                         "AimPoint3",
                         "AimPoint4",
                         "AimPoint5",
                         "BossHurtbox",
                         "BossFootDebrisFirePoint1",
                         "BossFootDebrisFirePoint2",
                     })
            {
                Transform child = root.transform.Find(childName);
                Assert.That(child, Is.Not.Null, childName);
                Assert.That(child.localScale, Is.EqualTo(Vector3.one), childName);
            }

            BoxCollider hurtbox = root.transform.Find("BossHurtbox")?.GetComponent<BoxCollider>();
            Assert.That(hurtbox, Is.Not.Null);
            Assert.That(hurtbox.size.x, Is.GreaterThan(0f));
            Assert.That(hurtbox.size.y, Is.GreaterThan(0f));
            Assert.That(hurtbox.size.z, Is.GreaterThan(0f));
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    [TestCase("Kaiju_001.mat")]
    [TestCase("Kaiju_Eye.mat")]
    [TestCase("Kaiju_HeadSail.mat")]
    public void KaijuMaterial_UsesSupportedUrpShaderAndOwnTexture(string fileName)
    {
        string materialPath = KaijuRoot + "/Art/RigA/Materials/" + fileName;
        Material material = AssetDatabase.LoadAssetAtPath<Material>(materialPath);
        Assert.That(material, Is.Not.Null, materialPath);
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
            Assert.That(texturePath, Does.StartWith(KaijuRoot + "/Art/RigA/Textures/"),
                $"{materialPath} property {propertyName} references {texturePath}");
        }
    }

    [Test]
    public void LegacyKaijuAssetFolders_AreEmptyOrRemoved()
    {
        string[] legacyRoots =
        {
            "Assets/Animation/Invader",
            "Assets/Invader",
            "Assets/Materials/Invader",
            "Assets/Textures/Invader",
        };

        foreach (string root in legacyRoots)
        {
            if (!AssetDatabase.IsValidFolder(root))
            {
                continue;
            }

            Assert.That(AssetDatabase.FindAssets(string.Empty, new[] { root }), Is.Empty, root);
        }
    }

    [Test]
    public void SaratanPrefab_UsesOwnCombatAnimatorAndEnabledTemporaryAttack()
    {
        GameObject kaiju = AssetDatabase.LoadAssetAtPath<GameObject>(KaijuPrefabPath);
        GameObject saratan = AssetDatabase.LoadAssetAtPath<GameObject>(SaratanPrefabPath);
        Assert.That(kaiju, Is.Not.Null);
        Assert.That(saratan, Is.Not.Null);
        Component attack = saratan.GetComponent(BossAttackControllerType);
        Assert.That(attack, Is.Not.Null);
        Assert.That(((Behaviour)attack).enabled, Is.True);

        Type patternType = RequireType("BossBulletPatternController, Assembly-CSharp");
        Component kaijuPatterns = kaiju.GetComponent(patternType);
        Component saratanPatterns = saratan.GetComponent(patternType);
        Assert.That(kaijuPatterns, Is.Not.Null);
        Assert.That(saratanPatterns, Is.Not.Null);
        Assert.That(((Behaviour)saratanPatterns).enabled, Is.True);
        AssertSerializedValuesEqual(kaiju.GetComponent(BossAttackControllerType), attack);
        AssertSerializedValuesEqual(kaijuPatterns, saratanPatterns);

        Animator animator = saratan.GetComponentInChildren<Animator>(true);
        Assert.That(animator, Is.Not.Null);
        Assert.That(animator.runtimeAnimatorController, Is.Not.Null);
        string controllerPath = AssetDatabase.GetAssetPath(animator.runtimeAnimatorController);
        Assert.That(controllerPath, Does.StartWith(SaratanRoot + "/Runtime/Animation/"));

        AnimationClip[] clips = animator.runtimeAnimatorController.animationClips;
        Assert.That(clips.Select(clip => clip.name), Is.EquivalentTo(new[]
        {
            "Saratan_RigB_BasicIdle",
            "Saratan_RigB_Attack_FiringFront",
            "Saratan_RigB_Attack_BreathFront",
        }));
        foreach (AnimationClip clip in clips)
        {
            Assert.That(AssetDatabase.GetAssetPath(clip), Does.StartWith(SaratanRoot + "/Art/RigB/Animations/"));
        }

        AnimationClip idle = clips.Single(clip => clip.name.Contains("BasicIdle", StringComparison.Ordinal));
        Assert.That(AnimationUtility.GetAnimationClipSettings(idle).loopTime, Is.True);
        foreach (AnimationClip attackClip in clips.Where(clip => clip != idle))
        {
            Assert.That(AnimationUtility.GetAnimationClipSettings(attackClip).loopTime, Is.False, attackClip.name);
        }

        Type driverType = RequireType("KaijuBossAnimationDriver, Assembly-CSharp");
        Assert.That(saratan.GetComponentInChildren(driverType, true), Is.Null);
        Assert.That(saratan.GetComponentsInChildren<Transform>(true).Count(t => t.name == "SaratanMouthFirePoint"), Is.EqualTo(1));
        Assert.That(saratan.GetComponentsInChildren<Transform>(true).Count(t => t.name == "BossFootDebrisFirePoint1"), Is.EqualTo(1));
        Assert.That(saratan.GetComponentsInChildren<Transform>(true).Count(t => t.name == "BossFootDebrisFirePoint2"), Is.EqualTo(1));

        SerializedObject serializedPatterns = new(saratanPatterns);
        UnityEngine.Object debrisCatalog = serializedPatterns.FindProperty("debrisFragmentCatalog").objectReferenceValue;
        string debrisCatalogPath = AssetDatabase.GetAssetPath(debrisCatalog);
        Assert.That(debrisCatalogPath, Is.EqualTo(
            SaratanRoot + "/Runtime/Attack/Data/SaratanDebrisFragmentCatalog.asset"));
        Assert.That(AssetDatabase.AssetPathToGUID(debrisCatalogPath), Is.Not.EqualTo(
            AssetDatabase.AssetPathToGUID("Assets/_Project/Resources/VFX/MonsterDebrisFragmentCatalog.asset")));
    }

    [Test]
    public void SaratanPrefab_UsesUnitScaleForGameplayAndVisualHierarchy()
    {
        GameObject root = PrefabUtility.LoadPrefabContents(SaratanPrefabPath);
        try
        {
            Assert.That(root.transform.localScale, Is.EqualTo(Vector3.one), "Boss root");

            Transform visualRoot = root.transform.Find("BossVisualRoot");
            Assert.That(visualRoot, Is.Not.Null);
            Assert.That(visualRoot.localScale, Is.EqualTo(Vector3.one), "BossVisualRoot");

            Animator animator = root.GetComponentInChildren<Animator>(true);
            Assert.That(animator, Is.Not.Null);
            Assert.That(animator.transform.localScale, Is.EqualTo(Vector3.one), "Saratan model");

            foreach (string childName in new[]
                     {
                         "AimPoint",
                         "BossHurtbox",
                         "BossFootDebrisFirePoint1",
                         "BossFootDebrisFirePoint2",
                     })
            {
                Transform child = root.transform.Find(childName);
                Assert.That(child, Is.Not.Null, childName);
                Assert.That(child.localScale, Is.EqualTo(Vector3.one), childName);
            }

            BoxCollider hurtbox = root.transform.Find("BossHurtbox")?.GetComponent<BoxCollider>();
            Assert.That(hurtbox, Is.Not.Null);
            Assert.That(hurtbox.size.x, Is.GreaterThan(0f));
            Assert.That(hurtbox.size.y, Is.GreaterThan(0f));
            Assert.That(hurtbox.size.z, Is.GreaterThan(0f));
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    [Test]
    public void BossVisualHeights_AreValidAtAuthoredUnitScales()
    {
        float kaijuHeight = MeasureVisualHeight(KaijuPrefabPath);
        float saratanHeight = MeasureVisualHeight(SaratanPrefabPath);

        Assert.That(kaijuHeight, Is.GreaterThan(0f));
        Assert.That(saratanHeight, Is.GreaterThan(0f));
    }

    [TestCase("Assets/Materials/Aircraft/Viper.mat")]
    [TestCase("Assets/Materials/Aircraft/20mmGatlingGun_Viper.mat")]
    [TestCase("Assets/Materials/Aircraft/AGM.mat")]
    [TestCase("Assets/Materials/Aircraft/PylonAGM4rds.mat")]
    [TestCase("Assets/Materials/Aircraft/RocketPod19rds.mat")]
    [TestCase("Assets/Materials/Aircraft/Sidewinder.mat")]
    [TestCase("Assets/Materials/Aircraft/pilot_test.mat")]
    [TestCase("Assets/Materials/Aircraft/ViperCockpitGlass.mat")]
    public void ViperMaterial_UsesSupportedUrpShader(string materialPath)
    {
        Material material = AssetDatabase.LoadAssetAtPath<Material>(materialPath);
        Assert.That(material, Is.Not.Null, materialPath);
        Assert.That(material.shader, Is.Not.Null, materialPath);
        Assert.That(material.shader.isSupported, Is.True, materialPath);
        Assert.That(material.shader.name, Is.EqualTo("Universal Render Pipeline/Lit"), materialPath);
    }

    [Test]
    public void SaratanContent_DoesNotReferenceKaijuOrImportedSourceAssets()
    {
        string[] dependencies = AssetDatabase.GetDependencies(SaratanRoot, true);
        string[] forbidden = dependencies.Where(path =>
                path.StartsWith(KaijuRoot, StringComparison.Ordinal) ||
                path.StartsWith("Assets/Animation/Invader/", StringComparison.Ordinal) ||
                path.StartsWith("Assets/Invader/", StringComparison.Ordinal) ||
                path.StartsWith("Assets/Materials/Invader/", StringComparison.Ordinal) ||
                path.StartsWith("Assets/Textures/Invader/", StringComparison.Ordinal) ||
                path.Equals("Assets/Prefabs/Characters/BossPlaceholder.prefab", StringComparison.Ordinal))
            .ToArray();

        Assert.That(forbidden, Is.Empty, string.Join("\n", forbidden));
    }

    [Test]
    public void SaratanMaterials_UseSupportedShaderAndOwnTextures()
    {
        string[] materialGuids = AssetDatabase.FindAssets("t:Material", new[] { SaratanRoot + "/Art" });
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
                Assert.That(texturePath, Does.StartWith(SaratanRoot + "/Art/"),
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

    private static bool IsLegacyKaijuPath(string path)
    {
        return path.StartsWith("Assets/Animation/Invader/", StringComparison.Ordinal) ||
               path.StartsWith("Assets/Invader/", StringComparison.Ordinal) ||
               path.StartsWith("Assets/Materials/Invader/", StringComparison.Ordinal) ||
               path.StartsWith("Assets/Textures/Invader/", StringComparison.Ordinal);
    }

    private static float MeasureVisualHeight(string prefabPath)
    {
        GameObject root = PrefabUtility.LoadPrefabContents(prefabPath);
        try
        {
            root.transform.SetLocalPositionAndRotation(Vector3.zero, Quaternion.identity);
            // Compare the authored in-game size; both boss prefabs keep their gameplay
            // hierarchy at unit scale.
            Transform visualRoot = root.transform.Find("BossVisualRoot");
            Assert.That(visualRoot, Is.Not.Null, prefabPath);

            Renderer[] renderers = visualRoot.GetComponentsInChildren<Renderer>(true);
            Assert.That(renderers, Is.Not.Empty, prefabPath);
            Bounds bounds = renderers[0].bounds;
            for (int i = 1; i < renderers.Length; i++)
            {
                bounds.Encapsulate(renderers[i].bounds);
            }

            return bounds.size.y;
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    private static void AssertSerializedValuesEqual(Component expected, Component actual)
    {
        Dictionary<string, string> expectedValues = CaptureNonReferenceValues(expected);
        Dictionary<string, string> actualValues = CaptureNonReferenceValues(actual);
        Assert.That(actualValues, Is.EqualTo(expectedValues));
    }

    private static Dictionary<string, string> CaptureNonReferenceValues(Component component)
    {
        SerializedObject serialized = new(component);
        SerializedProperty property = serialized.GetIterator();
        Dictionary<string, string> values = new(StringComparer.Ordinal);
        bool enterChildren = true;
        while (property.NextVisible(enterChildren))
        {
            enterChildren = true;
            if (property.propertyPath == "m_Script" ||
                property.propertyType == SerializedPropertyType.ObjectReference ||
                property.propertyType == SerializedPropertyType.Generic)
            {
                continue;
            }

            values[property.propertyPath] = property.boxedValue?.ToString() ?? "<null>";
        }

        return values;
    }
}
