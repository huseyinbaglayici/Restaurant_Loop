using UnityEngine;

namespace RestaurantLoop.Runtime.Conveyor
{
    /// <summary>
    /// 2D Sprite tabanlı yemek prefablarının 3D dünyada (kemerde, rafta veya uçarken)
    /// her zaman aktif kameraya dik (billboard) bakmasını sağlar.
    /// </summary>
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
