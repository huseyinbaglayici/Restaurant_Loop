using RestaurantLoop.Signals;
using UnityEngine;
using UnityEngine.SceneManagement;
using Zenject;

namespace RestaurantLoop.SceneFlow
{
    // The shared loading screen lives inside MainMenuScene (LoadingController) — it can't be shown
    // before that scene exists, so we don't try to; LoadingRoot's own authored default (active) is
    // what covers the instant it wakes up. Once MainMenuScene is loaded, MainMenuReadySignal tells
    // SceneLoader to continue straight on into GameplayScene, so the menu itself is never seen on
    // cold boot.
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
