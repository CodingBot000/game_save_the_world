using System.Collections.Generic;
using UnityEngine;

public class HomingMissileController : MonoBehaviour
{
    private enum MissilePhase
    {
        Straight,
        Turning,
        Boost,
    }

    private const string ExhaustAnchorName = "MissileExhaustAnchor";

    private BattleController battleController;
    private Transform targetTransform;
    private ProjectileTeam team;
    private MissilePhase phase;
    private float currentSpeed;
    private float cruiseSpeed;
    private float acceleration;
    private float turnRate;
    private float straightPhaseDuration;
    private float straightPhaseDistance;
    private float turnPhaseDuration;
    private float boostPhaseDuration;
    private float phaseElapsed;
    private float remainingLifetime;
    private float damage;
    private float hitRadius;
    private Vector3 straightPhaseStartPosition;
    private Vector3 straightDirection;
    private Vector3 boostDirection;
    private Quaternion turnStartRotation;
    private Quaternion turnTargetRotation;
    private Transform exhaustAnchor;
    private TrailRenderer trailRenderer;
    private ParticleSystem smokeTrail;
    private readonly List<Material> runtimeMaterials = new();

    public void Launch(
        BattleController owner,
        Transform target,
        ProjectileTeam projectileTeam,
        Vector3 initialDirection,
        float launchSpeed,
        float maxSpeed,
        float accelerationRate,
        float turnRateDegrees,
        float lockOnDelay,
        float straightDuration,
        float straightDistance,
        float turnDuration,
        float boostDuration,
        float lifetime,
        float damageAmount,
        float projectileHitRadius,
        GameObject visualTemplate = null)
    {
        battleController = owner;
        targetTransform = target;
        team = projectileTeam;

        Vector3 launchDirection = initialDirection.sqrMagnitude > 0.001f ? initialDirection.normalized : Vector3.forward;
        transform.rotation = Quaternion.LookRotation(launchDirection, Vector3.up);

        currentSpeed = Mathf.Max(1f, launchSpeed);
        cruiseSpeed = Mathf.Max(currentSpeed, maxSpeed);
        acceleration = Mathf.Max(0f, accelerationRate);
        turnRate = Mathf.Max(0f, turnRateDegrees);
        straightDirection = launchDirection;
        boostDirection = launchDirection;
        straightPhaseDuration = Mathf.Max(0f, straightDuration, lockOnDelay);
        straightPhaseDistance = Mathf.Max(0f, straightDistance);
        straightPhaseStartPosition = transform.position;
        turnPhaseDuration = Mathf.Max(0f, turnDuration);
        boostPhaseDuration = Mathf.Max(0.01f, boostDuration);
        phaseElapsed = 0f;
        phase = MissilePhase.Straight;
        remainingLifetime = Mathf.Max(0.5f, lifetime);
        damage = Mathf.Max(1f, damageAmount);
        hitRadius = Mathf.Max(0.1f, projectileHitRadius);

        EnsureVisuals(visualTemplate);
    }

    private void Update()
    {
        if (battleController == null)
        {
            Destroy(gameObject);
            return;
        }

        float deltaTime = Time.deltaTime;
        remainingLifetime -= deltaTime;
        if (remainingLifetime <= 0f || transform.position.magnitude > 150f || transform.position.y < -20f)
        {
            Destroy(gameObject);
            return;
        }

        UpdateFlight(deltaTime);

        bool hit = team == ProjectileTeam.Player
            ? battleController.TryHitBoss(transform.position, hitRadius, damage)
            : battleController.TryHitPlayer(transform.position, hitRadius, damage);

        if (hit)
        {
            Destroy(gameObject);
        }
    }

    private void UpdateFlight(float deltaTime)
    {
        phaseElapsed += deltaTime;

        switch (phase)
        {
            case MissilePhase.Straight:
                transform.rotation = Quaternion.LookRotation(straightDirection, Vector3.up);
                float straightProgress = straightPhaseDuration > 0.001f
                    ? Mathf.Clamp01(phaseElapsed / straightPhaseDuration)
                    : 1f;
                transform.position = straightPhaseStartPosition + straightDirection * (straightPhaseDistance * straightProgress);
                if (phaseElapsed >= straightPhaseDuration)
                {
                    BeginTurnPhase();
                }

                break;

            case MissilePhase.Turning:
                float turnProgress = turnPhaseDuration > 0.001f
                    ? Mathf.Clamp01(phaseElapsed / turnPhaseDuration)
                    : 1f;
                transform.rotation = Quaternion.Slerp(turnStartRotation, turnTargetRotation, turnProgress);
                transform.position += transform.forward * (currentSpeed * deltaTime);
                if (turnProgress >= 1f)
                {
                    BeginBoostPhase();
                }

                break;

            case MissilePhase.Boost:
                float appliedAcceleration = phaseElapsed <= boostPhaseDuration ? acceleration : 0f;
                currentSpeed = Mathf.MoveTowards(currentSpeed, cruiseSpeed, appliedAcceleration * deltaTime);
                transform.rotation = Quaternion.LookRotation(boostDirection, Vector3.up);
                transform.position += boostDirection * (currentSpeed * deltaTime);
                break;
        }
    }

