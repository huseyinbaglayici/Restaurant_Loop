using RestaurantLoop.Runtime.Data;
using UnityEngine;

namespace RestaurantLoop.Runtime.Conveyor
{
    // Places the camera for the loaded level: keeps the scene's authored offset from the queue center,
    // zooms out for grids bigger than the reference size and fits the board between the HUD bars.
    // Rotation is left untouched. Owned by CameraController.
    public class CameraLevelFraming
    {
        private readonly ConveyorLevelDataSO _level;
        private readonly ConveyorTuning _tuning;
        private readonly QueueAnchor _queueAnchor;
        private readonly Camera _camera;
        private readonly ThemeSettings _themeSettings;
        private readonly ConveyorPrefabSettings _prefabSettings;
        private readonly Vector3 _offsetFromQueueCenter;
        private readonly float _initialOrthoSize;

        public float ZoomFactor { get; private set; } = 1f;
        public float HeightZoomFactor { get; private set; } = 1f;

        public CameraLevelFraming(
            ConveyorLevelDataSO level,
            ConveyorTuning tuning,
            QueueAnchor queueAnchor,
            Camera camera,
            ThemeSettings themeSettings,
            ConveyorPrefabSettings prefabSettings)
        {
            _level = level;
            _tuning = tuning;
            _queueAnchor = queueAnchor;
            _camera = camera;
            _themeSettings = themeSettings;
            _prefabSettings = prefabSettings;
            _initialOrthoSize = camera.orthographicSize;
            _offsetFromQueueCenter = camera.transform.position - ComputeQueueCenter();
        }

        public void Apply()
        {
            ApplyGridZoom();

            var queueCenter = ComputeQueueCenter();
            var targetPos = queueCenter + _offsetFromQueueCenter;
            targetPos.x = queueCenter.x;

            if (ZoomFactor > 1f)
            {
                float zoomDiff = ZoomFactor - 1f;
                var theme = _themeSettings != null ? _themeSettings.GetTheme(_level.Theme) : null;

                float yMult = theme != null && theme.CameraZoomYShiftMultiplier != 0f
                    ? theme.CameraZoomYShiftMultiplier
                    : _tuning.CameraZoomYShiftMultiplier;

                float zMult = theme != null && theme.CameraZoomZShiftMultiplier != 0f
                    ? theme.CameraZoomZShiftMultiplier
                    : _tuning.CameraZoomZShiftMultiplier;

                targetPos.y += zoomDiff * yMult;
                targetPos.z += zoomDiff * zMult;
            }

            _camera.transform.position = targetPos;
            if (_camera.orthographic)
            {
                _camera.orthographicSize *= CameraController.AspectWidthFactor(_camera);
                FitBoardBetweenHud();
            }
        }

        private void ApplyGridZoom()
        {
            if (!_tuning.EnableDynamicCameraScale || !_camera.orthographic || _tuning.ReferenceGridWidth <= 0 || _tuning.ReferenceGridHeight <= 0)
                return;

            float widthRatio = (float)_level.GridWidth / _tuning.ReferenceGridWidth;
            float heightRatio = (float)_level.GridHeight / _tuning.ReferenceGridHeight;
            float maxRatio = Mathf.Max(widthRatio, heightRatio);

            float sensitivity = _tuning.CameraZoomSensitivity > 0f ? _tuning.CameraZoomSensitivity : 0.85f;

            if (maxRatio > 1f)
            {
                float scaleFactor = 1f + (maxRatio - 1f) * sensitivity;
                _camera.orthographicSize = _initialOrthoSize * scaleFactor;
                ZoomFactor = scaleFactor;
            }

            if (heightRatio > 1f)
            {
                HeightZoomFactor = 1f + (heightRatio - 1f) * sensitivity;
            }
        }

        private Vector3 ComputeQueueCenter()
        {
            var lanePositions = _queueAnchor.LanePositions;
            if (lanePositions.Length == 0) return Vector3.zero;

            var sum = Vector3.zero;
            foreach (var position in lanePositions) sum += position;
            return sum / lanePositions.Length;
        }

        // Keeps the belt and the first queue rows inside the band between the top HUD and the bottom
        // power-up bar: zooms out only if the board is taller than the band, then slides the camera.
        private void FitBoardBetweenHud()
        {
            if (!TryGetBoardViewportRange(out float minY, out float maxY, out float minX, out float maxX)) return;

            float bandMin = _tuning.CameraHudBottomFraction;
            float bandMax = 1f - _tuning.CameraHudTopFraction;
            float band = bandMax - bandMin;
            if (band <= 0f) return;

            float scale = Mathf.Max(1f, (maxY - minY) / band, (maxX - minX) / 0.96f);
            if (scale > 1f)
            {
                _camera.orthographicSize *= scale;
                minY = 0.5f + (minY - 0.5f) / scale;
                maxY = 0.5f + (maxY - 0.5f) / scale;
            }

            float shift = 0f;
            if (minY < bandMin) shift = bandMin - minY;
            else if (maxY > bandMax) shift = bandMax - maxY;
            if (Mathf.Approximately(shift, 0f)) return;

            // Moving the camera down its own up axis slides the content up the viewport.
            _camera.transform.position -= _camera.transform.up * (shift * 2f * _camera.orthographicSize);
        }

        private bool TryGetBoardViewportRange(out float minY, out float maxY, out float minX, out float maxX)
        {
            float yMin = float.MaxValue, yMax = float.MinValue, xMin = float.MaxValue, xMax = float.MinValue;
            bool any = false;

            float cellSize = _prefabSettings.CellWorldSize;
            var beltOrigin = ConveyorWorldUtil.ComputeBeltOrigin(_level, _tuning, cellSize);
            foreach (var cell in _level.ConveyorCells)
            {
                var world = ConveyorWorldUtil.ToWorldPosition(cell.Coord, cellSize, 0f, beltOrigin);
                Include(world + new Vector3(-cellSize, 0f, -cellSize) * 0.5f);
                Include(world + new Vector3(cellSize, 0f, cellSize) * 0.5f);
                any = true;
            }

            var lanes = _queueAnchor.LanePositions;
            for (int lane = 0; lane < lanes.Length && lane < _level.Queues.Count; lane++)
            {
                int rows = Mathf.Min(_level.Queues[lane].Entries.Count, Mathf.Max(1, _tuning.CameraVisibleQueueRows));
                float spacing = _tuning.QueueRowSpacing;
                Include(lanes[lane] + new Vector3(-spacing * 0.5f, 0f, spacing * 0.5f));
                Include(lanes[lane] + new Vector3(spacing * 0.5f, 0f, -(rows - 0.5f) * spacing));
                any = true;
            }

            minY = yMin;
            maxY = yMax;
            minX = xMin;
            maxX = xMax;
            return any;

            void Include(Vector3 world)
            {
                var v = _camera.WorldToViewportPoint(world);
                yMin = Mathf.Min(yMin, v.y);
                yMax = Mathf.Max(yMax, v.y);
                xMin = Mathf.Min(xMin, v.x);
                xMax = Mathf.Max(xMax, v.x);
            }
        }
    }
}
