using DG.Tweening;
using RestaurantLoop.Runtime.Signals;
using TMPro;
using UnityEngine;
using Zenject;

namespace RestaurantLoop.Runtime.Conveyor
{
    // Shows "available/total" for the tray dispenser. Polled in Update: the anchor has no change events and
    // two int compares a frame are cheap. Shakes and flashes red on TrayDispenserRejectedSignal.
    public class TrayDispenserCounterUI : MonoBehaviour
    {
        [Inject] private TrayDispenserAnchor _trayAnchor;
        [Inject] private Camera _camera;
        [Inject] private SignalBus _signalBus;
        [SerializeField] private TMP_Text _text;

        private int _lastAvailable = -1;
        private int _lastTotal = -1;
        private Color _originalColor = Color.white;
        private Vector3 _originalLocalPos;
        private bool _originalPosCaptured;
        private Tween _shakeTween;
        private Tween _colorTween;

        private void Awake()
        {
            if (_text == null) _text = GetComponentInChildren<TMP_Text>(true);
            if (_text != null)
            {
                _originalColor = _text.color;
            }
            _originalLocalPos = transform.localPosition;
            _originalPosCaptured = true;
        }

        // Only while active: a theme's own dispenser prefab hides the scene default, which must stay quiet.
        private void OnEnable()
        {
            _signalBus?.Subscribe<TrayDispenserRejectedSignal>(PlayRejectAnimation);
        }

        private void OnDisable()
        {
            _signalBus?.TryUnsubscribe<TrayDispenserRejectedSignal>(PlayRejectAnimation);
        }

        private void PlayRejectAnimation()
        {
            if (_text == null) return;
            if (!_originalPosCaptured)
            {
                _originalLocalPos = transform.localPosition;
                _originalPosCaptured = true;
            }

            _shakeTween?.Kill();
            _colorTween?.Kill();

            transform.localPosition = _originalLocalPos;
            _text.color = _originalColor;

            _shakeTween = transform.DOShakePosition(0.2f, strength: new Vector3(0.005f, 0f, 0f), vibrato: 8, randomness: 0, snapping: false, fadeOut: true)
                .OnComplete(() => transform.localPosition = _originalLocalPos);

            _text.color = Color.red;
            _colorTween = _text.DOColor(_originalColor, 0.45f).SetEase(Ease.InQuad);
        }

        private void Update()
        {
            if (_trayAnchor == null || _text == null) return;

            int available = _trayAnchor.AvailableCount;
            int total = _trayAnchor.TotalCount;
            if (available == _lastAvailable && total == _lastTotal) return;

            _lastAvailable = available;
            _lastTotal = total;
            _text.SetText("{0}/{1}", available, total);
        }

        private void LateUpdate()
        {
            var cam = _camera != null ? _camera : Camera.main;
            if (cam != null)
            {
                transform.rotation = cam.transform.rotation;
            }
        }
    }
}
