using System;
using RestaurantLoop.Runtime.Data;
using RestaurantLoop.Runtime.Save;
using UnityEngine;

namespace RestaurantLoop.Runtime.Audio
{
    // Owns the audio sources and volumes; what plays when is decided by MusicDirector.
    public class AudioService
    {
        private const float AmbienceToMusicRatio = 0.4f;
        private const float ConveyorToSfxRatio = 0.5f;

        private readonly AudioLibrary _settings;
        private readonly SaveService _saveService;

        private readonly AudioSource _musicSource;
        private readonly AudioSource _ambienceSource;
        private readonly AudioSource _sfxSource;
        private readonly AudioSource _conveyorSource;

        public float MusicVolume { get; private set; }
        public float SfxVolume { get; private set; }

        public event Action<float, float> OnAudioSettingsChanged;

        public AudioService(AudioLibrary settings, SaveService saveService)
        {
            _settings = settings;
            _saveService = saveService;

            MusicVolume = _saveService.MusicVolume;
            SfxVolume = _saveService.SfxVolume;

            var root = new GameObject("AudioService");
            UnityEngine.Object.DontDestroyOnLoad(root);

            _musicSource = CreateSource(root.transform, "Music", loop: true, volume: MusicVolume);
            _ambienceSource = CreateSource(root.transform, "Ambience", loop: true, volume: MusicVolume * AmbienceToMusicRatio);
            _sfxSource = CreateSource(root.transform, "Sfx", loop: false, volume: 1f);
            _conveyorSource = CreateSource(root.transform, "Conveyor", loop: true, volume: SfxVolume * ConveyorToSfxRatio);
        }

        public void SetMusicVolume(float volume)
        {
            MusicVolume = Mathf.Clamp01(volume);
            _musicSource.volume = MusicVolume;
            _ambienceSource.volume = MusicVolume * AmbienceToMusicRatio;
            _saveService.MusicVolume = MusicVolume;

            OnAudioSettingsChanged?.Invoke(MusicVolume, SfxVolume);
        }

        public void SetSfxVolume(float volume)
        {
            SfxVolume = Mathf.Clamp01(volume);
            _conveyorSource.volume = SfxVolume * ConveyorToSfxRatio;
            _saveService.SfxVolume = SfxVolume;

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

        public void PlaySfx(AudioClip clip, float volumeScale = 1f)
        {
            if (clip == null) return;
            _sfxSource.PlayOneShot(clip, SfxVolume * volumeScale);
        }

        public void PlayConveyorLoop(AudioClip clip = null)
        {
            var targetClip = clip != null ? clip : _settings.ConveyorLoopSfx;
            if (targetClip == null) return;

            if (_conveyorSource.clip == targetClip && _conveyorSource.isPlaying) return;

            _conveyorSource.Stop();
            _conveyorSource.clip = targetClip;
            _conveyorSource.volume = SfxVolume * ConveyorToSfxRatio;
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
