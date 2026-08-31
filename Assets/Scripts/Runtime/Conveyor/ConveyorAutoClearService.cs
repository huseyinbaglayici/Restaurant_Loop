using UnityEngine;
using Zenject;

namespace RestaurantLoop.Runtime.Conveyor
{
    // Once the queue is fully spent and everything left (belt + exit stack) is guaranteed to fit back on
    // the exit stack, the level can't be lost anymore — only won. From that point on this just speeds the
    // belt up and stops it docking items (loops them back to the entry instead) so nothing ever piles into
    // the exit stack again; the stack's existing occupants are left exactly where they are — the player
    // still clicks them back onto the belt themselves, same as any other exit-stack item.
    public class ConveyorAutoClearService : ITickable
    {
        private const float LoopSpeedMultiplier = 2f;

        private readonly QueueAnchor _queueAnchor;
        private readonly QueueSpawner _queueSpawner;
        private readonly ConveyorManager _conveyorManager;
        private readonly ExitStackAnchor _exitStackAnchor;

        private bool _activated;

        // Called once the level has failed and Keep Playing is used — a deadlocked stack shouldn't be
        // silently "rescued" into the guaranteed-win loop just because clearing the belt reset
        // ReservedItemCount to 0 and made the remaining count look like it fits.
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
