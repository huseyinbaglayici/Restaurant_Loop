using System;
using System.Collections.Generic;
using DG.Tweening;
using RestaurantLoop.Runtime.Data;
using UnityEngine;
using Zenject;

namespace RestaurantLoop.Runtime.Conveyor
{
    public class ExitStackAnchor : IInitializable, IDisposable
    {
        public const int BaseSlotCount = 5;

        private readonly ConveyorLevelDataSO _level;
        private readonly ConveyorPrefabSettings _prefabSettings;
        private readonly ConveyorTuning _tuning;
        private readonly ExitSlotController.Pool _exitSlotPool;
        private readonly FoodItemController.Pool _foodPool;
        private readonly ExitSlotPrefabSettings _exitSlotPrefabSettings;
        private readonly FoodFlightService _flightService;
        private readonly ThemeSettings _themeSettings;
        private readonly List<ExitSlotController> _spawnedFloors = new();
        private readonly List<GameObject> _extraRackVisuals = new();

        // null = empty. Index matches SlotPositions. Grows by one every time the Extra Tray power-up is used.
        private FoodItemController[] _occupants = new FoodItemController[BaseSlotCount];

        public Vector3[] SlotPositions { get; private set; }
        public int SlotCount => SlotPositions.Length;
        public int ExtraSlotsAdded { get; private set; }

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
            [Inject(Optional = true)] FoodFlightService flightService = null,
            [Inject(Optional = true)] ThemeSettings themeSettings = null)
        {
            _level = level;
            _prefabSettings = prefabSettings;
            _tuning = tuning;
            _exitSlotPool = exitSlotPool;
            _foodPool = foodPool;
            _exitSlotPrefabSettings = exitSlotPrefabSettings;
            _flightService = flightService;
            _themeSettings = themeSettings ?? Resources.Load<ThemeSettings>("ThemeSettings");

            SlotPositions = ComputeSlotPositions();
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

            // Center exactly at WorldOrigin.x + ExitStackXOffset
            float centerX = _tuning.WorldOrigin.x + _tuning.ExitStackXOffset;
            float startX = centerX - slotSpacing * (BaseSlotCount - 1) * 0.5f;

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
            var theme = _themeSettings != null && _level != null ? _themeSettings.GetTheme(_level.Theme) : null;

            var rack = _exitSlotPool.Spawn(center);
            rack.transform.rotation = Quaternion.Euler(_tuning.ExtraRackVisualRotation);

            if (theme != null && theme.RackPrefab != null)
            {
                rack.ApplyThemeVisual(theme.RackPrefab);
            }

            _spawnedFloors.Add(rack);
        }

        public void Dispose()
        {
            foreach (var floor in _spawnedFloors) _exitSlotPool.Despawn(floor);
            _spawnedFloors.Clear();

            foreach (var visual in _extraRackVisuals)
                if (visual != null) UnityEngine.Object.Destroy(visual);
            _extraRackVisuals.Clear();

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

        public Vector3 GetFoodPositionForOccupant(FoodItemController visual)
        {
            if (visual == null) return Vector3.zero;
            for (int i = 0; i < _occupants.Length; i++)
            {
                if (_occupants[i] == visual) return GetFoodPosition(i);
            }
            return Vector3.zero;
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

        public const int MaxExtraSlots = 1;
        public bool CanAddExtraSlot => SlotPositions != null && SlotPositions.Length > 0 && ExtraSlotsAdded < MaxExtraSlots;

        // Extra Tray power-up: appends one slot, following the same spacing as the last two.
        // Limited to MaxExtraSlots (1 extra slot max).
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

            // The base rack is one fixed-length piece that doesn't cover the extra slot —
            // drop the dedicated Foodrack_extra visual over it so the new dock point reads as furniture too.
            var theme = _themeSettings != null && _level != null ? _themeSettings.GetTheme(_level.Theme) : null;
            var extraRackPrefab = (theme != null && theme.ExtraRackPrefab != null) 
                ? theme.ExtraRackPrefab 
                : _exitSlotPrefabSettings.ExtraRackPrefab;

            if (extraRackPrefab != null)
            {
                var visual = UnityEngine.Object.Instantiate(
                    extraRackPrefab,
                    newSlotPosition + _tuning.ExtraRackVisualOffset,
                    Quaternion.Euler(_tuning.ExtraRackVisualRotation));
                _extraRackVisuals.Add(visual);
            }
            else
            {
                Debug.LogWarning("[ExitStackAnchor] ExtraRackPrefab is unassigned — " +
                                  "Extra Tray power-up won't show its decorative rack visual.");
            }

            ExtraSlotsAdded++;
            return true;
        }
    }
}