#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

public static class GarageThumbnailBuilder
{
    private const int PreviewLayer = 2;
    private const string PrefabFolder = "Assets/Resources/Vehicles/Helicopters/Prefabs";
    private const string OutputRootFolder = "Assets/Resources/Vehicles";
    private const string OutputFolder = "Assets/Resources/Vehicles/Helicopters/Thumbnails";
    private const string CatalogAssetPath = "Assets/Resources/Vehicles/VehicleCatalog.asset";
    private const float ThumbnailDistanceMultiplier = 2.15f;
    private const float ThumbnailCameraHeightFactor = 0.18f;
    private const float ThumbnailFieldOfView = 24f;
    private const float ThumbnailYaw = 210f;

    [MenuItem("Tools/Titan Destroyer/Rebuild Vehicle Helicopter Thumbnails")]
    public static void Rebuild()
    {
        EnsureFolderExists(OutputRootFolder);
        EnsureFolderExists("Assets/Resources/Vehicles/Helicopters");
        EnsureFolderExists(OutputFolder);

        List<string> prefabPaths = new List<string>();
        List<string> displayNames = new List<string>();

        foreach (string prefabPath in GetPrefabPaths())
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            if (prefab == null)
            {
                Debug.LogError($"Could not load vehicle helicopter prefab at '{prefabPath}'.");
                continue;
            }

            string displayName = Path.GetFileNameWithoutExtension(prefabPath);
            Texture2D thumbnail = RenderThumbnail(prefab);
            string absoluteOutputPath = Path.Combine(Application.dataPath, $"Resources/Vehicles/Helicopters/Thumbnails/{displayName}.png");
            File.WriteAllBytes(absoluteOutputPath, thumbnail.EncodeToPNG());
            Object.DestroyImmediate(thumbnail);
            prefabPaths.Add(prefabPath);
            displayNames.Add(displayName);
        }

        AssetDatabase.Refresh();
        List<VehicleDefinition> definitions = new List<VehicleDefinition>();
        for (int i = 0; i < prefabPaths.Count; i++)
        {
            string thumbnailAssetPath = $"{OutputFolder}/{displayNames[i]}.png";
            definitions.Add(CreateDefinition(prefabPaths[i], thumbnailAssetPath, displayNames[i]));
        }

