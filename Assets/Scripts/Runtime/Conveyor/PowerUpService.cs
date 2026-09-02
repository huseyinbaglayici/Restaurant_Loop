namespace RestaurantLoop.Runtime.Conveyor
{
    // Manual-call power-up entry points. No UI/cooldown wiring yet — call these directly.
    public class PowerUpService
    {
        private readonly QueueSpawner _queueSpawner;
        private readonly ExitStackAnchor _exitStackAnchor;
        private readonly TrayDispenserAnchor _trayDispenserAnchor;
        private readonly CameraController _cameraController;

        public PowerUpService(
            QueueSpawner queueSpawner,
            ExitStackAnchor exitStackAnchor,
            TrayDispenserAnchor trayDispenserAnchor,
            [Zenject.Inject(Optional = true)] CameraController cameraController = null)
        {
            _queueSpawner = queueSpawner;
            _exitStackAnchor = exitStackAnchor;
            _trayDispenserAnchor = trayDispenserAnchor;
            _cameraController = cameraController;
        }

        public void ActivateShuffle() => _queueSpawner.ShuffleLanes();

        public void ActivatePickFromBottom()
        {
            _queueSpawner.PickFromBottomEnabled = true;
            _cameraController?.FocusOnQueue(true);
        }

        public bool CanActivateExtraTray() => _exitStackAnchor != null && _exitStackAnchor.CanAddExtraSlot;

        // Adds an extra exit stack slot and adds an extra tray to the tray dispenser pool (up to max 1).
        public bool ActivateExtraTray()
        {
            bool addedSlot = _exitStackAnchor.TryAddExtraSlot();
            if (addedSlot && _trayDispenserAnchor != null)
            {
                _trayDispenserAnchor.TryAddExtraTray();
            }
            return addedSlot;
        }
    }
}
