using System.Collections;
using DG.Tweening;
using RestaurantLoop.Runtime.Data;
using RestaurantLoop.Runtime.SceneFlow;
using RestaurantLoop.Runtime.Signals;
using UnityEngine;
using Zenject;

namespace RestaurantLoop.Runtime.UI
{
    // Lives in MainMenuScene (loaded once, never unloaded), so one instance covers every scene transition.
    // loadingRoot is authored active: that is what covers cold boot, before LoadingStartedSignal can fire.
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
        // Stays 0 on cold boot, matching the overlay being visible from scene start.
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

            // Not hidden on MainMenuReadySignal: on cold boot that chains straight into GameplayScene.
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
