using UnityEngine;
using UnityEngine.SceneManagement;
using Zenject;
using RestaurantLoop.Signals;

namespace RestaurantLoop.SceneFlow
{
    public class SceneLoader : ISceneLoader, IInitializable
    {
        private const string GameplaySceneName = "GameplayScene";

        private readonly SignalBus _signalBus;
        private readonly CurrentLevelProvider _currentLevelProvider;

        // Tracks an in-flight unload so a load requested while it's still running (e.g. Claim ->
        // menu shown immediately -> player taps Play before the old scene is actually gone) waits
        // for it instead of loading a second GameplayScene instance on top of the first.
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
            var operation = SceneManager.LoadSceneAsync(GameplaySceneName, LoadSceneMode.Additive);
            operation.completed += OnGameplaySceneLoaded;
        }

        public void UnloadGameplayScene()
        {
            _pendingUnload = SceneManager.UnloadSceneAsync(GameplaySceneName);
        }

        private void OnRestartRequested(RestartLevelSignal signal)
        {
            UnloadGameplayScene();
            LoadGameplayScene();
        }

        private void OnGameplaySceneLoaded(AsyncOperation operation)
        {
            var scene = SceneManager.GetSceneByName(GameplaySceneName);
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
