using UnityEngine;
using Zenject;

namespace RestaurantLoop.Runtime.Conveyor
{
    // Once the queue is empty and everything left fits on the exit stack, the level can only be won: speeds
    // the belt up and loops items back to the entry instead of docking them. Stack occupants stay put.
    public class ConveyorAutoClearService : ITickable
    {
        private const float LoopSpeedMultiplier = 2f;

        private readonly QueueAnchor _queueAnchor;
        private readonly QueueSpawner _queueSpawner;
        private readonly ConveyorManager _conveyorManager;
        private readonly ExitStackAnchor _exitStackAnchor;

        private bool _activated;

        // Keeps a failed-then-resumed level from being "rescued" into the guaranteed-win loop.
        public void Disable() => _activated = true;

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

            _conveyorManager.SpeedMultiplier = LoopSpeedMultiplier;
            _conveyorManager.EnableLoopMode();
        }
    }
}
