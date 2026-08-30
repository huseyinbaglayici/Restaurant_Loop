using RestaurantLoop.Runtime.Conveyor;
using RestaurantLoop.Runtime.Data;
using RestaurantLoop.Runtime.Tutorial;
using RestaurantLoop.Signals;
using UnityEngine;
using UnityEngine.UI;
using Zenject;

namespace RestaurantLoop.Runtime.UI
{
    // Placeholder power-up bar. No cooldown/availability visuals yet — just wires clicks to PowerUpService.
    public class PowerUpButtonsController : MonoBehaviour
    {
        [SerializeField] private Button _buttonShuffle;
        [SerializeField] private Button _buttonPickFromBottom;
        [SerializeField] private Button _buttonExtraTray;

        private PowerUpService _powerUpService;
        private TutorialInputGate _gate;
        private SignalBus _signalBus;
        private CustomerConsumeService _consumeService;

        [Inject]
        public void Construct(
            PowerUpService powerUpService,
            TutorialInputGate gate,
            SignalBus signalBus,
            [Inject(Optional = true)] CustomerConsumeService consumeService = null)
        {
            _powerUpService = powerUpService;
            _gate = gate;
            _signalBus = signalBus;
            _consumeService = consumeService;
        }

        private void Start()
        {
            if (_consumeService != null)
            {
                _consumeService.OnLevelEnded += HandleLevelEnded;
            }

            _signalBus?.Subscribe<LevelResumedSignal>(HandleLevelResumed);

            _buttonShuffle.onClick.AddListener(() =>
            {
                if (!_gate.CanClickPowerUpButton(PowerUpButtonId.Shuffle)) return;
                _powerUpService.ActivateShuffle();
                FireActionPerformed(PowerUpButtonId.Shuffle);
            });
            _buttonPickFromBottom.onClick.AddListener(() =>
            {
                if (!_gate.CanClickPowerUpButton(PowerUpButtonId.PickFromBottom)) return;
                _powerUpService.ActivatePickFromBottom();
                FireActionPerformed(PowerUpButtonId.PickFromBottom);
            });
            _buttonExtraTray.onClick.AddListener(() =>
            {
                if (!_gate.CanClickPowerUpButton(PowerUpButtonId.ExtraTray)) return;
                if (_powerUpService.ActivateExtraTray()) FireActionPerformed(PowerUpButtonId.ExtraTray);
            });
        }

        private void OnDestroy()
        {
            if (_consumeService != null)
            {
                _consumeService.OnLevelEnded -= HandleLevelEnded;
            }

            _signalBus?.Unsubscribe<LevelResumedSignal>(HandleLevelResumed);
        }

        private void HandleLevelEnded(bool won)
        {
            gameObject.SetActive(false);
        }

        private void HandleLevelResumed()
        {
            gameObject.SetActive(true);
        }

        private void FireActionPerformed(PowerUpButtonId id) =>
            _signalBus.Fire(new TutorialActionPerformedSignal { Kind = TutorialHoverTargetKind.PowerUpButton, PowerUpButton = id });
    }
}
