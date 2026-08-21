using RestaurantLoop.Runtime.Data;
using UnityEngine;
using Zenject;

namespace RestaurantLoop.Runtime.Conveyor
{
    /// <summary>
    /// Foundation for the supply queue lanes — one X position per lane (lane count = the level's
    /// Queues.Count, not a hardcoded number). Aligned to ExitStackAnchor's own leftmost/rightmost
    /// slot positions (not the conveyor cells directly) so the queue lines up under the exit stack
    /// rather than the belt grid, offset further along -Z below it.
    /// </summary>
    public class QueueAnchor : IInitializable
    {
        // World units "below" (further -Z) ExitStackAnchor's slot row.
        private const float ZOffsetFromExitStack = 2.2f;

        // World units between consecutive lanes on X. Centered under the exit stack's own
        // midpoint, not stretched to span its full leftmost-rightmost width.
        private const float LaneSpacing = 1.2f;

        private readonly ConveyorLevelDataSO _level;
        private readonly ExitStackAnchor _exitStackAnchor;

        public Vector3[] LanePositions { get; private set; }

        public QueueAnchor(ConveyorLevelDataSO level, ExitStackAnchor exitStackAnchor)
        {
            _level = level;
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
            float z = _exitStackAnchor.SlotPositions[0].z - ZOffsetFromExitStack;

            var positions = new Vector3[laneCount];
            for (int i = 0; i < laneCount; i++)
            {
                float x = centerX + (i - (laneCount - 1) * 0.5f) * LaneSpacing;
                positions[i] = new Vector3(x, 0f, z);
            }
            return positions;
        }

        public void Initialize()
        {
            // Placeholder floors per lane go here once a prefab exists for them.
        }
    }
}
