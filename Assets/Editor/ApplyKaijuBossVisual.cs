using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class ApplyKaijuBossVisual
{
    private const string BattleArenaScenePath = "Assets/Scenes/BattleArena.unity/BattleArena.unity";
    private const string KaijuVisualAssetPath = "Assets/Invader/Kaiju_001.fbx";
    private const string KaijuIdleClipPath = "Assets/Animation/Invader/Kaiju_Turn_Idle_001.anim";
    private const string KaijuAttack1ClipPath = "Assets/Animation/Invader/Kaiju_Turn_Attack_001.anim";
    private const string KaijuAttack2ClipPath = "Assets/Animation/Invader/Kaiju_Turn_Attack_002.anim";
    private const string AnimatorControllerPath = "Assets/Animation/Invader/KaijuBoss.controller";
    private const string KaijuBodyMaterialPath = "Assets/Materials/Invader/Kaiju_001.mat";
    private const string KaijuEyeMaterialPath = "Assets/Materials/Invader/Kaiju_Eye.mat";
    private const string KaijuHeadSailMaterialPath = "Assets/Materials/Invader/Kaiju_HeadSail.mat";
    private const string KaijuBodyTexturePath = "Assets/Textures/Invader/Kaiju_001.png";
    private const string KaijuEyeTexturePath = "Assets/Textures/Invader/Kaiju_Eye.png";
    private const string KaijuHeadSailTexturePath = "Assets/Textures/Invader/Kaiju_HeadSail.png";

    private const string BossRootName = "BossPlaceholder";
    private const string BossVisualRootName = "BossVisualRoot";
    private const string VisualInstanceName = "BossVisual_Kaiju";
    private const string IdleStateName = "Idle";
    private const string Attack1StateName = "Attack1";
    private const string Attack2StateName = "Attack2";
    private const string Attack1TriggerName = "Attack1";
    private const string Attack2TriggerName = "Attack2";
    private const float FallbackTargetHeight = 6.4f;

    private static readonly Vector3 KaijuLocalEulerAngles = new(270f, 0f, 0f);

    [MenuItem("Tools/TitanDestroyer/Apply Kaiju Boss Visual")]
    public static void Apply()
    {
        Scene scene = EnsureBattleArenaScene();
        if (!scene.IsValid())
        {
            Debug.LogError("BattleArena scene could not be loaded.");
            return;
        }

        Transform bossRoot = FindInScene(scene, BossRootName);
        if (bossRoot == null)
        {
            Debug.LogError($"Could not find {BossRootName} in BattleArena.");
            return;
        }

        GameObject kaijuAsset = AssetDatabase.LoadAssetAtPath<GameObject>(KaijuVisualAssetPath);
        if (kaijuAsset == null)
        {
            Debug.LogError($"Kaiju FBX is missing: {KaijuVisualAssetPath}");
            return;
        }

        ConvertKaijuMaterialsToUrp();

        Transform visualRoot = EnsureChild(bossRoot, BossVisualRootName);
        bool hasPreviousBounds = TryGetRendererBounds(visualRoot, out Bounds previousBounds);

        ClearChildren(visualRoot);

        GameObject kaijuVisual = PrefabUtility.InstantiatePrefab(kaijuAsset, visualRoot) as GameObject;
        if (kaijuVisual == null)
        {
            Debug.LogError($"Failed to instantiate Kaiju asset: {KaijuVisualAssetPath}");
            return;
        }

        kaijuVisual.name = VisualInstanceName;
        ResetLocal(kaijuVisual.transform, Vector3.zero, KaijuLocalEulerAngles, Vector3.one);
        AttachBossAnimator(kaijuVisual);
        MatchPreviousVisualBounds(kaijuVisual.transform, hasPreviousBounds, previousBounds);

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Debug.Log($"Applied {KaijuVisualAssetPath} to {BossRootName}/{BossVisualRootName}.");
    }

    [MenuItem("Tools/TitanDestroyer/Rebuild Kaiju Boss Animator")]
    public static void RebuildAnimatorOnly()
    {
        RuntimeAnimatorController controller = EnsureAnimatorController();
        if (controller == null)
        {
            Debug.LogError("Kaiju boss animator controller could not be rebuilt.");
            return;
        }

        BindExistingSceneAnimator(controller);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log($"Rebuilt {AnimatorControllerPath} with idle and attack states.");
    }

    private static void ConvertKaijuMaterialsToUrp()
    {
        Shader shader = Shader.Find("Universal Render Pipeline/Lit");
        if (shader == null)
        {
            shader = Shader.Find("Standard");
        }

        if (shader == null)
        {
            Debug.LogWarning("Could not find a supported lit shader for Kaiju materials.");
            return;
        }

        ConfigureTexturedMaterial(KaijuBodyMaterialPath, KaijuBodyTexturePath, shader, Color.white);
        ConfigureTexturedMaterial(KaijuEyeMaterialPath, KaijuEyeTexturePath, shader, Color.white);
        ConfigureTexturedMaterial(KaijuHeadSailMaterialPath, KaijuHeadSailTexturePath, shader, Color.white);
    }

    private static void ConfigureTexturedMaterial(string materialPath, string texturePath, Shader shader, Color color)
    {
        Material material = AssetDatabase.LoadAssetAtPath<Material>(materialPath);
        Texture texture = AssetDatabase.LoadAssetAtPath<Texture>(texturePath);
        if (material == null || texture == null)
        {
            Debug.LogWarning($"Could not configure Kaiju material. Material: {materialPath}, Texture: {texturePath}");
            return;
        }

        material.shader = shader;

        if (material.HasProperty("_BaseMap"))
        {
            material.SetTexture("_BaseMap", texture);
        }

        if (material.HasProperty("_MainTex"))
        {
            material.SetTexture("_MainTex", texture);
        }

        if (material.HasProperty("_BaseColor"))
        {
            material.SetColor("_BaseColor", color);
        }

        if (material.HasProperty("_Color"))
        {
            material.SetColor("_Color", color);
        }

        if (material.HasProperty("_Metallic"))
        {
            material.SetFloat("_Metallic", 0f);
        }

        if (material.HasProperty("_Smoothness"))
        {
            material.SetFloat("_Smoothness", 0.35f);
        }

        material.enableInstancing = true;
        EditorUtility.SetDirty(material);
    }

    private static Scene EnsureBattleArenaScene()
    {
        Scene activeScene = SceneManager.GetActiveScene();
        if (activeScene.path == BattleArenaScenePath)
        {
            return activeScene;
        }

        return EditorSceneManager.OpenScene(BattleArenaScenePath, OpenSceneMode.Single);
    }

    private static Transform FindInScene(Scene scene, string name)
    {
        foreach (GameObject rootObject in scene.GetRootGameObjects())
        {
            foreach (Transform child in rootObject.GetComponentsInChildren<Transform>(true))
            {
                if (child.name == name)
                {
                    return child;
                }
            }
        }

        return null;
    }

    private static Transform EnsureChild(Transform parent, string name)
    {
        Transform existing = parent.Find(name);
        if (existing != null)
        {
            return existing;
        }

        GameObject child = new(name);
        child.transform.SetParent(parent, false);
        child.transform.localPosition = Vector3.zero;
        child.transform.localRotation = Quaternion.identity;
        child.transform.localScale = Vector3.one;
        return child.transform;
    }

    private static void ClearChildren(Transform root)
    {
        List<GameObject> children = new();
        for (int i = 0; i < root.childCount; i++)
        {
            children.Add(root.GetChild(i).gameObject);
        }

        foreach (GameObject child in children)
        {
            Object.DestroyImmediate(child);
        }
    }

    private static void ResetLocal(Transform target, Vector3 localPosition, Vector3 localEulerAngles, Vector3 localScale)
    {
        target.localPosition = localPosition;
        target.localRotation = Quaternion.Euler(localEulerAngles);
        target.localScale = localScale;
    }

    private static void AttachBossAnimator(GameObject kaijuVisual)
    {
        RuntimeAnimatorController controller = EnsureAnimatorController();
        if (controller == null)
        {
            return;
        }

        Animator animator = kaijuVisual.GetComponent<Animator>();
        if (animator == null)
        {
            animator = kaijuVisual.AddComponent<Animator>();
        }

        ConfigureAnimatorComponent(kaijuVisual, controller);
    }

    private static void BindExistingSceneAnimator(RuntimeAnimatorController controller)
    {
        Scene scene = SceneManager.GetSceneByPath(BattleArenaScenePath);
        if (!scene.IsValid() || !scene.isLoaded)
        {
            return;
        }

        Transform kaijuVisual = FindInScene(scene, VisualInstanceName);
        if (kaijuVisual == null)
        {
            return;
        }

        ConfigureAnimatorComponent(kaijuVisual.gameObject, controller);
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
    }

    private static void ConfigureAnimatorComponent(GameObject kaijuVisual, RuntimeAnimatorController controller)
    {
        Animator animator = kaijuVisual.GetComponent<Animator>();
        if (animator == null)
        {
            animator = kaijuVisual.AddComponent<Animator>();
        }

        animator.runtimeAnimatorController = controller;
        animator.avatar = LoadKaijuAvatar();
        animator.applyRootMotion = false;
        animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
        EditorUtility.SetDirty(animator);
    }

    private static Avatar LoadKaijuAvatar()
    {
        Object[] assets = AssetDatabase.LoadAllAssetsAtPath(KaijuVisualAssetPath);
        foreach (Object asset in assets)
        {
            if (asset is Avatar avatar)
            {
                return avatar;
            }
        }

        return null;
    }

    private static RuntimeAnimatorController EnsureAnimatorController()
    {
        AnimationClip idleClip = AssetDatabase.LoadAssetAtPath<AnimationClip>(KaijuIdleClipPath);
        AnimationClip attack1Clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(KaijuAttack1ClipPath);
        AnimationClip attack2Clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(KaijuAttack2ClipPath);
        if (idleClip == null || attack1Clip == null || attack2Clip == null)
        {
            Debug.LogWarning("One or more Kaiju animation clips were not found.");
            return null;
        }

        EnsureFolder(Path.GetDirectoryName(AnimatorControllerPath));
        SetClipLoop(idleClip, true);
        SetClipLoop(attack1Clip, false);
        SetClipLoop(attack2Clip, false);

        AnimatorController controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(AnimatorControllerPath);
        if (controller == null)
        {
            controller = AnimatorController.CreateAnimatorControllerAtPath(AnimatorControllerPath);
        }

        ConfigureAnimatorController(controller, idleClip, attack1Clip, attack2Clip);
        return controller;
    }

    private static void ConfigureAnimatorController(
        AnimatorController controller,
        AnimationClip idleClip,
        AnimationClip attack1Clip,
        AnimationClip attack2Clip)
    {
        EnsureTriggerParameter(controller, Attack1TriggerName);
        EnsureTriggerParameter(controller, Attack2TriggerName);

        AnimatorStateMachine stateMachine = controller.layers[0].stateMachine;
        ClearStateMachine(stateMachine);

        AnimatorState idleState = stateMachine.AddState(IdleStateName, new Vector3(200f, 0f, 0f));
        idleState.motion = idleClip;
        AnimatorState attack1State = stateMachine.AddState(Attack1StateName, new Vector3(200f, 100f, 0f));
        attack1State.motion = attack1Clip;
        AnimatorState attack2State = stateMachine.AddState(Attack2StateName, new Vector3(200f, 200f, 0f));
        attack2State.motion = attack2Clip;

        stateMachine.defaultState = idleState;

        AddAttackTransition(stateMachine, attack1State, Attack1TriggerName);
        AddAttackTransition(stateMachine, attack2State, Attack2TriggerName);
        AddReturnToIdleTransition(attack1State, idleState);
        AddReturnToIdleTransition(attack2State, idleState);

        EditorUtility.SetDirty(controller);
    }

    private static void EnsureTriggerParameter(AnimatorController controller, string parameterName)
    {
        foreach (AnimatorControllerParameter parameter in controller.parameters)
        {
            if (parameter.name != parameterName)
            {
                continue;
            }

            if (parameter.type == AnimatorControllerParameterType.Trigger)
            {
                return;
            }

            controller.RemoveParameter(parameter);
            break;
        }

        controller.AddParameter(parameterName, AnimatorControllerParameterType.Trigger);
    }

    private static void ClearStateMachine(AnimatorStateMachine stateMachine)
    {
        foreach (AnimatorStateTransition transition in stateMachine.anyStateTransitions)
        {
            stateMachine.RemoveAnyStateTransition(transition);
        }

        foreach (ChildAnimatorState childState in stateMachine.states)
        {
            stateMachine.RemoveState(childState.state);
        }

        foreach (ChildAnimatorStateMachine childStateMachine in stateMachine.stateMachines)
        {
            stateMachine.RemoveStateMachine(childStateMachine.stateMachine);
        }
    }

    private static void AddAttackTransition(AnimatorStateMachine stateMachine, AnimatorState targetState, string triggerName)
    {
        AnimatorStateTransition transition = stateMachine.AddAnyStateTransition(targetState);
        transition.hasExitTime = false;
        transition.duration = 0.05f;
        transition.hasFixedDuration = true;
        transition.canTransitionToSelf = true;
        transition.AddCondition(AnimatorConditionMode.If, 0f, triggerName);
    }

    private static void AddReturnToIdleTransition(AnimatorState attackState, AnimatorState idleState)
    {
        AnimatorStateTransition transition = attackState.AddTransition(idleState);
        transition.hasExitTime = true;
        transition.exitTime = 0.95f;
        transition.duration = 0.1f;
        transition.hasFixedDuration = true;
    }

    private static void SetClipLoop(AnimationClip clip, bool loopTime)
    {
        AnimationClipSettings settings = AnimationUtility.GetAnimationClipSettings(clip);
        if (settings.loopTime == loopTime)
        {
            return;
        }

        settings.loopTime = loopTime;
        AnimationUtility.SetAnimationClipSettings(clip, settings);
        EditorUtility.SetDirty(clip);
    }

    private static void EnsureFolder(string folderPath)
    {
        if (string.IsNullOrEmpty(folderPath) || AssetDatabase.IsValidFolder(folderPath))
        {
            return;
        }

        string parent = Path.GetDirectoryName(folderPath);
        EnsureFolder(parent);
        AssetDatabase.CreateFolder(parent, Path.GetFileName(folderPath));
    }

    private static void MatchPreviousVisualBounds(Transform visual, bool hasPreviousBounds, Bounds previousBounds)
    {
        if (!TryGetRendererBounds(visual, out Bounds bounds) || bounds.size.y <= 0.001f)
        {
            return;
        }

        float targetHeight = hasPreviousBounds && previousBounds.size.y > 0.001f
            ? previousBounds.size.y
            : FallbackTargetHeight;

        float scaleFactor = targetHeight / bounds.size.y;
        if (IsValidScale(scaleFactor))
        {
            visual.localScale *= scaleFactor;
        }

        if (!TryGetRendererBounds(visual, out bounds))
        {
            return;
        }

        Vector3 offset = hasPreviousBounds
            ? previousBounds.center - bounds.center
            : new Vector3(0f, visual.parent.position.y - bounds.min.y, 0f);

        visual.position += offset;
    }

    private static bool TryGetRendererBounds(Transform root, out Bounds bounds)
    {
        Renderer[] renderers = root.GetComponentsInChildren<Renderer>(true);
        bounds = new Bounds(root.position, Vector3.zero);
        bool hasBounds = false;

        foreach (Renderer renderer in renderers)
        {
            if (hasBounds)
            {
                bounds.Encapsulate(renderer.bounds);
            }
            else
            {
                bounds = renderer.bounds;
                hasBounds = true;
            }
        }

        return hasBounds;
    }

    private static bool IsValidScale(float value)
    {
        return value > 0.001f && value < 1000f && !float.IsNaN(value) && !float.IsInfinity(value);
    }
}
