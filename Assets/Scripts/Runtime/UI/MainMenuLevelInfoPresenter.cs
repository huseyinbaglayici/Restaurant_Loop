using RestaurantLoop.Runtime.Data;
using RestaurantLoop.Runtime.SceneFlow;
using RestaurantLoop.Runtime.Signals;
using TMPro;
using UnityEngine;
using Zenject;

namespace RestaurantLoop.Runtime.UI
{
    public class MainMenuLevelInfoPresenter : MonoBehaviour
    {
        [SerializeField] private TextMeshProUGUI _levelInfoText;
        [SerializeField] private TextMeshProUGUI _difficultyText;
        [Tooltip("Inspector'dan Main Menu arka plan Image objesini buraya bağlayabilirsiniz.")]
        [SerializeField] private UnityEngine.UI.Image _menuBackgroundImage;
        [Tooltip("Inspector'dan Play/Seviye butonunun Image objesini buraya bağlayabilirsiniz (Zorluk seviyesine göre butonu değiştirir).")]
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

        private void Awake()
        {
        }

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

            // Update Theme Background
            if (_themeSettings != null)
            {
                ThemeType themeType = level != null ? level.Theme : ThemeType.Asian;
                var theme = _themeSettings.GetTheme(themeType);
                if (theme != null && theme.MainMenuBackground != null)
                {
                    if (_menuBackgroundImage != null)
                    {
                        _menuBackgroundImage.sprite = theme.MainMenuBackground;
                    }
                    else
                    {
                        // Fallback: Check if there's a background Image component on root / parent
                        var bg = transform.parent != null ? transform.parent.Find("Background")?.GetComponent<UnityEngine.UI.Image>() : null;
                        if (bg != null) bg.sprite = theme.MainMenuBackground;
                    }
                }
            }

            // Update Play Button Sprite based on Level Difficulty
            if (_themeSettings != null)
            {
                var targetDifficulty = level != null ? level.Difficulty : LevelDifficulty.None;
                var buttonSprite = _themeSettings.GetPlayButtonSprite(targetDifficulty);
                if (buttonSprite != null)
                {
                    if (_playButtonImage != null)
                    {
                        _playButtonImage.sprite = buttonSprite;
                    }
                    else
                    {
                        // Fallback: Check if SeviyeButton or child has an image
                        var btnImage = GetComponentInChildren<UnityEngine.UI.Image>();
                        if (btnImage != null && btnImage != _menuBackgroundImage)
                        {
                            btnImage.sprite = buttonSprite;
                        }
                    }
                }
            }

            if (_difficultyText != null)
            {
                _difficultyText.gameObject.SetActive(false);
            }
        }
    }
}
