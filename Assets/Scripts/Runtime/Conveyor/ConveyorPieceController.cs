using UnityEngine;
using Zenject;

namespace RestaurantLoop.Runtime.Conveyor
{
    public class ConveyorPieceController : MonoBehaviour, IPoolable<Vector3, float>
    {
        private float _baseTiltX;
        private float _baseTiltZ;
        private float _axisSign;

        private IConveyorPieceVisual[] _visuals;

        private void Awake()
        {
            var e = transform.localRotation.eulerAngles;
            _baseTiltX = e.x;
            _baseTiltZ = e.z;

            _visuals = GetComponentsInChildren<IConveyorPieceVisual>(true);
        }

        public void OnSpawned(Vector3 position, float worldYRotation)
        {
            transform.SetPositionAndRotation(position, Quaternion.Euler(_baseTiltX, worldYRotation, _baseTiltZ));
            // axis flip at 90/270
            _axisSign = Mathf.Cos(2f * worldYRotation * Mathf.Deg2Rad);

            for (int i = 0; i < _visuals.Length; i++)
            {
                _visuals[i].OnPieceSpawned(_axisSign);
            }

            gameObject.SetActive(true);
        }

        public void OnDespawned()
        {
            gameObject.SetActive(false);
        }

        public class Pool : MonoPoolableMemoryPool<Vector3, float, ConveyorPieceController>
        {
        }
    }
}
