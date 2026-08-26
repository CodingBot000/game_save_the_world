using System;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>Creates phase-D assets without replacing the model, legacy controller, or source FBXs.</summary>
public static class KaijuCombatAnimationBuilder
{
    public const string ScenePath = "Assets/Scenes/BattleArena.unity/BattleArena.unity";
    public const string ControllerPath = "Assets/Animation/Invader/KaijuCombat.controller";
    public const string MaskPath = "Assets/Animation/Invader/KaijuUpperBody.mask";
    private const string ClipFolder = "Assets/Animation/Invader/Clips/";
    private const string ModelPath = "Assets/Invader/Kaiju_001.fbx";
    private const string Menu = "Tools/TitanDestroyer/Kaiju Combat/";

    [MenuItem(Menu + "1. Create combat assets and bind BattleArena")]
    public static void Build()
    {
        if (EditorApplication.isPlaying) throw new InvalidOperationException("Exit Play Mode before building.");
        Scene scene = SceneManager.GetSceneByPath(ScenePath);
        if (!scene.IsValid() || !scene.isLoaded || SceneManager.sceneCount != 1)
        {
            for (int i = 0; i < SceneManager.sceneCount; i++)
                if (SceneManager.GetSceneAt(i).isDirty)
                    throw new InvalidOperationException("Save open scenes before switching to BattleArena.");
            // Existing environment components find cameras/lights globally in OnValidate.
            // Isolate the battle scene so preview-scene references cannot leak into it.
            scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        }
        if (scene.isDirty) throw new InvalidOperationException("Save BattleArena first; the builder will not overwrite unsaved edits.");
        // EnvironmentBackgroundController.OnValidate can replace the scene's sun in memory.
        // Retain the saved lighting choice while changing only Kaiju bindings.
        var sunMatch = Regex.Match(File.ReadAllText(ScenePath), @"m_Sun: \{fileID: (\d+)\}");
        if (!sunMatch.Success) throw new InvalidOperationException("Could not identify the scene's saved sun reference.");
        ulong sunId = ulong.Parse(sunMatch.Groups[1].Value);
        Light savedSun = scene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<Light>(true))
            .FirstOrDefault(light => GlobalObjectId.GetGlobalObjectIdSlow(light).targetObjectId == sunId);
        if (sunId != 0 && savedSun == null) throw new InvalidOperationException("Saved scene sun could not be resolved.");
        BossController boss = scene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<BossController>(true)).Single();
        Animator animator = boss.GetComponentInChildren<Animator>(true);
        if (animator == null || PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(animator.gameObject) != ModelPath)
            throw new InvalidOperationException("Expected exactly the existing Kaiju_001.fbx instance.");

        AvatarMask mask = CreateMask();
        AnimatorController controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath);
        if (controller == null) controller = CreateController(mask);
        EnsureCompatibilityRelays(controller);
        AddCombatEvents();

        Undo.RecordObject(animator, "Bind Kaiju combat controller");
        animator.runtimeAnimatorController = controller;
        animator.applyRootMotion = false;
        animator.fireEvents = true;
        animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
        Undo.RecordObject(animator.transform, "Use authored Y-up Kaiju poses");
        animator.transform.localRotation = Quaternion.identity;
        // Keep the artist's existing scene position and scale; only remove the old pose correction.
        Clip("BasicIdle").SampleAnimation(animator.gameObject, 0f);

        Transform head = animator.transform.Find("Root/Pelvis/Spine 01/Spine 02/Neck_01/Neck_02/Head");
        Transform tail = animator.transform.Find("Root/Pelvis/Tail001/Tail002/Tail003/Tail004");
        if (head == null || tail == null) throw new InvalidOperationException("Required existing rig bones are missing.");
        Transform mouth = head.Find("KaijuMouthSocket");
        if (mouth == null)
        {
            var socket = new GameObject("KaijuMouthSocket");
            Undo.RegisterCreatedObjectUndo(socket, "Create Kaiju mouth socket");
            mouth = socket.transform;
            mouth.SetParent(head, false);
            // Initial mouth offset is adjustable in the scene, not an edit to the source FBX.
            mouth.position = head.position + animator.transform.forward * animator.transform.lossyScale.x * 1.2f;
        }
        KaijuBossAnimationDriver driver = animator.GetComponent<KaijuBossAnimationDriver>();
        if (driver == null) driver = Undo.AddComponent<KaijuBossAnimationDriver>(animator.gameObject);
        Undo.RecordObject(driver, "Configure Kaiju animation events");
        driver.Configure(boss, mouth, tail);

        foreach (SkinnedMeshRenderer renderer in animator.GetComponentsInChildren<SkinnedMeshRenderer>())
        {
            Undo.RecordObject(renderer, "Assign combat-only Kaiju material");
            renderer.sharedMaterial = CombatMaterial(renderer.name == "Kaiju" ? "Kaiju_001" : renderer.name);
            renderer.updateWhenOffscreen = true;
            PrefabUtility.RecordPrefabInstancePropertyModifications(renderer);
        }
        foreach (Transform t in animator.GetComponentsInChildren<Transform>(true))
            PrefabUtility.RecordPrefabInstancePropertyModifications(t);
        PrefabUtility.RecordPrefabInstancePropertyModifications(animator);
        EditorUtility.SetDirty(driver);
        EditorSceneManager.MarkSceneDirty(scene);
        RenderSettings.sun = savedSun;
        EditorSceneManager.SaveScene(scene);
        AssetDatabase.SaveAssets();
        VerifyAssets();
        Debug.Log("Kaiju phase D bound to BattleArena. Original model, Toon materials, and KaijuBoss.controller preserved.");
    }

    private static AnimationClip Clip(string suffix)
    {
        var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(ClipFolder + "Kaiju_" + suffix + ".anim");
        if (clip == null) throw new InvalidOperationException("Missing extracted clip: " + suffix);
        return clip;
    }

    private static AvatarMask CreateMask()
    {
        var mask = AssetDatabase.LoadAssetAtPath<AvatarMask>(MaskPath);
        if (mask != null) return mask; // Preserve subsequent manual mask tuning.
        mask = new AvatarMask { name = "KaijuUpperBody" };
        for (int i = 0; i < (int)AvatarMaskBodyPart.LastBodyPart; i++) mask.SetHumanoidBodyPartActive((AvatarMaskBodyPart)i, false);
        var model = AssetDatabase.LoadAssetAtPath<GameObject>(ModelPath);
        mask.AddTransformPath(model.transform, true);
        const string spine = "Root/Pelvis/Spine 01";
        for (int i = 0; i < mask.transformCount; i++)
        {
            string path = mask.GetTransformPath(i);
            mask.SetTransformActive(i, path == spine || path.StartsWith(spine + "/", StringComparison.Ordinal));
        }
        AssetDatabase.CreateAsset(mask, MaskPath);
        return mask;
    }

    private static AnimatorController CreateController(AvatarMask mask)
    {
        var controller = AnimatorController.CreateAnimatorControllerAtPath(ControllerPath);
        controller.AddParameter("TargetAngle", AnimatorControllerParameterType.Float);
        controller.AddParameter(new AnimatorControllerParameter { name = "ActionSpeed", type = AnimatorControllerParameterType.Float, defaultFloat = 1f });
        controller.AddParameter(new AnimatorControllerParameter { name = "FiringSpeed", type = AnimatorControllerParameterType.Float, defaultFloat = 1f });
        controller.AddParameter("Attack1", AnimatorControllerParameterType.Trigger);
        controller.AddParameter("Attack2", AnimatorControllerParameterType.Trigger);
        AnimatorStateMachine baseMachine = controller.layers[0].stateMachine;
        AnimatorState idle = State(baseMachine, "BasicIdle", Clip("BasicIdle"), new Vector3(250, 30));
        baseMachine.defaultState = idle;
        string[] fullStates = { "BeamLeftToR", "BeamRightToL", "Tail", "JumpTurnR", "Death" };
        string[] clipNames = { "Attack_BeamLeftToR", "Attack_BeamRightToL", "Attack_Tail", "JumpTurnR", "Death" };
        for (int i = 0; i < fullStates.Length; i++)
        {
            AnimatorState state = State(baseMachine, fullStates[i], Clip(clipNames[i]), new Vector3(520, i * 70));
            state.speedParameter = "ActionSpeed";
            state.speedParameterActive = true;
            if (i == 0) TriggerTransition(idle, state, "Attack2");
        }
        controller.AddLayer("UpperBody");
        var layers = controller.layers;
        layers[1].avatarMask = mask;
        layers[1].defaultWeight = 1f;
        layers[1].blendingMode = AnimatorLayerBlendingMode.Override;
        controller.layers = layers;
        AnimatorStateMachine upperMachine = layers[1].stateMachine;
        AnimatorState aim = State(upperMachine, "AimIdle", AimTree(controller, "AimIdleTree", "Idle"), new Vector3(250, 30));
        AnimatorState fire = State(upperMachine, "Firing", AimTree(controller, "FiringTree", "Attack_Firing"), new Vector3(510, 30));
        upperMachine.defaultState = aim;
        fire.speedParameter = "FiringSpeed";
        fire.speedParameterActive = true;
        TriggerTransition(aim, fire, "Attack1");
        EditorUtility.SetDirty(controller);
        return controller;
    }

    private static AnimatorState State(AnimatorStateMachine machine, string name, Motion motion, Vector3 position)
    {
        AnimatorState state = machine.AddState(name, position);
        state.motion = motion;
        state.writeDefaultValues = true; // Restore the 20 channels absent from BeamRightToL.
        return state;
    }

    private static void EnsureCompatibilityRelays(AnimatorController controller)
    {
        foreach (var layer in controller.layers)
        foreach (var child in layer.stateMachine.states)
        {
            string name = child.state.name;
            if (name != "Firing" && name != "BeamLeftToR" && name != "BeamRightToL") continue;
            if (child.state.behaviours.OfType<KaijuCombatStateRelay>().Any()) continue;
            var relay = child.state.AddStateMachineBehaviour<KaijuCombatStateRelay>();
            relay.action = name == "Firing" ? KaijuBossAnimationDriver.ActionKind.Firing :
                name == "BeamLeftToR" ? KaijuBossAnimationDriver.ActionKind.BeamLeftToRight : KaijuBossAnimationDriver.ActionKind.BeamRightToLeft;
            EditorUtility.SetDirty(child.state);
            EditorUtility.SetDirty(relay);
        }
        EditorUtility.SetDirty(controller);
    }

    private static void TriggerTransition(AnimatorState from, AnimatorState to, string trigger)
    {
        var transition = from.AddTransition(to);
        transition.hasExitTime = false;
        transition.hasFixedDuration = true;
        transition.duration = 0.06f;
        transition.AddCondition(AnimatorConditionMode.If, 0f, trigger);
    }

    private static BlendTree AimTree(AnimatorController controller, string name, string prefix)
    {
        var tree = new BlendTree { name = name, blendType = BlendTreeType.Simple1D, blendParameter = "TargetAngle", useAutomaticThresholds = false };
        AssetDatabase.AddObjectToAsset(tree, controller);
        tree.AddChild(Clip(prefix + "Left45"), -45f);
        tree.AddChild(Clip(prefix + "Front"), 0f);
        tree.AddChild(Clip(prefix + "Right45"), 45f);
        return tree;
    }

    private static void AddCombatEvents()
    {
        foreach (string direction in new[] { "Front", "Left45", "Right45" })
            Events(Clip("Attack_Firing" + direction),
                Cue(KaijuBossAnimationDriver.FireCueTime, "OnFireProjectile"), Cue(29f / 30f, "OnFiringEnd"));
        foreach (string direction in new[] { "LeftToR", "RightToL" })
            Events(Clip("Attack_Beam" + direction),
                Cue(KaijuBossAnimationDriver.BeamCueTime, "OnBeamStart"), Cue(KaijuBossAnimationDriver.BeamEndTime, "OnBeamEnd"),
                Cue(79f / 30f, "OnBeamRecovered"));
        Events(Clip("Attack_Tail"), Cue(KaijuBossAnimationDriver.TailCueTime, "OnTailImpact"), Cue(89f / 30f, "OnTailEnd"));
        Events(Clip("JumpTurnR"), Cue(KaijuBossAnimationDriver.TurnStartTime, "OnJumpTurnStart"),
            Cue(KaijuBossAnimationDriver.TurnEndTime, "OnJumpTurnEnd"), Cue(36f / 30f, "OnJumpTurnRecovered"));
    }

    private static AnimationEvent Cue(float time, string function) => new AnimationEvent { time = time, functionName = function };

    private static void Events(AnimationClip clip, params AnimationEvent[] additions)
    {
        var existing = AnimationUtility.GetAnimationEvents(clip).ToList();
        // Only add missing names. Preserve hand-tuned timing and unrelated future events on rerun.
        foreach (AnimationEvent cue in additions)
            if (!existing.Any(e => e.functionName == cue.functionName)) existing.Add(cue);
        AnimationUtility.SetAnimationEvents(clip, existing.OrderBy(e => e.time).ToArray());
        EditorUtility.SetDirty(clip);
    }

    private static Material CombatMaterial(string name)
    {
        string path = "Assets/Materials/Invader/" + name + "_Combat.mat";
        var material = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (material != null) return material;
        var shader = Shader.Find("Universal Render Pipeline/Lit");
        if (shader == null) throw new InvalidOperationException("URP Lit shader unavailable.");
        material = new Material(shader) { name = name + "_Combat", enableInstancing = true };
        material.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture>("Assets/Textures/Invader/" + name + ".png"));
        material.SetColor("_BaseColor", Color.white);
        material.SetFloat("_Smoothness", 0.25f);
        material.SetFloat("_Cull", 0f);
        AssetDatabase.CreateAsset(material, path);
        return material;
    }

    [MenuItem(Menu + "2. Verify combat assets")]
    public static void VerifyAssets()
    {
        var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath);
        Require(controller != null && controller.layers.Length == 2, "Expected two animator layers.");
        var mask = controller.layers[1].avatarMask;
        Require(mask != null, "Upper-body mask missing.");
        int active = 0;
        for (int i = 0; i < mask.transformCount; i++)
        {
            if (!mask.GetTransformActive(i)) continue;
            active++;
            Require(mask.GetTransformPath(i).StartsWith("Root/Pelvis/Spine 01", StringComparison.Ordinal), "Mask includes a lower-body bone.");
        }
        Require(active > 10, "Mask has no upper-body paths.");
        foreach (AnimationClip clip in controller.animationClips.Distinct())
        {
            Require(AssetDatabase.GetAssetPath(clip).EndsWith(".anim", StringComparison.Ordinal), "Embedded FBX clip dependency.");
            Require(!AssetDatabase.GetDependencies(AssetDatabase.GetAssetPath(clip)).Any(p => p.EndsWith(".fbx", StringComparison.OrdinalIgnoreCase)), "Clip depends on an FBX.");
            foreach (AnimationEvent evt in AnimationUtility.GetAnimationEvents(clip))
                Require(typeof(KaijuBossAnimationDriver).GetMethod(evt.functionName) != null, "Missing event receiver: " + evt.functionName);
        }
        Require(controller.animationClips.Distinct().Count() == 12, "Expected twelve standalone clips.");
        Require(controller.layers[0].stateMachine.states.Single(s => s.state.name == "Death").state.transitions.Length == 0, "Death must not exit.");
        Debug.Log($"Kaiju combat assets PASS: 12 clips, 2 layers, {active} upper-body paths, valid event receivers, no clip FBX dependencies.");
    }

    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}
