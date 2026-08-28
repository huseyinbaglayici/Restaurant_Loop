using System;
using DG.Tweening;
using UnityEngine;
using Zenject;

namespace RestaurantLoop.Runtime.Conveyor
{
    // Order-widget lifecycle lives in CustomerSpawner, not here — this keeps CustomerController free
    // of scene-scoped dependencies (CustomerOrderWidget.Pool needs GameplayScene's own Canvas refs),
    // which is what lets CustomerController.Pool itself live in ProjectContext/DDOL and survive scene
    // reloads instead of being torn down and reprewarmed every level.
    public class CustomerController : MonoBehaviour, IPoolable<Vector3, float>
    {
        // World-space handoff point the consumed FoodItemController flies to (ConveyorVisualService).
        // Should sit on the character's hand bone, not the UI canvas.
        [SerializeField] private Transform _foodHandoffAnchor;

        [SerializeField] private CustomerPlayableAnimator _playableAnimator;
        [SerializeField] private CustomerVisualVariantPicker _visualVariantPicker;

        private Tween _consumePunchTween;

        public Transform FoodHandoffAnchor => _foodHandoffAnchor;

        public void OnSpawned(Vector3 position, float worldYRotation)
        {
            transform.SetPositionAndRotation(position, Quaternion.Euler(0f, worldYRotation, 0f));
            gameObject.SetActive(true);

            _visualVariantPicker?.PickRandom();
            if (_playableAnimator != null) _playableAnimator.PlayIdleWithRandomOffset();
        }

        public void OnDespawned()
        {
            _consumePunchTween?.Kill();
            _visualVariantPicker?.ReleaseActive();
            gameObject.SetActive(false);
        }

        // Very small scale punch on the model root each time a food unit lands on this customer.
        public void PlayConsumePunch(Vector3 strength, float duration, int vibrato, float elasticity)
        {
            if (_playableAnimator == null) return;

            var target = _playableAnimator.transform;
            _consumePunchTween?.Kill(true);
            _consumePunchTween = target.DOPunchScale(strength, duration, vibrato, elasticity);
        }

        // hasSecondOrderRemaining == true: plays the "first order served" reaction, then returns to Idle.
        // hasSecondOrderRemaining == false: plays a happy reaction, then invokes onComplete (caller despawns).
        public void PlayServeReaction(bool hasSecondOrderRemaining, Action onComplete)
        {
            if (_playableAnimator == null)
            {
                onComplete?.Invoke();
                return;
            }

            _playableAnimator.PlayServeReaction(hasSecondOrderRemaining, onComplete);
        }

        public class Pool : MonoPoolableMemoryPool<Vector3, float, CustomerController>
        {
        }
    }
}
