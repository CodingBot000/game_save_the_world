#if UNITY_EDITOR
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

public static class BattleFireballProjectileBuilder
{
    private const string BattleArenaScenePath = "Assets/Scenes/BattleArena.unity/BattleArena.unity";
    private const string ProjectileTemplateName = "BossProjectileTemplate";
    private const string LegacyVisualName = "BossProjectileVisual";
    private const string CoreSphereName = "CoreSphere";
    private const string FlameShellName = "FlameSurfaceShell";
    private const string TrailName = "Optional Trail";
    private const string FrameFolder = "Assets/Art/VFX/FireballSphere/Frames";
    private const string FlameShaderName = "Titan Destroyer/VFX/Fireball Surface Shell";
    private const string FlameMaterialPath = "Assets/Materials/VFX/FireballSurfaceShell.mat";
    private const string TrailMaterialPath = "Assets/Materials/VFX/FireballTrail.mat";
    private const string BossProjectileMaterialPath = "Assets/Materials/Boss/BossProjectile.mat";

    private const float DefaultShellScale = 1.08f;
    private const float DefaultFrameRate = 18f;
    private const float DefaultAlpha = 0.88f;
    private const float DefaultEmission = 3f;
    private const float DefaultFrontStart = -0.12f;
    private const float DefaultFrontEnd = 0.64f;

    [MenuItem("Tools/Titan Destroyer/Rebuild Fireball Projectile Surface")]
    private static void RebuildLoadedScene()
    {
        ConfigureFrameImporters();
        Scene scene = SceneManager.GetActiveScene();
        if (!scene.IsValid())
        {
            Debug.LogError("Fireball projectile rebuild failed. No valid scene is loaded.");
            return;
        }

        if (BuildInScene(scene))
        {
            EditorSceneManager.MarkSceneDirty(scene);
            Debug.Log("Fireball projectile surface rebuilt on BossProjectileTemplate.");
        }
    }

    public static void RebuildBattleArenaForBatch()
    {
        ConfigureFrameImporters();
        Scene scene = EditorSceneManager.OpenScene(BattleArenaScenePath);
        if (BuildInScene(scene))
        {
            EditorSceneManager.SaveScene(scene);
        }
    }

    private static bool BuildInScene(Scene scene)
    {
        GameObject projectileTemplate = FindSceneObject(scene, ProjectileTemplateName);
        if (projectileTemplate == null)
        {
            Debug.LogError($"Fireball projectile rebuild failed. {ProjectileTemplateName} was not found.");
            return false;
        }

        Texture2D[] frames = LoadFrames();
        if (frames.Length == 0)
        {
            Debug.LogError($"Fireball projectile rebuild failed. No sphere_*.png frames were found in {FrameFolder}.");
            return false;
        }

        Material flameMaterial = CreateOrUpdateFlameMaterial(frames[0]);
        Material trailMaterial = CreateOrUpdateTrailMaterial();
        Material coreMaterial = AssetDatabase.LoadAssetAtPath<Material>(BossProjectileMaterialPath);
        if (flameMaterial == null || trailMaterial == null || coreMaterial == null)
        {
            Debug.LogError("Fireball projectile rebuild failed. One or more materials could not be loaded.");
            return false;
        }

        Transform root = projectileTemplate.transform;
        Transform coreSphere = EnsureCoreSphere(root, coreMaterial);
        Renderer shellRenderer = EnsureFlameShell(root, flameMaterial);
        EnsureTrail(root, trailMaterial);
        ConfigureAnimator(projectileTemplate, shellRenderer, frames);

        EditorUtility.SetDirty(projectileTemplate);
        EditorUtility.SetDirty(coreSphere.gameObject);
        EditorUtility.SetDirty(shellRenderer.gameObject);
        return true;
    }

    private static void ConfigureFrameImporters()
    {
        if (!Directory.Exists(FrameFolder))
        {
            return;
        }

        string[] framePaths = Directory.GetFiles(FrameFolder, "sphere_*.png").OrderBy(path => path).ToArray();
        for (int i = 0; i < framePaths.Length; i++)
        {
            string assetPath = framePaths[i].Replace('\\', '/');
            TextureImporter importer = AssetImporter.GetAtPath(assetPath) as TextureImporter;
            if (importer == null)
            {
                AssetDatabase.ImportAsset(assetPath, ImportAssetOptions.ForceUpdate);
                importer = AssetImporter.GetAtPath(assetPath) as TextureImporter;
            }

            if (importer == null)
            {
                continue;
            }

            importer.textureType = TextureImporterType.Default;
            importer.alphaIsTransparency = true;
            importer.mipmapEnabled = false;
            importer.sRGBTexture = true;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.filterMode = FilterMode.Bilinear;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            EditorUtility.SetDirty(importer);
            importer.SaveAndReimport();
        }
    }

