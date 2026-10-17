using RestaurantLoop.Runtime.Lives;
using RestaurantLoop.Runtime.Signals;
using UnityEngine;
using Zenject;

namespace RestaurantLoop.Runtime.UI
{
    public class PlayButtonController : MonoBehaviour
    {
        [SerializeField] private UnityEngine.UI.Button _button;

        private SignalBus _signalBus;
        private LivesService _livesService;

        [Inject]
        public void Construct(SignalBus signalBus, LivesService livesService)
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
