using System.Collections.Generic;
using UnityEngine;

namespace RestaurantLoop.Runtime.Conveyor
{
    // Swaps a pooled piece's own renderers and children for an instance of a theme prefab, or restores
    // them when the theme has none. Pooled pieces outlive the scene, so it is applied on every spawn.
    public class ThemeVisualOverride
    {
        private readonly Transform _owner;
        private readonly Renderer[] _originalRenderers;
        private readonly List<GameObject> _originalChildren = new();
        private GameObject _activeThemeVisual;

        public ThemeVisualOverride(Transform owner)
        {
            _owner = owner;
            _originalRenderers = owner.GetComponents<Renderer>();
            for (int i = 0; i < owner.childCount; i++)
            {
                _originalChildren.Add(owner.GetChild(i).gameObject);
            }
        }

        public void Apply(GameObject overridePrefab)
        {
            bool useOverride = overridePrefab != null;

            foreach (var r in _originalRenderers) r.enabled = !useOverride;

            foreach (var child in _originalChildren)
            {
                if (child != null) child.SetActive(!useOverride);
            }

            if (!useOverride)
            {
                if (_activeThemeVisual != null) _activeThemeVisual.SetActive(false);
                return;
            }

            if (_activeThemeVisual == null || _activeThemeVisual.name != overridePrefab.name)
            {
                if (_activeThemeVisual != null) Object.Destroy(_activeThemeVisual);
                _activeThemeVisual = Object.Instantiate(overridePrefab, _owner);
                _activeThemeVisual.name = overridePrefab.name;
                _activeThemeVisual.transform.localPosition = Vector3.zero;
                _activeThemeVisual.transform.localRotation = Quaternion.identity;
                _activeThemeVisual.transform.localScale = Vector3.one;
            }
            _activeThemeVisual.SetActive(true);
        }
    }
}
