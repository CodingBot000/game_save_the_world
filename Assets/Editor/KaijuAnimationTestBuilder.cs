using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using TitanDestroyer.Debugging;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Object = UnityEngine.Object;

public static class KaijuAnimationTestBuilder
{
    public const string ModelPath = "Assets/Invader/Kaiju_001.fbx";
    public const string ClipsFolder = "Assets/Animation/Invader/Clips";
    public const string ScenePath = "Assets/Scenes/animTestScene.unity";
    public static readonly string[] ClipNames =
    {
        "Kaiju_BasicIdle", "Kaiju_IdleFront", "Kaiju_IdleLeft45", "Kaiju_IdleRight45",
        "Kaiju_Attack_FiringFront", "Kaiju_Attack_FiringLeft45", "Kaiju_Attack_FiringRight45",
        "Kaiju_Attack_BeamLeftToR", "Kaiju_Attack_BeamRightToL", "Kaiju_Attack_Tail",
        "Kaiju_JumpTurnR", "Kaiju_Death"
    };

    [MenuItem("Tools/TitanDestroyer/Kaiju Animation Test/1. Extract missing clips")]
    public static void ExtractClips()
    {
        RequireEditMode();
        string source = Path.GetFullPath(Path.Combine(Application.dataPath, "../../TitanSlayerNewAssets/FBX"));
        if (!Directory.Exists(source)) throw new DirectoryNotFoundException(source);
        var target = AssetDatabase.LoadAssetAtPath<GameObject>(ModelPath);
        if (target == null) throw new InvalidOperationException("Existing Kaiju model not found.");
        EnsureFolder(ClipsFolder);
        string stage = AssetDatabase.GenerateUniqueAssetPath("Assets/Editor/KaijuAnimationImportTemp");
        AssetDatabase.CreateFolder("Assets/Editor", Path.GetFileName(stage));
        try
        {
            for (int index = 0; index < ClipNames.Length; index++)
            {
                string name = ClipNames[index];
                string output = ClipsFolder + "/" + name + ".anim";
                // Never overwrite an extracted clip that may have subsequently been hand-edited.
                if (File.Exists(output))
                {
                    ValidateClip(AssetDatabase.LoadAssetAtPath<AnimationClip>(output), target);
                    Debug.Log("Preserved existing clip: " + output);
                    continue;
                }
                string input = stage + "/" + name + ".fbx";
                File.Copy(Path.Combine(source, name + ".fbx"), input);
                AssetDatabase.ImportAsset(input, ImportAssetOptions.ForceSynchronousImport);
                var importer = (ModelImporter)AssetImporter.GetAtPath(input);
                // Match the existing model: Generic, NoAvatar. Do not create a foreign Avatar.
                importer.animationType = ModelImporterAnimationType.Generic;
                importer.avatarSetup = ModelImporterAvatarSetup.NoAvatar;
                importer.importAnimation = true;
                importer.optimizeGameObjects = false;
                importer.materialImportMode = ModelImporterMaterialImportMode.None;
                importer.animationCompression = ModelImporterAnimationCompression.Off;
                importer.resampleCurves = true;
                importer.SaveAndReimport();
                var imported = AssetDatabase.LoadAllAssetsAtPath(input).OfType<AnimationClip>()
                    .Where(c => !c.name.StartsWith("__preview", StringComparison.Ordinal)).ToArray();
                if (imported.Length != 1) throw new InvalidOperationException(name + ": expected exactly one clip.");
                var clip = Object.Instantiate(imported[0]);
                clip.name = name;
                clip.hideFlags = HideFlags.None;
                clip.legacy = false;
                var settings = AnimationUtility.GetAnimationClipSettings(clip);
                settings.loopTime = index < 4;
                settings.loopBlend = false;
                AnimationUtility.SetAnimationClipSettings(clip, settings);
                try
                {
                    ValidateClip(clip, target);
                    AssetDatabase.CreateAsset(clip, output);
                }
                catch
                {
                    if (!AssetDatabase.Contains(clip)) Object.DestroyImmediate(clip);
                    throw;
                }
                Debug.Log($"Extracted {name}: {clip.length:F4}s, {clip.frameRate}fps, " +
                    $"{AnimationUtility.GetCurveBindings(clip).Length} curves, loop={clip.isLooping}");
            }
            AssetDatabase.SaveAssets();
            foreach (string name in ClipNames)
            {
                string output = ClipsFolder + "/" + name + ".anim";
                if (AssetDatabase.GetDependencies(output, true).Any(p => p.StartsWith(stage + "/", StringComparison.Ordinal)))
                    throw new InvalidOperationException("Clip still depends on temporary FBX: " + name);
            }
        }
        finally
        {
            // This unique folder contains only copies created by this invocation, never the source files.
            AssetDatabase.DeleteAsset(stage);
        }
    }

