using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

[DisallowMultipleComponent]
public sealed class MountedSidewinderCosmeticController : MonoBehaviour
{
    public const float CosmeticDamage = 0f;
    public const float DefaultIgnitionDuration = 1f;
    public const float DefaultMaximumFlightDuration = 6f;
    public const float DefaultSlowFlightDuration = 0.5f;
    public const float DefaultCosmeticLaunchSpeed = 5f;
    public const float DefaultCosmeticCruiseSpeed = 35f;
    public const float DefaultCosmeticAcceleration = 20f;

    private const string PlayerVisualRootName = "PlayerVisualRoot";
    private const string LeftPylonName = "WeponePylon_L_03";
    private const string RightPylonName = "WeponePylon_R_03";
    private const string SidewinderRootName = "Sidewinder";
    private const string NozzleName = "FX_Nozzle";
    private const string ExhaustEffectName = "MountedSidewinderExhaust";

    [SerializeField, Min(0.01f)]
    private float ignitionDuration = DefaultIgnitionDuration;
    [SerializeField, Min(0.1f)]
    private float maximumFlightDuration = DefaultMaximumFlightDuration;
    [SerializeField, Min(0.01f)] private float visualHitRadius = 1.8f;
    [Header("Cosmetic Flight Visibility")]
    [SerializeField, Min(0f), Tooltip(
        "Seconds the detached Sidewinders remain at their slow launch speed before accelerating.")]
    private float slowFlightDuration = DefaultSlowFlightDuration;
    [SerializeField, Min(0.1f)]
    private float cosmeticLaunchSpeed = DefaultCosmeticLaunchSpeed;
    [SerializeField, Min(0.1f)]
    private float cosmeticCruiseSpeed = DefaultCosmeticCruiseSpeed;
    [SerializeField, Min(0f)]
    private float cosmeticAcceleration = DefaultCosmeticAcceleration;
    [SerializeField, Min(0f)] private float cosmeticTurnRate = 180f;

    private readonly MountedSidewinderBinding[] bindings = new MountedSidewinderBinding[2];
    private PlayerLockOnController lockOnController;
    private PlayerCombatController playerCombatController;
    private PlayerOrbitController playerOrbitController;
    private PlayerVisualOverlayRenderer overlayRenderer;
    private Coroutine sequenceRoutine;
    private int activeSalvoId;
    private static Material sharedExhaustMaterial;

    public event Action<int> MountedSidewindersDetached;

    public float IgnitionDuration => ignitionDuration;
    public float MaximumFlightDuration => maximumFlightDuration;
    public float DamagePerCosmeticMissile => CosmeticDamage;
    public bool IsIgniting { get; private set; }
    public bool IsWaitingForVisualTurn { get; private set; }
    public bool IsFlightActive { get; private set; }
    public int ActiveSalvoId => activeSalvoId;
    public int ResolvedMountedSidewinderCount => CountResolvedBindings();
    public int LastDetachedSidewinderCount { get; private set; }
    public int LastRestoredSidewinderCount { get; private set; }
    public int ActiveExhaustCount => CountActiveExhausts();
    public int LastIgnitionExhaustCount { get; private set; }
    public bool LastIgnitionStartedAfterVisualTurn { get; private set; }
    public float LastIgnitionVisualTurnProgress { get; private set; }
    public float LastPreIgnitionDelay { get; private set; }
    public float SlowFlightDuration => slowFlightDuration;
    public float CosmeticLaunchSpeed => cosmeticLaunchSpeed;
    public float CosmeticCruiseSpeed => cosmeticCruiseSpeed;
    public float CosmeticAcceleration => cosmeticAcceleration;
    public float ExpectedDetachmentDelay =>
        (playerOrbitController != null
            ? playerOrbitController.DebugFullSalvoVisualTurnDuration
            : 0f) + ignitionDuration;
    public float LastInitialFlightSpeed { get; private set; }
    public float MinimumObservedFlightSpeed { get; private set; }
    public float PeakObservedFlightSpeed { get; private set; }
    public string LastBindingFailure { get; private set; } = string.Empty;

    public void Configure(
        PlayerLockOnController controller,
        PlayerCombatController combatController,
        PlayerOrbitController orbitController)
    {
        Unsubscribe();
        StopSequenceAndRestore();
        lockOnController = controller;
        playerCombatController = combatController;
        playerOrbitController = orbitController;
        overlayRenderer = ResolveOverlayRenderer();
        ResolveMountedSidewinders();
        Subscribe();
    }

