using System;
using RestaurantLoop.Runtime.Economy;
using RestaurantLoop.Runtime.Conveyor;
using RestaurantLoop.Runtime.SceneFlow;
using RestaurantLoop.Runtime.Signals;
using TMPro;
using UnityEngine;
using Zenject;

namespace RestaurantLoop.Runtime.UI
{
    public class LevelOverlayPresenter : IInitializable, IDisposable
    {
        private readonly CurrentLevelProvider _currentLevelProvider;
        private readonly TextMeshProUGUI _levelText;
        private readonly TextMeshProUGUI _moneyText;
        private readonly EconomyService _economyService;
        private readonly CustomerConsumeService _consumeService;
        private readonly SignalBus _signalBus;

        private readonly GameObject _topHolder;

        public LevelOverlayPresenter(
            CurrentLevelProvider currentLevelProvider,
            TextMeshProUGUI levelText,
            [Inject(Id = "GameplayMoneyText")] TextMeshProUGUI moneyText,
            [Inject(Id = "TopLevelHolder")] Transform topLevelHolder,
            EconomyService economyService,
            CustomerConsumeService consumeService,
            SignalBus signalBus)
        {
            _currentLevelProvider = currentLevelProvider;
            _levelText = levelText;
            _moneyText = moneyText;
            _topHolder = topLevelHolder.gameObject;
            _economyService = economyService;
            _consumeService = consumeService;
            _signalBus = signalBus;
        }

        public void Initialize()
        {
            if (_levelText != null && _currentLevelProvider != null && _currentLevelProvider.Current != null)
            {
                _levelText.text = $"LEVEL {_currentLevelProvider.Current.LevelId}";
            }

            if (_moneyText != null && _economyService != null)
            {
                _economyService.OnCoinsChanged += OnCoinsChanged;
                UpdateCoins(_economyService.Coins);
            }

            if (_consumeService != null)
            {
                _consumeService.OnLevelEnded += HandleLevelEnded;
            }

            _signalBus?.Subscribe<LevelResumedSignal>(HandleLevelResumed);
        }

        private void OnCoinsChanged(int coins) => UpdateCoins(coins);

        private void UpdateCoins(int coins)
        {
            if (_moneyText != null)
            {
                _moneyText.text = coins.ToString();
            }
        }

        private void HandleLevelEnded(bool won)
        {
            SetVisible(false);
        }

        private void HandleLevelResumed()
        {
            SetVisible(true);
        }

        private void SetVisible(bool visible) => _topHolder.SetActive(visible);

        public void Dispose()
        {
            if (_economyService != null)
            {
                _economyService.OnCoinsChanged -= OnCoinsChanged;
            }

            if (_consumeService != null)
            {
                _consumeService.OnLevelEnded -= HandleLevelEnded;
            }

            _signalBus?.Unsubscribe<LevelResumedSignal>(HandleLevelResumed);
        }
    }
}
