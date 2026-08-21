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

        // Feeds a new item onto the belt at the entry (path index 0, right after the Gate).
        public void AddItemAtEntry(FoodStackData stack)
        {
            if (_pathWorldPositions.Count == 0) return;

            var visual = _foodPool.Spawn(_pathWorldPositions[0], stack);
            _items.Add(new BeltItem { Visual = visual, Progress = 0f });
        }

        public void Tick()
        {
            if (_pathWorldPositions.Count == 0) return;

            float delta = _tuning.BeltSpeed * Time.deltaTime;
            float lastIndex = _pathWorldPositions.Count - 1;

            for (int i = _items.Count - 1; i >= 0; i--)
            {
                var item = _items[i];
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

                item.Visual.transform.position = SampleWorldPosition(item.Progress);
            }
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