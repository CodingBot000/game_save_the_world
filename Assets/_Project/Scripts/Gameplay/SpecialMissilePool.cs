using System.Collections.Generic;
using UnityEngine;

public sealed class SpecialMissilePool : MonoBehaviour
{
    private static Material sharedFallbackImpactMaterial;

    private sealed class ImpactInstance
    {
        public GameObject GameObject;
        public Vector3 BaseLocalScale;
        public ParticleSystem[] ParticleSystems;
        public int TemplateId;
        public float ReleaseTime;
    }

    private readonly Queue<SpecialHomingMissileController> availableMissiles = new();
    private readonly Dictionary<int, Queue<ImpactInstance>> availableImpacts = new();
    private readonly List<ImpactInstance> activeImpacts = new();
    private bool disposed;

    public static SpecialMissilePool Create(Transform owner)
    {
        GameObject poolObject = new("SpecialMissilePool");
        poolObject.transform.SetParent(owner, false);
        return poolObject.AddComponent<SpecialMissilePool>();
    }

    public void Prewarm(int count)
    {
        int targetCount = Mathf.Max(0, count);
        while (!disposed && availableMissiles.Count < targetCount)
        {
            SpecialHomingMissileController missile = CreateMissile();
            missile.gameObject.SetActive(false);
            availableMissiles.Enqueue(missile);
        }
    }

    public void PrewarmImpacts(GameObject template, int count)
    {
        if (disposed || template == null)
        {
            return;
        }

        int templateId = template.GetInstanceID();
        Queue<ImpactInstance> queue = GetImpactQueue(templateId);
        int targetCount = Mathf.Max(0, count);
        while (queue.Count < targetCount)
        {
            queue.Enqueue(CreateImpact(template, templateId));
        }
    }

    public SpecialHomingMissileController Get()
    {
        if (disposed)
        {
            return null;
        }

        SpecialHomingMissileController missile = null;
        while (availableMissiles.Count > 0 && missile == null)
        {
            missile = availableMissiles.Dequeue();
        }

        if (missile == null)
        {
            missile = CreateMissile();
        }

        missile.transform.SetParent(null, true);
        missile.gameObject.SetActive(true);
        return missile;
    }

    internal void Release(SpecialHomingMissileController missile)
    {
        if (missile == null)
        {
            return;
        }

        if (disposed || this == null)
        {
            Destroy(missile.gameObject);
            return;
        }

        missile.PrepareForPool();
        missile.transform.SetParent(transform, false);
        missile.gameObject.SetActive(false);
        availableMissiles.Enqueue(missile);
    }

    internal void SpawnImpact(
        GameObject template,
        Vector3 position,
        Quaternion rotation,
        float scale,
        float lifetime = 4f)
    {
        if (disposed || template == null)
        {
            return;
        }

        int templateId = template.GetInstanceID();
        Queue<ImpactInstance> queue = GetImpactQueue(templateId);
        ImpactInstance impact = null;
        while (queue.Count > 0 && (impact == null || impact.GameObject == null))
        {
            impact = queue.Dequeue();
        }

        if (impact == null || impact.GameObject == null)
        {
            impact = CreateImpact(template, templateId);
        }

        Transform impactTransform = impact.GameObject.transform;
        impactTransform.SetParent(transform, false);
        impactTransform.SetPositionAndRotation(position, rotation);
        impactTransform.localScale = impact.BaseLocalScale * Mathf.Max(0.01f, scale);
        impact.GameObject.SetActive(true);
        RestartParticleSystems(impact);
        impact.ReleaseTime = Time.time + Mathf.Max(0.1f, lifetime);
        activeImpacts.Add(impact);
    }

    public void Dispose()
    {
        if (disposed)
        {
            return;
        }

        disposed = true;
        if (this != null && gameObject != null)
        {
            Destroy(gameObject);
        }
    }

    private void Update()
    {
        for (int i = activeImpacts.Count - 1; i >= 0; i--)
        {
            ImpactInstance impact = activeImpacts[i];
            if (impact == null || impact.GameObject == null)
            {
                activeImpacts.RemoveAt(i);
                continue;
            }

            if (Time.time < impact.ReleaseTime)
            {
                continue;
            }

            activeImpacts.RemoveAt(i);
            StopParticleSystems(impact);
            impact.GameObject.SetActive(false);
            GetImpactQueue(impact.TemplateId).Enqueue(impact);
        }
    }

    private SpecialHomingMissileController CreateMissile()
    {
        GameObject missileObject = new("PlayerSpecialMissileRuntime");
        missileObject.transform.SetParent(transform, false);
        SpecialHomingMissileController missile = missileObject.AddComponent<SpecialHomingMissileController>();
        missile.SetPool(this);
        return missile;
    }

    private ImpactInstance CreateImpact(GameObject template, int templateId)
    {
        GameObject instance = null;
        if (!string.IsNullOrWhiteSpace(template.name))
        {
            int childCountBefore = transform.childCount;
            try
            {
                Object instantiatedObject = Instantiate((Object)template, transform);
                instance = ResolveInstantiatedGameObject(instantiatedObject);
                if (instance == null && transform.childCount > childCountBefore)
                {
                    instance = transform.GetChild(transform.childCount - 1).gameObject;
                }
            }
            catch (System.Exception)
            {
                instance = null;
            }
        }

        if (instance == null)
        {
            instance = CreateFallbackImpact();
        }

        instance.name = string.IsNullOrWhiteSpace(template.name)
            ? "SpecialMissileImpactFallback_Pooled"
            : $"{template.name}_Pooled";
        ImpactInstance impact = new()
        {
            GameObject = instance,
            BaseLocalScale = instance.transform.localScale,
            ParticleSystems = instance.GetComponentsInChildren<ParticleSystem>(true),
            TemplateId = templateId,
        };
        StopParticleSystems(impact);
        instance.SetActive(false);
        return impact;
    }

