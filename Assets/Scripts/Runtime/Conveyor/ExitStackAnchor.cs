using System;
using DG.Tweening;
using RestaurantLoop.Runtime.Data;
using UnityEngine;
using Zenject;

namespace RestaurantLoop.Runtime.Conveyor
{
    // The exit stack's slot model: slot positions for the level, which food occupies which slot, and the
    // Extra Tray slot. The rack furniture underneath lives in ExitRackVisuals.
    public class ExitStackAnchor : IInitializable, IDisposable
    {
        public const int BaseSlotCount = 5;
        public const int MaxExtraSlots = 1;

        private readonly ConveyorLevelDataSO _level;
        private readonly ConveyorPrefabSettings _prefabSettings;
        private readonly ConveyorTuning _tuning;
        private readonly FoodItemController.Pool _foodPool;
        private readonly FoodFlightService _flightService;
        private readonly ExitRackVisuals _rackVisuals;

        // null = empty. Index matches SlotPositions.
        private FoodItemController[] _occupants = new FoodItemController[BaseSlotCount];

        public Vector3[] SlotPositions { get; private set; }
        public int SlotCount => SlotPositions.Length;
        public int ExtraSlotsAdded { get; private set; }
        public bool CanAddExtraSlot => SlotPositions != null && SlotPositions.Length > 0 && ExtraSlotsAdded < MaxExtraSlots;

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
            FoodItemController.Pool foodPool,
            ExitSlotPrefabSettings exitSlotPrefabSettings,
            [Inject(Optional = true)] FoodFlightService flightService,
            ThemeSettings themeSettings)
        {
            _level = level;
            _prefabSettings = prefabSettings;
            _tuning = tuning;
            _foodPool = foodPool;
            _flightService = flightService;
            _rackVisuals = new ExitRackVisuals(level, tuning, exitSlotPool, exitSlotPrefabSettings, themeSettings);

            SlotPositions = ComputeSlotPositions();
        }

        public void Initialize()
        {
            if (SlotPositions.Length == 0) return; // ComputeSlotPositions already logged why

            _rackVisuals.SpawnBaseRack(SlotPositions[0], SlotPositions[^1]);
        }

        public void Dispose()
        {
            _rackVisuals.Clear();

            for (int i = 0; i < _occupants.Length; i++)
            {
                if (_occupants[i] == null) continue;
                _foodPool.Despawn(_occupants[i]);
                _occupants[i] = null;
            }
        }

        // Slot ownership is taken immediately; ConveyorVisualService flies the visual there on FoodDockedSignal.
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

        // Where the food visual rests in a slot (slot positions themselves are at floor height).
        public Vector3 GetFoodPosition(int slotIndex)
        {
            if (slotIndex < 0 || slotIndex >= SlotPositions.Length) return Vector3.zero;

            var position = SlotPositions[slotIndex];
            position.y = _tuning.ExitStackFoodHeight;
            return position;
        }

        public Vector3 GetFoodPositionForOccupant(FoodItemController visual)
        {
            if (visual == null) return Vector3.zero;
            for (int i = 0; i < _occupants.Length; i++)
            {
                if (_occupants[i] == visual) return GetFoodPosition(i);
            }
            return Vector3.zero;
        }

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

        public void CompactSlots(float duration = 0.22f)
        {
            int targetIndex = 0;
            for (int i = 0; i < _occupants.Length; i++)
            {
                if (_occupants[i] == null) continue;

                var occupant = _occupants[i];
                if (i != targetIndex)
                {
                    _occupants[targetIndex] = occupant;
                    _occupants[i] = null;

                    var targetPos = GetFoodPosition(targetIndex);
                    if (_flightService == null || !_flightService.IsFlying(occupant.transform))
                    {
                        occupant.transform.DOKill();
                        occupant.transform.DOMove(targetPos, duration).SetEase(Ease.OutQuad);
                    }
                }
                targetIndex++;
            }
        }

        // Extra Tray power-up: appends one slot with the same spacing as the last two.
        public bool TryAddExtraSlot()
        {
            if (SlotPositions.Length == 0 || ExtraSlotsAdded >= MaxExtraSlots) return false;

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

            _rackVisuals.SpawnExtraRack(newSlotPosition);

            ExtraSlotsAdded++;
            return true;
        }

        private Vector3[] ComputeSlotPositions()
        {
            if (_level.ConveyorCells.Count == 0)
            {
                Debug.LogError("[ExitStackAnchor] Level has no conveyor cells — can't position exit stack.");
                return Array.Empty<Vector3>();
            }

            int minY = int.MaxValue;
            foreach (var cell in _level.ConveyorCells)
            {
                if (cell.Coord.y < minY) minY = cell.Coord.y;
            }

            float cellSize = _prefabSettings.CellWorldSize;
            float slotSpacing = _tuning.ExitStackSlotSpacing > 0f ? _tuning.ExitStackSlotSpacing : cellSize;

            var beltOrigin = ConveyorWorldUtil.ComputeBeltOrigin(_level, _tuning, cellSize);

            float centerX = _tuning.WorldOrigin.x + _tuning.ExitStackXOffset;
            float startX = centerX - slotSpacing * (BaseSlotCount - 1) * 0.5f;

            // Just in front of the belt's nearest row.
            float nearestRowEdgeZ = minY * cellSize - cellSize * 0.5f + beltOrigin.z;
            float z = nearestRowEdgeZ - _tuning.ExitStackZOffsetFromGrid;

            var positions = new Vector3[BaseSlotCount];
            for (int i = 0; i < BaseSlotCount; i++)
            {
                float x = startX + slotSpacing * i;
                positions[i] = new Vector3(x, _tuning.ExitStackGroundHeight + _tuning.WorldOrigin.y, z);
            }

            return positions;
        }
    }
}