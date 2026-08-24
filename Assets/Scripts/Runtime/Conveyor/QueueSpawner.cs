using System;
using System.Collections.Generic;
using RestaurantLoop.Runtime.Data;
using UnityEngine;
using Zenject;

namespace RestaurantLoop.Runtime.Conveyor
{
 
    public class QueueSpawner : IInitializable, ITickable, IDisposable
    {
        private readonly ConveyorLevelDataSO _level;
        private readonly ConveyorTuning _tuning;
        private readonly QueueAnchor _queueAnchor;
        private readonly FoodItemController.Pool _foodPool;

        // Per lane, front-to-back (index 0 = front).
        private readonly List<List<FoodItemController>> _lanes = new();

        // Pick From Bottom power-up: while true, any row in a lane is pickable, not just the front. One-shot.
        public bool PickFromBottomEnabled { get; set; }

        public QueueSpawner(ConveyorLevelDataSO level, ConveyorTuning tuning, QueueAnchor queueAnchor, FoodItemController.Pool foodPool)
        {
            _level = level;
            _tuning = tuning;
            _queueAnchor = queueAnchor;
            _foodPool = foodPool;
        }

        public void Initialize()
        {
            int laneCount = Mathf.Min(_level.Queues.Count, _queueAnchor.LanePositions.Length);
            for (int lane = 0; lane < laneCount; lane++)
            {
                var laneVisuals = new List<FoodItemController>();
                var lanePosition = _queueAnchor.LanePositions[lane];
                var entries = _level.Queues[lane].Entries;

                for (int row = 0; row < entries.Count; row++)
                {
                    var position = lanePosition + new Vector3(0f, 0f, -row * _tuning.QueueRowSpacing);
                    laneVisuals.Add(_foodPool.Spawn(position, entries[row], FoodVisualState.Closed));
                }

                _lanes.Add(laneVisuals);
            }
        }

        public void Tick()
        {
            for (int lane = 0; lane < _lanes.Count; lane++)
            {
                var lanePosition = _queueAnchor.LanePositions[lane];
                var laneVisuals = _lanes[lane];

                for (int row = 0; row < laneVisuals.Count; row++)
                {
                    var target = lanePosition + new Vector3(0f, 0f, -row * _tuning.QueueRowSpacing);
                    var visualTransform = laneVisuals[row].transform;
                    visualTransform.position = Vector3.MoveTowards(visualTransform.position, target, _tuning.QueueShiftSpeed * Time.deltaTime);
                }
            }
        }

        public int GetLaneCount(int lane) => (lane >= 0 && lane < _lanes.Count) ? _lanes[lane].Count : 0;

        public bool TryGetVisual(int lane, int row, out FoodItemController visual)
        {
            if (lane < 0 || lane >= _lanes.Count || row < 0 || row >= _lanes[lane].Count)
            {
                visual = null;
                return false;
            }

            visual = _lanes[lane][row];
            return true;
        }

        public void PopAt(int lane, int row)
        {
            if (!TryDetachAt(lane, row, out var visual)) return;
            _foodPool.Despawn(visual);
        }

        // Removes the entry from its lane but keeps the visual alive, so the caller can keep flying the
        // very same object onto the belt instead of despawn + respawn (which would read as a teleport).
        public bool TryDetachAt(int lane, int row, out FoodItemController visual)
        {
            visual = null;
            if (lane < 0 || lane >= _lanes.Count) return false;

            var laneVisuals = _lanes[lane];
            if (row < 0 || row >= laneVisuals.Count) return false;

            visual = laneVisuals[row];
            laneVisuals.RemoveAt(row);
            return true;
        }

        // Shuffle power-up: flattens every lane, shuffles, then redeals round-robin.
        // Tick() picks up the new order next frame and animates each visual to its new slot.
        public void ShuffleLanes()
        {
            if (_lanes.Count == 0) return;

            var all = new List<FoodItemController>();
            foreach (var lane in _lanes)
            {
                all.AddRange(lane);
                lane.Clear();
            }

            for (int i = 0; i < all.Count; i++)
            {
                int randomIndex = UnityEngine.Random.Range(i, all.Count);
                (all[i], all[randomIndex]) = (all[randomIndex], all[i]);
            }

            for (int i = 0; i < all.Count; i++)
                _lanes[i % _lanes.Count].Add(all[i]);
        }

        public void Dispose()
        {
            foreach (var lane in _lanes)
                foreach (var visual in lane)
                    _foodPool.Despawn(visual);
            _lanes.Clear();
        }
    }
}
