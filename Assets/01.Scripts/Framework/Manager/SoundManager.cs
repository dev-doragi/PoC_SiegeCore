using UnityEngine;

[DefaultExecutionOrder(-750)]
public class SoundManager : Singleton<SoundManager>
{
    [SerializeField] private AudioSource _bgmSource;
    [SerializeField] private AudioSource _sfxSource;
    [SerializeField] private bool _keepBgmAcrossScenes = true;

    protected override void OnInitialize()
    {
        EnsureAudioSources();
    }

    protected override void OnStartService()
    {
        EventBus.Instance.Subscribe<SceneLoadedEvent>(OnSceneLoaded);
        EventBus.Instance.Subscribe<PlaySoundEvent>(OnPlaySound);
        EventBus.Instance.Subscribe<StopSoundEvent>(OnStopSound);
    }

    private void OnSceneLoaded(SceneLoadedEvent evt)
    {
        if (_keepBgmAcrossScenes)
        {
            return;
        }
        _bgmSource.Stop();
        _bgmSource.clip = null;
    }

    protected override void OnStopService()
    {
        EventBus.Instance.Unsubscribe<SceneLoadedEvent>(OnSceneLoaded);
        EventBus.Instance.Unsubscribe<PlaySoundEvent>(OnPlaySound);
        EventBus.Instance.Unsubscribe<StopSoundEvent>(OnStopSound);
    }

    private void EnsureAudioSources()
    {
        if (_bgmSource == null)
        {
            _bgmSource = gameObject.AddComponent<AudioSource>();
            _bgmSource.loop = true;
        }

        if (_sfxSource == null)
        {
            _sfxSource = gameObject.AddComponent<AudioSource>();
            _sfxSource.loop = false;
        }

        if (!_keepBgmAcrossScenes)
        {
            _bgmSource.Stop();
            _bgmSource.clip = null;
        }
    }

    private void OnPlaySound(PlaySoundEvent evt)
    {
        if (evt.Clip == null)
        {
            Debug.LogError("[SoundManager] PlaySoundEvent clip is null.", this);
            return;
        }

        if (evt.IsBgm)
        {
            _bgmSource.clip = evt.Clip;
            _bgmSource.volume = Mathf.Clamp01(evt.Volume);
            _bgmSource.Play();
            return;
        }

        _sfxSource.PlayOneShot(evt.Clip, Mathf.Clamp01(evt.Volume));
    }

    private void OnStopSound(StopSoundEvent evt)
    {
        if (evt.StopBgm)
        {
            _bgmSource.Stop();
            return;
        }

        _sfxSource.Stop();
    }
}