    private void BeginTurnPhase()
    {
        phase = MissilePhase.Turning;
        phaseElapsed = 0f;
        turnStartRotation = transform.rotation;
        Quaternion fullTurnTargetRotation = Quaternion.LookRotation(GetTargetDirection(straightDirection), Vector3.up);
        turnTargetRotation = Quaternion.Slerp(turnStartRotation, fullTurnTargetRotation, 2f / 3f);
    }

    private void BeginBoostPhase()
    {
        phase = MissilePhase.Boost;
        phaseElapsed = 0f;
        boostDirection = GetTargetDirection(transform.forward);
        transform.rotation = Quaternion.LookRotation(boostDirection, Vector3.up);
    }

    private Vector3 GetTargetDirection(Vector3 fallbackDirection)
    {
        if (targetTransform == null)
        {
            return fallbackDirection.sqrMagnitude > 0.001f ? fallbackDirection.normalized : Vector3.forward;
        }

        Vector3 desiredDirection = targetTransform.position - transform.position;
        if (desiredDirection.sqrMagnitude < 0.001f)
        {
            return fallbackDirection.sqrMagnitude > 0.001f ? fallbackDirection.normalized : Vector3.forward;
        }

        Vector3 resolvedDirection = desiredDirection.normalized;
        return turnRate > 0f
            ? Vector3.RotateTowards(fallbackDirection.normalized, resolvedDirection, Mathf.Deg2Rad * turnRate, 0f).normalized
            : resolvedDirection;
    }

    private void EnsureVisuals(GameObject visualTemplate)
    {
        if (transform.Find("MissileVisualRoot") != null)
        {
            return;
        }

        GameObject visualRoot = new("MissileVisualRoot");
        visualRoot.transform.SetParent(transform, false);
        visualRoot.transform.localPosition = Vector3.zero;
        visualRoot.transform.localRotation = Quaternion.identity;

        if (visualTemplate != null)
        {
            GameObject customVisual = Instantiate(visualTemplate, visualRoot.transform);
            customVisual.name = "MissileVisual";
            customVisual.transform.localPosition = Vector3.zero;
            customVisual.transform.localRotation = Quaternion.identity;
            customVisual.transform.localScale = Vector3.one;
        }
        else
        {
            CreateDefaultVisual(visualRoot.transform);
        }

        exhaustAnchor = ResolveExhaustAnchor(visualRoot.transform);
        EnsureTrailRenderer();
        EnsureSmokeTrail();
    }

    private void CreateDefaultVisual(Transform parent)
    {
        Material bodyMaterial = CreateRuntimeMaterial(
            "RuntimeMissileBodyMaterial",
            new Color(0.96f, 0.97f, 1f, 1f),
            false,
            "Universal Render Pipeline/Lit",
            "Standard",
            "Universal Render Pipeline/Unlit",
            "Sprites/Default");

        GameObject body = GameObject.CreatePrimitive(PrimitiveType.Capsule);
        body.name = "MissileVisual";
        body.transform.SetParent(parent, false);
        body.transform.localPosition = Vector3.zero;
        body.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
        body.transform.localScale = new Vector3(0.13f, 0.42f, 0.13f);
        Collider bodyCollider = body.GetComponent<Collider>();
        if (bodyCollider != null)
        {
            Destroy(bodyCollider);
        }

        ApplyMaterial(body.GetComponent<Renderer>(), bodyMaterial);

        GameObject nose = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        nose.name = "MissileNose";
        nose.transform.SetParent(parent, false);
        nose.transform.localPosition = new Vector3(0f, 0f, 0.38f);
        nose.transform.localRotation = Quaternion.identity;
        nose.transform.localScale = new Vector3(0.1f, 0.1f, 0.16f);
        Collider noseCollider = nose.GetComponent<Collider>();
        if (noseCollider != null)
        {
            Destroy(noseCollider);
        }

        ApplyMaterial(nose.GetComponent<Renderer>(), bodyMaterial);

        CreateFin(parent, bodyMaterial, "FinTop", new Vector3(0f, 0.06f, -0.2f), new Vector3(0.02f, 0.12f, 0.14f));
        CreateFin(parent, bodyMaterial, "FinBottom", new Vector3(0f, -0.06f, -0.2f), new Vector3(0.02f, 0.12f, 0.14f));
        CreateFin(parent, bodyMaterial, "FinLeft", new Vector3(-0.06f, 0f, -0.2f), new Vector3(0.12f, 0.02f, 0.14f));
        CreateFin(parent, bodyMaterial, "FinRight", new Vector3(0.06f, 0f, -0.2f), new Vector3(0.12f, 0.02f, 0.14f));
    }

