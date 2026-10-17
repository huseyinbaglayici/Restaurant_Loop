using DG.Tweening;
using RestaurantLoop.Runtime.Data;
using UnityEngine;
using Zenject;

namespace RestaurantLoop.Runtime.Conveyor
{
    // Frames the camera for each level (CameraLevelFraming), then plays the runtime camera moves:
    // impact shake and the pick-from-bottom queue focus, both returning to the framed rest pose.
    public class CameraController : IInitializable
    {
        // Layouts are authored for 9:16 portrait; narrower (taller) screens widen the ortho size so the
        // board keeps its full width and the extra height goes behind the HUD bars.
        public const float ReferenceAspect = 9f / 16f;

        private readonly ConveyorTuning _tuning;
        private readonly Camera _camera;
        private readonly CameraLevelFraming _framing;
        private Vector3 _defaultPosition;
        private float _defaultOrthoSize;
        private Tween _moveTween;
        private Tween _orthoTween;
        private bool _isFocusedOnQueue;

        public Camera Camera => _camera;
        public float CameraZoomFactor => _framing.ZoomFactor;
        public float HeightZoomFactor => _framing.HeightZoomFactor;

        public static float AspectWidthFactor(Camera camera) =>
            camera != null && camera.aspect > 0f && camera.aspect < ReferenceAspect ? ReferenceAspect / camera.aspect : 1f;

        public CameraController(
            ConveyorLevelDataSO level,
            ConveyorTuning tuning,
            QueueAnchor queueAnchor,
            Camera camera,
            ThemeSettings themeSettings,
            ConveyorPrefabSettings prefabSettings)
        {
            _tuning = tuning;
            _camera = camera;
            // Captures the scene camera's authored pose now, before any level framing moves it.
            _framing = new CameraLevelFraming(level, tuning, queueAnchor, camera, themeSettings, prefabSettings);
        }

        public void Initialize()
        {
            _framing.Apply();

            _defaultPosition = _camera.transform.position;
            _defaultOrthoSize = _camera.orthographicSize;
        }

        // Short positional shake for impacts (power-ups); always settles back on the resting position.
        public void Shake(float strength, float duration)
        {
            if (_camera == null) return;
            var cameraTransform = _camera.transform;
            cameraTransform.DOKill(true);
            var rest = cameraTransform.position;
            cameraTransform.DOShakePosition(duration, new Vector3(strength, strength, 0f), 18, 90f, false, true)
                .SetTarget(cameraTransform)
                .OnKill(() => { if (cameraTransform != null) cameraTransform.position = rest; });
        }

        public void FocusOnQueue(bool enabled)
        {
            if (_isFocusedOnQueue == enabled || _camera == null) return;
            _isFocusedOnQueue = enabled;

            _camera.transform.DOKill(true);
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
