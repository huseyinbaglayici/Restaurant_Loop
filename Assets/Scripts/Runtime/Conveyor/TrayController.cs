using System;
using DG.Tweening;
using RestaurantLoop.Runtime.Data;
using UnityEngine;

namespace RestaurantLoop.Runtime.Conveyor
{
    // Pure visual companion to a FoodItemController: one of the 5 trays already sitting in
    // tepsi_dagitici's stack. Leaves its authored resting pose, flies to the belt entry turning flat as it
    // goes, then TrayVisualService.Tick() drives its position directly (SetRidePosition) for the rest of
    // the ride — never reparented to the food. Rotation is set once (flat) and never touched again: with
    // no parent-child relationship to the food, there is nothing of the food's own rotation (its flight's
    // rotation reset, belt spin, a consume punch) for the tray to ever inherit, on any frame, including the
    // one where the food gets eaten before this tray has even landed. Returns to a slot pose handed to it
    // by TrayDispenserAnchor — shrinking away where it stands and popping back in at the dispenser rather
    // than flying the whole way back. Never touches gameplay state.
    public class TrayController : MonoBehaviour
    {
        // Captured once at Awake, before TrayDispenserAnchor ever repositions the dispenser root — local
        // space, so it stays correct even though the root itself moves to a different Gate cell per level.
        // The tray's parent never changes after this (unlike the old attach-to-food design), so these stay
        // valid as "home" for the tray's whole lifetime, not just at boot.
        private Vector3 _initialLocalPosition;
        private Quaternion _initialLocalRotation;
        private Vector3 _initialLocalScale;
        private bool _initialPoseCaptured;

        private Tween _rotateTween;
        private Tween _scaleTween;

        public Vector3 InitialLocalPosition => _initialLocalPosition;
        public Quaternion InitialLocalRotation => _initialLocalRotation;
        public Vector3 InitialLocalScale => _initialLocalScale;

        private void Awake() => CaptureInitialPoseOnce();

        private void CaptureInitialPoseOnce()
        {
            if (_initialPoseCaptured) return;

            _initialPoseCaptured = true;
            _initialLocalPosition = transform.localPosition;
            _initialLocalRotation = transform.localRotation;
            _initialLocalScale = transform.localScale;
        }

        // Flies from the resting pose to the belt entry point, turning flat alongside it. Rotation is
        // animated in LOCAL space — the authored resting rotation's Y goes from -90 to 0, X/Z stay exactly
        // as authored — which reads correctly regardless of which way the dispenser is currently facing,
        // since the parent (and therefore this local -> world mapping) never changes again after this.
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

        // Called every frame by TrayVisualService while this tray is riding — the one and only thing that
        // moves it once EnterBelt's flight has landed. Direct set, no tween, no flight: it goes exactly
        // where the belt says the food is, because it's driven from the same Progress value the food's own
        // visual is, not from chasing the food's transform.
        public void SetRidePosition(Vector3 worldPosition) => transform.position = worldPosition;

        // Hard reset, no animation — used when the belt is cleared (level restart/teardown) so no tray
        // is ever left stranded mid-flight or mid-belt.
        public void SnapToInitialPose()
        {
            _rotateTween?.Kill();
            _scaleTween?.Kill();

            transform.localPosition = _initialLocalPosition;
            transform.localRotation = _initialLocalRotation;
            transform.localScale = _initialLocalScale;
        }

        // Belt-ride pose, in LOCAL space: same authored X/Z as the resting pose, Y zeroed (resting Y is
        // -90 authored in the prefab; on the belt it reads as 0).
        public Quaternion ResolveFlatLocalRotation()
        {
            var e = _initialLocalRotation.eulerAngles;
            return Quaternion.Euler(e.x, 0f, e.z);
        }

        // withPunch: food was fully eaten mid-belt (snappier vanish/reappear).
        // !withPunch: food completed its lap and docked (calmer vanish/reappear).
        // Either way: no flight back across the belt — it shrinks away where it stands, teleports to the
        // resting pose, then grows back in.
        public void ReturnToInitialPose(
            Vector3 targetLocalPosition,
            Quaternion targetLocalRotation,
            Vector3 targetLocalScale,
            bool withPunch,
            float vanishDuration,
            float punchOvershoot,
            Action onArrive)
        {
            _rotateTween?.Kill();
            _scaleTween?.Kill();

            _scaleTween = (withPunch
                    ? transform.DOScale(Vector3.zero, vanishDuration).SetEase(Ease.InBack, punchOvershoot)
                    : transform.DOScale(Vector3.zero, vanishDuration).SetEase(Ease.InQuad))
                .OnComplete(() =>
                {
                    transform.localPosition = targetLocalPosition;
                    transform.localRotation = targetLocalRotation;

                    _scaleTween = (withPunch
                            ? transform.DOScale(targetLocalScale, vanishDuration).SetEase(Ease.OutBack, punchOvershoot)
                            : transform.DOScale(targetLocalScale, vanishDuration).SetEase(Ease.OutQuad))
                        .OnComplete(() => onArrive?.Invoke());
                });
        }
    }
}
