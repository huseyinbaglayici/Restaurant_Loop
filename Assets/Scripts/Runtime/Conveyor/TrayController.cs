using System;
using DG.Tweening;
using RestaurantLoop.Runtime.Data;
using UnityEngine;

namespace RestaurantLoop.Runtime.Conveyor
{
    public class TrayController : MonoBehaviour
    {
        private Vector3 _initialLocalPosition;
        private Quaternion _initialLocalRotation;
        private Vector3 _initialLocalScale;
        private bool _initialPoseCaptured;

        private Tween _rotateTween;

        public Vector3 InitialLocalPosition
        {
            get
            {
                CaptureInitialPoseOnce();
                return _initialLocalPosition;
            }
        }

        public Quaternion InitialLocalRotation
        {
            get
            {
                CaptureInitialPoseOnce();
                return _initialLocalRotation;
            }
        }

        public Vector3 InitialLocalScale
        {
            get
            {
                CaptureInitialPoseOnce();
                return _initialLocalScale;
            }
        }

        private void Awake() => CaptureInitialPoseOnce();

        public void CaptureInitialPoseOnce()
        {
            if (_initialPoseCaptured) return;

            _initialPoseCaptured = true;
            _initialLocalPosition = transform.localPosition;
            _initialLocalRotation = transform.localRotation;
            _initialLocalScale = transform.localScale;
        }

        public void EnterBelt(
            Func<Vector3> targetProvider,
            Quaternion beltLocalRotation,
            float rotateDuration,
            ConveyorFeelSettings.FlightTuning flightTuning,
            FoodFlightService flightService,
            Action onArrive)
        {
            _rotateTween?.Kill();
            _rotateTween = transform.DOLocalRotateQuaternion(beltLocalRotation, rotateDuration);

            flightService.Launch(transform, targetProvider, flightTuning, onArrive);
        }

        public void SetRidePosition(Vector3 worldPosition) => transform.position = worldPosition;

        public void SnapToInitialPose()
        {
            _rotateTween?.Kill();

            transform.localPosition = _initialLocalPosition;
            transform.localRotation = _initialLocalRotation;
            transform.localScale = _initialLocalScale;
        }

        public Quaternion ResolveFlatLocalRotation()
        {
            var e = _initialLocalRotation.eulerAngles;
            return Quaternion.Euler(e.x, 0f, e.z);
        }

        public void ReturnToInitialPose(
            Vector3 targetLocalPosition,
            Quaternion targetLocalRotation,
            Vector3 targetLocalScale,
            ConveyorFeelSettings.FlightTuning flightTuning,
            FoodFlightService flightService,
            Action onArrive)
        {
            _rotateTween?.Kill();
            transform.localScale = targetLocalScale;

            var targetWorldPosition = transform.parent != null
                ? transform.parent.TransformPoint(targetLocalPosition)
                : targetLocalPosition;

            float flightDuration =
                flightTuning.ResolveDuration(Vector3.Distance(transform.position, targetWorldPosition));
            _rotateTween = transform.DOLocalRotateQuaternion(targetLocalRotation, flightDuration);

            flightService.Launch(transform, targetWorldPosition, flightTuning, onArrive);
        }
    }
}