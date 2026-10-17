using RestaurantLoop.Runtime.Signals;
using UnityEngine;
using Zenject;

namespace RestaurantLoop.Runtime.UI
{
    // Hides the menu content while GameplayScene is up (so it can't eat input) and shows it on MenuReadySignal,
    // which fires only after GameplayScene has finished unloading (never on cold boot).
    public class MainMenuVisibilityController : MonoBehaviour
    {
        [SerializeField] private GameObject menuRoot;

        private SignalBus _signalBus;

        [Inject]
        public void Construct(SignalBus signalBus)
        {
            _signalBus = signalBus;

            _signalBus.Subscribe<GameplaySceneReadySignal>(HideMenu);
            _signalBus.Subscribe<MenuReadySignal>(ShowMenu);

            menuRoot.SetActive(false);
        }

        private void OnDestroy()
        {
            _signalBus.Unsubscribe<GameplaySceneReadySignal>(HideMenu);
            _signalBus.Unsubscribe<MenuReadySignal>(ShowMenu);
        }

        private void HideMenu() => menuRoot.SetActive(false);

        private void ShowMenu() => menuRoot.SetActive(true);
    }
}
