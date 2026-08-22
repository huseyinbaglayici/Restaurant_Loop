using System;
using System.Collections.Generic;
using RestaurantLoop.Runtime.Conveyor;
using RestaurantLoop.Runtime.Data;
using UnityEngine;
using Zenject;

namespace RestaurantLoop.Runtime.Conveyor
{
    public class ConveyorManager : IInitializable, ITickable, IDisposable
    {
        private class BeltItem
        {
            public FoodItemController Visual;
            public float Progress;
        }

        private readonly ConveyorLevelDataSO _level;
        private readonly ConveyorPrefabSettings _prefabSettings;
        private readonly ConveyorTuning _tuning;
        private readonly FoodItemController.Pool _foodPool;
        private readonly ExitStackAnchor _exitStackAnchor;

        private readonly List<Vector3> _pathWorldPositions = new();
        private readonly List<BeltItem> _items = new();
        
        private readonly List<(int PathIndex, FoodItemController Visual)> _crossingsThisFrame = new();
        public IReadOnlyList<(int PathIndex, FoodItemController Visual)> CrossingsThisFrame => _crossingsThisFrame;

        public int ItemCount => _items.Count;

        public ConveyorManager(
            ConveyorLevelDataSO level,
            ConveyorPrefabSettings prefabSettings,
            ConveyorTuning tuning,
            FoodItemController.Pool foodPool,
            ExitStackAnchor exitStackAnchor)
        {
            _level = level;
            _prefabSettings = prefabSettings;
            _tuning = tuning;
            _foodPool = foodPool;
            _exitStackAnchor = exitStackAnchor;
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
                _pathWorldPositions.Add(ConveyorWorldUtil.ToWorldPosition(coord, cellSize));

            foreach (var entity in _level.ConveyorEntities)
            {
                int index = orderedPath.IndexOf(entity.Coord);
                if (index < 0)
                {
                    Debug.LogError($"[ConveyorManager] Entity at {entity.Coord} isn't on the baked path — skipped.");
                    continue;
                }

                var visual = _foodPool.Spawn(_pathWorldPositions[index], entity.Stack);
                _items.Add(new BeltItem { Visual = visual, Progress = index });
            }
        }

        public void AddItemAtEntry(FoodStackData stack)
        {
            if (_pathWorldPositions.Count == 0) return;

            var visual = _foodPool.Spawn(_pathWorldPositions[0], stack);
            _items.Add(new BeltItem { Visual = visual, Progress = 0f });
            _crossingsThisFrame.Add((0, visual));
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
                    if (!_exitStackAnchor.TryDock(item.Visual))
                    {
                        Debug.LogWarning("[ConveyorManager] Exit stack is full — item despawned.");
                        _foodPool.Despawn(item.Visual);
                    }

                    _items.RemoveAt(i);
                    continue;
                }

                // lag-skip guard
                int newIndex = Mathf.FloorToInt(item.Progress);
                for (int idx = prevIndex + 1; idx <= newIndex; idx++)
                    _crossingsThisFrame.Add((idx, item.Visual));

                item.Visual.transform.position = SampleWorldPosition(item.Progress);
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

        // by-reference, not by position
        public int ConsumeFromItem(FoodItemController visual, int amountWanted)
        {
            for (int i = 0; i < _items.Count; i++)
            {
                if (_items[i].Visual != visual) continue;

                int amountTaken = Mathf.Min(amountWanted, visual.Stack.Amount);
                if (amountTaken >= visual.Stack.Amount)
                {
                    _foodPool.Despawn(visual);
                    _items.RemoveAt(i);
                }
                else
                {
                    visual.ReduceAmount(amountTaken);
                }

                return amountTaken;
            }

            return 0;
        }

        private Vector3 SampleWorldPosition(float progress)
        {
            int count = _pathWorldPositions.Count;
            int index = Mathf.FloorToInt(progress) % count;
            int nextIndex = (index + 1) % count;
            float frac = progress - Mathf.Floor(progress);
            return Vector3.Lerp(_pathWorldPositions[index], _pathWorldPositions[nextIndex], frac);
        }

        public void Dispose()
        {
            foreach (var item in _items) _foodPool.Despawn(item.Visual);
            _items.Clear();
        }
    }
}