    private void CreateFin(Transform parent, Material material, string name, Vector3 localPosition, Vector3 localScale)
    {
        GameObject fin = GameObject.CreatePrimitive(PrimitiveType.Cube);
        fin.name = name;
        fin.transform.SetParent(parent, false);
        fin.transform.localPosition = localPosition;
        fin.transform.localRotation = Quaternion.identity;
        fin.transform.localScale = localScale;
        Collider finCollider = fin.GetComponent<Collider>();
        if (finCollider != null)
        {
            Destroy(finCollider);
        }

        ApplyMaterial(fin.GetComponent<Renderer>(), material);
    }

    private void EnsureTrailRenderer()
    {
        trailRenderer = GetComponent<TrailRenderer>() ?? gameObject.AddComponent<TrailRenderer>();
        trailRenderer.time = 0.35f;
        trailRenderer.minVertexDistance = 0.03f;
        trailRenderer.startWidth = 0.14f;
        trailRenderer.endWidth = 0.025f;
        trailRenderer.numCornerVertices = 2;
        trailRenderer.numCapVertices = 2;
        trailRenderer.textureMode = LineTextureMode.Stretch;
        trailRenderer.alignment = LineAlignment.View;
        trailRenderer.colorGradient = CreateTrailGradient();
        trailRenderer.sharedMaterial = CreateRuntimeMaterial(
            "RuntimeMissileTrailMaterial",
            new Color(0.92f, 0.94f, 1f, 0.9f),
            true,
            "Sprites/Default",
            "Universal Render Pipeline/Particles/Unlit",
            "Particles/Standard Unlit");
    }

    private void EnsureSmokeTrail()
    {
        GameObject smokeObject = new("SmokeTrail");
        smokeObject.transform.SetParent(exhaustAnchor != null ? exhaustAnchor : transform, false);
        smokeObject.transform.localPosition = Vector3.zero;
        smokeObject.transform.localRotation = Quaternion.Euler(0f, 180f, 0f);

        smokeTrail = smokeObject.AddComponent<ParticleSystem>();
        ParticleSystem.MainModule main = smokeTrail.main;
        main.loop = true;
        main.playOnAwake = true;
        main.duration = 1f;
        main.simulationSpace = ParticleSystemSimulationSpace.Local;
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.32f, 0.58f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(1.8f, 3.4f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.18f, 0.34f);
        main.startColor = new Color(0.96f, 0.97f, 1f, 0.72f);
        main.maxParticles = 160;

        ParticleSystem.EmissionModule emission = smokeTrail.emission;
        emission.enabled = true;
        emission.rateOverTime = 96f;

        ParticleSystem.ShapeModule shape = smokeTrail.shape;
        shape.enabled = true;
        shape.shapeType = ParticleSystemShapeType.Cone;
        shape.angle = 16f;
        shape.radius = 0.026f;
        shape.radiusThickness = 0.35f;

        ParticleSystem.ColorOverLifetimeModule colorOverLifetime = smokeTrail.colorOverLifetime;
        colorOverLifetime.enabled = true;
        colorOverLifetime.color = new ParticleSystem.MinMaxGradient(CreateSmokeGradient());

        ParticleSystem.SizeOverLifetimeModule sizeOverLifetime = smokeTrail.sizeOverLifetime;
        sizeOverLifetime.enabled = true;
        AnimationCurve sizeCurve = new();
        sizeCurve.AddKey(0f, 0.65f);
        sizeCurve.AddKey(0.5f, 1f);
        sizeCurve.AddKey(1f, 1.9f);
        sizeOverLifetime.size = new ParticleSystem.MinMaxCurve(1f, sizeCurve);

        ParticleSystemRenderer renderer = smokeTrail.GetComponent<ParticleSystemRenderer>();
        renderer.renderMode = ParticleSystemRenderMode.Billboard;
        renderer.maxParticleSize = 0.6f;
        renderer.sharedMaterial = CreateRuntimeMaterial(
            "RuntimeMissileSmokeMaterial",
            new Color(0.96f, 0.97f, 1f, 0.72f),
            true,
            "Universal Render Pipeline/Particles/Unlit",
            "Particles/Standard Unlit",
            "Sprites/Default");
        smokeTrail.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        smokeTrail.Play(true);
    }