    private GameObject CreateFallbackImpact()
    {
        GameObject impactObject = new("SpecialMissileImpactFallback");
        impactObject.transform.SetParent(transform, false);
        impactObject.transform.localScale = Vector3.one * 12.5f;

        ParticleSystem particles = impactObject.AddComponent<ParticleSystem>();
        particles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

        ParticleSystem.MainModule main = particles.main;
        main.loop = false;
        main.playOnAwake = false;
        main.duration = 0.55f;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.22f, 0.48f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(4f, 9f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.35f, 1.1f);
        main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
        main.maxParticles = 48;

        ParticleSystem.EmissionModule emission = particles.emission;
        emission.enabled = true;
        emission.rateOverTime = 0f;
        emission.SetBursts(new[] { new ParticleSystem.Burst(0f, 26) });

        ParticleSystem.ShapeModule shape = particles.shape;
        shape.enabled = true;
        shape.shapeType = ParticleSystemShapeType.Sphere;
        shape.radius = 0.22f;
        shape.radiusThickness = 1f;

        Gradient colorGradient = new();
        colorGradient.SetKeys(
            new[]
            {
                new GradientColorKey(new Color(1f, 1f, 0.92f), 0f),
                new GradientColorKey(new Color(1f, 0.48f, 0.08f), 0.38f),
                new GradientColorKey(new Color(0.1f, 0.8f, 1f), 1f),
            },
            new[]
            {
                new GradientAlphaKey(1f, 0f),
                new GradientAlphaKey(0.9f, 0.45f),
                new GradientAlphaKey(0f, 1f),
            });
        ParticleSystem.ColorOverLifetimeModule colorOverLifetime = particles.colorOverLifetime;
        colorOverLifetime.enabled = true;
        colorOverLifetime.color = new ParticleSystem.MinMaxGradient(colorGradient);

        AnimationCurve sizeCurve = new();
        sizeCurve.AddKey(0f, 0.25f);
        sizeCurve.AddKey(0.18f, 1f);
        sizeCurve.AddKey(1f, 0f);
        ParticleSystem.SizeOverLifetimeModule sizeOverLifetime = particles.sizeOverLifetime;
        sizeOverLifetime.enabled = true;
        sizeOverLifetime.size = new ParticleSystem.MinMaxCurve(1f, sizeCurve);

        ParticleSystemRenderer renderer = particles.GetComponent<ParticleSystemRenderer>();
        renderer.renderMode = ParticleSystemRenderMode.Billboard;
        renderer.maxParticleSize = 1.5f;
        renderer.sharedMaterial = GetFallbackImpactMaterial();
        return impactObject;
    }

    private static Material GetFallbackImpactMaterial()
    {
        if (sharedFallbackImpactMaterial != null)
        {
            return sharedFallbackImpactMaterial;
        }

        Shader shader = Shader.Find("Universal Render Pipeline/Particles/Unlit") ??
                        Shader.Find("Particles/Standard Unlit") ??
                        Shader.Find("Sprites/Default");
        if (shader == null)
        {
            return null;
        }

        sharedFallbackImpactMaterial = new Material(shader)
        {
            name = "SharedSpecialMissileImpactMaterial",
            hideFlags = HideFlags.HideAndDontSave,
            renderQueue = 3000,
        };
        if (sharedFallbackImpactMaterial.HasProperty("_Surface"))
        {
            sharedFallbackImpactMaterial.SetFloat("_Surface", 1f);
        }

        if (sharedFallbackImpactMaterial.HasProperty("_SrcBlend"))
        {
            sharedFallbackImpactMaterial.SetFloat("_SrcBlend", 5f);
        }

        if (sharedFallbackImpactMaterial.HasProperty("_DstBlend"))
        {
            sharedFallbackImpactMaterial.SetFloat("_DstBlend", 10f);
        }

        if (sharedFallbackImpactMaterial.HasProperty("_ZWrite"))
        {
            sharedFallbackImpactMaterial.SetFloat("_ZWrite", 0f);
        }

        if (sharedFallbackImpactMaterial.HasProperty("_BaseColor"))
        {
            sharedFallbackImpactMaterial.SetColor("_BaseColor", Color.white);
        }

        if (sharedFallbackImpactMaterial.HasProperty("_Color"))
        {
            sharedFallbackImpactMaterial.SetColor("_Color", Color.white);
        }

        return sharedFallbackImpactMaterial;
    }

    private static GameObject ResolveInstantiatedGameObject(Object instance)
    {
        if (instance is GameObject gameObject)
        {
            return gameObject;
        }

        if (instance is Component component)
        {
            return component.gameObject;
        }

        return null;
    }

    private Queue<ImpactInstance> GetImpactQueue(int templateId)
    {
        if (!availableImpacts.TryGetValue(templateId, out Queue<ImpactInstance> queue))
        {
            queue = new Queue<ImpactInstance>();
            availableImpacts.Add(templateId, queue);
        }

        return queue;
    }

    private static void RestartParticleSystems(ImpactInstance impact)
    {
        ParticleSystem[] systems = impact.ParticleSystems;
        for (int i = 0; i < systems.Length; i++)
        {
            systems[i].Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            systems[i].Play(true);
        }
    }

    private static void StopParticleSystems(ImpactInstance impact)
    {
        ParticleSystem[] systems = impact.ParticleSystems;
        for (int i = 0; i < systems.Length; i++)
        {
            systems[i].Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        }
    }

    private void OnDestroy()
    {
        disposed = true;
        availableMissiles.Clear();
        activeImpacts.Clear();
        availableImpacts.Clear();
    }
}
