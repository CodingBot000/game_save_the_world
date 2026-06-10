using System;
using System.Reflection;
using UnityEditor;
using UnityEngine;

public static class EditorAudioDiagnostics
{
    private const string MenuPath = "TitanDestroyer/Audio/Unmute Editor Audio";

    [MenuItem(MenuPath)]
    public static void UnmuteEditorAudio()
    {
        LogEditorAudioState("before");
        int changedCount = 0;
        changedCount += InvokeStaticBoolSetters("SetMutePlayers", false);
        changedCount += SetStaticBoolProperties("MutePlayers", false);
        changedCount += SetStaticBoolProperties("IsMutePlayers", false);
        changedCount += SetStaticBoolProperties("audioMasterMute", false);
        changedCount += SetStaticBoolProperties("Internal_AudioMasterMute", false);
        changedCount += InvokeAudioManagerBoolMethod("SetMasterGroupMute", false);

        AudioListener.pause = false;
        AudioListener.volume = 1f;

        Debug.Log($"Editor audio unmute requested. Applied {changedCount} editor mute setter(s).");
        LogEditorAudioState("after");
    }

    [MenuItem("TitanDestroyer/Audio/Log Editor Audio State")]
    public static void LogEditorAudioStateMenu()
    {
        LogEditorAudioState("manual");
    }

    private static void LogEditorAudioState(string label)
    {
        string state = $"Editor audio state ({label}): " +
            $"AudioListener.pause={AudioListener.pause}, " +
            $"AudioListener.volume={AudioListener.volume}, " +
            $"AudioSettings.dspTime={AudioSettings.dspTime:0.000}, " +
            $"AudioSettings.driverCapabilities={AudioSettings.driverCapabilities}, " +
            $"AudioSettings.speakerMode={AudioSettings.speakerMode}, " +
            $"AudioSettings.outputSampleRate={AudioSettings.outputSampleRate}, " +
            $"GetMutePlayers={InvokeStaticBoolGetter("GetMutePlayers")}, " +
            $"MutePlayers={GetStaticBoolProperty("MutePlayers")}, " +
            $"IsMutePlayers={GetStaticBoolProperty("IsMutePlayers")}, " +
            $"audioMasterMute={GetStaticBoolProperty("audioMasterMute")}, " +
            $"Internal_AudioMasterMute={GetStaticBoolProperty("Internal_AudioMasterMute")}, " +
            $"AudioManager.GetMasterGroupMute={InvokeAudioManagerBoolGetter("GetMasterGroupMute")}";

        Debug.Log(state);
    }

    private static int InvokeStaticBoolSetters(string methodName, bool value)
    {
        int changedCount = 0;
        Assembly unityEditorAssembly = typeof(Editor).Assembly;
        foreach (Type type in unityEditorAssembly.GetTypes())
        {
            MethodInfo[] methods = type.GetMethods(BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
            foreach (MethodInfo method in methods)
            {
                if (!string.Equals(method.Name, methodName, StringComparison.Ordinal))
                {
                    continue;
                }

                ParameterInfo[] parameters = method.GetParameters();
                if (parameters.Length != 1 || parameters[0].ParameterType != typeof(bool))
                {
                    continue;
                }

                try
                {
                    method.Invoke(null, new object[] { value });
                    changedCount++;
                }
                catch (Exception exception)
                {
                    Debug.LogWarning($"Could not invoke {type.FullName}.{method.Name}: {exception.Message}");
                }
            }
        }

        return changedCount;
    }

    private static string InvokeStaticBoolGetter(string methodName)
    {
        Assembly unityEditorAssembly = typeof(Editor).Assembly;
        foreach (Type type in unityEditorAssembly.GetTypes())
        {
            MethodInfo method = type.GetMethod(methodName, BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
            if (method == null || method.ReturnType != typeof(bool) || method.GetParameters().Length != 0)
            {
                continue;
            }

            try
            {
                return $"{type.FullName}.{method.Name}={method.Invoke(null, null)}";
            }
            catch (Exception exception)
            {
                return $"{type.FullName}.{method.Name}=error:{exception.Message}";
            }
        }

        return "not-found";
    }

    private static int SetStaticBoolProperties(string propertyName, bool value)
    {
        int changedCount = 0;
        Assembly unityEditorAssembly = typeof(Editor).Assembly;
        foreach (Type type in unityEditorAssembly.GetTypes())
        {
            PropertyInfo property = type.GetProperty(
                propertyName,
                BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);

            if (property == null || property.PropertyType != typeof(bool) || property.SetMethod == null)
            {
                continue;
            }

            try
            {
                property.SetValue(null, value);
                changedCount++;
            }
            catch (Exception exception)
            {
                Debug.LogWarning($"Could not set {type.FullName}.{property.Name}: {exception.Message}");
            }
        }

        return changedCount;
    }

    private static string GetStaticBoolProperty(string propertyName)
    {
        Assembly unityEditorAssembly = typeof(Editor).Assembly;
        foreach (Type type in unityEditorAssembly.GetTypes())
        {
            PropertyInfo property = type.GetProperty(
                propertyName,
                BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);

            if (property == null || property.PropertyType != typeof(bool) || property.GetMethod == null)
            {
                continue;
            }

            try
            {
                return $"{type.FullName}.{property.Name}={property.GetValue(null)}";
            }
            catch (Exception exception)
            {
                return $"{type.FullName}.{property.Name}=error:{exception.Message}";
            }
        }

        return "not-found";
    }

    private static object GetAudioManager()
    {
        Assembly unityEditorAssembly = typeof(Editor).Assembly;
        Type audioUtilType = unityEditorAssembly.GetType("UnityEditor.AudioUtil");
        MethodInfo method = audioUtilType?.GetMethod("GetAudioManager", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
        return method?.Invoke(null, null);
    }

    private static int InvokeAudioManagerBoolMethod(string methodName, bool value)
    {
        object audioManager = GetAudioManager();
        if (audioManager == null)
        {
            return 0;
        }

        MethodInfo method = audioManager.GetType().GetMethod(methodName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        if (method == null)
        {
            return 0;
        }

        ParameterInfo[] parameters = method.GetParameters();
        if (parameters.Length != 1 || parameters[0].ParameterType != typeof(bool))
        {
            return 0;
        }

        method.Invoke(audioManager, new object[] { value });
        return 1;
    }

    private static string InvokeAudioManagerBoolGetter(string methodName)
    {
        object audioManager = GetAudioManager();
        if (audioManager == null)
        {
            return "not-found";
        }

        MethodInfo method = audioManager.GetType().GetMethod(methodName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        if (method == null || method.ReturnType != typeof(bool) || method.GetParameters().Length != 0)
        {
            return "not-found";
        }

        try
        {
            return $"{audioManager.GetType().FullName}.{method.Name}={method.Invoke(audioManager, null)}";
        }
        catch (Exception exception)
        {
            return $"{audioManager.GetType().FullName}.{method.Name}=error:{exception.Message}";
        }
    }
}
