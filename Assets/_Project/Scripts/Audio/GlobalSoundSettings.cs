using System;
using UnityEngine;

public static class GlobalSoundSettings
{
    private const bool DefaultSoundEnabled = false;
    private static bool soundEnabled = DefaultSoundEnabled;

    public static event Action<bool> SoundEnabledChanged;

    public static bool SoundEnabled
    {
        get => soundEnabled;
        set => SetSoundEnabled(value);
    }

    public static void ToggleSound()
    {
        SoundEnabled = !SoundEnabled;
    }

    public static void ApplyCurrentState()
    {
        // Music sources opt out through GlobalMusicSource. Every other current
        // and future AudioSource follows this listener-level Sound switch.
        // Volume is used instead of pause so sounds triggered while OFF expire
        // silently instead of resuming late when Sound is turned back on.
        AudioListener.pause = false;
        AudioListener.volume = soundEnabled ? 1f : 0f;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetRuntimeState()
    {
        soundEnabled = DefaultSoundEnabled;
        SoundEnabledChanged = null;
        ApplyCurrentState();
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void ApplyDefaultStateBeforeSceneLoad()
    {
        ApplyCurrentState();
    }

    private static void SetSoundEnabled(bool enabled)
    {
        bool changed = soundEnabled != enabled;
        soundEnabled = enabled;
        ApplyCurrentState();

        if (changed)
        {
            SoundEnabledChanged?.Invoke(soundEnabled);
        }
    }
}
