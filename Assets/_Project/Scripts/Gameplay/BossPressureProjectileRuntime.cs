using UnityEngine;
using UnityEngine.Rendering;

[DisallowMultipleComponent]
public class BossPressureProjectileRuntime : MonoBehaviour, IProjectilePlayerHitListener
{
    private const string AirPressureMaterialResourcePath = "VFX/AirPressureDistortion";
    private const string DistortionObjectName = "AirPressureDistortion";
    private const string DebugRadiusObjectName = "AirPressureDebugRadius";
    private const bool ShowDebugPressureRadius = false;

    private static readonly int DistortionStrengthId = Shader.PropertyToID("_DistortionStrength");
    private static readonly int RippleStrengthId = Shader.PropertyToID("_RippleStrength");
    private static readonly int FlowSpeedId = Shader.PropertyToID("_FlowSpeed");
    private static readonly int WaveStartTimeId = Shader.PropertyToID("_WaveStartTime");
    private static readonly int AlphaId = Shader.PropertyToID("_Alpha");

    private PlayerCombatController playerCombatController;
    private PlayerOrbitController playerOrbitController;
    private float projectileHitRadius;
    private float pressureShellRadius;
    private float pushDistance;
    private float pushDuration;
    private int rollCount;
    private float rollDuration;
    private Vector3 previousPosition;
    private bool configured;
    private bool pressureApplied;
    private GameObject distortionObject;
    private GameObject debugRadiusObject;
    private Material distortionMaterial;
    private Material debugRadiusMaterial;

    public void Configure(
        PlayerCombatController playerCombat,
        PlayerOrbitController playerOrbit,
        float hitRadius,
        float nearMissPressureShellRadius,
        float airPressurePushDistance,
        float airPressurePushDuration,
        int visualRollCount,
        float visualRollDuration)
    {
        playerCombatController = playerCombat;
        playerOrbitController = playerOrbit != null ? playerOrbit : FindAnyObjectByType<PlayerOrbitController>();
        projectileHitRadius = Mathf.Max(0f, hitRadius);
        pressureShellRadius = Mathf.Max(0f, nearMissPressureShellRadius);
        pushDistance = Mathf.Max(0f, airPressurePushDistance);
        pushDuration = Mathf.Max(0.01f, airPressurePushDuration);
        rollCount = Mathf.Max(0, visualRollCount);
        rollDuration = Mathf.Max(0.01f, visualRollDuration);
        previousPosition = transform.position;
        configured = true;
        EnsureDistortionVisual();
        EnsureDebugPressureRadiusVisual();
    }

    private void LateUpdate()
    {
        if (!configured)
        {
            previousPosition = transform.position;
            return;
        }

        UpdateDistortionVisual();
        UpdateDebugPressureRadiusVisual();
        TryApplyAirPressure(previousPosition, transform.position, allowAfterDeath: false);
        previousPosition = transform.position;
    }

    public void OnProjectilePlayerHit(Vector3 previousWorldPoint, Vector3 worldPoint, float projectileHitRadius)
    {
        if (projectileHitRadius > 0f)
        {
            this.projectileHitRadius = Mathf.Max(0f, projectileHitRadius);
        }

        TryApplyAirPressure(previousWorldPoint, worldPoint, allowAfterDeath: true);
    }

    private void OnDestroy()
    {
        if (debugRadiusMaterial != null)
        {
            Destroy(debugRadiusMaterial);
            debugRadiusMaterial = null;
        }

        if (distortionObject != null)
        {
            Destroy(distortionObject);
            distortionObject = null;
        }

        if (distortionMaterial != null)
        {
            Destroy(distortionMaterial);
            distortionMaterial = null;
        }
    }

    private void TryApplyAirPressure(Vector3 segmentStart, Vector3 segmentEnd, bool allowAfterDeath)
    {
        if (pressureApplied ||
            playerCombatController == null ||
            playerOrbitController == null ||
            (!allowAfterDeath && !playerCombatController.IsAlive) ||
            pressureShellRadius <= 0f)
        {
            return;
        }

        Vector3 playerPoint = playerCombatController.HitPoint;
        Vector3 closestPoint = ClosestPointOnSegment(playerPoint, segmentStart, segmentEnd);
        if (!playerCombatController.CheckHit(segmentStart, segmentEnd, pressureShellRadius))
        {
            return;
        }

        Vector3 pushDirection = playerPoint - closestPoint;
        if (pushDirection.sqrMagnitude <= 0.0001f)
        {
            pushDirection = ResolveFallbackPushDirection(playerPoint);
        }

        pressureApplied = true;
        playerOrbitController.ApplyAirPressureImpulse(
            pushDirection.normalized,
            pushDistance,
            pushDuration,
            rollCount,
            rollDuration);
    }

    private Vector3 ResolveFallbackPushDirection(Vector3 playerPoint)
    {
        Vector3 direction = playerPoint - transform.position;
        if (direction.sqrMagnitude > 0.0001f)
        {
            return direction.normalized;
        }

        Vector3 side = Vector3.Cross(transform.forward, Vector3.up);
        return side.sqrMagnitude > 0.0001f ? side.normalized : Vector3.right;
    }

