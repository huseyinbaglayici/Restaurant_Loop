using System;
using DG.Tweening;
using RestaurantLoop.Runtime.Data;
using UnityEngine;
using Zenject;

namespace RestaurantLoop.Runtime.Conveyor
{
    // Pure visual companion to a FoodItemController: rides directly under it on the belt, then returns
    // to the dispenser's rest pose either with a shrink punch (food fully eaten) or without one (food
    // completed its lap and docked). Never touches gameplay state — driven entirely by TrayVisualService.
    public class TrayController : MonoBehaviour, IPoolable<Vector3, Quaternion>
    {
        // The dispenser's rest orientation for the level this tray was spawned into — not the prefab's
        // own authored rotation, since the dispenser (and so this pose) moves per level with the Gate cell.
        private Quaternion _uprightRotation;

        private Tween _rotateTween;
        private Tween _punchTween;

        public void OnSpawned(Vector3 anchorPosition, Quaternion anchorRotation)
        {
            _uprightRotation = anchorRotation;

            transform.SetPositionAndRotation(anchorPosition, anchorRotation);
            transform.localScale = Vector3.one;
            gameObject.SetActive(true);
        }

        public void OnDespawned()
        {
            _rotateTween?.Kill();
            _punchTween?.Kill();
            gameObject.SetActive(false);
        }

        // Flies from wherever the tray currently sits (the dispenser anchor) to the belt entry point,
        // flattening from its authored upright pose to beltRotation at the same time.
        public void EnterBelt(
            Func<Vector3> targetProvider,
            Quaternion beltRotation,
            float rotateDuration,
            ConveyorFeelSettings.FlightTuning flightTuning,
            FoodFlightService flightService,
            Action onArrive)
        {
            _rotateTween?.Kill();
            _rotateTween = transform.DORotateQuaternion(beltRotation, rotateDuration);

            flightService.Launch(transform, targetProvider, flightTuning, onArrive);
        }

        // Every tick while riding the belt — direct assignment, no tween, so it stays glued to the food.
        public void Follow(Vector3 worldPosition)
        {
            transform.position = worldPosition;
        }

        // withPunch: food was fully eaten mid-belt (shrink punch, then fly home).
        // !withPunch: food completed its lap and docked (fly home right away, no punch).
        public void ReturnToAnchor(
            Vector3 anchorPosition,
            Quaternion anchorRotation,
            float rotateDuration,
            ConveyorFeelSettings.FlightTuning flightTuning,
            FoodFlightService flightService,
            bool withPunch,
            Vector3 punchStrength,
            float punchDuration,
            int punchVibrato,
            float punchElasticity,
            Action onArrive)
        {
            if (withPunch)
            {
                _punchTween?.Kill(true);
                _punchTween = transform.DOPunchScale(punchStrength, punchDuration, punchVibrato, punchElasticity)
                    .OnComplete(() => FlyHome(anchorPosition, anchorRotation, rotateDuration, flightTuning, flightService, onArrive));
            }
            else
            {
                FlyHome(anchorPosition, anchorRotation, rotateDuration, flightTuning, flightService, onArrive);
            }
        }

        private void FlyHome(
            Vector3 anchorPosition,
            Quaternion anchorRotation,
            float rotateDuration,
            ConveyorFeelSettings.FlightTuning flightTuning,
            FoodFlightService flightService,
            Action onArrive)
        {
            _rotateTween?.Kill();
            _rotateTween = transform.DORotateQuaternion(anchorRotation, rotateDuration);

            flightService.Launch(transform, anchorPosition, flightTuning, onArrive);
        }

        // Belt-ride pose: same facing as the dispenser rest pose but leveled flat (X/Z tilt zeroed).
        public Quaternion ResolveFlatRotation() => Quaternion.Euler(0f, _uprightRotation.eulerAngles.y, 0f);

        public class Pool : MonoPoolableMemoryPool<Vector3, Quaternion, TrayController>
        {
        }
    }
}
