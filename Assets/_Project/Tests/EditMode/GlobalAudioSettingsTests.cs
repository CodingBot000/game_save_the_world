using System;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

public class GlobalAudioSettingsTests
{
    private const BindingFlags StaticFlags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static;

    private Type soundSettingsType;
    private PropertyInfo soundEnabledProperty;
    private MethodInfo resetSoundStateMethod;
    private GameObject testObject;

    [SetUp]
    public void SetUp()
    {
        soundSettingsType = Type.GetType("GlobalSoundSettings, Assembly-CSharp");
        Assert.That(soundSettingsType, Is.Not.Null);

        soundEnabledProperty = soundSettingsType.GetProperty("SoundEnabled", StaticFlags);
        resetSoundStateMethod = soundSettingsType.GetMethod("ResetRuntimeState", StaticFlags);
        Assert.That(soundEnabledProperty, Is.Not.Null);
        Assert.That(resetSoundStateMethod, Is.Not.Null);

        resetSoundStateMethod.Invoke(null, null);
    }

    [TearDown]
    public void TearDown()
    {
        if (testObject != null)
        {
            UnityEngine.Object.DestroyImmediate(testObject);
        }

        resetSoundStateMethod?.Invoke(null, null);
    }

    [Test]
    public void RuntimeReset_DefaultsSoundToOff()
    {
        soundEnabledProperty.SetValue(null, true);
        resetSoundStateMethod.Invoke(null, null);

        Assert.That(soundEnabledProperty.GetValue(null), Is.False);
        Assert.That(AudioListener.pause, Is.False);
        Assert.That(AudioListener.volume, Is.Zero);
    }

    [Test]
    public void SoundSwitch_ControlsGlobalListenerVolume()
    {
        soundEnabledProperty.SetValue(null, true);
        Assert.That(AudioListener.pause, Is.False);
        Assert.That(AudioListener.volume, Is.EqualTo(1f));

        soundEnabledProperty.SetValue(null, false);
        Assert.That(AudioListener.pause, Is.False);
        Assert.That(AudioListener.volume, Is.Zero);
    }

    [Test]
    public void MusicAndSoundEffectSources_UseSeparateListenerRules()
    {
        Type outputGuardType = Type.GetType("RuntimeAudioOutputGuard, Assembly-CSharp");
        Type musicSourceType = Type.GetType("GlobalMusicSource, Assembly-CSharp");
        Assert.That(outputGuardType, Is.Not.Null);
        Assert.That(musicSourceType, Is.Not.Null);

        MethodInfo configureSoundEffect = outputGuardType.GetMethod("ConfigureSoundEffect2D", StaticFlags);
        MethodInfo ensureMusicSource = musicSourceType.GetMethod("Ensure", StaticFlags);
        Assert.That(configureSoundEffect, Is.Not.Null);
        Assert.That(ensureMusicSource, Is.Not.Null);

        testObject = new GameObject("GlobalAudioSettingsTestsSource");
        AudioSource source = testObject.AddComponent<AudioSource>();

        configureSoundEffect.Invoke(null, new object[] { source, 0.5f });
        Assert.That(source.ignoreListenerPause, Is.False);
        Assert.That(source.ignoreListenerVolume, Is.False);

        ensureMusicSource.Invoke(null, new object[] { source });
        Assert.That(source.ignoreListenerPause, Is.True);
        Assert.That(source.ignoreListenerVolume, Is.True);
    }
}
