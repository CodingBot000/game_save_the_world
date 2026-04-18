using System;
using System.IO;
using UnityEditor;
using UnityEngine;

public static class MissileProjectilePrefabBuilder
{
    private const string ViperSourcePrefabPath = "Assets/Prefabs/Combat/ViperSidewinderMissile.prefab";
    private const string ViperOutputMeshPath = "Assets/Prefabs/Combat/ViperSidewinderProjectileMesh.asset";
    private const string ViperOutputPrefabPath = "Assets/Prefabs/Combat/ViperSidewinderProjectile.prefab";

    [MenuItem("Tools/Titan Destroyer/Rebuild Viper Sidewinder Projectile")]
    public static void RebuildViperSidewinderProjectile()
    {
        BuildStaticProjectilePrefab(
            ViperSourcePrefabPath,
            ViperOutputMeshPath,
            ViperOutputPrefabPath,
            "ViperSidewinderProjectile");
    }

    public static GameObject BuildStaticProjectilePrefab(
        string sourcePrefabPath,
        string outputMeshPath,
        string outputPrefabPath,
        string prefabName)
    {
        GameObject sourcePrefab = AssetDatabase.LoadAssetAtPath<GameObject>(sourcePrefabPath);
        if (sourcePrefab == null)
        {
            throw new InvalidOperationException($"Missile source prefab not found: {sourcePrefabPath}");
        }

        Directory.CreateDirectory(Path.GetDirectoryName(outputMeshPath) ?? "Assets");
        Directory.CreateDirectory(Path.GetDirectoryName(outputPrefabPath) ?? "Assets");

        GameObject sourceInstance = PrefabUtility.InstantiatePrefab(sourcePrefab) as GameObject;
        if (sourceInstance == null)
        {
            throw new InvalidOperationException($"Failed to instantiate missile source prefab: {sourcePrefabPath}");
        }

        sourceInstance.hideFlags = HideFlags.HideAndDontSave;
        sourceInstance.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
        sourceInstance.transform.localScale = Vector3.one;

        GameObject projectileRoot = null;

        try
        {
            Renderer sourceRenderer = FindPrimaryRenderer(sourceInstance);
            if (sourceRenderer == null)
            {
                throw new InvalidOperationException($"No renderer found in missile source prefab: {sourcePrefabPath}");
            }

            Mesh bakedMesh = ExtractMeshInRootSpace(sourceInstance.transform, sourceRenderer, $"{prefabName}Mesh");
            Mesh outputMesh = SaveMeshAsset(bakedMesh, outputMeshPath);

            projectileRoot = new GameObject(prefabName);

            GameObject visual = new GameObject("MissileVisual");
            visual.transform.SetParent(projectileRoot.transform, false);
            visual.transform.localPosition = Vector3.zero;
            visual.transform.localRotation = Quaternion.identity;
            visual.transform.localScale = Vector3.one;

            MeshFilter meshFilter = visual.AddComponent<MeshFilter>();
            meshFilter.sharedMesh = outputMesh;

            MeshRenderer meshRenderer = visual.AddComponent<MeshRenderer>();
            meshRenderer.sharedMaterials = sourceRenderer.sharedMaterials;

            Bounds bounds = outputMesh.bounds;
            GameObject exhaustAnchor = new GameObject("MissileExhaustAnchor");
            exhaustAnchor.transform.SetParent(projectileRoot.transform, false);
            exhaustAnchor.transform.localPosition = new Vector3(bounds.center.x, bounds.center.y, bounds.min.z - 0.02f);
            exhaustAnchor.transform.localRotation = Quaternion.identity;

            GameObject savedPrefab = PrefabUtility.SaveAsPrefabAsset(projectileRoot, outputPrefabPath);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            return savedPrefab;
        }
        finally
        {
            if (sourceInstance != null)
            {
                UnityEngine.Object.DestroyImmediate(sourceInstance);
            }

            if (projectileRoot != null)
            {
                UnityEngine.Object.DestroyImmediate(projectileRoot);
            }
        }
    }