    private static Texture2D[] LoadFrames()
    {
        if (!Directory.Exists(FrameFolder))
        {
            return new Texture2D[0];
        }

        return Directory.GetFiles(FrameFolder, "sphere_*.png")
            .OrderBy(path => path)
            .Select(path => AssetDatabase.LoadAssetAtPath<Texture2D>(path.Replace('\\', '/')))
            .Where(texture => texture != null)
            .ToArray();
    }

    private static Material CreateOrUpdateFlameMaterial(Texture2D firstFrame)
    {
        Shader shader = Shader.Find(FlameShaderName);
        if (shader == null)
        {
            Debug.LogError($"Fireball projectile rebuild failed. Shader was not found: {FlameShaderName}");
            return null;
        }

        Material material = AssetDatabase.LoadAssetAtPath<Material>(FlameMaterialPath);
        if (material == null)
        {
            material = new Material(shader)
            {
                name = Path.GetFileNameWithoutExtension(FlameMaterialPath)
            };
            AssetDatabase.CreateAsset(material, FlameMaterialPath);
        }

        material.shader = shader;
        material.SetTexture("_FireFrame", firstFrame);
        material.SetColor("_Tint", new Color(1f, 0.72f, 0.28f, 1f));
        material.SetFloat("_Alpha", DefaultAlpha);
        material.SetFloat("_EmissionStrength", DefaultEmission);
        material.SetFloat("_FrontStart", DefaultFrontStart);
        material.SetFloat("_FrontEnd", DefaultFrontEnd);
        material.SetFloat("_DarkCutoff", 0.03f);
        material.SetFloat("_DarkSoftness", 0.14f);
        material.SetFloat("_EdgeFadeStart", 0.84f);
        material.SetFloat("_EdgeFadeEnd", 1f);
        EditorUtility.SetDirty(material);
        return material;
    }

    private static Material CreateOrUpdateTrailMaterial()
    {
        Shader shader =
            Shader.Find("Universal Render Pipeline/Particles/Unlit") ??
            Shader.Find("Sprites/Default") ??
            Shader.Find("Unlit/Transparent") ??
            Shader.Find("Universal Render Pipeline/Unlit");

        if (shader == null)
        {
            Debug.LogError("Fireball projectile rebuild failed. No compatible trail shader was found.");
            return null;
        }

        Material material = AssetDatabase.LoadAssetAtPath<Material>(TrailMaterialPath);
        if (material == null)
        {
            material = new Material(shader)
            {
                name = Path.GetFileNameWithoutExtension(TrailMaterialPath)
            };
            AssetDatabase.CreateAsset(material, TrailMaterialPath);
        }

        material.shader = shader;
        if (material.HasProperty("_BaseColor"))
        {
            material.SetColor("_BaseColor", new Color(1f, 0.42f, 0.06f, 0.62f));
        }

        if (material.HasProperty("_Color"))
        {
            material.SetColor("_Color", new Color(1f, 0.42f, 0.06f, 0.62f));
        }

        EditorUtility.SetDirty(material);
        return material;
    }

    private static Transform EnsureCoreSphere(Transform root, Material material)
    {
        Transform core = root.Find(CoreSphereName);
        if (core == null)
        {
            Transform legacyVisual = root.Find(LegacyVisualName);
            if (legacyVisual != null)
            {
                legacyVisual.name = CoreSphereName;
                core = legacyVisual;
            }
        }

        if (core == null)
        {
            GameObject coreObject = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            coreObject.name = CoreSphereName;
            core = coreObject.transform;
            core.SetParent(root, false);
        }

        Collider collider = core.GetComponent<Collider>();
        if (collider != null)
        {
            Object.DestroyImmediate(collider);
        }

        core.localPosition = Vector3.zero;
        core.localRotation = Quaternion.identity;
        core.localScale = Vector3.one;

        MeshRenderer renderer = core.GetComponent<MeshRenderer>();
        if (renderer != null)
        {
            renderer.sharedMaterial = material;
            renderer.enabled = true;
            renderer.shadowCastingMode = ShadowCastingMode.On;
            renderer.receiveShadows = true;
            EditorUtility.SetDirty(renderer);
        }

        return core;
    }

    private static Renderer EnsureFlameShell(Transform root, Material material)
    {
        Transform shell = root.Find(FlameShellName);
        if (shell == null)
        {
            GameObject shellObject = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            shellObject.name = FlameShellName;
            shell = shellObject.transform;
            shell.SetParent(root, false);
        }

        Collider collider = shell.GetComponent<Collider>();
        if (collider != null)
        {
            Object.DestroyImmediate(collider);
        }

        shell.localPosition = Vector3.zero;
        shell.localRotation = Quaternion.identity;
        shell.localScale = Vector3.one * DefaultShellScale;

        MeshRenderer renderer = shell.GetComponent<MeshRenderer>();
        renderer.sharedMaterial = material;
        renderer.enabled = true;
        renderer.shadowCastingMode = ShadowCastingMode.Off;
        renderer.receiveShadows = false;
        EditorUtility.SetDirty(renderer);
        return renderer;
    }