    public static void ValidateClip(AnimationClip clip, GameObject target)
    {
        if (clip == null || clip.length <= 0f) throw new InvalidOperationException("Missing/empty clip.");
        var bindings = AnimationUtility.GetCurveBindings(clip);
        if (bindings.Length == 0) throw new InvalidOperationException(clip.name + ": no animation curves.");
        var missing = bindings.Where(b => b.path != "" && target.transform.Find(b.path) == null)
            .Select(b => b.path).Distinct().ToArray();
        if (missing.Length > 0) throw new InvalidOperationException(clip.name + ": missing paths: " + string.Join(", ", missing));
        if (AnimationUtility.GetObjectReferenceCurveBindings(clip).Length > 0)
            throw new InvalidOperationException(clip.name + ": object-reference curves require explicit dependency review.");
    }

    [MenuItem("Tools/TitanDestroyer/Kaiju Animation Test/2. Create animTestScene (if missing)")]
    public static void CreateScene()
    {
        RequireEditMode();
        if (File.Exists(ScenePath)) throw new InvalidOperationException("Scene already exists; open it instead. No changes made.");
        var clips = ClipNames.Select(n => AssetDatabase.LoadAssetAtPath<AnimationClip>(ClipsFolder + "/" + n + ".anim")).ToArray();
        var asset = AssetDatabase.LoadAssetAtPath<GameObject>(ModelPath);
        foreach (var clip in clips) ValidateClip(clip, asset);
        var previous = SceneManager.GetActiveScene();
        bool untitled = string.IsNullOrEmpty(previous.path);
        if (untitled && previous.isDirty)
            throw new InvalidOperationException("Save the untitled scene before creating animTestScene.");
        // Unity cannot add a scene beside an untitled one. A clean untitled default scene has no edits to preserve.
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,
            untitled ? NewSceneMode.Single : NewSceneMode.Additive);
        try
        {
            SceneManager.SetActiveScene(scene);
            var model = (GameObject)PrefabUtility.InstantiatePrefab(asset, scene);
            model.name = "Kaiju_ExistingModel";
            var renderers = model.GetComponentsInChildren<SkinnedMeshRenderer>(true);
            foreach (var r in renderers)
            {
                string materialName = r.name == "Kaiju" ? "Kaiju_001" : r.name;
                r.sharedMaterial = PreviewMaterial(materialName);
                r.updateWhenOffscreen = true;
            }
            Bounds bounds = NormalizeAndMeasure(model, clips, renderers);
            var animator = model.GetComponent<Animator>();
            if (animator == null) animator = model.AddComponent<Animator>();
            animator.runtimeAnimatorController = null;
            animator.avatar = null;
            animator.applyRootMotion = false;
            animator.fireEvents = false;
            animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;

            var cameraObject = new GameObject("Animation Test Camera", typeof(Camera), typeof(AudioListener));
            cameraObject.tag = "MainCamera";
            var camera = cameraObject.GetComponent<Camera>();
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.055f, 0.075f, 0.105f);
            camera.orthographic = true;
            camera.nearClipPlane = 0.1f;
            camera.farClipPlane = 300f;
            FrameCamera(camera, bounds);

