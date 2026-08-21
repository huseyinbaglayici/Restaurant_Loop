using UnityEngine;
using Zenject;

namespace RestaurantLoop.Runtime.Conveyor
{
    /// <summary>
    /// Pooled visual for one exit-stack slot floor. Position-only — rotation is always identity,
    /// there's nothing to rotate for a floor marker.
    /// </summary>
    public class ExitSlotController : MonoBehaviour, IPoolable<Vector3>
    {
        public void OnSpawned(Vector3 position)
        {
            transform.SetPositionAndRotation(position, Quaternion.identity);
            gameObject.SetActive(true);
        }

        public void OnDespawned()
        {
            gameObject.SetActive(false);
        }

        public class Pool : MonoPoolableMemoryPool<Vector3, ExitSlotController> { }
    }
}
