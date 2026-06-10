using UnityEngine;

public static class RuntimeAudioOutputGuard
{
    private static bool audioSettingsReset;

    public static void Restore()
    {
        AudioListener.pause = false;
        AudioListener.volume = 1f;

        if (audioSettingsReset)
        {
            return;
        }

        AudioConfiguration configuration = AudioSettings.GetConfiguration();
        AudioSettings.Reset(configuration);
        audioSettingsReset = true;
    }

    public static void PrimeClip(AudioClip clip)
    {
        if (clip == null || clip.loadState != AudioDataLoadState.Unloaded)
        {
            return;
        }

        clip.LoadAudioData();
    }

    public static void ConfigureAlwaysAudible2D(AudioSource source, float volume)
    {
        if (source == null)
        {
            return;
        }

        source.volume = Mathf.Clamp01(volume);
        source.spatialBlend = 0f;
        source.ignoreListenerPause = true;
        source.ignoreListenerVolume = true;
        source.bypassEffects = true;
        source.bypassListenerEffects = true;
        source.bypassReverbZones = true;
    }
}
