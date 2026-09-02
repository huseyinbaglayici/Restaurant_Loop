using RestaurantLoop.Runtime.Data;
using RestaurantLoop.SceneFlow;
using RestaurantLoop.Signals;
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

        private CurrentLevelProvider _currentLevelProvider;
        private ThemeSettings _themeSettings;
        private SignalBus _signalBus;

        [Inject]
        public void Construct(
            [Inject(Optional = true)] CurrentLevelProvider currentLevelProvider = null,
            [Inject(Optional = true)] ThemeSettings themeSettings = null,
            [Inject(Optional = true)] SignalBus signalBus = null)
        {
            _currentLevelProvider = currentLevelProvider;
            _themeSettings = themeSettings ?? Resources.Load<ThemeSettings>("ThemeSettings");
            _signalBus = signalBus;
        }

        private void Awake()
        {
            if (_themeSettings == null) _themeSettings = Resources.Load<ThemeSettings>("ThemeSettings");
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
            if (_themeSettings == null) _themeSettings = Resources.Load<ThemeSettings>("ThemeSettings");

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

            if (level == null || _difficultyText == null) return;

            bool isHard = level.Difficulty is LevelDifficulty.Hard or LevelDifficulty.SuperHard;
            _difficultyText.gameObject.SetActive(isHard || level.Difficulty == LevelDifficulty.Bonus);
            _difficultyText.text = level.Difficulty switch
            {
                LevelDifficulty.Bonus => "Bonus",
                LevelDifficulty.Hard => "Hard",
                LevelDifficulty.SuperHard => "Super Hard",
                _ => string.Empty
            };
        }
    }
}