    public bool RefreshBindingsForDebug()
    {
        StopSequenceAndRestore();
        return ResolveMountedSidewinders();
    }

    public void CancelActiveSequence()
    {
        StopSequenceAndRestore();
    }

    private void HandleFullSalvoStarting(int salvoId)
    {
        if (salvoId <= 0 || lockOnController == null)
        {
            return;
        }

        StopSequenceAndRestore();
        if (!ResolveMountedSidewinders())
        {
            Debug.LogWarning(
                $"[MountedSidewinder] Cosmetic launch skipped: {LastBindingFailure}",
                this);
            return;
        }

        IReadOnlyList<SalvoTargetSnapshot> targets =
            lockOnController.LastReleaseIntent?.TargetSnapshots;
        if (targets == null || targets.Count == 0)
        {
            LastBindingFailure = "FullSalvoTargetsUnavailable";
            Debug.LogWarning(
                "[MountedSidewinder] Cosmetic launch skipped because no full-salvo target snapshot exists.",
                this);
            return;
        }

        activeSalvoId = salvoId;
        LastDetachedSidewinderCount = 0;
        LastRestoredSidewinderCount = 0;
        LastIgnitionExhaustCount = 0;
        LastIgnitionStartedAfterVisualTurn = false;
        LastIgnitionVisualTurnProgress = 0f;
        LastPreIgnitionDelay = 0f;
        LastInitialFlightSpeed = 0f;
        MinimumObservedFlightSpeed = 0f;
        PeakObservedFlightSpeed = 0f;
        sequenceRoutine = StartCoroutine(RunLaunchSequence(targets));
    }

    private void HandleSalvoFinished(int salvoId, bool canceled)
    {
        if (canceled && salvoId == activeSalvoId)
        {
            StopSequenceAndRestore();
        }
    }

    private IEnumerator RunLaunchSequence(IReadOnlyList<SalvoTargetSnapshot> targets)
    {
        IsWaitingForVisualTurn = true;
        float preIgnitionElapsed = 0f;

        // The shared lock-on turn starts before OnFullSalvoStarting. Preserve the
        // one-frame lead-in before checking its progress and beginning ignition.
        yield return null;
        while (ShouldWaitForVisualTurn())
        {
            preIgnitionElapsed += Time.deltaTime;
            yield return null;
        }

        IsWaitingForVisualTurn = false;
        LastPreIgnitionDelay = preIgnitionElapsed;
        LastIgnitionVisualTurnProgress = playerOrbitController != null
            ? playerOrbitController.FullSalvoVisualTurnProgress
            : 1f;
        LastIgnitionStartedAfterVisualTurn = playerOrbitController == null ||
                                             (!playerOrbitController.IsFullSalvoVisualTurning &&
                                              LastIgnitionVisualTurnProgress >= 0.999f);

        IsIgniting = true;
        for (int i = 0; i < bindings.Length; i++)
        {
            BeginExhaust(bindings[i]);
        }

        LastIgnitionExhaustCount = CountActiveExhausts();

        float ignitionElapsed = 0f;
        while (ignitionElapsed < ignitionDuration)
        {
            ignitionElapsed += Time.deltaTime;
            yield return null;
        }

        IsIgniting = false;
        overlayRenderer = ResolveOverlayRenderer();
        for (int i = 0; i < bindings.Length; i++)
        {
            SalvoTargetSnapshot target = targets[Mathf.Min(i, targets.Count - 1)];
            DetachForFlight(bindings[i], target);
        }

        MountedSidewindersDetached?.Invoke(activeSalvoId);

        IsFlightActive = true;
        float flightElapsed = 0f;
        while (flightElapsed < maximumFlightDuration && HasActiveFlight())
        {
            float deltaTime = Time.deltaTime;
            flightElapsed += deltaTime;
            for (int i = 0; i < bindings.Length; i++)
            {
                UpdateFlight(bindings[i], deltaTime);
            }

            yield return null;
        }

        for (int i = 0; i < bindings.Length; i++)
        {
            RestoreBinding(bindings[i]);
        }

        IsFlightActive = false;
        activeSalvoId = 0;
        sequenceRoutine = null;
    }

