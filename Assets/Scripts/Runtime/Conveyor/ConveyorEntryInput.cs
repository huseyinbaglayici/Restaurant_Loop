using Zenject;

namespace RestaurantLoop.Runtime.Conveyor
{
    // Routes a click to the queue first; if the queue doesn't take it, the exit stack gets a try.
    public class ConveyorEntryInput : ITickable
    {
        private readonly ConveyorPointer _pointer;
        private readonly QueuePickHandler _queuePick;
        private readonly ExitStackPickHandler _exitStackPick;

        public ConveyorEntryInput(ConveyorPointer pointer, QueuePickHandler queuePick, ExitStackPickHandler exitStackPick)
        {
            _pointer = pointer;
            _queuePick = queuePick;
            _exitStackPick = exitStackPick;
        }

        public void Tick()
        {
            if (!_pointer.TryGetClick(out var camera, out var screenPoint)) return;

            if (_queuePick.TryHandleClick(camera, screenPoint)) return;
            _exitStackPick.TryHandleClick(camera, screenPoint);
        }
    }
}
