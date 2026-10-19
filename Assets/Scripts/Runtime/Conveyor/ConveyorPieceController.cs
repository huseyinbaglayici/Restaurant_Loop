using UnityEngine;
using Zenject;

namespace RestaurantLoop.Runtime.Conveyor
{
    public class ConveyorPieceController : MonoBehaviour, IPoolable<Vector3, float>
    {
        public static float GlobalSpeedMultiplier { get; set; } = 1f;

        private float _baseTiltX;
        private float _baseEulerY;
        private float _baseTiltZ;
        private float _axisSign;

        private ThemeVisualOverride _themeVisual;

        private void Awake()
        {
            var e = transform.localRotation.eulerAngles;
            _baseTiltX = e.x;
            _baseEulerY = e.y;
            _baseTiltZ = e.z;

            _themeVisual = new ThemeVisualOverride(transform);
        }

        // Null restores the base look.
        public void ApplyThemeVisual(GameObject overridePrefab)
        {
            _themeVisual ??= new ThemeVisualOverride(transform);
            _themeVisual.Apply(overridePrefab);
            UpdateVisualsAxisSign();
        }

        public void SetAxisSign(float sign)
        {
            _axisSign = Mathf.Abs(sign) > 0.001f ? Mathf.Sign(sign) : 1f;
            UpdateVisualsAxisSign();
        }

        public void OnSpawned(Vector3 position, float worldYRotation)
        {
            transform.SetPositionAndRotation(position, Quaternion.Euler(_baseTiltX, _baseEulerY + worldYRotation, _baseTiltZ));
            // Flow flips for pieces turned 90/270 degrees.
            _axisSign = Mathf.Cos(2f * worldYRotation * Mathf.Deg2Rad);

            gameObject.SetActive(true);

            UpdateVisualsAxisSign();
        }

        public void OnDespawned()
        {
            gameObject.SetActive(false);
        }

        private void UpdateVisualsAxisSign()
        {
            // Re-queried each time: the active theme visual decides which piece visuals exist.
            var visuals = GetComponentsInChildren<IConveyorPieceVisual>(false);
            for (int i = 0; i < visuals.Length; i++)
            {
                visuals[i].OnPieceSpawned(_axisSign);
            }
        }

        public class Pool : MonoPoolableMemoryPool<Vector3, float, ConveyorPieceController>
        {
        }
    }
}
