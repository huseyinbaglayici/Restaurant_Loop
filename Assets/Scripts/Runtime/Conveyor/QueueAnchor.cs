using RestaurantLoop.Runtime.Data;
using UnityEngine;
using Zenject;

namespace RestaurantLoop.Runtime.Conveyor
{
    public class QueueAnchor : IInitializable
    {
        private readonly ConveyorLevelDataSO _level;
        private readonly ConveyorTuning _tuning;
        private readonly ExitStackAnchor _exitStackAnchor;

        public Vector3[] LanePositions { get; private set; }

        public QueueAnchor(ConveyorLevelDataSO level, ConveyorTuning tuning, ExitStackAnchor exitStackAnchor)
        {
            _level = level;
            _tuning = tuning;
            _exitStackAnchor = exitStackAnchor;
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
            float centerX = (leftX + rightX) * 0.5f;
            float z = _exitStackAnchor.SlotPositions[0].z - _tuning.QueueZOffsetFromExitStack;

            var positions = new Vector3[laneCount];
            for (int i = 0; i < laneCount; i++)
            {
                float x = centerX + (i - (laneCount - 1) * 0.5f) * _tuning.QueueLaneSpacing;
                positions[i] = new Vector3(x, _tuning.QueueFoodHeight, z);
            }

            return positions;
        }

        public void Initialize()
        {
            // TODO: lane floors
        }
    }
}