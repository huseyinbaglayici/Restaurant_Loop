using RestaurantLoop.Runtime.Data;
using UnityEngine;

namespace RestaurantLoop.Runtime.Conveyor
{
    // World positions of the queue lanes, centered on the table in front of the exit rack.
    public class QueueAnchor
    {
        private readonly ConveyorLevelDataSO _level;
        private readonly ConveyorTuning _tuning;
        private readonly ExitStackAnchor _exitStackAnchor;
        private readonly TableSettings _tableSettings;

        public Vector3[] LanePositions { get; private set; }

        public QueueAnchor(
            ConveyorLevelDataSO level,
            ConveyorTuning tuning,
            ExitStackAnchor exitStackAnchor,
            TableSettings tableSettings)
        {
            _level = level;
            _tuning = tuning;
            _exitStackAnchor = exitStackAnchor;
            _tableSettings = tableSettings;
            LanePositions = ComputeLanePositions();
        }

        private Vector3[] ComputeLanePositions()
        {
            int laneCount = _level.Queues.Count;
            if (laneCount == 0 || _exitStackAnchor.SlotPositions.Length == 0)
            {
                Debug.LogError("[QueueAnchor] No queue lanes, or exit stack has no slots — can't position queue.");
                return System.Array.Empty<Vector3>();
            }

            float leftX = _exitStackAnchor.SlotPositions[0].x;
            float rightX = _exitStackAnchor.SlotPositions[^1].x;
            float rackCenterX = (leftX + rightX) * 0.5f;

            float tableX = rackCenterX + (_tableSettings != null ? _tableSettings.TableXOffset : 0f);
            float queueCenterX = tableX + (_tableSettings != null ? _tableSettings.QueueFoodXOffsetOnTable : 0f);

            float gapFromRack = _tableSettings != null && _tableSettings.GapFromExitRack > 0f
                ? _tableSettings.GapFromExitRack
                : _tuning.QueueZOffsetFromExitStack;
            float foodZOffset = _tableSettings != null ? _tableSettings.QueueFoodZOffsetOnTable : 0f;
            float z = _exitStackAnchor.SlotPositions[0].z - (gapFromRack + foodZOffset);

            var positions = new Vector3[laneCount];
            for (int i = 0; i < laneCount; i++)
            {
                float x = queueCenterX + (i - (laneCount - 1) * 0.5f) * _tuning.QueueLaneSpacing;
                positions[i] = new Vector3(x, _tuning.QueueFoodHeight + _tuning.WorldOrigin.y, z);
            }

            return positions;
        }
    }
}