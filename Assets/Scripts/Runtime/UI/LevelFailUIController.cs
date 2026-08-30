using RestaurantLoop.Audio;
using RestaurantLoop.Lives;
using RestaurantLoop.Runtime.Conveyor;
using RestaurantLoop.Runtime.Data;
using RestaurantLoop.SceneFlow;
using RestaurantLoop.Signals;
using TMPro;
using UnityEngine;
using Zenject;

namespace RestaurantLoop.Runtime.UI
{
    public class LevelFailUIController : MonoBehaviour
    {
        [SerializeField] private GameObject _panelKeepPlaying;
        [SerializeField] private GameObject _panelFailed;
        [SerializeField] private UnityEngine.UI.Button _buttonPlayOn;
        [SerializeField] private UnityEngine.UI.Button _buttonKeepPlayingClose;
        [SerializeField] private UnityEngine.UI.Button _buttonTryAgain;
        [SerializeField] private UnityEngine.UI.Button _buttonExit;
        [SerializeField] private TMP_Text _keepPlayingLevelText;
        [SerializeField] private TMP_Text _failedLevelText;
        [SerializeField] private TMP_Text _failedRibbonLevelText;
        [SerializeField] private TMP_Text _coinRewardText;

        private CustomerConsumeService _consumeService;
        private ConveyorManager _conveyorManager;
        private ExitStackAnchor _exitStackAnchor;
        private FoodItemController.Pool _foodPool;
        private SignalBus _signalBus;
        private PowerUpService _powerUpService;
        private IAudioService _audioService;
        private AudioLibrary _audioLibrary;
        private ILivesService _livesService;
        private CurrentLevelProvider _currentLevelProvider;
        private ConveyorAutoClearService _autoClearService;

        [Inject]
        public void Construct(
            CustomerConsumeService consumeService,
            ConveyorManager conveyorManager,
            ExitStackAnchor exitStackAnchor,
            FoodItemController.Pool foodPool,
            SignalBus signalBus,
            PowerUpService powerUpService,
            [Inject(Optional = true)] IAudioService audioService = null,
            [Inject(Optional = true)] AudioLibrary audioLibrary = null,
            [Inject(Optional = true)] ILivesService livesService = null,
            [Inject(Optional = true)] CurrentLevelProvider currentLevelProvider = null,
            [Inject(Optional = true)] ConveyorAutoClearService autoClearService = null)
        {
            _consumeService = consumeService;
            _conveyorManager = conveyorManager;
            _exitStackAnchor = exitStackAnchor;
            _foodPool = foodPool;
            _signalBus = signalBus;
            _powerUpService = powerUpService;
            _audioService = audioService;
            _audioLibrary = audioLibrary;
            _livesService = livesService;
            _currentLevelProvider = currentLevelProvider;
            _autoClearService = autoClearService;
        }

        private void Start()
        {
            _consumeService.OnLevelEnded += HandleLevelEnded;

            _buttonPlayOn.onClick.AddListener(OnPlayOnClicked);
            _buttonKeepPlayingClose.onClick.AddListener(OnKeepPlayingCloseClicked);
            _buttonTryAgain.onClick.AddListener(OnTryAgainClicked);
            _buttonExit.onClick.AddListener(OnExitClicked);
        }

        private void OnDestroy()
        {
            _consumeService.OnLevelEnded -= HandleLevelEnded;
        }

        private void HandleLevelEnded(bool won)
        {
            if (!won) ShowKeepPlaying();
        }

        private void ShowKeepPlaying()
        {
            UpdateKeepPlayingTexts();
            _panelKeepPlaying.SetActive(true);
            Time.timeScale = 0f;

            if (_audioService != null)
            {
                _audioService.PauseMusic();
                _audioService.PauseAmbience();
                _audioService.StopConveyorLoop();

                if (_audioLibrary != null && _audioLibrary.FailSfx != null)
                {
                    _audioService.PlaySfx(_audioLibrary.FailSfx);
                }
            }
        }

        private void UpdateKeepPlayingTexts()
        {
            if (_currentLevelProvider == null || _currentLevelProvider.Current == null) return;
            int levelId = _currentLevelProvider.Current.LevelId;

            if (_keepPlayingLevelText != null)
            {
                _keepPlayingLevelText.text = $"LEVEL {levelId}";
            }
        }

        private void UpdateFailedTexts()
        {
            if (_currentLevelProvider == null || _currentLevelProvider.Current == null) return;
            int levelId = _currentLevelProvider.Current.LevelId;

            if (_failedLevelText != null)
            {
                _failedLevelText.text = $"Level {levelId}";
            }

            if (_failedRibbonLevelText != null)
            {
                _failedRibbonLevelText.text = $"LEVEL {levelId}";
            }

            if (_coinRewardText != null)
            {
                int reward = _currentLevelProvider.Current.Difficulty switch
                {
                    LevelDifficulty.Hard => 75,
                    LevelDifficulty.SuperHard => 100,
                    LevelDifficulty.Bonus => 100,
                    _ => 50
                };
                _coinRewardText.text = reward.ToString();
            }
        }

        private void OnPlayOnClicked()
        {
            _conveyorManager.ClearBelt();
            _powerUpService.ActivateExtraTray(); // Extra Tray power-up: adds another slot each time Keep Playing is used.

            // ClearBelt() zeroes ReservedItemCount, which can make ConveyorAutoClearService think the
            // (still deadlocked) exit stack now "fits" and guarantees a win — yanking every occupant back
            // onto the belt right as the player resumes. The level already failed once; don't let it be
            // silently auto-solved out from under a manual Keep Playing continuation.
            _autoClearService?.Disable();

            _panelKeepPlaying.SetActive(false);
            Time.timeScale = 1f;

            if (_audioService != null)
            {
                _audioService.ResumeMusic();
                _audioService.ResumeAmbience();
            }
        }

        private void OnKeepPlayingCloseClicked()
        {
            _livesService?.ConsumeLife();
            _panelKeepPlaying.SetActive(false);
            UpdateFailedTexts();
            _panelFailed.SetActive(true);
            Time.timeScale = 0f;
        }

        private void OnTryAgainClicked()
        {
            _panelFailed.SetActive(false);
            Time.timeScale = 1f;

            if (_audioService != null)
            {
                _audioService.StopMusic();
                _audioService.StopAmbience();
            }

            _signalBus.Fire<RestartLevelSignal>();
        }

        private void OnExitClicked()
        {
            _panelFailed.SetActive(false);
            Time.timeScale = 1f;

            if (_audioService != null)
            {
                _audioService.StopMusic();
                _audioService.StopAmbience();
            }

            _signalBus.Fire<LevelEndedSignal>();
        }
    }
}
