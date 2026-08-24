using RestaurantLoop.Runtime.Conveyor;
using RestaurantLoop.Runtime.Data;
using RestaurantLoop.Save;
using RestaurantLoop.SceneFlow;
using RestaurantLoop.Signals;
using UnityEngine;
using Zenject;

namespace RestaurantLoop.Runtime.UI
{
    public class LevelCompleteUIController : MonoBehaviour
    {
        [SerializeField] private GameObject _panelCompleted;
        [SerializeField] private UnityEngine.UI.Button _buttonNext;

        private CustomerConsumeService _consumeService;
        private SignalBus _signalBus;
        private ISaveManager _saveManager;
        private CurrentLevelProvider _currentLevelProvider;
        private LevelDatabase _levelDatabase;

        [Inject]
        public void Construct(
            CustomerConsumeService consumeService,
            SignalBus signalBus,
            ISaveManager saveManager,
            CurrentLevelProvider currentLevelProvider,
            LevelDatabase levelDatabase)
        {
            _consumeService = consumeService;
            _signalBus = signalBus;
            _saveManager = saveManager;
            _currentLevelProvider = currentLevelProvider;
            _levelDatabase = levelDatabase;
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
        }

        private void OnNextClicked()
        {
            _panelCompleted.SetActive(false);
            Time.timeScale = 1f;

            AdvanceSavedLevel();

            _signalBus.Fire<LevelEndedSignal>();
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
