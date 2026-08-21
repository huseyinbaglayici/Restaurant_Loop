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
                    laneVisuals.Add(_foodPool.Spawn(position, entries[row]));
                }

                _lanes.Add(laneVisuals);
            }
        }

        // Every item is nudged toward the slot its current row index implies. A no-op for items
        // already there (the common case) — only actually moves anything for the few frames right
        // after a PopFront shifts everyone's row index down by one.
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

        // The visual itself, not just its Stack — callers that need to hit-test against what's
        // actually on screen (ConveyorEntryInput) want FoodItemController.VisualCenter, which
        // accounts for stack height.
        public bool TryGetFrontVisual(int lane, out FoodItemController visual)
        {
            if (lane < 0 || lane >= _lanes.Count || _lanes[lane].Count == 0)
            {
                visual = null;
                return false;
            }

            visual = _lanes[lane][0];
            return true;
        }

        // Despawns the front item; Tick() animates the rest of the lane forward on subsequent
        // frames. Caller is responsible for reading the front's Stack before calling this.
        public void PopFront(int lane)
        {
            if (lane < 0 || lane >= _lanes.Count || _lanes[lane].Count == 0) return;

            var laneVisuals = _lanes[lane];
            _foodPool.Despawn(laneVisuals[0]);
            laneVisuals.RemoveAt(0);
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
