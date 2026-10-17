using UnityEngine;
using UnityEngine.SceneManagement;
using Zenject;
using RestaurantLoop.Signals;
using RestaurantLoop.Runtime.Data;

namespace RestaurantLoop.SceneFlow
{
    public class SceneLoader : ISceneLoader, IInitializable
    {
        private readonly SignalBus _signalBus;
        private readonly CurrentLevelProvider _currentLevelProvider;

        private AsyncOperation _pendingUnload;

        public SceneLoader(SignalBus signalBus, CurrentLevelProvider currentLevelProvider)
        {
            _signalBus = signalBus;
            _currentLevelProvider = currentLevelProvider;
        }

        public void Initialize()
        {
            _signalBus.Subscribe<LevelEndedSignal>(OnLevelEnded);
            _signalBus.Subscribe<PlayRequestedSignal>(OnPlayRequested);
            _signalBus.Subscribe<RestartLevelSignal>(OnRestartRequested);

            // Cold boot only: MainMenuScene just finished loading. Continue straight on into
            // GameplayScene under the same loading screen — the player never sees the menu.
            _signalBus.Subscribe<MainMenuReadySignal>(LoadGameplayScene);
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
            // Null Level means "continue with whatever SaveManager resolved at boot" — only an
            // explicit level-select (future menu) should override it.
            if (signal.Level != null)
                _currentLevelProvider.Current = signal.Level;

            LoadGameplayScene();
        }
    }
}
