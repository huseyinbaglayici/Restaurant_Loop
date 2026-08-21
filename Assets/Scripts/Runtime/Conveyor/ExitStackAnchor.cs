using System;
using System.Collections.Generic;
using RestaurantLoop.Runtime.Data;
using UnityEngine;
using Zenject;

namespace RestaurantLoop.Runtime.Conveyor
{
    /// <summary>
    /// Foundation for the exit stack — where food that exits the belt at the Gate will eventually
    /// be placed, in arrival order, and (later) trigger level-end once full. Not level-authored:
    /// fixed slot count, positions derived from the painted ConveyorCells' actual min/max X (not
    /// GridWidth — the loop rarely fills the whole grid) and the belt's own minimum-Y row, not a
    /// fixed distance from world Z=0.
    /// </summary>
    public class ExitStackAnchor : IInitializable, IDisposable
    {
        public const int SlotCount = 5;

        // World units "below" (negative Z) the belt's own closest (minimum-Y) row. Flip the sign
        // here if "below" turns out to read as +Z once you see it against the camera.
        private const float ZOffsetFromGrid = 1.5f;

        private readonly ConveyorLevelDataSO _level;
        private readonly ConveyorPrefabSettings _prefabSettings;
        private readonly ExitSlotController.Pool _exitSlotPool;
        private readonly List<ExitSlotController> _spawnedFloors = new();

        public Vector3[] SlotPositions { get; private set; }

        public ExitStackAnchor(ConveyorLevelDataSO level, ConveyorPrefabSettings prefabSettings, ExitSlotController.Pool exitSlotPool)
        {
            _level = level;
            _prefabSettings = prefabSettings;
            _exitSlotPool = exitSlotPool;

            // Computed in the constructor (not Initialize) so dependents — QueueAnchor reads
            // SlotPositions via constructor injection — see a fully-populated array regardless of
            // IInitializable call order between the two.
            SlotPositions = ComputeSlotPositions();
        }

        private Vector3[] ComputeSlotPositions()
        {
            if (_level.ConveyorCells.Count == 0)
            {
                Debug.LogError("[ExitStackAnchor] Level has no conveyor cells — can't position exit stack.");
                return Array.Empty<Vector3>();
            }

            int minX = int.MaxValue, maxX = int.MinValue, minY = int.MaxValue;
            foreach (var cell in _level.ConveyorCells)
            {
                if (cell.Coord.x < minX) minX = cell.Coord.x;
                if (cell.Coord.x > maxX) maxX = cell.Coord.x;
                if (cell.Coord.y < minY) minY = cell.Coord.y;
            }

            float cellSize = _prefabSettings.CellWorldSize;
            float spanWorldX = (maxX - minX + 1) * cellSize;
            float slotSpacing = spanWorldX / SlotCount;
            float startX = minX * cellSize - cellSize * 0.5f;

            // minY's row edge (closest belt element to the exit stack), then the fixed margin below it.
            float nearestRowEdgeZ = minY * cellSize - cellSize * 0.5f;
            float z = nearestRowEdgeZ - ZOffsetFromGrid;

            var positions = new Vector3[SlotCount];
            for (int i = 0; i < SlotCount; i++)
            {
                float x = startX + slotSpacing * (i + 0.5f);
                positions[i] = new Vector3(x, 0f, z);
            }
            return positions;
        }

        public void Initialize()
        {
            if (SlotPositions.Length == 0) return; // ComputeSlotPositions already logged why

            for (int i = 0; i < SlotPositions.Length; i++)
                _spawnedFloors.Add(_exitSlotPool.Spawn(SlotPositions[i]));
        }

        public void Dispose()
        {
            foreach (var floor in _spawnedFloors) _exitSlotPool.Despawn(floor);
            _spawnedFloors.Clear();
        }
    }
}
