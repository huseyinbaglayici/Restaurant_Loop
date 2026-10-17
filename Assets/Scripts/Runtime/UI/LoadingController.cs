using System.Collections;
using DG.Tweening;
using RestaurantLoop.Runtime.Data;
using RestaurantLoop.Runtime.SceneFlow;
using RestaurantLoop.Runtime.Signals;
using UnityEngine;
using Zenject;

namespace RestaurantLoop.Runtime.UI
{
    // Lives in MainMenuScene (loaded once at boot, never unloaded) so the same instance covers every
    // scene transition — boot's implicit continue-into-Gameplay included, not just later Play/Claim
    // Menu<->Gameplay loads. Owns only the loading overlay; MainMenu's own content visibility is
    // MainMenuVisibilityController's job (same scene, separate concern).
    //
    // loadingRoot's authored default is ACTIVE. That's deliberate: on cold boot nothing fires
    // LoadingStartedSignal before this scene exists (there's no subscriber to hear it), so the
    // default-on state is what covers the instant this scene wakes up. _loadingStartedAt defaults to
    // 0, which happens to line up with "loading became visible at scene start" for that same reason,
    // so HideLoading()'s elapsed-time math is still correct without an explicit boot-time Show call.
    public class LoadingController : MonoBehaviour
    {
        private const float MinLoadingDuration = 1f;
        private const float FadeOutDuration = 0.3f;

        [SerializeField] private GameObject loadingRoot;
        [SerializeField] private UnityEngine.UI.Image _backgroundImage;
        [SerializeField] private Sprite _initialBackground;
        [SerializeField] private Sprite _inBetweenLevelsBackground;

        private SignalBus _signalBus;
        private CurrentLevelProvider _currentLevelProvider;
        private ThemeSettings _themeSettings;
        private float _loadingStartedAt;
        private bool _isFirstLoad = true;
        private CanvasGroup _loadingGroup;

        [Inject]
        public void Construct(
            SignalBus signalBus,
            CurrentLevelProvider currentLevelProvider,
            ThemeSettings themeSettings)
        {
            _signalBus = signalBus;
            _currentLevelProvider = currentLevelProvider;
            _themeSettings = themeSettings;

            // Note: MainMenuReadySignal is NOT one of the hide triggers — on cold boot that signal
            // chains straight into loading GameplayScene (see SceneLoader), and this screen should
            // stay up through that so the menu is never actually seen.
            _signalBus.Subscribe<LoadingStartedSignal>(ShowLoading);
            _signalBus.Subscribe<GameplaySceneReadySignal>(HideLoading);
            _signalBus.Subscribe<MenuReadySignal>(HideLoading);
        }

        private void OnDestroy()
        {
            _signalBus.Unsubscribe<LoadingStartedSignal>(ShowLoading);
            _signalBus.Unsubscribe<GameplaySceneReadySignal>(HideLoading);
            _signalBus.Unsubscribe<MenuReadySignal>(HideLoading);
        }

        private void Awake()
        {
            _loadingGroup = loadingRoot.GetComponent<CanvasGroup>();
            if (_loadingGroup == null) _loadingGroup = loadingRoot.AddComponent<CanvasGroup>();
            UpdateBackground();
        }

        private void ShowLoading()
        {
            _loadingStartedAt = Time.time;
            UpdateBackground();
            _loadingGroup.DOKill();
            _loadingGroup.alpha = 1f;
            _loadingGroup.blocksRaycasts = true;
            loadingRoot.SetActive(true);
        }

        private void UpdateBackground()
        {
            if (_backgroundImage == null) return;

            Sprite themeBg = null;
            if (_currentLevelProvider?.Current != null && _themeSettings != null)
            {
                var theme = _themeSettings.GetTheme(_currentLevelProvider.Current.Theme);
                if (theme != null && theme.LoadingScreenBackground != null)
                {
                    themeBg = theme.LoadingScreenBackground;
                }
            }

            if (themeBg != null)
            {
                _backgroundImage.sprite = themeBg;
                return;
            }

            if (_isFirstLoad)
            {
                if (_initialBackground != null) _backgroundImage.sprite = _initialBackground;
            }
            else
            {
                if (_inBetweenLevelsBackground != null) _backgroundImage.sprite = _inBetweenLevelsBackground;
            }
        }

        private void HideLoading()
        {
            var elapsed = Time.time - _loadingStartedAt;
            var remaining = MinLoadingDuration - elapsed;

            if (remaining > 0f)
            {
                StartCoroutine(HideLoadingAfterDelay(remaining));
            }
            else
            {
                FadeOut();
            }
        }

        private IEnumerator HideLoadingAfterDelay(float delay)
        {
            yield return new WaitForSeconds(delay);
            FadeOut();
        }

        private void FadeOut()
        {
            _isFirstLoad = false;
            _loadingGroup.blocksRaycasts = false;
            _signalBus.Fire<LoadingHiddenSignal>();
            _loadingGroup.DOKill();
            _loadingGroup.DOFade(0f, FadeOutDuration).SetUpdate(true).SetTarget(_loadingGroup)
                .OnComplete(() => loadingRoot.SetActive(false));
        }
    }
}
