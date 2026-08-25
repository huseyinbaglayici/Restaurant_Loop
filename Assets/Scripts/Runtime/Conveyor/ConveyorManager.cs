using System;
using System.Collections.Generic;
using RestaurantLoop.Runtime.Conveyor;
using RestaurantLoop.Runtime.Data;
using RestaurantLoop.Signals;
using UnityEngine;
using Zenject;

namespace RestaurantLoop.Runtime.Conveyor
{
    // Logical layer: owns where every item is on the baked path, who crosses which cell, and what gets
    // consumed or docked. It never moves a transform — it fires signals and ConveyorVisualService plays
    // them back a few frames later (see ConveyorFeelSettings.VisualLagSeconds).
    public class ConveyorManager : IInitializable, ITickable, IDisposable
    {
        private class BeltItem
        {
            public int Id;
            public FoodItemController Visual;
            public float Progress;
        }

        private readonly ConveyorLevelDataSO _level;
        private readonly ConveyorPrefabSettings _prefabSettings;
        private readonly ConveyorTuning _tuning;
        private readonly FoodItemController.Pool _foodPool;
        private readonly ExitStackAnchor _exitStackAnchor;
        private readonly SignalBus _signalBus;

        private readonly List<Vector3> _pathWorldPositions = new();
        private readonly List<BeltItem> _items = new();

        private readonly List<(int PathIndex, FoodItemController Visual)> _crossingsThisFrame = new();
        public IReadOnlyList<(int PathIndex, FoodItemController Visual)> CrossingsThisFrame => _crossingsThisFrame;

        private int _nextItemId;

        public int ItemCount => _items.Count;
        public int PathLength => _pathWorldPositions.Count;

        // Mirrors QueueSpawner.TryGetVisual so callers (e.g. QueueLinkVisualizer) can enumerate belt
        // items the same way they enumerate queue items, without exposing the BeltItem type itself.
        public FoodItemController GetVisualAt(int index) => _items[index].Visual;

        public ConveyorManager(
            ConveyorLevelDataSO level,
            ConveyorPrefabSettings prefabSettings,
            ConveyorTuning tuning,
            FoodItemController.Pool foodPool,
            ExitStackAnchor exitStackAnchor,
            SignalBus signalBus)
        {
            _level = level;
            _prefabSettings = prefabSettings;
            _tuning = tuning;
            _foodPool = foodPool;
            _exitStackAnchor = exitStackAnchor;
            _signalBus = signalBus;
        }

        public void Initialize()
        {
            var orderedPath = _level.BakedPath.OrderedPath;
            if (orderedPath.Count == 0)
            {
                Debug.LogError(
                    "[ConveyorManager] Level has no baked path — save it in the level editor (with a Gate set) first.");
                return;
            }

            float cellSize = _prefabSettings.CellWorldSize;
            foreach (var coord in orderedPath)
            {
                var position = ConveyorWorldUtil.ToWorldPosition(coord, cellSize);
                position.y = _tuning.BeltFoodHeight;
                _pathWorldPositions.Add(position);
            }

            foreach (var entity in _level.ConveyorEntities)
            {
                int index = orderedPath.IndexOf(entity.Coord);
                if (index < 0)
                {
                    Debug.LogError($"[ConveyorManager] Entity at {entity.Coord} isn't on the baked path — skipped.");
                    continue;
                }

                // Level-authored items start already on the belt: no entry flight, just a placed visual.
                var visual = _foodPool.Spawn(_pathWorldPositions[index], entity.Stack, FoodVisualState.Opened);
                AddItem(entity.Stack, visual, index, _pathWorldPositions[index], playEntryFlight: false);
            }
        }

        // Spawns a fresh visual at the belt entry (no travel).
        public void AddItemAtEntry(FoodStackData stack)
        {
            if (_pathWorldPositions.Count == 0) return;

            var visual = _foodPool.Spawn(_pathWorldPositions[0], stack, FoodVisualState.Opened);
            AddItem(stack, visual, 0f, _pathWorldPositions[0], playEntryFlight: false);
        }

        // Reuses an existing visual (queue slot, exit-stack slot) and lets the visual layer fly it in
        // from where it currently sits. Logically the item is on the belt entry right away.
        // entryProgress lets two items entering together (linked pair) land a grid apart instead of
        // stacking on the same cell — pass a small negative value to place an item behind the entry.
        public void AddItemAtEntry(FoodStackData stack, FoodItemController reusedVisual, Vector3 sourceWorldPosition, float entryProgress = 0f)
        {
            if (_pathWorldPositions.Count == 0) return;

            if (reusedVisual == null)
            {
                AddItemAtEntry(stack);
                return;
            }

            AddItem(stack, reusedVisual, entryProgress, sourceWorldPosition, playEntryFlight: true);
        }