    private bool ShouldWaitForVisualTurn()
    {
        return playerOrbitController != null &&
               playerOrbitController.FullSalvoVisualSalvoId == activeSalvoId &&
               (playerOrbitController.IsFullSalvoVisualTurning ||
                playerOrbitController.FullSalvoVisualTurnProgress < 0.999f);
    }

    private bool ResolveMountedSidewinders()
    {
        UnregisterBindingsFromVisualCentering();
        LastBindingFailure = string.Empty;
        Transform visualRoot = ResolvePlayerVisualRoot();
        if (visualRoot == null)
        {
            LastBindingFailure = "PlayerVisualRootUnavailable";
            ClearBindings();
            return false;
        }

        bindings[0] = CreateBinding(visualRoot, LeftPylonName);
        bindings[1] = CreateBinding(visualRoot, RightPylonName);
        if (bindings[0] == null || bindings[1] == null)
        {
            LastBindingFailure = bindings[0] == null && bindings[1] == null
                ? "LeftAndRightMountedSidewindersUnavailable"
                : bindings[0] == null
                    ? "LeftMountedSidewinderUnavailable"
                    : "RightMountedSidewinderUnavailable";
            return false;
        }

        RegisterBindingsOutsideVisualCentering();
        return true;
    }

    private void RegisterBindingsOutsideVisualCentering()
    {
        overlayRenderer = ResolveOverlayRenderer();
        if (overlayRenderer == null)
        {
            return;
        }

        for (int i = 0; i < bindings.Length; i++)
        {
            if (bindings[i]?.Root != null)
            {
                overlayRenderer.RegisterCenteringIgnoredRoot(bindings[i].Root);
            }
        }
    }

    private void UnregisterBindingsFromVisualCentering()
    {
        if (overlayRenderer == null)
        {
            return;
        }

        for (int i = 0; i < bindings.Length; i++)
        {
            if (bindings[i]?.Root != null)
            {
                overlayRenderer.UnregisterCenteringIgnoredRoot(bindings[i].Root);
            }
        }
    }

    private Transform ResolvePlayerVisualRoot()
    {
        overlayRenderer = ResolveOverlayRenderer();
        if (overlayRenderer != null && overlayRenderer.VisualRoot != null)
        {
            return overlayRenderer.VisualRoot;
        }

        Transform owner = playerCombatController != null
            ? playerCombatController.transform
            : transform;
        return owner.name == PlayerVisualRootName
            ? owner
            : FindDeepChild(owner, PlayerVisualRootName);
    }

    private PlayerVisualOverlayRenderer ResolveOverlayRenderer()
    {
        if (playerOrbitController != null &&
            playerOrbitController.OriginalVisualOverlayRenderer != null)
        {
            return playerOrbitController.OriginalVisualOverlayRenderer;
        }

        return playerCombatController != null
            ? playerCombatController.GetComponent<PlayerVisualOverlayRenderer>()
            : null;
    }

    private static MountedSidewinderBinding CreateBinding(
        Transform visualRoot,
        string pylonName)
    {
        Transform pylon = FindDeepChild(visualRoot, pylonName);
        Transform sidewinderRoot = FindDirectChild(pylon, SidewinderRootName);
        Transform nozzle = FindDeepChild(sidewinderRoot, NozzleName);
        if (sidewinderRoot == null || nozzle == null ||
            sidewinderRoot.GetComponentInChildren<Renderer>(true) == null)
        {
            return null;
        }

        return new MountedSidewinderBinding(sidewinderRoot, nozzle);
    }

