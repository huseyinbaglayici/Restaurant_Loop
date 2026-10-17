using RestaurantLoop.Runtime.Signals;
using UnityEngine;
using Zenject;

namespace RestaurantLoop.Runtime.UI
{
    // Lives in MainMenuScene. Hides the menu content while GameplayScene is up (also stops it eating
    // input behind the gameplay view) and brings it back once the loading screen is about to hand
    // control back to the menu. Waits for MenuReadySignal rather than the raw LevelEndedSignal so it
    // only ever becomes visible once GameplayScene has actually finished unloading — on cold boot
    // MenuReadySignal never fires at all, so this stays hidden the whole way into first gameplay.
    public class MainMenuVisibilityController : MonoBehaviour
    {
        [SerializeField] private GameObject menuRoot;
        [SerializeField] private UnityEngine.UI.Image _menuBackgroundImage;

        private SignalBus _signalBus;
        private SceneFlow.CurrentLevelProvider _currentLevelProvider;
        private RestaurantLoop.Runtime.Data.ThemeSettings _themeSettings;

        [Inject]
        public void Construct(
            SignalBus signalBus,
            SceneFlow.CurrentLevelProvider currentLevelProvider,
            RestaurantLoop.Runtime.Data.ThemeSettings themeSettings)
        {
            _signalBus = signalBus;
            _currentLevelProvider = currentLevelProvider;
            _themeSettings = themeSettings;

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

        private void ShowMenu()
        {
            UpdateThemeBackground();
            menuRoot.SetActive(true);
        }

        private void UpdateThemeBackground()
        {
            var targetImage = _menuBackgroundImage;
            if (targetImage == null && menuRoot != null)
            {
                // Auto-resolve Background Image from menuRoot if not assigned in Inspector
                var bgTransform = menuRoot.transform.Find("Background") ?? menuRoot.transform.Find("BG") ?? menuRoot.transform.Find("Bg");
                if (bgTransform != null) targetImage = bgTransform.GetComponent<UnityEngine.UI.Image>();
            }

            if (targetImage == null) return;

            if (_currentLevelProvider != null && _themeSettings != null)
            {
                if (_currentLevelProvider.Current == null) _currentLevelProvider.ResolveFromSave();

                if (_currentLevelProvider.Current != null)
                {
                    var theme = _themeSettings.GetTheme(_currentLevelProvider.Current.Theme);
                    if (theme != null && theme.MainMenuBackground != null)
                    {
                        targetImage.sprite = theme.MainMenuBackground;
                    }
                }
            }
        }
    }
}
