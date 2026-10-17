using UnityEngine;
using Zenject;

namespace RestaurantLoop.Runtime.Conveyor
{
    // One pooled character model of a single variant, rented per customer spawn by CustomerVisualVariantPicker.
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

        // Back under the pool's holding group, so it never rides along with the customer's next spawn.
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
