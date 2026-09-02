using RestaurantLoop.Signals;
using RestaurantLoop.Runtime.Data;
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
        private const string MainMenuSceneName = "MainMenuScene";

        public static string ActiveMainMenuSceneName
        {
            get
            {
                if (ShowcaseModeConfig.IsEnabled) return ShowcaseModeConfig.TargetMainMenuSceneName;
                
                // If running from EylulBootstrap or if MainMenuScene is not in build, fallback to EylulMainMenuScene
                string activeName = SceneManager.GetActiveScene().name;
                if (activeName.Contains("Eylul") || Application.CanStreamedLevelBeLoaded("EylulMainMenuScene"))
                {
                    if (!Application.CanStreamedLevelBeLoaded(MainMenuSceneName))
                        return "EylulMainMenuScene";
                }

                return MainMenuSceneName;
            }
        }

        private SignalBus _signalBus;

        [Inject]
        public void Construct(SignalBus signalBus)
        {
            _signalBus = signalBus;
        }

        private void Start()
        {
            var targetScene = ActiveMainMenuSceneName;
            if (!Application.CanStreamedLevelBeLoaded(targetScene))
            {
                if (Application.CanStreamedLevelBeLoaded("EylulMainMenuScene"))
                    targetScene = "EylulMainMenuScene";
                else if (Application.CanStreamedLevelBeLoaded("MainMenuScene"))
                    targetScene = "MainMenuScene";
            }

            var operation = SceneManager.LoadSceneAsync(targetScene, LoadSceneMode.Additive);
            operation.completed += OnMainMenuLoaded;
        }

        private void OnMainMenuLoaded(AsyncOperation operation)
        {
            _signalBus.Fire<MainMenuReadySignal>();
        }
    }
}