    private void BeginExhaust(MountedSidewinderBinding binding)
    {
        if (binding == null || binding.Root == null || binding.Nozzle == null)
        {
            return;
        }

        StopExhaust(binding);
        GameObject effectObject = new(ExhaustEffectName);
        effectObject.transform.SetParent(binding.Nozzle, false);
        effectObject.transform.localPosition = Vector3.zero;

        Vector3 exhaustDirection = binding.Nozzle.position - binding.Root.position;
        if (exhaustDirection.sqrMagnitude < 0.0001f)
        {
            exhaustDirection = binding.Nozzle.forward;
        }

        Vector3 upDirection = playerOrbitController != null
            ? playerOrbitController.transform.up
            : Vector3.up;
        effectObject.transform.rotation = Quaternion.LookRotation(
            exhaustDirection.normalized,
            ResolveNonParallelUp(exhaustDirection.normalized, upDirection));

        ParticleSystem particles = effectObject.AddComponent<ParticleSystem>();
        particles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        ParticleSystem.MainModule main = particles.main;
        main.loop = true;
        main.playOnAwake = false;
        main.duration = 1f;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.scalingMode = ParticleSystemScalingMode.Shape;
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.22f, 0.42f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(4.5f, 8f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.2f, 0.34f);
        main.startColor = new ParticleSystem.MinMaxGradient(
            new Color(1f, 0.92f, 0.3f, 1f),
            new Color(1f, 0.22f, 0.02f, 0.92f));
        main.maxParticles = 220;

        ParticleSystem.EmissionModule emission = particles.emission;
        emission.enabled = true;
        emission.rateOverTime = 180f;

        ParticleSystem.ShapeModule shape = particles.shape;
        shape.enabled = true;
        shape.shapeType = ParticleSystemShapeType.Cone;
        shape.angle = 7f;
        shape.radius = 0.04f;
        shape.radiusThickness = 0.2f;

        ParticleSystem.ColorOverLifetimeModule colorOverLifetime =
            particles.colorOverLifetime;
        colorOverLifetime.enabled = true;
        colorOverLifetime.color = new ParticleSystem.MinMaxGradient(
            CreateExhaustGradient());

        ParticleSystem.SizeOverLifetimeModule sizeOverLifetime =
            particles.sizeOverLifetime;
        sizeOverLifetime.enabled = true;
        AnimationCurve sizeCurve = new();
        sizeCurve.AddKey(0f, 0.55f);
        sizeCurve.AddKey(0.35f, 1f);
        sizeCurve.AddKey(1f, 0.08f);
        sizeOverLifetime.size = new ParticleSystem.MinMaxCurve(1f, sizeCurve);

        ParticleSystemRenderer renderer =
            effectObject.GetComponent<ParticleSystemRenderer>();
        renderer.renderMode = ParticleSystemRenderMode.Billboard;
        renderer.alignment = ParticleSystemRenderSpace.View;
        renderer.shadowCastingMode = ShadowCastingMode.Off;
        renderer.receiveShadows = false;
        renderer.sharedMaterial = GetSharedExhaustMaterial();

        TrailRenderer trail = effectObject.AddComponent<TrailRenderer>();
        trail.time = 0.45f;
        trail.minVertexDistance = 0.025f;
        trail.widthCurve = new AnimationCurve(
            new Keyframe(0f, 0.28f),
            new Keyframe(0.35f, 0.18f),
            new Keyframe(1f, 0f));
        trail.colorGradient = CreateExhaustGradient();
        trail.alignment = LineAlignment.View;
        trail.textureMode = LineTextureMode.Stretch;
        trail.shadowCastingMode = ShadowCastingMode.Off;
        trail.receiveShadows = false;
        trail.sharedMaterial = GetSharedExhaustMaterial();
        trail.emitting = true;
        trail.Clear();

        int visualLayer = overlayRenderer != null
            ? overlayRenderer.VisualLayer
            : binding.Root.gameObject.layer;
        effectObject.layer = visualLayer >= 0 ? visualLayer : binding.Root.gameObject.layer;
        binding.Exhaust = particles;
        binding.ExhaustTrail = trail;
        particles.Play(true);
    }

    private void DetachForFlight(
        MountedSidewinderBinding binding,
        SalvoTargetSnapshot target)
    {
        if (binding == null || binding.Root == null)
        {
            return;
        }

        binding.CaptureMountPose();
        Vector3 noseDirection = binding.Root.position - binding.Nozzle.position;
        if (noseDirection.sqrMagnitude < 0.0001f)
        {
            noseDirection = -binding.Root.up;
        }

        binding.LocalNoseAxis = binding.Root.InverseTransformDirection(
            noseDirection.normalized);
        binding.Target = target?.Target;
        binding.FallbackTargetWorldPosition = target != null
            ? target.TargetWorldPosition
            : binding.Root.position + noseDirection.normalized * 40f;
        binding.FlightElapsed = 0f;
        binding.CurrentSpeed = EvaluateCosmeticFlightSpeed(
            binding.FlightElapsed,
            cosmeticLaunchSpeed,
            cosmeticCruiseSpeed,
            slowFlightDuration,
            cosmeticAcceleration);
        if (LastDetachedSidewinderCount == 0)
        {
            LastInitialFlightSpeed = binding.CurrentSpeed;
            MinimumObservedFlightSpeed = binding.CurrentSpeed;
            PeakObservedFlightSpeed = binding.CurrentSpeed;
        }

        binding.InFlight = true;
        binding.Root.SetParent(null, true);
        overlayRenderer?.RegisterExternalVisualRoot(binding.Root);
        LastDetachedSidewinderCount++;
    }

