using UnityEngine;
using Zenject;

namespace RestaurantLoop.Runtime.Conveyor
{
    public class ExitSlotController : MonoBehaviour, IPoolable<Vector3>
    {
        private GameObject _activeThemeVisual;
        private readonly System.Collections.Generic.List<GameObject> _originalChildren = new();
        private Renderer[] _originalRenderers;

        private void Awake()
        {
            _originalRenderers = GetComponents<Renderer>();
            for (int i = 0; i < transform.childCount; i++)
            {
                _originalChildren.Add(transform.GetChild(i).gameObject);
            }
        }

        public void ApplyThemeVisual(GameObject overridePrefab)
        {
            if (overridePrefab != null)
            {
                if (_originalRenderers != null)
                {
                    foreach (var r in _originalRenderers) r.enabled = false;
                }

                foreach (var child in _originalChildren)
                {
                    if (child != null) child.SetActive(false);
                }

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

                if (_originalRenderers != null)
                {
                    foreach (var r in _originalRenderers) r.enabled = true;
                }

                foreach (var child in _originalChildren)
                {
                    if (child != null) child.SetActive(true);
                }
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