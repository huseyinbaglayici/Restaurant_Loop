using RestaurantLoop.Runtime.Conveyor;
using RestaurantLoop.Runtime.Data;
using RestaurantLoop.Runtime.Economy;
using RestaurantLoop.Runtime.SceneFlow;
using RestaurantLoop.Runtime.Signals;
using RestaurantLoop.Runtime.Tutorial;
using Zenject;

namespace RestaurantLoop.Runtime.UI
{
    // Power-up rules behind the bottom buttons: unlock state, buying with coins, spending inventory, activating.
    public class PowerUpPurchaseService
    {
        private readonly PowerUpService _powerUpService;
        private readonly PowerUpInventoryService _inventory;
        private readonly EconomyService _economyService;
        private readonly PowerUpSettings _settings;
        private readonly CurrentLevelProvider _currentLevelProvider;
        private readonly TutorialInputGate _gate;
        private readonly SignalBus _signalBus;

        public PowerUpPurchaseService(
            PowerUpService powerUpService,
            PowerUpInventoryService inventory,
            EconomyService economyService,
            PowerUpSettings settings,
            CurrentLevelProvider currentLevelProvider,
            TutorialInputGate gate,
            SignalBus signalBus)
        {
            _powerUpService = powerUpService;
            _inventory = inventory;
            _economyService = economyService;
            _settings = settings;
            _currentLevelProvider = currentLevelProvider;
            _gate = gate;
            _signalBus = signalBus;
        }

        private int CurrentLevelId => _currentLevelProvider.Current != null ? _currentLevelProvider.Current.LevelId : 1;

        public int GetCount(PowerUpButtonId id) => _inventory.GetCount(id);

        public int GetPrice(PowerUpButtonId id) => _settings.GetGoldPrice(id);

        // Inventory already reports everything unlocked while the infinite power-ups debug toggle is on.
        public bool IsUnlocked(PowerUpButtonId id) => _inventory.IsUnlocked(id, CurrentLevelId);

        // Only Extra Tray can be blocked by the board itself (no free exit slot left).
        public bool CanUseEffect(PowerUpButtonId id) =>
            id != PowerUpButtonId.ExtraTray || _powerUpService.CanActivateExtraTray();

        // Checked on click only, so the button never greys out on a state no refresh event would undo.
        // Blocks a charge or coins being spent on a shuffle that changes nothing or a second Pick From Bottom.
        private bool WouldHaveEffectNow(PowerUpButtonId id) => id switch
        {
            PowerUpButtonId.Shuffle => _powerUpService.CanActivateShuffle(),
            PowerUpButtonId.PickFromBottom => _powerUpService.CanActivatePickFromBottom(),
            _ => true
        };

        // A tutorial step waiting on this button gets it for free, so the step can never soft-lock.
        private bool IsTutorialPress(PowerUpButtonId id) => _gate.IsPowerUpRequired(id);

        public bool IsUsable(PowerUpButtonId id) =>
            IsUnlocked(id) && _gate.CanClickPowerUpButton(id) && CanUseEffect(id) &&
            (IsTutorialPress(id) || WouldHaveEffectNow(id));

        public bool NeedsPurchase(PowerUpButtonId id) =>
            !IsTutorialPress(id) && GetCount(id) <= 0 && !PowerUpInventoryService.IsInfinitePowerUpsEnabled();

        // Buys one charge into the inventory. False when the player can't afford it.
        public bool TryBuy(PowerUpButtonId id)
        {
            if (!_economyService.TrySpendCoins(GetPrice(id))) return false;

            _inventory.AddCount(id, 1);
            return true;
        }

        // Spends one charge and runs the effect. Extra Tray only spends if a slot was actually added.
        // A tutorial-required press spends nothing.
        public bool TryActivate(PowerUpButtonId id)
        {
            bool isFree = IsTutorialPress(id);
            if (!isFree && !WouldHaveEffectNow(id)) return false;

            if (id == PowerUpButtonId.ExtraTray)
            {
                if (!_powerUpService.ActivateExtraTray()) return false;
                if (!isFree) _inventory.TryUse(id);
            }
            else
            {
                if (!isFree && !_inventory.TryUse(id)) return false;

                if (id == PowerUpButtonId.Shuffle) _powerUpService.ActivateShuffle();
                else _powerUpService.ActivatePickFromBottom();
            }

            _signalBus.Fire(new TutorialActionPerformedSignal { Kind = TutorialHoverTargetKind.PowerUpButton, PowerUpButton = id });
            return true;
        }
    }
}
