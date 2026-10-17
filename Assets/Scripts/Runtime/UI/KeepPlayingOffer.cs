using RestaurantLoop.Runtime.Conveyor;
using RestaurantLoop.Runtime.Economy;
using RestaurantLoop.Runtime.Lives;

namespace RestaurantLoop.Runtime.UI
{
    // The "Keep Playing" offer after a fail: pay coins to continue with an extra exit-stack slot, or decline and lose a life.
    public class KeepPlayingOffer
    {
        public const int Cost = 300;

        private readonly EconomyService _economyService;
        private readonly LivesService _livesService;
        private readonly PowerUpService _powerUpService;

        public KeepPlayingOffer(
            EconomyService economyService,
            LivesService livesService,
            PowerUpService powerUpService)
        {
            _economyService = economyService;
            _livesService = livesService;
            _powerUpService = powerUpService;
        }

        // The extra slot is the whole offer — once it's used up, a fail goes straight to Level Failed.
        public bool IsAvailable => _powerUpService.CanActivateExtraTray();

        // False when the offer isn't available or the player can't afford it; nothing changes on the board then.
        public bool TryAccept()
        {
            if (!IsAvailable) return false;

            bool infinite = PowerUpInventoryService.IsInfinitePowerUpsEnabled();
            if (!infinite && !_economyService.TrySpendCoins(Cost)) return false;

            // Belt and hovering food stay in play — every level's supply exactly matches demand, so clearing
            // them would make it unwinnable. The new slot is what makes room (an item held at the belt end
            // docks into it right away).
            _powerUpService.ActivateExtraTray();
            return true;
        }

        public void Decline() => _livesService.ConsumeLife();
    }
}
