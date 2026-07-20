using UnityEngine;

[DisallowMultipleComponent]
public class FireballSurfaceAnimator : MonoBehaviour
{
    private static readonly int FireFrameId = Shader.PropertyToID("_FireFrame");
    private static readonly int ProjectileForwardId = Shader.PropertyToID("_ProjectileForwardWS");
    private static readonly int AlphaId = Shader.PropertyToID("_Alpha");
    private static readonly int EmissionStrengthId = Shader.PropertyToID("_EmissionStrength");
    private static readonly int FrontStartId = Shader.PropertyToID("_FrontStart");
    private static readonly int FrontEndId = Shader.PropertyToID("_FrontEnd");
    private static readonly int DarkCutoffId = Shader.PropertyToID("_DarkCutoff");
    private static readonly int DarkSoftnessId = Shader.PropertyToID("_DarkSoftness");

    [Header("References")]
    [SerializeField] private Renderer flameShellRenderer;
    [SerializeField] private Transform flameShellTransform;
    [SerializeField] private Texture2D[] flameFrames;

    [Header("Flipbook")]
    [SerializeField, Min(0.1f)] private float frameRate = 18f;

    [Header("Shell")]
    [SerializeField, Range(1.03f, 1.12f)] private float shellScale = 1.08f;
    [SerializeField, Range(0f, 1f)] private float alpha = 0.88f;
    [SerializeField, Min(0f)] private float emissionStrength = 3f;
    [SerializeField, Range(-1f, 1f)] private float frontStart = -0.12f;
    [SerializeField, Range(-1f, 1f)] private float frontEnd = 0.64f;
    [SerializeField, Range(0f, 1f)] private float darkCutoff = 0.03f;
    [SerializeField, Range(0.001f, 1f)] private float darkSoftness = 0.14f;
    [SerializeField, Min(0.0001f)] private float velocityFallbackThreshold = 0.02f;

    private Rigidbody cachedRigidbody;
    private MaterialPropertyBlock propertyBlock;
    private Vector3 previousPosition;
    private int frameIndex;
    private float frameTimer;

    private void Awake()
    {
        ResolveReferences();
        previousPosition = transform.position;
        ApplyShellScale();
        ApplyFrame(forceFrame: true);
    }

    private void OnEnable()
    {
        previousPosition = transform.position;
        frameIndex = 0;
        frameTimer = 0f;
        ApplyShellScale();
        ApplyFrame(forceFrame: true);
    }

    private void Reset()
    {
        ResolveReferences();
    }

    private void OnValidate()
    {
        ResolveReferences();
        frontEnd = Mathf.Max(frontStart + 0.001f, frontEnd);
        ApplyShellScale();
        ApplyFrame(forceFrame: true);
    }

    private void LateUpdate()
    {
        ResolveReferences();
        AdvanceFrame(Time.deltaTime);
        ApplyShellScale();
        ApplyFrame(forceFrame: false);
        previousPosition = transform.position;
    }

    private void ResolveReferences()
    {
        if (flameShellRenderer == null)
        {
            Transform shell = transform.Find("FlameSurfaceShell");
            if (shell != null)
            {
                flameShellRenderer = shell.GetComponent<Renderer>();
            }
        }

        if (flameShellTransform == null && flameShellRenderer != null)
        {
            flameShellTransform = flameShellRenderer.transform;
        }

        if (cachedRigidbody == null)
        {
            cachedRigidbody = GetComponent<Rigidbody>();
        }

        propertyBlock ??= new MaterialPropertyBlock();
    }

    private void AdvanceFrame(float deltaTime)
    {
        if (flameFrames == null || flameFrames.Length <= 1 || frameRate <= 0f)
        {
            return;
        }

        frameTimer += deltaTime;
        float frameDuration = 1f / Mathf.Max(0.1f, frameRate);
        while (frameTimer >= frameDuration)
        {
            frameTimer -= frameDuration;
            frameIndex = (frameIndex + 1) % flameFrames.Length;
        }
    }

    private void ApplyShellScale()
    {
        if (flameShellTransform != null)
        {
            flameShellTransform.localScale = Vector3.one * shellScale;
        }
    }

    private void ApplyFrame(bool forceFrame)
    {
        if (flameShellRenderer == null || flameFrames == null || flameFrames.Length == 0)
        {
            return;
        }

        Texture2D frame = flameFrames[Mathf.Clamp(frameIndex, 0, flameFrames.Length - 1)];
        if (frame == null)
        {
            return;
        }

        flameShellRenderer.GetPropertyBlock(propertyBlock);
        propertyBlock.SetTexture(FireFrameId, frame);

        propertyBlock.SetVector(ProjectileForwardId, ResolveProjectileForward());
        propertyBlock.SetFloat(AlphaId, alpha);
        propertyBlock.SetFloat(EmissionStrengthId, emissionStrength);
        propertyBlock.SetFloat(FrontStartId, frontStart);
        propertyBlock.SetFloat(FrontEndId, Mathf.Max(frontStart + 0.001f, frontEnd));
        propertyBlock.SetFloat(DarkCutoffId, darkCutoff);
        propertyBlock.SetFloat(DarkSoftnessId, darkSoftness);
        flameShellRenderer.SetPropertyBlock(propertyBlock);
    }

    private Vector3 ResolveProjectileForward()
    {
        if (cachedRigidbody != null && cachedRigidbody.linearVelocity.sqrMagnitude > velocityFallbackThreshold * velocityFallbackThreshold)
        {
            return cachedRigidbody.linearVelocity.normalized;
        }

        Vector3 displacement = transform.position - previousPosition;
        if (displacement.sqrMagnitude > velocityFallbackThreshold * velocityFallbackThreshold)
        {
            return displacement.normalized;
        }

        return transform.forward.sqrMagnitude > 0.0001f ? transform.forward.normalized : Vector3.forward;
    }
}
