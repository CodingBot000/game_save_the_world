using System.Collections.Generic;
using UnityEngine;

public struct SpecialMissileStrikePath
{
    public Transform TargetAnchor;
    public Vector3 TargetLocalOffset;
    public Vector3 FanOutDirection;
    public float FanOutDuration;
    public float FanOutDistance;
    public Vector3 ArcControlPoint;
    public Vector3 TerminalEntryPoint;
    public float ArcDuration;
}

public class SpecialHomingMissileController : MonoBehaviour
{
    private const float DefaultMissileVisualLength = 0.92f;
    private const float DefaultTrailTime = 1.6f;
    private static Material sharedTrailCoreMaterial;
    private static Material sharedTrailGlowMaterial;
    private static Mesh sharedBarrageConeMesh;
    private enum MissilePhase
    {
        FanOut,
        Arc,
        Terminal,
        ImpactFade,
    }

    private const string ExhaustAnchorName = "MissileExhaustAnchor";

    private BattleController battleController;
    private Transform targetAnchor;
    private Vector3 targetLocalOffset;
    private ProjectileTeam team;
    private MissilePhase phase;
    private float currentSpeed;
    private float cruiseSpeed;
    private float acceleration;
    private float turnRate;
    private float phaseElapsed;
    private float remainingLifetime;
    private float damage;
    private float hitRadius;
    private float visualScale = 1f;
    private float smokeScale = 1f;
    private float impactEffectScale = 1f;
    private bool useTemplateOriginalMaterials;
    private bool targetLost;
    private bool lifecycleCompleted;
    private Color templateTint = Color.white;
    private Vector3 templateLocalEulerAngles;
    private Vector3 fanOutStartPosition;
    private Vector3 fanOutEndPosition;
    private Vector3 fanOutDirection;
    private float fanOutDuration;
    private Vector3 arcStartPosition;
    private Vector3 arcControlPosition;
    private Vector3 arcEndPosition;
    private float arcDuration;
    private float impactFadeDuration;
    private Transform exhaustAnchor;
    private Transform visualRoot;
    private GameObject smokeTemplate;
    private GameObject impactEffectTemplate;
    private Texture2D visualTexture;
    private Texture2D smokeTexture;
    private GameObject smokeInstance;
    private TrailRenderer trailRenderer;
    private TrailRenderer glowTrailRenderer;
    private ParticleSystem smokeTrail;
    private Renderer[] visualRenderers = new Renderer[0];
    private SpecialMissilePool pool;
    private readonly List<Material> runtimeMaterials = new();

