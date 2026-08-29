using UnityEngine;

[DisallowMultipleComponent]
public sealed class BackgroundAllyUnitView : MonoBehaviour
{
    [SerializeField] private Transform visualRoot;
    [SerializeField] private Transform muzzle;
    [SerializeField] private Transform mainRotorBlur;
    [SerializeField] private Transform tailRotorBlur;
    [SerializeField] private Renderer[] muzzleFlashRenderers;
    [SerializeField] private ParticleSystem crashSmoke;
    [SerializeField] private Renderer[] cachedRenderers;

    public Transform VisualRoot => visualRoot != null ? visualRoot : transform;
    public Transform Muzzle => muzzle != null ? muzzle : VisualRoot;
    public Transform MainRotorBlur => mainRotorBlur;
    public Transform TailRotorBlur => tailRotorBlur;
    public ParticleSystem CrashSmoke => crashSmoke;
    public Renderer[] CachedRenderers => cachedRenderers;
    public bool IsMuzzleFlashVisible
    {
        get
        {
            if (muzzleFlashRenderers == null)
            {
                return false;
            }

            for (int i = 0; i < muzzleFlashRenderers.Length; i++)
            {
                if (muzzleFlashRenderers[i] != null && muzzleFlashRenderers[i].enabled)
                {
                    return true;
                }
            }

            return false;
        }
    }

    private void Awake()
    {
        CacheRenderers();
        SetMuzzleFlash(false);
        SetCrashSmoke(false);
    }

    private void OnValidate()
    {
        CacheRenderers();
    }

    public void ConfigureForEditor(
        Transform configuredVisualRoot,
        Transform configuredMuzzle,
        Transform configuredMainRotorBlur,
        Transform configuredTailRotorBlur,
        Renderer[] configuredMuzzleFlashRenderers,
        ParticleSystem configuredCrashSmoke)
    {
        visualRoot = configuredVisualRoot;
        muzzle = configuredMuzzle;
        mainRotorBlur = configuredMainRotorBlur;
        tailRotorBlur = configuredTailRotorBlur;
        muzzleFlashRenderers = configuredMuzzleFlashRenderers;
        crashSmoke = configuredCrashSmoke;
        CacheRenderers();
    }

    public void SetMuzzleFlash(bool visible)
    {
        if (muzzleFlashRenderers == null)
        {
            return;
        }

        for (int i = 0; i < muzzleFlashRenderers.Length; i++)
        {
            if (muzzleFlashRenderers[i] != null)
            {
                muzzleFlashRenderers[i].enabled = visible;
            }
        }
    }

    public void SetCrashSmoke(bool active)
    {
        if (crashSmoke == null)
        {
            return;
        }

        if (active)
        {
            if (!crashSmoke.isPlaying)
            {
                crashSmoke.Play(true);
            }

            return;
        }

        crashSmoke.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
    }

    public void TickRotors(float deltaTime, float mainDegreesPerSecond, float tailDegreesPerSecond)
    {
        if (deltaTime <= 0f)
        {
            return;
        }

        if (mainRotorBlur != null)
        {
            mainRotorBlur.Rotate(0f, 0f, mainDegreesPerSecond * deltaTime, Space.Self);
        }

        if (tailRotorBlur != null)
        {
            tailRotorBlur.Rotate(0f, 0f, tailDegreesPerSecond * deltaTime, Space.Self);
        }
    }

    public void SetRenderEnabled(bool visible)
    {
        CacheRenderers();
        for (int i = 0; i < cachedRenderers.Length; i++)
        {
            if (cachedRenderers[i] != null)
            {
                cachedRenderers[i].enabled = visible;
            }
        }
    }

    private void CacheRenderers()
    {
        if (cachedRenderers == null || cachedRenderers.Length == 0)
        {
            cachedRenderers = GetComponentsInChildren<Renderer>(true);
        }
    }
}