        SaveCatalog(definitions);
        VehiclePlayerStateCatalogBuilder.SyncCatalog();
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("Vehicle helicopter thumbnails and catalog rebuilt.");
    }

    private static Texture2D RenderThumbnail(GameObject prefab)
    {
        GameObject stageRoot = new GameObject("VehicleThumbnailStage");
        stageRoot.hideFlags = HideFlags.HideAndDontSave;

        GameObject instance = PrefabUtility.InstantiatePrefab(prefab) as GameObject;
        if (instance == null)
        {
            instance = Object.Instantiate(prefab);
        }

        Transform modelAnchor = new GameObject("ModelAnchor").transform;
        modelAnchor.gameObject.hideFlags = HideFlags.HideAndDontSave;
        modelAnchor.SetParent(stageRoot.transform, false);
        modelAnchor.localRotation = Quaternion.Euler(0f, ThumbnailYaw, 0f);

        instance.hideFlags = HideFlags.HideAndDontSave;
        instance.transform.SetParent(modelAnchor, false);
        instance.transform.localPosition = Vector3.zero;
        instance.transform.localRotation = Quaternion.identity;
        instance.transform.localScale = Vector3.one;

        DisablePreviewBehaviours(instance);
        SetLayerRecursively(stageRoot, PreviewLayer);

        GameObject cameraObject = new GameObject("ThumbnailCamera", typeof(Camera), typeof(UniversalAdditionalCameraData));
        cameraObject.hideFlags = HideFlags.HideAndDontSave;
        cameraObject.transform.SetParent(stageRoot.transform, false);

        Camera camera = cameraObject.GetComponent<Camera>();
        camera.clearFlags = CameraClearFlags.SolidColor;
        camera.backgroundColor = new Color(0.03f, 0.05f, 0.08f, 1f);
        camera.fieldOfView = ThumbnailFieldOfView;
        camera.nearClipPlane = 0.1f;
        camera.farClipPlane = 200f;
        camera.enabled = false;
        camera.cullingMask = 1 << PreviewLayer;
        ConfigurePreviewCamera(camera);

        CreatePreviewLight(stageRoot.transform, "ThumbnailKeyLight", new Vector3(40f, -35f, 0f), 1.35f);
        CreatePreviewLight(stageRoot.transform, "ThumbnailFillLight", new Vector3(20f, 145f, 0f), 0.7f);

        PositionThumbnailCamera(instance, camera);

        RenderTexture renderTexture = new RenderTexture(512, 512, 24, RenderTextureFormat.ARGB32);
        renderTexture.Create();
        camera.targetTexture = renderTexture;

        RenderPipeline.StandardRequest request = new RenderPipeline.StandardRequest
        {
            destination = renderTexture,
            mipLevel = 0,
            slice = 0,
            face = CubemapFace.Unknown,
        };
        RenderPipeline.SubmitRenderRequest(camera, request);

        RenderTexture previous = RenderTexture.active;
        RenderTexture.active = renderTexture;

        Texture2D texture = new Texture2D(512, 512, TextureFormat.RGBA32, false);
        texture.ReadPixels(new Rect(0f, 0f, 512f, 512f), 0, 0);
        texture.Apply();

        RenderTexture.active = previous;
        camera.targetTexture = null;
        renderTexture.Release();
        Object.DestroyImmediate(renderTexture);
        Object.DestroyImmediate(stageRoot);
        return texture;
    }

    private static VehicleDefinition CreateDefinition(string prefabPath, string thumbnailPath, string displayName)
    {
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
        Texture2D thumbnail = AssetDatabase.LoadAssetAtPath<Texture2D>(thumbnailPath);
        VehicleDefinition definition = new VehicleDefinition();
        definition.Set(GetIdFromDisplayName(displayName), displayName, prefab, thumbnail);
        return definition;
    }

    private static string GetIdFromDisplayName(string displayName)
    {
        Match match = Regex.Match(displayName, @"(\d+)");
        return match.Success ? $"helicopter_{match.Value}" : displayName.ToLowerInvariant();
    }

    private static IEnumerable<string> GetPrefabPaths()
    {
        string[] guids = AssetDatabase.FindAssets("t:Prefab", new[] { PrefabFolder });
        return guids
            .Select(AssetDatabase.GUIDToAssetPath)
            .OrderBy(path => ExtractIndex(Path.GetFileNameWithoutExtension(path)));
    }

    private static int ExtractIndex(string name)
    {
        Match match = Regex.Match(name, @"(\d+)");
        return match.Success && int.TryParse(match.Value, out int index) ? index : int.MaxValue;
    }

    private static void SaveCatalog(List<VehicleDefinition> definitions)
    {
        VehicleCatalog catalog = AssetDatabase.LoadAssetAtPath<VehicleCatalog>(CatalogAssetPath);
        if (catalog == null)
        {
            catalog = ScriptableObject.CreateInstance<VehicleCatalog>();
            AssetDatabase.CreateAsset(catalog, CatalogAssetPath);
        }

        catalog.ReplaceHelicopters(definitions);
        EditorUtility.SetDirty(catalog);
    }

    private static void EnsureFolderExists(string assetPath)
    {
        if (AssetDatabase.IsValidFolder(assetPath))
        {
            return;
        }

        string[] parts = assetPath.Split('/');
        string current = parts[0];
        for (int i = 1; i < parts.Length; i++)
        {
            string next = $"{current}/{parts[i]}";
            if (!AssetDatabase.IsValidFolder(next))
            {
                AssetDatabase.CreateFolder(current, parts[i]);
            }

            current = next;
        }
    }

    private static void ConfigurePreviewCamera(Camera camera)
    {
        UniversalAdditionalCameraData additionalCameraData =
            camera.GetComponent<UniversalAdditionalCameraData>() ??
            camera.gameObject.AddComponent<UniversalAdditionalCameraData>();

        additionalCameraData.renderType = CameraRenderType.Base;
        additionalCameraData.requiresColorOption = CameraOverrideOption.Off;
        additionalCameraData.requiresDepthOption = CameraOverrideOption.Off;
        additionalCameraData.SetRenderer(0);
    }

    private static void CreatePreviewLight(Transform parent, string name, Vector3 eulerAngles, float intensity)
    {
        GameObject lightObject = new GameObject(name, typeof(Light));
        lightObject.hideFlags = HideFlags.HideAndDontSave;
        lightObject.transform.SetParent(parent, false);
        lightObject.transform.localRotation = Quaternion.Euler(eulerAngles);
        lightObject.layer = PreviewLayer;

        Light light = lightObject.GetComponent<Light>();
        light.type = LightType.Directional;
        light.intensity = intensity;
        light.color = Color.white;
        light.cullingMask = 1 << PreviewLayer;
    }

    private static void PositionThumbnailCamera(GameObject target, Camera camera)
    {
        float radius = CenterPreviewAtOrigin(target);
        float distance = radius / Mathf.Tan(camera.fieldOfView * 0.5f * Mathf.Deg2Rad) * ThumbnailDistanceMultiplier;

        camera.transform.localPosition = new Vector3(0f, radius * ThumbnailCameraHeightFactor, -distance);
        camera.transform.localRotation = Quaternion.Euler(3f, 0f, 0f);
    }

    private static float CenterPreviewAtOrigin(GameObject target)
    {
        Bounds bounds = CalculateBounds(target);
        target.transform.localPosition -= bounds.center;

        bounds = CalculateBounds(target);
        return Mathf.Max(0.75f, bounds.extents.x, bounds.extents.y, bounds.extents.z);
    }

    private static Bounds CalculateBounds(GameObject target)
    {
        Renderer[] renderers = target.GetComponentsInChildren<Renderer>(true);
        if (renderers.Length == 0)
        {
            return new Bounds(Vector3.zero, Vector3.one);
        }

        Bounds bounds = renderers[0].bounds;
        for (int i = 1; i < renderers.Length; i++)
        {
            bounds.Encapsulate(renderers[i].bounds);
        }

        return bounds;
    }

    private static void DisablePreviewBehaviours(GameObject target)
    {
        MonoBehaviour[] behaviours = target.GetComponentsInChildren<MonoBehaviour>(true);
        for (int i = 0; i < behaviours.Length; i++)
        {
            behaviours[i].enabled = false;
        }
    }

    private static void SetLayerRecursively(GameObject target, int layer)
    {
        target.layer = layer;
        for (int i = 0; i < target.transform.childCount; i++)
        {
            SetLayerRecursively(target.transform.GetChild(i).gameObject, layer);
        }
    }
}
#endif
