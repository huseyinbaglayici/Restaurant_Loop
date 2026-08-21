using UnityEngine;
using Zenject;

namespace RestaurantLoop.Runtime.Conveyor
{
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

        public class Pool : MonoPoolableMemoryPool<Vector3, ExitSlotController>
        {
        }
    }
}