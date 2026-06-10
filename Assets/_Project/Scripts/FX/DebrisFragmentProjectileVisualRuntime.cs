using UnityEngine;

public sealed class DebrisFragmentProjectileVisualRuntime : MonoBehaviour
{
    private const float TrailLifetime = 0.32f;
    private const float TrailMinVertexDistance = 0.04f;
    private const float TrailStartWidthMultiplier = 1.35f;
    private const float TrailEndWidthMultiplier = 0.2f;

    private static Material sharedTrailMaterial;

    private Transform visualRoot;
    private Vector3 spinDegreesPerSecond;

    public void Configure(GameObject fragmentPrefab, float targetVisualRadius)
    {
        if (fragmentPrefab == null)
        {
            return;
        }

        DisableExistingRenderers();

        GameObject visualInstance = Instantiate(fragmentPrefab, transform);
        visualInstance.name = "DebrisFragmentVisual";
        visualRoot = visualInstance.transform;
        visualRoot.localPosition = Vector3.zero;
        visualRoot.localRotation = Random.rotation;
        visualRoot.localScale = Vector3.one;

        DisableVisualColliders(visualRoot);
        NormalizeVisualScale(Mathf.Max(0.02f, targetVisualRadius));
        ConfigureTrail(Mathf.Max(0.02f, targetVisualRadius));

        float spinSpeed = Random.Range(260f, 720f);
        spinDegreesPerSecond = Random.onUnitSphere * spinSpeed;
    }

    private void Update()
    {
        if (visualRoot == null)
        {
            Destroy(this);
            return;
        }

        visualRoot.Rotate(spinDegreesPerSecond * Time.deltaTime, Space.Self);
    }

    private void DisableExistingRenderers()
    {
        Renderer[] renderers = GetComponentsInChildren<Renderer>();
        for (int i = 0; i < renderers.Length; i++)
        {
            renderers[i].enabled = false;
        }
    }

    private static void DisableVisualColliders(Transform root)
    {
        Collider[] colliders = root.GetComponentsInChildren<Collider>();
        for (int i = 0; i < colliders.Length; i++)
        {
            colliders[i].enabled = false;
        }
    }

    private void NormalizeVisualScale(float targetVisualRadius)
    {
        Bounds bounds = default;
        bool hasBounds = false;
        Renderer[] renderers = visualRoot.GetComponentsInChildren<Renderer>();
        for (int i = 0; i < renderers.Length; i++)
        {
            if (!renderers[i].enabled)
            {
                renderers[i].enabled = true;
            }

            if (!hasBounds)
            {
                bounds = renderers[i].bounds;
                hasBounds = true;
            }
            else
            {
                bounds.Encapsulate(renderers[i].bounds);
            }
        }

        if (!hasBounds)
        {
            return;
        }

        float currentRadius = Mathf.Max(bounds.extents.x, bounds.extents.y, bounds.extents.z);
        if (currentRadius <= 0.0001f)
        {
            return;
        }

        float scaleMultiplier = targetVisualRadius / currentRadius;
        visualRoot.localScale *= scaleMultiplier;
    }

    private void ConfigureTrail(float targetVisualRadius)
    {
        TrailRenderer trailRenderer = GetComponent<TrailRenderer>();
        if (trailRenderer == null)
        {
            trailRenderer = gameObject.AddComponent<TrailRenderer>();
        }

        trailRenderer.time = TrailLifetime;
        trailRenderer.minVertexDistance = TrailMinVertexDistance;
        trailRenderer.startWidth = Mathf.Max(0.02f, targetVisualRadius * TrailStartWidthMultiplier);
        trailRenderer.endWidth = Mathf.Max(0.005f, targetVisualRadius * TrailEndWidthMultiplier);
        trailRenderer.numCornerVertices = 2;
        trailRenderer.numCapVertices = 2;
        trailRenderer.textureMode = LineTextureMode.Stretch;
        trailRenderer.alignment = LineAlignment.View;
        trailRenderer.colorGradient = CreateTrailGradient();
        trailRenderer.sharedMaterial = ResolveTrailMaterial();
        trailRenderer.emitting = true;
        trailRenderer.Clear();
    }

    private static Gradient CreateTrailGradient()
    {
        Gradient gradient = new();
        gradient.SetKeys(
            new[]
            {
                new GradientColorKey(new Color(1f, 0.9f, 0.35f), 0f),
                new GradientColorKey(new Color(1f, 0.45f, 0.08f), 0.45f),
                new GradientColorKey(new Color(0.95f, 0.12f, 0.02f), 1f),
            },
            new[]
            {
                new GradientAlphaKey(0.85f, 0f),
                new GradientAlphaKey(0.42f, 0.55f),
                new GradientAlphaKey(0f, 1f),
            });
        return gradient;
    }

    private static Material ResolveTrailMaterial()
    {
        if (sharedTrailMaterial != null)
        {
            return sharedTrailMaterial;
        }

        Shader shader = Shader.Find("Sprites/Default");
        shader ??= Shader.Find("Universal Render Pipeline/Particles/Unlit");
        shader ??= Shader.Find("Particles/Standard Unlit");
        if (shader == null)
        {
            return null;
        }

        sharedTrailMaterial = new Material(shader)
        {
            name = "RuntimeDebrisFragmentTrailMaterial",
            hideFlags = HideFlags.HideAndDontSave,
            renderQueue = 3000,
        };

        Color color = new(1f, 0.62f, 0.08f, 0.78f);
        if (sharedTrailMaterial.HasProperty("_Surface"))
        {
            sharedTrailMaterial.SetFloat("_Surface", 1f);
        }

        if (sharedTrailMaterial.HasProperty("_SrcBlend"))
        {
            sharedTrailMaterial.SetFloat("_SrcBlend", 5f);
        }

        if (sharedTrailMaterial.HasProperty("_DstBlend"))
        {
            sharedTrailMaterial.SetFloat("_DstBlend", 10f);
        }

        if (sharedTrailMaterial.HasProperty("_ZWrite"))
        {
            sharedTrailMaterial.SetFloat("_ZWrite", 0f);
        }

        if (sharedTrailMaterial.HasProperty("_BaseColor"))
        {
            sharedTrailMaterial.SetColor("_BaseColor", color);
        }

        if (sharedTrailMaterial.HasProperty("_Color"))
        {
            sharedTrailMaterial.SetColor("_Color", color);
        }

        return sharedTrailMaterial;
    }
}
