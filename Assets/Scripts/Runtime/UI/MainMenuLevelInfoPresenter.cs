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

        private CurrentLevelProvider _currentLevelProvider;
        private SignalBus _signalBus;

        [Inject]
        public void Construct(
            CurrentLevelProvider currentLevelProvider,
            [Inject(Optional = true)] SignalBus signalBus = null)
        {
            _currentLevelProvider = currentLevelProvider;
            _signalBus = signalBus;
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
            if (_currentLevelProvider == null) return;

            var level = _currentLevelProvider.Current;
            if (level == null)
            {
                _currentLevelProvider.ResolveFromSave();
                level = _currentLevelProvider.Current;
            }

            if (level == null) return;

            if (_levelInfoText != null)
            {
                _levelInfoText.text = $"Level {level.LevelId}";
            }

            if (_difficultyText == null) return;

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
