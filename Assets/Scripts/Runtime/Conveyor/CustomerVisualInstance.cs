using UnityEngine;
using Zenject;

namespace RestaurantLoop.Runtime.Conveyor
{
    // One pooled character model for a single variant. CustomerVisualVariantPicker rents one of
    // these per customer spawn instead of every occupant pre-instantiating all 6 variants (was 6x
    // skinned mesh/skeleton resident per pooled customer slot, only 1 ever visible).
    public class CustomerVisualInstance : MonoBehaviour, IPoolable<Transform>
    {
        [SerializeField] private Animator _animator;

        private Transform _homeParent;

        public Animator Animator => _animator;

        private void Awake()
        {
            _homeParent = transform.parent;
        }

        public void OnSpawned(Transform parent)
        {
            transform.SetParent(parent, false);
            gameObject.SetActive(true);
        }

        // Returned to the pool's own holding group, not left parented under whichever customer
        // last used it — that customer instance gets reused by the pool too, and a stale child
        // here would silently ride along with the next unrelated spawn.
        public void OnDespawned()
        {
            gameObject.SetActive(false);
            transform.SetParent(_homeParent, false);
        }

        public class Pool : MonoPoolableMemoryPool<Transform, CustomerVisualInstance>
        {
        }
    }
}
