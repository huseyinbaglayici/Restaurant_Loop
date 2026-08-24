using RestaurantLoop.Runtime.Conveyor;
using RestaurantLoop.Signals;
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

        private CustomerConsumeService _consumeService;
        private ConveyorManager _conveyorManager;
        private ExitStackAnchor _exitStackAnchor;
        private FoodItemController.Pool _foodPool;
        private SignalBus _signalBus;

        [Inject]
        public void Construct(
            CustomerConsumeService consumeService,
            ConveyorManager conveyorManager,
            ExitStackAnchor exitStackAnchor,
            FoodItemController.Pool foodPool,
            SignalBus signalBus)
        {
            _consumeService = consumeService;
            _conveyorManager = conveyorManager;
            _exitStackAnchor = exitStackAnchor;
            _foodPool = foodPool;
            _signalBus = signalBus;
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
            _panelKeepPlaying.SetActive(true);
            Time.timeScale = 0f;
        }

        private void OnPlayOnClicked()
        {
            _conveyorManager.ClearBelt();
            _exitStackAnchor.TryRemoveFromSlot(_exitStackAnchor.SlotCount - 1, out var visual);
            if (visual != null) _foodPool.Despawn(visual);

            _panelKeepPlaying.SetActive(false);
            Time.timeScale = 1f;
        }

        private void OnKeepPlayingCloseClicked()
        {
            _panelKeepPlaying.SetActive(false);
            _panelFailed.SetActive(true);
            Time.timeScale = 0f;
        }

        private void OnTryAgainClicked()
        {
            _panelFailed.SetActive(false);
            Time.timeScale = 1f;
            _signalBus.Fire<RestartLevelSignal>();
        }

        private void OnExitClicked()
        {
            _panelFailed.SetActive(false);
            Time.timeScale = 1f;
            _signalBus.Fire<LevelEndedSignal>();
        }
    }
}