    private static Renderer FindPrimaryRenderer(GameObject root)
    {
        Renderer[] renderers = root.GetComponentsInChildren<Renderer>(true);
        if (renderers == null || renderers.Length == 0)
        {
            return null;
        }

        Renderer selected = renderers[0];
        float largestMagnitude = selected.bounds.size.sqrMagnitude;
        for (int i = 1; i < renderers.Length; i++)
        {
            Renderer candidate = renderers[i];
            float candidateMagnitude = candidate.bounds.size.sqrMagnitude;
            if (candidateMagnitude > largestMagnitude)
            {
                selected = candidate;
                largestMagnitude = candidateMagnitude;
            }
        }

        return selected;
    }

    private static Mesh ExtractMeshInRootSpace(Transform rootTransform, Renderer sourceRenderer, string meshName)
    {
        Mesh mesh = new Mesh
        {
            name = meshName,
            indexFormat = UnityEngine.Rendering.IndexFormat.UInt32,
        };

        switch (sourceRenderer)
        {
            case SkinnedMeshRenderer skinnedMeshRenderer:
                skinnedMeshRenderer.BakeMesh(mesh);
                ApplyTransform(mesh, rootTransform.worldToLocalMatrix * skinnedMeshRenderer.transform.localToWorldMatrix);
                break;

            case MeshRenderer meshRenderer:
            {
                MeshFilter meshFilter = meshRenderer.GetComponent<MeshFilter>();
                if (meshFilter == null || meshFilter.sharedMesh == null)
                {
                    throw new InvalidOperationException($"Mesh renderer is missing a mesh filter: {meshRenderer.name}");
                }

                EditorUtility.CopySerialized(meshFilter.sharedMesh, mesh);
                ApplyTransform(mesh, rootTransform.worldToLocalMatrix * meshRenderer.transform.localToWorldMatrix);
                break;
            }

            default:
                throw new InvalidOperationException($"Unsupported renderer type: {sourceRenderer.GetType().Name}");
        }

        mesh.RecalculateBounds();
        return mesh;
    }

    private static Mesh SaveMeshAsset(Mesh sourceMesh, string outputMeshPath)
    {
        Mesh existingMesh = AssetDatabase.LoadAssetAtPath<Mesh>(outputMeshPath);
        if (existingMesh == null)
        {
            AssetDatabase.CreateAsset(sourceMesh, outputMeshPath);
            return sourceMesh;
        }

        EditorUtility.CopySerialized(sourceMesh, existingMesh);
        UnityEngine.Object.DestroyImmediate(sourceMesh);
        EditorUtility.SetDirty(existingMesh);
        return existingMesh;
    }

    private static void ApplyTransform(Mesh mesh, Matrix4x4 matrix)
    {
        Vector3[] vertices = mesh.vertices;
        for (int i = 0; i < vertices.Length; i++)
        {
            vertices[i] = matrix.MultiplyPoint3x4(vertices[i]);
        }

        mesh.vertices = vertices;

        Vector3[] normals = mesh.normals;
        if (normals != null && normals.Length == vertices.Length)
        {
            Matrix4x4 normalMatrix = matrix.inverse.transpose;
            for (int i = 0; i < normals.Length; i++)
            {
                normals[i] = normalMatrix.MultiplyVector(normals[i]).normalized;
            }

            mesh.normals = normals;
        }

        Vector4[] tangents = mesh.tangents;
        if (tangents != null && tangents.Length == vertices.Length)
        {
            Matrix4x4 normalMatrix = matrix.inverse.transpose;
            for (int i = 0; i < tangents.Length; i++)
            {
                Vector3 tangentDirection = new Vector3(tangents[i].x, tangents[i].y, tangents[i].z);
                tangentDirection = normalMatrix.MultiplyVector(tangentDirection).normalized;
                tangents[i] = new Vector4(tangentDirection.x, tangentDirection.y, tangentDirection.z, tangents[i].w);
            }

            mesh.tangents = tangents;
        }
    }
}