        // No entry-cell crossing is emitted here: the item becomes eligible for a consume match only
        // once Tick() carries its Progress past cell 0, same as every other belt cell.
        private void AddItem(
            FoodStackData stack,
            FoodItemController visual,
            float startProgress,
            Vector3 sourceWorldPosition,
            bool playEntryFlight)
        {
            var item = new BeltItem { Id = _nextItemId++, Visual = visual, Progress = startProgress };
            _items.Add(item);

            _signalBus.Fire(new FoodEnteredBeltSignal
            {
                ItemId = item.Id,
                Visual = visual,
                SourceWorldPosition = sourceWorldPosition,
                StartProgress = startProgress,
                PlayEntryFlight = playEntryFlight
            });
        }

        // cleared by ConsumeService, not here
        public void ClearCrossings() => _crossingsThisFrame.Clear();

        public void ClearBelt()
        {
            for (int i = _items.Count - 1; i >= 0; i--)
            {
                _foodPool.Despawn(_items[i].Visual);
                _items.RemoveAt(i);
            }

            _signalBus.Fire(new BeltClearedSignal());
        }

        public void Tick()
        {
            if (_pathWorldPositions.Count == 0) return;

            float delta = _tuning.BeltSpeed * Time.deltaTime;
            float lastIndex = _pathWorldPositions.Count - 1;

            for (int i = _items.Count - 1; i >= 0; i--)
            {
                var item = _items[i];
                int prevIndex = Mathf.FloorToInt(item.Progress);
                item.Progress += delta;

                if (item.Progress >= lastIndex)
                {
                    if (_exitStackAnchor.TryDock(item.Visual, out int slotIndex))
                    {
                        _signalBus.Fire(new FoodDockedSignal
                        {
                            ItemId = item.Id,
                            Visual = item.Visual,
                            SlotIndex = slotIndex,
                            SlotWorldPosition = _exitStackAnchor.GetFoodPosition(slotIndex)
                        });
                    }
                    else
                    {
                        Debug.LogWarning("[ConveyorManager] Exit stack is full — item despawned.");
                        _foodPool.Despawn(item.Visual);
                    }

                    _items.RemoveAt(i);
                    continue;
                }

                // lag-skip guard (items can start at a negative entryProgress — skip crossings before cell 0)
                int newIndex = Mathf.FloorToInt(item.Progress);
                for (int idx = Mathf.Max(0, prevIndex + 1); idx <= newIndex; idx++)
                    _crossingsThisFrame.Add((idx, item.Visual));
            }
        }

        // peek-only
        public bool TryPeekTypeAt(int pathIndex, out FoodType type)
        {
            for (int i = 0; i < _items.Count; i++)
            {
                int itemIndex = Mathf.RoundToInt(_items[i].Progress) % _pathWorldPositions.Count;
                if (itemIndex != pathIndex) continue;

                type = _items[i].Visual.Stack.Type;
                return true;
            }

            type = default;
            return false;
        }

        // by-reference, not by position. On a full take the item leaves the belt but its visual is NOT
        // despawned — ConveyorVisualService takes ownership and flies it to the customer.
        public int ConsumeFromItem(FoodItemController visual, int amountWanted, out int itemId, out bool itemFullyConsumed)
        {
            for (int i = 0; i < _items.Count; i++)
            {
                if (_items[i].Visual != visual) continue;

                itemId = _items[i].Id;
                int amountTaken = Mathf.Min(amountWanted, visual.Stack.Amount);
                itemFullyConsumed = amountTaken >= visual.Stack.Amount;

                if (itemFullyConsumed) _items.RemoveAt(i);
                else visual.ReduceAmount(amountTaken);

                return amountTaken;
            }

            itemId = -1;
            itemFullyConsumed = false;
            return 0;
        }

        public Vector3 SampleWorldPosition(float progress)
        {
            int count = _pathWorldPositions.Count;
            if (count == 0) return Vector3.zero;

            // Before cell 0 (entryProgress < 0, e.g. the trailing half of a linked pair): extrapolate
            // backward along the entry direction instead of clamping to cell 0, so it doesn't overlap it.
            if (progress < 0f && count > 1)
            {
                Vector3 entryDir = _pathWorldPositions[0] - _pathWorldPositions[1];
                return _pathWorldPositions[0] + entryDir * -progress;
            }

            int index = Mathf.Clamp(Mathf.FloorToInt(progress), 0, count - 1);
            int nextIndex = (index + 1) % count;
            float frac = Mathf.Clamp01(progress - Mathf.Floor(progress));
            return Vector3.Lerp(_pathWorldPositions[index], _pathWorldPositions[nextIndex], frac);
        }

        public void Dispose()
        {
            foreach (var item in _items) _foodPool.Despawn(item.Visual);
            _items.Clear();
        }
    }
}
