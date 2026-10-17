using UnityEngine;

namespace RestaurantLoop.Runtime.Conveyor
{
    // Keeps sprite-based food prefabs facing the camera on the belt, the rack and in flight.
    public class BillboardUnit : MonoBehaviour
    {
        private Transform _camTransform;

        private void OnEnable()
        {
            FindCamera();
        }

        private void LateUpdate()
        {
            if (_camTransform == null || !_camTransform.gameObject.activeInHierarchy)
            {
                FindCamera();
                if (_camTransform == null) return;
            }

            transform.rotation = _camTransform.rotation;
        }

        private void FindCamera()
        {
            if (Camera.main != null)
            {
                _camTransform = Camera.main.transform;
            }
        }
    }
}
