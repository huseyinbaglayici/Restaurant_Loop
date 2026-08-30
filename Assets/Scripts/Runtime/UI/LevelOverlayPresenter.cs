using System;
using RestaurantLoop.Economy;
using RestaurantLoop.SceneFlow;
using TMPro;
using Zenject;

namespace RestaurantLoop.Runtime.UI
{
    public class LevelOverlayPresenter : IInitializable, IDisposable
    {
        private readonly CurrentLevelProvider _currentLevelProvider;
        private readonly TextMeshProUGUI _levelText;
        private readonly TextMeshProUGUI _moneyText;
        private readonly IEconomyService _economyService;

        public LevelOverlayPresenter(
            CurrentLevelProvider currentLevelProvider,
            TextMeshProUGUI levelText,
            [Inject(Id = "GameplayMoneyText", Optional = true)] TextMeshProUGUI moneyText = null,
            [Inject(Optional = true)] IEconomyService economyService = null)
        {
            _currentLevelProvider = currentLevelProvider;
            _levelText = levelText;
            _moneyText = moneyText;
            _economyService = economyService;
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
        }

        private void OnCoinsChanged(int coins) => UpdateCoins(coins);

        private void UpdateCoins(int coins)
        {
            if (_moneyText != null)
            {
                _moneyText.text = coins.ToString();
            }
        }

        public void Dispose()
        {
            if (_economyService != null)
            {
                _economyService.OnCoinsChanged -= OnCoinsChanged;
            }
        }
    }
}
