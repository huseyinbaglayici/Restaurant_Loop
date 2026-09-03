using UnityEngine;
using Zenject;

namespace RestaurantLoop.Runtime.Conveyor
{
    public class ExitSlotController : MonoBehaviour, IPoolable<Vector3>
    {
        private GameObject _activeThemeVisual;
        private GameObject _baseVisualChild;

        private void Awake()
        {
            if (transform.childCount > 0)
            {
                _baseVisualChild = transform.GetChild(0).gameObject;
            }
        }

        public void ApplyThemeVisual(GameObject overridePrefab)
        {
            if (overridePrefab != null)
            {
                if (_baseVisualChild != null) _baseVisualChild.SetActive(false);
                if (_activeThemeVisual == null || _activeThemeVisual.name != overridePrefab.name)
                {
                    if (_activeThemeVisual != null) Destroy(_activeThemeVisual);
                    _activeThemeVisual = Instantiate(overridePrefab, transform);
                    _activeThemeVisual.name = overridePrefab.name;
                    _activeThemeVisual.transform.localPosition = Vector3.zero;
                    _activeThemeVisual.transform.localRotation = Quaternion.identity;
                    _activeThemeVisual.transform.localScale = Vector3.one;
                }
                _activeThemeVisual.SetActive(true);
            }
            else
            {
                if (_activeThemeVisual != null) _activeThemeVisual.SetActive(false);
                if (_baseVisualChild != null) _baseVisualChild.SetActive(true);
            }
        }

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