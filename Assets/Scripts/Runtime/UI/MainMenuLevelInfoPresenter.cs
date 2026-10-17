using RestaurantLoop.Runtime.Data;
using RestaurantLoop.Runtime.SceneFlow;
using RestaurantLoop.Runtime.Signals;
using TMPro;
using UnityEngine;
using Zenject;

namespace RestaurantLoop.Runtime.UI
{
    // Menu level label, themed background and per-difficulty play button sprite.
    // Single owner of the menu background sprite.
    public class MainMenuLevelInfoPresenter : MonoBehaviour
    {
        [SerializeField] private TextMeshProUGUI _levelInfoText;
        [SerializeField] private TextMeshProUGUI _difficultyText;
        [Tooltip("Main menu background image. If empty, a sibling named \"Background\" is used.")]
        [SerializeField] private UnityEngine.UI.Image _menuBackgroundImage;
        [Tooltip("Play button image, swapped per level difficulty. If empty, the first child Image is used.")]
        [SerializeField] private UnityEngine.UI.Image _playButtonImage;

        private CurrentLevelProvider _currentLevelProvider;
        private ThemeSettings _themeSettings;
        private SignalBus _signalBus;

        [Inject]
        public void Construct(
            CurrentLevelProvider currentLevelProvider,
            ThemeSettings themeSettings,
            SignalBus signalBus)
        {
            _currentLevelProvider = currentLevelProvider;
            _themeSettings = themeSettings;
            _signalBus = signalBus;
        }

        // Also runs when MainMenuVisibilityController re-activates the menu root.
        private void OnEnable()
        {
            UpdateView();
        }

        private void Start()
        {
            if (_currentLevelProvider != null)
            {
                _currentLevelProvider.OnCurrentLevelChanged += OnCurrentLevelChanged;
            }

            _signalBus?.Subscribe<MenuReadySignal>(UpdateView);

            UpdateView();
        }

        private void OnDestroy()
        {
            if (_currentLevelProvider != null)
            {
                _currentLevelProvider.OnCurrentLevelChanged -= OnCurrentLevelChanged;
            }

            _signalBus?.Unsubscribe<MenuReadySignal>(UpdateView);
        }

        private void OnCurrentLevelChanged(ConveyorLevelDataSO level)
        {
            UpdateView();
        }

        public void UpdateView()
        {
            ConveyorLevelDataSO level = _currentLevelProvider?.Current;
            if (level == null && _currentLevelProvider != null)
            {
                _currentLevelProvider.ResolveFromSave();
                level = _currentLevelProvider.Current;
            }

            if (level != null && _levelInfoText != null)
            {
                _levelInfoText.text = $"Level {level.LevelId}";
            }

            if (_themeSettings != null)
            {
                ApplyBackground(level);
                ApplyPlayButtonSprite(level);
            }

            if (_difficultyText != null)
            {
                _difficultyText.gameObject.SetActive(false);
            }
        }

        private void ApplyBackground(ConveyorLevelDataSO level)
        {
            ThemeType themeType = level != null ? level.Theme : ThemeType.Asian;
            var theme = _themeSettings.GetTheme(themeType);
            if (theme == null || theme.MainMenuBackground == null) return;

            if (_menuBackgroundImage != null)
            {
                _menuBackgroundImage.sprite = theme.MainMenuBackground;
                return;
            }

            var bg = transform.parent != null ? transform.parent.Find("Background")?.GetComponent<UnityEngine.UI.Image>() : null;
            if (bg != null) bg.sprite = theme.MainMenuBackground;
        }

        private void ApplyPlayButtonSprite(ConveyorLevelDataSO level)
        {
            var difficulty = level != null ? level.Difficulty : LevelDifficulty.None;
            var buttonSprite = _themeSettings.GetPlayButtonSprite(difficulty);
            if (buttonSprite == null) return;

            if (_playButtonImage != null)
            {
                _playButtonImage.sprite = buttonSprite;
                return;
            }

            var btnImage = GetComponentInChildren<UnityEngine.UI.Image>();
            if (btnImage != null && btnImage != _menuBackgroundImage)
            {
                btnImage.sprite = buttonSprite;
            }
        }
    }
}
