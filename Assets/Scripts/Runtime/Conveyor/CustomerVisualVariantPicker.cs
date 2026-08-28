using System.Collections.Generic;
using UnityEngine;
using Zenject;

namespace RestaurantLoop.Runtime.Conveyor
{
    // Rents one pooled character model per spawn instead of randomizing per-part material colors —
    // every customer of a given variant shares the same Material assets, so the SRP Batcher isn't
    // broken by per-instance material instances. Each variant lives in its own small Zenject pool
    // (see ConveyorInstaller) sized for how many customers are ever on screen at once, instead of
    // every occupant slot pre-instantiating all 6 variants (see PF_Customer_Occupant history).
    public class CustomerVisualVariantPicker : MonoBehaviour
    {
        [SerializeField] private CustomerPlayableAnimator _playableAnimator;

        [Inject] private DiContainer _container;
        [Inject] private IReadOnlyList<CustomerVariant> _availableVariants;

        private CustomerVisualInstance _active;
        private CustomerVariant _activeVariant;

        public void PickRandom()
        {
            if (_availableVariants == null || _availableVariants.Count == 0) return;

            ReleaseActive();

            _activeVariant = _availableVariants[Random.Range(0, _availableVariants.Count)];
            _active = ResolvePool(_activeVariant).Spawn(transform);

            if (_playableAnimator != null) _playableAnimator.SetAnimator(_active.Animator);
        }

        public void ReleaseActive()
        {
            if (_active == null) return;

            ResolvePool(_activeVariant).Despawn(_active);
            _active = null;
        }

        private CustomerVisualInstance.Pool ResolvePool(CustomerVariant variant) =>
            _container.ResolveId<CustomerVisualInstance.Pool>(variant);
    }
}
