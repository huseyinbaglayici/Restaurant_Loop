using System;
using System.Collections.Generic;
using RestaurantLoop.Runtime.Data;
using UnityEngine;
using Zenject;

namespace RestaurantLoop.Runtime.Conveyor
{
    /// <summary>
    /// Builds the visible supply queue from the level's Queues data, pulling stacks from
    /// PoolInstaller's FoodItemController pool — the queue-side equivalent of ConveyorPieceSpawner.
    /// Each lane's entries (index 0 = front/servable) are placed one behind another going further
    /// -Z, deeper into the lane, away from the belt. Despawns everything back to the pool on
    /// container teardown (GameplayScene unload) — same reasoning as ConveyorPieceSpawner.
    /// </summary>
    public class QueueSpawner : IInitializable, IDisposable
    {
        // World units between consecutive rows within one lane.
        private const float RowSpacing = 1.2f;

        private readonly ConveyorLevelDataSO _level;
        private readonly QueueAnchor _queueAnchor;
        private readonly FoodItemController.Pool _foodPool;

        private readonly List<FoodItemController> _spawned = new();

        public QueueSpawner(ConveyorLevelDataSO level, QueueAnchor queueAnchor, FoodItemController.Pool foodPool)
        {
            _level = level;
            _queueAnchor = queueAnchor;
            _foodPool = foodPool;
        }

        public void Initialize()
        {
            int laneCount = Mathf.Min(_level.Queues.Count, _queueAnchor.LanePositions.Length);
            for (int lane = 0; lane < laneCount; lane++)
            {
                var lanePosition = _queueAnchor.LanePositions[lane];
                var entries = _level.Queues[lane].Entries;

                for (int row = 0; row < entries.Count; row++)
                {
                    var position = lanePosition + new Vector3(0f, 0f, -row * RowSpacing);
                    _spawned.Add(_foodPool.Spawn(position, entries[row]));
                }
            }
        }

        public void Dispose()
        {
            foreach (var item in _spawned) _foodPool.Despawn(item);
            _spawned.Clear();
        }
    }
}
