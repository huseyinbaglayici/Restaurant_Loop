using RestaurantLoop.Audio;
using RestaurantLoop.Economy;
using RestaurantLoop.Runtime.Conveyor;
using RestaurantLoop.Runtime.Data;
using RestaurantLoop.Save;
using RestaurantLoop.SceneFlow;
using RestaurantLoop.Signals;
using TMPro;
using UnityEngine;
using Zenject;

namespace RestaurantLoop.Runtime.UI
{
    public class LevelCompleteUIController : MonoBehaviour
    {
        [SerializeField] private GameObject _panelCompleted;
        [SerializeField] private UnityEngine.UI.Button _buttonNext;
        [SerializeField] private TMP_Text _levelText;
        [SerializeField] private TMP_Text _coinRewardText;

        private CustomerConsumeService _consumeService;
        private ConveyorManager _conveyorManager;
        private ExitStackAnchor _exitStackAnchor;
        private FoodItemController.Pool _foodPool;
        private SignalBus _signalBus;
        private ISaveManager _saveManager;
        private CurrentLevelProvider _currentLevelProvider;
        private LevelDatabase _levelDatabase;
        private IAudioService _audioService;
        private AudioLibrary _audioLibrary;
        private IEconomyService _economyService;

        [Inject]
        public void Construct(
            CustomerConsumeService consumeService,
            ConveyorManager conveyorManager,
            ExitStackAnchor exitStackAnchor,
            FoodItemController.Pool foodPool,
            SignalBus signalBus,
            ISaveManager saveManager,
            CurrentLevelProvider currentLevelProvider,
            LevelDatabase levelDatabase,
            [Inject(Optional = true)] IAudioService audioService = null,
            [Inject(Optional = true)] AudioLibrary audioLibrary = null,
            [Inject(Optional = true)] IEconomyService economyService = null)
        {
            _consumeService = consumeService;
            _conveyorManager = conveyorManager;
            _exitStackAnchor = exitStackAnchor;
            _foodPool = foodPool;
            _signalBus = signalBus;
            _saveManager = saveManager;
            _currentLevelProvider = currentLevelProvider;
            _levelDatabase = levelDatabase;
            _audioService = audioService;
            _audioLibrary = audioLibrary;
            _economyService = economyService;
        }

        private void Start()
        {
            _consumeService.OnLevelEnded += HandleLevelEnded;
            _buttonNext.onClick.AddListener(OnNextClicked);
        }

        private void OnDestroy()
        {
            _consumeService.OnLevelEnded -= HandleLevelEnded;
        }

        private void HandleLevelEnded(bool won)
        {
            if (won) ShowCompleted();
        }

        private void ShowCompleted()
        {
            Time.timeScale = 0f;
            _panelCompleted.SetActive(true);

            UpdateTexts();
            AwardLevelCoins();

            if (_audioService != null)
            {
                _audioService.StopMusic();
                _audioService.StopAmbience();
                _audioService.StopConveyorLoop();

                if (_audioLibrary != null && _audioLibrary.WinSfx != null)
                {
                    _audioService.PlaySfx(_audioLibrary.WinSfx);
                }
            }
        }

        private void UpdateTexts()
        {
            if (_currentLevelProvider == null || _currentLevelProvider.Current == null) return;

            int levelId = _currentLevelProvider.Current.LevelId;
            if (_levelText != null)
            {
                _levelText.text = $"Level {levelId}";
            }

            int reward = GetRewardCoins(_currentLevelProvider.Current.Difficulty);
            if (_coinRewardText != null)
            {
                _coinRewardText.text = reward.ToString();
            }
        }

        private int GetRewardCoins(LevelDifficulty difficulty)
        {
            return difficulty switch
            {
                LevelDifficulty.Hard => 75,
                LevelDifficulty.SuperHard => 100,
                LevelDifficulty.Bonus => 100,
                _ => 50
            };
        }

        private void AwardLevelCoins()
        {
            if (_economyService == null || _currentLevelProvider == null || _currentLevelProvider.Current == null) return;

            int reward = GetRewardCoins(_currentLevelProvider.Current.Difficulty);
            _economyService.AddCoins(reward);
        }

        private void OnNextClicked()
        {
            _panelCompleted.SetActive(false);
            Time.timeScale = 1f;

            ClearRemainingEntities();
            AdvanceSavedLevel();

            _signalBus.Fire<LevelEndedSignal>();
        }

        private void ClearRemainingEntities()
        {
            _conveyorManager.ClearBelt();

            for (int i = 0; i < _exitStackAnchor.SlotCount; i++)
            {
                if (!_exitStackAnchor.TryRemoveFromSlot(i, out var visual)) continue;
                if (visual != null) _foodPool.Despawn(visual);
            }
        }

        // Only commits if the next id actually exists — otherwise this was the last level, so
        // progress stays put instead of saving a level id the database can't resolve on next boot.
        private void AdvanceSavedLevel()
        {
            if (_currentLevelProvider.Current == null) return;

            int nextLevelId = _currentLevelProvider.Current.LevelId + 1;
            if (!_levelDatabase.TryGetLevel(nextLevelId, out _)) return;

            _saveManager.SetCurrentLevel(nextLevelId);
            _currentLevelProvider.ResolveFromSave();
        }
    }
}
