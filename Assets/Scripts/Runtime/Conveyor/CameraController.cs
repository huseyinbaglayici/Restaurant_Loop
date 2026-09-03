using DG.Tweening;
using RestaurantLoop.Runtime.Data;
using UnityEngine;
using Zenject;

namespace RestaurantLoop.Runtime.Conveyor
{
    // Keeps the scene's current camera offset (X/Z/Y delta from the queue center) as a fixed
    // baseline, then re-centers the camera on the queue's lane center for whatever level loads —
    // rotation is left untouched.
    public class CameraController : IInitializable
    {
        private readonly ConveyorLevelDataSO _level;
        private readonly ConveyorTuning _tuning;
        private readonly QueueAnchor _queueAnchor;
        private readonly Camera _camera;
        private readonly ThemeSettings _themeSettings;
        public Camera Camera => _camera;
        private readonly Vector3 _offsetFromQueueCenter;
        private readonly float _initialOrthoSize;
        private Vector3 _defaultPosition;
        private float _defaultOrthoSize;
        private Tween _moveTween;
        private Tween _orthoTween;
        private bool _isFocusedOnQueue;

        public float CameraZoomFactor { get; private set; } = 1f;
        public float HeightZoomFactor { get; private set; } = 1f;

        public CameraController(
            ConveyorLevelDataSO level,
            ConveyorTuning tuning,
            QueueAnchor queueAnchor,
            Camera camera,
            [Inject(Optional = true)] ThemeSettings themeSettings = null)
        {
            _level = level;
            _tuning = tuning;
            _queueAnchor = queueAnchor;
            _camera = camera;
            _themeSettings = themeSettings ?? Resources.Load<ThemeSettings>("ThemeSettings");
            _initialOrthoSize = camera.orthographicSize;
            _offsetFromQueueCenter = camera.transform.position - ComputeQueueCenter();
        }

        private Vector3 ComputeQueueCenter()
        {
            var lanePositions = _queueAnchor.LanePositions;
            if (lanePositions.Length == 0) return Vector3.zero;

            var sum = Vector3.zero;
            foreach (var position in lanePositions) sum += position;
            return sum / lanePositions.Length;
        }

        public void Initialize()
        {
            if (_tuning.EnableDynamicCameraScale && _camera.orthographic && _tuning.ReferenceGridWidth > 0 && _tuning.ReferenceGridHeight > 0)
            {
                float widthRatio = (float)_level.GridWidth / _tuning.ReferenceGridWidth;
                float heightRatio = (float)_level.GridHeight / _tuning.ReferenceGridHeight;
                float maxRatio = Mathf.Max(widthRatio, heightRatio);

                float sensitivity = _tuning.CameraZoomSensitivity > 0f ? _tuning.CameraZoomSensitivity : 0.85f;

                if (maxRatio > 1f)
                {
                    float scaleFactor = 1f + (maxRatio - 1f) * sensitivity;
                    _camera.orthographicSize = _initialOrthoSize * scaleFactor;
                    CameraZoomFactor = scaleFactor;
                }

                if (heightRatio > 1f)
                {
                    HeightZoomFactor = 1f + (heightRatio - 1f) * sensitivity;
                }
            }

            var queueCenter = ComputeQueueCenter();
            var targetPos = queueCenter + _offsetFromQueueCenter;
            targetPos.x = queueCenter.x;

            // Apply camera Y and Z zoom shift if camera has zoomed out
            if (CameraZoomFactor > 1f)
            {
                float zoomDiff = CameraZoomFactor - 1f;
                var theme = _themeSettings != null ? _themeSettings.GetTheme(_level.Theme) : null;

                float yMult = theme != null && theme.CameraZoomYShiftMultiplier != 0f
                    ? theme.CameraZoomYShiftMultiplier
                    : _tuning.CameraZoomYShiftMultiplier;

                float zMult = theme != null && theme.CameraZoomZShiftMultiplier != 0f
                    ? theme.CameraZoomZShiftMultiplier
                    : _tuning.CameraZoomZShiftMultiplier;

                targetPos.y += zoomDiff * yMult;
                targetPos.z += zoomDiff * zMult;
            }

            _camera.transform.position = targetPos;

            _defaultPosition = targetPos;
            _defaultOrthoSize = _camera.orthographicSize;
        }

        public void FocusOnQueue(bool enabled)
        {
            if (_isFocusedOnQueue == enabled || _camera == null) return;
            _isFocusedOnQueue = enabled;

            _moveTween?.Kill();
            _orthoTween?.Kill();

            float duration = _tuning != null && _tuning.CameraPickFromBottomDuration > 0f
                ? _tuning.CameraPickFromBottomDuration
                : 0.35f;

            if (enabled)
            {
                Vector3 shift = _tuning != null ? _tuning.CameraPickFromBottomOffset : new Vector3(0f, -0.6f, -1.8f);
                Vector3 targetPos = _defaultPosition + shift;

                _moveTween = _camera.transform.DOMove(targetPos, duration).SetEase(Ease.OutCubic);

                if (_camera.orthographic)
                {
                    float multiplier = _tuning != null && _tuning.CameraPickFromBottomOrthoMultiplier > 0f
                        ? _tuning.CameraPickFromBottomOrthoMultiplier
                        : 0.95f;
                    _orthoTween = DOTween.To(() => _camera.orthographicSize, x => _camera.orthographicSize = x, _defaultOrthoSize * multiplier, duration).SetEase(Ease.OutCubic);
                }
            }
            else
            {
                _moveTween = _camera.transform.DOMove(_defaultPosition, duration).SetEase(Ease.OutCubic);

                if (_camera.orthographic)
                {
                    _orthoTween = DOTween.To(() => _camera.orthographicSize, x => _camera.orthographicSize = x, _defaultOrthoSize, duration).SetEase(Ease.OutCubic);
                }
            }
        }
    }
}
