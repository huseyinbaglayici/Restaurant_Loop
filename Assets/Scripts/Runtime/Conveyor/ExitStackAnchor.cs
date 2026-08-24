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
        public const int BaseSlotCount = 5;

        // Foodrack.fbx authored length along local Z at scale 1 (measured from its mesh bounds).
        // The single rack is scaled on Z to stretch across every slot instead of spawning one piece per slot.
        private const float RackAuthoredLength = 7.59f;

        private readonly ConveyorLevelDataSO _level;
        private readonly ConveyorPrefabSettings _prefabSettings;
        private readonly ConveyorTuning _tuning;
        private readonly ExitSlotController.Pool _exitSlotPool;
        private readonly FoodItemController.Pool _foodPool;
        private readonly List<ExitSlotController> _spawnedFloors = new();

        // null = empty. Index matches SlotPositions. Grows by one if an Extra Tray power-up is used.
        private FoodItemController[] _occupants = new FoodItemController[BaseSlotCount];

        public Vector3[] SlotPositions { get; private set; }
        public int SlotCount => SlotPositions.Length;
        public bool HasExtraSlot { get; private set; }

        public int OccupiedCount
        {
            get
            {
                int count = 0;
                for (int i = 0; i < _occupants.Length; i++)
                    if (_occupants[i] != null) count++;
                return count;
            }
        }

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
            float slotSpacing = spanWorldX / BaseSlotCount;
            float startX = minX * cellSize - cellSize * 0.5f;

            float nearestRowEdgeZ = minY * cellSize - cellSize * 0.5f;
            float z = nearestRowEdgeZ - _tuning.ExitStackZOffsetFromGrid;

            var positions = new Vector3[BaseSlotCount];
            for (int i = 0; i < BaseSlotCount; i++)
            {
                float x = startX + slotSpacing * (i + 0.5f);
                positions[i] = new Vector3(x, 0f, z);
            }

            return positions;
        }

        public void Initialize()
        {
            if (SlotPositions.Length == 0) return; // ComputeSlotPositions already logged why

            SpawnRack(SlotPositions[0], SlotPositions[^1]);
        }

        // One rack piece spans every slot from firstSlot to lastSlot — food items still dock individually
        // on top of it, only the visual floor underneath is now a single object instead of one per slot.
        private void SpawnRack(Vector3 firstSlot, Vector3 lastSlot)
        {
            var center = (firstSlot + lastSlot) * 0.5f;
            float spanWorldX = (lastSlot.x - firstSlot.x) + (lastSlot.x - firstSlot.x) / (SlotPositions.Length - 1);

            var rack = _exitSlotPool.Spawn(center);
            rack.transform.rotation = Quaternion.Euler(0f, 90f, 0f);
            rack.transform.localScale = new Vector3(1f, 1f, spanWorldX / RackAuthoredLength);
            _spawnedFloors.Add(rack);
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

        // Slot ownership is taken immediately; moving the visual into the slot is the visual layer's job
        // (ConveyorVisualService flies it there on FoodDockedSignal).
        public bool TryDock(FoodItemController visual, out int slotIndex)
        {
            for (int i = 0; i < _occupants.Length; i++)
            {
                if (_occupants[i] != null) continue;

                _occupants[i] = visual;
                slotIndex = i;
                return true;
            }

            slotIndex = -1;
            return false;
        }

        // Where the food visual rests in a slot — the slot floor prefab keeps SlotPositions itself.
        public Vector3 GetFoodPosition(int slotIndex)
        {
            if (slotIndex < 0 || slotIndex >= SlotPositions.Length) return Vector3.zero;

            var position = SlotPositions[slotIndex];
            position.y = _tuning.ExitStackFoodHeight;
            return position;
        }

        // peek-only
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

        // Extra Tray power-up: appends one slot, following the same spacing as the last two.
        // Capped at once per level — call with HasExtraSlot already true is a no-op.
        public bool TryAddExtraSlot()
        {
            if (HasExtraSlot) return false;
            if (SlotPositions.Length == 0) return false;

            Vector3 delta = SlotPositions.Length > 1
                ? SlotPositions[^1] - SlotPositions[^2]
                : new Vector3(_prefabSettings.CellWorldSize, 0f, 0f);
            Vector3 newSlotPosition = SlotPositions[^1] + delta;

            var newPositions = new Vector3[SlotPositions.Length + 1];
            SlotPositions.CopyTo(newPositions, 0);
            newPositions[^1] = newSlotPosition;
            SlotPositions = newPositions;

            var newOccupants = new FoodItemController[_occupants.Length + 1];
            _occupants.CopyTo(newOccupants, 0);
            _occupants = newOccupants;

            // No extra floor spawn — the rack is one fixed-length piece; the extra slot only adds
            // a dock point past its edge.
            HasExtraSlot = true;
            return true;
        }
    }
}