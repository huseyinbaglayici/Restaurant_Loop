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

            // Normal boot goes MainMenuReadySignal -> GameplaySceneReadySignal via Bootstrapper/SceneLoader.
            // Hitting Play with GameplayScene or MainMenuScene already open (editor testing) skips that
            // chain entirely, so bootstrap straight off whatever's already active instead of staying silent.
            var activeSceneName = SceneManager.GetActiveScene().name;
            if (activeSceneName == SceneNames.Gameplay) OnGameplayReady();
            else if (activeSceneName == SceneNames.MainMenu) OnMenuReady();
        }

        // Cold boot fires this too, but GameplaySceneReadySignal follows right behind it
        // (SceneLoader continues straight into gameplay) and overrides the track before it's heard.
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
                    // Select music according to level difficulty
                    if (level.Difficulty == LevelDifficulty.SuperHard && theme.SuperHardGameplayMusic != null)
                    {
                        gameplayMusic = theme.SuperHardGameplayMusic;
                    }
                    else if ((level.Difficulty == LevelDifficulty.Hard || level.Difficulty == LevelDifficulty.SuperHard) && theme.HardGameplayMusic != null)
                    {
                        gameplayMusic = theme.HardGameplayMusic;
                    }
                    else if (theme.GameplayMusic != null)
                    {
                        gameplayMusic = theme.GameplayMusic;
                    }

                    if (theme.GameplayAmbience != null) ambience = theme.GameplayAmbience;
                }
            }

            if (gameplayMusic != null) _audioService.PlayMusic(gameplayMusic);
            if (ambience != null) _audioService.PlayAmbience(ambience);
        }

        // Stop gameplay ambience and conveyor loop the moment a level ends, before the async scene unload even starts —
        // MenuReadySignal (fired once the unload completes) picks the music back up.
        private void OnLevelEnded(LevelEndedSignal _)
        {
            _audioService.StopAmbience();
            _audioService.StopConveyorLoop();
        }
    }
}
