using UnityEngine;

public static class RuntimeAudioOutputGuard
{
    private static bool audioSettingsReset;

    public static void Restore()
    {
        if (!audioSettingsReset)
        {
            AudioConfiguration configuration = AudioSettings.GetConfiguration();
            AudioSettings.Reset(configuration);
            audioSettingsReset = true;
        }

        GlobalSoundSettings.ApplyCurrentState();
    }

    public static void PrimeClip(AudioClip clip)
    {
        if (clip == null || clip.loadState != AudioDataLoadState.Unloaded)
        {
            return;
        }

        clip.LoadAudioData();
    }

    public static void ConfigureMusic2D(AudioSource source, float volume)
    {
        Configure2D(source, volume, true);
    }

    public static void ConfigureSoundEffect2D(AudioSource source, float volume)
    {
        Configure2D(source, volume, false);
    }

    private static void Configure2D(AudioSource source, float volume, bool ignoreListenerControls)
    {
        if (source == null)
        {
            return;
        }

        source.volume = Mathf.Clamp01(volume);
        source.spatialBlend = 0f;
        source.ignoreListenerPause = ignoreListenerControls;
        source.ignoreListenerVolume = ignoreListenerControls;
        source.bypassEffects = true;
        source.bypassListenerEffects = true;
        source.bypassReverbZones = true;
    }
}
