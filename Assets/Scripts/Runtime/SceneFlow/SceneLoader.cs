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

        public SceneLoader(SignalBus signalBus)
        {
            _signalBus = signalBus;
        }

        public void Initialize()
        {
            _signalBus.Subscribe<LevelEndedSignal>(OnLevelEnded);
            _signalBus.Subscribe<PlayRequestedSignal>(OnPlayRequested);
        }

        public void LoadGameplayScene()
        {
            _signalBus.Fire<LoadingStartedSignal>();

            var operation = SceneManager.LoadSceneAsync(GameplaySceneName, LoadSceneMode.Additive);
            operation.completed += OnGameplaySceneLoaded;
        }

        public void UnloadGameplayScene()
        {
            SceneManager.UnloadSceneAsync(GameplaySceneName);
        }

        private void OnGameplaySceneLoaded(AsyncOperation operation)
        {
            var scene = SceneManager.GetSceneByName(GameplaySceneName);
            SceneManager.SetActiveScene(scene);

            _signalBus.Fire<GameplaySceneReadySignal>();
        }

        private void OnLevelEnded(LevelEndedSignal signal)
        {
            UnloadGameplayScene();
        }

        private void OnPlayRequested(PlayRequestedSignal signal)
        {
            LoadGameplayScene();
        }
    }
}
