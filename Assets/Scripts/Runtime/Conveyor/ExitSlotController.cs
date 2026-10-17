using UnityEngine;
using Zenject;

namespace RestaurantLoop.Runtime.Conveyor
{
    // Pooled exit-stack rack piece.
    public class ExitSlotController : MonoBehaviour, IPoolable<Vector3>
    {
        private ThemeVisualOverride _themeVisual;

        private void Awake()
        {
            _themeVisual = new ThemeVisualOverride(transform);
        }

        // Null restores the base look.
        public void ApplyThemeVisual(GameObject overridePrefab) => _themeVisual.Apply(overridePrefab);

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