using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(AudioSource))]
public sealed class GlobalMusicSource : MonoBehaviour
{
    [SerializeField] private AudioSource musicSource;

    public AudioSource MusicSource => ResolveMusicSource();

    public static GlobalMusicSource Ensure(AudioSource source)
    {
        if (source == null)
        {
            return null;
        }

        GlobalMusicSource binding = source.GetComponent<GlobalMusicSource>();
        if (binding == null)
        {
            binding = source.gameObject.AddComponent<GlobalMusicSource>();
        }

        binding.musicSource = source;
        if (binding.isActiveAndEnabled)
        {
            binding.RegisterCurrentSource();
        }

        return binding;
    }

    private void Reset()
    {
        musicSource = GetComponent<AudioSource>();
    }

    private void Awake()
    {
        ResolveMusicSource();
    }

    private void OnEnable()
    {
        RegisterCurrentSource();
    }

    private void OnDisable()
    {
        GlobalMusicSettings.UnregisterSource(musicSource);
    }

    private void OnDestroy()
    {
        GlobalMusicSettings.UnregisterSource(musicSource);
    }

    private AudioSource ResolveMusicSource()
    {
        if (musicSource == null)
        {
            musicSource = GetComponent<AudioSource>();
        }

        return musicSource;
    }

    private void RegisterCurrentSource()
    {
        GlobalMusicSettings.RegisterSource(ResolveMusicSource());
    }
}