    internal bool IsOwnedByFixedPool => pool != null;

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
        GameObject visualTemplate = null,
        GameObject smokePrefab = null,
        GameObject impactEffectPrefab = null,
        Texture2D visualTextureAsset = null,
        Texture2D smokeTextureAsset = null,
        float customVisualScale = 1f,
        float customSmokeScale = 1f,
        float customImpactEffectScale = 1f,
        bool preserveTemplateMaterials = false,
        Color customTemplateTint = default,
        Vector3 customTemplateLocalEulerAngles = default)
    {
        battleController = owner;
        targetAnchor = target;
        targetLost = false;
        lifecycleCompleted = false;
        targetLocalOffset = Vector3.zero;
        team = projectileTeam;
        smokeTemplate = smokePrefab;
        impactEffectTemplate = impactEffectPrefab;
        visualTexture = visualTextureAsset;
        smokeTexture = smokeTextureAsset;
        visualScale = Mathf.Max(0.01f, customVisualScale);
        smokeScale = Mathf.Max(0.01f, customSmokeScale);
        impactEffectScale = Mathf.Max(0.01f, customImpactEffectScale);
        useTemplateOriginalMaterials = preserveTemplateMaterials;
        templateTint = customTemplateTint == default ? Color.white : customTemplateTint;
        templateLocalEulerAngles = customTemplateLocalEulerAngles;

        Vector3 launchDirection = initialDirection.sqrMagnitude > 0.001f ? initialDirection.normalized : Vector3.forward;
        transform.rotation = Quaternion.LookRotation(launchDirection, Vector3.up);

        currentSpeed = Mathf.Max(1f, launchSpeed);
        cruiseSpeed = Mathf.Max(currentSpeed, maxSpeed);
        acceleration = Mathf.Max(0f, accelerationRate);
        turnRate = Mathf.Max(0f, turnRateDegrees);
        phaseElapsed = 0f;
        phase = MissilePhase.Terminal;
        remainingLifetime = Mathf.Max(0.5f, lifetime);
        damage = Mathf.Max(0f, damageAmount);
        hitRadius = Mathf.Max(0.1f, projectileHitRadius);

        EnsureVisuals(visualTemplate);
        SetVisualsVisible(true);
        BeginTrailEmission();
    }

    public void ConfigureStrikePath(SpecialMissileStrikePath path)
    {
        targetAnchor = path.TargetAnchor != null ? path.TargetAnchor : targetAnchor;
        targetLocalOffset = path.TargetLocalOffset;
        fanOutStartPosition = transform.position;
        fanOutDirection = path.FanOutDirection.sqrMagnitude > 0.001f
            ? path.FanOutDirection.normalized
            : transform.forward;
        fanOutDuration = Mathf.Max(0.01f, path.FanOutDuration);
        fanOutEndPosition = fanOutStartPosition + fanOutDirection * Mathf.Max(0f, path.FanOutDistance);
        arcStartPosition = fanOutEndPosition;
        arcControlPosition = path.ArcControlPoint;
        arcEndPosition = path.TerminalEntryPoint;
        arcDuration = Mathf.Max(0.05f, path.ArcDuration);
        phaseElapsed = 0f;
        phase = MissilePhase.FanOut;

        if (fanOutDirection.sqrMagnitude > 0.001f)
        {
            transform.rotation = Quaternion.LookRotation(fanOutDirection, Vector3.up);
        }
    }

    internal void SetPool(SpecialMissilePool ownerPool)
    {
        pool = ownerPool;
    }

    internal void PrepareForPool()
    {
        battleController = null;
        targetAnchor = null;
        targetLost = false;
        lifecycleCompleted = true;
        targetLocalOffset = Vector3.zero;
        smokeTemplate = null;
        impactEffectTemplate = null;
        visualTexture = null;
        smokeTexture = null;
        phase = MissilePhase.ImpactFade;
        phaseElapsed = 0f;
        remainingLifetime = 0f;
        SetVisualsVisible(false);
        ClearRuntimeTrail();

        if (smokeTrail != null)
        {
            smokeTrail.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        }
    }

    private void Update()
    {
        float deltaTime = Time.deltaTime;
        if (phase == MissilePhase.ImpactFade)
        {
            phaseElapsed += deltaTime;
            if (phaseElapsed >= impactFadeDuration)
            {
                CompleteLifecycle();
            }

            return;
        }

        if (battleController == null)
        {
            BeginImpactFade(false);
            return;
        }

        remainingLifetime -= deltaTime;
        if (remainingLifetime <= 0f || transform.position.magnitude > 150f || transform.position.y < -20f)
        {
            BeginImpactFade(false);
            return;
        }

        if (!targetLost && (targetAnchor == null || !targetAnchor.gameObject.activeInHierarchy))
        {
            targetLost = true;
            targetAnchor = null;
            phase = MissilePhase.Terminal;
            phaseElapsed = 0f;
        }

        Vector3 previousPosition = transform.position;
        UpdateFlight(deltaTime);

        bool hit = team == ProjectileTeam.Player
            ? battleController.TryHitBoss(
                previousPosition,
                transform.position,
                hitRadius,
                damage,
                projectileCollider: null)
            : battleController.TryHitPlayer(
                previousPosition,
                transform.position,
                hitRadius,
                damage);

        if (hit)
        {
            BeginImpactFade(true);
        }
    }

    private void BeginImpactFade(bool spawnImpact)
    {
        if (phase == MissilePhase.ImpactFade)
        {
            return;
        }

        if (spawnImpact)
        {
            SpawnImpactEffect();
        }

        phase = MissilePhase.ImpactFade;
        phaseElapsed = 0f;
        impactFadeDuration = Mathf.Max(
            trailRenderer != null ? trailRenderer.time : 0f,
            glowTrailRenderer != null ? glowTrailRenderer.time : 0f);
        SetVisualsVisible(false);
        StopTrailEmission();

        if (impactFadeDuration <= 0.01f)
        {
            CompleteLifecycle();
        }
    }

    private void CompleteLifecycle()
    {
        if (lifecycleCompleted)
        {
            return;
        }

        lifecycleCompleted = true;
        if (pool != null)
        {
            pool.Release(this);
            return;
        }

        ClearRuntimeTrail();
        Destroy(gameObject);
    }

    private void UpdateFlight(float deltaTime)
    {
        phaseElapsed += deltaTime;

        switch (phase)
        {
            case MissilePhase.FanOut:
                float fanOutProgress = fanOutDuration > 0.001f
                    ? Mathf.Clamp01(phaseElapsed / fanOutDuration)
                    : 1f;
                float easedFanOutProgress = Mathf.SmoothStep(0f, 1f, fanOutProgress);
                transform.position = Vector3.LerpUnclamped(
                    fanOutStartPosition,
                    fanOutEndPosition,
                    easedFanOutProgress);
                transform.rotation = Quaternion.LookRotation(fanOutDirection, Vector3.up);

                if (fanOutProgress >= 1f)
                {
                    transform.position = fanOutEndPosition;
                    phase = MissilePhase.Arc;
                    phaseElapsed = 0f;
                }

                break;

            case MissilePhase.Arc:
                float arcProgress = arcDuration > 0.001f
                    ? Mathf.Clamp01(phaseElapsed / arcDuration)
                    : 1f;
                float easedArcProgress = Mathf.SmoothStep(0f, 1f, arcProgress);
                transform.position = EvaluateQuadraticBezier(easedArcProgress);

                Vector3 arcTangent = GetQuadraticBezierTangent(easedArcProgress);
                if (arcTangent.sqrMagnitude > 0.001f)
                {
                    transform.rotation = Quaternion.LookRotation(arcTangent.normalized, Vector3.up);
                }

                if (arcProgress >= 1f)
                {
                    transform.position = arcEndPosition;
                    phase = MissilePhase.Terminal;
                    phaseElapsed = 0f;
                }

                break;

            case MissilePhase.Terminal:
                currentSpeed = Mathf.MoveTowards(currentSpeed, cruiseSpeed, acceleration * deltaTime);
                Vector3 targetDirection = GetTargetDirection(transform.forward);
                Vector3 terminalDirection = turnRate > 0f
                    ? Vector3.RotateTowards(
                        transform.forward,
                        targetDirection,
                        Mathf.Deg2Rad * turnRate * deltaTime,
                        0f).normalized
                    : targetDirection;
                if (terminalDirection.sqrMagnitude > 0.001f)
                {
                    transform.rotation = Quaternion.LookRotation(terminalDirection, Vector3.up);
                }

                transform.position += transform.forward * (currentSpeed * deltaTime);
                break;
        }
    }

    private Vector3 EvaluateQuadraticBezier(float progress)
    {
        float t = Mathf.Clamp01(progress);
        float inverseT = 1f - t;
        return
            inverseT * inverseT * arcStartPosition +
            2f * inverseT * t * arcControlPosition +
            t * t * arcEndPosition;
    }

    private Vector3 GetQuadraticBezierTangent(float progress)
    {
        float t = Mathf.Clamp01(progress);
        return
            2f * (1f - t) * (arcControlPosition - arcStartPosition) +
            2f * t * (arcEndPosition - arcControlPosition);
    }

    private Vector3 GetTargetDirection(Vector3 fallbackDirection)
    {
        if (targetLost || targetAnchor == null || !targetAnchor.gameObject.activeInHierarchy)
        {
            return fallbackDirection.sqrMagnitude > 0.001f ? fallbackDirection.normalized : Vector3.forward;
        }

        Vector3 desiredDirection = targetAnchor.TransformPoint(targetLocalOffset) - transform.position;
        if (desiredDirection.sqrMagnitude < 0.001f)
        {
            return fallbackDirection.sqrMagnitude > 0.001f ? fallbackDirection.normalized : Vector3.forward;
        }

        return desiredDirection.normalized;
    }

    private void EnsureVisuals(GameObject visualTemplate)
    {
        visualRoot = transform.Find("MissileVisualRoot");
        if (visualRoot == null)
        {
            GameObject visualRootObject = new("MissileVisualRoot");
            visualRootObject.transform.SetParent(transform, false);
            visualRootObject.transform.localPosition = Vector3.zero;
            visualRootObject.transform.localRotation = Quaternion.identity;
            visualRoot = visualRootObject.transform;
            bool hasCustomVisual = false;

            if (visualTemplate != null)
            {
                GameObject customVisual = InstantiateTemplate(visualTemplate, visualRoot);
                if (customVisual == null)
                {
                    string templateName = string.IsNullOrWhiteSpace(visualTemplate.name) ? "<unnamed>" : visualTemplate.name;
                    Debug.LogWarning(
                        $"Missile visual template failed to instantiate. Template='{templateName}', Type='{visualTemplate.GetType().Name}'",
                        this);
                }
                else
                {
                    customVisual.name = "MissileSkin";
                    customVisual.transform.localPosition = Vector3.zero;
                    customVisual.transform.localRotation = Quaternion.Euler(templateLocalEulerAngles);
                    ApplyTemplateVisualAppearance(customVisual);
                    hasCustomVisual = true;
                    if (!NormalizeCustomVisualScale(customVisual.transform))
                    {
                        customVisual.transform.localScale = customVisual.transform.localScale * visualScale;
                        Debug.LogWarning($"Missile visual bounds normalization failed, using raw scale on template: {visualTemplate.name}", this);
                    }
                }
            }
            if (!hasCustomVisual)
            {
                CreateGameplayShell(visualRoot);
            }
        }

        exhaustAnchor = ResolveExhaustAnchor(visualRoot);
        visualRenderers = visualRoot.GetComponentsInChildren<Renderer>(true);
        EnsureTrailRenderer();
    }

    private void CreateGameplayShell(Transform parent)
    {
        GameObject shellRoot = new("MissileGameplayShell");
        shellRoot.transform.SetParent(parent, false);
        shellRoot.transform.localPosition = Vector3.zero;
        shellRoot.transform.localRotation = Quaternion.identity;
        shellRoot.transform.localScale = Vector3.one * GetGameplayShellScale();

        Material bodyMaterial = CreateRuntimeMaterial(
            "RuntimeBlackBarrageMissileMaterial",
            new Color(0.012f, 0.014f, 0.018f, 1f),
            false,
            "Universal Render Pipeline/Unlit",
            "Sprites/Default",
            "Universal Render Pipeline/Lit",
            "Standard");

        GameObject body = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        body.name = "BarrageMissileCylinder";
        body.transform.SetParent(shellRoot.transform, false);
        body.transform.localPosition = new Vector3(0f, 0f, -0.02f);
        body.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
        body.transform.localScale = new Vector3(0.18f, 0.30f, 0.18f);
        Collider bodyCollider = body.GetComponent<Collider>();
        if (bodyCollider != null)
        {
            Destroy(bodyCollider);
        }

        ApplyMaterial(body.GetComponent<Renderer>(), bodyMaterial);

        GameObject nose = new("BarrageMissileConeNose");
        nose.transform.SetParent(shellRoot.transform, false);
        nose.transform.localPosition = new Vector3(0f, 0f, 0.28f);
        nose.transform.localRotation = Quaternion.identity;
        nose.transform.localScale = new Vector3(0.18f, 0.18f, 0.28f);
        MeshFilter noseMeshFilter = nose.AddComponent<MeshFilter>();
        noseMeshFilter.sharedMesh = GetSharedBarrageConeMesh();
        MeshRenderer noseRenderer = nose.AddComponent<MeshRenderer>();
        ApplyMaterial(noseRenderer, bodyMaterial);
    }

    private float GetGameplayShellScale()
    {
        return Mathf.Clamp(0.9f + visualScale * 0.1f, 0.9f, 1.2f);
    }

    private static Mesh GetSharedBarrageConeMesh()
    {
        if (sharedBarrageConeMesh != null)
        {
            return sharedBarrageConeMesh;
        }

        const int segmentCount = 16;
        Vector3[] vertices = new Vector3[segmentCount + 2];
        int[] triangles = new int[segmentCount * 6];
        vertices[0] = Vector3.zero;
        for (int segment = 0; segment < segmentCount; segment++)
        {
            float angle = segment / (float)segmentCount * Mathf.PI * 2f;
            vertices[segment + 1] = new Vector3(
                Mathf.Cos(angle) * 0.5f,
                Mathf.Sin(angle) * 0.5f,
                0f);
        }

        int tipIndex = vertices.Length - 1;
        vertices[tipIndex] = Vector3.forward;
        for (int segment = 0; segment < segmentCount; segment++)
        {
            int current = segment + 1;
            int next = (segment + 1) % segmentCount + 1;
            int triangle = segment * 6;
            triangles[triangle] = 0;
            triangles[triangle + 1] = next;
            triangles[triangle + 2] = current;
            triangles[triangle + 3] = current;
            triangles[triangle + 4] = next;
            triangles[triangle + 5] = tipIndex;
        }

        sharedBarrageConeMesh = new Mesh
        {
            name = "RuntimeBlackBarrageConeMesh",
            vertices = vertices,
            triangles = triangles,
            hideFlags = HideFlags.HideAndDontSave,
        };
        sharedBarrageConeMesh.RecalculateNormals();
        sharedBarrageConeMesh.RecalculateBounds();
        return sharedBarrageConeMesh;
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
        if (!TryGetComponent(out trailRenderer) || trailRenderer == null)
        {
            trailRenderer = gameObject.AddComponent<TrailRenderer>();
        }

        if (trailRenderer == null)
        {
            return;
        }

        trailRenderer.time = DefaultTrailTime;
        trailRenderer.minVertexDistance = 0.12f;
        trailRenderer.startWidth = 0.1f;
        trailRenderer.endWidth = 0.015f;
        trailRenderer.numCornerVertices = 2;
        trailRenderer.numCapVertices = 1;
        trailRenderer.textureMode = LineTextureMode.Stretch;
        trailRenderer.alignment = LineAlignment.View;
        trailRenderer.colorGradient = CreateTrailGradient(false);
        trailRenderer.sharedMaterial = GetSharedTrailMaterial(false);

        Transform glowTrailTransform = transform.Find("MissileGlowTrail");
        if (glowTrailTransform == null)
        {
            GameObject glowTrailObject = new("MissileGlowTrail");
            glowTrailObject.transform.SetParent(transform, false);
            glowTrailTransform = glowTrailObject.transform;
        }

        if (!glowTrailTransform.TryGetComponent(out glowTrailRenderer) || glowTrailRenderer == null)
        {
            glowTrailRenderer = glowTrailTransform.gameObject.AddComponent<TrailRenderer>();
        }

        glowTrailRenderer.time = DefaultTrailTime;
        glowTrailRenderer.minVertexDistance = 0.12f;
        glowTrailRenderer.startWidth = 0.24f;
        glowTrailRenderer.endWidth = 0.03f;
        glowTrailRenderer.numCornerVertices = 2;
        glowTrailRenderer.numCapVertices = 1;
        glowTrailRenderer.textureMode = LineTextureMode.Stretch;
        glowTrailRenderer.alignment = LineAlignment.View;
        glowTrailRenderer.colorGradient = CreateTrailGradient(true);
        glowTrailRenderer.sharedMaterial = GetSharedTrailMaterial(true);
    }

    private void SetVisualsVisible(bool visible)
    {
        for (int i = 0; i < visualRenderers.Length; i++)
        {
            if (visualRenderers[i] != null)
            {
                visualRenderers[i].enabled = visible;
            }
        }
    }

    private void BeginTrailEmission()
    {
        SetTrailState(trailRenderer, true, true);
        SetTrailState(glowTrailRenderer, true, true);
    }

    private void StopTrailEmission()
    {
        SetTrailState(trailRenderer, true, false);
        SetTrailState(glowTrailRenderer, true, false);
    }

    private static void SetTrailState(TrailRenderer trail, bool enabled, bool emitting)
    {
        if (trail == null)
        {
            return;
        }

        if (emitting)
        {
            trail.Clear();
        }

        trail.enabled = enabled;
        trail.emitting = emitting;
    }

    private void EnsureSmokeTrail()
    {
        if (smokeTexture != null)
        {
            CreateTexturedSmokeTrail();
            return;
        }

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
        main.startSize = new ParticleSystem.MinMaxCurve(0.38f, 0.68f);
        main.startColor = new Color(0.96f, 0.97f, 1f, 0.95f);
        main.maxParticles = 90;

        ParticleSystem.EmissionModule emission = smokeTrail.emission;
        emission.enabled = true;
        emission.rateOverTime = 36f;

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
            new Color(0.96f, 0.97f, 1f, 0.95f),
            true,
            "Universal Render Pipeline/Particles/Unlit",
            "Particles/Standard Unlit",
            "Sprites/Default");
        smokeTrail.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        smokeTrail.Play(true);
    }

    private void SpawnImpactEffect()
    {
        if (impactEffectTemplate == null)
        {
            return;
        }

        if (pool != null)
        {
            pool.SpawnImpact(
                impactEffectTemplate,
                transform.position,
                transform.rotation,
                impactEffectScale);
            return;
        }

        GameObject effectInstance = InstantiateTemplate(impactEffectTemplate, transform.position, transform.rotation);
        if (effectInstance == null)
        {
            return;
        }

        effectInstance.transform.localScale = effectInstance.transform.localScale * impactEffectScale;
        Destroy(effectInstance, 4f);
    }

    private static GameObject InstantiateTemplate(GameObject template, Transform parent)
    {
        if (template == null)
        {
            return null;
        }

        int childCountBefore = parent != null ? parent.childCount : 0;
        Object instance = Instantiate((Object)template, parent);
        GameObject resolved = ResolveInstantiatedGameObject(instance);
        if (resolved != null)
        {
            return resolved;
        }

        if (parent != null && parent.childCount > childCountBefore)
        {
            return parent.GetChild(parent.childCount - 1).gameObject;
        }

        return null;
    }

    private static GameObject InstantiateTemplate(GameObject template, Vector3 position, Quaternion rotation)
    {
        if (template == null)
        {
            return null;
        }

        Object instance = Instantiate((Object)template, position, rotation);
        return ResolveInstantiatedGameObject(instance);
    }

    private static GameObject ResolveInstantiatedGameObject(Object instance)
    {
        if (instance == null)
        {
            return null;
        }

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

    private static Gradient CreateTrailGradient(bool glow)
    {
        Gradient gradient = new();
        gradient.SetKeys(
            new[]
            {
                new GradientColorKey(
                    glow ? new Color(0.25f, 2.2f, 2.8f) : new Color(1.4f, 3.8f, 4.2f),
                    0f),
                new GradientColorKey(new Color(0.08f, 1.45f, 1.9f), 0.45f),
                new GradientColorKey(new Color(0.02f, 0.35f, 0.5f), 1f),
            },
            new[]
            {
                new GradientAlphaKey(glow ? 0.52f : 1f, 0f),
                new GradientAlphaKey(glow ? 0.3f : 0.82f, 0.38f),
                new GradientAlphaKey(0f, 1f),
            });
        return gradient;
    }

    private static Material GetSharedTrailMaterial(bool glow)
    {
        Material material = glow ? sharedTrailGlowMaterial : sharedTrailCoreMaterial;
        if (material != null)
        {
            return material;
        }

        Shader shader = Shader.Find("Universal Render Pipeline/Particles/Unlit") ??
                        Shader.Find("Particles/Standard Unlit") ??
                        Shader.Find("Sprites/Default");
        if (shader == null)
        {
            return null;
        }

        material = new Material(shader)
        {
            name = glow ? "SharedSpecialMissileTrailGlow" : "SharedSpecialMissileTrailCore",
            hideFlags = HideFlags.HideAndDontSave,
            renderQueue = 3000,
        };
        Color color = glow
            ? new Color(0.05f, 1.4f, 1.8f, 0.42f)
            : new Color(0.6f, 2.8f, 3.2f, 1f);
        ConfigureTransparentMaterial(material, color);

        if (glow)
        {
            sharedTrailGlowMaterial = material;
        }
        else
        {
            sharedTrailCoreMaterial = material;
        }

        return material;
    }

    private static void ConfigureTransparentMaterial(Material material, Color color)
    {
        if (material.HasProperty("_Surface"))
        {
            material.SetFloat("_Surface", 1f);
        }

        if (material.HasProperty("_SrcBlend"))
        {
            material.SetFloat("_SrcBlend", 5f);
        }

        if (material.HasProperty("_DstBlend"))
        {
            material.SetFloat("_DstBlend", 10f);
        }

        if (material.HasProperty("_ZWrite"))
        {
            material.SetFloat("_ZWrite", 0f);
        }

        if (material.HasProperty("_BaseColor"))
        {
            material.SetColor("_BaseColor", color);
        }

        if (material.HasProperty("_Color"))
        {
            material.SetColor("_Color", color);
        }
    }

    private void CreateTexturedSmokeTrail()
    {
        GameObject smokeObject = new("SmokeTrail");
        smokeObject.transform.SetParent(exhaustAnchor != null ? exhaustAnchor : transform, false);
        smokeObject.transform.localPosition = Vector3.zero;
        smokeObject.transform.localRotation = Quaternion.Euler(0f, 180f, 0f);

        smokeTrail = smokeObject.AddComponent<ParticleSystem>();
        smokeTrail.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        ParticleSystem.MainModule main = smokeTrail.main;
        main.loop = true;
        main.playOnAwake = false;
        main.duration = 1.6f;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.startLifetime = new ParticleSystem.MinMaxCurve(1.15f, 2.05f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(1.1f, 2.1f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.48f, 0.9f);
        main.startColor = new Color(0.99f, 0.995f, 1f, 1f);
        main.maxParticles = 180;

        ParticleSystem.EmissionModule emission = smokeTrail.emission;
        emission.enabled = true;
        emission.rateOverTime = 54f;

        ParticleSystem.ShapeModule shape = smokeTrail.shape;
        shape.enabled = true;
        shape.shapeType = ParticleSystemShapeType.Cone;
        shape.angle = 9f;
        shape.radius = 0.019f;
        shape.radiusThickness = 0.12f;

        ParticleSystem.VelocityOverLifetimeModule velocityOverLifetime = smokeTrail.velocityOverLifetime;
        velocityOverLifetime.enabled = true;
        velocityOverLifetime.space = ParticleSystemSimulationSpace.Local;
        velocityOverLifetime.x = new ParticleSystem.MinMaxCurve(0f, 0f);
        velocityOverLifetime.y = new ParticleSystem.MinMaxCurve(0f, 0f);
        velocityOverLifetime.z = new ParticleSystem.MinMaxCurve(-0.9f, -1.8f);

        ParticleSystem.ColorOverLifetimeModule colorOverLifetime = smokeTrail.colorOverLifetime;
        colorOverLifetime.enabled = true;
        colorOverLifetime.color = new ParticleSystem.MinMaxGradient(CreateSmokeGradient());

        ParticleSystem.SizeOverLifetimeModule sizeOverLifetime = smokeTrail.sizeOverLifetime;
        sizeOverLifetime.enabled = true;
        AnimationCurve sizeCurve = new();
        sizeCurve.AddKey(0f, 0.31f);
        sizeCurve.AddKey(0.18f, 0.59f);
        sizeCurve.AddKey(0.55f, 0.96f);
        sizeCurve.AddKey(1f, 1.575f);
        sizeOverLifetime.size = new ParticleSystem.MinMaxCurve(1f, sizeCurve);

        ParticleSystemRenderer renderer = smokeTrail.GetComponent<ParticleSystemRenderer>();
        renderer.renderMode = ParticleSystemRenderMode.Billboard;
        renderer.maxParticleSize = 2.2f;

        Material smokeMaterial = CreateRuntimeMaterial(
            "RuntimeTemplateSmokeMaterial",
            new Color(1f, 1f, 1f, 1f),
            true,
            "Universal Render Pipeline/Particles/Unlit",
            "Particles/Standard Unlit",
            "Sprites/Default");
        ApplyTexture(smokeMaterial, smokeTexture);
        renderer.sharedMaterial = smokeMaterial;

        smokeObject.transform.localScale = Vector3.one * Mathf.Max(0.1f, smokeScale * 1.15f);
        smokeTrail.Play(true);
    }

    private void ConfigureSmokeTemplate(GameObject smokeObject)
    {
        if (smokeObject == null)
        {
            return;
        }

        ParticleSystem[] smokeSystems = smokeObject.GetComponentsInChildren<ParticleSystem>(true);
        for (int i = 0; i < smokeSystems.Length; i++)
        {
            ParticleSystem.MainModule main = smokeSystems[i].main;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
        }

        if (smokeTexture == null)
        {
            return;
        }

        Material smokeMaterial = CreateRuntimeMaterial(
            "RuntimeTemplateSmokeMaterial",
            new Color(1f, 1f, 1f, 1f),
            true,
            "Universal Render Pipeline/Particles/Unlit",
            "Particles/Standard Unlit",
            "Sprites/Default");
        if (smokeMaterial == null)
        {
            return;
        }

        ApplyTexture(smokeMaterial, smokeTexture);

        ParticleSystemRenderer[] renderers = smokeObject.GetComponentsInChildren<ParticleSystemRenderer>(true);
        for (int i = 0; i < renderers.Length; i++)
        {
            renderers[i].renderMode = ParticleSystemRenderMode.Billboard;
            renderers[i].maxParticleSize = Mathf.Max(1f, renderers[i].maxParticleSize);
            renderers[i].sharedMaterial = smokeMaterial;
        }
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
                new GradientAlphaKey(1f, 0f),
                new GradientAlphaKey(0.82f, 0.35f),
                new GradientAlphaKey(0f, 1f),
            });
        return gradient;
    }

    private void ApplyTemplateVisualAppearance(GameObject visualRoot)
    {
        if (visualRoot == null || useTemplateOriginalMaterials)
        {
            return;
        }

        Material overrideMaterial = CreateRuntimeMaterial(
            "RuntimeMissileTemplateOverrideMaterial",
            templateTint,
            false,
            "Universal Render Pipeline/Lit",
            "Standard",
            "Universal Render Pipeline/Unlit",
            "Sprites/Default");
        if (overrideMaterial == null)
        {
            return;
        }

        ApplyTexture(overrideMaterial, visualTexture);

        Renderer[] renderers = visualRoot.GetComponentsInChildren<Renderer>(true);
        for (int i = 0; i < renderers.Length; i++)
        {
            ApplyMaterialToAllSlots(renderers[i], overrideMaterial);
        }
    }

    private bool NormalizeCustomVisualScale(Transform customVisual)
    {
        if (customVisual == null)
        {
            return false;
        }

        Renderer[] renderers = customVisual.GetComponentsInChildren<Renderer>(true);
        if (renderers == null || renderers.Length == 0)
        {
            return false;
        }

        Bounds combinedBounds = renderers[0].bounds;
        bool foundVisibleBounds = false;
        for (int i = 0; i < renderers.Length; i++)
        {
            Renderer renderer = renderers[i];
            if (renderer == null || !renderer.enabled)
            {
                continue;
            }

            if (!foundVisibleBounds)
            {
                combinedBounds = renderer.bounds;
                foundVisibleBounds = true;
                continue;
            }

            combinedBounds.Encapsulate(renderer.bounds);
        }

        if (!foundVisibleBounds)
        {
            return false;
        }

        float longestDimension = Mathf.Max(combinedBounds.size.x, combinedBounds.size.y, combinedBounds.size.z);
        if (longestDimension <= 0.0001f)
        {
            return false;
        }

        Vector3 originalScale = customVisual.localScale;
        float targetLength = Mathf.Max(0.1f, DefaultMissileVisualLength * visualScale);
        float scaleFactor = targetLength / longestDimension;
        customVisual.localScale = originalScale * scaleFactor;
        return true;
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

    private static void ApplyTexture(Material material, Texture texture)
    {
        if (material == null || texture == null)
        {
            return;
        }

        if (material.HasProperty("_BaseMap"))
        {
            material.SetTexture("_BaseMap", texture);
        }

        if (material.HasProperty("_MainTex"))
        {
            material.SetTexture("_MainTex", texture);
        }
    }

    private static void ApplyMaterial(Renderer renderer, Material material)
    {
        if (renderer == null || material == null)
        {
            return;
        }

        renderer.sharedMaterial = material;
    }

    private static void ApplyMaterialToAllSlots(Renderer renderer, Material material)
    {
        if (renderer == null || material == null)
        {
            return;
        }

        Material[] sharedMaterials = renderer.sharedMaterials;
        if (sharedMaterials == null || sharedMaterials.Length == 0)
        {
            renderer.sharedMaterial = material;
            return;
        }

        for (int i = 0; i < sharedMaterials.Length; i++)
        {
            sharedMaterials[i] = material;
        }

        renderer.sharedMaterials = sharedMaterials;
    }

    private void OnDestroy()
    {
        if (pool != null)
        {
            pool.NotifyMissileDestroyedOutsidePool(this);
        }

        ClearRuntimeTrail();
        DetachSmokeEffect();

        for (int i = 0; i < runtimeMaterials.Count; i++)
        {
            if (runtimeMaterials[i] != null)
            {
                Destroy(runtimeMaterials[i]);
            }
        }
    }

    private void OnDisable()
    {
        ClearRuntimeTrail();
    }

    private void ClearRuntimeTrail()
    {
        if (trailRenderer == null)
        {
            TryGetComponent(out trailRenderer);
        }

        ClearTrail(trailRenderer);
        ClearTrail(glowTrailRenderer);
    }

    private static void ClearTrail(TrailRenderer trail)
    {
        if (trail == null)
        {
            return;
        }

        trail.emitting = false;
        trail.Clear();
        trail.enabled = false;
    }

    private void DetachSmokeEffect()
    {
        if (smokeInstance != null)
        {
            smokeInstance.transform.SetParent(null, true);
            ParticleSystem[] smokeSystems = smokeInstance.GetComponentsInChildren<ParticleSystem>(true);
            for (int i = 0; i < smokeSystems.Length; i++)
            {
                smokeSystems[i].Stop(true, ParticleSystemStopBehavior.StopEmitting);
            }

            Destroy(smokeInstance, 4f);
            smokeInstance = null;
        }

        if (smokeTrail != null)
        {
            smokeTrail.transform.SetParent(null, true);
            smokeTrail.Stop(true, ParticleSystemStopBehavior.StopEmitting);
            Destroy(smokeTrail.gameObject, 4f);
            smokeTrail = null;
        }
    }
}

