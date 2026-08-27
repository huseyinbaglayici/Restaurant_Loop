using RestaurantLoop.Runtime.Data;
using UnityEngine;
using Zenject;

namespace RestaurantLoop.Runtime.Conveyor
{
    public class TableController : IInitializable
    {
        private readonly TableSettings _settings;
        private readonly CameraController _cameraController;
        private GameObject _tableInstance;

        public TableController([Inject(Optional = true)] TableSettings settings, CameraController cameraController)
        {
            _settings = settings;
            _cameraController = cameraController;
        }

        public void Initialize()
        {
            if (_settings == null) return;

            // Find existing table in scene or spawn from prefab
            var existingTable = GameObject.Find("yemek_alti_masa");
            if (existingTable != null)
            {
                _tableInstance = existingTable;
            }
            else if (_settings.TablePrefab != null)
            {
                _tableInstance = Object.Instantiate(_settings.TablePrefab, _settings.BasePosition, Quaternion.Euler(_settings.BaseRotation));
                _tableInstance.name = "yemek_alti_masa";
            }

            if (_tableInstance == null) return;

            // Apply camera zoom scale if enabled
            if (_settings.ScaleWithCamera && _cameraController != null)
            {
                float zoom = _cameraController.CameraZoomFactor;
                float multiplier = _settings.TableScaleMultiplier;
                float finalScaleFactor = 1f + (zoom - 1f) * multiplier;

                _tableInstance.transform.localScale = _settings.BaseScale * finalScaleFactor;
            }
        }
    }
}
