using DG.Tweening;
using RestaurantLoop.Runtime.Audio;
using RestaurantLoop.Runtime.Conveyor;
using RestaurantLoop.Runtime.Data;
using RestaurantLoop.Runtime.SceneFlow;
using RestaurantLoop.Runtime.Signals;
using TMPro;
using UnityEngine;
using Zenject;

namespace RestaurantLoop.Runtime.UI
{
    // Keep Playing and Level Failed panels; the offer itself lives in KeepPlayingOffer.
    public class LevelFailUIController : MonoBehaviour
    {
        [SerializeField] private GameObject _panelKeepPlaying;
        [SerializeField] private GameObject _panelFailed;
        [SerializeField] private UnityEngine.UI.Button _buttonPlayOn;
        [SerializeField] private UnityEngine.UI.Button _buttonKeepPlayingClose;
        [SerializeField] private UnityEngine.UI.Button _buttonTryAgain;
        [SerializeField] private UnityEngine.UI.Button _buttonExit;
        [SerializeField] private TMP_Text _keepPlayingLevelText;
        [SerializeField] private TMP_Text _keepPlayingCostText;
        [SerializeField] private TMP_Text _failedLevelText;
        [SerializeField] private TMP_Text _failedRibbonLevelText;
        [SerializeField] private TMP_Text _coinRewardText;

        private CustomerConsumeService _consumeService;
        private SignalBus _signalBus;
        private AudioService _audioService;
        private AudioLibrary _audioLibrary;
        private CurrentLevelProvider _currentLevelProvider;
        private LevelProgression _levelProgression;
        private KeepPlayingOffer _keepPlayingOffer;

        [Inject]
        public void Construct(
            CustomerConsumeService consumeService,
            SignalBus signalBus,
            AudioService audioService,
            AudioLibrary audioLibrary,
            CurrentLevelProvider currentLevelProvider,
            LevelProgression levelProgression,
            KeepPlayingOffer keepPlayingOffer)
        {
            _consumeService = consumeService;
            _signalBus = signalBus;
            _audioService = audioService;
            _audioLibrary = audioLibrary;
            _currentLevelProvider = currentLevelProvider;
            _levelProgression = levelProgression;
            _keepPlayingOffer = keepPlayingOffer;
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
            if (!won)
            {
                _levelProgression.RecordLevelFailed();
                ShowKeepPlaying();
            }
        }

        private void ShowKeepPlaying()
        {
            UpdateKeepPlayingTexts();
            _panelKeepPlaying.SetActive(true);
            Time.timeScale = 0f;

            _audioService.PauseMusic();
            _audioService.PauseAmbience();
            _audioService.StopConveyorLoop();

            if (_audioLibrary != null && _audioLibrary.FailSfx != null)
            {
                _audioService.PlaySfx(_audioLibrary.FailSfx);
            }
        }

        private void UpdateKeepPlayingTexts()
        {
            if (_currentLevelProvider.Current == null) return;
            int levelId = _currentLevelProvider.Current.LevelId;

            if (_keepPlayingLevelText != null)
            {
                _keepPlayingLevelText.text = $"LEVEL {levelId}";
            }
        }

        private void UpdateFailedTexts()
        {
            if (_currentLevelProvider.Current == null) return;
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
                _coinRewardText.text = LevelProgression.GetRewardCoins(_currentLevelProvider.Current.Difficulty).ToString();
            }
        }

        private void OnPlayOnClicked()
        {
            if (!_keepPlayingOffer.TryAccept())
            {
                Debug.LogWarning($"[LevelFailUIController] Not enough coins (needs {KeepPlayingOffer.Cost}) to keep playing.");
                PlayNotEnoughCoinsFeedback();
                return;
            }

            _panelKeepPlaying.SetActive(false);
            Time.timeScale = 1f;

            _signalBus.Fire<LevelResumedSignal>();

            _audioService.ResumeMusic();
            _audioService.ResumeAmbience();
        }

        private void OnKeepPlayingCloseClicked()
        {
            _keepPlayingOffer.Decline();
            _panelKeepPlaying.SetActive(false);
            UpdateFailedTexts();
            _panelFailed.SetActive(true);
            Time.timeScale = 0f;
        }

        private void OnTryAgainClicked()
        {
            _panelFailed.SetActive(false);
            Time.timeScale = 1f;

            _audioService.StopMusic();
            _audioService.StopAmbience();

            _signalBus.Fire<RestartLevelSignal>();
        }

        private void OnExitClicked()
        {
            _panelFailed.SetActive(false);
            Time.timeScale = 1f;

            _audioService.StopMusic();
            _audioService.StopAmbience();

            _signalBus.Fire<LevelEndedSignal>();
        }

        private void PlayNotEnoughCoinsFeedback()
        {
            if (_audioLibrary != null)
            {
                var sfx = _audioLibrary.NotEnoughCoinsSfx != null ? _audioLibrary.NotEnoughCoinsSfx : _audioLibrary.FailSfx;
                if (sfx != null) _audioService.PlaySfx(sfx);
            }

            var targetText = _keepPlayingCostText;
            if (targetText == null && _buttonPlayOn != null)
            {
                targetText = _buttonPlayOn.GetComponentInChildren<TMP_Text>();
            }

            if (targetText != null)
            {
                DOTween.Kill(targetText);
                DOTween.Kill(targetText.transform);

                Color originalColor = targetText.color;
                targetText.color = new Color(1f, 0.25f, 0.25f, 1f); // Red warning
                targetText.transform.DOPunchPosition(new Vector3(10f, 0f, 0f), 0.45f, 20, 1f, false)
                    .SetUpdate(true);
                targetText.DOColor(originalColor, 0.45f)
                    .SetUpdate(true);
            }
        }
    }
}