public class CartoonSmokePuff : MonoBehaviour
{
    private const int TextureSize = 64;
    private const int CloudCircleCount = 3;
    private const int DensityLayerCount = 2;
    private const int VerticesPerCircle = 4;
    private const int TrianglesPerCircle = 6;
    private const float FadeStartProgress = 0.2f;
    private static readonly string[] RuntimeSmokeObjectNamePrefixes =
    {
        "CartoonSmokePuff",
        "SmokeTrail",
        "PlayerSpecialMissileRuntime",
    };
    private static readonly Queue<CartoonSmokePuff> Pool = new();
    private static Mesh sharedMesh;
    private static Material sharedMaterial;
    private static Texture2D sharedTexture;
    private static Camera cachedCamera;
    private static int colorPropertyId = -1;

    private MeshRenderer meshRenderer;
    private MaterialPropertyBlock propertyBlock;
    private float age;
    private float lifetime;
    private float startScale;
    private float endScale;
    private Color color;
    private Vector3 driftVelocity;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStaticState()
    {
        Pool.Clear();
        cachedCamera = null;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void ClearLeftoverPuffs()
    {
        ClearAllRuntimeSmokeObjects();
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void ClearSceneLoadedPuffs()
    {
        ClearAllRuntimeSmokeObjects();
    }

    public static void ClearAllRuntimeSmokeObjects()
    {
        Pool.Clear();
        cachedCamera = null;

        SpecialHomingMissileController[] specialMissiles = Resources.FindObjectsOfTypeAll<SpecialHomingMissileController>();
        for (int i = 0; i < specialMissiles.Length; i++)
        {
            if (specialMissiles[i] != null && !specialMissiles[i].IsOwnedByFixedPool)
            {
                HideAndDestroyRuntimeObject(specialMissiles[i].gameObject);
            }
        }

        CartoonSmokePuff[] existingPuffs = Resources.FindObjectsOfTypeAll<CartoonSmokePuff>();
        for (int i = 0; i < existingPuffs.Length; i++)
        {
            if (existingPuffs[i] != null)
            {
                HideAndDestroyRuntimeObject(existingPuffs[i].gameObject);
            }
        }

        TrailRenderer[] existingTrails = Resources.FindObjectsOfTypeAll<TrailRenderer>();
        for (int i = 0; i < existingTrails.Length; i++)
        {
            TrailRenderer trail = existingTrails[i];
            if (trail == null ||
                IsOwnedByFixedMissilePool(trail.gameObject) ||
                !IsRuntimeMissileTrail(trail))
            {
                continue;
            }

            trail.emitting = false;
            trail.Clear();
            trail.enabled = false;
            HideAndDestroyRuntimeObject(trail.gameObject);
        }

        GameObject[] existingObjects = Resources.FindObjectsOfTypeAll<GameObject>();
        for (int i = 0; i < existingObjects.Length; i++)
        {
            GameObject existingObject = existingObjects[i];
            if (IsOwnedByFixedMissilePool(existingObject) ||
                !IsRuntimeSmokeObject(existingObject))
            {
                continue;
            }

            HideAndDestroyRuntimeObject(existingObject);
        }
    }

    public static void Spawn(Vector3 position, float size, float duration, Color puffColor, Vector3 drift)
    {
        CartoonSmokePuff puff = GetPooledPuff();
        puff.gameObject.SetActive(true);
        puff.transform.position = position;
        puff.Initialize(size, duration, puffColor, drift);
    }

    private static CartoonSmokePuff GetPooledPuff()
    {
        while (Pool.Count > 0)
        {
            CartoonSmokePuff pooledPuff = Pool.Dequeue();
            if (pooledPuff != null && pooledPuff.gameObject != null)
            {
                return pooledPuff;
            }
        }

        return CreatePuff();
    }

    private static bool IsRuntimeSmokeObject(GameObject target)
    {
        if (target == null || !IsRuntimeObjectInstance(target))
        {
            return false;
        }

        for (int i = 0; i < RuntimeSmokeObjectNamePrefixes.Length; i++)
        {
            if (target.name.StartsWith(RuntimeSmokeObjectNamePrefixes[i]))
            {
                return true;
            }
        }

        return false;
    }

    private static bool IsOwnedByFixedMissilePool(GameObject target)
    {
        if (target == null)
        {
            return false;
        }

        SpecialHomingMissileController missile =
            target.GetComponentInParent<SpecialHomingMissileController>(true);
        return missile != null && missile.IsOwnedByFixedPool;
    }

    private static bool IsRuntimeMissileTrail(TrailRenderer trail)
    {
        if (trail == null || !IsRuntimeObjectInstance(trail.gameObject))
        {
            return false;
        }

        GameObject trailObject = trail.gameObject;
        if (trailObject.GetComponent<SpecialHomingMissileController>() != null)
        {
            return true;
        }

        if (IsRuntimeSmokeObject(trailObject))
        {
            return true;
        }

        Transform root = trailObject.transform.root;
        if (root != null && IsRuntimeSmokeObject(root.gameObject))
        {
            return true;
        }

        Material material = trail.sharedMaterial;
        return material != null && material.name.StartsWith("RuntimeMissileTrailMaterial");
    }

    private static bool IsRuntimeObjectInstance(GameObject target)
    {
        if (target == null)
        {
            return false;
        }

        if (target.scene.IsValid())
        {
            return true;
        }

        return (target.hideFlags & HideFlags.DontSave) != 0;
    }

    private static void HideAndDestroyRuntimeObject(GameObject target)
    {
        if (target == null || !IsRuntimeObjectInstance(target))
        {
            return;
        }

        TrailRenderer[] trailRenderers = target.GetComponentsInChildren<TrailRenderer>(true);
        for (int i = 0; i < trailRenderers.Length; i++)
        {
            trailRenderers[i].emitting = false;
            trailRenderers[i].Clear();
            trailRenderers[i].enabled = false;
        }

        Renderer[] renderers = target.GetComponentsInChildren<Renderer>(true);
        for (int i = 0; i < renderers.Length; i++)
        {
            renderers[i].enabled = false;
        }

        ParticleSystem[] particleSystems = target.GetComponentsInChildren<ParticleSystem>(true);
        for (int i = 0; i < particleSystems.Length; i++)
        {
            particleSystems[i].Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            particleSystems[i].Clear(true);
        }

        target.SetActive(false);
        if (Application.isPlaying)
        {
            Destroy(target);
        }
        else
        {
            DestroyImmediate(target);
        }
    }

    private static CartoonSmokePuff CreatePuff()
    {
        GameObject puffObject = new("CartoonSmokePuff");
        puffObject.hideFlags = HideFlags.HideAndDontSave;
        CartoonSmokePuff puff = puffObject.AddComponent<CartoonSmokePuff>();
        puff.EnsureRenderer();
        puffObject.SetActive(false);
        return puff;
    }

    private void Awake()
    {
        EnsureRenderer();
    }

    private void Initialize(float size, float duration, Color puffColor, Vector3 drift)
    {
        EnsureRenderer();
        age = 0f;
        lifetime = Mathf.Max(0.05f, duration);
        startScale = Mathf.Max(0.01f, size * 0.45f);
        endScale = Mathf.Max(startScale, size * 1.28f);
        color = puffColor;
        driftVelocity = drift;
        ApplyVisual(0f);
    }

    private void Update()
    {
        age += Time.deltaTime;
        if (age >= lifetime)
        {
            Release();
            return;
        }

        transform.position += driftVelocity * Time.deltaTime;
        Camera mainCamera = GetMainCamera();
        if (mainCamera != null)
        {
            Vector3 directionToCamera = transform.position - mainCamera.transform.position;
            if (directionToCamera.sqrMagnitude > 0.0001f)
            {
                transform.rotation = Quaternion.LookRotation(directionToCamera, mainCamera.transform.up);
            }
        }

        ApplyVisual(Mathf.Clamp01(age / lifetime));
    }

    private static Camera GetMainCamera()
    {
        if (cachedCamera == null || !cachedCamera.isActiveAndEnabled)
        {
            cachedCamera = Camera.main;
        }

        return cachedCamera;
    }

    private void ApplyVisual(float progress)
    {
        float easedGrowth = Mathf.SmoothStep(0f, 1f, progress);
        transform.localScale = Vector3.one * Mathf.Lerp(startScale, endScale, easedGrowth);

        float fadeProgress = Mathf.SmoothStep(
            0f,
            1f,
            Mathf.Clamp01((progress - FadeStartProgress) / (1f - FadeStartProgress)));
        Color appliedColor = color;
        appliedColor.a *= 1f - fadeProgress;

        propertyBlock ??= new MaterialPropertyBlock();
        meshRenderer.GetPropertyBlock(propertyBlock);
        propertyBlock.SetColor(colorPropertyId, appliedColor);
        if (colorPropertyId != Shader.PropertyToID("_Color"))
        {
            propertyBlock.SetColor("_Color", appliedColor);
        }

        meshRenderer.SetPropertyBlock(propertyBlock);
    }

    private void Release()
    {
        gameObject.SetActive(false);
        Pool.Enqueue(this);
    }

    private void EnsureRenderer()
    {
        MeshFilter meshFilter = GetComponent<MeshFilter>();
        if (meshFilter == null)
        {
            meshFilter = gameObject.AddComponent<MeshFilter>();
        }

        meshRenderer = GetComponent<MeshRenderer>();
        if (meshRenderer == null)
        {
            meshRenderer = gameObject.AddComponent<MeshRenderer>();
        }

        meshFilter.sharedMesh = GetSharedMesh();
        meshRenderer.sharedMaterial = GetSharedMaterial();
        meshRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        meshRenderer.receiveShadows = false;
        propertyBlock ??= new MaterialPropertyBlock();
    }

    private static Mesh GetSharedMesh()
    {
        if (sharedMesh != null)
        {
            return sharedMesh;
        }

        int totalCircles = CloudCircleCount * DensityLayerCount;
        Vector3[] vertices = new Vector3[totalCircles * VerticesPerCircle];
        Vector2[] uvs = new Vector2[totalCircles * VerticesPerCircle];
        int[] triangles = new int[totalCircles * TrianglesPerCircle];
        Vector2[] layerOffsets =
        {
            Vector2.zero,
            new(0.055f, -0.035f),
        };
        float[] layerScales = { 1f, 0.9f };

        for (int layer = 0; layer < DensityLayerCount; layer++)
        {
            int circleStart = layer * CloudCircleCount;
            AddCloudCircle(vertices, uvs, triangles, circleStart, new Vector2(-0.23f, 0.04f) + layerOffsets[layer], 0.82f * layerScales[layer]);
            AddCloudCircle(vertices, uvs, triangles, circleStart + 1, new Vector2(0.2f, 0.03f) + layerOffsets[layer], 0.92f * layerScales[layer]);
            AddCloudCircle(vertices, uvs, triangles, circleStart + 2, new Vector2(0.02f, -0.18f) + layerOffsets[layer], 0.72f * layerScales[layer]);
        }

        sharedMesh = new Mesh
        {
            name = "CartoonSmokePuffMesh",
            vertices = vertices,
            uv = uvs,
            triangles = triangles,
            hideFlags = HideFlags.HideAndDontSave
        };
        sharedMesh.RecalculateBounds();
        return sharedMesh;
    }

    private static void AddCloudCircle(
        Vector3[] vertices,
        Vector2[] uvs,
        int[] triangles,
        int circleIndex,
        Vector2 center,
        float size)
    {
        int vertexStart = circleIndex * 4;
        int triangleStart = circleIndex * 6;
        float halfSize = size * 0.5f;

        vertices[vertexStart] = new Vector3(center.x - halfSize, center.y - halfSize, 0f);
        vertices[vertexStart + 1] = new Vector3(center.x - halfSize, center.y + halfSize, 0f);
        vertices[vertexStart + 2] = new Vector3(center.x + halfSize, center.y + halfSize, 0f);
        vertices[vertexStart + 3] = new Vector3(center.x + halfSize, center.y - halfSize, 0f);

        uvs[vertexStart] = new Vector2(0f, 0f);
        uvs[vertexStart + 1] = new Vector2(0f, 1f);
        uvs[vertexStart + 2] = new Vector2(1f, 1f);
        uvs[vertexStart + 3] = new Vector2(1f, 0f);

        triangles[triangleStart] = vertexStart;
        triangles[triangleStart + 1] = vertexStart + 1;
        triangles[triangleStart + 2] = vertexStart + 2;
        triangles[triangleStart + 3] = vertexStart;
        triangles[triangleStart + 4] = vertexStart + 2;
        triangles[triangleStart + 5] = vertexStart + 3;
    }

    private static Material GetSharedMaterial()
    {
        if (sharedMaterial != null)
        {
            return sharedMaterial;
        }

        Shader shader = Shader.Find("Sprites/Default");
        if (shader == null)
        {
            shader = Shader.Find("Universal Render Pipeline/Unlit");
        }

        if (shader == null)
        {
            shader = Shader.Find("Unlit/Transparent");
        }

        if (shader == null)
        {
            shader = Shader.Find("Standard");
        }

        sharedMaterial = new Material(shader)
        {
            name = "CartoonSmokePuffMaterial",
            hideFlags = HideFlags.HideAndDontSave,
            mainTexture = GetSharedTexture()
        };

        if (sharedMaterial.HasProperty("_Surface"))
        {
            sharedMaterial.SetFloat("_Surface", 1f);
        }

        if (sharedMaterial.HasProperty("_SrcBlend"))
        {
            sharedMaterial.SetFloat("_SrcBlend", 5f);
        }

        if (sharedMaterial.HasProperty("_DstBlend"))
        {
            sharedMaterial.SetFloat("_DstBlend", 10f);
        }

        if (sharedMaterial.HasProperty("_ZWrite"))
        {
            sharedMaterial.SetFloat("_ZWrite", 0f);
        }

        if (sharedMaterial.HasProperty("_Cull"))
        {
            sharedMaterial.SetFloat("_Cull", 0f);
        }

        if (sharedMaterial.HasProperty("_BaseMap"))
        {
            sharedMaterial.SetTexture("_BaseMap", GetSharedTexture());
        }

        if (sharedMaterial.HasProperty("_MainTex"))
        {
            sharedMaterial.SetTexture("_MainTex", GetSharedTexture());
        }

        colorPropertyId = sharedMaterial.HasProperty("_BaseColor")
            ? Shader.PropertyToID("_BaseColor")
            : Shader.PropertyToID("_Color");
        return sharedMaterial;
    }

    private static Texture2D GetSharedTexture()
    {
        if (sharedTexture != null)
        {
            return sharedTexture;
        }

        sharedTexture = new Texture2D(TextureSize, TextureSize, TextureFormat.RGBA32, false)
        {
            name = "CartoonSmokePuffTexture",
            hideFlags = HideFlags.HideAndDontSave,
            filterMode = FilterMode.Bilinear,
            wrapMode = TextureWrapMode.Clamp
        };

        Color[] pixels = new Color[TextureSize * TextureSize];
        Vector2 center = new((TextureSize - 1) * 0.5f, (TextureSize - 1) * 0.5f);
        float radius = TextureSize * 0.48f;
        for (int y = 0; y < TextureSize; y++)
        {
            for (int x = 0; x < TextureSize; x++)
            {
                float distance = Vector2.Distance(new Vector2(x, y), center) / radius;
                float alpha = 1f - Mathf.SmoothStep(0.7f, 1f, distance);
                alpha = Mathf.Pow(alpha, 0.68f);
                pixels[y * TextureSize + x] = new Color(1f, 1f, 1f, alpha);
            }
        }

        sharedTexture.SetPixels(pixels);
        sharedTexture.Apply(false, true);
        return sharedTexture;
    }
}
