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

    private readonly Dictionary<int, MissilePoolLease> activeMissileLeases = new();
    private readonly Dictionary<int, Queue<ImpactInstance>> availableImpacts = new();
    private readonly List<ImpactInstance> activeImpacts = new();
    private MissilePoolLedger missileLedger;
    private SpecialHomingMissileController[] missileSlots;
    private bool missilePoolInitializationAttempted;
    private bool missilePoolInitialized;
    private bool missilePoolCorrupted;
    private bool disposed;

    public int TotalMissiles => missileLedger != null ? missileLedger.TotalCount : 0;
    public int AvailableMissiles => missileLedger != null ? missileLedger.AvailableCount : 0;
    public int ReservedMissiles => missileLedger != null ? missileLedger.ReservedCount : 0;
    public int LeasedMissiles => missileLedger != null ? missileLedger.LeasedCount : 0;
    public int CreatedMissiles
    {
        get
        {
            if (missileSlots == null)
            {
                return 0;
            }

            int count = 0;
            for (int i = 0; i < missileSlots.Length; i++)
            {
                if (missileSlots[i] != null)
                {
                    count++;
                }
            }

            return count;
        }
    }

    public bool HasValidMissileCounts =>
        missilePoolInitialized &&
        !missilePoolCorrupted &&
        missileLedger != null &&
        missileSlots != null &&
        missileSlots.Length == missileLedger.TotalCount &&
        CreatedMissiles == missileLedger.TotalCount &&
        activeMissileLeases.Count == missileLedger.LeasedCount &&
        missileLedger.HasValidCounts;

    public static SpecialMissilePool Create(Transform owner)
    {
        GameObject poolObject = new("SpecialMissilePool");
        poolObject.transform.SetParent(owner, false);
        return poolObject.AddComponent<SpecialMissilePool>();
    }

    public bool InitializeFixedCapacity(int capacity, int maximumReservationSize)
    {
        if (disposed || capacity <= 0 || maximumReservationSize <= 0 || maximumReservationSize > capacity)
        {
            return false;
        }

        if (missilePoolInitialized)
        {
            return missileLedger != null &&
                   missileLedger.Capacity == capacity &&
                   missileLedger.MaximumReservationSize == maximumReservationSize &&
                   HasValidMissileCounts;
        }

        if (missilePoolInitializationAttempted)
        {
            return false;
        }

        missilePoolInitializationAttempted = true;
        missileLedger = new MissilePoolLedger(capacity, maximumReservationSize);
        missileSlots = new SpecialHomingMissileController[capacity];
        for (int slotId = 0; slotId < capacity; slotId++)
        {
            SpecialHomingMissileController missile = CreateMissile(slotId);
            if (missile == null)
            {
                Debug.LogError($"Failed to create fixed missile pool slot {slotId} of {capacity}.", this);
                return false;
            }

            missile.gameObject.SetActive(false);
            missileSlots[slotId] = missile;
        }

        missilePoolInitialized = true;
        return HasValidMissileCounts;
    }

    public bool TryReserve(
        int missileCount,
        out MissilePoolReservation reservation,
        out MissilePoolReservationFailure failure)
    {
        reservation = null;
        if (disposed || !missilePoolInitialized || missileLedger == null || !HasValidMissileCounts)
        {
            failure = MissilePoolReservationFailure.PoolCapacityUnavailable;
            return false;
        }

        return missileLedger.TryReserve(missileCount, out reservation, out failure);
    }

    public bool TryLeaseReserved(
        MissilePoolReservation reservation,
        out SpecialHomingMissileController missile)
    {
        missile = null;
        if (disposed || !missilePoolInitialized || missileLedger == null || missileSlots == null)
        {
            return false;
        }

        if (!missileLedger.TryLeaseReserved(reservation, out MissilePoolLease lease))
        {
            return false;
        }

        int slotId = lease.SlotId;
        if (slotId < 0 || slotId >= missileSlots.Length || missileSlots[slotId] == null)
        {
            missileLedger.ReturnLeased(lease);
            Debug.LogError($"Fixed missile pool slot {slotId} is unavailable.", this);
            return false;
        }

        missile = missileSlots[slotId];
        int instanceId = missile.GetInstanceID();
        if (activeMissileLeases.ContainsKey(instanceId))
        {
            missileLedger.ReturnLeased(lease);
            missile = null;
            Debug.LogError($"Fixed missile pool slot {slotId} was leased twice.", this);
            return false;
        }

        activeMissileLeases.Add(instanceId, lease);
        missile.transform.SetParent(null, true);
        missile.gameObject.SetActive(true);
        return true;
    }

    public bool ReleaseUnusedReservation(MissilePoolReservation reservation, out int releasedCount)
    {
        releasedCount = 0;
        return !disposed &&
               missileLedger != null &&
               missileLedger.ReleaseUnusedReservation(reservation, out releasedCount);
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

        int instanceId = missile.GetInstanceID();
        if (!activeMissileLeases.Remove(instanceId, out MissilePoolLease lease) ||
            missileLedger == null ||
            !missileLedger.ReturnLeased(lease))
        {
            Debug.LogError("Rejected duplicate or foreign missile return.", this);
            return;
        }

        missile.PrepareForPool();
        missile.transform.SetParent(transform, false);
        missile.gameObject.SetActive(false);
    }

    internal void NotifyMissileDestroyedOutsidePool(SpecialHomingMissileController missile)
    {
        if (disposed ||
            missile == null ||
            !Application.isPlaying ||
            gameObject == null ||
            !gameObject.scene.isLoaded)
        {
            return;
        }

        missilePoolCorrupted = true;
        Debug.LogError(
            $"A fixed-pool missile was destroyed outside the pool. Runtime replacement is forbidden; subsequent reservations will fail. Missile={missile.name}",
            this);
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
        DestroyDetachedMissiles();
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

    private SpecialHomingMissileController CreateMissile(int slotId)
    {
        GameObject missileObject = new($"PlayerSpecialMissileRuntime_{slotId:00}");
        missileObject.transform.SetParent(transform, false);
        SpecialHomingMissileController missile = missileObject.AddComponent<SpecialHomingMissileController>();
        missile.SetPool(this);
        return missile;
    }

    private void DestroyDetachedMissiles()
    {
        if (missileSlots == null)
        {
            return;
        }

        for (int i = 0; i < missileSlots.Length; i++)
        {
            SpecialHomingMissileController missile = missileSlots[i];
            if (missile != null && missile.transform.parent != transform)
            {
                Destroy(missile.gameObject);
            }
        }
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
        DestroyDetachedMissiles();
        activeMissileLeases.Clear();
        missileSlots = null;
        missileLedger = null;
        missilePoolInitializationAttempted = false;
        missilePoolInitialized = false;
        missilePoolCorrupted = false;
        activeImpacts.Clear();
        availableImpacts.Clear();
    }
}
