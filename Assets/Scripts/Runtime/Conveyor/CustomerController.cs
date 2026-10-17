using System;
using UnityEngine;
using Zenject;

namespace RestaurantLoop.Runtime.Conveyor
{
    // Order widgets are handled by CustomerSpawner, so this has no scene-scoped dependencies and its
    // pool can live in ProjectContext across level reloads.
    public class CustomerController : MonoBehaviour, IPoolable<Vector3, float>
    {
        // Where consumed food flies to; should sit on the character's hand bone.
        [SerializeField] private Transform _foodHandoffAnchor;

        [SerializeField] private VATCustomerVisual _playableAnimator;
        [SerializeField] private CustomerVisualVariantPicker _visualVariantPicker;

        public Transform FoodHandoffAnchor => _foodHandoffAnchor;

        public void OnSpawned(Vector3 position, float worldYRotation)
        {
            transform.SetPositionAndRotation(position, Quaternion.Euler(0f, worldYRotation, 0f));
            gameObject.SetActive(true);

            // VATCustomerVisual randomizes its idle phase in its own OnEnable.
            _visualVariantPicker?.PickRandom();
        }

        public void OnDespawned()
        {
            _visualVariantPicker?.ReleaseActive();
            gameObject.SetActive(false);
        }

        // Small scale punch each time a food unit lands on this customer.
        public void PlayConsumePunch(Vector3 strength, float duration, int vibrato, float elasticity)
        {
            if (_playableAnimator == null) return;
            _playableAnimator.PlayConsumePunch(strength, duration, vibrato, elasticity);
        }

        // With a second order left the customer just reacts; otherwise onComplete fires (caller despawns).
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
