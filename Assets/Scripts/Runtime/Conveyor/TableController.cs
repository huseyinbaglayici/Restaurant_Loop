using RestaurantLoop.Runtime.Data;
using UnityEngine;
using Zenject;

namespace RestaurantLoop.Runtime.Conveyor
{
    public class TableController : IInitializable
    {
        private readonly TableSettings _settings;
        private readonly CameraController _cameraController;
        private readonly QueueAnchor _queueAnchor;
        private GameObject _tableInstance;

        public TableController(
            [Inject(Optional = true)] TableSettings settings,
            CameraController cameraController,
            QueueAnchor queueAnchor)
        {
            _settings = settings;
            _cameraController = cameraController;
            _queueAnchor = queueAnchor;
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

            // Align table X/Z with queue center if queue lanes exist
            if (_queueAnchor.LanePositions.Length > 0)
            {
                var sum = Vector3.zero;
                foreach (var lanePos in _queueAnchor.LanePositions) sum += lanePos;
                var queueCenter = sum / _queueAnchor.LanePositions.Length;

                var tablePos = _tableInstance.transform.position;
                tablePos.x = queueCenter.x + _settings.QueueCenterOffset.x;
                tablePos.y = _settings.BasePosition.y + _settings.QueueCenterOffset.y;
                tablePos.z = queueCenter.z + _settings.QueueCenterOffset.z;
                _tableInstance.transform.position = tablePos;
            }

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
