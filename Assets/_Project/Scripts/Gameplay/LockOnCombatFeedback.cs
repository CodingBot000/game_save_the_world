using System.Collections;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class LockOnCombatFeedback : MonoBehaviour
{
    private const int SampleRate = 24000;
    private static readonly float[] StageFrequencies = { 480f, 610f, 760f, 930f, 1120f };
    private static readonly float[] ReleaseShakeDurations = { 0.07f, 0.09f, 0.11f, 0.14f, 0.20f };
    private static readonly float[] ReleaseShakeAmplitudes = { 0.0015f, 0.0022f, 0.0032f, 0.0048f, 0.009f };

    [SerializeField, Range(0f, 1f)] private float stageSfxVolume = 0.52f;
    [SerializeField, Range(0f, 1f)] private float releaseSfxVolume = 0.68f;
    [SerializeField, Range(0f, 1f)] private float boostSfxVolume = 0.48f;
    [SerializeField] private bool enableProjectionShake = true;

    private readonly AudioClip[] stageClips = new AudioClip[5];
    private PlayerLockOnController lockOnController;
    private Camera battleCamera;
    private AudioSource stageAudioSource;
    private AudioSource releaseAudioSource;
    private AudioSource boostAudioSource;
    private AudioClip releaseClip;
    private AudioClip boostClip;
    private Coroutine shakeRoutine;
    private Camera shakenCamera;
    private Matrix4x4 shakeBaseProjection;
    private bool configured;

    public int StageSfxPlayCount { get; private set; }
    public int ReleaseSfxPlayCount { get; private set; }
    public int BoostSfxPlayCount { get; private set; }
    public int FullSalvoFeedbackCount { get; private set; }
    public int LastFeedbackStage { get; private set; }
    public bool IsProjectionShakeActive => shakeRoutine != null;
    public float LastShakeAmplitude { get; private set; }
    public float PeakShakeProjectionOffset { get; private set; }
    public bool ProjectionRestoredAfterShake { get; private set; } = true;
    public bool HasGeneratedFeedbackAudio =>
        stageClips[0] != null && stageClips[4] != null &&
        releaseClip != null && boostClip != null;
    public bool IsFeedbackAudioPlaying =>
        (stageAudioSource != null && stageAudioSource.isPlaying) ||
        (releaseAudioSource != null && releaseAudioSource.isPlaying) ||
        (boostAudioSource != null && boostAudioSource.isPlaying);

    public void Configure(PlayerLockOnController controller, Camera targetCamera)
    {
        Unsubscribe();
        lockOnController = controller;
        battleCamera = targetCamera != null ? targetCamera : Camera.main;
        EnsureAudioOutput();
        configured = lockOnController != null;
        Subscribe();
    }

    private void HandleStageUp(int successfulLockCount)
    {
        int stage = Mathf.Clamp(successfulLockCount, 1, stageClips.Length);
        AudioClip clip = stageClips[stage - 1];
        if (stageAudioSource != null && clip != null)
        {
            stageAudioSource.pitch = 1f;
            stageAudioSource.PlayOneShot(clip, stageSfxVolume);
        }

        StageSfxPlayCount++;
        LastFeedbackStage = stage;
    }

    private void HandleLockRelease(LockOnReleaseIntent intent)
    {
        int stage = Mathf.Clamp(intent?.SuccessfulLockCount ?? 1, 1, stageClips.Length);
        LastFeedbackStage = stage;
        if (releaseAudioSource != null && releaseClip != null)
        {
            releaseAudioSource.PlayOneShot(releaseClip, releaseSfxVolume);
        }

        if (boostAudioSource != null && boostClip != null)
        {
            boostAudioSource.PlayOneShot(boostClip, boostSfxVolume);
        }

        ReleaseSfxPlayCount++;
        BoostSfxPlayCount++;
        TriggerProjectionShake(stage);
    }

    private void HandleFullSalvo()
    {
        FullSalvoFeedbackCount++;
    }

    private void TriggerProjectionShake(int stage)
    {
        StopProjectionShake(restoreProjection: true);
        int index = Mathf.Clamp(stage - 1, 0, ReleaseShakeAmplitudes.Length - 1);
        LastShakeAmplitude = ReleaseShakeAmplitudes[index];
        PeakShakeProjectionOffset = 0f;
        ProjectionRestoredAfterShake = false;
        if (!enableProjectionShake || battleCamera == null || LastShakeAmplitude <= 0f)
        {
            ProjectionRestoredAfterShake = true;
            return;
        }

        shakenCamera = battleCamera;
        shakeBaseProjection = shakenCamera.projectionMatrix;
        shakeRoutine = StartCoroutine(ShakeProjectionRoutine(
            ReleaseShakeDurations[index],
            LastShakeAmplitude,
            stage * 19.37f));
    }

    private IEnumerator ShakeProjectionRoutine(float duration, float amplitude, float seed)
    {
        float elapsed = 0f;
        while (elapsed < duration && shakenCamera != null)
        {
            float progress = Mathf.Clamp01(elapsed / Mathf.Max(0.01f, duration));
            float envelope = 1f - progress;
            float sampleTime = Time.unscaledTime * 47f;
            float offsetX = (Mathf.PerlinNoise(seed, sampleTime) * 2f - 1f) * amplitude * envelope;
            float offsetY = (Mathf.PerlinNoise(seed + 11.9f, sampleTime) * 2f - 1f) * amplitude * envelope;
            Matrix4x4 projection = shakeBaseProjection;
            projection.m02 += offsetX;
            projection.m12 += offsetY;
            shakenCamera.projectionMatrix = projection;
            PeakShakeProjectionOffset = Mathf.Max(
                PeakShakeProjectionOffset,
                Mathf.Max(Mathf.Abs(offsetX), Mathf.Abs(offsetY)));
            elapsed += Mathf.Min(Time.unscaledDeltaTime, 1f / 30f);
            yield return null;
        }

        RestoreProjection();
        shakeRoutine = null;
    }

    private void StopProjectionShake(bool restoreProjection)
    {
        if (shakeRoutine != null)
        {
            StopCoroutine(shakeRoutine);
            shakeRoutine = null;
        }

        if (restoreProjection)
        {
            RestoreProjection();
        }
    }

    private void RestoreProjection()
    {
        if (shakenCamera != null)
        {
            shakenCamera.projectionMatrix = shakeBaseProjection;
        }

        shakenCamera = null;
        ProjectionRestoredAfterShake = true;
    }

    private void EnsureAudioOutput()
    {
        RuntimeAudioOutputGuard.Restore();
        stageAudioSource ??= CreateAudioSource("LockOnStageSfx");
        releaseAudioSource ??= CreateAudioSource("LockOnReleaseSfx");
        boostAudioSource ??= CreateAudioSource("LockOnBoostSfx");

        for (int i = 0; i < stageClips.Length; i++)
        {
            if (stageClips[i] == null)
            {
                stageClips[i] = CreateStageClip(i + 1, StageFrequencies[i]);
            }
        }

        releaseClip ??= CreateReleaseClip();
        boostClip ??= CreateBoostClip();
    }

    private AudioSource CreateAudioSource(string sourceName)
    {
        GameObject sourceObject = new(sourceName);
        sourceObject.transform.SetParent(transform, false);
        AudioSource source = sourceObject.AddComponent<AudioSource>();
        source.playOnAwake = false;
        source.loop = false;
        RuntimeAudioOutputGuard.ConfigureAlwaysAudible2D(source, 1f);
        return source;
    }

    private static AudioClip CreateStageClip(int stage, float frequency)
    {
        float duration = stage == 5 ? 0.20f : 0.10f;
        int sampleCount = Mathf.CeilToInt(SampleRate * duration);
        float[] samples = new float[sampleCount];
        for (int i = 0; i < sampleCount; i++)
        {
            float time = i / (float)SampleRate;
            float progress = i / (float)Mathf.Max(1, sampleCount - 1);
            float envelope = Mathf.Sin(Mathf.PI * progress);
            float primary = Mathf.Sin(Mathf.PI * 2f * frequency * time);
            float overtone = Mathf.Sin(Mathf.PI * 2f * frequency * 2f * time) * 0.24f;
            float completionChord = stage == 5
                ? Mathf.Sin(Mathf.PI * 2f * frequency * 1.5f * time) * 0.40f
                : 0f;
            samples[i] = (primary + overtone + completionChord) * envelope * 0.28f;
        }

        return CreateClip($"LockOnStage{stage}_Runtime", samples);
    }

    private static AudioClip CreateReleaseClip()
    {
        const float duration = 0.18f;
        int sampleCount = Mathf.CeilToInt(SampleRate * duration);
        float[] samples = new float[sampleCount];
        for (int i = 0; i < sampleCount; i++)
        {
            float time = i / (float)SampleRate;
            float progress = i / (float)Mathf.Max(1, sampleCount - 1);
            float envelope = Mathf.Pow(1f - progress, 2.2f);
            float frequency = Mathf.Lerp(180f, 72f, progress);
            float body = Mathf.Sin(Mathf.PI * 2f * frequency * time);
            float grit = DeterministicNoise(i) * 0.24f;
            samples[i] = (body + grit) * envelope * 0.38f;
        }

        return CreateClip("LockOnRelease_Runtime", samples);
    }

    private static AudioClip CreateBoostClip()
    {
        const float duration = 0.30f;
        int sampleCount = Mathf.CeilToInt(SampleRate * duration);
        float[] samples = new float[sampleCount];
        for (int i = 0; i < sampleCount; i++)
        {
            float time = i / (float)SampleRate;
            float progress = i / (float)Mathf.Max(1, sampleCount - 1);
            float attack = Mathf.Clamp01(progress / 0.08f);
            float release = Mathf.Pow(1f - progress, 1.4f);
            float frequency = Mathf.Lerp(260f, 920f, progress);
            float sweep = Mathf.Sin(Mathf.PI * 2f * frequency * time) * 0.45f;
            float air = DeterministicNoise(i + 7919) * 0.55f;
            samples[i] = (sweep + air) * attack * release * 0.24f;
        }

        return CreateClip("LockOnBoost_Runtime", samples);
    }

    private static float DeterministicNoise(int index)
    {
        float value = Mathf.Sin(index * 12.9898f + 78.233f) * 43758.5453f;
        return (value - Mathf.Floor(value)) * 2f - 1f;
    }

    private static AudioClip CreateClip(string clipName, float[] samples)
    {
        AudioClip clip = AudioClip.Create(clipName, samples.Length, 1, SampleRate, false);
        clip.hideFlags = HideFlags.DontSave;
        clip.SetData(samples, 0);
        return clip;
    }

    private void Subscribe()
    {
        if (!configured || lockOnController == null || !isActiveAndEnabled)
        {
            return;
        }

        lockOnController.OnLockStageUp -= HandleStageUp;
        lockOnController.OnLockRelease -= HandleLockRelease;
        lockOnController.OnFullSalvo -= HandleFullSalvo;
        lockOnController.OnLockStageUp += HandleStageUp;
        lockOnController.OnLockRelease += HandleLockRelease;
        lockOnController.OnFullSalvo += HandleFullSalvo;
    }

    private void Unsubscribe()
    {
        if (lockOnController == null)
        {
            return;
        }

        lockOnController.OnLockStageUp -= HandleStageUp;
        lockOnController.OnLockRelease -= HandleLockRelease;
        lockOnController.OnFullSalvo -= HandleFullSalvo;
    }

    private void OnEnable()
    {
        Subscribe();
    }

    private void OnDisable()
    {
        Unsubscribe();
        StopProjectionShake(restoreProjection: true);
    }

    private void OnDestroy()
    {
        Unsubscribe();
        StopProjectionShake(restoreProjection: true);
        for (int i = 0; i < stageClips.Length; i++)
        {
            DestroyGeneratedClip(stageClips[i]);
            stageClips[i] = null;
        }

        DestroyGeneratedClip(releaseClip);
        DestroyGeneratedClip(boostClip);
        releaseClip = null;
        boostClip = null;
    }

    private static void DestroyGeneratedClip(AudioClip clip)
    {
        if (clip == null)
        {
            return;
        }

        if (Application.isPlaying)
        {
            Destroy(clip);
        }
        else
        {
            DestroyImmediate(clip);
        }
    }
}