    private static Transform ResolveExhaustAnchor(Transform parent)
    {
        Transform existing = FindChildRecursive(parent, ExhaustAnchorName);
        if (existing != null)
        {
            return existing;
        }

        GameObject anchorObject = new(ExhaustAnchorName);
        anchorObject.transform.SetParent(parent, false);
        anchorObject.transform.localPosition = new Vector3(0f, 0f, -0.36f);
        anchorObject.transform.localRotation = Quaternion.identity;
        return anchorObject.transform;
    }

    private static Transform FindChildRecursive(Transform parent, string childName)
    {
        for (int i = 0; i < parent.childCount; i++)
        {
            Transform child = parent.GetChild(i);
            if (child.name == childName)
            {
                return child;
            }

            Transform nested = FindChildRecursive(child, childName);
            if (nested != null)
            {
                return nested;
            }
        }

        return null;
    }

    private static Gradient CreateTrailGradient()
    {
        Gradient gradient = new();
        gradient.SetKeys(
            new[]
            {
                new GradientColorKey(new Color(1f, 1f, 1f), 0f),
                new GradientColorKey(new Color(0.9f, 0.92f, 1f), 0.35f),
                new GradientColorKey(new Color(0.58f, 0.62f, 0.7f), 1f),
            },
            new[]
            {
                new GradientAlphaKey(0.9f, 0f),
                new GradientAlphaKey(0.28f, 0.5f),
                new GradientAlphaKey(0f, 1f),
            });
        return gradient;
    }

    private static Gradient CreateSmokeGradient()
    {
        Gradient gradient = new();
        gradient.SetKeys(
            new[]
            {
                new GradientColorKey(new Color(1f, 1f, 1f), 0f),
                new GradientColorKey(new Color(0.86f, 0.88f, 0.92f), 0.45f),
                new GradientColorKey(new Color(0.54f, 0.56f, 0.6f), 1f),
            },
            new[]
            {
                new GradientAlphaKey(0.78f, 0f),
                new GradientAlphaKey(0.38f, 0.45f),
                new GradientAlphaKey(0f, 1f),
            });
        return gradient;
    }

    private Material CreateRuntimeMaterial(string name, Color color, bool transparent, params string[] shaderNames)
    {
        Shader shader = null;
        for (int i = 0; i < shaderNames.Length; i++)
        {
            shader = Shader.Find(shaderNames[i]);
            if (shader != null)
            {
                break;
            }
        }

        if (shader == null)
        {
            return null;
        }

        Material material = new(shader)
        {
            name = name,
            hideFlags = HideFlags.HideAndDontSave,
        };

        if (material.HasProperty("_Surface"))
        {
            material.SetFloat("_Surface", transparent ? 1f : 0f);
        }

        if (material.HasProperty("_Blend"))
        {
            material.SetFloat("_Blend", transparent ? 0f : 0f);
        }

        if (material.HasProperty("_SrcBlend"))
        {
            material.SetFloat("_SrcBlend", transparent ? 5f : 1f);
        }

        if (material.HasProperty("_DstBlend"))
        {
            material.SetFloat("_DstBlend", transparent ? 10f : 0f);
        }

        if (material.HasProperty("_ZWrite"))
        {
            material.SetFloat("_ZWrite", transparent ? 0f : 1f);
        }

        if (material.HasProperty("_BaseColor"))
        {
            material.SetColor("_BaseColor", color);
        }

        if (material.HasProperty("_Color"))
        {
            material.SetColor("_Color", color);
        }

        runtimeMaterials.Add(material);
        return material;
    }

    private static void ApplyMaterial(Renderer renderer, Material material)
    {
        if (renderer == null || material == null)
        {
            return;
        }

        renderer.sharedMaterial = material;
    }

    private void OnDestroy()
    {
        for (int i = 0; i < runtimeMaterials.Count; i++)
        {
            if (runtimeMaterials[i] != null)
            {
                Destroy(runtimeMaterials[i]);
            }
        }
    }
}
