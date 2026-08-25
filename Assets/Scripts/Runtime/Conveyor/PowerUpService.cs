namespace RestaurantLoop.Runtime.Conveyor
{
    // Manual-call power-up entry points. No UI/cooldown wiring yet — call these directly.
    public class PowerUpService
    {
        private readonly QueueSpawner _queueSpawner;
        private readonly ExitStackAnchor _exitStackAnchor;

        public PowerUpService(QueueSpawner queueSpawner, ExitStackAnchor exitStackAnchor)
        {
            _queueSpawner = queueSpawner;
            _exitStackAnchor = exitStackAnchor;
        }

        public void ActivateShuffle() => _queueSpawner.ShuffleLanes();

        public void ActivatePickFromBottom() => _queueSpawner.PickFromBottomEnabled = true;

        // Repeatable — every call adds another slot.
        public bool ActivateExtraTray() => _exitStackAnchor.TryAddExtraSlot();
    }
}