    private void EnsureDistortionVisual()
    {
        if (distortionObject != null)
        {
            return;
        }

        Material template = Resources.Load<Material>(AirPressureMaterialResourcePath);
        if (template != null)
        {
            distortionMaterial = new Material(template)
            {
                name = "RuntimeAirPressureDistortionMaterial",
                hideFlags = HideFlags.HideAndDontSave
            };
        }
        else
        {
            Shader shader = Shader.Find("Titan Destroyer/VFX/Air Pressure Distortion");
            if (shader == null)
            {
                return;
            }

            distortionMaterial = new Material(shader)
            {
                name = "RuntimeAirPressureDistortionMaterial",
                hideFlags = HideFlags.HideAndDontSave
            };
        }

        distortionMaterial.renderQueue = 3100;
        distortionMaterial.SetFloat(WaveStartTimeId, Time.time);
        distortionObject = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        distortionObject.name = DistortionObjectName;
        distortionObject.transform.SetParent(transform, false);
        distortionObject.transform.localPosition = Vector3.zero;
        distortionObject.transform.localRotation = Quaternion.identity;
        distortionObject.transform.localScale = Vector3.one * (Mathf.Max(0.1f, pressureShellRadius) * 2f);

        Collider collider = distortionObject.GetComponent<Collider>();
        if (collider != null)
        {
            Destroy(collider);
        }

        Renderer renderer = distortionObject.GetComponent<Renderer>();
        if (renderer == null)
        {
            return;
        }

        renderer.sharedMaterial = distortionMaterial;
        renderer.shadowCastingMode = ShadowCastingMode.Off;
        renderer.receiveShadows = false;
    }

    private void UpdateDistortionVisual()
    {
        if (distortionObject != null)
        {
            SetChildSphereWorldRadius(distortionObject.transform, Mathf.Max(0.1f, pressureShellRadius));
        }

        if (distortionMaterial == null)
        {
            return;
        }

        distortionMaterial.SetFloat(DistortionStrengthId, 0.045f);
        distortionMaterial.SetFloat(RippleStrengthId, 0.92f);
        distortionMaterial.SetFloat(FlowSpeedId, 2.4f);
        distortionMaterial.SetFloat(AlphaId, 0.78f);
    }

    private void EnsureDebugPressureRadiusVisual()
    {
        if (!ShowDebugPressureRadius || debugRadiusObject != null)
        {
            return;
        }

        debugRadiusMaterial = CreateDebugRadiusMaterial();
        if (debugRadiusMaterial == null)
        {
            return;
        }

        debugRadiusObject = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        debugRadiusObject.name = DebugRadiusObjectName;
        debugRadiusObject.transform.SetParent(transform, false);
        debugRadiusObject.transform.localPosition = Vector3.zero;
        debugRadiusObject.transform.localRotation = Quaternion.identity;

        Collider collider = debugRadiusObject.GetComponent<Collider>();
        if (collider != null)
        {
            Destroy(collider);
        }

        Renderer renderer = debugRadiusObject.GetComponent<Renderer>();
        if (renderer == null)
        {
            return;
        }

        renderer.sharedMaterial = debugRadiusMaterial;
        renderer.shadowCastingMode = ShadowCastingMode.Off;
        renderer.receiveShadows = false;
        UpdateDebugPressureRadiusVisual();
    }

    private void UpdateDebugPressureRadiusVisual()
    {
        if (!ShowDebugPressureRadius || debugRadiusObject == null)
        {
            return;
        }

        float debugRadius = Mathf.Max(0.01f, pressureShellRadius);
        SetChildSphereWorldRadius(debugRadiusObject.transform, debugRadius);
    }

    private static void SetChildSphereWorldRadius(Transform child, float worldRadius)
    {
        if (child == null)
        {
            return;
        }

        Transform parent = child.parent;
        Vector3 parentScale = parent != null ? parent.lossyScale : Vector3.one;
        float diameter = Mathf.Max(0.01f, worldRadius * 2f);
        child.localScale = new Vector3(
            diameter / Mathf.Max(0.0001f, Mathf.Abs(parentScale.x)),
            diameter / Mathf.Max(0.0001f, Mathf.Abs(parentScale.y)),
            diameter / Mathf.Max(0.0001f, Mathf.Abs(parentScale.z)));
    }

    private static Material CreateDebugRadiusMaterial()
    {
        Shader shader =
            Shader.Find("Universal Render Pipeline/Unlit") ??
            Shader.Find("Unlit/Color") ??
            Shader.Find("Sprites/Default");
        if (shader == null)
        {
            return null;
        }

        Material material = new(shader)
        {
            name = "RuntimeAirPressureDebugRadiusMaterial",
            hideFlags = HideFlags.HideAndDontSave,
            renderQueue = 3050
        };

        if (material.HasProperty("_Color"))
        {
            material.SetColor("_Color", new Color(0.1f, 0.85f, 1f, 0.18f));
        }

        if (material.HasProperty("_BaseColor"))
        {
            material.SetColor("_BaseColor", new Color(0.1f, 0.85f, 1f, 0.18f));
        }

        if (material.HasProperty("_Surface"))
        {
            material.SetFloat("_Surface", 1f);
        }

        if (material.HasProperty("_Blend"))
        {
            material.SetFloat("_Blend", 0f);
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

        return material;
    }

    private static Vector3 ClosestPointOnSegment(Vector3 point, Vector3 start, Vector3 end)
    {
        Vector3 segment = end - start;
        float segmentLengthSqr = segment.sqrMagnitude;
        if (segmentLengthSqr <= 0.0001f)
        {
            return start;
        }

        float t = Mathf.Clamp01(Vector3.Dot(point - start, segment) / segmentLengthSqr);
        return start + segment * t;
    }
}
