using System.Collections.Generic;
using UnityEngine;
using Zenject;

namespace RestaurantLoop.Runtime.Conveyor
{
    public class ConveyorPieceController : MonoBehaviour, IPoolable<Vector3, float>
    {
        private float _baseTiltX;
        private float _baseEulerY;
        private float _baseTiltZ;
        private float _axisSign;

        private IConveyorPieceVisual[] _visuals;

        private GameObject _activeThemeVisual;
        private readonly List<GameObject> _originalChildren = new();
        private Renderer[] _originalRenderers;

        private void Awake()
        {
            var e = transform.localRotation.eulerAngles;
            _baseTiltX = e.x;
            _baseEulerY = e.y;
            _baseTiltZ = e.z;

            _visuals = GetComponentsInChildren<IConveyorPieceVisual>(true);
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
                // Disable base mesh renderer on self if any
                if (_originalRenderers != null)
                {
                    foreach (var r in _originalRenderers) r.enabled = false;
                }

                // Disable all original child visuals of the base conveyor piece
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

        public void OnSpawned(Vector3 position, float worldYRotation)
        {
            transform.SetPositionAndRotation(position, Quaternion.Euler(_baseTiltX, _baseEulerY + worldYRotation, _baseTiltZ));
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
