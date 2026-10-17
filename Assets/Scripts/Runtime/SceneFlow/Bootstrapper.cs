using RestaurantLoop.Runtime.Signals;
using UnityEngine;
using UnityEngine.SceneManagement;
using Zenject;

namespace RestaurantLoop.Runtime.SceneFlow
{
    // Loads MainMenuScene (which holds the loading screen) additively, then fires MainMenuReadySignal
    // so SceneLoader can continue into GameplayScene.
    public class Bootstrapper : MonoBehaviour
    {
        private SignalBus _signalBus;

        [Inject]
        public void Construct(SignalBus signalBus)
        {
            _signalBus = signalBus;
        }

        private void Start()
        {
            var operation = SceneManager.LoadSceneAsync(SceneNames.MainMenu, LoadSceneMode.Additive);
            operation.completed += OnMainMenuLoaded;
        }

        private void OnMainMenuLoaded(AsyncOperation operation)
        {
            _signalBus.Fire<MainMenuReadySignal>();
        }
    }
}
