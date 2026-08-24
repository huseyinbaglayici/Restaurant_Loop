using RestaurantLoop.Signals;
using UnityEngine;
using Zenject;

namespace RestaurantLoop.UI
{
    // Lives in MainMenuScene. Hides the menu content while GameplayScene is up (also stops it eating
    // input behind the gameplay view) and brings it back once the loading screen is about to hand
    // control back to the menu. Waits for MenuReadySignal rather than the raw LevelEndedSignal so it
    // only ever becomes visible once GameplayScene has actually finished unloading — on cold boot
    // MenuReadySignal never fires at all, so this stays hidden the whole way into first gameplay.
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
