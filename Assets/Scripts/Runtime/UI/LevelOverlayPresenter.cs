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

        private GameObject _topHolder;

        public LevelOverlayPresenter(
            CurrentLevelProvider currentLevelProvider,
            TextMeshProUGUI levelText,
            [Inject(Id = "GameplayMoneyText", Optional = true)] TextMeshProUGUI moneyText,
            EconomyService economyService,
            [Inject(Optional = true)] CustomerConsumeService consumeService,
            SignalBus signalBus)
        {
            _currentLevelProvider = currentLevelProvider;
            _levelText = levelText;
            _moneyText = moneyText;
            _economyService = economyService;
            _consumeService = consumeService;
            _signalBus = signalBus;
        }

        public void Initialize()
        {
            _topHolder = ResolveTopHolder();

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

        private void SetVisible(bool visible)
        {
            if (_topHolder != null)
            {
                _topHolder.SetActive(visible);
            }
            else
            {
                if (_levelText != null) _levelText.gameObject.SetActive(visible);
                if (_moneyText != null) _moneyText.gameObject.SetActive(visible);
            }
        }

        private GameObject ResolveTopHolder()
        {
            if (_levelText != null)
            {
                Transform curr = _levelText.transform;
                while (curr != null && curr.parent != null && curr.parent.GetComponent<Canvas>() == null)
                {
                    if (curr.gameObject.name.Equals("holder_TopLevel", StringComparison.OrdinalIgnoreCase))
                        return curr.gameObject;
                    curr = curr.parent;
                }
                if (curr != null && curr.gameObject.name.Equals("holder_TopLevel", StringComparison.OrdinalIgnoreCase))
                    return curr.gameObject;
                if (curr != null && curr != _levelText.transform)
                    return curr.gameObject;
            }

            var found = GameObject.Find("holder_TopLevel");
            if (found != null) return found;

            return null;
        }

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
