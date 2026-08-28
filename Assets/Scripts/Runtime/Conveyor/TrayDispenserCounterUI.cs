using DG.Tweening;
using TMPro;
using UnityEngine;
using Zenject;

namespace RestaurantLoop.Runtime.Conveyor
{
    // Shows "available/total" for tepsi_dagitici's own tray pool (TrayDispenserAnchor). Plain Update
    // poll instead of a signal/event — TrayDispenserAnchor has no change notifications, and this is just
    // two int comparisons a frame, cheap enough not to need one. Update() only starts running after
    // Zenject's SceneContext finishes injection/Initialize (it holds scene objects inactive until then),
    // so the very first frame already reads the post-reset pool state (5/5).
    public class TrayDispenserCounterUI : MonoBehaviour
    {
        [Inject] private TrayDispenserAnchor _trayAnchor;
        [Inject(Optional = true)] private Camera _camera;
        [SerializeField] private TMP_Text _text;

        private int _lastAvailable = -1;
        private int _lastTotal = -1;
        private Color _originalColor = Color.white;
        private Vector3 _originalLocalPos;
        private bool _originalPosCaptured;
        private DG.Tweening.Tween _shakeTween;
        private DG.Tweening.Tween _colorTween;

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

        public void PlayRejectAnimation()
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

            // Shake position left-right (very subtle feedback)
            _shakeTween = transform.DOShakePosition(0.2f, strength: new Vector3(0.005f, 0f, 0f), vibrato: 8, randomness: 0, snapping: false, fadeOut: true)
                .OnComplete(() => transform.localPosition = _originalLocalPos);

            // Flash red and return to original color
            _text.color = Color.red;
            _colorTween = _text.DOColor(_originalColor, 0.45f).SetEase(DG.Tweening.Ease.InQuad);
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
