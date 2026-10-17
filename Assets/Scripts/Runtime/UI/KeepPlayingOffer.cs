using RestaurantLoop.Runtime.Conveyor;
using RestaurantLoop.Runtime.Economy;
using RestaurantLoop.Runtime.Lives;

namespace RestaurantLoop.Runtime.UI
{
    // The "Keep Playing" offer after a fail: pay coins to continue with a clear belt and an extra tray, or decline and lose a life.
    public class KeepPlayingOffer
    {
        public const int Cost = 300;

        private readonly EconomyService _economyService;
        private readonly LivesService _livesService;
        private readonly ConveyorManager _conveyorManager;
        private readonly PowerUpService _powerUpService;
        private readonly ConveyorAutoClearService _autoClearService;

        public KeepPlayingOffer(
            EconomyService economyService,
            LivesService livesService,
            ConveyorManager conveyorManager,
            PowerUpService powerUpService,
            ConveyorAutoClearService autoClearService)
        {
            _economyService = economyService;
            _livesService = livesService;
            _conveyorManager = conveyorManager;
            _powerUpService = powerUpService;
            _autoClearService = autoClearService;
        }

        // False when the player can't afford it; nothing changes on the board then.
        public bool TryAccept()
        {
            bool infinite = PowerUpInventoryService.IsInfinitePowerUpsEnabled();
            if (!infinite && !_economyService.TrySpendCoins(Cost)) return false;

            _conveyorManager.ClearBelt();
            _powerUpService.ActivateExtraTray(); // Extra Tray power-up: adds another slot each time Keep Playing is used.

            // ClearBelt() zeroes ReservedItemCount, which can make ConveyorAutoClearService think the
            // (still deadlocked) exit stack now "fits" and guarantees a win — yanking every occupant back
            // onto the belt right as the player resumes. The level already failed once; don't let it be
            // silently auto-solved out from under a manual Keep Playing continuation.
            _autoClearService.Disable();
            return true;
        }

        public void Decline() => _livesService.ConsumeLife();
    }
}
