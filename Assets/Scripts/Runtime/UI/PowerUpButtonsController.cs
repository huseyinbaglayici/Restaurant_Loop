using System.Collections.Generic;
using RestaurantLoop.Runtime.Audio;
using RestaurantLoop.Runtime.Conveyor;
using RestaurantLoop.Runtime.Data;
using RestaurantLoop.Runtime.Economy;
using RestaurantLoop.Runtime.Signals;
using RestaurantLoop.Runtime.Tutorial;
using UnityEngine;
using UnityEngine.UI;
using Zenject;

namespace RestaurantLoop.Runtime.UI
{
    // Bottom power-up bar: wires the three buttons to PowerUpPurchaseService and keeps their slot visuals fresh.
    public class PowerUpButtonsController : MonoBehaviour
    {
        [SerializeField] private Button _buttonShuffle;
        [SerializeField] private Button _buttonPickFromBottom;
        [SerializeField] private Button _buttonExtraTray;

        private readonly List<PowerUpSlotView> _slots = new();

        private PowerUpPurchaseService _purchaseService;
        private TutorialTargetRegistry _registry;
        private PowerUpInventoryService _inventory;
        private EconomyService _economyService;
        private CustomerConsumeService _consumeService;
        private SignalBus _signalBus;
        private PowerUpSettings _settings;
        private AudioService _audioService;
        private AudioLibrary _audioLibrary;

        [Inject]
        public void Construct(
            PowerUpPurchaseService purchaseService,
            TutorialTargetRegistry registry,
            PowerUpInventoryService inventory,
            EconomyService economyService,
            CustomerConsumeService consumeService,
            SignalBus signalBus,
            PowerUpSettings settings,
            AudioService audioService,
            AudioLibrary audioLibrary)
        {
            _purchaseService = purchaseService;
            _registry = registry;
            _inventory = inventory;
            _economyService = economyService;
            _consumeService = consumeService;
            _signalBus = signalBus;
            _settings = settings;
            _audioService = audioService;
            _audioLibrary = audioLibrary;

            RegisterTutorialTargets();
        }

        private void OnEnable()
        {
            RegisterTutorialTargets();
        }

        private void Start()
        {
            RegisterTutorialTargets();

            AddSlot(PowerUpButtonId.Shuffle, _buttonShuffle);
            AddSlot(PowerUpButtonId.PickFromBottom, _buttonPickFromBottom);
            AddSlot(PowerUpButtonId.ExtraTray, _buttonExtraTray);

            _consumeService.OnLevelEnded += HandleLevelEnded;
            _inventory.OnInventoryChanged += RefreshAllSlots;
            _economyService.OnCoinsChanged += HandleCoinsChanged;
            _signalBus.Subscribe<LevelResumedSignal>(HandleLevelResumed);

            RefreshAllSlots();
        }

        private void OnDestroy()
        {
            if (_consumeService == null) return;

            _consumeService.OnLevelEnded -= HandleLevelEnded;
            _inventory.OnInventoryChanged -= RefreshAllSlots;
            _economyService.OnCoinsChanged -= HandleCoinsChanged;
            _signalBus.TryUnsubscribe<LevelResumedSignal>(HandleLevelResumed);
        }

        public void RefreshAllSlots()
        {
            foreach (var slot in _slots) RefreshSlot(slot);
        }

        private void AddSlot(PowerUpButtonId id, Button button)
        {
            if (button == null) return;

            var slot = new PowerUpSlotView(id, button, _settings);
            button.onClick.AddListener(() => OnSlotClicked(slot));
            _slots.Add(slot);
        }

        // The tutorial highlights buttons by id; re-registered on enable since the bar is toggled per level.
        private void RegisterTutorialTargets()
        {
            if (_registry == null) return;

            RegisterTarget(PowerUpButtonId.Shuffle, _buttonShuffle);
            RegisterTarget(PowerUpButtonId.PickFromBottom, _buttonPickFromBottom);
            RegisterTarget(PowerUpButtonId.ExtraTray, _buttonExtraTray);
        }

        private void RegisterTarget(PowerUpButtonId id, Button button)
        {
            if (button != null) _registry.Register(id, (RectTransform)button.transform);
        }

        private void OnSlotClicked(PowerUpSlotView slot)
        {
            var id = slot.Id;
            if (!_purchaseService.IsUsable(id)) return;

            if (_purchaseService.NeedsPurchase(id))
            {
                if (!_purchaseService.TryBuy(id))
                {
                    PlayNotEnoughCoinsFeedback(slot);
                    return;
                }
                _audioService.PlaySfx(_audioLibrary.FoodSendSfx);
            }

            if (_purchaseService.TryActivate(id)) RefreshSlot(slot);
        }

        private void RefreshSlot(PowerUpSlotView slot)
        {
            var id = slot.Id;
            slot.Refresh(
                _purchaseService.IsUnlocked(id),
                _purchaseService.CanUseEffect(id),
                _purchaseService.GetCount(id),
                _purchaseService.GetPrice(id));
        }

        private void PlayNotEnoughCoinsFeedback(PowerUpSlotView slot)
        {
            _audioService.PlaySfx(_audioLibrary.NotEnoughCoinsSfx != null ? _audioLibrary.NotEnoughCoinsSfx : _audioLibrary.FailSfx);
            slot.PlayNotEnoughCoins();
        }

        private void HandleCoinsChanged(int coins) => RefreshAllSlots();

        private void HandleLevelEnded(bool won)
        {
            gameObject.SetActive(false);
        }

        private void HandleLevelResumed()
        {
            gameObject.SetActive(true);
            RefreshAllSlots();
        }
    }
}