    private void UpdateFlight(MountedSidewinderBinding binding, float deltaTime)
    {
        if (binding == null || !binding.InFlight || binding.Root == null ||
            deltaTime <= 0f)
        {
            return;
        }

        Vector3 targetPosition = binding.Target != null &&
                                 binding.Target.gameObject.activeInHierarchy
            ? binding.Target.position
            : binding.FallbackTargetWorldPosition;
        Vector3 toTarget = targetPosition - binding.Root.position;
        if (toTarget.sqrMagnitude <= visualHitRadius * visualHitRadius)
        {
            RestoreBinding(binding);
            return;
        }

        Vector3 desiredDirection = toTarget.normalized;
        Vector3 currentNoseDirection = binding.Root.TransformDirection(
            binding.LocalNoseAxis);
        if (currentNoseDirection.sqrMagnitude < 0.0001f)
        {
            currentNoseDirection = desiredDirection;
        }

        Quaternion desiredRotation = Quaternion.FromToRotation(
            currentNoseDirection.normalized,
            desiredDirection) * binding.Root.rotation;
        float turnRate = Mathf.Max(0f, cosmeticTurnRate);
        binding.Root.rotation = Quaternion.RotateTowards(
            binding.Root.rotation,
            desiredRotation,
            turnRate * deltaTime);

        binding.FlightElapsed += deltaTime;
        binding.CurrentSpeed = EvaluateCosmeticFlightSpeed(
            binding.FlightElapsed,
            cosmeticLaunchSpeed,
            cosmeticCruiseSpeed,
            slowFlightDuration,
            cosmeticAcceleration);
        MinimumObservedFlightSpeed = Mathf.Min(
            MinimumObservedFlightSpeed,
            binding.CurrentSpeed);
        PeakObservedFlightSpeed = Mathf.Max(
            PeakObservedFlightSpeed,
            binding.CurrentSpeed);

        Vector3 flightDirection = binding.Root.TransformDirection(
            binding.LocalNoseAxis).normalized;
        Vector3 previousPosition = binding.Root.position;
        Vector3 nextPosition = previousPosition +
                               flightDirection * binding.CurrentSpeed * deltaTime;
        binding.Root.position = nextPosition;

        if (SegmentReachesTarget(
                previousPosition,
                nextPosition,
                targetPosition,
                visualHitRadius))
        {
            RestoreBinding(binding);
        }
    }

    public static float EvaluateCosmeticFlightSpeed(
        float flightElapsed,
        float launchSpeed,
        float cruiseSpeed,
        float slowDuration,
        float acceleration)
    {
        float safeLaunchSpeed = Mathf.Max(0.1f, launchSpeed);
        float safeCruiseSpeed = Mathf.Max(safeLaunchSpeed, cruiseSpeed);
        float accelerationElapsed = Mathf.Max(
            0f,
            Mathf.Max(0f, flightElapsed) - Mathf.Max(0f, slowDuration));
        return Mathf.MoveTowards(
            safeLaunchSpeed,
            safeCruiseSpeed,
            Mathf.Max(0f, acceleration) * accelerationElapsed);
    }

    public static bool SegmentReachesTarget(
        Vector3 segmentStart,
        Vector3 segmentEnd,
        Vector3 target,
        float radius)
    {
        Vector3 segment = segmentEnd - segmentStart;
        float lengthSquared = segment.sqrMagnitude;
        float progress = lengthSquared <= 0.000001f
            ? 0f
            : Mathf.Clamp01(Vector3.Dot(target - segmentStart, segment) / lengthSquared);
        Vector3 closestPoint = segmentStart + segment * progress;
        float safeRadius = Mathf.Max(0f, radius);
        return (target - closestPoint).sqrMagnitude <= safeRadius * safeRadius;
    }

    private bool HasActiveFlight()
    {
        for (int i = 0; i < bindings.Length; i++)
        {
            if (bindings[i] != null && bindings[i].InFlight)
            {
                return true;
            }
        }

        return false;
    }

