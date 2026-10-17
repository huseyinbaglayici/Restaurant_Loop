using RestaurantLoop.Runtime.Conveyor;

namespace RestaurantLoop.Runtime.UI
{
    // Returns every food item still on the belt or in the exit stack to the pool before leaving a won level.
    public class LevelBoardCleanup
    {
        private readonly ConveyorManager _conveyorManager;
        private readonly ExitStackAnchor _exitStackAnchor;
        private readonly FoodItemController.Pool _foodPool;

        public LevelBoardCleanup(
            ConveyorManager conveyorManager,
            ExitStackAnchor exitStackAnchor,
            FoodItemController.Pool foodPool)
        {
            _conveyorManager = conveyorManager;
            _exitStackAnchor = exitStackAnchor;
            _foodPool = foodPool;
        }

        public void ClearBeltAndExitStack()
        {
            _conveyorManager.ClearBelt();

            for (int i = 0; i < _exitStackAnchor.SlotCount; i++)
            {
                if (!_exitStackAnchor.TryRemoveFromSlot(i, out var visual)) continue;
                if (visual != null) _foodPool.Despawn(visual);
            }
        }
    }
}
