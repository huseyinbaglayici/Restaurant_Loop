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
        public Camera Camera => _camera;
        private readonly Vector3 _offsetFromQueueCenter;
        private readonly float _initialOrthoSize;
        private Vector3 _defaultPosition;
        private float _defaultOrthoSize;
        private Tween _moveTween;
        private Tween _orthoTween;
        private bool _isFocusedOnQueue;

        public float CameraZoomFactor { get; private set; } = 1f;

        public CameraController(ConveyorLevelDataSO level, ConveyorTuning tuning, QueueAnchor queueAnchor, Camera camera)
        {
            _level = level;
            _tuning = tuning;
            _queueAnchor = queueAnchor;
            _camera = camera;
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
            if (_tuning.EnableDynamicCameraScale && _camera.orthographic && _tuning.ReferenceGridHeight > 0)
            {
                int maxDimension = Mathf.Max(_level.GridWidth, _level.GridHeight);
                int refDimension = Mathf.Max(_tuning.ReferenceGridWidth, _tuning.ReferenceGridHeight);
                if (maxDimension > refDimension)
                {
                    float rawRatio = (float)maxDimension / refDimension;
                    float sensitivity = _tuning.CameraZoomSensitivity > 0f ? _tuning.CameraZoomSensitivity : 0.85f;
                    float scaleFactor = 1f + (rawRatio - 1f) * sensitivity;
                    _camera.orthographicSize = _initialOrthoSize * scaleFactor;
                    CameraZoomFactor = scaleFactor;
                }
            }

            var queueCenter = ComputeQueueCenter();
            // Camera position: keep X aligned with queue/world origin, adjust Y/Z with offset
            var targetPos = queueCenter + _offsetFromQueueCenter;
            targetPos.x = queueCenter.x;
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
