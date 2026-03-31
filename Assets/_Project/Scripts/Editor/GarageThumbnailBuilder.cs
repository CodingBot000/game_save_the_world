#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEngine;

public static class GarageThumbnailBuilder
{
    private const string PrefabFolder = "Assets/Resources/Garage/HelicopterPrefabs";
    private const string OutputRootFolder = "Assets/Resources/Garage";
    private const string OutputFolder = "Assets/Resources/Garage/HelicopterThumbnails";
    private const float ThumbnailDistanceMultiplier = 1.55f;
    private const float ThumbnailCameraHeightFactor = 0.04f;
    private const float ThumbnailFieldOfView = 20f;
    private static readonly Vector3 PreviewScale = new Vector3(1f, 0.6920179f, 1f);
    private static readonly Quaternion PreviewRotation = Quaternion.Euler(-90f, 210f, 0f);

    [MenuItem("Tools/Titan Destroyer/Rebuild Garage Helicopter Thumbnails")]
    public static void Rebuild()
    {
        EnsureFolderExists(OutputRootFolder);
        EnsureFolderExists(OutputFolder);

        for (int i = 1; i <= 8; i++)
        {
            string helicopterName = $"Helicopter{i}";
            string prefabPath = $"{PrefabFolder}/{helicopterName}.prefab";
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            if (prefab == null)
            {
                Debug.LogError($"Could not load garage helicopter prefab at '{prefabPath}'.");
                continue;
            }

            MeshFilter meshFilter = prefab.GetComponent<MeshFilter>();
            MeshRenderer meshRenderer = prefab.GetComponent<MeshRenderer>();
            if (meshFilter == null || meshRenderer == null || meshFilter.sharedMesh == null)
            {
                Debug.LogError($"Garage thumbnail builder could not find mesh components on '{prefabPath}'.");
                continue;
            }

            Texture2D thumbnail = RenderThumbnail(meshFilter.sharedMesh, meshRenderer.sharedMaterials, GetTint(i));

            string absoluteOutputPath = Path.Combine(Application.dataPath, $"Resources/Garage/HelicopterThumbnails/{helicopterName}.png");
            File.WriteAllBytes(absoluteOutputPath, thumbnail.EncodeToPNG());

            Object.DestroyImmediate(thumbnail);
        }

        AssetDatabase.Refresh();
        Debug.Log("Garage helicopter thumbnails rebuilt.");
    }

    private static Texture2D RenderThumbnail(Mesh mesh, Material[] sourceMaterials, Color tint)
    {
        PreviewRenderUtility previewRenderUtility = new PreviewRenderUtility();
        previewRenderUtility.camera.clearFlags = CameraClearFlags.SolidColor;
        previewRenderUtility.camera.backgroundColor = new Color(0.03f, 0.05f, 0.08f, 0f);
        previewRenderUtility.camera.fieldOfView = ThumbnailFieldOfView;
        previewRenderUtility.camera.nearClipPlane = 0.1f;
        previewRenderUtility.camera.farClipPlane = 200f;

        previewRenderUtility.lights[0].intensity = 1.35f;
        previewRenderUtility.lights[0].transform.rotation = Quaternion.Euler(40f, -35f, 0f);
        previewRenderUtility.lights[1].intensity = 0.7f;
        previewRenderUtility.lights[1].transform.rotation = Quaternion.Euler(20f, 145f, 0f);

        Matrix4x4 rotationScaleMatrix = Matrix4x4.TRS(Vector3.zero, PreviewRotation, PreviewScale);
        Bounds transformedBounds = TransformBounds(mesh.bounds, rotationScaleMatrix);
        float radius = Mathf.Max(
            0.75f,
            transformedBounds.extents.x,
            transformedBounds.extents.y,
            transformedBounds.extents.z);

        float distance = radius / Mathf.Tan(previewRenderUtility.camera.fieldOfView * 0.5f * Mathf.Deg2Rad) * ThumbnailDistanceMultiplier;
        previewRenderUtility.camera.transform.position = new Vector3(0f, radius * ThumbnailCameraHeightFactor, -distance);
        previewRenderUtility.camera.transform.rotation = Quaternion.Euler(3f, 0f, 0f);

        previewRenderUtility.BeginStaticPreview(new Rect(0f, 0f, 512f, 512f));
        Matrix4x4 matrix = Matrix4x4.TRS(-transformedBounds.center, PreviewRotation, PreviewScale);

        Material[] materials = CreateTintedMaterials(sourceMaterials, tint);
        int subMeshCount = Mathf.Min(mesh.subMeshCount, materials.Length);
        for (int i = 0; i < subMeshCount; i++)
        {
            previewRenderUtility.DrawMesh(mesh, matrix, materials[i], i);
        }

        previewRenderUtility.Render(true);
        Texture2D texture = previewRenderUtility.EndStaticPreview();

        for (int i = 0; i < materials.Length; i++)
        {
            Object.DestroyImmediate(materials[i]);
        }

        previewRenderUtility.Cleanup();
        return texture;
    }

    private static Bounds TransformBounds(Bounds localBounds, Matrix4x4 matrix)
    {
        Vector3 center = localBounds.center;
        Vector3 extents = localBounds.extents;

        Vector3[] corners =
        {
            center + new Vector3(extents.x, extents.y, extents.z),
            center + new Vector3(extents.x, extents.y, -extents.z),
            center + new Vector3(extents.x, -extents.y, extents.z),
            center + new Vector3(extents.x, -extents.y, -extents.z),
            center + new Vector3(-extents.x, extents.y, extents.z),
            center + new Vector3(-extents.x, extents.y, -extents.z),
            center + new Vector3(-extents.x, -extents.y, extents.z),
            center + new Vector3(-extents.x, -extents.y, -extents.z),
        };

        Vector3 transformedPoint = matrix.MultiplyPoint3x4(corners[0]);
        Bounds transformedBounds = new Bounds(transformedPoint, Vector3.zero);
        for (int i = 1; i < corners.Length; i++)
        {
            transformedBounds.Encapsulate(matrix.MultiplyPoint3x4(corners[i]));
        }

        return transformedBounds;
    }

    private static Material[] CreateTintedMaterials(Material[] sourceMaterials, Color tint)
    {
        Material[] materialCopies = new Material[sourceMaterials.Length];
        for (int i = 0; i < sourceMaterials.Length; i++)
        {
            Material source = sourceMaterials[i];
            if (source == null)
            {
                materialCopies[i] = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                continue;
            }

            Material copy = new Material(source);
            if (copy.HasProperty("_BaseColor"))
            {
                copy.SetColor("_BaseColor", tint);
            }
            else if (copy.HasProperty("_Color"))
            {
                copy.SetColor("_Color", tint);
            }

            materialCopies[i] = copy;
        }

        return materialCopies;
    }

    private static Color GetTint(int index)
    {
        return index switch
        {
            1 => new Color(0.87f, 0.29f, 0.25f),
            2 => new Color(0.2f, 0.52f, 0.92f),
            3 => new Color(0.27f, 0.72f, 0.46f),
            4 => new Color(0.82f, 0.67f, 0.22f),
            5 => new Color(0.88f, 0.9f, 0.96f),
            6 => new Color(0.68f, 0.34f, 0.84f),
            7 => new Color(0.17f, 0.8f, 0.78f),
            _ => new Color(0.94f, 0.5f, 0.2f),
        };
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
}
#endif