            var light = new GameObject("Key Light", typeof(Light)).GetComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = 1.5f;
            light.transform.rotation = Quaternion.Euler(45f, -35f, 0f);
            light.shadows = LightShadows.Soft;
            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.55f, 0.60f, 0.68f);
            RenderSettings.fog = false;
            var ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
            ground.name = "Preview Ground";
            ground.transform.position = new Vector3(0f, renderers.Min(r => r.bounds.min.y) - 0.03f, 0f);
            ground.transform.localScale = Vector3.one * 30f;
            Object.DestroyImmediate(ground.GetComponent<Collider>());
            EnsureFolder("Assets/Materials/Debug");
            const string groundPath = "Assets/Materials/Debug/KaijuAnimationTestGround.mat";
            var groundMaterial = AssetDatabase.LoadAssetAtPath<Material>(groundPath);
            if (groundMaterial == null)
            {
                groundMaterial = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                groundMaterial.color = new Color(0.16f, 0.20f, 0.25f);
                AssetDatabase.CreateAsset(groundMaterial, groundPath);
            }
            ground.GetComponent<Renderer>().sharedMaterial = groundMaterial;

            var tester = new GameObject("Animation Test Controls").AddComponent<KaijuAnimationTester>();
            BuildUI(tester, animator, clips, camera);
            PrefabUtility.RecordPrefabInstancePropertyModifications(model.transform);
            foreach (var bone in model.GetComponentsInChildren<Transform>(true))
                PrefabUtility.RecordPrefabInstancePropertyModifications(bone);
            foreach (var renderer in renderers) PrefabUtility.RecordPrefabInstancePropertyModifications(renderer);
            if (!EditorSceneManager.SaveScene(scene, ScenePath)) throw new IOException("Could not save test scene.");
            AssetDatabase.SaveAssets();
            Debug.Log("Created " + ScenePath + " with one existing model and 12 independent clips.");
        }
        finally
        {
            // Do not close or save the user's previously open scene.
            if (previous.IsValid() && previous.isLoaded) SceneManager.SetActiveScene(previous);
        }
    }

    [MenuItem("Tools/TitanDestroyer/Kaiju Animation Test/Open animTestScene")]
    public static void OpenScene()
    {
        RequireEditMode();
        if (EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) EditorSceneManager.OpenScene(ScenePath);
    }

    [MenuItem("Tools/TitanDestroyer/Kaiju Animation Test/3. Verify saved assets")]
    public static void VerifyAssets()
    {
        var model = AssetDatabase.LoadAssetAtPath<GameObject>(ModelPath);
        for (int i = 0; i < ClipNames.Length; i++)
        {
            string path = ClipsFolder + "/" + ClipNames[i] + ".anim";
            var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(path);
            ValidateClip(clip, model);
            Check(clip.isLooping == (i < 4), "Loop policy: " + clip.name);
            Check(!AssetDatabase.GetDependencies(path, true).Any(p => p.EndsWith(".fbx", StringComparison.OrdinalIgnoreCase)),
                "Clip must be independent of FBX: " + clip.name);
        }
        var models = AssetDatabase.GetDependencies(ScenePath, true)
            .Where(p => p.EndsWith(".fbx", StringComparison.OrdinalIgnoreCase)).ToArray();
        Check(models.Length == 1 && models[0] == ModelPath, "Scene must reference only the existing Kaiju FBX.");
        Debug.Log("KAIJU ASSET CHECK PASS: 12 independent clips, matching bone paths, 4 loops, 1 existing model dependency.");
    }

    [MenuItem("Tools/TitanDestroyer/Kaiju Animation Test/4. Verify buttons and poses (Play Mode)")]
    public static void VerifyPlayback()
    {
        Check(EditorApplication.isPlaying, "Enter Play Mode in animTestScene first.");
        var testers = Object.FindObjectsByType<KaijuAnimationTester>();
        Check(testers.Length == 1 && testers[0].gameObject.scene.path == ScenePath, "Open only animTestScene for playback validation.");
        var tester = testers[0];
        // Validation must not depend on the speed last selected through the debug UI.
        for (int i = 0; i < 4 && Mathf.Abs(tester.PlaybackSpeed - 1f) > 0.001f; i++) tester.CycleSpeed();
        var buttons = Object.FindObjectsByType<Button>().Where(b => b.onClick.GetPersistentEventCount() == 1 &&
            b.onClick.GetPersistentTarget(0) == tester && b.onClick.GetPersistentMethodName(0) == "PlayClip").ToArray();
        Check(buttons.Length == 12, "Expected 12 clip buttons.");
        var expected = Object.Instantiate(tester.Target.gameObject);
        expected.name = "Temporary verification model";
        expected.SetActive(false);
        try
        {
            for (int i = 0; i < tester.Clips.Length; i++)
            {
                var clip = tester.Clips[i];
                var button = buttons.Single(b => b.name == clip.name.Replace("Kaiju_", ""));
                ExecuteEvents.Execute(button.gameObject,
                    new PointerEventData(EventSystem.current) { button = PointerEventData.InputButton.Left },
                    ExecuteEvents.pointerClickHandler);
                Check(tester.SelectedIndex == i && tester.PlaybackTime == 0f, "Button selection: " + clip.name);
                tester.SeekNormalized(0.37f);
                clip.SampleAnimation(expected, clip.length * 0.37f);
                float maxPosition = 0f, maxAngle = 0f;
                foreach (string path in AnimationUtility.GetCurveBindings(clip).Where(b => b.type == typeof(Transform) && b.path != "")
                    .Select(b => b.path).Distinct())
                {
                    var actualBone = tester.Target.transform.Find(path);
                    var expectedBone = expected.transform.Find(path);
                    maxPosition = Mathf.Max(maxPosition, Vector3.Distance(actualBone.localPosition, expectedBone.localPosition));
                    maxAngle = Mathf.Max(maxAngle, Quaternion.Angle(actualBone.localRotation, expectedBone.localRotation));
                    Check(Vector3.Distance(actualBone.localScale, expectedBone.localScale) < 0.001f, clip.name + " scale " + path);
                }
                Check(maxPosition < 0.01f && maxAngle < 0.1f,
                    clip.name + $" runtime/sample pose mismatch: position={maxPosition}, angle={maxAngle}");
                tester.Replay();
                tester.Advance(clip.length + 0.1f);
                Check(clip.isLooping ? !tester.IsPaused && tester.PlaybackTime < clip.length :
                    tester.IsPaused && Mathf.Abs(tester.PlaybackTime - clip.length) < 0.001f, "End/loop behavior: " + clip.name);
                Debug.Log($"KAIJU PLAY PASS {clip.name}: pointer click, pose, loop/end; positionError={maxPosition:F6}, angleError={maxAngle:F4}");
            }
            tester.PlayClip(0);
            tester.TogglePause();
            tester.Advance(0.2f);
            Check(tester.PlaybackTime == 0f, "Pause must freeze time.");
            tester.StepFrame();
            Check(Mathf.Abs(tester.PlaybackTime - 1f / 30f) < 0.0001f, "Frame step.");
            tester.PlayClip(0);
            tester.CycleSpeed(); // 1x -> 2x
            tester.Advance(0.1f);
            Check(Mathf.Abs(tester.PlaybackTime - 0.2f) < 0.0001f, "2x speed.");
            tester.CycleSpeed(); tester.CycleSpeed(); tester.CycleSpeed(); // Restore 1x.
            tester.PlayClip(0);
            Check(Object.FindObjectsByType<SkinnedMeshRenderer>().Length == 3, "One Kaiju model (3 renderers).");
            Debug.Log("KAIJU PLAYBACK CHECK PASS: 12/12 buttons and sampled poses, loop/end, Death->Idle, pause, frame step, speed.");
        }
        finally { Object.DestroyImmediate(expected); }
    }

    private static void Check(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }

    // The existing Toon/Toon shader fails to compile on this project's Unity 6.4 / Metal setup.
    // Preview-only Lit materials reuse the existing PNGs without editing production materials.
    public static Material PreviewMaterial(string name)
    {
        EnsureFolder("Assets/Materials/Debug");
        string path = "Assets/Materials/Debug/" + name + "_AnimationPreview.mat";
        var material = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (material != null) return material;
        var shader = Shader.Find("Universal Render Pipeline/Lit");
        var texture = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Textures/Invader/" + name + ".png");
        if (shader == null || texture == null) throw new InvalidOperationException("Missing preview shader or PNG: " + name);
        material = new Material(shader) { name = name + "_AnimationPreview" };
        material.SetTexture("_BaseMap", texture);
        material.SetColor("_BaseColor", Color.white);
        material.SetFloat("_Smoothness", 0.25f);
        material.SetFloat("_Cull", (float)CullMode.Off);
        AssetDatabase.CreateAsset(material, path);
        return material;
    }

    private static Bounds BakedBounds(IEnumerable<SkinnedMeshRenderer> renderers)
    {
        var bounds = new Bounds();
        bool first = true;
        var mesh = new Mesh();
        try
        {
            foreach (var renderer in renderers)
            {
                // Compensate renderer scale in the baked vertices; TransformPoint applies it once below.
                renderer.BakeMesh(mesh, true);
                foreach (var vertex in mesh.vertices)
                {
                    Vector3 point = renderer.transform.TransformPoint(vertex);
                    if (first) { bounds = new Bounds(point, Vector3.zero); first = false; }
                    else bounds.Encapsulate(point);
                }
            }
        }
        finally { Object.DestroyImmediate(mesh); }
        return bounds;
    }

    private static Bounds NormalizeAndMeasure(GameObject model, AnimationClip[] clips, SkinnedMeshRenderer[] renderers)
    {
        // Imported animation poses are already Y-up. The production scene's X=270 rotation
        // is not appropriate for this independent, flat-floor preview.
        model.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
        model.transform.localScale = Vector3.one;
        clips[0].SampleAnimation(model, 0f);
        var bounds = BakedBounds(renderers);
        model.transform.localScale = Vector3.one * (6f / Mathf.Max(0.001f, bounds.size.y));
        bounds = BakedBounds(renderers);
        model.transform.position -= new Vector3(bounds.center.x, bounds.min.y, bounds.center.z);
        bounds = BakedBounds(renderers);
        foreach (var clip in clips)
            for (int f = 0; f <= 8; f++)
            {
                clip.SampleAnimation(model, clip.length * f / 8f);
                bounds.Encapsulate(BakedBounds(renderers));
            }
        clips[0].SampleAnimation(model, 0f);
        return bounds;
    }

    private static void FrameCamera(Camera camera, Bounds bounds)
    {
        camera.transform.position = bounds.center + new Vector3(0.65f, 0.28f, 1f).normalized * 45f;
        camera.transform.LookAt(bounds.center);
        Vector3 right = camera.transform.right, up = camera.transform.up, e = bounds.extents;
        float halfWidth = Mathf.Abs(right.x) * e.x + Mathf.Abs(right.y) * e.y + Mathf.Abs(right.z) * e.z;
        float halfHeight = Mathf.Abs(up.x) * e.x + Mathf.Abs(up.y) * e.y + Mathf.Abs(up.z) * e.z;
        camera.orthographicSize = Mathf.Max(halfHeight * 1.25f, halfWidth / (16f / 9f * 0.65f) * 1.2f, 4.2f);
        // Frame the projected bounds inside the area to the right of the panel.
        camera.transform.position -= right * camera.orthographicSize * 0.60f;
    }

    [MenuItem("Tools/TitanDestroyer/Kaiju Animation Test/Reset preview framing")]
    public static void ResetPreviewFraming()
    {
        RequireEditMode();
        var tester = Object.FindFirstObjectByType<KaijuAnimationTester>();
        Check(tester != null && tester.gameObject.scene.path == ScenePath, "Open animTestScene first.");
        var model = tester.Target.gameObject;
        var camera = tester.gameObject.scene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<Camera>()).Single();
        var transforms = model.GetComponentsInChildren<Transform>(true);
        Undo.RecordObjects(transforms, "Frame Kaiju preview");
        Undo.RecordObjects(new Object[] { camera, camera.transform }, "Frame Kaiju preview camera");
        FrameCamera(camera, NormalizeAndMeasure(model, tester.Clips, model.GetComponentsInChildren<SkinnedMeshRenderer>()));
        foreach (var bone in transforms) PrefabUtility.RecordPrefabInstancePropertyModifications(bone);
        EditorSceneManager.MarkSceneDirty(tester.gameObject.scene);
        EditorSceneManager.SaveScene(tester.gameObject.scene);
    }

    private static void BuildUI(KaijuAnimationTester tester, Animator animator, AnimationClip[] clips, Camera camera)
    {
        var canvas = new GameObject("Animation Test UI", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        canvas.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceCamera;
        canvas.GetComponent<Canvas>().worldCamera = camera;
        canvas.GetComponent<Canvas>().planeDistance = 1f;
        var scaler = canvas.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1600, 900);
        scaler.matchWidthOrHeight = 1f;
        var panel = Rect("Clip Panel", canvas.transform, 20, 20, 430, 860);
        panel.gameObject.AddComponent<Image>().color = new Color(0.035f, 0.055f, 0.085f, 0.96f);
        Label("Heading", panel, "KAIJU / ANIMATION TEST", 18, 18, 394, 34, 24);
        Label("Subtitle", panel, "12 clips  /  1 existing model\nSelect a clip to restart it from frame 0.", 18, 62, 394, 48, 17);
        var buttons = new Button[clips.Length];
        for (int i = 0; i < clips.Length; i++)
        {
            string label = ClipNames[i].Replace("Kaiju_", "");
            buttons[i] = Button(panel, label, 18, 128 + i * 43, 394, 36, out _);
            UnityEventTools.AddIntPersistentListener(buttons[i].onClick, tester.PlayClip, i);
        }
        var replay = Button(panel, "Replay", 18, 654, 124, 36, out _);
        var pause = Button(panel, "Pause", 153, 654, 124, 36, out Text pauseText);
        var speed = Button(panel, "Speed 1x", 288, 654, 124, 36, out Text speedText);
        UnityEventTools.AddPersistentListener(replay.onClick, tester.Replay);
        UnityEventTools.AddPersistentListener(pause.onClick, tester.TogglePause);
        UnityEventTools.AddPersistentListener(speed.onClick, tester.CycleSpeed);
        var step = Button(panel, "+1 frame (pause)", 18, 702, 394, 32, out _);
        UnityEventTools.AddPersistentListener(step.onClick, tester.StepFrame);

        var sliderRect = Rect("Timeline", panel, 26, 746, 378, 24);
        sliderRect.gameObject.AddComponent<Image>().color = new Color(0.22f, 0.28f, 0.36f);
        var slider = sliderRect.gameObject.AddComponent<Slider>();
        slider.minValue = 0f;
        slider.maxValue = 1f;
        var handleArea = Rect("Handle Area", sliderRect, 8, 0, 362, 24);
        var handle = Rect("Handle", handleArea, 0, 0, 16, 24);
        handle.sizeDelta = new Vector2(16f, 0f);
        handle.pivot = new Vector2(0.5f, 0.5f);
        handle.gameObject.AddComponent<Image>().color = new Color(0.25f, 0.86f, 0.80f);
        slider.handleRect = handle;
        slider.targetGraphic = handle.GetComponent<Image>();
        slider.direction = Slider.Direction.LeftToRight;
        Label("Notice", panel, "Raw clip preview / no attack effects\nDrag timeline to pause and inspect a pose.", 18, 788, 394, 52, 16);
        var status = Label("Current Clip", canvas.transform, "Press Play to preview animations", 480, 26, 1080, 64, 23);
        tester.Configure(animator, clips, status, pauseText, speedText, slider, buttons);

        var events = new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
        events.GetComponent<InputSystemUIInputModule>().AssignDefaultActions();
    }

    private static RectTransform Rect(string name, Transform parent, float x, float y, float width, float height)
    {
        var rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
        rect.SetParent(parent, false);
        rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0f, 1f);
        rect.anchoredPosition = new Vector2(x, -y);
        rect.sizeDelta = new Vector2(width, height);
        return rect;
    }

    private static Text Label(string name, Transform parent, string value, float x, float y, float width, float height, int size)
    {
        var text = Rect(name, parent, x, y, width, height).gameObject.AddComponent<Text>();
        text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        text.text = value;
        text.fontSize = size;
        text.color = new Color(0.89f, 0.94f, 0.98f);
        text.alignment = TextAnchor.MiddleLeft;
        text.raycastTarget = false;
        return text;
    }

    private static Button Button(Transform parent, string name, float x, float y, float width, float height, out Text label)
    {
        var rect = Rect(name, parent, x, y, width, height);
        var image = rect.gameObject.AddComponent<Image>();
        image.color = Color.white;
        var button = rect.gameObject.AddComponent<Button>();
        button.targetGraphic = image;
        var colors = button.colors;
        colors.normalColor = new Color(0.20f, 0.26f, 0.34f);
        colors.highlightedColor = new Color(0.27f, 0.47f, 0.53f);
        colors.pressedColor = new Color(0.15f, 0.65f, 0.64f);
        colors.selectedColor = colors.normalColor;
        button.colors = colors;
        label = Label("Label", rect, name, 10, 0, width - 20, height, 17);
        return button;
    }

    private static void RequireEditMode()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Exit Play Mode first.");
    }

    private static void EnsureFolder(string folder)
    {
        if (AssetDatabase.IsValidFolder(folder)) return;
        string parent = Path.GetDirectoryName(folder).Replace('\\', '/');
        EnsureFolder(parent);
        AssetDatabase.CreateFolder(parent, Path.GetFileName(folder));
    }
}
