using RestaurantLoop.Signals;
using UnityEngine;
using Zenject;

namespace RestaurantLoop.UI
{
    public class PlayButtonController : MonoBehaviour
    {
        [SerializeField] private UnityEngine.UI.Button _button;

        private SignalBus _signalBus;

        [Inject]
        public void Construct(SignalBus signalBus)
        {
            _signalBus = signalBus;
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
            _signalBus.Fire(new PlayRequestedSignal());
        }
    }
}