    private static void EnsureTrail(Transform root, Material material)
    {
        Transform trailTransform = root.Find(TrailName);
        if (trailTransform == null)
        {
            GameObject trailObject = new(TrailName);
            trailTransform = trailObject.transform;
            trailTransform.SetParent(root, false);
        }

        trailTransform.localPosition = Vector3.zero;
        trailTransform.localRotation = Quaternion.identity;
        trailTransform.localScale = Vector3.one;

        TrailRenderer trail = trailTransform.GetComponent<TrailRenderer>();
        if (trail == null)
        {
            trail = trailTransform.gameObject.AddComponent<TrailRenderer>();
        }

        trail.sharedMaterial = material;
        trail.time = 0.18f;
        trail.startWidth = 0.34f;
        trail.endWidth = 0.02f;
        trail.minVertexDistance = 0.03f;
        trail.numCapVertices = 4;
        trail.numCornerVertices = 4;
        trail.alignment = LineAlignment.View;
        trail.textureMode = LineTextureMode.Stretch;
        trail.shadowCastingMode = ShadowCastingMode.Off;
        trail.receiveShadows = false;
        trail.emitting = true;
        trail.colorGradient = CreateTrailGradient();
        EditorUtility.SetDirty(trail);
    }

    private static Gradient CreateTrailGradient()
    {
        Gradient gradient = new();
        gradient.SetKeys(
            new[]
            {
                new GradientColorKey(new Color(1f, 0.86f, 0.18f), 0f),
                new GradientColorKey(new Color(1f, 0.28f, 0.02f), 0.48f),
                new GradientColorKey(new Color(0.32f, 0.04f, 0f), 1f)
            },
            new[]
            {
                new GradientAlphaKey(0.72f, 0f),
                new GradientAlphaKey(0.34f, 0.5f),
                new GradientAlphaKey(0f, 1f)
            });
        return gradient;
    }

    private static void ConfigureAnimator(GameObject projectileTemplate, Renderer shellRenderer, Texture2D[] frames)
    {
        FireballSurfaceAnimator animator = projectileTemplate.GetComponent<FireballSurfaceAnimator>();
        if (animator == null)
        {
            animator = projectileTemplate.AddComponent<FireballSurfaceAnimator>();
        }

        SerializedObject serializedAnimator = new(animator);
        SetSerializedReference(serializedAnimator, "flameShellRenderer", shellRenderer);
        SetSerializedReference(serializedAnimator, "flameShellTransform", shellRenderer.transform);
        SetTextureArray(serializedAnimator.FindProperty("flameFrames"), frames);
        SetSerializedFloat(serializedAnimator, "frameRate", DefaultFrameRate);
        SetSerializedFloat(serializedAnimator, "shellScale", DefaultShellScale);
        SetSerializedFloat(serializedAnimator, "alpha", DefaultAlpha);
        SetSerializedFloat(serializedAnimator, "emissionStrength", DefaultEmission);
        SetSerializedFloat(serializedAnimator, "frontStart", DefaultFrontStart);
        SetSerializedFloat(serializedAnimator, "frontEnd", DefaultFrontEnd);
        serializedAnimator.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(animator);
    }

    private static GameObject FindSceneObject(Scene scene, string objectName)
    {
        foreach (GameObject root in scene.GetRootGameObjects())
        {
            Transform found = FindDeepChild(root.transform, objectName);
            if (found != null)
            {
                return found.gameObject;
            }
        }

        return null;
    }

    private static Transform FindDeepChild(Transform root, string targetName)
    {
        if (root == null || root.name == targetName)
        {
            return root;
        }

        for (int i = 0; i < root.childCount; i++)
        {
            Transform found = FindDeepChild(root.GetChild(i), targetName);
            if (found != null)
            {
                return found;
            }
        }

        return null;
    }

    private static void SetSerializedReference(SerializedObject serializedObject, string propertyName, Object value)
    {
        SerializedProperty property = serializedObject.FindProperty(propertyName);
        if (property != null)
        {
            property.objectReferenceValue = value;
        }
    }

    private static void SetSerializedFloat(SerializedObject serializedObject, string propertyName, float value)
    {
        SerializedProperty property = serializedObject.FindProperty(propertyName);
        if (property != null)
        {
            property.floatValue = value;
        }
    }

    private static void SetTextureArray(SerializedProperty property, Texture2D[] frames)
    {
        if (property == null)
        {
            return;
        }

        property.arraySize = frames.Length;
        for (int i = 0; i < frames.Length; i++)
        {
            property.GetArrayElementAtIndex(i).objectReferenceValue = frames[i];
        }
    }
}
#endif