    private void RestoreBinding(MountedSidewinderBinding binding)
    {
        if (binding == null || binding.Root == null)
        {
            return;
        }

        bool wasDetached = binding.InFlight || binding.Root.parent != binding.MountParent;
        binding.RestoreMountPose();
        // Reattach first so unregistering refreshes the same renderer as part of
        // the helicopter root without one-frame layer flicker.
        overlayRenderer?.UnregisterExternalVisualRoot(binding.Root);
        StopExhaust(binding);
        binding.Target = null;
        binding.InFlight = false;
        if (wasDetached)
        {
            LastRestoredSidewinderCount++;
        }
    }

    private void StopSequenceAndRestore()
    {
        if (sequenceRoutine != null)
        {
            StopCoroutine(sequenceRoutine);
            sequenceRoutine = null;
        }

        for (int i = 0; i < bindings.Length; i++)
        {
            RestoreBinding(bindings[i]);
        }

        IsIgniting = false;
        IsWaitingForVisualTurn = false;
        IsFlightActive = false;
        activeSalvoId = 0;
    }

    private static void StopExhaust(MountedSidewinderBinding binding)
    {
        if (binding?.Exhaust == null)
        {
            return;
        }

        ParticleSystem particles = binding.Exhaust;
        binding.Exhaust = null;
        binding.ExhaustTrail = null;
        particles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        GameObject effectObject = particles.gameObject;
        if (Application.isPlaying)
        {
            Destroy(effectObject);
        }
        else
        {
            DestroyImmediate(effectObject);
        }
    }

    private int CountResolvedBindings()
    {
        int count = 0;
        for (int i = 0; i < bindings.Length; i++)
        {
            if (bindings[i] != null && bindings[i].Root != null)
            {
                count++;
            }
        }

        return count;
    }

    private int CountActiveExhausts()
    {
        int count = 0;
        for (int i = 0; i < bindings.Length; i++)
        {
            if (bindings[i]?.Exhaust != null && bindings[i].Exhaust.isPlaying)
            {
                count++;
            }
        }

        return count;
    }

    private void ClearBindings()
    {
        UnregisterBindingsFromVisualCentering();
        for (int i = 0; i < bindings.Length; i++)
        {
            bindings[i] = null;
        }
    }

    private void Subscribe()
    {
        if (lockOnController == null || !isActiveAndEnabled)
        {
            return;
        }

        lockOnController.OnFullSalvoStarting -= HandleFullSalvoStarting;
        lockOnController.OnLockOnSalvoFinished -= HandleSalvoFinished;
        lockOnController.OnFullSalvoStarting += HandleFullSalvoStarting;
        lockOnController.OnLockOnSalvoFinished += HandleSalvoFinished;
    }

    private void Unsubscribe()
    {
        if (lockOnController == null)
        {
            return;
        }

        lockOnController.OnFullSalvoStarting -= HandleFullSalvoStarting;
        lockOnController.OnLockOnSalvoFinished -= HandleSalvoFinished;
    }

    private void OnEnable()
    {
        Subscribe();
    }

    private void OnDisable()
    {
        Unsubscribe();
        StopSequenceAndRestore();
    }

    private void OnDestroy()
    {
        Unsubscribe();
        StopSequenceAndRestore();
        ClearBindings();
    }

    private void OnValidate()
    {
        ignitionDuration = Mathf.Max(0.01f, ignitionDuration);
        maximumFlightDuration = Mathf.Max(0.1f, maximumFlightDuration);
        visualHitRadius = Mathf.Max(0.01f, visualHitRadius);
        slowFlightDuration = Mathf.Max(0f, slowFlightDuration);
        cosmeticLaunchSpeed = Mathf.Max(0.1f, cosmeticLaunchSpeed);
        cosmeticCruiseSpeed = Mathf.Max(cosmeticLaunchSpeed, cosmeticCruiseSpeed);
        cosmeticAcceleration = Mathf.Max(0f, cosmeticAcceleration);
        cosmeticTurnRate = Mathf.Max(0f, cosmeticTurnRate);
    }

    private static Transform FindDirectChild(Transform parent, string childName)
    {
        if (parent == null || string.IsNullOrWhiteSpace(childName))
        {
            return null;
        }

        for (int i = 0; i < parent.childCount; i++)
        {
            Transform child = parent.GetChild(i);
            if (child.name == childName)
            {
                return child;
            }
        }

        return null;
    }

