using System;
using System.Collections.Generic;
using UnityEngine;

public static class GlobalMusicSettings
{
    private static readonly HashSet<AudioSource> RegisteredSources = new HashSet<AudioSource>();
    private static bool musicEnabled = true;

    public static event Action<bool> MusicEnabledChanged;

    public static bool MusicEnabled
    {
        get => musicEnabled;
        set => SetMusicEnabled(value);
    }

    public static int RegisteredSourceCount
    {
        get
        {
            RemoveInvalidSources();
            return RegisteredSources.Count;
        }
    }

    public static void ToggleMusic()
    {
        MusicEnabled = !MusicEnabled;
    }

    public static void RegisterSource(AudioSource source)
    {
        if (source == null)
        {
            return;
        }

        RemoveInvalidSources();
        RegisteredSources.Add(source);
        ApplyState(source);
    }

    public static void UnregisterSource(AudioSource source)
    {
        if (source != null)
        {
            RegisteredSources.Remove(source);
        }

        RemoveInvalidSources();
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetRuntimeState()
    {
        RegisteredSources.Clear();
        musicEnabled = true;
        MusicEnabledChanged = null;
    }

    private static void SetMusicEnabled(bool enabled)
    {
        bool changed = musicEnabled != enabled;
        musicEnabled = enabled;
        ApplyStateToAllSources();

        if (changed)
        {
            MusicEnabledChanged?.Invoke(musicEnabled);
        }
    }

    private static void ApplyStateToAllSources()
    {
        RemoveInvalidSources();
        foreach (AudioSource source in RegisteredSources)
        {
            ApplyState(source);
        }
    }

    private static void ApplyState(AudioSource source)
    {
        if (source == null)
        {
            return;
        }

        source.mute = !musicEnabled;
        if (musicEnabled && Application.isPlaying && source.isActiveAndEnabled && source.clip != null && !source.isPlaying)
        {
            source.Play();
        }
    }

    private static void RemoveInvalidSources()
    {
        RegisteredSources.RemoveWhere(source => source == null);
    }
}
