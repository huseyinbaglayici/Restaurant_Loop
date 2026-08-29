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
        private readonly ExitStackAnchor _exitStackAnchor;
        private GameObject _tableInstance;

        public TableController(
            [Inject(Optional = true)] TableSettings settings,
            CameraController cameraController,
            QueueAnchor queueAnchor,
            ExitStackAnchor exitStackAnchor)
        {
            _settings = settings;
            _cameraController = cameraController;
            _queueAnchor = queueAnchor;
            _exitStackAnchor = exitStackAnchor;
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
            float finalScaleFactor = 1f;
            if (_settings.ScaleWithCamera && _cameraController != null)
            {
                float zoom = _cameraController.CameraZoomFactor;
                float multiplier = _settings.TableScaleMultiplier;
                finalScaleFactor = 1f + (zoom - 1f) * multiplier;

                _tableInstance.transform.localScale = _settings.BaseScale * finalScaleFactor;
            }

            // Align table X/Z with Exit Rack and Queue center
            if (_exitStackAnchor != null && _exitStackAnchor.SlotPositions.Length > 0)
            {
                float leftX = _exitStackAnchor.SlotPositions[0].x;
                float rightX = _exitStackAnchor.SlotPositions[^1].x;
                float rackCenterX = (leftX + rightX) * 0.5f;
                float rackZ = _exitStackAnchor.SlotPositions[0].z;

                var tablePos = _tableInstance.transform.position;
                tablePos.x = rackCenterX + (_settings.TableXOffset + _settings.QueueCenterOffset.x) * finalScaleFactor + (finalScaleFactor - 1f) * _settings.ZoomOffsetCompensation.x;
                tablePos.y = _settings.BasePosition.y + _settings.QueueCenterOffset.y * finalScaleFactor + (finalScaleFactor - 1f) * _settings.ZoomOffsetCompensation.y;
                tablePos.z = rackZ - (_settings.GapFromExitRack - _settings.QueueCenterOffset.z) * finalScaleFactor + (finalScaleFactor - 1f) * _settings.ZoomOffsetCompensation.z;
                _tableInstance.transform.position = tablePos;
            }
            else if (_queueAnchor.LanePositions.Length > 0)
            {
                var sum = Vector3.zero;
                foreach (var lanePos in _queueAnchor.LanePositions) sum += lanePos;
                var queueCenter = sum / _queueAnchor.LanePositions.Length;

                var tablePos = _tableInstance.transform.position;
                tablePos.x = queueCenter.x + (_settings.TableXOffset + _settings.QueueCenterOffset.x) * finalScaleFactor + (finalScaleFactor - 1f) * _settings.ZoomOffsetCompensation.x;
                tablePos.y = _settings.BasePosition.y + _settings.QueueCenterOffset.y * finalScaleFactor + (finalScaleFactor - 1f) * _settings.ZoomOffsetCompensation.y;
                tablePos.z = queueCenter.z + _settings.QueueCenterOffset.z * finalScaleFactor + (finalScaleFactor - 1f) * _settings.ZoomOffsetCompensation.z;
                _tableInstance.transform.position = tablePos;
            }
        }
    }
}