    private static Transform FindDeepChild(Transform parent, string childName)
    {
        if (parent == null || string.IsNullOrWhiteSpace(childName))
        {
            return null;
        }

        for (int i = 0; i < parent.childCount; i++)
        {
            Transform child = parent.GetChild(i);
            if (child.name == childName)
            {
                return child;
            }

            Transform nested = FindDeepChild(child, childName);
            if (nested != null)
            {
                return nested;
            }
        }

        return null;
    }

    private static Vector3 ResolveNonParallelUp(Vector3 forward, Vector3 preferredUp)
    {
        Vector3 up = preferredUp.sqrMagnitude > 0.0001f
            ? preferredUp.normalized
            : Vector3.up;
        return Mathf.Abs(Vector3.Dot(forward, up)) > 0.98f
            ? Vector3.right
            : up;
    }

    private static Gradient CreateExhaustGradient()
    {
        Gradient gradient = new();
        gradient.SetKeys(
            new[]
            {
                new GradientColorKey(new Color(1f, 0.96f, 0.5f), 0f),
                new GradientColorKey(new Color(1f, 0.38f, 0.03f), 0.48f),
                new GradientColorKey(new Color(0.55f, 0.04f, 0.01f), 1f),
            },
            new[]
            {
                new GradientAlphaKey(1f, 0f),
                new GradientAlphaKey(0.86f, 0.5f),
                new GradientAlphaKey(0f, 1f),
            });
        return gradient;
    }

    private static Material GetSharedExhaustMaterial()
    {
        if (sharedExhaustMaterial != null)
        {
            return sharedExhaustMaterial;
        }

        Shader shader = Shader.Find("Universal Render Pipeline/Particles/Unlit");
        if (shader == null)
        {
            shader = Shader.Find("Particles/Standard Unlit");
        }

        if (shader == null)
        {
            shader = Shader.Find("Sprites/Default");
        }

        sharedExhaustMaterial = new Material(shader)
        {
            name = "MountedSidewinderExhaustMaterial",
            hideFlags = HideFlags.HideAndDontSave,
        };
        if (sharedExhaustMaterial.HasProperty("_Surface"))
        {
            sharedExhaustMaterial.SetFloat("_Surface", 1f);
        }

        if (sharedExhaustMaterial.HasProperty("_Blend"))
        {
            sharedExhaustMaterial.SetFloat("_Blend", 1f);
        }

        if (sharedExhaustMaterial.HasProperty("_SrcBlend"))
        {
            sharedExhaustMaterial.SetFloat("_SrcBlend", 5f);
        }

        if (sharedExhaustMaterial.HasProperty("_DstBlend"))
        {
            sharedExhaustMaterial.SetFloat("_DstBlend", 1f);
        }

        if (sharedExhaustMaterial.HasProperty("_ZWrite"))
        {
            sharedExhaustMaterial.SetFloat("_ZWrite", 0f);
        }

        return sharedExhaustMaterial;
    }

    private sealed class MountedSidewinderBinding
    {
        public MountedSidewinderBinding(Transform root, Transform nozzle)
        {
            Root = root;
            Nozzle = nozzle;
            CaptureMountPose();
        }

        public Transform Root { get; }
        public Transform Nozzle { get; }
        public Transform MountParent { get; private set; }
        public Vector3 MountLocalPosition { get; private set; }
        public Quaternion MountLocalRotation { get; private set; }
        public Vector3 MountLocalScale { get; private set; }
        public ParticleSystem Exhaust { get; set; }
        public TrailRenderer ExhaustTrail { get; set; }
        public Transform Target { get; set; }
        public Vector3 FallbackTargetWorldPosition { get; set; }
        public Vector3 LocalNoseAxis { get; set; }
        public float FlightElapsed { get; set; }
        public float CurrentSpeed { get; set; }
        public bool InFlight { get; set; }

        public void CaptureMountPose()
        {
            if (Root == null || InFlight)
            {
                return;
            }

            MountParent = Root.parent;
            MountLocalPosition = Root.localPosition;
            MountLocalRotation = Root.localRotation;
            MountLocalScale = Root.localScale;
        }

        public void RestoreMountPose()
        {
            if (Root == null || MountParent == null)
            {
                return;
            }

            Root.SetParent(MountParent, false);
            Root.localPosition = MountLocalPosition;
            Root.localRotation = MountLocalRotation;
            Root.localScale = MountLocalScale;
        }
    }
}
