using RestaurantLoop.Runtime.Data;
using RestaurantLoop.Signals;
using UnityEngine;
using UnityEngine.SceneManagement;
using Zenject;

namespace RestaurantLoop.Audio
{
    // ProjectContext-scoped singleton — outlives every scene load/unload, so it's the one place
    // that knows what's currently playing. MainMenuScene stays loaded (additive) underneath
    // GameplayScene for the whole play session, so per-scene AudioSources would double up; routing
    // every play call through here means a new track always stops the previous one first.
    public class AudioService : IAudioService, IInitializable
    {
        private readonly SignalBus _signalBus;
        private readonly AudioLibrary _settings;

        private readonly AudioSource _musicSource;
        private readonly AudioSource _ambienceSource;
        private readonly AudioSource _sfxSource;

        public AudioService(SignalBus signalBus, AudioLibrary settings)
        {
            _signalBus = signalBus;
            _settings = settings;

            var root = new GameObject("AudioService");
            Object.DontDestroyOnLoad(root);

            _musicSource = CreateSource(root.transform, "Music", loop: true, volume: settings.MusicVolume);
            _ambienceSource = CreateSource(root.transform, "Ambience", loop: true, volume: settings.AmbienceVolume);
            _sfxSource = CreateSource(root.transform, "Sfx", loop: false, volume: 1f);
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
            if (activeSceneName == "GameplayScene") OnGameplayReady();
            else if (activeSceneName == "MainMenuScene") OnMenuReady();
        }

        // Cold boot fires this too, but GameplaySceneReadySignal follows right behind it
        // (SceneLoader continues straight into gameplay) and overrides the track before it's heard.
        private void OnMenuReady() => PlayMusic(_settings.MenuMusic);

        private void OnGameplayReady()
        {
            PlayMusic(_settings.GameplayMusic);
            PlayAmbience(_settings.GameplayAmbience);
        }

        // Stop gameplay ambience the moment a level ends, before the async scene unload even starts —
        // MenuReadySignal (fired once the unload completes) picks the music back up.
        private void OnLevelEnded(LevelEndedSignal _) => StopAmbience();

        public void PlaySfx(AudioClip clip, float volumeScale = 1f)
        {
            if (clip == null) return;
            _sfxSource.PlayOneShot(clip, _settings.SfxVolume * volumeScale);
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
    }
}
