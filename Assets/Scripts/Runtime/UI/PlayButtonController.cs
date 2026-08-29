using RestaurantLoop.Lives;
using RestaurantLoop.Signals;
using UnityEngine;
using Zenject;

namespace RestaurantLoop.UI
{
    public class PlayButtonController : MonoBehaviour
    {
        [SerializeField] private UnityEngine.UI.Button _button;

        private SignalBus _signalBus;
        private ILivesService _livesService;

        [Inject]
        public void Construct(SignalBus signalBus, [Inject(Optional = true)] ILivesService livesService = null)
        {
            _signalBus = signalBus;
            _livesService = livesService;
        }

        private void Start()
        {
            if (_button != null) _button.onClick.AddListener(OnPlayClicked);
        }

        private void OnDestroy()
        {
            if (_button != null) _button.onClick.RemoveListener(OnPlayClicked);
        }

        private void OnPlayClicked()
        {
            if (_livesService != null && !_livesService.HasLives)
            {
                Debug.LogWarning("[PlayButtonController] Cannot start level — no lives remaining!");
                return;
            }

            _signalBus.Fire(new PlayRequestedSignal());
        }
    }
}
