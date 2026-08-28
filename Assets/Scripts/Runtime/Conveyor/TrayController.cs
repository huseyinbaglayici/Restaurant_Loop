using System;
using DG.Tweening;
using RestaurantLoop.Runtime.Data;
using UnityEngine;

namespace RestaurantLoop.Runtime.Conveyor
{
    // Pure visual companion to a FoodItemController: one of the 5 trays already sitting in
    // tepsi_dagitici's stack. Leaves its authored resting pose, flies to the belt entry, then becomes a
    // child of the food it's carrying for the rest of the ride (AttachToRider) — the food's transform is
    // the only thing moving it from then on. Returns to a slot pose handed to it by TrayDispenserAnchor —
    // shrinking away where it stands and popping back in at the dispenser rather than flying the whole
    // way back. Never touches gameplay state — driven entirely by TrayVisualService via
    // TrayDispenserAnchor's acquire/reserve.
    public class TrayController : MonoBehaviour
    {
        // Captured once at Awake, before TrayDispenserAnchor ever repositions the dispenser root — local
        // space, so it stays correct even though the root itself moves to a different Gate cell per level.
        private Vector3 _initialLocalPosition;
        private Quaternion _initialLocalRotation;
        private Vector3 _initialLocalScale;
        private bool _initialPoseCaptured;

        // The dispenser slot this tray lives under when it isn't riding a food item. Captured once so
        // AttachToRider/DetachFromRider can move it in and out of the belt-item hierarchy and always know
        // where "home" is.
        private Transform _homeParent;

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
            _homeParent = transform.parent;
            _initialLocalPosition = transform.localPosition;
            _initialLocalRotation = transform.localRotation;
            _initialLocalScale = transform.localScale;
        }

        // Makes this tray a child of the food it's carrying, at the belt-ride height directly beneath it.
        // From this point on the tray needs no per-frame position code at all — it moves exactly when and
        // however the food moves, because it IS part of the food's hierarchy now. This is what replaces
        // chasing the food's position tick-by-tick: a whole class of "the two drifted apart" bugs becomes
        // structurally impossible instead of something to keep re-synchronizing.
        public void AttachToRider(Transform rider, float worldHeight)
        {
            // The rotate tween's values are local to the OLD parent (the dispenser root); if it's still
            // running when this reparents, the same numbers land on a completely different world rotation
            // under the food (whose own authored rotation isn't the dispenser root's). Kill it and set the
            // flat rotation explicitly, in world space, computed off the still-stable dispenser root — the
            // one reference point that hasn't changed just because the tray's parent has.
            _rotateTween?.Kill();
            var flatWorldRotation = _homeParent.rotation * ResolveFlatLocalRotation();

            transform.SetParent(rider, worldPositionStays: true);

            var position = rider.position;
            position.y = worldHeight;
            transform.position = position;
            transform.rotation = flatWorldRotation;
        }

        // Returns this tray to its dispenser slot's hierarchy. No-op if it's already there (never rode,
        // or was already detached) — safe to call unconditionally before any pose reset.
        public void DetachFromRider()
        {
            if (transform.parent == _homeParent) return;
            transform.SetParent(_homeParent, worldPositionStays: true);
        }

        // Flies from the resting pose to the belt entry point. Rotation is animated in LOCAL space — the
        // authored resting rotation's Y goes from -90 to 0, X/Z stay exactly as authored — so this only
        // ever reads as "the tray turns flat", regardless of which way the dispenser itself is currently
        // facing (that facing lives entirely in the parent's own rotation).
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

        // Hard reset, no animation — used when the belt is cleared (level restart/teardown) so no tray
        // is ever left stranded mid-flight or mid-belt.
        public void SnapToInitialPose()
        {
            _rotateTween?.Kill();
            _scaleTween?.Kill();
            DetachFromRider();

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
            DetachFromRider();

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
