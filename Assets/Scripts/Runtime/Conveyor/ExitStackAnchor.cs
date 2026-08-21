using System;
using System.Collections.Generic;
using RestaurantLoop.Runtime.Conveyor;
using RestaurantLoop.Runtime.Data;
using UnityEngine;
using Zenject;

namespace RestaurantLoop.Runtime.Conveyor
{
    public class ExitStackAnchor : IInitializable, IDisposable
    {
        public const int SlotCount = 5;

        private readonly ConveyorLevelDataSO _level;
        private readonly ConveyorPrefabSettings _prefabSettings;
        private readonly ConveyorTuning _tuning;
        private readonly ExitSlotController.Pool _exitSlotPool;
        private readonly FoodItemController.Pool _foodPool;
        private readonly List<ExitSlotController> _spawnedFloors = new();

        // null = empty. Index matches SlotPositions.
        private readonly FoodItemController[] _occupants = new FoodItemController[SlotCount];

        public Vector3[] SlotPositions { get; private set; }

        public ExitStackAnchor(
            ConveyorLevelDataSO level,
            ConveyorPrefabSettings prefabSettings,
            ConveyorTuning tuning,
            ExitSlotController.Pool exitSlotPool,
            FoodItemController.Pool foodPool)
        {
            _level = level;
            _prefabSettings = prefabSettings;
            _tuning = tuning;
            _exitSlotPool = exitSlotPool;
            _foodPool = foodPool;

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
            float z = nearestRowEdgeZ - _tuning.ExitStackZOffsetFromGrid;

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

            for (int i = 0; i < _occupants.Length; i++)
            {
                if (_occupants[i] == null) continue;
                _foodPool.Despawn(_occupants[i]);
                _occupants[i] = null;
            }
        }

        public bool TryDock(FoodItemController visual)
        {
            for (int i = 0; i < _occupants.Length; i++)
            {
                if (_occupants[i] != null) continue;

                _occupants[i] = visual;
                visual.transform.position = SlotPositions[i];
                return true;
            }

            return false;
        }

        // Non-removing peek — for hit-testing (ConveyorEntryInput) before committing to a slot.
        public bool TryGetOccupant(int slotIndex, out FoodItemController visual)
        {
            if (slotIndex < 0 || slotIndex >= _occupants.Length || _occupants[slotIndex] == null)
            {
                visual = null;
                return false;
            }

            visual = _occupants[slotIndex];
            return true;
        }

        // Caller takes ownership of the returned visual (despawn it, or hand it to whatever's
        // next — this only frees the slot).
        public bool TryRemoveFromSlot(int slotIndex, out FoodItemController visual)
        {
            if (slotIndex < 0 || slotIndex >= _occupants.Length || _occupants[slotIndex] == null)
            {
                visual = null;
                return false;
            }

            visual = _occupants[slotIndex];
            _occupants[slotIndex] = null;
            return true;
        }
    }
}