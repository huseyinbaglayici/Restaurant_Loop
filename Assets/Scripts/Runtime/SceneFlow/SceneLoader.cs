using UnityEngine;
using UnityEngine.SceneManagement;
using Zenject;
using RestaurantLoop.Runtime.Signals;
using RestaurantLoop.Runtime.Data;
using RestaurantLoop.Runtime.Lives;

namespace RestaurantLoop.Runtime.SceneFlow
{
    public class SceneLoader : IInitializable
    {
        private readonly SignalBus _signalBus;
        private readonly CurrentLevelProvider _currentLevelProvider;
        private readonly LivesService _livesService;

        private AsyncOperation _pendingUnload;

        public SceneLoader(SignalBus signalBus, CurrentLevelProvider currentLevelProvider, LivesService livesService)
        {
            _signalBus = signalBus;
            _currentLevelProvider = currentLevelProvider;
            _livesService = livesService;
        }

        public void Initialize()
        {
            _signalBus.Subscribe<LevelEndedSignal>(OnLevelEnded);
            _signalBus.Subscribe<PlayRequestedSignal>(OnPlayRequested);
            _signalBus.Subscribe<RestartLevelSignal>(OnRestartRequested);

            _signalBus.Subscribe<MainMenuReadySignal>(OnMainMenuReady);
        }

        // Cold boot only: MainMenuScene just finished loading. Continue straight on into GameplayScene
        // under the same loading screen — unless the player is out of lives, then the menu (with its
        // refill timer) is shown instead of a free attempt.
        private void OnMainMenuReady()
        {
            if (_livesService.HasLives) LoadGameplayScene();
            else _signalBus.Fire<MenuReadySignal>();
        }

        public void LoadGameplayScene()
        {
            _signalBus.Fire<LoadingStartedSignal>();

            if (_pendingUnload != null && !_pendingUnload.isDone)
            {
                _pendingUnload.completed += _ => BeginLoad();
                return;
            }

            BeginLoad();
        }

        private void BeginLoad()
        {
            var targetScene = SceneNames.Gameplay;
            var operation = SceneManager.LoadSceneAsync(targetScene, LoadSceneMode.Additive);
            operation.completed += OnGameplaySceneLoaded;
        }

        public void UnloadGameplayScene()
        {
            var targetScene = SceneNames.Gameplay;
            _pendingUnload = SceneManager.UnloadSceneAsync(targetScene);
        }

        private void OnRestartRequested(RestartLevelSignal signal)
        {
            UnloadGameplayScene();
            LoadGameplayScene();
        }

        private void OnGameplaySceneLoaded(AsyncOperation operation)
        {
            var targetScene = SceneNames.Gameplay;
            var scene = SceneManager.GetSceneByName(targetScene);
            SceneManager.SetActiveScene(scene);

            _signalBus.Fire<GameplaySceneReadySignal>();
        }

        // Claim: show the loading screen for the teardown too, then reveal the menu once
        // GameplayScene is actually gone (not on the raw signal — the unload is still async).
        private void OnLevelEnded(LevelEndedSignal signal)
        {
            _signalBus.Fire<LoadingStartedSignal>();

            UnloadGameplayScene();
            if (_pendingUnload != null)
                _pendingUnload.completed += _ => _signalBus.Fire<MenuReadySignal>();
            else
                _signalBus.Fire<MenuReadySignal>();
        }

        private void OnPlayRequested(PlayRequestedSignal signal)
        {
            // Null Level means "continue with whatever CurrentLevelProvider resolved at boot" — only an
            // explicit level-select (future menu) should override it.
            if (signal.Level != null)
                _currentLevelProvider.Current = signal.Level;

            LoadGameplayScene();
        }
    }
}
