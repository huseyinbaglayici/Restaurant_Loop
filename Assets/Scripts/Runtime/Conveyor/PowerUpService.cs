using RestaurantLoop.Runtime.Audio;
using RestaurantLoop.Runtime.Data;
using RestaurantLoop.Runtime.Effects;
using UnityEngine;

namespace RestaurantLoop.Runtime.Conveyor
{
    // Manual-call power-up entry points. No UI/cooldown wiring yet — call these directly.
    public class PowerUpService
    {
        private readonly QueueSpawner _queueSpawner;
        private readonly ExitStackAnchor _exitStackAnchor;
        private readonly TrayDispenserAnchor _trayDispenserAnchor;
        private readonly CameraController _cameraController;
        private readonly QueuePowerUpAnimator _queueAnimator;
        private readonly FxService _fxService;
        private readonly AudioService _audioService;
        private readonly AudioLibrary _audioLibrary;

        public PowerUpService(
            QueueSpawner queueSpawner,
            ExitStackAnchor exitStackAnchor,
            TrayDispenserAnchor trayDispenserAnchor,
            CameraController cameraController,
            QueuePowerUpAnimator queueAnimator,
            FxService fxService,
            AudioService audioService,
            AudioLibrary audioLibrary)
        {
            _queueSpawner = queueSpawner;
            _exitStackAnchor = exitStackAnchor;
            _trayDispenserAnchor = trayDispenserAnchor;
            _cameraController = cameraController;
            _queueAnimator = queueAnimator;
            _fxService = fxService;
            _audioService = audioService;
            _audioLibrary = audioLibrary;
        }

        // Fewer than two queue items: a shuffle can't change anything.
        public bool CanActivateShuffle() => _queueSpawner.TotalCount >= 2 && !_queueSpawner.IsAnimating;

        public bool CanActivatePickFromBottom() => !_queueSpawner.PickFromBottomEnabled;

        public void ActivateShuffle() => _queueAnimator.PlayShuffle();

        public void ActivatePickFromBottom()
        {
            _queueSpawner.PickFromBottomEnabled = true;
            _cameraController.FocusOnQueue(true);
            _queueAnimator.PlayPickFromBottomRipple();
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

            if (addedSlot)
            {
                _fxService.Play(FxType.SlotAppear, _exitStackAnchor.SlotPositions[^1] + Vector3.up * 0.4f);
                _cameraController.Shake(0.06f, 0.2f);
                if (_audioLibrary.PowerUpSfx != null) _audioService.PlaySfx(_audioLibrary.PowerUpSfx);
            }
            return addedSlot;
        }
    }
}
