using RestaurantLoop.Runtime.Conveyor;
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

        [Inject]
        public void Construct(PowerUpService powerUpService)
        {
            _powerUpService = powerUpService;
        }

        private void Start()
        {
            _buttonShuffle.onClick.AddListener(() => _powerUpService.ActivateShuffle());
            _buttonPickFromBottom.onClick.AddListener(() => _powerUpService.ActivatePickFromBottom());
            _buttonExtraTray.onClick.AddListener(() => _powerUpService.ActivateExtraTray());
        }
    }
}
