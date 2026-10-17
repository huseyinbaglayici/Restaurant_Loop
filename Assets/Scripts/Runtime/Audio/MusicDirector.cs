using RestaurantLoop.Runtime.Data;
using RestaurantLoop.Runtime.SceneFlow;
using RestaurantLoop.Runtime.Signals;
using UnityEngine;
using UnityEngine.SceneManagement;
using Zenject;

namespace RestaurantLoop.Runtime.Audio
{
    // Picks menu/gameplay music and ambience from scene flow signals and the current level's theme.
    public class MusicDirector : IInitializable
    {
        private readonly SignalBus _signalBus;
        private readonly AudioService _audioService;
        private readonly AudioLibrary _settings;
        private readonly CurrentLevelProvider _currentLevelProvider;
        private readonly ThemeSettings _themeSettings;

        public MusicDirector(
            SignalBus signalBus,
            AudioService audioService,
            AudioLibrary settings,
            CurrentLevelProvider currentLevelProvider,
            ThemeSettings themeSettings)
        {
            _signalBus = signalBus;
            _audioService = audioService;
            _settings = settings;
            _currentLevelProvider = currentLevelProvider;
            _themeSettings = themeSettings;
        }

        public void Initialize()
        {
            _signalBus.Subscribe<MainMenuReadySignal>(OnMenuReady);
            _signalBus.Subscribe<MenuReadySignal>(OnMenuReady);
            _signalBus.Subscribe<GameplaySceneReadySignal>(OnGameplayReady);
            _signalBus.Subscribe<LevelEndedSignal>(OnLevelEnded);

            // Entering Play Mode with GameplayScene or MainMenuScene open skips the boot signal chain.
            var activeSceneName = SceneManager.GetActiveScene().name;
            if (activeSceneName == SceneNames.Gameplay) OnGameplayReady();
            else if (activeSceneName == SceneNames.MainMenu) OnMenuReady();
        }

        // On cold boot GameplaySceneReadySignal follows right away and replaces this track before it's heard.
        private void OnMenuReady()
        {
            _audioService.StopConveyorLoop();

            AudioClip menuMusic = _settings != null ? _settings.MenuMusic : null;
            if (_currentLevelProvider?.Current != null && _themeSettings != null)
            {
                var theme = _themeSettings.GetTheme(_currentLevelProvider.Current.Theme);
                if (theme != null && theme.MenuMusic != null) menuMusic = theme.MenuMusic;
            }

            if (menuMusic != null) _audioService.PlayMusic(menuMusic);
        }

        private void OnGameplayReady()
        {
            AudioClip gameplayMusic = _settings != null ? _settings.GameplayMusic : null;
            AudioClip ambience = _settings != null ? _settings.GameplayAmbience : null;

            if (_currentLevelProvider?.Current != null && _themeSettings != null)
            {
                var level = _currentLevelProvider.Current;
                var theme = _themeSettings.GetTheme(level.Theme);
                if (theme != null)
                {
                    gameplayMusic = PickGameplayMusic(theme, level.Difficulty, gameplayMusic);
                    if (theme.GameplayAmbience != null) ambience = theme.GameplayAmbience;
                }
            }

            if (gameplayMusic != null) _audioService.PlayMusic(gameplayMusic);
            if (ambience != null) _audioService.PlayAmbience(ambience);
        }

        // Super Hard falls back to the Hard track, both fall back to the theme's normal track.
        private static AudioClip PickGameplayMusic(ThemeEntry theme, LevelDifficulty difficulty, AudioClip fallback)
        {
            if (difficulty == LevelDifficulty.SuperHard && theme.SuperHardGameplayMusic != null)
                return theme.SuperHardGameplayMusic;
            if ((difficulty == LevelDifficulty.Hard || difficulty == LevelDifficulty.SuperHard) && theme.HardGameplayMusic != null)
                return theme.HardGameplayMusic;
            return theme.GameplayMusic != null ? theme.GameplayMusic : fallback;
        }

        // Stops right away instead of waiting for the async unload; MenuReadySignal brings the music back.
        private void OnLevelEnded(LevelEndedSignal _)
        {
            _audioService.StopAmbience();
            _audioService.StopConveyorLoop();
        }
    }
}
