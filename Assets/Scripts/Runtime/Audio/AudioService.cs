using System;
using RestaurantLoop.Runtime.Data;
using RestaurantLoop.Signals;
using UnityEngine;
using UnityEngine.SceneManagement;
using Zenject;

namespace RestaurantLoop.Audio
{
    public class AudioService : IAudioService, IInitializable
    {
        private const string PrefsMusicKey = "Audio_MusicVolume";
        private const string PrefsSfxKey = "Audio_SfxVolume";

        private readonly SignalBus _signalBus;
        private readonly AudioLibrary _settings;

        private readonly AudioSource _musicSource;
        private readonly AudioSource _ambienceSource;
        private readonly AudioSource _sfxSource;
        private readonly AudioSource _conveyorSource;

        public float MusicVolume => _settings != null ? _settings.MusicVolume : 0.5f;
        public float SfxVolume => _settings != null ? _settings.SfxVolume : 0.5f;

        public event Action<float, float> OnAudioSettingsChanged;

        public AudioService(SignalBus signalBus, AudioLibrary settings)
        {
            _signalBus = signalBus;
            _settings = settings;

            // PlayerPrefs'ten önceden kaydedilmiş değerleri yükle (ilk açılışta AudioLibrary'deki varsayılanı kullanır)
            if (PlayerPrefs.HasKey(PrefsMusicKey))
            {
                _settings.MusicVolume = PlayerPrefs.GetFloat(PrefsMusicKey);
            }
            if (PlayerPrefs.HasKey(PrefsSfxKey))
            {
                _settings.SfxVolume = PlayerPrefs.GetFloat(PrefsSfxKey);
            }

            var root = new GameObject("AudioService");
            UnityEngine.Object.DontDestroyOnLoad(root);

            _musicSource = CreateSource(root.transform, "Music", loop: true, volume: _settings.MusicVolume);
            _ambienceSource = CreateSource(root.transform, "Ambience", loop: true, volume: _settings.AmbienceVolume);
            _sfxSource = CreateSource(root.transform, "Sfx", loop: false, volume: 1f);
            _conveyorSource = CreateSource(root.transform, "Conveyor", loop: true, volume: _settings.ConveyorVolume > 0f ? _settings.ConveyorVolume : _settings.SfxVolume * 2f);
        }

        public void SetMusicVolume(float volume)
        {
            volume = Mathf.Clamp(volume, 0f, 0.2f); // Max 0.2
            if (_settings != null)
            {
                _settings.MusicVolume = volume;
                _settings.AmbienceVolume = volume * 0.5f; // Ambiyans da müzik seviyesine orantılı ayarlanır
            }
            if (_musicSource != null)
            {
                _musicSource.volume = volume;
            }
            if (_ambienceSource != null)
            {
                _ambienceSource.volume = volume * 0.5f;
            }
            PlayerPrefs.SetFloat(PrefsMusicKey, volume);
            PlayerPrefs.Save();

            OnAudioSettingsChanged?.Invoke(MusicVolume, SfxVolume);
        }

        public void SetSfxVolume(float volume)
        {
            volume = Mathf.Clamp(volume, 0f, 0.2f); // Max 0.2
            if (_settings != null)
            {
                _settings.SfxVolume = volume;
                _settings.ConveyorVolume = volume;
            }
            if (_conveyorSource != null)
            {
                _conveyorSource.volume = volume;
            }
            PlayerPrefs.SetFloat(PrefsSfxKey, volume);
            PlayerPrefs.Save();

            OnAudioSettingsChanged?.Invoke(MusicVolume, SfxVolume);
        }

        private static AudioSource CreateSource(Transform parent, string name, bool loop, float volume)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);

            var source = go.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.loop = loop;
            source.spatialBlend = 0f;
            source.volume = volume;
            return source;
        }

        public void Initialize()
        {
            _signalBus.Subscribe<MainMenuReadySignal>(OnMenuReady);
            _signalBus.Subscribe<MenuReadySignal>(OnMenuReady);
            _signalBus.Subscribe<GameplaySceneReadySignal>(OnGameplayReady);
            _signalBus.Subscribe<LevelEndedSignal>(OnLevelEnded);

            // Normal boot goes MainMenuReadySignal -> GameplaySceneReadySignal via Bootstrapper/SceneLoader.
            // Hitting Play with GameplayScene or MainMenuScene already open (editor testing) skips that
            // chain entirely, so bootstrap straight off whatever's already active instead of staying silent.
            var activeSceneName = SceneManager.GetActiveScene().name;
            if (activeSceneName == "GameplayScene" || activeSceneName == "EylulGameplayScene") OnGameplayReady();
            else if (activeSceneName == "MainMenuScene" || activeSceneName == "EylulMainMenuScene") OnMenuReady();
        }

        // Cold boot fires this too, but GameplaySceneReadySignal follows right behind it
        // (SceneLoader continues straight into gameplay) and overrides the track before it's heard.
        private void OnMenuReady()
        {
            StopConveyorLoop();
            PlayMusic(_settings.MenuMusic);
        }

        private void OnGameplayReady()
        {
            PlayMusic(_settings.GameplayMusic);
            PlayAmbience(_settings.GameplayAmbience);
        }

        // Stop gameplay ambience and conveyor loop the moment a level ends, before the async scene unload even starts —
        // MenuReadySignal (fired once the unload completes) picks the music back up.
        private void OnLevelEnded(LevelEndedSignal _)
        {
            StopAmbience();
            StopConveyorLoop();
        }

        public void PlaySfx(AudioClip clip, float volumeScale = 1f)
        {
            if (clip == null) return;
            _sfxSource.PlayOneShot(clip, _settings.SfxVolume * volumeScale);
        }

        public void PlayConveyorLoop(AudioClip clip = null)
        {
            var targetClip = clip != null ? clip : _settings.ConveyorLoopSfx;
            if (targetClip == null) return;

            if (_conveyorSource.clip == targetClip && _conveyorSource.isPlaying) return;

            _conveyorSource.Stop();
            _conveyorSource.clip = targetClip;
            _conveyorSource.volume = _settings.ConveyorVolume > 0f ? _settings.ConveyorVolume : Mathf.Clamp01(_settings.SfxVolume * 2f);
            _conveyorSource.Play();
        }

        public void StopConveyorLoop()
        {
            if (_conveyorSource != null && _conveyorSource.isPlaying)
            {
                _conveyorSource.Stop();
            }
        }

        public void PlayMusic(AudioClip clip)
        {
            if (clip == null)
            {
                StopMusic();
                return;
            }

            if (_musicSource.clip == clip && _musicSource.isPlaying) return;

            _musicSource.Stop();
            _musicSource.clip = clip;
            _musicSource.Play();
        }

        public void StopMusic() => _musicSource.Stop();

        public void PauseMusic() => _musicSource.Pause();

        public void ResumeMusic()
        {
            if (!_musicSource.isPlaying && _musicSource.clip != null)
            {
                _musicSource.UnPause();
            }
        }

        public void PlayAmbience(AudioClip clip)
        {
            if (clip == null)
            {
                StopAmbience();
                return;
            }

            if (_ambienceSource.clip == clip && _ambienceSource.isPlaying) return;

            _ambienceSource.Stop();
            _ambienceSource.clip = clip;
            _ambienceSource.Play();
        }

        public void StopAmbience() => _ambienceSource.Stop();

        public void PauseAmbience() => _ambienceSource.Pause();

        public void ResumeAmbience()
        {
            if (!_ambienceSource.isPlaying && _ambienceSource.clip != null)
            {
                _ambienceSource.UnPause();
            }
        }
    }
}
