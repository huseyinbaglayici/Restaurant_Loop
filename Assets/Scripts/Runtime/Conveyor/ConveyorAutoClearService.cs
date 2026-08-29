using UnityEngine;
using Zenject;

namespace RestaurantLoop.Runtime.Conveyor
{
    // Once the queue is fully spent and everything left (belt + exit stack) is guaranteed to fit back on
    // the exit stack, the level can't be lost anymore — only won. From that point on, waiting for more
    // player clicks just stalls the win, so this switches the belt into an auto-clear loop: every exit
    // stack occupant gets fed back onto the belt, belt speed doubles, and ConveyorManager stops docking
    // items at the end of the belt and instead loops them back to the entry until every customer is served.
    public class ConveyorAutoClearService : ITickable
    {
        private const float LoopSpeedMultiplier = 2f;

        private readonly QueueAnchor _queueAnchor;
        private readonly QueueSpawner _queueSpawner;
        private readonly ConveyorManager _conveyorManager;
        private readonly ExitStackAnchor _exitStackAnchor;

        private bool _activated;

        public ConveyorAutoClearService(
            QueueAnchor queueAnchor,
            QueueSpawner queueSpawner,
            ConveyorManager conveyorManager,
            ExitStackAnchor exitStackAnchor)
        {
            _queueAnchor = queueAnchor;
            _queueSpawner = queueSpawner;
            _conveyorManager = conveyorManager;
            _exitStackAnchor = exitStackAnchor;
        }

        public void Tick()
        {
            if (_activated) return;
            if (!IsQueueEmpty()) return;

            int remaining = _conveyorManager.ReservedItemCount + _exitStackAnchor.OccupiedCount;
            if (remaining > _exitStackAnchor.SlotCount) return;

            Activate();
        }

        private bool IsQueueEmpty()
        {
            for (int lane = 0; lane < _queueAnchor.LanePositions.Length; lane++)
                if (_queueSpawner.GetLaneCount(lane) > 0) return false;

            return true;
        }

        private void Activate()
        {
            _activated = true;

            for (int i = 0; i < _exitStackAnchor.SlotPositions.Length; i++)
            {
                if (!_exitStackAnchor.TryRemoveFromSlot(i, out var visual)) continue;
                _conveyorManager.AddItemAtEntry(visual.Stack, visual, visual.transform.position);
            }

            _conveyorManager.SpeedMultiplier = LoopSpeedMultiplier;
            _conveyorManager.EnableLoopMode();
        }
    }
}
